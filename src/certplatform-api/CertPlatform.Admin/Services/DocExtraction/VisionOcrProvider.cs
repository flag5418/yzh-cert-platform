using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CertPlatform.Shared.DocExtraction;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SqlSugar;
using YZH.Core.DataBase.Interfaces;

namespace CertPlatform.Admin.Services.DocExtraction
{
    /// <summary>
    /// ★ <b>视觉识别 Provider</b>（2026-10-03 真正落地 —— 此前是恒 <c>IsAvailable =&gt; false</c> 的空壳）
    ///
    /// <para><b>在链路中的位置（36 号 §5.1，2026-10-03 用户确认的顺序：先判断能不能解析，不能才用 AI）</b>：</para>
    /// <code>
    /// 任意文件
    ///   ├─ 有文本层（Word / 文本 PDF / txt / md）→ anydoc 直接出 Markdown        ← 第一选择，零 AI
    ///   └─ 无文本层（图片 / 扫描件 PDF）
    ///        └─ 本 Provider（视觉模型）→ Markdown                            ← 只在真需要时才付 AI
    /// </code>
    ///
    /// <para><b>⛔ 绝不能反过来做</b>（先扔 AI、再看对不对）：文本 PDF 走 AI 会
    /// <b>慢一个数量级、贵一个数量级</b>，而 anydoc 对它是秒级且免费。
    /// 判定顺序由 <c>FileConvertCore.ConvertMarkdownInnerAsync</c> 的分支 A
    /// （<c>result.NeedsOcr</c>，anydoc 退出码 3）决定 —— <b>本类只在那一个分支被调用</b>。</para>
    ///
    /// <para><b>配置来源</b>：系统参数 <c>ai_vision_config</c>（一条 JSON，见 <see cref="VisionSettings"/>）。
    /// ⛔ <b>不复用 <c>ai_model_name</c></b> —— 否则提取主流程会全线按视觉模型计价。
    /// 参数里 <c>BaseUrl</c>/<c>ApiKey</c> 留空时自动回填文本模型那两个
    /// （同厂商时不必重复配；切到别的厂商才需在 JSON 里显式覆盖）。</para>
    ///
    /// <para><b>⛔ 为什么本类在 Admin 而不在 Shared</b>：读系统参数需要 <c>IDbOrm</c>，
    /// 而 <c>CertPlatform.Shared</c> <b>不引用</b> <c>YZH.Core.DataBase</c>（分层约束）。
    /// Shared 侧只留纯 DTO（<see cref="VisionSettings"/>）与字节工具（<see cref="ImagePreprocess"/>）。</para>
    ///
    /// <para><b>⚠️ 已知边界（诚实声明，不假装支持）</b>：</para>
    /// <list type="number">
    ///   <item><b>多页扫描 PDF 只识别第 1 页</b>：<c>soffice --convert-to png</c> 只能出首页
    ///         （2026-09-26 实测）。彻底解决需给 <c>yzh-libreoffice</c> 镜像加
    ///         <c>poppler-utils</c> 并逐页栅格化。</item>
    ///   <item>图片按 <b>token</b> 计费（实测 1520×1240 ≈ 1874 image_tokens）；
    ///         <c>ImageMaxEdge</c> 钩子已留，但<b>缩放库未接</b>（容器内无 libgdiplus / 未引 ImageSharp），
    ///         当前原图下发。</item>
    /// </list>
    /// </summary>
    public class VisionOcrProvider : IOcrProvider
    {
        private readonly LlmInvokeService _llm;
        // ⚠️ 必须用 IServiceScopeFactory 而不是直接注入 IDbOrm：
        //    本 Provider 注册为 **Singleton**（随 FileConvertCore 单例一起常驻），
        //    而 IDbOrm 是 **Scoped** ⇒ 直接注入会让容器启动即抛
        //    「Cannot consume scoped service from singleton」（2026-10-03 实测踩到，整个后端起不来）。
        //    IServiceScopeFactory 正是为「单例消费 scoped」设计的。
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<VisionOcrProvider> _logger;

        public VisionOcrProvider(
            LlmInvokeService llm,
            IServiceScopeFactory scopeFactory,
            ILogger<VisionOcrProvider> logger)
        {
            _llm = llm;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        /// <summary>
        /// 是否可用：视觉配置启用 **且** 模型看起来是视觉模型。
        ///
        /// <para><b>⚠️ 为什么要卡「像视觉模型」这道闸</b>（2026-10-03 实测）：纯文本模型
        /// （<c>qwen-flash</c>）收到 <c>image_url</c> 会<b>静默忽略</b> ——
        /// HTTP 200 + 正常回答，但图片根本没进模型（它回答「请提供图片」）。
        /// 不卡这道闸 ⇒ 用户以为「已经识别过了」而实际拿到空内容。</para>
        /// </summary>
        public bool IsAvailable
        {
            get
            {
                var v = LoadSettings();
                if (!v.Enabled) return false;
                if (!v.LooksLikeVisionModel)
                {
                    _logger.LogWarning(
                        "[视觉] 模型 {Model} 不含 vl/vision/omni 关键字 ⇒ 已禁用视觉识别。"
                        + "纯文本模型会**静默忽略**图片（实测 qwen-flash 答「请提供图片」）。",
                        v.Model);
                    return false;
                }
                return true;
            }
        }

        /// <summary>
        /// ★ PDF 逐页 OCR 的渲染参数（页数上限 / DPI），取自同一行 <c>ai_vision_config</c>
        /// （2026-10-09）。<c>FileConvertCore</c> 渲染前会读它 —— 故即便 <see cref="IsAvailable"/>
        /// 为 false，本属性也必须能安全返回（<see cref="PdfOcrOptions.Normalized"/> 兜住 0/负数）。
        /// </summary>
        public PdfOcrOptions PdfOptions
        {
            get
            {
                var v = LoadSettings();
                return new PdfOcrOptions(v.OcrMaxPages, v.OcrRenderDpi).Normalized();
            }
        }

        public async Task<OcrResult> ToMarkdownAsync(string fileName, byte[] content)
        {
            if (content == null || content.Length == 0)
                return OcrResult.NotAvailable("文件内容为空");

            // ★ 2026-10-09 硬闸：PDF 字节不能直接送视觉模型（实测两种 MIME 均 400
            //   `The image format is illegal and cannot be opened`）。
            //   正确做法是先经 DocumentConvertClient.RenderPdfPagesAsync 逐页渲染成图片。
            //   ⛔ 这里必须明确报错而不是放行 —— 放行的症状是「视觉识别失败：AI 调用失败（400）」，
            //   看的人会去查模型名/密钥，真因却是「送错了东西」。
            if (ImagePreprocess.IsPdf(content))
                return OcrResult.NotAvailable(
                    "收到 PDF 字节：视觉模型不接受 PDF 直接识别，必须先逐页渲染成图片"
                    + "（见 FileConvertCore 的 PDF OCR 分支 / DocumentConvertClient.RenderPdfPagesAsync）");

            var v = LoadSettings();

            if (!v.Enabled)
                return OcrResult.NotAvailable("视觉识别已关闭（系统参数 ai_vision_config.Enabled = false）");

            if (!v.LooksLikeVisionModel)
                return OcrResult.NotAvailable(
                    $"配置的模型「{v.Model}」不是视觉模型，无法读取图片内容（纯文本模型会静默忽略图片）");

            if (content.Length > v.MaxImageBytes)
                return OcrResult.NotAvailable(
                    $"图片 {content.Length / 1024 / 1024} MB 超过上限 {v.MaxImageBytes / 1024 / 1024} MB，未做识别");

            try
            {
                var bytes = ImagePreprocess.ScaleDown(content, v.ImageMaxEdge, out var mime);

                var resp = await _llm.CompleteAsync(new LlmInvokeRequest
                {
                    BaseUrl = v.BaseUrl,
                    ApiKey = v.ApiKey,
                    Model = v.Model,
                    MaxTokens = v.MaxTokens,
                    Temperature = v.Temperature,
                    Prompt = v.DocumentParsePrompt,
                    // ⛔ 必须 false：文档解析要的是自然语言 Markdown，不是 JSON
                    ForceJson = false,
                    Images = new List<byte[]> { bytes },
                    ImageMimeType = mime,
                });

                _logger.LogInformation(
                    "[视觉] {File} → {Model}，{Ok}，{Ms}ms，{Pt}/{Ct} tokens，来源 {Src}",
                    fileName, v.Model, resp.Success, resp.DurationMs,
                    resp.PromptTokens, resp.CompletionTokens, v.Source);

                if (!resp.Success)
                    return OcrResult.NotAvailable($"视觉识别失败：{resp.Message}");

                var markdown = ImagePreprocess.StripCodeFence(resp.Content);
                if (string.IsNullOrWhiteSpace(markdown))
                    return OcrResult.NotAvailable("视觉模型未返回内容（图片可能是空白）");

                return OcrResult.Ok(markdown, Encoding.UTF8.GetBytes(markdown));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[视觉] 识别异常: {File}", fileName);
                return OcrResult.NotAvailable($"视觉识别异常：{ex.Message}");
            }
        }

        /// <summary>
        /// ★ 图片 / PDF「分类 + 作用 + 关键词」识别（§2 路径② 两层过滤的输入，2026-10-07）。
        ///
        /// <para><b>★ 与 <see cref="ToMarkdownAsync"/> 共用同一视觉模型 + 同一行 <c>ai_vision_config</c></b>：
        /// 底层核心能力不版本管理、一份配置服务所有文档；OCR 与「分类/作用」用同一模型，
        /// ⛔ 不拆第二行（否则会出现「这份用 A 模型、那份用 B 模型」的不一致）。</para>
        ///
        /// <para>读 <see cref="VisionSettings.DocPurposePrompt"/>（新增字段），<c>ForceJson=true</c> 让模型返回
        /// 结构化 <c>{kind,purpose,keywords,confidence}</c>。⛔ 铁律沿用（2026-10-03 实测）：
        /// 只让模型选 <c>kind</c> 固定枚举 + 自由文本 purpose/keywords，⛔ 不输出派生布尔。</para>
        ///
        /// <para>配置关闭 / 模型非视觉 / 超大图时返回 <see cref="DocPurposeJudge.NotAvailable"/>（不抛异常）。
        /// ⚠️ 按 §9.5 铁律：该能力是文档必要条件，返回 NotAvailable 时调用方应拒执（不降级 partial）。</para>
        /// </summary>
        public async Task<DocPurposeJudge> JudgeDocPurposeAsync(string fileName, byte[] content)
        {
            if (content == null || content.Length == 0)
                return DocPurposeJudge.NotAvailable("文件内容为空");

            // ★ 复用现有 LoadSettings()：同一行 ai_vision_config、同一视觉模型
            var v = LoadSettings();
            if (!v.Enabled)
                return DocPurposeJudge.NotAvailable("视觉识别已关闭（ai_vision_config.Enabled=false）");
            if (!v.LooksLikeVisionModel)
                return DocPurposeJudge.NotAvailable(
                    $"配置的模型「{v.Model}」不是视觉模型，无法读取图片内容（纯文本模型会静默忽略图片）");
            if (content.Length > v.MaxImageBytes)
                return DocPurposeJudge.NotAvailable(
                    $"图片 {content.Length / 1024 / 1024} MB 超过上限 {v.MaxImageBytes / 1024 / 1024} MB，未识别");

            try
            {
                // ★ 复用 OCR 的图片预处理（缩放 + 判 MIME），⛔ 不另写一份
                var bytes = ImagePreprocess.ScaleDown(content, v.ImageMaxEdge, out var mime);

                var resp = await _llm.CompleteAsync(new LlmInvokeRequest
                {
                    BaseUrl = v.BaseUrl,
                    ApiKey = v.ApiKey,
                    Model = v.Model,
                    MaxTokens = v.MaxTokens,
                    Temperature = v.Temperature,
                    Prompt = v.DocPurposePrompt,      // ★ 新增字段（与 OCR 同一行配置）
                    // ★ ForceJson=true：本能力要的是结构化 {kind,purpose,keywords,confidence}
                    ForceJson = true,
                    Images = new List<byte[]> { bytes },
                    ImageMimeType = mime,
                });

                _logger.LogInformation(
                    "[视觉·作用] {File} → {Model}，{Ok}，{Ms}ms，来源 {Src}",
                    fileName, v.Model, resp.Success, resp.DurationMs, v.Source);

                if (!resp.Success)
                    return DocPurposeJudge.NotAvailable($"识别失败：{resp.Message}");

                // ★ 只读 JSON，⛔ 不让模型派生布尔（沿用 2026-10-03 铁律：kind 全对、派生布尔全错）
                if (resp.Json?.RootElement.ValueKind != JsonValueKind.Object)
                    return DocPurposeJudge.NotAvailable("模型未返回可解析的 JSON");

                var root = resp.Json.RootElement;
                var kind = root.TryGetProperty("kind", out var k) ? (k.GetString() ?? "").Trim() : "";
                var purpose = root.TryGetProperty("purpose", out var p) ? (p.GetString() ?? "").Trim() : "";
                var keywords = new List<string>();
                if (root.TryGetProperty("keywords", out var kw) && kw.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in kw.EnumerateArray())
                    {
                        var w = (el.GetString() ?? "").Trim();
                        if (w.Length > 0) keywords.Add(w);
                    }
                }
                double conf = 0;
                if (root.TryGetProperty("confidence", out var c) &&
                    double.TryParse(c.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var cf))
                    conf = Math.Clamp(cf, 0.0, 1.0);

                return new DocPurposeJudge
                {
                    Success = true,
                    Kind = kind,
                    Purpose = purpose,
                    Keywords = keywords,
                    Confidence = conf,
                    Message = "",
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[视觉·作用] 识别异常: {File}", fileName);
                return DocPurposeJudge.NotAvailable($"识别异常：{ex.Message}");
            }
        }

        /// <summary>读 <c>ai_vision_config</c>；缺 BaseUrl/ApiKey 时用文本模型的 <c>ai_*</c> 回填</summary>
        private VisionSettings LoadSettings()
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IDbOrm>();

            var v = VisionSettings.Parse(VisionConfigReader.GetString(db, "ai_vision_config"));
            if (string.IsNullOrWhiteSpace(v.BaseUrl) || string.IsNullOrWhiteSpace(v.ApiKey))
                v.ResolveFrom(
                    VisionConfigReader.GetString(db, "ai_base_url"),
                    VisionConfigReader.GetString(db, "ai_api_key"));
            return v;
        }
    }

    /// <summary>
/// 系统参数读取（<c>cert_sys_config</c>）+ 60s 内存缓存（避免每次转换都查库）。
/// ⛔ 类名带 <c>Vision</c> 前缀：同命名空间已有实体类 <c>SysConfig</c>，同名会让
/// <c>DocExtractionRuleService.AI.cs</c> 里的泛型参数解析失败（CS0718）。
/// </summary>
    internal static class VisionConfigReader
    {
        private static readonly Dictionary<string, (DateTime At, string? Value)> Cache = new();
        private static readonly object Gate = new();
        private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

        public static string? GetString(IDbOrm db, string key)
        {
            lock (Gate)
            {
                if (Cache.TryGetValue(key, out var hit) && DateTime.Now - hit.At < Ttl)
                    return hit.Value;
            }

            string? value = null;
            try
            {
                value = db.Client.Ado.GetString(
                    "SELECT `ConfigValue` FROM `cert_sys_config` WHERE `ConfigKey` = @k LIMIT 1",
                    new SugarParameter("@k", key));
            }
            catch (Exception)
            {
                // 表不存在 / DB 未初始化 ⇒ 用默认值，不让配置问题炸掉转换链
                value = null;
            }

            lock (Gate)
            {
                Cache[key] = (DateTime.Now, value);
            }
            return value;
        }
    }
}
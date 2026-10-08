using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CertPlatform.Shared.DocExtraction
{
    /// <summary>
    /// ★ <b>视觉模型配置</b>（系统参数 <c>ai_vision_config</c> 的 JSON 形态）
    ///
    /// <para><b>为什么做成「一条 JSON 系统参数」而不是像文本那样六个 <c>ai_*</c> 键</b>
    /// （用户 2026-10-03 裁决）：</para>
    /// <list type="bullet">
    ///   <item>文本模型只有 <c>base_url / api_key / model / max_tokens / temperature</c> 五个维度，
    ///         拆成五个键没问题；</item>
    ///   <item>视觉/证件识别还要加 <b>提示词、厂商特有开关、超时、是否启用</b> ⇒ 键会膨胀到十几个，
    ///         再拆成十几个 <c>ai_vision_*</c> 键就彻底不可维护。</item>
    ///   <item><b>换厂商只改一条配置不改代码</b> —— 这是关键诉求：先用百炼，将来可能切到
    ///         agnes 或私有化部署，只改 JSON 值即可。</item>
    /// </list>
    ///
    /// <para><b>⚠️ 与现有 <c>ai_*</c> 六键的关系</b>：本参数<b>只管视觉</b>，
    /// 文本调用仍读原六键（<c>ai_model_name</c> 等），两条通道互不影响。
    /// 缺省时 <see cref="ResolveFrom"/> 会用 <c>ai_*</c> 回填（base_url / api_key），
    /// 保证「只加一条参数就能用」。</para>
    /// </summary>
    public class VisionSettings
    {
        /// <summary>是否启用视觉识别（关掉 ⇒ 图片/扫描件回落到「需人工填写」）</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>厂商标识（仅作日志与将来扩展用）：<c>qianwen</c> / <c>agnes</c> / <c>custom</c></summary>
        public string Provider { get; set; } = "qianwen";

        /// <summary>
        /// 模型名。⭐ <b>必须是视觉模型</b>（<c>qwen3-vl-flash</c> / <c>qwen-vl-max</c> …）。
        /// ⛔ <b>纯文本模型会静默忽略图片</b>：实测 <c>qwen-flash</c> 传 <c>image_url</c> 后
        /// HTTP 200 + 回答「请提供图片」，图片根本没进模型（2026-10-03 实测）。
        /// </summary>
        public string Model { get; set; } = "qwen3-vl-flash";

        /// <summary>OpenAI 兼容端点（不含 <c>/chat/completions</c>）</summary>
        public string BaseUrl { get; set; } = "https://dashscope.aliyuncs.com/compatible-mode/v1";

        /// <summary>★ 密钥（用户口径「jwt」）。<b>与文本模型共用同一把时留空即可</b>，见 <see cref="ResolveFrom"/>。</summary>
        public string ApiKey { get; set; } = "";

        public int TimeoutSeconds { get; set; } = 180;

        public int MaxTokens { get; set; } = 8192;

        public float Temperature { get; set; } = 0.1f;

        /// <summary>
        /// ★ <b>文档解析提示词</b>（图片 / 扫描件 → Markdown）。
        /// <para>Qwen3-VL 的官方推荐值就是 <c>qwenvl markdown</c>（另一个是 <c>qwenvl html</c>）。
        /// 2026-10-03 实测该值能稳定产出带表格结构的 Markdown。</para>
        /// <para>换模型时这一项最可能需要改（例如 Claude 用「Convert this page to markdown」）。</para>
        /// </summary>
        public string DocumentParsePrompt { get; set; } = "qwenvl markdown";

        /// <summary>
        /// ★ <b>图片语义识别提示词</b>（证件/证书判定，见 37 号）。
        /// <para><b>⚠️ 铁律（2026-10-03 实测踩到）</b>：⛔ <b>不要让模型输出派生布尔字段</b>。
        /// 实测让 <c>qwen-vl-max</c> 同时输出「枚举 <c>kind</c>」与布尔 <c>isFixedForm</c>：
        /// <c>kind</c> 全对，但 <c>isFixedForm</c> <b>全错</b>（质量手册/内审计划都被判成证件）。
        /// ⇒ <b>只让模型从固定枚举里选 <c>kind</c>，派生布尔一律由代码映射</b>
        /// （见 <c>DocumentKindJudge.IsFixedFormByCode</c>）。</para>
        /// </summary>
        public string ImageJudgePrompt { get; set; } =
            "判断这份资料的类型。只输出一个 JSON：{\"kind\":\"体系文件|营业执照|身份证|资质证书|许可证|检测报告|其他\",\"confidence\":0.0-1.0,\"reason\":\"一句话\"}";

        /// <summary>
        /// ★ <b>文档作用识别提示词</b>（图片 / PDF → 分类 + 作用 + 关键词，§2 路径② 两层过滤用）。
        /// <para>与 <see cref="ImageJudgePrompt"/>（只判 kind 枚举）不同：本 prompt 额外要求
        /// 输出 <c>purpose</c>（一句话作用）与 <c>keywords</c>（3~5 个匹配关键词），
        /// 供「分组标签 → 文件作用」两层过滤的对齐判定使用。</para>
        /// <para>★ <b>与 OCR（<see cref="DocumentParsePrompt"/>）共用同一行 <c>ai_vision_config</c>、同一视觉模型</b>
        /// （用户 2026-10-07 裁决：底层核心能力不版本管理、一份配置服务所有文档；拆两行会出现
        /// 「这份用 A 模型、那份用 B 模型」的不一致）。换模型时这一项最可能要调。</para>
        /// </summary>
        public string DocPurposePrompt { get; set; } =
            "你是体系认证文件分析专家。请分析这份企业文件：只输出一个 JSON：" +
            "{\"kind\":\"体系文件|营业执照|身份证|资质证书|许可证|检测报告|手册|记录表|其他\"," +
            "\"purpose\":\"一句话描述该文件在体系文件中的用途\",\"keywords\":[],\"confidence\":0.0-1.0}";

        /// <summary>图片压缩：长边上限（px）。<b>图片按 token 计费</b>，压到 1600 可省约一半。</summary>
        public int ImageMaxEdge { get; set; } = 1600;

        /// <summary>压缩后仍超过此字节数则放弃视觉识别（防止超大扫描件拖垮链路）</summary>
        public int MaxImageBytes { get; set; } = 12 * 1024 * 1024;

        /// <summary>配置来源说明（诊断用：回退了几次）</summary>
        [JsonIgnore]
        public string Source { get; set; } = "default";

        /// <summary>
        /// 解析系统参数 JSON；失败回落到默认值（<b>不抛异常</b> —— 配置坏了不该让整条转换链挂掉）。
        /// </summary>
        public static VisionSettings Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new VisionSettings { Source = "default(no config)" };
            try
            {
                var s = JsonSerializer.Deserialize<VisionSettings>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (s == null) return new VisionSettings { Source = "default(null)" };
                s.Source = "ai_vision_config";
                if (string.IsNullOrWhiteSpace(s.Model)) s.Model = "qwen3-vl-flash";
                if (string.IsNullOrWhiteSpace(s.DocumentParsePrompt)) s.DocumentParsePrompt = "qwenvl markdown";
                if (string.IsNullOrWhiteSpace(s.DocPurposePrompt))
                    s.DocPurposePrompt =
                        "你是体系认证文件分析专家。请分析这份企业文件：只输出一个 JSON：" +
                        "{\"kind\":\"体系文件|营业执照|身份证|资质证书|许可证|检测报告|手册|记录表|其他\"," +
                        "\"purpose\":\"一句话描述该文件在体系文件中的用途\",\"keywords\":[],\"confidence\":0.0-1.0}";
                if (s.TimeoutSeconds <= 0) s.TimeoutSeconds = 180;
                if (s.MaxTokens <= 0) s.MaxTokens = 8192;
                if (s.MaxImageBytes <= 0) s.MaxImageBytes = 12 * 1024 * 1024;
                if (s.ImageMaxEdge <= 0) s.ImageMaxEdge = 1600;
                return s;
            }
            catch (JsonException)
            {
                return new VisionSettings { Source = "default(bad json)" };
            }
        }

        /// <summary>
        /// 用文本模型的 <c>ai_*</c> 六键回填空缺（保证「只加一条参数就能用」）。
        /// <para><c>ApiKey</c> 与 <c>BaseUrl</c> 优先沿用文本模型那把/那个地址 ——
        /// 同一个厂商时没必要重复配；只有切到<b>别的厂商</b>（如 agnes）才需要在 JSON 里显式覆盖。</para>
        /// </summary>
        public VisionSettings ResolveFrom(string? textBaseUrl, string? textApiKey)
        {
            if (string.IsNullOrWhiteSpace(BaseUrl)) BaseUrl = textBaseUrl ?? BaseUrl;
            if (string.IsNullOrWhiteSpace(ApiKey)) ApiKey = textApiKey ?? "";
            return this;
        }

        /// <summary>模型是否可能是视觉模型（诊断用：<b>不保证准</b>，只用来给出更准的告警文案）</summary>
        [JsonIgnore]
        public bool LooksLikeVisionModel
            => Model.Contains("vl", StringComparison.OrdinalIgnoreCase)
               || Model.Contains("vision", StringComparison.OrdinalIgnoreCase)
               || Model.Contains("omni", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// ★ 图片 / PDF「分类 + 作用 + 关键词」识别结果（§2 路径② 两层过滤的输入，2026-10-07）。
    ///
    /// <para><b>放 Shared 而非 Admin</b>：标准侧（Admin <c>StandardDocContractController</c>）与
    /// 企业侧（Auditor <c>EnterpriseOriginalAnalyzeExecutor</c>）都要消费它；⛔ Auditor 不能引 Admin
    /// （C2 分层），故结果 DTO 必须落在 Shared。识别调用本身（读 <c>ai_vision_config</c>）由两端各自薄封装。</para>
    /// <para>⛔ <b>不做可信度门槛拦截</b>（§9.5 全量留痕）：<see cref="Confidence"/> 无论高低都记录，
    /// 审核环节可审批 / 改源 / 改值。</para>
    /// </summary>
    public sealed record DocPurposeJudge
    {
        /// <summary>是否成功产出结构化结论。</summary>
        public bool Success { get; init; }

        /// <summary>分类（kind 枚举：体系文件|营业执照|身份证|…|其他）。</summary>
        public string Kind { get; init; } = "";

        /// <summary>作用（一句话用途 → <c>cert_enterprise_doc_profile.DocPurpose</c>）。</summary>
        public string Purpose { get; init; } = "";

        /// <summary>关键词（3~5 个 → 供 §2 路径② 第一层分组标签过滤对齐）。</summary>
        public List<string> Keywords { get; init; } = new();

        /// <summary>模型自评可信度 0.00~1.00（→ <c>MatchConfidence</c>；⛔ 仅记录不拦截）。</summary>
        public double Confidence { get; init; }

        /// <summary>失败 / 未接入说明（<see cref="Success"/>=false 时非空）。</summary>
        public string Message { get; init; } = "";

        public static DocPurposeJudge NotAvailable(string message) => new() { Success = false, Message = message };
    }
}
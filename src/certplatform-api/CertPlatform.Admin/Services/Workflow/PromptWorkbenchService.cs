using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SqlSugar;
using CertPlatform.Shared.DocExtraction;
using CertPlatform.Admin.Entities.Cert;
using CertPlatform.Admin.Entities.Dir;
using CertPlatform.Admin.Entities.Doc;
using CertPlatform.Admin.Entities.Wf;
using YZH.Core.Api.Models.System;
using YZH.Core.DataBase.Interfaces;

namespace CertPlatform.Admin.Services.Workflow;

/// <summary>
/// 提示词工作台服务（2026-10-02 新增）
///
/// <para><b>业务定位</b>（用户逐字口径）：「这个功能非常重要，我们需要不停的尝试完善提示词」——
/// 所以本服务提供「**边写边试**」的闭环：编辑提示词 → 一键 AI 生成草稿 → 上传真文件试跑 → 看结果 → 再改。</para>
///
/// <para><b>两个能力</b>：</para>
/// <list type="number">
///   <item><b>AI 自动生成 / 优化</b>（<see cref="GenerateAsync"/>）—— 用 <c>prompt_generator</c> 元提示词，
///         按「提示词类型 + 适用标准」生成 <c>doc_group</c>（分类）或 <c>doc_content</c>（作用）草稿；
///         传入 <c>currentTemplate</c> 时改为**优化**用户手写的现有提示词。
///         ★ 生成结果<b>不落库</b>，由用户确认后自行保存。</item>
///   <item><b>上传试跑</b>（<see cref="TestAsync"/>）—— 文件字节 → <see cref="DocumentConvertClient"/> 转 Markdown
///         → 渲染提示词 → <see cref="LlmInvokeService"/> 调用 → 返回结果。</item>
/// </list>
///
/// <para><b>★ Markdown 复用（2026-10-02 勘误，替代原「文件即弃」）</b>：</para>
/// <code>
/// 首次/换文件：Files → 转 Markdown → 落 PromptMarkdownCache（Redis, TTL 8h）→ 返回全文
/// 之后反复调提示词：只传 CacheKey → 读缓存直接跑 LLM，**零转换、零上传**
/// 重新上传：Files 覆写同一 CacheKey
/// </code>
///
/// <para><b>模型参数（2026-10-02 起「统一 AI 配置」，Q3=a）</b>：一律读
/// <c>cert_sys_config</c> 六键（<c>ai_model_name</c> / <c>ai_max_tokens</c> / <c>ai_temperature</c>）。
/// ⛔ 提示词行上的 <c>ModelName</c>/<c>MaxTokens</c>/<c>Temperature</c> **本工作台不再读取**
///（NC 链路 <c>BuildNpPromptSkill</c> 另行使用，未受影响）。</para>
///
/// <para><b>提示词定位（三层回退）</b>：<see cref="ResolveActiveAsync"/> 按「标准级 → 平台级」逐层回退，
/// 口径与全局参数（<c>ParamValueResolver.PickMostSpecific</c>）同构。</para>
/// </summary>
public class PromptWorkbenchService
{
    private readonly IDbOrm _db;
    private readonly LlmInvokeService _llm;
    private readonly DocumentConvertClient _convert;
    private readonly PromptMarkdownCache _mdCache;
    private readonly ILogger<PromptWorkbenchService> _logger;

    /// <summary>提示词类型常量</summary>
    public static class Types
    {
        /// <summary>元提示词（生成器）</summary>
        public const string Generator = "prompt_generator";
        /// <summary>★ 标题 / 分类提示词（输入文件清单 → 输出类别）</summary>
        public const string Group = "doc_group";
        /// <summary>★ 作用提示词（输入单文件 Markdown → 输出作用）</summary>
        public const string Content = "doc_content";
        /// <summary>★ 语义精要提示词（长文档摘要，输入长 Markdown → 输出精简画像）</summary>
        public const string Essential = "doc_essential";
    }

    /// <summary>
    /// <b>仅</b>「提示词工作台·单次试跑」(<see cref="TestAsync"/>) 接受的文件数上限（防超时 / 防 token 爆）。
    /// <para>⛔ <b>不是队列上限</b>：队列入口 <see cref="AnalyzeForQueueAsync"/> 自 2026-10-06（M8-1）起
    /// 用<b>自己的</b> <c>maxFiles</c> 参数（默认不限），由调用方按批切片。误用本常数会让
    /// <c>doc_group</c> 整批被拒 ⇒ 分组静默全丢。</para>
    /// </summary>
    private const int MaxTestFiles = 20;

    /// <summary>文件清单里每个文件注入的开头片段长度</summary>
    private const int HeadSnippetChars = 300;

    /// <summary><c>{{std_doc_catalog}}</c> 最多列多少行标准文档（防正文膨胀）</summary>
    private const int MaxCatalogRows = 120;

    // ========================================================
    // ★ 占位符契约（33 号 §4.2；新增三个语义上下文占位符）
    // ========================================================

    /// <summary>该标准启用的标签清单 —— LLM <b>只能从中选</b>（14 号 D14 约束）</summary>
    private const string PhTagList = "tag_list";
    /// <summary>标准目录文件夹树（3 层全路径）</summary>
    private const string PhFolderTree = "folder_tree";
    /// <summary>编号前缀 → 标签映射（L0 规则）</summary>
    private const string PhCodePrefixMap = "code_prefix_map";
    /// <summary>输出 JSON Schema 文本（33 号 §3.3）</summary>
    private const string PhOutputSchema = "output_schema";
    /// <summary>「只能从字典选」硬约束文本</summary>
    private const string PhTagConstraint = "tag_constraint";
    /// <summary>批次文件名清单（分组专用；既有实现名，勿改 —— 种子提示词已引用）</summary>
    private const string PhFileList = "file_list";
    /// <summary>单份全文（作用专用；既有实现名，勿改）</summary>
    private const string PhDocContent = "document_content";
    /// <summary>★ 标准名 + 版本年 —— <b>提示词按标准差异化的第一入口</b>（33 号 §0.4）</summary>
    private const string PhStandardName = "standard_name";
    /// <summary>该标准下的标准文档清单摘要（文档名 + 已有标签），让提示词知道"标准侧要什么"</summary>
    private const string PhStdDocCatalog = "std_doc_catalog";
    // ⛔ 2026-10-03：原 `PhFolderTreePh = "folder_tree"` 已删除 ——
    //    与上面的 `PhFolderTree` **同值同义且从未被引用**（死代码）。
    //    两个同名常量并存时，改动其中一个（如改值/改名）不会有任何编译错误，
    //    但另一处仍在用旧值 ⇒ 占位符静默不替换。保留唯一一份。

    /// <summary>
    /// 输出 JSON Schema（33 号 §3.3 —— 两条提示词共享的唯一契约，对齐 06 号 §4.1 并增补 tags/infoItems）。
    /// <para><c>doc_group</c> 可只出子集（<c>tags</c> + <c>suggestedPolicy</c>）。</para>
    /// </summary>
    private const string OutputSchemaJson =
        """
        {
          "tags": [
            { "tagCode": "RecordInternalAudit", "tagName": "内审记录",
              "confidence": 0.92, "reason": "标题含「内审」，编号前缀 XASL-QR" }
          ],
          "purpose": "【是什么】…【审核关注点】…【来源口径】…【包含信息】…",
          "infoItems": [
            { "itemName": "年度", "itemDesc": "所覆盖年度", "valueType": "number", "isKey": true }
          ],
          "typeGuess": "内审计划",
          "fields": [
            { "name": "文件编号", "key": "wen_jian_bian_hao", "value": "XASL-QR-014",
              "dataType": "string", "confidence": 0.95, "isKeyField": true }
          ],
          "tables": [
            { "index": 0, "name": "审核安排", "header": ["日期","部门","审核员"],
              "rowCount": 8, "columnCount": 3, "confidence": 0.9 }
          ],
          "suggestedPolicy": "analyze",
          "policyReason": "",
          "confidence": 0.9
        }
        """;

    /// <summary>
    /// 「只能从字典选」硬约束（14 号 D14 / 26 号 R-4）。
    /// <para>⚠️ 措辞里<b>不要写带花括号的占位符</b>：本常量会经
    /// <see cref="PromptRenderer.Render"/> 单趟替换，写进去的 {{tag_list}} 不会被二次渲染，
    /// 会以字面量残留在最终提示词里（模型看到无意义的 {{tag_list}}）。
    /// 真正的 {{tag_list}} 由模板自己写。</para>
    /// </summary>
    private const string TagConstraintText =
        "【硬约束】tags[].tagCode 必须逐字取自本提示词下方的「标签清单」中的编码，禁止自造、禁止同义改写；" +
        "确实无法归类时统一用 OTHER；tagCode 越界会被后端判为无效并强制回退 OTHER。";


    public PromptWorkbenchService(
        IDbOrm db,
        LlmInvokeService llm,
        DocumentConvertClient convert,
        PromptMarkdownCache mdCache,
        ILogger<PromptWorkbenchService> logger)
    {
        _db = db;
        _llm = llm;
        _convert = convert;
        _mdCache = mdCache;
        _logger = logger;
    }

    // ========================================================
    // 一、查询
    // ========================================================

    /// <summary>按类型 / 标准列出提示词（标准为空 = 只列不限标准的）</summary>
    public async Task<List<PromptTemplate>> ListAsync(string? promptType, string? standardCode)
    {
        var result = await _db.GetListAsync<PromptTemplate>(x =>
            x.IsValid == 1
            && (string.IsNullOrEmpty(promptType) || x.PromptType == promptType)
            && (string.IsNullOrEmpty(standardCode) || x.StandardCode == standardCode || x.StandardCode == null));

        return result.Success && result.Data != null
            ? result.Data.OrderBy(x => x.PromptType).ThenBy(x => x.PromptCode).ToList()
            : new List<PromptTemplate>();
    }

    /// <summary>按 PromptCode 取一条</summary>
    public async Task<PromptTemplate?> GetByCodeAsync(string promptCode)
    {
        if (string.IsNullOrWhiteSpace(promptCode)) return null;
        var r = await _db.GetOneAsync<PromptTemplate>(x => x.PromptCode == promptCode && x.IsValid == 1);
        return r.Success ? r.Data : null;
    }

    /// <summary>
    /// 三层回退定位生效提示词：**标准级（StandardCode 命中）→ 平台级（StandardCode = NULL）**。
    /// <para>⚠️ 与全局参数同一口径 —— 两套口径会导致「后台看 A、实际跑 B」且不报错。</para>
    /// </summary>
    public async Task<PromptTemplate?> ResolveActiveAsync(string promptType, string? standardCode)
    {
        var list = await ListAsync(promptType, null);
        var active = list.Where(x => x.IsActive).ToList();
        if (active.Count == 0) return null;

        if (!string.IsNullOrWhiteSpace(standardCode))
        {
            var specific = active.FirstOrDefault(x => x.StandardCode == standardCode);
            if (specific != null) return specific;
        }
        return active.FirstOrDefault(x => string.IsNullOrWhiteSpace(x.StandardCode));
    }

    // ========================================================
    // 二、AI 自动生成提示词（元提示词驱动）
    // ========================================================

    /// <summary>
    /// 用 <c>prompt_generator</c> 元提示词生成（或<b>优化</b>）一条提示词草稿（**不落库**）。
    /// <para><b>两种模式</b>（2026-10-02）：</para>
    /// <list type="bullet">
    ///   <item><c>currentTemplate</c> 为空 → 按标准<b>从零生成</b></item>
    ///   <item><c>currentTemplate</c> 非空 → <b>优化</b>这段现有手写内容（保留结构与业务口径，不全量重写）</item>
    /// </list>
    /// </summary>
    /// <param name="promptType">要生成的类型：<see cref="Types.Group"/> / <see cref="Types.Content"/></param>
    /// <param name="standardCode">适用标准（GUID）；仅用于给元提示词提供上下文</param>
    /// <param name="extraRequirement">额外要求（可选，用户自由输入）</param>
    /// <param name="currentTemplate">现有提示词正文；非空 = 走优化模式</param>
    public async Task<GenerateResult> GenerateAsync(
        string promptType, string? standardCode, string? extraRequirement, string? currentTemplate = null)
    {
        if (promptType != Types.Group && promptType != Types.Content)
            return GenerateResult.Fail($"不支持生成的提示词类型：{promptType}（只支持 doc_group / doc_content）");

        var generator = await ResolveActiveAsync(Types.Generator, null);
        if (generator == null || string.IsNullOrWhiteSpace(generator.Template))
            return GenerateResult.Fail("未找到生效的元提示词（PromptCode = prompt_generator），请先执行提示词种子脚本");

        var standardName = await ResolveStandardNameAsync(standardCode);
        var typeName = promptType == Types.Group ? "分类提示词（doc_group）" : "作用提示词（doc_content）";
        var isOptimize = !string.IsNullOrWhiteSpace(currentTemplate);

        var context = await BuildSemanticContextAsync(standardCode, promptType);
        context["prompt_type_name"] = typeName;
        context["standard_name"] = string.IsNullOrWhiteSpace(standardName) ? "通用（未指定标准）" : standardName!;
        context["extra_requirement"] = string.IsNullOrWhiteSpace(extraRequirement) ? "（无）" : extraRequirement!;
        // ★ 优化模式：把现有正文交给元提示词，并声明「只优化、不重写」
        context["current_template"] = isOptimize ? currentTemplate!.Trim() : "";
        context["work_mode"] = isOptimize ? "optimize" : "generate";

        var prompt = PromptRenderer.Render(generator.Template, context);

        var settings = await GetAiSettingsAsync();
        var model = settings.Model;
        var totalMs = 0L;
        var totalPt = 0;
        var totalCt = 0;

        // 一次真实的元提示词调用；本地函数负责计费日志与 token 累加（重试也走它）
        async Task<(bool Ok, string Message, string Text)> CallAsync(string sent)
        {
            var r = await _llm.CompleteAsync(new LlmInvokeRequest
            {
                BaseUrl = settings.BaseUrl,
                ApiKey = settings.ApiKey,
                Model = model,
                Temperature = settings.Temperature,
                MaxTokens = settings.MaxTokens,
                Prompt = sent,
                // ★ 生成的是「提示词正文」，不是 JSON —— 必须关掉 ForceJson，
                //   否则 dashscope 会强加 response_format=json_object，把提示词包成 JSON 字符串
                ForceJson = false
            });
            // ★ 元提示词生成同样计费（BusinessType='ent_profile'，与 06 号画像链同一口径）
            await LogUsageAsync("ent_profile", $"prompt_gen:{promptType}", settings, model, r);
            totalMs += r.DurationMs;
            totalPt += r.PromptTokens ?? 0;
            totalCt += r.CompletionTokens ?? 0;
            return (r.Success, r.Message, r.Success ? StripFence(r.Content) : "");
        }

        var first = await CallAsync(prompt);
        if (!first.Ok)
            return GenerateResult.Fail(first.Message, totalMs);

        // ★ 质检 + 一次纠偏重试（2026-10-02）：
        //   qwen-flash 常把元提示词里「下游模型只输出 JSON」误读成「自己只输出 JSON」，
        //   直接把契约 Schema 当正文回吐（实测 680 字纯 JSON）。首次不合格 → 携错因重问一次。
        var text = first.Text;
        var problems = ValidateGeneratedPrompt(text, promptType);
        if (problems.Count > 0)
        {
            var corrective = prompt
                + "\n\n## 追加要求（你上一次的输出不合格，必须重来）\n"
                + "不合格原因：" + string.Join("；", problems) + "\n"
                + "请重新输出**提示词正文**：Markdown 指令文本，按「正文骨架」分节成文，"
                + "占位符补上两个花括号，全文不少于 600 字。\n"
                + "⛔ 严禁输出 JSON Schema 本体、严禁输出示例 JSON、严禁只输出 JSON。\n";

            var second = await CallAsync(corrective);
            if (second.Ok)
            {
                text = second.Text;
                problems = ValidateGeneratedPrompt(text, promptType);
            }
            else
            {
                problems.Insert(0, "重试失败：" + second.Message);
            }
        }

        if (problems.Count > 0)
            return GenerateResult.Fail("AI 生成结果不合格：" + string.Join("；", problems) + "，请点「AI 生成」重试", totalMs);

        return GenerateResult.Ok(text, totalMs, totalPt, totalCt);
    }

    /// <summary>
    /// 元提示词输出质检（2026-10-02 新增）。
    /// <para>三条硬判据，任一不过即判「不合格」并触发纠偏重试：</para>
    /// <list type="number">
    ///   <item><b>JSON 本体</b> —— 把下游契约当成正文回吐（最常见翻车方式）</item>
    ///   <item><b>缺占位符</b> —— 少了运行时就没法注入，生成结果不可用</item>
    ///   <item><b>过短</b> —— 只给提纲不给正文，同样不可投产</item>
    /// </list>
    /// </summary>
    private static List<string> ValidateGeneratedPrompt(string? text, string promptType)
    {
        var problems = new List<string>();
        var t = (text ?? "").Trim();
        if (t.Length == 0) { problems.Add("输出为空"); return problems; }

        if (LooksLikeJsonOnly(t))
            problems.Add("输出的是 JSON 本体而不是提示词正文");

        var required = promptType == Types.Group
            ? new[] { "standard_name", "std_doc_catalog", "tag_list", "output_schema", "tag_constraint", "file_list" }
            : new[] { "standard_name", "std_doc_catalog", "tag_list", "output_schema", "tag_constraint", "document_content" };
        var missing = required.Where(n => !t.Contains("{{" + n + "}}")).ToList();
        if (missing.Count > 0)
            problems.Add("缺少占位符 " + string.Join("、", missing.Select(n => "{{" + n + "}}")));

        if (t.Length < 400) problems.Add($"正文过短（{t.Length} 字，要求 ≥ 400 字）");
        return problems;
    }

    /// <summary>整段能被 JSON 解析 = 模型回吐的是数据本体，不是提示词正文。</summary>
    private static bool LooksLikeJsonOnly(string t)
    {
        var s = t.TrimStart();
        if (s.Length == 0 || (s[0] != '{' && s[0] != '[')) return false;
        try { JsonNode.Parse(t); return true; }
        catch (JsonException) { return false; }
    }

    // ========================================================
    // 三、上传试跑（转 Markdown → 落 Redis 复用 → 跑提示词）
    // ========================================================

    /// <summary>
    /// 试跑一条提示词。
    /// <para><b>分类提示词</b>：可多文件 → 各自 Markdown 取「标题 + 开头片段」→ 组装文件清单 → 注入 <c>{{file_list}}</c></para>
    /// <para><b>作用提示词</b>：取第一个文件 → Markdown 全文 → 注入 <c>{{document_content}}</c></para>
    /// <para><b>★ Markdown 复用</b>（2026-10-02 勘误，替代「文件即弃」）：</para>
    /// <list type="bullet">
    ///   <item>传 <c>files</c> → 转 Markdown → 写入 <see cref="PromptMarkdownCache"/>（TTL 8h）→ 返回全文</item>
    ///   <item>不传 <c>files</c> 但传 <c>cacheKey</c> → **读缓存直接跑 LLM，零转换**</item>
    ///   <item>两者都无 → 报「请先选择要测试的文件」</item>
    ///   <item>缓存读不到 → 报「测试缓存已过期，请重新上传文件」（**不静默转换**，避免误判换文件）</item>
    /// </list>
    /// </summary>
    /// <param name="cacheKey">
    /// 前端持有的缓存键。**为空时后端生成并回传**，前端存起来供下次「重新分析」复用。
    /// </param>
    public async Task<TestResult> TestAsync(
        string promptType, string? templateOverride, string? standardCode,
        IReadOnlyList<(string FileName, byte[] Content)> files,
        string? cacheKey = null)
    {
        var hasFiles = files != null && files.Count > 0;
        if (!hasFiles && string.IsNullOrWhiteSpace(cacheKey))
            return TestResult.Fail("请先选择要测试的文件");

        if (hasFiles && files!.Count > MaxTestFiles)
            return TestResult.Fail($"一次最多测试 {MaxTestFiles} 个文件（当前 {files.Count} 个），请分批测试");

        // 0. 缓存键：前端传了就用它的（换文件时会覆写同一把 key），没传就生成一把回传
        var effectiveKey = string.IsNullOrWhiteSpace(cacheKey)
            ? Guid.NewGuid().ToString("N")
            : cacheKey!.Trim();

        // 1. 提示词正文：优先用页面上「未保存的编辑内容」（边改边试），否则用库里的生效版本
        var template = templateOverride;
        if (string.IsNullOrWhiteSpace(template))
        {
            var promptRow = await ResolveActiveAsync(promptType, standardCode);
            if (promptRow == null || string.IsNullOrWhiteSpace(promptRow.Template))
                return TestResult.Fail($"未找到生效的「{promptType}」提示词，请先填写提示词内容", cacheKey: effectiveKey);
            template = promptRow.Template;
        }

        // 2. 取 Markdown：换文件（或首次）→ 转换 + 覆写缓存；否则 → 读缓存
        List<(string FileName, string? Markdown, string? Error)> converted;
        if (hasFiles)
        {
            converted = await ConvertToMarkdownsAsync(files!);
            var ok = converted.Where(x => x.Markdown != null).ToList();
            if (ok.Count > 0)
                await SaveCacheAsync(effectiveKey, ok);
        }
        else
        {
            var cached = await LoadCacheAsync(effectiveKey);
            if (cached == null || cached.Count == 0)
                return TestResult.Fail(
                    "测试缓存已过期或不存在，请重新上传文件（缓存保留 8 小时）",
                    cacheKey: effectiveKey);

            converted = cached
                .Select(x => (x.FileName, (string?)x.Markdown, (string?)null))
                .ToList();
        }

        // 2.5 ★ 语义上下文占位符（{{tag_list}} / {{folder_tree}} / {{code_prefix_map}}
        //      / {{output_schema}} / {{tag_constraint}}）—— 必须在注入原始正文**之前**渲染，
        //      否则 Markdown 里偶发的 {{...}} 会被当成占位符误替换。
        template = PromptRenderer.Render(template, await BuildSemanticContextAsync(standardCode, promptType));

        // 3. 组装注入上下文
        string prompt;
        var convertLog = new List<ConvertLogItem>();
        foreach (var c in converted)
            convertLog.Add(new ConvertLogItem
            {
                FileName = c.FileName,
                Success = c.Markdown != null,
                Message = c.Error,
                MarkdownLength = c.Markdown?.Length ?? 0,
                MarkdownHead = c.Markdown == null ? null : Head(c.Markdown, 800),
                Markdown = c.Markdown
            });

        if (promptType == Types.Group)
        {
            var usable = converted.Where(x => x.Markdown != null).ToList();
            if (usable.Count == 0)
                return TestResult.Fail("所选文件均未能转换为 Markdown，无法测试分类提示词", convertLog, effectiveKey);

            var fileList = BuildFileListJson(usable.Select(x => (x.FileName, x.Markdown!)).ToList());
            // ★ 用常量拼 token（而非字面量 "{{file_list}}"）：占位符名只有一处定义，
            //   改常量即全链路生效；写成字面量会让常量沦为死代码且两处可能悄悄不一致。
            var fileListToken = "{{" + PhFileList + "}}";
            prompt = template!.Contains(fileListToken, StringComparison.Ordinal)
                ? template.Replace(fileListToken, fileList)
                : template + "\n\n" + fileList;
        }
        else
        {
            var first = converted.FirstOrDefault(x => x.Markdown != null);
            if (first.Markdown == null)
                return TestResult.Fail($"文件未能转换为 Markdown：{first.Error ?? "未知原因"}", convertLog, effectiveKey);

            var docToken = "{{" + PhDocContent + "}}";
            prompt = template!.Contains(docToken, StringComparison.Ordinal)
                ? template.Replace(docToken, first.Markdown)
                : template + "\n\n---\n" + first.Markdown + "\n---";
        }

        // 4. 调 LLM（★ 2026-10-02 起统一 AI 配置：一律读 cert_sys_config，不再读提示词行覆盖）
        var settings = await GetAiSettingsAsync();
        var model = settings.Model;
        var maxTokens = settings.MaxTokens;
        var temperature = settings.Temperature;

        var resp = await _llm.CompleteAsync(new LlmInvokeRequest
        {
            BaseUrl = settings.BaseUrl,
            ApiKey = settings.ApiKey,
            Model = model,
            Temperature = temperature,
            MaxTokens = maxTokens,
            Prompt = prompt,
            ForceJson = true
        });

        _logger.LogInformation("[PromptWorkbench] 试跑 {Type} 文件 {N} 个 → {Ok}，{Ms}ms",
            promptType, converted.Count, resp.Success, resp.DurationMs);

        // ★ 试跑也计费（33 号 §八 F5 / 验收 A4）：BusinessType='ent_profile'
        await LogUsageAsync("ent_profile", $"prompt_test:{promptType}", settings, model, resp);

        // ★ 33 号 §3.3 后端六条校验：越界 tagCode → 强制 OTHER、分数 clamp、截断、缺作用降权。
        //   解析失败 = 分析输出不符合规范 ⇒ Success=false（前端据此**硬拦保存**，不往下走）。
        var rawJson = resp.Json?.RootElement.GetRawText();
        var validTagCodes = (await LoadTagsAsync(standardCode))
            .Select(t => t.TagCode)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var (jsonValid, vMsgs, normalized) = ValidateSemanticOutput(rawJson, validTagCodes);

        bool success;
        string message;
        if (!resp.Success)
        {
            success = false;
            message = resp.Message;
        }
        else if (!jsonValid)
        {
            success = false;
            message = $"分析输出不符合规范：{vMsgs.FirstOrDefault() ?? "输出不是合法 JSON"}";
        }
        else
        {
            success = true;
            message = vMsgs.Count > 0 ? $"测试完成（后端已校正 {vMsgs.Count} 处）" : "测试完成";
        }

        return new TestResult
        {
            Success = success,
            Message = message,
            Model = model,
            MaxTokens = maxTokens,
            Temperature = temperature,
            PromptText = prompt,
            RawOutput = resp.Content,
            JsonOutput = normalized ?? rawJson,
            DurationMs = resp.DurationMs,
            PromptTokens = resp.PromptTokens,
            CompletionTokens = resp.CompletionTokens,
            Files = convertLog,
            // ★ 回传缓存键：前端存起来，下次只传 key + 新提示词即可复用 Markdown
            CacheKey = effectiveKey,
            CacheHit = !hasFiles,
            Validation = new ValidationInfo
            {
                Valid = jsonValid,
                Messages = vMsgs,
                NormalizedJson = normalized
            }
        };
    }

    // ========================================================
    // ★ 队列语义入口（36 号 §八 T2.1）
    // ========================================================

    /// <summary>
    /// <b>队列语义</b>的分析入口（企业原始资料 / 任何「自动跑提示词」的场景）。
    ///
    /// <para><b>与 <see cref="TestAsync"/> 的三点差异</b>（其余逻辑全部共用，不复制）：</para>
    /// <list type="number">
    ///   <item><b>输入是已转好的 Markdown，不是文件字节</b> —— 上游 ingest 链已把 Markdown 落进 MinIO
    ///         并记在 <c>MarkdownPath</c>，<b>队列语义没有转换缓存</b>（<c>PromptMarkdownCache</c> 是
    ///         「页面上传文件试跑」的人工闭环缓存，TTL 8h，对队列毫无意义）。</item>
    ///   <item><b>产出落画像表</b>（<c>ProfileVersion+1</c>、旧版 <c>IsLatest=0</c>），
    ///         而工作台产出只回给页面 + 写日志。</item>
    ///   <item><b><c>doc_group</c> 按批次传多份</b>（33 号定义它是「批次提示词，吃文件名清单」），
    ///         <c>doc_content</c> 传单份。逐文件跑 <c>doc_group</c> 是语义错配且成本 N 倍。</item>
    /// </list>
    ///
    /// <para>⚠️ 私有核心（<c>BuildSemanticContextAsync</c> / <c>ValidateSemanticOutput</c> /
    /// <c>LoadTagsAsync</c> / <c>BuildFileListJson</c> / <c>LogUsageAsync</c>）<b>一个都不复制</b>，
    /// 只在此处复用 —— 复制即漂移，33 号的六条输出校验一旦分叉，两侧画像字段就会长出不同形状。</para>
    /// </summary>
    /// <param name="promptType"><see cref="Types.Group"/> 或 <see cref="Types.Content"/></param>
    /// <param name="standardCode">标准 Code（<b>GUID</b>）；空 = 平台级。<b>⚠️ 不接受 slug</b>。</param>
    /// <param name="markdownByFile">文件名 → Markdown 全文（<c>doc_group</c> 传整批 / <c>doc_content</c> 传一份）</param>
    /// <param name="businessRef">计费与画像的关联键（落 <c>cert_ai_usage_log.BusinessRef</c>）</param>
    /// <param name="maxFiles">
    /// 本方法<b>自身</b>的单次文件数上限；<c>0</c>（默认）= <b>不限</b>。
    ///
    /// <para>★ <b>2026-10-06（M8-1）</b>：此前这里误用了测试页常数 <see cref="MaxTestFiles"/>（<c>=20</c>），
    /// 导致 <c>doc_group</c> 一旦收到 101 份就被<b>整体拒绝</b> ⇒ 执行器只 <c>LogWarning</c> 继续跑
    /// <c>doc_content</c> ⇒ <c>groupByFile</c> 空 ⇒ <b>分组全丢且页面看不出异常</b>（静默降级）。
    /// 队列侧的正确做法是<b>调用方按批切片</b>（见 <c>EnterpriseOriginalAnalyzeExecutor</c> 的
    /// <c>GroupBatchSize</c>），⛔ 而不是在这里用测试页的常数拦。</para>
    /// <para>⛔ <b>不要</b>把默认值改成 <see cref="MaxTestFiles"/> —— 那等于把刚修好的 bug 又写回来。</para>
    /// </param>
    public async Task<AnalyzeForQueueResult> AnalyzeForQueueAsync(
        string promptType, string? standardCode,
        IReadOnlyList<(string FileName, string? Markdown)> markdownByFile,
        string businessRef,
        int maxFiles = 0)
    {
        var usable = markdownByFile.Where(x => !string.IsNullOrWhiteSpace(x.Markdown)).ToList();
        if (usable.Count == 0)
            return AnalyzeForQueueResult.Fail("没有可用的 Markdown 输入（转换未完成或内容为空）");

        // ⚠️ 只在调用方**显式**传了正数时才拦（测试页走 TestAsync，不经这里）
        if (maxFiles > 0 && usable.Count > maxFiles)
            return AnalyzeForQueueResult.Fail($"一次最多分析 {maxFiles} 个文件（当前 {usable.Count} 个），请分批");

        // 1. 提示词正文（本方法<strong>不接未保存的编辑内容</strong> —— 队列只认库里生效版本）
        var promptRow = await ResolveActiveAsync(promptType, standardCode);
        if (promptRow == null || string.IsNullOrWhiteSpace(promptRow.Template))
            return AnalyzeForQueueResult.Fail($"未找到生效的「{promptType}」提示词，请先在提示词工作台填写内容");

        // 2. 语义上下文占位符必须在注入原始正文<b>之前</b>渲染
        //    （否则 Markdown 里偶发的 {{...}} 会被当占位符误替换）
        var template = PromptRenderer.Render(promptRow.Template, await BuildSemanticContextAsync(standardCode, promptType));

        // 3. 组装注入上下文
        string prompt;
        if (promptType == Types.Group)
        {
            var fileList = BuildFileListJson(
                usable.Select(x => (x.FileName, x.Markdown!)).ToList());
            var fileListToken = "{{" + PhFileList + "}}";
            prompt = template.Contains(fileListToken, StringComparison.Ordinal)
                ? template.Replace(fileListToken, fileList)
                : template + "\n\n" + fileList;
        }
        else
        {
            if (usable.Count != 1)
                return AnalyzeForQueueResult.Fail($"作用提示词（{Types.Content}）一次只接受 1 份文件，当前 {usable.Count} 份");
            var docToken = "{{" + PhDocContent + "}}";
            prompt = template.Contains(docToken, StringComparison.Ordinal)
                ? template.Replace(docToken, usable[0].Markdown!)
                : template + "\n\n---\n" + usable[0].Markdown + "\n---";
        }

        // 4. 调 LLM（配置口径与 TestAsync 完全一致：统一读 cert_sys_config 六键）
        var settings = await GetAiSettingsAsync();
        var resp = await _llm.CompleteAsync(new LlmInvokeRequest
        {
            BaseUrl = settings.BaseUrl,
            ApiKey = settings.ApiKey,
            Model = settings.Model,
            Temperature = settings.Temperature,
            MaxTokens = settings.MaxTokens,
            Prompt = prompt,
            ForceJson = true
        });

        _logger.LogInformation("[AnalyzeForQueue] {Type} 文件 {N} 个 → {Ok}，{Ms}ms",
            promptType, usable.Count, resp.Success, resp.DurationMs);

        // ★ 与 TestAsync 同一 BusinessType（'ent_profile'）⇒ AI 费用分析页能合并统计
        await LogUsageAsync("ent_profile", businessRef, settings, settings.Model, resp);

        var rawJson = resp.Json?.RootElement.GetRawText();

        // ★ ⚠️ GUID → slug：`cert_tag_dict.StandardCodes` 存 slug，而 standardCode 是 GUID
        var tagScope = await ResolveStandardSlugAsync(standardCode);
        var validTagCodes = (await LoadTagsAsync(tagScope))
            .Select(t => t.TagCode)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var (jsonValid, vMsgs, normalized) = ValidateSemanticOutput(rawJson, validTagCodes);

        if (!resp.Success)
            return AnalyzeForQueueResult.Fail(resp.Message);
        if (!jsonValid)
            return AnalyzeForQueueResult.Fail(
                $"分析输出不符合规范：{vMsgs.FirstOrDefault() ?? "输出不是合法 JSON"}", resp, rawJson);

        return new AnalyzeForQueueResult
        {
            Success = true,
            Message = vMsgs.Count > 0 ? $"分析完成（后端已校正 {vMsgs.Count} 处）" : "分析完成",
            Model = settings.Model,
            PromptCode = promptRow.Code,
            PromptVersion = promptRow.Version,
            Json = normalized ?? rawJson,
            RawOutput = resp.Content,
            DurationMs = resp.DurationMs,
            PromptTokens = (int)(resp.PromptTokens ?? 0),
            CompletionTokens = (int)(resp.CompletionTokens ?? 0),
            ValidationMessages = vMsgs,
        };
    }

    /// <summary>
    /// 标准 Code（GUID）→ 可读 slug（<c>cert_iso_standard.standard_code</c>）。
    /// <para>★ <b>为什么必须有这一步</b>：<c>wf_prompt_template.StandardCode</c> 存 <b>GUID</b>
    /// （<c>20261002_prompt_workbench_V1.sql:38</c>），而 <c>cert_tag_dict.StandardCodes</c> 存
    /// <b>slug</b>（<c>20261002_doc_semantic_rule_V1.sql:161</c>，示例 <c>["iso9001"]</c>）。
    /// 两者口径不同 ⇒ 直接把 GUID 传给标签裁剪会<b>恒裁不出东西</b>（静默失效）。</para>
    /// </summary>
    private async Task<string?> ResolveStandardSlugAsync(string? standardCode)
    {
        if (string.IsNullOrWhiteSpace(standardCode)) return null;
        var r = await _db.GetOneAsync<ISOStandard>(x => x.Code == standardCode);
        if (r.Success && r.Data != null && !string.IsNullOrWhiteSpace(r.Data.StandardCode))
            return r.Data.StandardCode;
        // 已经是 slug（老数据兜底）就直接用
        return standardCode;
    }

    /// <summary>
    /// 逐个把文件字节转成 Markdown。
    /// <para>★ <c>DocumentConvertClient</c> 内部会话目录由 finally 清理 —— 清的是**转换临时文件**，
    /// 与本服务把**转出来的 Markdown 文本**落 Redis 缓存互不冲突。</para>
    /// </summary>
    private async Task<List<(string FileName, string? Markdown, string? Error)>> ConvertToMarkdownsAsync(
        IReadOnlyList<(string FileName, byte[] Content)> files)
    {
        var converted = new List<(string FileName, string? Markdown, string? Error)>();
        foreach (var (fileName, content) in files)
        {
            if (content == null || content.Length == 0)
            {
                converted.Add((fileName, null, "文件内容为空"));
                continue;
            }

            var r = await _convert.ConvertToMarkdownAsync(fileName, content);
            if (!r.Success || r.Content == null)
            {
                converted.Add((fileName, null, r.Message));
                continue;
            }

            converted.Add((fileName, Encoding.UTF8.GetString(r.Content), null));
        }
        return converted;
    }

    /// <summary>把转换成功的 Markdown 写入 Redis（失败只记日志，不影响试跑）</summary>
    private async Task SaveCacheAsync(string cacheKey, List<(string FileName, string? Markdown, string? Error)> ok)
    {
        try
        {
            var payload = ok
                .Select(x => new CachedFile { FileName = x.FileName, Markdown = x.Markdown! })
                .ToList();
            await _mdCache.SetAsync(cacheKey, JsonSerializer.Serialize(payload));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[PromptWorkbench] Markdown 缓存写入失败（可继续，下次仍会重新转换）");
        }
    }

    /// <summary>读取 Redis 中的 Markdown；未命中 / 反序列化失败返回 null</summary>
    private async Task<List<CachedFile>?> LoadCacheAsync(string cacheKey)
    {
        var json = await _mdCache.GetAsync(cacheKey);
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            return JsonSerializer.Deserialize<List<CachedFile>>(json);
        }
        catch
        {
            await _mdCache.RemoveAsync(cacheKey);   // 脏数据直接丢，避免反复读失败
            return null;
        }
    }

    /// <summary>Redis 中缓存的单个文件（文件名 + 完整 Markdown）</summary>
    private sealed class CachedFile
    {
        [JsonPropertyName("fileName")] public string FileName { get; set; } = "";
        [JsonPropertyName("markdown")] public string Markdown { get; set; } = "";
    }

    // ========================================================
    // 三.4 ★ 语义输出校验（33 号 §3.3 后端六条）
    // ========================================================
    //
    //  ① tagCode 必须 ∈ cert_tag_dict —— 越界强制回退 OTHER（记消息，**不整单失败**）
    //  ② 分数 clamp [0,1] 并 ROUND(,2)
    //  ③ tables > 50 截断
    //  ④ fields > 100 截断
    //  ⑤ purpose 缺失 → Confidence ≤ 0.5 并标 [缺作用]
    //  ⑥ tags 缺失/为空 → 后端补一条 OTHER，避免下游无标签可比
    //
    //  形态两种（分组 = 批次 {items:[…]}，作用 = 单对象），共用同一段校验。
    // ========================================================

    /// <summary>语义输出校验结果（<c>Valid</c> / <c>Messages</c> / <c>NormalizedJson</c>）</summary>
    private sealed record SemanticValidation(bool Valid, List<string> Messages, string? NormalizedJson);

    /// <summary>
    /// 校验并<b>原地修复</b>语义分析输出。
    /// </summary>
    /// <param name="jsonText">LLM 返回的 JSON 原文</param>
    /// <param name="validTagCodes">合法标签编码集合（cert_tag_dict）；为空 = 字典未就绪，跳过越界判定</param>
    /// <returns>Valid=JSON 是否可解析；Messages=修复/告警明细；NormalizedJson=修复后的 JSON</returns>
    private static SemanticValidation ValidateSemanticOutput(
        string? jsonText, ISet<string> validTagCodes)
    {
        if (string.IsNullOrWhiteSpace(jsonText))
            return new SemanticValidation(false, new List<string> { "未返回 JSON 输出" }, null);

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(jsonText);
        }
        catch (JsonException ex)
        {
            return new SemanticValidation(false, new List<string> { $"JSON 解析失败：{ex.Message}" }, null);
        }

        if (node is not JsonObject root)
            return new SemanticValidation(false, new List<string> { "输出不是 JSON 对象（应为 {...}）" }, null);

        var messages = new List<string>();
        if (root["items"] is JsonArray items)
        {
            if (items.Count == 0)
                messages.Add("items 为空数组 —— 未返回任何文件的判定");

            for (var i = 0; i < items.Count; i++)
            {
                if (items[i] is not JsonObject doc)
                {
                    messages.Add($"items[{i}] 不是 JSON 对象，已忽略");
                    continue;
                }
                ValidateDocObject(doc, validTagCodes, messages, $"items[{i}]");
            }
        }
        else
        {
            ValidateDocObject(root, validTagCodes, messages, "");
        }

        return new SemanticValidation(true, messages, root.ToJsonString());
    }

    /// <summary>对<b>单个文档对象</b>做六条修复</summary>
    private static void ValidateDocObject(
        JsonObject doc, ISet<string> validTagCodes, List<string> messages, string prefix)
    {
        var p = string.IsNullOrEmpty(prefix) ? "" : prefix + ".";
        var hasDict = validTagCodes.Count > 0;

        // ① tags 越界 → OTHER；缺失 → 补一条 OTHER
        JsonArray tags;
        if (doc["tags"] is JsonArray arr && arr.Count > 0)
        {
            tags = arr;
        }
        else
        {
            tags = new JsonArray { MakeTag("OTHER", "其他", 0.3, "输出未给出标签，后端兜底补 OTHER") };
            doc["tags"] = tags;
            messages.Add($"{p}tags 缺失或为空 → 后端补 OTHER");
        }

        foreach (var item in tags.ToList())
        {
            if (item is not JsonObject tag)
            {
                messages.Add($"{p}tags 中存在非对象元素，已忽略");
                continue;
            }

            var code = (tag["tagCode"] as JsonNode)?.ToString()?.Trim();
            if (hasDict && (string.IsNullOrWhiteSpace(code) || !validTagCodes.Contains(code!)))
            {
                var bad = string.IsNullOrWhiteSpace(code) ? "(空)" : code!;
                tag["tagCode"] = "OTHER";
                tag["tagName"] = "其他";
                messages.Add($"{p}tags 标签越界 {bad} → 强制回退 OTHER");
            }

            ClampScore(tag, "confidence", 0.5, messages, $"{p}tags.confidence");
        }

        // ② 顶层 confidence
        ClampScore(doc, "confidence", 0.5, messages, $"{p}confidence");

        // ③ tables ≤ 50
        if (doc["tables"] is JsonArray tables && tables.Count > 50)
        {
            for (var i = tables.Count - 1; i >= 50; i--) tables.RemoveAt(i);
            messages.Add($"{p}tables 超 50 个 → 已截断");
        }

        // ④ fields ≤ 100
        if (doc["fields"] is JsonArray fields && fields.Count > 100)
        {
            for (var i = fields.Count - 1; i >= 100; i--) fields.RemoveAt(i);
            messages.Add($"{p}fields 超 100 条 → 已截断");
        }

        // ⑤ purpose 缺失 → Confidence ≤ 0.5 + [缺作用]
        var purpose = (doc["purpose"] as JsonNode)?.ToString();
        if (string.IsNullOrWhiteSpace(purpose))
        {
            doc["purpose"] = "[缺作用] 输出未给出 purpose，已降权";
            ForceMaxConfidence(doc, 0.5);
            messages.Add($"{p}purpose 缺失 → 标 [缺作用] 且 confidence ≤ 0.5");
        }
        else if (purpose!.StartsWith("[缺作用]", StringComparison.Ordinal))
        {
            ForceMaxConfidence(doc, 0.5);
        }
    }

    /// <summary>生成一条最小可用的 tag 节点</summary>
    private static JsonObject MakeTag(string code, string name, double confidence, string reason)
        => new()
        {
            ["tagCode"] = code,
            ["tagName"] = name,
            ["confidence"] = confidence,
            ["reason"] = reason
        };

    /// <summary>把某字段读成 double；读不出返回 null</summary>
    private static double? ReadNumber(JsonObject obj, string name)
    {
        if (obj[name] is not JsonNode node) return null;
        if (node is JsonValue v && v.TryGetValue<double>(out var d)) return d;
        return double.TryParse(node.ToString(), System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    /// <summary>分数 clamp [0,1] 并保留两位小数；缺失时补默认值</summary>
    private static void ClampScore(JsonObject obj, string name, double fallback,
        List<string> messages, string label)
    {
        var raw = ReadNumber(obj, name);
        if (raw == null)
        {
            obj[name] = Math.Round(fallback, 2);
            messages.Add($"{label} 缺失 → 补 {Math.Round(fallback, 2)}");
            return;
        }

        var clamped = Math.Round(Math.Clamp(raw.Value, 0, 1), 2);
        if (Math.Abs(clamped - raw.Value) > 1e-9)
        {
            obj[name] = clamped;
            messages.Add($"{label} 越界 {raw.Value} → clamp 为 {clamped}");
        }
    }

    /// <summary>把 confidence 压到 ≤ max（只降不升）</summary>
    private static void ForceMaxConfidence(JsonObject doc, double max)
    {
        var cur = ReadNumber(doc, "confidence");
        if (cur == null || cur.Value > max) doc["confidence"] = Math.Round(max, 2);
    }

    // ========================================================
    // 三.5 ★ 语义上下文注入（33 号 §4.2 三个新占位符）
    // ========================================================

    /// <summary>
    /// 组装语义上下文字典：<c>{{tag_list}}</c> / <c>{{folder_tree}}</c> / <c>{{code_prefix_map}}</c>
    /// / <c>{{output_schema}}</c> / <c>{{tag_constraint}}</c> / <c>{{standard_name}}</c> / <c>{{std_doc_catalog}}</c>。
    /// <para>模板里没有的占位符不会被替换（<see cref="PromptRenderer.Render"/> 未命中保留原文），
    /// 因此**业务提示词可按需选用**，不必全写。</para>
    /// <para>★ 2026-10-02 补：原先此处<b>只有标签类上下文，没有标准身份</b>，
    /// 导致 <c>doc_group_iso9001</c> 与 <c>doc_group_iso13485</c> 送给模型的输入逐字节相同
    /// —— 33 号 §0.4「标签集合、作用描述都是『标准 × 文档』的函数」落不了地。
    /// 现补 <c>standard_name</c> + <c>std_doc_catalog</c>，让提示词知道自己服务的是哪个标准。</para>
    /// </summary>
    private async Task<Dictionary<string, object>> BuildSemanticContextAsync(string? standardCode, string promptType)
    {
        var tags = await LoadTagsAsync(standardCode);
        var ctx = new Dictionary<string, object>
        {
            [PhTagList] = BuildTagListText(tags),
            [PhCodePrefixMap] = BuildCodePrefixMapText(tags),
            [PhFolderTree] = await BuildFolderTreeTextAsync(standardCode),
            [PhOutputSchema] = OutputSchemaJson,
            [PhTagConstraint] = TagConstraintText,
            [PhStandardName] = await BuildStandardNameTextAsync(standardCode),
            [PhStdDocCatalog] = await BuildStdDocCatalogTextAsync(standardCode, tags),
            ["prompt_type"] = promptType
        };
        return ctx;
    }

    /// <summary>
    /// <c>{{standard_name}}</c>：标准名 + 版本年 + 编号规则提示。
    /// <para>空作用域（平台级）时明确写「未指定标准」，<b>不留空串</b> ——
    /// 空串会让模型以为"没有标准要求"，从而自由发挥。</para>
    /// </summary>
    private async Task<string> BuildStandardNameTextAsync(string? standardCode)
    {
        if (string.IsNullOrWhiteSpace(standardCode)) return "未指定标准（平台级默认规则，适用全部标准）";

        var r = await _db.GetOneAsync<ISOStandard>(x => x.Code == standardCode);
        if (!r.Success || r.Data == null) return "未指定标准（平台级默认规则，适用全部标准）";

        var s = r.Data;
        var sb = new StringBuilder();
        sb.Append(s.StandardName);
        if (s.VersionYear > 0) sb.Append("（").Append(s.VersionYear).Append(" 版）");
        if (!string.IsNullOrWhiteSpace(s.Category)) sb.Append(" · 类别：").Append(s.Category);
        if (!string.IsNullOrWhiteSpace(s.Description)) sb.Append('\n').Append(s.Description);
        return sb.ToString();
    }

    /// <summary>
    /// <c>{{std_doc_catalog}}</c>：该标准下的标准文档清单摘要。
    /// <para>★ 这是让 <c>doc_content</c>「按标准差异化」的关键输入 ——
    /// 不同标准下同一类文档的要求不同（33 号 §0.4：13485 有检验作业指导书、9001 没有），
    /// 模型只有看到标准侧要什么，才可能判出"这份企业资料能否填这份标准文档"。</para>
    /// <para>只取前 <see cref="MaxCatalogRows"/> 行，避免正文膨胀。</para>
    /// </summary>
    private async Task<string> BuildStdDocCatalogTextAsync(string? standardCode, List<TagDict> tags)
    {
        if (string.IsNullOrWhiteSpace(standardCode)) return "（平台级规则，未绑定具体标准，无标准文档清单）";

        var cfgR = await _db.GetListAsync<StandardDirectoryConfig>(
            x => x.StandardCode == standardCode && x.IsValid == 1 && !x.IsDeleted);
        var cfgCodes = cfgR.Success && cfgR.Data != null
            ? cfgR.Data.Select(x => x.Code).Where(c => !string.IsNullOrWhiteSpace(c)).ToList()
            : new List<string>();
        if (cfgCodes.Count == 0) return "（该标准尚无目录配置，无标准文档清单）";

        var fileR = await _db.GetListAsync<StandardDirectoryFile>(
            x => cfgCodes.Contains(x.ConfigCode) && x.IsValid == 1 && !x.IsDeleted);
        var rows = fileR.Success && fileR.Data != null ? fileR.Data : new List<StandardDirectoryFile>();
        if (rows.Count == 0) return "（该标准尚无标准文档）";

        // ★ 必须按 FileName 去重：实测同一标准下有 4 份目录配置（阶段/机构维度的副本），
        //   668 行里只有 168 个不同标准文档（与 33 号 §0.1「实测 ISO13485 167 个」吻合）。
        //   不去重会把同一份文档重复 4 次塞进提示词 —— 白烧 token 且清单难读。
        var files = rows
            .GroupBy(x => (x.FileName ?? string.Empty).Trim())
            .Select(g => g.First())
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.FileName)
            .ToList();
        if (files.Count == 0) return "（该标准尚无标准文档）";

        var known = tags.Select(t => t.TagCode).Where(c => !string.IsNullOrWhiteSpace(c)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var sb = new StringBuilder();
        sb.Append("本标准要求的标准文档共 ").Append(files.Count).Append(" 份（按目录顺序");
        if (files.Count > MaxCatalogRows) sb.Append("，仅列前 ").Append(MaxCatalogRows);
        sb.AppendLine(" 份）：");
        foreach (var f in files.Take(MaxCatalogRows))
        {
            sb.Append("- ").Append(f.FileName);
            if (f.DocCategory == "fixed") sb.Append("  [固定文档 · 指纹匹配即终点]");
            else if (f.DocCategory == "hybrid") sb.Append("  [混合文档]");
            sb.AppendLine();
        }
        if (files.Count > MaxCatalogRows)
            sb.AppendLine($"…（其余 {files.Count - MaxCatalogRows} 份略）");

        if (known.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("★ 判定要点：企业文档若能对应到上面某份标准文档，作用描述要写清它能填哪一份；对应不上时不要硬套。");
        }
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// 取该标准启用的标签（`IsValid=1` + `Status=active`，按 `StandardCodes` 裁剪）。
    /// <para>裁剪规则（33 号 Q1）：`StandardCodes` 为 NULL / 空 = 全标准通用；否则须包含当前标准。</para>
    /// </summary>
    private async Task<List<TagDict>> LoadTagsAsync(string? standardCode)
    {
        var r = await _db.GetListAsync<TagDict>(x => x.IsValid == 1 && x.Status == "active");
        var all = r.Success && r.Data != null ? r.Data : new List<TagDict>();
        if (string.IsNullOrWhiteSpace(standardCode)) return all;

        return all.Where(t =>
            string.IsNullOrWhiteSpace(t.StandardCodes)
            || t.StandardCodes.Contains(standardCode, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>`{{tag_list}}`：一行一条，编码 + 名称 + 分组 + 识别特征（few-shot 用）</summary>
    private static string BuildTagListText(List<TagDict> tags)
    {
        if (tags.Count == 0)
            return "（字典暂无标签 —— 请先在「标签清单」Tab 维护 cert_tag_dict）";

        var sb = new StringBuilder();
        sb.AppendLine("可选标签编码（tagCode 只能取以下值）：");
        foreach (var t in tags.OrderBy(x => x.Sort).ThenBy(x => x.TagCode))
        {
            sb.Append("- ").Append(t.TagCode)
              .Append(" | ").Append(t.TagName);
            if (!string.IsNullOrWhiteSpace(t.TagGroup)) sb.Append(" | 分组：").Append(t.TagGroup);
            if (!string.IsNullOrWhiteSpace(t.MatchFeature)) sb.Append(" | 识别特征：").Append(t.MatchFeature);
            if (!string.IsNullOrWhiteSpace(t.SampleDocNames)) sb.Append(" | 样例：").Append(t.SampleDocNames);
            if (!string.IsNullOrWhiteSpace(t.TagPurposeHint)) sb.Append(" | 作用要点：").Append(t.TagPurposeHint);
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    /// <summary>`{{code_prefix_map}}`：L0 编号前缀规则（零 LLM，命中即定标签）</summary>
    private static string BuildCodePrefixMapText(List<TagDict> tags)
    {
        var withFeature = tags.Where(t => !string.IsNullOrWhiteSpace(t.MatchFeature)).ToList();
        if (withFeature.Count == 0) return "（暂无编号前缀规则）";

        var sb = new StringBuilder();
        sb.AppendLine("编号前缀 / 文件名特征 → 标签（L0 确定性规则，优先于语义判断）：");
        foreach (var t in withFeature)
            sb.Append("- ").Append(t.MatchFeature).Append(" → ").Append(t.TagCode)
              .Append("（").Append(t.TagName).AppendLine("）");
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// `{{folder_tree}}`：该标准的目录文件夹树（缩进 = 层级）。
    /// <para>链路：`StandardDirectoryConfig.StandardCode` → `ConfigCode` → `cert_standard_directory_folder`。</para>
    /// </summary>
    private async Task<string> BuildFolderTreeTextAsync(string? standardCode)
    {
        if (string.IsNullOrWhiteSpace(standardCode)) return "（未指定标准）";

        var cfgR = await _db.GetListAsync<StandardDirectoryConfig>(
            x => x.StandardCode == standardCode && x.IsValid == 1);
        var cfgs = cfgR.Success && cfgR.Data != null ? cfgR.Data : new List<StandardDirectoryConfig>();
        if (cfgs.Count == 0) return "（该标准尚无目录配置）";
        var cfgCodes = cfgs.Select(x => x.Code).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        if (cfgCodes.Count == 0) return "（该标准尚无目录配置）";

        var folderR = await _db.GetListAsync<StandardDirectoryFolder>(
            x => cfgCodes.Contains(x.ConfigCode) && x.IsValid == 1);
        var folders = folderR.Success && folderR.Data != null ? folderR.Data : new List<StandardDirectoryFolder>();
        if (folders.Count == 0) return "（该标准尚无文件夹）";

        var byParent = folders.GroupBy(x => x.ParentCode ?? "")
                              .ToDictionary(g => g.Key, g => g.OrderBy(x => x.SortOrder).ThenBy(x => x.Sort).ToList());
        var sb = new StringBuilder();
        sb.AppendLine("标准目录文件夹树（文档可能落在哪一层）：");
        AppendFolderLevel(sb, byParent, string.Empty, 0);
        return sb.ToString().TrimEnd();
    }

    private static void AppendFolderLevel(
        StringBuilder sb, Dictionary<string, List<StandardDirectoryFolder>> byParent,
        string parentCode, int depth)
    {
        if (depth > 6) return; // 防环
        if (!byParent.TryGetValue(parentCode, out var kids)) return;
        foreach (var f in kids)
        {
            sb.Append(new string(' ', depth * 2)).Append("- ").AppendLine(f.FolderName ?? "(未命名)");
            AppendFolderLevel(sb, byParent, f.Code ?? "", depth + 1);
        }
    }

    // ========================================================
    // 四、内部辅助
    // ========================================================

    /// <summary>组装 <c>{{file_list}}</c> 注入内容：文件名 + 标题（首个 # 行）+ 开头片段</summary>
    private static string BuildFileListJson(List<(string FileName, string Markdown)> files)
    {
        var sb = new StringBuilder();
        sb.AppendLine("共 ").Append(files.Count).AppendLine(" 个文件：");
        sb.AppendLine();
        for (var i = 0; i < files.Count; i++)
        {
            var (name, md) = files[i];
            sb.Append("### ").Append(i + 1).AppendLine(". " + name);
            sb.Append("标题：").AppendLine(ExtractTitle(md, name));
            sb.Append("开头片段：").AppendLine(Head(md, HeadSnippetChars));
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    /// <summary>取 Markdown 的标题：首个 <c>#</c> 行；没有则回退文件名去扩展名</summary>
    private static string ExtractTitle(string markdown, string fileName)
    {
        foreach (var raw in markdown.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith('#'))
            {
                var t = line.TrimStart('#').Trim();
                if (t.Length > 0) return t;
            }
        }
        var dot = fileName.LastIndexOf('.');
        return dot > 0 ? fileName[..dot] : fileName;
    }

    /// <summary>取前 N 个字符（压掉多余空行）</summary>
    private static string Head(string text, int max)
    {
        var compact = Regex.Replace(text, @"\n{3,}", "\n\n").Trim();
        return compact.Length <= max ? compact : compact[..max] + "…";
    }

    /// <summary>剥掉 ``` 围栏（模型偶发）</summary>
    private static string StripFence(string? content)
    {
        var t = (content ?? string.Empty).Trim();
        if (!t.StartsWith("```")) return t;
        var firstNl = t.IndexOf('\n');
        if (firstNl > 0) t = t[(firstNl + 1)..].Trim();
        if (t.EndsWith("```")) t = t[..^3].Trim();
        return t;
    }

    /// <summary>标准 Code（GUID）→ 展示名；空则返回空串</summary>
    private async Task<string> ResolveStandardNameAsync(string? standardCode)
    {
        if (string.IsNullOrWhiteSpace(standardCode)) return string.Empty;
        var r = await _db.GetOneAsync<ISOStandard>(x => x.Code == standardCode);
        return r.Success && r.Data != null ? $"{r.Data.StandardName}（{r.Data.StandardCode}）" : standardCode!;
    }

    /// <summary>AI 连接配置（cert_sys_config 六键，与 DocExtractionRuleService 同源）</summary>
    /// <para>★ 2026-10-02 起**统一 AI 配置**：提示词行上的 ModelName/MaxTokens/Temperature 不再参与本工作台。</para>
    private class AiSettings
    {
        public string ApiKey { get; set; } = "";
        public string BaseUrl { get; set; } = "https://dashscope.aliyuncs.com/compatible-mode/v1";
        public string Model { get; set; } = "qwen-flash";
        /// <summary>AI 提供方（cert_sys_config: ai_provider），仅用于落 usage_log</summary>
        public string Provider { get; set; } = "qianwen";
        public int MaxTokens { get; set; } = 32768;
        public float Temperature { get; set; } = 0.2f;
    }

    private class ConfigKV
    {
        public string ConfigKey { get; set; } = "";
        public string? ConfigValue { get; set; }
    }

    private async Task<AiSettings> GetAiSettingsAsync()
    {
        var s = new AiSettings();
        var rows = await _db.Client.Queryable<SysConfig>()
            .Where(x => x.Category == "ai_model" && !x.IsDeleted)
            .Select(x => new ConfigKV { ConfigKey = x.ConfigKey, ConfigValue = x.ConfigValue })
            .ToListAsync();

        foreach (var row in rows)
        {
            switch (row.ConfigKey)
            {
                case "ai_api_key": s.ApiKey = row.ConfigValue ?? ""; break;
                case "ai_base_url": s.BaseUrl = row.ConfigValue ?? s.BaseUrl; break;
                case "ai_model_name": s.Model = row.ConfigValue ?? s.Model; break;
                case "ai_provider": s.Provider = row.ConfigValue ?? s.Provider; break;
                case "ai_max_tokens":
                    if (int.TryParse(row.ConfigValue, out var mt)) s.MaxTokens = mt;
                    break;
                case "ai_temperature":
                    if (float.TryParse(row.ConfigValue, out var tp)) s.Temperature = tp;
                    break;
            }
        }
        return s;
    }

    /// <summary>
    /// 写 <c>cert_ai_usage_log</c>（费用统计）。**日志失败不影响主流程**。
    /// <para>口径与 <c>DocExtractionRuleService.LogAiUsageAsync</c> 一致；<c>CostUsd</c> 暂恒 0（全仓无单价表）。</para>
    /// </summary>
    private async Task LogUsageAsync(
        string businessType, string businessRef, AiSettings settings, string model,
        LlmInvokeResponse result)
    {
        try
        {
            var log = new AiUsageLog
            {
                CallId = Guid.NewGuid().ToString("N"),
                Code = Guid.NewGuid().ToString("N"),
                BusinessType = businessType,
                BusinessRef = businessRef,
                Skill = "prompt_workbench",
                Provider = string.IsNullOrWhiteSpace(settings.Provider) ? "qianwen" : settings.Provider,
                Model = model,
                PromptTokens = result.PromptTokens ?? 0,
                CompletionTokens = result.CompletionTokens ?? 0,
                TotalTokens = (result.PromptTokens ?? 0) + (result.CompletionTokens ?? 0),
                DurationMs = result.DurationMs,
                Success = result.Success,
                ErrorMessage = result.Success ? null : Truncate(result.Message, 500),
                CreateTime = DateTime.Now
            };
            await _db.Client.Insertable(log).ExecuteCommandAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[PromptWorkbench] AI 用量日志写入失败");
        }
    }

    private static string? Truncate(string? s, int max)
        => string.IsNullOrEmpty(s) ? null : (s.Length <= max ? s : s[..max]);

    // ========================================================
    // 五、结果模型
    // ========================================================
    //
    // ⚠️ 为什么每个属性都显式标 [JsonPropertyName]（不要省）：
    //   本项目的 MVC JSON 选项是 PropertyNamingPolicy = null（PascalCase），
    //   而 DTO 惯例是 camelCase（如 PromptTemplateDto）。两者混在一个端点里
    //   ⇒ 前端「读 data.Prompt 得 undefined、页面空着、零报错」——
    //   正是 §二十 那一类静默失败。显式标注后**契约由代码自证**，不靠记忆。
    // ========================================================

    public class GenerateResult
    {
        [JsonPropertyName("success")] public bool Success { get; set; }
        [JsonPropertyName("message")] public string Message { get; set; } = "";
        /// <summary>生成的提示词正文（★ 不落库，由用户确认后保存）</summary>
        [JsonPropertyName("prompt")] public string? Prompt { get; set; }
        [JsonPropertyName("durationMs")] public long DurationMs { get; set; }
        [JsonPropertyName("promptTokens")] public int? PromptTokens { get; set; }
        [JsonPropertyName("completionTokens")] public int? CompletionTokens { get; set; }

        public static GenerateResult Ok(string prompt, long ms, int? pt, int? ct) => new()
        {
            Success = true, Message = "生成成功", Prompt = prompt,
            DurationMs = ms, PromptTokens = pt, CompletionTokens = ct
        };

        public static GenerateResult Fail(string message, long ms = 0) => new()
        {
            Success = false, Message = message, DurationMs = ms
        };
    }

    public class ConvertLogItem
    {
        [JsonPropertyName("fileName")] public string FileName { get; set; } = "";
        [JsonPropertyName("success")] public bool Success { get; set; }
        [JsonPropertyName("message")] public string? Message { get; set; }
        [JsonPropertyName("markdownLength")] public int MarkdownLength { get; set; }
        /// <summary>转换出的 Markdown 开头（供列表摘要显示，800 字）</summary>
        [JsonPropertyName("markdownHead")] public string? MarkdownHead { get; set; }
        /// <summary>
        /// ★ 完整 Markdown 正文（2026-10-02 新增）。
        /// 后端已把它写入 Redis 缓存；前端可展示预览，**但不需要自行保管** —— 复用靠 <c>cacheKey</c>。
        /// </summary>
        [JsonPropertyName("markdown")] public string? Markdown { get; set; }
    }

    /// <summary>
    /// 队列语义分析结果（<see cref="AnalyzeForQueueAsync"/>）。
    /// <para>⚠️ 字段名<b>不加前缀</b>，与 <see cref="TestResult"/> 区分靠类名而非字段名 ——
    /// 下游（executor）读的都是 PascalCase，加前缀反而要写两套映射。</para>
    /// </summary>
    public class AnalyzeForQueueResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        /// <summary>模型名（快照落画像的 <c>ModelName</c>）</summary>
        public string Model { get; set; } = "";
        public string PromptCode { get; set; } = "";
        public int PromptVersion { get; set; } = 1;
        /// <summary>经六条校验修复后的 JSON（<b>落库用这个</b>，不是 RawOutput）</summary>
        public string? Json { get; set; }
        /// <summary>LLM 原始返回（仅留档，不落画像）</summary>
        public string? RawOutput { get; set; }
        public long DurationMs { get; set; }
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        /// <summary>后端自动校正明细（写 <c>TagsReason</c> / 返回前端展示）</summary>
        public List<string> ValidationMessages { get; set; } = new();

        public static AnalyzeForQueueResult Fail(string message, LlmInvokeResponse? resp = null, string? raw = null)
            => new()
            {
                Success = false,
                Message = message,
                RawOutput = raw ?? resp?.Content,
                DurationMs = resp?.DurationMs ?? 0,
                PromptTokens = (int)(resp?.PromptTokens ?? 0),
                CompletionTokens = (int)(resp?.CompletionTokens ?? 0),
            };
    }

    public class TestResult
    {
        [JsonPropertyName("success")] public bool Success { get; set; }
        [JsonPropertyName("message")] public string Message { get; set; } = "";
        [JsonPropertyName("model")] public string? Model { get; set; }
        [JsonPropertyName("maxTokens")] public int? MaxTokens { get; set; }
        [JsonPropertyName("temperature")] public float? Temperature { get; set; }
        /// <summary>实际送出的提示词全文（调试用，可确认占位符替换正确）</summary>
        [JsonPropertyName("promptText")] public string? PromptText { get; set; }
        [JsonPropertyName("rawOutput")] public string? RawOutput { get; set; }
        /// <summary>解析后的 JSON（ForceJson 成功时）</summary>
        [JsonPropertyName("jsonOutput")] public string? JsonOutput { get; set; }
        [JsonPropertyName("durationMs")] public long DurationMs { get; set; }
        [JsonPropertyName("promptTokens")] public int? PromptTokens { get; set; }
        [JsonPropertyName("completionTokens")] public int? CompletionTokens { get; set; }
        [JsonPropertyName("files")] public List<ConvertLogItem> Files { get; set; } = new();
        /// <summary>
        /// ★ Redis 缓存键（2026-10-02 新增）。前端存起来，改提示词「重新分析」时回传即可复用 Markdown。
        /// </summary>
        [JsonPropertyName("cacheKey")] public string? CacheKey { get; set; }
        /// <summary>本次是否命中缓存（true = 没重新转换）</summary>
        [JsonPropertyName("cacheHit")] public bool CacheHit { get; set; }
        /// <summary>★ 33 号 §3.3 后端六条校验结果（越界回退 / clamp / 截断 / 缺作用）</summary>
        [JsonPropertyName("validation")] public ValidationInfo? Validation { get; set; }

        public static TestResult Fail(string message, List<ConvertLogItem>? files = null, string? cacheKey = null) => new()
        {
            Success = false,
            Message = message,
            Files = files ?? new List<ConvertLogItem>(),
            CacheKey = cacheKey
        };
    }

    /// <summary>语义输出校验结果（33 号 §3.3）</summary>
    public class ValidationInfo
    {
        /// <summary>false = 输出不是合法 JSON，前端应**硬拦保存**</summary>
        [JsonPropertyName("valid")] public bool Valid { get; set; }
        /// <summary>校正/告警明细；每条对应一次后端修复</summary>
        [JsonPropertyName("messages")] public List<string> Messages { get; set; } = new();
        /// <summary>校验修复后的 JSON（已回写 OTHER / clamp / 截断）</summary>
        [JsonPropertyName("normalizedJson")] public string? NormalizedJson { get; set; }
    }
}

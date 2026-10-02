using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SqlSugar;
using CertPlatform.Shared.DocExtraction;
using CertPlatform.Shared.Entities.Cert;
using CertPlatform.Shared.Entities.Wf;
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
///   <item><b>AI 自动生成</b>（<see cref="GenerateAsync"/>）—— 用 <c>prompt_generator</c> 元提示词，
///         按「提示词类型 + 适用标准」生成 <c>doc_group</c>（分类）或 <c>doc_content</c>（作用）草稿。
///         ★ 生成结果<b>不落库</b>，由用户确认后自行保存。</item>
///   <item><b>上传试跑</b>（<see cref="TestAsync"/>）—— 文件字节 → <see cref="DocumentConvertClient"/> 转 Markdown
///         → 渲染提示词 → <see cref="LlmInvokeService"/> 调用 → 返回结果。
///         ★ 文件<b>只落转换容器的临时会话目录</b>（<c>DocumentConvertClient</c> 内部 finally 已清理），
///         <b>不落 MinIO、不落 DB、不留痕</b> —— 满足用户「后直接删除该文件」的要求。</item>
/// </list>
///
/// <para><b>模型参数优先级</b>（用户：「支持在控制成本情况下合理切换模型，并设置模型参数」）：</para>
/// <code>
/// 提示词行上的 ModelName / MaxTokens / Temperature   ← 最具体
///        ↓ 为 NULL 时回退
/// cert_sys_config 六键（ai_model_name / ai_max_tokens / ai_temperature）  ← 系统默认
/// </code>
///
/// <para><b>提示词定位（三层回退）</b>：<see cref="ResolveActiveAsync"/> 按「标准级 → 平台级」逐层回退，
/// 口径与全局参数（<c>ParamValueResolver.PickMostSpecific</c>）同构。</para>
/// </summary>
public class PromptWorkbenchService
{
    private readonly IDbOrm _db;
    private readonly LlmInvokeService _llm;
    private readonly DocumentConvertClient _convert;
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
    }

    /// <summary>单次试跑最多接受的文件数（防超时 / 防 token 爆）</summary>
    private const int MaxTestFiles = 20;

    /// <summary>文件清单里每个文件注入的开头片段长度</summary>
    private const int HeadSnippetChars = 300;

    public PromptWorkbenchService(
        IDbOrm db,
        LlmInvokeService llm,
        DocumentConvertClient convert,
        ILogger<PromptWorkbenchService> logger)
    {
        _db = db;
        _llm = llm;
        _convert = convert;
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
    /// 用 <c>prompt_generator</c> 元提示词生成一条提示词草稿（**不落库**）。
    /// </summary>
    /// <param name="promptType">要生成的类型：<see cref="Types.Group"/> / <see cref="Types.Content"/></param>
    /// <param name="standardCode">适用标准（GUID）；仅用于给元提示词提供上下文</param>
    /// <param name="extraRequirement">额外要求（可选，用户自由输入）</param>
    public async Task<GenerateResult> GenerateAsync(string promptType, string? standardCode, string? extraRequirement)
    {
        if (promptType != Types.Group && promptType != Types.Content)
            return GenerateResult.Fail($"不支持生成的提示词类型：{promptType}（只支持 doc_group / doc_content）");

        var generator = await ResolveActiveAsync(Types.Generator, null);
        if (generator == null || string.IsNullOrWhiteSpace(generator.Template))
            return GenerateResult.Fail("未找到生效的元提示词（PromptCode = prompt_generator），请先执行提示词种子脚本");

        var standardName = await ResolveStandardNameAsync(standardCode);
        var typeName = promptType == Types.Group ? "分类提示词（doc_group）" : "作用提示词（doc_content）";

        var context = new Dictionary<string, object>
        {
            ["prompt_type"] = promptType,
            ["prompt_type_name"] = typeName,
            ["standard_name"] = string.IsNullOrWhiteSpace(standardName) ? "通用（未指定标准）" : standardName,
            ["extra_requirement"] = string.IsNullOrWhiteSpace(extraRequirement) ? "（无）" : extraRequirement!
        };

        var prompt = PromptRenderer.Render(generator.Template, context);

        var settings = await GetAiSettingsAsync();
        var resp = await _llm.CompleteAsync(new LlmInvokeRequest
        {
            BaseUrl = settings.BaseUrl,
            ApiKey = settings.ApiKey,
            Model = generator.ModelName ?? settings.Model,
            Temperature = (float)(generator.Temperature ?? (decimal)settings.Temperature),
            MaxTokens = generator.MaxTokens ?? settings.MaxTokens,
            Prompt = prompt,
            // ★ 生成的是「提示词正文」，不是 JSON —— 必须关掉 ForceJson，
            //   否则 dashscope 会强加 response_format=json_object，把提示词包成 JSON 字符串
            ForceJson = false
        });

        if (!resp.Success)
            return GenerateResult.Fail(resp.Message, resp.DurationMs);

        // 防御：模型偶尔仍会套 ``` 围栏
        var text = StripFence(resp.Content);
        return GenerateResult.Ok(text, resp.DurationMs, resp.PromptTokens, resp.CompletionTokens);
    }

    // ========================================================
    // 三、上传试跑（转 Markdown → 跑提示词 → 文件即弃）
    // ========================================================

    /// <summary>
    /// 试跑一条提示词。
    /// <para><b>分类提示词</b>：可传多文件 → 逐个转 Markdown 取「标题 + 开头片段」→ 组装文件清单 → 注入 <c>{{file_list}}</c></para>
    /// <para><b>作用提示词</b>：取第一个文件 → 转 Markdown 全文 → 注入 <c>{{document_content}}</c></para>
    /// </summary>
    public async Task<TestResult> TestAsync(
        string promptType, string? templateOverride, string? standardCode,
        IReadOnlyList<(string FileName, byte[] Content)> files)
    {
        if (files == null || files.Count == 0)
            return TestResult.Fail("请先选择要测试的文件");

        if (files.Count > MaxTestFiles)
            return TestResult.Fail($"一次最多测试 {MaxTestFiles} 个文件（当前 {files.Count} 个），请分批测试");

        // 1. 提示词正文：优先用页面上「未保存的编辑内容」（边改边试），否则用库里的生效版本
        var template = templateOverride;
        PromptTemplate? promptRow = null;
        if (string.IsNullOrWhiteSpace(template))
        {
            promptRow = await ResolveActiveAsync(promptType, standardCode);
            if (promptRow == null || string.IsNullOrWhiteSpace(promptRow.Template))
                return TestResult.Fail($"未找到生效的「{promptType}」提示词，请先填写提示词内容");
            template = promptRow.Template;
        }

        // 2. 逐个转 Markdown（★ DocumentConvertClient 内部会话目录 finally 已清理 ⇒ 文件即弃）
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

            var md = Encoding.UTF8.GetString(r.Content);
            converted.Add((fileName, md, null));
        }

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
                MarkdownHead = c.Markdown == null ? null : Head(c.Markdown, 800)
            });

        if (promptType == Types.Group)
        {
            var usable = converted.Where(x => x.Markdown != null).ToList();
            if (usable.Count == 0)
                return TestResult.Fail("所选文件均未能转换为 Markdown，无法测试分类提示词", convertLog);

            var fileList = BuildFileListJson(usable.Select(x => (x.FileName, x.Markdown!)).ToList());
            prompt = template!.Contains("{{file_list}}", StringComparison.Ordinal)
                ? template.Replace("{{file_list}}", fileList)
                : template + "\n\n" + fileList;
        }
        else
        {
            var first = converted.FirstOrDefault(x => x.Markdown != null);
            if (first.Markdown == null)
                return TestResult.Fail($"文件未能转换为 Markdown：{first.Error ?? "未知原因"}", convertLog);

            prompt = template!.Contains("{{document_content}}", StringComparison.Ordinal)
                ? template.Replace("{{document_content}}", first.Markdown)
                : template + "\n\n---\n" + first.Markdown + "\n---";
        }

        // 4. 调 LLM（模型参数：提示词行覆盖 > 系统默认）
        var settings = await GetAiSettingsAsync();
        var model = promptRow?.ModelName ?? settings.Model;
        var maxTokens = promptRow?.MaxTokens ?? settings.MaxTokens;
        var temperature = (float)(promptRow?.Temperature ?? (decimal)settings.Temperature);

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
            promptType, files.Count, resp.Success, resp.DurationMs);

        return new TestResult
        {
            Success = resp.Success,
            Message = resp.Success ? "测试完成" : resp.Message,
            Model = model,
            MaxTokens = maxTokens,
            Temperature = temperature,
            PromptText = prompt,
            RawOutput = resp.Content,
            JsonOutput = resp.Json?.RootElement.GetRawText(),
            DurationMs = resp.DurationMs,
            PromptTokens = resp.PromptTokens,
            CompletionTokens = resp.CompletionTokens,
            Files = convertLog
        };
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
    private class AiSettings
    {
        public string ApiKey { get; set; } = "";
        public string BaseUrl { get; set; } = "https://dashscope.aliyuncs.com/compatible-mode/v1";
        public string Model { get; set; } = "qwen-turbo";
        public int MaxTokens { get; set; } = 4096;
        public float Temperature { get; set; } = 0.7f;
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
        /// <summary>转换出的 Markdown 开头（供界面确认「文件确实被读到了」）</summary>
        [JsonPropertyName("markdownHead")] public string? MarkdownHead { get; set; }
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

        public static TestResult Fail(string message, List<ConvertLogItem>? files = null) => new()
        {
            Success = false, Message = message, Files = files ?? new List<ConvertLogItem>()
        };
    }
}

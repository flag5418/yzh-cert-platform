using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using YZH.Core.Api.Controllers;
using YZH.Core.Api.Services;
using YZH.Core.Stand.Helpers;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models;
using YZH.Core.Stand.Models.Config;
using YZH.Core.Stand.Models.Request;
using YZH.Core.Stand.Models.Result;
using YZH.Core.Api.Models.System;
using YZH.Core.DataBase.Interfaces;
using CertPlatform.Admin.Entities.Doc;
using CertPlatform.Admin.Entities.Workflow;
using CertPlatform.Admin.Services.Cert;
using CertPlatform.Admin.Services.Workflow.Skills.Fill.Ai;
using CertPlatform.Shared.DocExtraction;

namespace CertPlatform.Admin.Controllers.Workflow;

/// <summary>
///     文档提取规则管理控制器
///
///     功能：
///     - 每标准一行，存储该国标 Markdown 知识库 + 提取提示词
///     - 标准 CRUD（左树数据源）
///     - /test：上传文档 → LLM → 返回 JSON（不落库）
///     - /prompt：从 cert_prompt_template 读取/保存提示词（作用域 = 标准Code）
///
///     路由前缀：/api/Admin/Workflow/DocExtractRule
///     末段 DocExtractRule ⇒ ApiCode 按框架规则生成
///
///     数据库：cert_doc_extract_rules（实体）+ cert_prompt_template（提示词）
///     前端路由：/business/doc-extract-rule
///
///     ★ 提示词存储策略（2026-10-29 统一）：
///       全局默认 ⇒ PromptCode=doc_clause_extract, ScopeKey=空
///       标准级   ⇒ PromptCode=doc_clause_extract, ScopeKey=标准Code（如 iso9001）
///       页面编辑的正文 = Auditor 侧 ClauseExtractExecutor 真正消费的那条（2026-11-10 从 doc_extract 切过来）
///       优先匹配标准级，缺失回落到全局默认。
/// </summary>
[ApiController]
[Route("api/Admin/Workflow/[controller]")]
public class DocExtractRuleController : YzhControllerBase<DocExtractRule>
{
    private readonly PromptTemplateService _promptTemplate;
    private readonly IFileConvertCore _convert;
    private readonly LlmInvokeService _llm;
    private readonly IDbOrm _db;
    private readonly ILogger<DocExtractRuleController> _logger;

    /// <summary>
    /// 注册页面要展示的占位符（与 DocExtractPromptService 的替换常量逐字一致）。
    /// <para>⚠️ 只影响前端提示，不参与渲染；渲染在 Auditor 侧 DocExtractPromptService.Render。</para>
    /// </summary>
    static DocExtractRuleController()
    {
        PromptTemplateService.RegisterPlaceholder("doc_clause_extract", "{{standard_name}}", "标准名称，如 GB/T 19001-2016 质量管理体系");
        PromptTemplateService.RegisterPlaceholder("doc_clause_extract", "{{standard_code}}", "标准短码，如 iso9001");
        PromptTemplateService.RegisterPlaceholder("doc_clause_extract", "{{document_name}}", "被分析文档的文件名");
        PromptTemplateService.RegisterPlaceholder("doc_clause_extract", "{{clause_list}}", "可命中的条款清单（编号 + 标题，一行一条）");
        PromptTemplateService.RegisterPlaceholder("doc_clause_extract", "{{document_content}}", "文档 Markdown 全文");
    }

    public DocExtractRuleController(
        EntityService<DocExtractRule> entityService,
        IUserContext userContext,
        PromptTemplateService promptTemplate,
        IFileConvertCore convert,
        LlmInvokeService llm,
        IDbOrm db,
        ILogger<DocExtractRuleController> logger)
        : base(entityService, userContext)
    {
        _promptTemplate = promptTemplate;
        _convert = convert;
        _llm = llm;
        _db = db;
        _logger = logger;
    }

    /// <summary>加载 EntityConfig（Workflow/DocExtractRule.json）</summary>
    protected override EntityConfig LoadConfig()
    {
        return EntityConfigHelper.GetConfig("Workflow/DocExtractRule");
    }

    /// <summary>★ 开发期强制暴露缺配置问题</summary>
    protected override bool StrictConfigLoad => true;

    /// <summary>
    /// 修改前校验：不能改 StandardCode（标准编码是"每标准一行"的锚点）
    /// 标准变了 → 应该新增一条，而不是改原来的
    /// </summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeUpdate(DocExtractRule entity)
    {
        var existing = await Entity.GetByCode(entity.Code);
        if (existing.Success && existing.Data != null && existing.Data.StandardCode != entity.StandardCode)
            return (false, "标准编码不可修改");
        return (true, null);
    }

    /// <summary>
    /// 删除前校验：检查是否存在测试数据关联（当前无关联表，此处预留钩子）
    /// </summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeDelete(string[] codes)
    {
        // TODO: 未来等 cert_doc_extract 等表上线后，检查关联数据
        return (true, null);
    }

    /// <summary>
    /// 列表查询后脱敏 MarkdownContent / PromptContent（太大，不在列表中传输全文）。
    /// ⚠️ 排除 "按 Code 取单条详情" 的场景——那个端点就是为取全文存在的。
    /// </summary>
    protected override void OnQueried(PagedResult<DocExtractRule> result)
    {
        // ★ 只对多行列表脱敏；单行详情（code 过滤器命中 1 条）保留全文。
        //   前端 getRuleDetail 复用 /filter 端点 + Code=eq 过滤器，故以 Items.Count=1 + 有 Code 过滤器判定。
        if (result.Items.Count > 1)
        {
            foreach (var row in result.Items)
            {
                row.MarkdownContent = null;
                row.PromptContent = null;
            }
        }
    }

    /// <summary>
    /// ★ 取单条详情（含 MarkdownContent + PromptContent 全文）。
    /// 前端点左树节点时按需调用，绕过 OnQueried 的列表脱敏。
    /// </summary>
    [HttpGet("detail/{code}")]
    public async Task<IActionResult> GetDetail(string code)
    {
        var result = await Entity.GetByCode(code);
        if (!result.Success || result.Data == null)
            return Ok(ApiResponse<DocExtractRule?>.Fail("记录不存在", 404));
        return Ok(ApiResponse<DocExtractRule?>.Ok(result.Data));
    }

    // ════════════════════════════════════════════════════════════════════
    // 提示词维护（委托通用 PromptTemplateService，PromptCode = "doc_clause_extract"）
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 读取某标准的文档提取提示词。
    /// <para>标准级（ScopeKey=标准Code）优先；缺失回落到全局默认（ScopeKey=空）。</para>
    /// </summary>
    /// <param name="standardCode">标准短码（如 iso9001）；空 = 取全局默认</param>
    [HttpGet("prompt")]
    public async Task<IActionResult> GetPrompt([FromQuery] string? standardCode = null)
    {
        var template = await _promptTemplate.GetAsync("doc_clause_extract", null, standardCode);
        return Ok(ApiResponse<DocExtractPromptDto>.Ok(new DocExtractPromptDto
        {
            PromptCode = "doc_clause_extract",
            StandardCode = standardCode,
            Template = template.Template,
            IsFallback = template.Status == "fallback",
            Placeholders = PromptTemplateService.GetPlaceholders("doc_clause_extract"),
        }));
    }

    /// <summary>
    /// 保存某标准的文档提取提示词（幂等 upsert）。
    /// <para>standardCode 空 = 保存全局默认；非空 = 保存该标准的专属提示词。</para>
    /// </summary>
    [HttpPost("prompt")]
    public async Task<IActionResult> SavePrompt([FromBody] DocExtractPromptSaveRequest request)
    {
        var (ok, message) = await _promptTemplate.SaveAsync(
            "doc_clause_extract",
            request.Template,
            null,
            request.StandardCode,
            "文档提取提示词",
            string.IsNullOrEmpty(request.StandardCode)
                ? "文档提取提示词的全局默认模板"
                : $"标准【{request.StandardCode}】的专属提取提示词",
            UserContext.UserCode);

        return ok
            ? Ok(ApiResponse.Ok(message))
            : Ok(ApiResponse.BizFail(message));
    }

    // ════════════════════════════════════════════════════════════════════
    // ★ 页面内试跑（2026-11-10）：上传文档 → 转 Markdown → 用「当前编辑正文」跑一次
    // ════════════════════════════════════════════════════════════════════

    /// <summary>单次试跑送模型的文档字符上限（与 ClauseExtractExecutor.ChunkChars 同口径 3 万字）</summary>
    private const int MaxTestDocChars = 30_000;

    /// <summary>
    /// <b>试跑文档提取提示词</b>（不落库、不写队列）。
    ///
    /// <para>入参 = 文件 + 标准短码 + <b>页面当前正文</b>（未保存也能试，实现边改边看）。</para>
    /// <para>链路与线上同源：<c>IFileConvertCore.ConvertToMarkdownInlineAsync</c>（anydoc → OCR 兜底），
    /// 不是裸 anydoc —— 否则扫描件在测试环境永远测不出真实结果。</para>
    /// </summary>
    [HttpPost("prompt/test")]
    [RequestSizeLimit(200_000_000)]
    public async Task<IActionResult> TestPrompt([FromForm] DocExtractPromptTestRequest req)
    {
        if (req == null || req.File == null || req.File.Length == 0)
            return Ok(ApiResponse.Fail("请先选择要测试的文档"));
        if (string.IsNullOrWhiteSpace(req.Template))
            return Ok(ApiResponse.Fail("提示词正文为空：请先在编辑器里填写提示词再试跑"));

        // 1) 文件 → Markdown（与线上同一条转换内核）
        byte[] bytes;
        using (var ms = new MemoryStream())
        {
            await req.File.CopyToAsync(ms);
            bytes = ms.ToArray();
        }
        var conv = await _convert.ConvertToMarkdownInlineAsync(req.File.FileName, bytes);
        if (!conv.Success || conv.Content == null || conv.Content.Length == 0)
            return Ok(ApiResponse.Fail("文档转换失败：" + (string.IsNullOrWhiteSpace(conv.Message) ? "未知原因" : conv.Message)));

        var markdown = Encoding.UTF8.GetString(conv.Content);
        var truncated = markdown.Length > MaxTestDocChars;
        var docForModel = truncated ? markdown.Substring(0, MaxTestDocChars) : markdown;

        // 2) 标准信息（页面传的是短码 iso9001；也容忍 GUID）
        var stdList = await _db.Client.Ado.SqlQueryAsync<StdRow>(
            "SELECT Code, StandardCode, StandardName FROM cert_iso_standard "
            + "WHERE (StandardCode = @key OR Code = @key) AND IsDeleted = 0 AND IsValid = 1 LIMIT 1",
            new { key = (req.StandardCode ?? "").Trim() });
        var std = stdList?.FirstOrDefault();
        var stdName = std?.StandardName ?? (req.StandardCode ?? "未知标准");
        var stdGuid = std?.Code ?? "";

        // 3) 条款清单（只给编号 + 标题，供模型选；⛔ 不带任何“该不该查”的判据）
        var clauseRows = string.IsNullOrWhiteSpace(stdGuid)
            ? new List<ClauseRow>()
            : await _db.Client.Ado.SqlQueryAsync<ClauseRow>(
                "SELECT ClauseNumber, Title FROM cert_iso_clause "
                + "WHERE StandardCode = @guid AND IsValid = 1 AND IsDeleted = 0 "
                + "GROUP BY ClauseNumber, Title ORDER BY MIN(Sort)",
                new { guid = stdGuid });
        var clauseList = string.Join("\n", (clauseRows ?? new List<ClauseRow>())
            .Where(c => !string.IsNullOrWhiteSpace(c.ClauseNumber))
            .Select(c => "- " + c.ClauseNumber.Trim() + (string.IsNullOrWhiteSpace(c.Title) ? "" : " " + c.Title!.Trim())));

        // 4) 渲染占位符（与 DocExtractPromptService.Render 同口径，Ordinal 替换）
        var prompt = req.Template
            .Replace("{{standard_name}}", stdName, StringComparison.Ordinal)
            .Replace("{{standard_code}}", req.StandardCode ?? "", StringComparison.Ordinal)
            .Replace("{{document_name}}", req.File.FileName ?? "", StringComparison.Ordinal)
            .Replace("{{clause_list}}", clauseList, StringComparison.Ordinal)
            .Replace("{{document_content}}", docForModel, StringComparison.Ordinal);

        // 5) 调模型（统一 AI 配置 cert_sys_config.ai_*）
        var ai = await ReadAiSettingsAsync();
        var resp = await _llm.CompleteAsync(new LlmInvokeRequest
        {
            BaseUrl = ai.BaseUrl,
            ApiKey = ai.ApiKey,
            Model = ai.Model,
            Temperature = ai.Temperature,
            MaxTokens = ai.MaxTokens,
            Prompt = prompt,
            ForceJson = true,
            // ★ 2026-11-11：真实手册（2.7 万字）试跑实测 137s，固定 180s 余量太小；
            //   与线上提取同口径给 600s，避免「测试跑一半被判超时」。
            TimeoutSeconds = 600,
        });

        await LogUsageAsync(ai, resp, req.StandardCode ?? "");

        // 6) JSON 合法性（只判“是不是一个 JSON 对象”，不判内容对错）
        var raw = string.IsNullOrWhiteSpace(resp.Content) ? "" : StripFence(resp.Content);
        bool jsonValid = false;
        if (raw.Length > 0)
        {
            try { using var _ = JsonDocument.Parse(raw); jsonValid = true; }
            catch (JsonException) { jsonValid = false; }
        }

        return Ok(ApiResponse<object>.Ok(new
        {
            Success = resp.Success,
            Message = resp.Success ? "试跑完成" : resp.Message,
            Model = ai.Model,
            DurationMs = resp.DurationMs,
            PromptTokens = resp.PromptTokens ?? 0,
            CompletionTokens = resp.CompletionTokens ?? 0,
            JsonValid = jsonValid,
            Output = raw,
            PromptChars = prompt.Length,
            ClauseCount = clauseRows?.Count ?? 0,
            MarkdownLength = markdown.Length,
            Truncated = truncated,
            MarkdownHead = markdown.Length > 1200 ? markdown.Substring(0, 1200) : markdown,
            FileName = req.File.FileName,
        }, resp.Success ? "试跑完成" : resp.Message));
    }

    /// <summary>AI 配置（口径与 PromptWorkbenchService / DocFillPromptController 一致）</summary>
    private async Task<AiCfg> ReadAiSettingsAsync()
    {
        var s = new AiCfg();
        var rows = await _db.Client.Queryable<SysConfig>()
            .Where(x => x.Category == "ai_model" && !x.IsDeleted)
            .Select(x => new { x.ConfigKey, x.ConfigValue })
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

    /// <summary>计费留痕（失败不影响主流程）</summary>
    private async Task LogUsageAsync(AiCfg cfg, LlmInvokeResponse resp, string standardCode)
    {
        try
        {
            var log = new AiUsageLog
            {
                CallId = Guid.NewGuid().ToString("N"),
                Code = Guid.NewGuid().ToString("N"),
                BusinessType = "doc_extract_test",
                BusinessRef = "doc_extract_prompt_test:" + (standardCode ?? ""),
                Skill = "doc_extract_prompt_test",
                Provider = string.IsNullOrWhiteSpace(cfg.Provider) ? "qianwen" : cfg.Provider,
                Model = cfg.Model,
                PromptTokens = resp.PromptTokens ?? 0,
                CompletionTokens = resp.CompletionTokens ?? 0,
                TotalTokens = (resp.PromptTokens ?? 0) + (resp.CompletionTokens ?? 0),
                DurationMs = resp.DurationMs,
                Success = resp.Success,
                ErrorMessage = resp.Success ? null : (resp.Message ?? ""),
                CreateTime = DateTime.Now,
            };
            await _db.Client.Insertable(log).ExecuteCommandAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[DocExtractRule] 试跑计费落库失败（不影响结果）");
        }
    }

    /// <summary>去 ```json 围栏</summary>
    private static string StripFence(string text)
    {
        var t = (text ?? "").Trim();
        if (!t.StartsWith("```", StringComparison.Ordinal)) return t;
        var nl = t.IndexOf('\n');
        if (nl < 0) return t;
        t = t.Substring(nl + 1);
        var end = t.LastIndexOf("```", StringComparison.Ordinal);
        return (end >= 0 ? t.Substring(0, end) : t).Trim();
    }

    private sealed class AiCfg
    {
        public string ApiKey { get; set; } = "";
        public string BaseUrl { get; set; } = "https://dashscope.aliyuncs.com/compatible-mode/v1";
        public string Model { get; set; } = "qwen-flash";
        public string Provider { get; set; } = "qianwen";
        public int MaxTokens { get; set; } = 32768;
        public float Temperature { get; set; } = 0.2f;
    }

    private sealed class StdRow
    {
        public string Code { get; set; } = "";
        public string StandardCode { get; set; } = "";
        public string StandardName { get; set; } = "";
    }

    private sealed class ClauseRow
    {
        public string ClauseNumber { get; set; } = "";
        public string? Title { get; set; }
    }
}

// ════════════════════════════════════════════════════════════════════════
// 契约 DTO
// ════════════════════════════════════════════════════════════════════════

/// <summary>文档提取提示词读取结果</summary>
public sealed class DocExtractPromptDto
{
    public string PromptCode { get; set; } = "doc_extract";
    public string? StandardCode { get; set; }
    public string Template { get; set; } = string.Empty;
    public bool IsFallback { get; set; }
    // PlaceholderDescriptor 是 Services.Cert 命名空间级 record（非 PromptTemplateService 嵌套类型）
    public IReadOnlyList<PlaceholderDescriptor> Placeholders { get; set; } = Array.Empty<PlaceholderDescriptor>();
}

/// <summary>文档提取提示词保存入参</summary>
public sealed class DocExtractPromptSaveRequest
{
    public string Template { get; set; } = string.Empty;
    public string? StandardCode { get; set; }
}

/// <summary>页面内试跑入参（multipart）</summary>
public sealed class DocExtractPromptTestRequest
{
    /// <summary>被测试的文档</summary>
    public IFormFile? File { get; set; }
    /// <summary>标准短码（iso9001 / iso14001 / iso45001 / iso13485）</summary>
    public string? StandardCode { get; set; }
    /// <summary>页面当前正文（未保存也能试）</summary>
    public string? Template { get; set; }
}

using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Controllers;
using YZH.Core.Api.Services;
using YZH.Core.Stand.Helpers;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models;
using YZH.Core.Stand.Models.Config;
using YZH.Core.Stand.Models.Request;
using YZH.Core.Stand.Models.Result;
using CertPlatform.Admin.Entities.Workflow;
using CertPlatform.Admin.Services.Cert;

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
///       全局默认 ⇒ PromptCode=doc_extract, ScopeKey=空
///       标准级   ⇒ PromptCode=doc_extract, ScopeKey=标准Code（如 iso9001）
///       优先匹配标准级，缺失回落到全局默认。
/// </summary>
[ApiController]
[Route("api/Admin/Workflow/[controller]")]
public class DocExtractRuleController : YzhControllerBase<DocExtractRule>
{
    private readonly PromptTemplateService _promptTemplate;

    public DocExtractRuleController(
        EntityService<DocExtractRule> entityService,
        IUserContext userContext,
        PromptTemplateService promptTemplate)
        : base(entityService, userContext)
    {
        _promptTemplate = promptTemplate;
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
    // 提示词维护（委托通用 PromptTemplateService，PromptCode = "doc_extract"）
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 读取某标准的文档提取提示词。
    /// <para>标准级（ScopeKey=标准Code）优先；缺失回落到全局默认（ScopeKey=空）。</para>
    /// </summary>
    /// <param name="standardCode">标准短码（如 iso9001）；空 = 取全局默认</param>
    [HttpGet("prompt")]
    public async Task<IActionResult> GetPrompt([FromQuery] string? standardCode = null)
    {
        var template = await _promptTemplate.GetAsync("doc_extract", null, standardCode);
        return Ok(ApiResponse<DocExtractPromptDto>.Ok(new DocExtractPromptDto
        {
            PromptCode = "doc_extract",
            StandardCode = standardCode,
            Template = template.Template,
            IsFallback = template.Status == "fallback",
            Placeholders = PromptTemplateService.GetPlaceholders("doc_extract"),
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
            "doc_extract",
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

using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Controllers;
using YZH.Core.Api.Services;
using YZH.Core.Stand.Helpers;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Config;
using YZH.Core.Stand.Models.Request;
using YZH.Core.Stand.Models.Result;
using CertPlatform.Admin.Entities.Cert;
using CertPlatform.Admin.Services.Cert;
// 消歧：Entities.Cert 与 Entities.Wf 均为全局 using，两边都有 PromptTemplate（wf_prompt_template 不接管）
using PromptTemplate = CertPlatform.Admin.Entities.Cert.PromptTemplate;

namespace CertPlatform.Admin.Controllers.Cert;

/// <summary>
/// 提示词模板统一控制器
///
/// <para><b>职责</b>：所有业务模块（条款属性 / 文档提取 / 入场检查 / 报告结论 ...）的提示词
/// 经由此控制器存取，只传 <c>promptCode</c> + <c>scopeKey</c> 即可。</para>
///
/// <para><b>路由前缀</b>：<c>/api/Admin/Cert/PromptTemplate</c></para>
///
/// <para><b>已注册的 PromptCode</b>：
/// <list type="bullet">
/// <item><c>clause_attr_gen</c> —— 条款属性预生成（原 ClauseAttrPromptService）</item>
/// <item><c>doc_extract</c> —— 文档提取（原 DocExtractRuleService.PromptContent）</item>
/// </list></para>
///
/// <para><b>作用域解析优先级</b>（具体 → 通用）：
/// OrgCode + ScopeKey → OrgCode → ScopeKey → 平台默认</para>
/// </summary>
[ApiController]
[Route("api/Admin/Cert/[controller]")]
public class PromptTemplateController : YzhControllerBase<PromptTemplate>
{
    private readonly PromptTemplateService _promptTemplate;

    public PromptTemplateController(
        EntityService<PromptTemplate> entityService,
        IUserContext userContext,
        PromptTemplateService promptTemplate)
        : base(entityService, userContext)
    {
        _promptTemplate = promptTemplate;
    }

    /// <summary>加载 EntityConfig</summary>
    protected override EntityConfig LoadConfig()
    {
        return EntityConfigHelper.GetConfig<PromptTemplate>();
    }

    /// <summary>开发期强制暴露缺配置问题</summary>
    protected override bool StrictConfigLoad => true;

    // ════════════════════════════════════════════════════════════════════
    // 一、作用域解析读取（核心端点）
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 按 promptCode + 作用域解析获取最匹配的提示词。
    /// <para>优先级：OrgCode + ScopeKey → OrgCode → ScopeKey → 平台默认。</para>
    /// </summary>
    /// <param name="promptCode">提示词代码（必传）</param>
    /// <param name="orgCode">机构 Code（可空 = 不限）</param>
    /// <param name="scopeKey">细分维度 —— 标准Code / 阶段Code（可空 = 平台默认）</param>
    [HttpGet("resolve")]
    public async Task<IActionResult> Resolve(
        [FromQuery] string promptCode,
        [FromQuery] string? orgCode = null,
        [FromQuery] string? scopeKey = null)
    {
        if (string.IsNullOrWhiteSpace(promptCode))
            return Ok(ApiResponse.Fail("promptCode 不能为空"));

        var template = await _promptTemplate.GetAsync(promptCode, orgCode, scopeKey);
        return Ok(ApiResponse<PromptTemplate>.Ok(template));
    }

    /// <summary>按 promptCode 取所有作用域变体（给管理端用）</summary>
    [HttpGet("by-code/{promptCode}")]
    public async Task<IActionResult> GetByCode(string promptCode)
    {
        var list = await _promptTemplate.GetByCodeAsync(promptCode);
        return Ok(ApiResponse<List<PromptTemplate>>.Ok(list));
    }

    // ════════════════════════════════════════════════════════════════════
    // 二、作用域解析保存
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 保存提示词（幂等 upsert）。
    /// <para>按 (PromptCode, OrgCode, ScopeKey) 三元组定位：有则改正文，无则新建。</para>
    /// </summary>
    [HttpPost("save")]
    public async Task<IActionResult> Save([FromBody] PromptTemplateSaveRequest request)
    {
        var (ok, message) = await _promptTemplate.SaveAsync(
            request.PromptCode,
            request.Template,
            request.OrgCode,
            request.ScopeKey,
            request.PromptName,
            request.Remark,
            UserContext.UserCode);

        return ok
            ? Ok(ApiResponse.Ok(message))
            : Ok(ApiResponse.BizFail(message));
    }

    /// <summary>删除提示词（逻辑删除）</summary>
    [HttpPost("delete")]
    public async Task<IActionResult> Delete([FromBody] PromptTemplateDeleteRequest request)
    {
        var (ok, message) = await _promptTemplate.DeleteAsync(
            request.PromptCode,
            request.OrgCode,
            request.ScopeKey,
            UserContext.UserCode);

        return ok
            ? Ok(ApiResponse.Ok(message))
            : Ok(ApiResponse.BizFail(message));
    }

    // ════════════════════════════════════════════════════════════════════
    // 三、标准 CRUD 基类端点覆盖（用 Code/Id 定位，不走作用域三元组）
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 查询后脱敏：列表不返 Template 正文（太长），只在单条详情时返。
    /// </summary>
    protected override void OnQueried(PagedResult<PromptTemplate> result)
    {
        if (result.Items.Count > 1)
        {
            foreach (var row in result.Items)
            {
                row.Template = null;
                row.Placeholders = null;
            }
        }
    }
}

// ════════════════════════════════════════════════════════════════════════
// 请求 DTO
// ════════════════════════════════════════════════════════════════════════

/// <summary>保存请求</summary>
public sealed class PromptTemplateSaveRequest
{
    public string PromptCode { get; set; } = string.Empty;
    public string Template { get; set; } = string.Empty;
    public string? OrgCode { get; set; }
    public string? ScopeKey { get; set; }
    public string? PromptName { get; set; }
    public string? Remark { get; set; }
}

/// <summary>删除请求</summary>
public sealed class PromptTemplateDeleteRequest
{
    public string PromptCode { get; set; } = string.Empty;
    public string? OrgCode { get; set; }
    public string? ScopeKey { get; set; }
}

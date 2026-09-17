extern alias SharedEntities;

using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Controllers;
using YZH.Core.Api.Services;
using YZH.Core.Stand.Helpers;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models;
using YZH.Core.Stand.Models.Config;
using YZH.Core.Stand.Models.Request;
using YZH.Core.Stand.Models.Result;

namespace CertPlatform.Admin.Controllers.Workflow;

/// <summary>
/// NC 检查规则管理控制器
///
/// 继承 YzhControllerBase 获得能力：
/// - 分页查询：POST /filter（FilterRequest）
/// - 新增：POST /add
/// - 修改：POST /update
/// - 删除：POST /delete（codes 数组）
/// - 配置：GET /config
///
/// 自定义端点：
/// - POST /toggle-active?code=  切换启用状态
/// - POST /copy?sourceCode=    深拷贝规则
/// </summary>
[ApiController]
[Route("api/ValidationRule")]
public class ValidationRuleController
    : YzhControllerBase<SharedEntities::YZH.Entity.Admin.Platform.Cert.ValidationRule>
{
    private readonly EntityService<SharedEntities::YZH.Entity.Admin.Platform.Cert.ISOClause> _clauseService;

    public ValidationRuleController(
        EntityService<SharedEntities::YZH.Entity.Admin.Platform.Cert.ValidationRule> entityService,
        EntityService<SharedEntities::YZH.Entity.Admin.Platform.Cert.ISOClause> clauseService,
        IUserContext userContext)
        : base(entityService, userContext)
    {
        _clauseService = clauseService;
    }

    // ========================================================
    // 配置
    // ========================================================

    protected override EntityConfig LoadConfig()
    {
        return EntityConfigHelper.GetConfig("Cert/ValidationRule");
    }

    protected override bool StrictConfigLoad => true;

    // ========================================================
    // 查询钩子：填充条款编号/标题
    // ========================================================

    protected override void OnQueried(PagedResult<SharedEntities::YZH.Entity.Admin.Platform.Cert.ValidationRule> result)
    {
        var clauseCodes = result.Items
            .Select(r => r.ClauseCode)
            .Where(c => !string.IsNullOrEmpty(c))
            .Distinct()
            .ToList();

        if (clauseCodes.Count == 0) return;

        var clauses = _clauseService.GetListAsync(c => clauseCodes.Contains(c.Code))
            .GetAwaiter().GetResult();

        var clauseDict = clauses.Data?
            .ToDictionary(c => c.Code, c => c)
            ?? new Dictionary<string, SharedEntities::YZH.Entity.Admin.Platform.Cert.ISOClause>();

        foreach (var rule in result.Items)
        {
            if (clauseDict.TryGetValue(rule.ClauseCode ?? "", out var clause))
            {
                rule.ClauseNumber = clause.ClauseNumber;
                rule.ClauseTitle = clause.Title;
            }
        }
    }

    // ========================================================
    // Update 覆盖：排除 Code 字段避免 SqlSugar 参数重复
    // （BaseEntity.Code 与 ValidationRule.Code(new) 冲突）
    // ========================================================

    [HttpPost("update")]
    public override async Task<ActionResult<ApiResponse<SharedEntities::YZH.Entity.Admin.Platform.Cert.ValidationRule>>> Update(
        [FromBody] SharedEntities::YZH.Entity.Admin.Platform.Cert.ValidationRule entity)
    {
        var fields = typeof(SharedEntities::YZH.Entity.Admin.Platform.Cert.ValidationRule)
            .GetProperties()
            .Where(p => p.Name != "Code" && p.Name != "Id"
                     && p.Name != "CreateTime" && p.Name != "CreateBy"
                     && p.Name != "ClauseNumber" && p.Name != "ClauseTitle"
                     && p.Name != "CheckFlag" && p.Name != "DeleteFlag"
                     && p.Name != "RowVersion")
            .Select(p => p.Name)
            .ToArray();

        var result = await Entity.Update(entity, updateFields: fields);
        if (!result.Success)
            return BadRequest(ApiResponse.Fail(result.Error!));

        return Ok(ApiResponse<SharedEntities::YZH.Entity.Admin.Platform.Cert.ValidationRule>.Ok(result.Data));
    }

    // ========================================================
    // 新增钩子：自动生成 RuleCode
    // ========================================================

    protected override async Task<(bool ok, string? msg)> OnBeforeAdd(
        SharedEntities::YZH.Entity.Admin.Platform.Cert.ValidationRule entity)
    {
        if (string.IsNullOrEmpty(entity.Code))
            entity.Code = Guid.NewGuid().ToString("N");

        if (string.IsNullOrEmpty(entity.RuleCode))
        {
            var existing = await Entity.GetListAsync(r =>
                r.StandardCode == entity.StandardCode);
            var seq = (existing.Data?.Count ?? 0) + 1;
            entity.RuleCode = $"NC-{entity.StandardCode}-{seq:D3}";
        }

        return (true, null);
    }

    // ========================================================
    // 自定义端点
    // ========================================================

    /// <summary>切换启用状态</summary>
    [HttpPost("toggle-active")]
    public async Task<IActionResult> ToggleActive([FromQuery] string code)
    {
        var rule = await Entity.GetOne(r => r.Code == code);
        if (!rule.Success || rule.Data == null)
            return BadRequest(ApiResponse.Fail("规则不存在"));

        rule.Data.IsActive = !rule.Data.IsActive;
        var result = await Entity.Update(rule.Data);
        if (!result.Success)
            return BadRequest(ApiResponse.Fail(result.Error!));

        return Ok(ApiResponse<SharedEntities::YZH.Entity.Admin.Platform.Cert.ValidationRule>.Ok(rule.Data));
    }

    /// <summary>深拷贝规则</summary>
    [HttpPost("copy")]
    public async Task<IActionResult> Copy([FromQuery] string sourceCode)
    {
        var source = await Entity.GetOne(r => r.Code == sourceCode);
        if (!source.Success || source.Data == null)
            return BadRequest(ApiResponse.Fail("源规则不存在"));

        var copy = new SharedEntities::YZH.Entity.Admin.Platform.Cert.ValidationRule
        {
            Code = Guid.NewGuid().ToString("N"),
            OrgCode = source.Data.OrgCode,
            StandardCode = source.Data.StandardCode,
            PhaseCode = source.Data.PhaseCode,
            ClauseCode = source.Data.ClauseCode,
            WorkflowCode = source.Data.WorkflowCode,
            RuleName = $"{source.Data.RuleName}（副本）",
            RuleNameEn = source.Data.RuleNameEn,
            SeverityIfViolated = source.Data.SeverityIfViolated,
            NcDescriptionTemplate = source.Data.NcDescriptionTemplate,
            Remark = source.Data.Remark,
            IsActive = false,
        };

        var existing = await Entity.GetListAsync(r =>
            r.StandardCode == copy.StandardCode);
        var seq = (existing.Data?.Count ?? 0) + 1;
        copy.RuleCode = $"NC-{copy.StandardCode}-{seq:D3}";

        var result = await Entity.Insert(copy);
        if (!result.Success)
            return BadRequest(ApiResponse.Fail(result.Error!));

        return Ok(ApiResponse<SharedEntities::YZH.Entity.Admin.Platform.Cert.ValidationRule>.Ok(result.Data));
    }
}

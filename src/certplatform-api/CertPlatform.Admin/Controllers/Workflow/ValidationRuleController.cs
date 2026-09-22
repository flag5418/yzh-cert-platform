
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
/// - 行自定义操作：POST /action/{methodName}（RegisterRowAction 注册，
///   按钮由 EntityConfig.RowButtons.CustomButtons 配置驱动）
/// </summary>
[ApiController]
[Route("api/ValidationRule")]
public class ValidationRuleController
    : YzhControllerBase<ValidationRule>
{
    private readonly EntityService<ISOClause> _clauseService;

    public ValidationRuleController(
        EntityService<ValidationRule> entityService,
        EntityService<ISOClause> clauseService,
        IUserContext userContext)
        : base(entityService, userContext)
    {
        _clauseService = clauseService;

        // 行自定义操作（走标准 /action/{method} 约定，
        // 前端按钮由 Cert/ValidationRule.json 的 RowButtons.CustomButtons 驱动）
        RegisterRowAction("ToggleActive", ToggleActiveAction);
        RegisterRowAction("Copy", CopyAction);
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

    protected override void OnQueried(PagedResult<ValidationRule> result)
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
            ?? new Dictionary<string, ISOClause>();

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
    public override async Task<ActionResult<ApiResponse<ValidationRule>>> Update(
        [FromBody] ValidationRule entity)
    {
        var fields = typeof(ValidationRule)
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

        return Ok(ApiResponse<ValidationRule>.Ok(result.Data));
    }

    // ========================================================
    // 新增钩子：自动生成 RuleCode
    // ========================================================

    protected override async Task<(bool ok, string? msg)> OnBeforeAdd(
        ValidationRule entity)
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
    // 行自定义操作（RegisterRowAction，POST /action/{method}）
    // ========================================================

    /// <summary>切换启用状态（前端按钮：RowButtons.CustomButtons["启用/禁用"]）</summary>
    private async Task<Result<ApiResponse<object?>>> ToggleActiveAction(ValidationRule entity)
    {
        var rule = await Entity.GetOne(r => r.Code == entity.Code);
        if (!rule.Success || rule.Data == null)
            return Result<ApiResponse<object?>>.Fail("规则不存在");

        rule.Data.IsActive = !rule.Data.IsActive;
        var result = await Entity.Update(rule.Data);
        if (!result.Success)
            return Result<ApiResponse<object?>>.Fail(result.Error!);

        return Result<ApiResponse<object?>>.Ok(ApiResponse<object?>.Ok(rule.Data.IsActive ? "已启用" : "已禁用"));
    }

    /// <summary>深拷贝规则（前端按钮：RowButtons.CustomButtons["复制"]）</summary>
    private async Task<Result<ApiResponse<object?>>> CopyAction(ValidationRule entity)
    {
        var source = await Entity.GetOne(r => r.Code == entity.Code);
        if (!source.Success || source.Data == null)
            return Result<ApiResponse<object?>>.Fail("源规则不存在");

        var copy = new ValidationRule
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
            return Result<ApiResponse<object?>>.Fail(result.Error!);

        return Result<ApiResponse<object?>>.Ok(ApiResponse<object?>.Ok("复制成功"));
    }
}

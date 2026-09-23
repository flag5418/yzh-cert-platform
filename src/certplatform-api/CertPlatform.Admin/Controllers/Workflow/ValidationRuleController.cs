
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
    // 查询覆盖：默认按规则名称排序（对齐历史 NCConfig 列表行为）
    // ========================================================

    /// <summary>
    /// 过滤查询覆盖 — 未显式指定排序时默认 RuleName 升序。
    /// <para>历史项目 NCConfig 左侧树依赖稳定顺序，新架构未设默认排序导致顺序随存储引擎漂移。</para>
    /// </summary>
    [NonAction]
    public override async Task<Result<PagedResult<ValidationRule>>> FilterCore(FilterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SortField))
        {
            request.SortField = "RuleName";
            request.SortOrder = "asc";
        }

        return await base.FilterCore(request);
    }

    // ========================================================
    // 单条详情：GET /api/ValidationRule/{code}
    // ========================================================

    /// <summary>
    /// 获取单条规则详情（含 RuleJson 工作流定义 / LayoutJson 布局）。
    /// <para>用途：NC 规则设计页（WorkflowDesigner）选中叶子后懒加载完整字段，
    /// 避免列表接口裁剪大字段导致「已配置」状态判定失败。</para>
    /// <para>字段命名遵循 YZH 命名铁律：DB 列名 = C# 属性名 = TS 字段名（PascalCase）。</para>
    /// </summary>
    /// <param name="code">规则 Code（业务唯一标识，非 Id）</param>
    [HttpGet("{code}")]
    public async Task<ActionResult<ApiResponse<ValidationRule>>> GetByCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest(ApiResponse.Fail("规则编码不能为空"));

        var result = await Entity.GetOne(r => r.Code == code);
        if (!result.Success)
            return BadRequest(ApiResponse.Fail(result.Error!));

        var rule = result.Data;
        if (rule == null)
            return NotFound(ApiResponse.Fail($"规则不存在：{code}"));

        // 回填条款编号/标题，与列表接口保持一致
        if (!string.IsNullOrEmpty(rule.ClauseCode))
        {
            var clause = await _clauseService.GetOne(c => c.Code == rule.ClauseCode);
            if (clause.Success && clause.Data != null)
            {
                rule.ClauseNumber = clause.Data.ClauseNumber;
                rule.ClauseTitle = clause.Data.Title;
            }
        }

        return Ok(ApiResponse<ValidationRule>.Ok(rule));
    }

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

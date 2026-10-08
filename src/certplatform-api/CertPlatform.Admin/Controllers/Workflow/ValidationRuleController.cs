
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
/// <para><b>★ 端标记（2026-10-03）</b>：路由加 <c>Admin/</c> 段，与专家端 <c>/api/Auditor/*</c> 对称。
/// <para>背景：后台端 20 个 Controller 此前零端标记，4 个连业务域前缀都没有（<c>api/AIUsage</c>
/// <c>api/PromptTemplate</c> <c>api/ValidationRule</c> <c>api/ReportDefinition</c>），
/// 且 <c>api/System/[controller]</c> 与框架层 <c>YZH.Core.Web</c> 的 <c>api/System/*</c> 撞前缀。</para>
/// <para><b>不影响授权</b>：<c>ApiCode = Sha256("{METHOD}|{路由末段}|{动作名}")</c>（ApiScanner.cs:326-331）
/// 只取路由<b>末段</b>作控制器名，本 Controller 的末段未变 ⇒ <c>ApiCode</c> 不变 ⇒
/// <b>角色-接口关联不断裂</b>，无需重跑 ApiSync。</para>
[Route("api/Admin/Workflow/ValidationRule")]
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
        // 状态型按钮必须是 disable/enable 两个确定方法 —— 前端按行 IsActive
        // 二选一（启用行只显「禁用」、停用行只显「启用」），不再发合并文案「启用/禁用」
        RegisterRowAction("disable", DisableAction);
        RegisterRowAction("enable", EnableAction);
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
            return Ok(ApiResponse.Fail("规则编码不能为空"));

        var rule = await FindByCodeIgnoreValidAsync(code);
        if (rule == null)
            return Ok(ApiResponse.Fail($"规则不存在：{code}"));

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
        // ★ NULL = 未配 DAG（唯一口径，见 ValidationRuleRules.HasWorkflow）：
        //   空画布 / "{}" / "null" / "[]" 在落库前一律归一为 NULL
        entity.RuleJson = ValidationRuleRules.NormalizeDag(entity.RuleJson);

        var fields = typeof(ValidationRule)
            .GetProperties()
            .Where(p => p.Name != "Code" && p.Name != "Id"
                     && p.Name != "CreateTime" && p.Name != "CreateBy"
                     && p.Name != "ClauseNumber" && p.Name != "ClauseTitle"
                     && p.Name != "CheckFlag" && p.Name != "DeleteFlag"
                     && p.Name != "RowVersion"
                     // ★ 软删除三字段不进更新白名单 —— 编辑行不得改写删除态
                     && p.Name != "IsDeleted" && p.Name != "DeleteBy" && p.Name != "DeleteTime")
            .Select(p => p.Name)
            .ToArray();

        var result = await Entity.Update(entity, updateFields: fields);
        if (!result.Success)
            return Ok(ApiResponse.Fail(result.Error!));

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

        // ★ NULL = 未配 DAG（唯一口径）：空画布 / "{}" / "null" / "[]" 落库前归一为 NULL
        entity.RuleJson = ValidationRuleRules.NormalizeDag(entity.RuleJson);

        if (string.IsNullOrEmpty(entity.RuleCode))
        {
            // ★ includeDisabled: true —— 编号算法已改为 MAX(seq)+1，必须把已禁用行一并纳入；
            //   否则删/禁行后新号与既有行重复，直接撞 uk_rule_code 唯一键
            var existing = await Entity.GetListAsync(r =>
                r.StandardCode == entity.StandardCode, includeDisabled: true);
            entity.RuleCode = ValidationRuleRules.NextRuleCode(
                entity.StandardCode, existing.Data);
        }

        return (true, null);
    }

    // ========================================================
    // 行自定义操作（RegisterRowAction，POST /action/{method}）
    // ========================================================

    /// <summary>行按钮「禁用」（RowButtons.CustomButtons["disable"]，POST /action/disable）</summary>
    private Task<Result<ApiResponse<object?>>> DisableAction(ValidationRule entity)
        => SetActiveAsync(entity, false);

    /// <summary>行按钮「启用」（RowButtons.CustomButtons["enable"]，POST /action/enable）</summary>
    private Task<Result<ApiResponse<object?>>> EnableAction(ValidationRule entity)
        => SetActiveAsync(entity, true);

    /// <summary>
    /// 按 <c>Code</c> 取规则 —— <b>★ 不过滤 <c>IsValid</c></b>。
    ///
    /// <para><b>为什么必须单独开一个方法（2026-10-08 实测踩坑）</b>：本实体实现
    /// <see cref="IIsValid"/> 后，<c>SqlSugarDbOrm.GetOneAsync</c> 会自动附加
    /// <c>IsValid = 1</c> 与 <c>IsDeleted = 0</c> 两个条件（`Where(IsValidCondition<T>())`）。
    /// 于是 <c>Entity.GetOne(r =&gt; r.Code == code)</c> <b>取不到已禁用行</b>，直接造成三个可用性缺陷：
    /// ① 点「禁用」后无法再「启用」（报“规则不存在”）⇒ <b>禁用不可逆</b>；
    /// ② 已禁用规则无法「复制」；③ 已禁用规则详情（设计器 GET /{code}）打不开。</para>
    ///
    /// <para>凡「按 Code 精确定位」的启停 / 复制 / 详情一律走本方法；
    /// 列表类查询仍走默认口径（只看启用行），两者职责不同。</para>
    /// </summary>
    private async Task<ValidationRule?> FindByCodeIgnoreValidAsync(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var result = await Entity.GetListAsync(
            r => r.Code == code, includeDisabled: true);
        return result.Success ? result.Data?.FirstOrDefault() : null;
    }

    /// <summary>
    /// 按 Code 置 <c>IsValid</c>（1=启用 / 0=禁用）。
    /// <para>★ 2026-10-08：启停字段由 <c>IsActive</c> 统一为 <c>IsValid</c>（铁律九），
    /// 与其余 8 张配置表判据一致，且与列按钮显隐所读的 <c>EnableField</c> 同源。</para>
    /// </summary>
    private async Task<Result<ApiResponse<object?>>> SetActiveAsync(ValidationRule entity, bool active)
    {
        // ★ 必须用 includeDisabled 取数：否则停用后再点「启用」会报“规则不存在”
        var rule = await FindByCodeIgnoreValidAsync(entity.Code);
        if (rule == null)
            return Result<ApiResponse<object?>>.Fail("规则不存在");

        rule.IsValid = active ? 1 : 0;
        var result = await Entity.Update(rule);
        if (!result.Success)
            return Result<ApiResponse<object?>>.Fail(result.Error!);

        return Result<ApiResponse<object?>>.Ok(ApiResponse<object?>.Ok(active ? "已启用" : "已禁用"));
    }

    /// <summary>深拷贝规则（前端按钮：RowButtons.CustomButtons["复制"]）</summary>
    private async Task<Result<ApiResponse<object?>>> CopyAction(ValidationRule entity)
    {
        // ★ 必须用 includeDisabled 取数：否则「复制」一条已禁用规则会报“源规则不存在”
        var sourceData = await FindByCodeIgnoreValidAsync(entity.Code);
        if (sourceData == null)
            return Result<ApiResponse<object?>>.Fail("源规则不存在");

        var copy = new ValidationRule
        {
            Code = Guid.NewGuid().ToString("N"),
            OrgCode = sourceData.OrgCode,
            StandardCode = sourceData.StandardCode,
            PhaseCode = sourceData.PhaseCode,
            ClauseCode = sourceData.ClauseCode,
            RuleName = $"{sourceData.RuleName}（副本）",
            RuleNameEn = sourceData.RuleNameEn,
            SeverityIfViolated = sourceData.SeverityIfViolated,
            NcDescriptionTemplate = sourceData.NcDescriptionTemplate,
            Remark = sourceData.Remark,
            // 副本以「禁用」状态落库，待确认后再启用（IsValid: 1=启用 / 0=禁用）
            IsValid = 0,
        };

        var existing = await Entity.GetListAsync(r =>
            r.StandardCode == copy.StandardCode, includeDisabled: true);
        copy.RuleCode = ValidationRuleRules.NextRuleCode(
            copy.StandardCode, existing.Data);

        var result = await Entity.Insert(copy);
        if (!result.Success)
            return Result<ApiResponse<object?>>.Fail(result.Error!);

        return Result<ApiResponse<object?>>.Ok(ApiResponse<object?>.Ok("复制成功"));
    }
}

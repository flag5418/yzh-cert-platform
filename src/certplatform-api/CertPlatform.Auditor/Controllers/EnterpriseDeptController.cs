using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Controllers;
using YZH.Core.Api.Models;
using YZH.Core.Api.Services;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Helpers;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models;
using YZH.Core.Stand.Models.Config;
using YZH.Core.Stand.Models.Result;
using CertPlatform.Auditor.Entities.Expert;
using CertPlatform.Auditor.Services;
using CertPlatform.Shared.Domain.Scope;
using Ent = CertPlatform.Shared.Entities.Cert.Enterprise;

namespace CertPlatform.Auditor.Controllers;

/// <summary>
/// 企业机构管理控制器（专家端 —— 左树右表）
///
/// <para><b>路由</b>：<c>api/Auditor/EnterpriseDept</c></para>
/// <para><b>左树</b>：<c>cert_enterprise</c>（根，多企业并列）+ <c>cert_enterprise_dept</c>（子，不限层级）</para>
/// <para><b>右表</b>：<c>cert_enterprise_dept_member</c>（选中部门后维护该部门人员）</para>
///
/// <para><b>用途</b>：入场计划「准备阶段派沟通任务、计划阶段排日程、现场阶段定访谈对象」。</para>
///
/// <para><b>硬约束</b>：</para>
/// <list type="number">
///   <item><b>工作区隔离</b>：左树根只列本工作区企业；右表按本工作区企业集合收敛</item>
///   <item><b>企业节点不可增改删</b>（企业由业务模块维护，本页只读）</item>
///   <item><b>禁止删除有子部门的部门</b>（基类 AllowDeleteWithChildren=false + 钩子）</item>
///   <item><b>部门名称同企业同层级唯一</b></item>
/// </list>
/// </summary>
[ApiController]
[Route("api/Auditor/[controller]")]
public class EnterpriseDeptController
    : TreeTableControllerBase<CertEnterpriseDept, CertEnterpriseDeptMember>
{
    private readonly WorkspaceContextService _workspace;
    private readonly ICoreResourceQuery _coreResource;
    private readonly IDbOrm _db;

    /// <summary>节点类型：企业（左树根，来自 cert_enterprise）</summary>
    private const string NodeTypeEnterprise = "enterprise";

    /// <summary>节点类型：部门（左树子节点，来自 cert_enterprise_dept）</summary>
    private const string NodeTypeDept = "dept";

    /// <summary>空企业集合哨兵（钻进 IN 的值，非列名）</summary>
    private const string NoEnterpriseSentinel = "__NO_ENTERPRISE__";

    public EnterpriseDeptController(
        EntityService<CertEnterpriseDept> treeEntityService,
        EntityService<CertEnterpriseDeptMember> tableEntityService,
        IUserContext userContext,
        WorkspaceContextService workspace,
        ICoreResourceQuery coreResource,
        IDbOrm db)
        : base(treeEntityService, tableEntityService, userContext)
    {
        _workspace = workspace;
        _coreResource = coreResource;
        _db = db;

        // 左树（部门）配置
        TreeConfig.NameField = "DeptName";
        TreeConfig.CodeField = "Code";
        TreeConfig.ParentCodeField = "ParentCode";
        TreeConfig.RelateField = "DeptCode";
        TreeConfig.EnableField = "IsValid";
        TreeConfig.Lazy = true;
        TreeConfig.AllowAddChild = true;
        TreeConfig.AllowEdit = true;
        TreeConfig.AllowDelete = true;
        // 启停走自定义动作（RegisterTreeAction），关掉基类内置 toggle 避免按钮重复
        TreeConfig.AllowToggle = false;
        // 未选中节点 → 右表空（基类 FilterCore 直接短路，不发 SQL）
        TreeConfig.NoSelectionBehavior = "empty";
        TreeConfig.AllowDeleteWithChildren = false;

        // 树节点表单（部门）
        TreeFormConfigName = "Expert/CertEnterpriseDept";

        // 右表（人员）行操作
        RegisterRowAction("disable", DisableMemberAsync);
        RegisterRowAction("enable", EnableMemberAsync);

        // 左树（部门）节点操作
        RegisterTreeAction("disable", DisableDeptAsync);
        RegisterTreeAction("enable", EnableDeptAsync);
    }

    /// <summary>缺 EntityConfig 时直接抛错</summary>
    protected override bool StrictConfigLoad => true;

    /// <summary>右表（部门人员）配置</summary>
    protected override EntityConfig LoadConfig()
    {
        return EntityConfigHelper.GetConfig<CertEnterpriseDeptMember>();
    }

    // ========================================================
    // 一、右表查询：工作区隔离（左树 RelateField 由基类注入）
    // ========================================================

    protected override List<FilterItem> OnBuildingFilter(List<FilterItem> filters)
    {
        filters = base.OnBuildingFilter(filters);

        var ws = _workspace.Resolve(UserContext.UserCode);
        if (!ws.Success || ws.Data == null)
            throw new InvalidOperationException(ws.Error ?? "无法定位当前工作区");

        // 防御：伪造 DeptCode 直连 /filter 时仍收敛在本工作区企业集合内
        filters.RemoveAll(f => f.Field == "EnterpriseCode");
        filters.Add(new FilterItem
        {
            Field = "EnterpriseCode",
            Operator = "in",
            Value = WorkspaceEnterpriseCodes(ws.Data.Code),
        });
        return filters;
    }

    /// <summary>本工作区有效企业 Code 集合（同步 —— OnBuildingFilter 是同步钩子）</summary>
    private List<string> WorkspaceEnterpriseCodes(string workspaceCode)
    {
        var codes = _db.Client.Queryable<Ent>()
            .Where(p => p.OrgCode == workspaceCode && p.IsValid == 1 && !p.IsDeleted)
            .Select(p => p.Code)
            .ToList();
        if (codes.Count == 0) codes.Add(NoEnterpriseSentinel);
        return codes;
    }

    // ========================================================
    // 二、左树数据：根 = 本工作区企业；子 = 部门
    // ========================================================

    /// <summary>根节点 = 本工作区全部有效企业（多企业并列）</summary>
    [HttpPost("tree/root")]
    public override async Task<ActionResult<ApiResponse<TreeItemDto[]>>> GetRootNodes()
    {
        try
        {
            var ws = _workspace.Resolve(UserContext.UserCode);
            if (!ws.Success || ws.Data == null)
                return Ok(ApiResponse<TreeItemDto[]>.Fail(ws.Error ?? "无法定位当前工作区"));

            // 核心资源取数唯一入口（铁律 L5 / A-R6）
            var enterprises = (await _coreResource.GetEnterprisesAsync(ws.Data.Code))
                .OrderBy(p => p.Sort).ThenBy(p => p.EnterpriseNo)
                .ToList();

            if (enterprises.Count == 0)
                return Ok(ApiResponse<TreeItemDto[]>.Ok(Array.Empty<TreeItemDto>()));

            // 顶层部门存在性 → 决定企业节点是否可展开
            var entCodes = enterprises.Select(e => e.Code).ToList();
            var topDepts = await TreeEntity.GetListAsync(
                p => p.ParentCode == null && entCodes.Contains(p.EnterpriseCode),
                includeDisabled: true);
            var hasChild = (topDepts.Data ?? new List<CertEnterpriseDept>())
                .Select(d => d.EnterpriseCode)
                .ToHashSet();

            var dtos = enterprises.Select(e => new TreeItemDto
            {
                Code = e.Code,
                Name = e.Name,
                ParentCode = null,
                NodeType = NodeTypeEnterprise,
                IsLeaf = !hasChild.Contains(e.Code),
                Level = 0,
                Sort = e.Sort,
                Extra = new Dictionary<string, object>
                {
                    ["NodeType"] = NodeTypeEnterprise,
                    ["EnterpriseCode"] = e.Code,
                },
            }).ToArray();

            return Ok(ApiResponse<TreeItemDto[]>.Ok(dtos));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<TreeItemDto[]>.Fail($"加载根节点失败：{ex.Message}"));
        }
    }

    /// <summary>子节点：ParentCode 是企业 → 顶层部门；是部门 → 子部门</summary>
    [HttpPost("tree/children")]
    public override async Task<ActionResult<ApiResponse<TreeItemDto[]>>> GetChildren(
        [FromBody] TreeChildrenRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.ParentCode))
                return await GetRootNodes();

            var ws = _workspace.Resolve(UserContext.UserCode);
            if (!ws.Success || ws.Data == null)
                return Ok(ApiResponse<TreeItemDto[]>.Fail(ws.Error ?? "无法定位当前工作区"));

            var level = request.Level + 1;
            List<CertEnterpriseDept> depts;

            var parentDept = await TreeEntity.GetByCode(request.ParentCode);
            if (parentDept.Success && parentDept.Data != null)
            {
                // 父节点是部门
                var owner = await _coreResource.GetEnterpriseByCodeAsync(parentDept.Data.EnterpriseCode);
                if (owner == null || owner.OrgCode != ws.Data.Code)
                    return Ok(ApiResponse<TreeItemDto[]>.Ok(Array.Empty<TreeItemDto>()));

                var list = await TreeEntity.GetListAsync(
                    p => p.ParentCode == request.ParentCode
                         && p.EnterpriseCode == parentDept.Data.EnterpriseCode,
                    includeDisabled: true);
                depts = list.Data ?? new List<CertEnterpriseDept>();
            }
            else
            {
                // 父节点是企业 → 顶层部门
                var owner = await _coreResource.GetEnterpriseByCodeAsync(request.ParentCode);
                if (owner == null || owner.OrgCode != ws.Data.Code)
                    return Ok(ApiResponse<TreeItemDto[]>.Ok(Array.Empty<TreeItemDto>()));

                var list = await TreeEntity.GetListAsync(
                    p => p.EnterpriseCode == request.ParentCode && p.ParentCode == null,
                    includeDisabled: true);
                depts = list.Data ?? new List<CertEnterpriseDept>();
            }

            var dtos = depts
                .OrderBy(p => p.Sort).ThenBy(p => p.DeptName)
                .Select(d => MapToTreeItem(d, level))
                .ToList();

            await FillIsLeafBatch(dtos);
            return Ok(ApiResponse<TreeItemDto[]>.Ok(dtos.ToArray()));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<TreeItemDto[]>.Fail($"加载子节点失败：{ex.Message}"));
        }
    }

    /// <summary>
    /// 新增树节点：ParentCode 可能是企业 Code（→ 顶层部门）或部门 Code（→ 子部门）。
    /// 归一后交基类执行（基类只认 ParentCode 指向部门的情形）。
    /// </summary>
    [HttpPost("tree/add")]
    public override async Task<ActionResult<ApiResponse<TreeItemDto>>> AddTreeNode(
        [FromBody] CertEnterpriseDept entity)
    {
        var parentCode = entity.ParentCode;
        if (!string.IsNullOrWhiteSpace(parentCode))
        {
            var parentDept = await TreeEntity.GetByCode(parentCode);
            if (parentDept.Success && parentDept.Data != null)
            {
                // 父节点是部门：继承其企业归属
                entity.EnterpriseCode = parentDept.Data.EnterpriseCode;
            }
            else
            {
                // 父节点是企业：作为该企业的顶层部门
                var owner = await _coreResource.GetEnterpriseByCodeAsync(parentCode);
                if (owner == null)
                    return Ok(ApiResponse.Fail($"上级节点 {parentCode} 不存在"));

                entity.EnterpriseCode = owner.Code;
                entity.ParentCode = null;
            }
        }

        return await base.AddTreeNode(entity);
    }

    /// <summary>
    /// 树节点 DTO：显式补 PascalCase Extra。
    /// 前端 openTreeNodeDialog 编辑回填只认 Extra 中的表单 prop（逐字匹配），
    /// TreeMapper 默认只写 camelCase（如 isValid）→ 回填会全空。
    /// </summary>
    protected override TreeItemDto MapToTreeItem(CertEnterpriseDept entity, int level)
    {
        var dto = base.MapToTreeItem(entity, level);
        dto.NodeType = NodeTypeDept;
        dto.Extra ??= new Dictionary<string, object>();
        dto.Extra["NodeType"] = NodeTypeDept;
        dto.Extra["EnterpriseCode"] = entity.EnterpriseCode;
        dto.Extra["ParentCode"] = entity.ParentCode ?? string.Empty;
        dto.Extra["IsValid"] = entity.IsValid;
        dto.Extra["Sort"] = entity.Sort;
        dto.Extra["LeaderName"] = entity.LeaderName ?? string.Empty;
        dto.Extra["LeaderTitle"] = entity.LeaderTitle ?? string.Empty;
        dto.Extra["LeaderPhone"] = entity.LeaderPhone ?? string.Empty;
        dto.Extra["Remark"] = entity.Remark ?? string.Empty;
        return dto;
    }

    // ========================================================
    // 三、左树（部门）钩子
    // ========================================================

    protected override async Task<(bool ok, string? msg)> OnBeforeAddTree(CertEnterpriseDept entity)
    {
        if (string.IsNullOrWhiteSpace(entity.DeptName))
            return (false, "部门名称不能为空");

        if (string.IsNullOrWhiteSpace(entity.EnterpriseCode))
            return (false, "请先选择企业或部门");

        if (entity.IsValid == 0) entity.IsValid = 1;

        var ws = _workspace.Resolve(UserContext.UserCode);
        if (!ws.Success || ws.Data == null)
            return (false, ws.Error ?? "无法定位当前工作区");

        var owner = await _coreResource.GetEnterpriseByCodeAsync(entity.EnterpriseCode);
        if (owner == null || owner.OrgCode != ws.Data.Code)
            return (false, "该企业不属于当前工作区，无权操作");

        // 同级同企业部门名称唯一
        var dup = await TreeEntity.ExistsAsync(p =>
            p.EnterpriseCode == entity.EnterpriseCode &&
            p.ParentCode == entity.ParentCode &&
            p.DeptName == entity.DeptName &&
            !p.IsDeleted);
        if (dup.Data)
            return (false, $"部门名称【{entity.DeptName}】在该层级下已存在");

        return (true, null);
    }

    protected override async Task<(bool ok, string? msg)> OnBeforeUpdateTree(CertEnterpriseDept entity)
    {
        if (string.IsNullOrWhiteSpace(entity.Code))
            return (false, "更新失败：缺少业务键 Code");

        if (string.IsNullOrWhiteSpace(entity.DeptName))
            return (false, "部门名称不能为空");

        var existing = await TreeEntity.GetByCodeAny(entity.Code);
        if (!existing.Success || existing.Data == null)
            return (false, "部门不存在或已删除");

        // ★ 树表单不含 EnterpriseCode；基类 Update 写全列，不回填会把该列刷成 NULL
        entity.EnterpriseCode = existing.Data.EnterpriseCode;

        var ws = _workspace.Resolve(UserContext.UserCode);
        if (!ws.Success || ws.Data == null)
            return (false, ws.Error ?? "无法定位当前工作区");

        var owner = await _coreResource.GetEnterpriseByCodeAsync(entity.EnterpriseCode);
        if (owner == null || owner.OrgCode != ws.Data.Code)
            return (false, "该企业不属于当前工作区，无权操作");

        // 同级同企业部门名称唯一（排除自身）
        var dup = await TreeEntity.ExistsAsync(p =>
            p.Code != entity.Code &&
            p.EnterpriseCode == entity.EnterpriseCode &&
            p.ParentCode == entity.ParentCode &&
            p.DeptName == entity.DeptName &&
            !p.IsDeleted);
        if (dup.Data)
            return (false, $"部门名称【{entity.DeptName}】在该层级下已存在");

        return (true, null);
    }

    protected override async Task<(bool ok, string? msg)> OnBeforeDeleteTree(string[] codes)
    {
        if (codes == null || codes.Length == 0)
            return (false, "未指定要删除的节点");

        var ws = _workspace.Resolve(UserContext.UserCode);
        if (!ws.Success || ws.Data == null)
            return (false, ws.Error ?? "无法定位当前工作区");

        foreach (var code in codes)
        {
            if (string.IsNullOrWhiteSpace(code)) continue;

            var dept = await TreeEntity.GetByCodeAny(code);
            if (!dept.Success || dept.Data == null)
                return (false, "该节点不存在或不允许删除（企业节点不可删除）");

            var owner = await _coreResource.GetEnterpriseByCodeAsync(dept.Data.EnterpriseCode);
            if (owner == null || owner.OrgCode != ws.Data.Code)
                return (false, "该企业不属于当前工作区，无权操作");
        }

        return (true, null);
    }

    // ========================================================
    // 四、右表（人员）钩子
    // ========================================================

    protected override async Task<(bool ok, string? msg)> OnBeforeAdd(CertEnterpriseDeptMember entity)
    {
        if (string.IsNullOrWhiteSpace(entity.Name))
            return (false, "姓名不能为空");

        if (string.IsNullOrWhiteSpace(entity.DeptCode))
            return (false, "请先选择部门");

        if (entity.IsValid == 0) entity.IsValid = 1;

        var dept = await TreeEntity.GetByCode(entity.DeptCode);
        if (!dept.Success || dept.Data == null)
            return (false, "所属部门不存在或已删除");

        // 企业归属以库中部门为准（不信任前端传入）
        entity.EnterpriseCode = dept.Data.EnterpriseCode;

        var ws = _workspace.Resolve(UserContext.UserCode);
        if (!ws.Success || ws.Data == null)
            return (false, ws.Error ?? "无法定位当前工作区");

        var owner = await _coreResource.GetEnterpriseByCodeAsync(entity.EnterpriseCode);
        if (owner == null || owner.OrgCode != ws.Data.Code)
            return (false, "该企业不属于当前工作区，无权操作");

        return (true, null);
    }

    protected override async Task<(bool ok, string? msg)> OnBeforeUpdate(CertEnterpriseDeptMember entity)
    {
        if (string.IsNullOrWhiteSpace(entity.Code))
            return (false, "更新失败：缺少业务键 Code");

        if (string.IsNullOrWhiteSpace(entity.Name))
            return (false, "姓名不能为空");

        var existing = await Entity.GetByCodeAny(entity.Code);
        if (!existing.Success || existing.Data == null)
            return (false, "人员不存在或已删除");

        // 表单不含 DeptCode / EnterpriseCode，均以库中为准
        entity.DeptCode = existing.Data.DeptCode;
        entity.EnterpriseCode = existing.Data.EnterpriseCode;

        var ws = _workspace.Resolve(UserContext.UserCode);
        if (!ws.Success || ws.Data == null)
            return (false, ws.Error ?? "无法定位当前工作区");

        var owner = await _coreResource.GetEnterpriseByCodeAsync(entity.EnterpriseCode);
        if (owner == null || owner.OrgCode != ws.Data.Code)
            return (false, "该企业不属于当前工作区，无权操作");

        return (true, null);
    }

    protected override async Task<(bool ok, string? msg)> OnBeforeDelete(string[] codes)
    {
        if (codes == null || codes.Length == 0)
            return (false, "未指定要删除的记录");

        var ws = _workspace.Resolve(UserContext.UserCode);
        if (!ws.Success || ws.Data == null)
            return (false, ws.Error ?? "无法定位当前工作区");

        foreach (var code in codes)
        {
            if (string.IsNullOrWhiteSpace(code)) continue;

            var row = await Entity.GetByCodeAny(code);
            if (!row.Success || row.Data == null)
                return (false, "记录不存在或已删除");

            var owner = await _coreResource.GetEnterpriseByCodeAsync(row.Data.EnterpriseCode);
            if (owner == null || owner.OrgCode != ws.Data.Code)
                return (false, "该企业不属于当前工作区，无权操作");
        }

        return (true, null);
    }

    // ========================================================
    // 五、行操作：人员 禁用 / 启用
    // ========================================================

    private async Task<Result<ApiResponse<object?>>> DisableMemberAsync(CertEnterpriseDeptMember entity)
    {
        return await SetMemberValidAsync(entity.Code, 0);
    }

    private async Task<Result<ApiResponse<object?>>> EnableMemberAsync(CertEnterpriseDeptMember entity)
    {
        return await SetMemberValidAsync(entity.Code, 1);
    }

    private async Task<Result<ApiResponse<object?>>> SetMemberValidAsync(string? code, int isValid)
    {
        if (string.IsNullOrWhiteSpace(code))
            return Result<ApiResponse<object?>>.Fail("缺少业务键 Code");

        var result = await Entity.GetByCodeAny(code);
        if (!result.Success || result.Data == null)
            return Result<ApiResponse<object?>>.Fail("记录不存在");

        var target = result.Data;

        var ws = _workspace.Resolve(UserContext.UserCode);
        if (!ws.Success || ws.Data == null)
            return Result<ApiResponse<object?>>.Fail(ws.Error ?? "无法定位当前工作区");

        var owner = await _coreResource.GetEnterpriseByCodeAsync(target.EnterpriseCode);
        if (owner == null || owner.OrgCode != ws.Data.Code)
            return Result<ApiResponse<object?>>.Fail("该企业不属于当前工作区，无权操作");

        if (target.IsValid == isValid)
            return Result<ApiResponse<object?>>.Ok(
                ApiResponse<object?>.Ok(isValid == 1 ? "该人员已是启用状态" : "该人员已是禁用状态"));

        target.IsValid = isValid;
        var upd = await Entity.Update(target, UserContext.ClientIp);
        if (!upd.Success)
            return Result<ApiResponse<object?>>.Fail(upd.Error);

        return Result<ApiResponse<object?>>.Ok(
            ApiResponse<object?>.Ok(isValid == 1 ? "已启用" : "已禁用"));
    }

    // ========================================================
    // 六、树操作：部门 禁用 / 启用
    // ========================================================

    private async Task<object?> DisableDeptAsync(CertEnterpriseDept entity)
    {
        return await SetDeptValidAsync(entity.Code, 0);
    }

    private async Task<object?> EnableDeptAsync(CertEnterpriseDept entity)
    {
        return await SetDeptValidAsync(entity.Code, 1);
    }

    private async Task<object?> SetDeptValidAsync(string? code, int isValid)
    {
        if (string.IsNullOrWhiteSpace(code))
            return OperationResult<string>.Fail("缺少业务键 Code");

        var result = await TreeEntity.GetByCodeAny(code);
        if (!result.Success || result.Data == null)
            return OperationResult<string>.Fail("部门不存在或已删除");

        var target = result.Data;

        var ws = _workspace.Resolve(UserContext.UserCode);
        if (!ws.Success || ws.Data == null)
            return OperationResult<string>.Fail(ws.Error ?? "无法定位当前工作区");

        var owner = await _coreResource.GetEnterpriseByCodeAsync(target.EnterpriseCode);
        if (owner == null || owner.OrgCode != ws.Data.Code)
            return OperationResult<string>.Fail("该企业不属于当前工作区，无权操作");

        if (target.IsValid == isValid)
            return OperationResult<string>.Ok(isValid == 1 ? "该部门已是启用状态" : "该部门已是禁用状态");

        target.IsValid = isValid;
        var upd = await TreeEntity.Update(target, UserContext.ClientIp);
        if (!upd.Success)
            return OperationResult<string>.Fail(upd.Error ?? "操作失败");

        return OperationResult<string>.Ok(isValid == 1 ? "已启用" : "已禁用");
    }
}

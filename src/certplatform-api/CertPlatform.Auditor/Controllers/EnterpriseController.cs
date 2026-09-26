using Microsoft.AspNetCore.Mvc;
using SqlSugar;
using YZH.Core.Api.Controllers;
using YZH.Core.Api.Models.Organization;
using YZH.Core.Api.Services;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Helpers;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models;
using YZH.Core.Stand.Models.Config;
using YZH.Core.Stand.Models.Result;
using CertPlatform.Auditor.Services;
using CertPlatform.Shared.Entities.Cert;

namespace CertPlatform.Auditor.Controllers;

/// <summary>
/// 企业管理控制器（专家端 —— 专家平台的第一个业务功能）
///
/// <para><b>路由</b>：<c>api/Auditor/Enterprise</c>（端专属前缀，避免与后台 Admin 复用冲突）</para>
/// <para><b>数据表</b>：<c>cert_enterprise</c>（唯一企业表，单表结构）</para>
///
/// <para><b>三条硬约束</b>：</para>
/// <list type="number">
///   <item><b>工作区隔离</b>：列表/查询/新增/行操作全部按 <c>OrgCode = 当前工作区</c> 收敛；
///         同一企业可存在于不同工作区（约束作用域 = 单个工作区）</item>
///   <item><b>建档 = 2 张表</b>（<c>cert_enterprise</c> + <c>Sys_Organization</c> 企业节点）
///         → 基类 <c>AddCore</c> **不在事务里**，故必须覆写自开事务，否则失败会留半成品数据</item>
///   <item><b>企业 ↔ 机构节点强一致</b>（2026-09-26 新增）：企业节点是企业在机构树中的**唯一投影**，
///         故 <b>新增 / 改名 / 禁用 / 启用 / 删除</b> 五个动作都必须同步该节点，否则
///         「列表显示的行 ≠ 树里看到的企业」——两边都不报错，是最难查的一类 bug。</item>
/// </list>
///
/// <para><b>组织树形态</b>（2026-09-26 用户裁决，与 <c>AuditorRegisterService</c> 对齐）：</para>
/// <code>
/// 虚拟体系机构            VirtualOrg  L1
///   └── {机构简称} · {姓名}   Dept        L2   ← 工作区（AuditorRegisterService 创建）
///         ├── 管理员          Dept        L3
///         ├── 审核员          Dept        L3
///         ├── 审核组长        Dept        L3
///         └── 企业信息        Dept        L3   ← ★ 文件夹，本控制器负责确保存在
///               └── {企业全称}  Enterprise  L4   ← ★ 企业节点，挂在文件夹下
/// </code>
/// <para>历史命名曾为「企业用户」，且早期实现把该占位节点**就地改造**为企业节点（导致企业直接挂在 L3）。
/// 本版本改为「文件夹 + 子节点」两层，并对旧数据**自动收敛**：遇旧名自动改名、遇挂错层的企业节点自动改挂。</para>
///
/// <para><b>行按钮（禁用/启用）</b>：与后台管理 <c>System/OrganizationController</c> 同一套架构 ——
/// 构造函数 <c>RegisterRowAction("disable"/"enable")</c> 注册 → 前端
/// <c>EntityConfig.RowButtons.CustomButtons</c> 驱动 → 落到 <c>POST api/Auditor/Enterprise/action/{disable|enable}</c>。
/// <b>必须同时把 <c>Enable</c> 置 false</b>，否则基类会再追加一个 <c>toggle-valid</c> 按钮 → 同一行出现两个「禁用/启用」。</para>
/// </summary>
[ApiController]
[Route("api/Auditor/[controller]")]
public class EnterpriseController : YzhControllerBase<Enterprise>
{
    // ========================================================
    // 常量：机构树结构（与 AuditorRegisterService 必须一致）
    // ========================================================

    /// <summary>工作区下「企业信息」文件夹节点名（与 <c>AuditorRegisterService.DefaultGroups</c> 一致）</summary>
    private const string EnterpriseGroupName = "企业信息";

    /// <summary>历史命名「企业用户」—— 2026-09-26 起收敛为「企业信息」，遇到旧名自动改名</summary>
    private const string LegacyEnterpriseGroupName = "企业用户";

    /// <summary>「企业信息」文件夹在机构树中的层级（虚拟体系机构 L1 → 工作区 L2 → 分组 L3）</summary>
    private const int EnterpriseGroupLevel = 3;

    /// <summary>企业节点在机构树中的层级（挂在「企业信息」文件夹下）</summary>
    private const int EnterpriseNodeLevel = 4;

    /// <summary>「企业信息」文件夹排序号（管理员 10 / 审核员 20 / 审核组长 30 / 企业信息 40）</summary>
    private const int EnterpriseGroupSort = 40;

    /// <summary>企业节点在「企业信息」文件夹内的起始排序号</summary>
    private const int EnterpriseNodeSortBase = 10;

    /// <summary>机构节点类型：企业</summary>
    private const string OrgTypeEnterprise = "Enterprise";

    /// <summary>机构节点类型：部门 / 文件夹</summary>
    private const string OrgTypeDept = "Dept";

    private readonly IDbOrm _db;
    private readonly EntityService<Sys_Organization> _orgService;
    private readonly WorkspaceContextService _workspace;

    public EnterpriseController(
        EntityService<Enterprise> entityService,
        IUserContext userContext,
        IDbOrm db,
        EntityService<Sys_Organization> orgService,
        WorkspaceContextService workspace)
        : base(entityService, userContext)
    {
        _db = db;
        _orgService = orgService;
        _workspace = workspace;

        // ★ 行按钮：禁用/启用。与后台管理 OrganizationController 同一套「委托注册」架构。
        //   若此处漏注册，前端点击会收到「操作 [disable] 未注册，请检查 RegisterRowAction 调用」。
        RegisterRowAction("disable", DisableEnterpriseAsync);
        RegisterRowAction("enable", EnableEnterpriseAsync);
    }

    /// <summary>缺 EntityConfig 时直接抛错（开发期暴露，避免「页面空白且零报错」）</summary>
    protected override bool StrictConfigLoad => true;

    protected override EntityConfig LoadConfig()
    {
        return EntityConfigHelper.GetConfig<Enterprise>();
    }

    /// <summary>
    ///     行按钮配置：Edit + Delete + 自定义 disable/enable。
    ///
    /// <para><b>为什么必须 <c>Enable = false</c></b>：<c>Enable = true</c> 会让基类额外追加一个
    /// <c>toggle-valid</c> 按钮，而前端 <c>toRowActions</c> 对「已注册状态型自定义按钮 + EnableField」
    /// 会再渲染一个二选一按钮 → <b>同一行出现两个「禁用/启用」</b>。
    /// 参考实现：<c>System/OrganizationController.GetRowButtons()</c>（同样返回 Enable = false）。</para>
    ///
    /// <para>以 <c>base.GetRowButtons()</c>（= JSON <c>RowButtons</c>）为基线再**补齐**，
    /// 保证 JSON 漂移时按钮依然存在（单表基类不做自动注入，只有 TreeTableControllerBase 会注入）。</para>
    /// </summary>
    protected override RowButtonConfig GetRowButtons()
    {
        // ⚠️ 不要就地修改 base.GetRowButtons() 的返回值 —— 那是 EntityConfig 的**缓存实例**，
        //    就地改会污染全局配置（跨请求 / 并发可见）。此处构造新对象。
        var source = base.GetRowButtons();

        var custom = new Dictionary<string, string>();

        // 保留 JSON 里声明的其它自定义按钮（不覆盖、不丢弃）
        if (source.CustomButtons != null)
        {
            foreach (var kv in source.CustomButtons)
                custom[kv.Key] = kv.Value;
        }

        custom["disable"] = "禁用";
        custom["enable"] = "启用";

        return new RowButtonConfig
        {
            Edit = source.Edit,
            Delete = source.Delete,
            Enable = false,          // ★ 见上方注释：必须 false，否则同一行出现两个「禁用/启用」
            CustomButtons = custom
        };
    }

    // ========================================================
    // 一、查询：强制收敛到当前工作区
    // ========================================================

    /// <summary>
    /// 构建过滤条件：叠加 <c>OrgCode = 当前工作区</c>（多租户隔离）。
    /// <para>解析失败时**不静默放行** —— 抛错，避免越权看到全部工作区的数据。</para>
    /// <para><c>ShowDisabled</c> 由基类消费（<c>true</c> → 不注入 <c>IsValid=1</c>，
    /// 故「显示已禁用」开关无需本类额外处理）。</para>
    /// </summary>
    protected override List<FilterItem> OnBuildingFilter(List<FilterItem> filters)
    {
        filters = base.OnBuildingFilter(filters);

        var ws = _workspace.Resolve(UserContext.UserCode);
        if (!ws.Success || ws.Data == null)
            throw new InvalidOperationException(ws.Error ?? "无法定位当前工作区");

        filters.RemoveAll(f => f.Field == "OrgCode");
        filters.Add(new FilterItem
        {
            Field = "OrgCode",
            Operator = "eq",
            Value = ws.Data.Code
        });

        return filters;
    }

    // ========================================================
    // 二、新增前校验（基类 AddCore 亦会调用）
    // ========================================================

    /// <summary>新增前：补工作区 / 企业编号，并做工作区内唯一性校验</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeAdd(Enterprise entity)
    {
        var ws = _workspace.Resolve(UserContext.UserCode);
        if (!ws.Success || ws.Data == null)
            return (false, ws.Error ?? "无法定位当前工作区");

        entity.OrgCode = ws.Data.Code;

        if (string.IsNullOrWhiteSpace(entity.Name))
            return (false, "企业全称不能为空");

        // 工作区内企业名称唯一（uk_org_ent_name）
        var dupName = await Entity.ExistsAsync(p => p.OrgCode == entity.OrgCode && p.Name == entity.Name);
        if (dupName.Data)
            return (false, $"企业名称【{entity.Name}】在当前工作区已存在");

        // 工作区内统一社会信用代码唯一（uk_org_ent_credit，仅在有值时校验）
        if (!string.IsNullOrWhiteSpace(entity.CreditCode))
        {
            var dupCredit = await Entity.ExistsAsync(p =>
                p.OrgCode == entity.OrgCode && p.CreditCode == entity.CreditCode);
            if (dupCredit.Data)
                return (false, $"统一社会信用代码【{entity.CreditCode}】在当前工作区已被占用");
        }

        if (string.IsNullOrWhiteSpace(entity.EnterpriseNo))
            entity.EnterpriseNo = await NextEnterpriseNoAsync(entity.OrgCode!);

        if (entity.IsValid == 0)
            entity.IsValid = 1;

        return (true, null);
    }

    /// <summary>
    ///     修改前：① 工作区归属校验（只能改本工作区的企业）② 名称 / 信用代码唯一性（排除自身）
    /// </summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeUpdate(Enterprise entity)
    {
        if (string.IsNullOrWhiteSpace(entity.Code))
            return (false, "更新失败：缺少业务键 Code");

        // ★ 与 OnBeforeAdd 对称：Enterprise.json 里 Name 的 Yxk=false（不加 [Required] 是为了
        //   不被 [ApiController] 的自动模型校验拦成 400），故必填必须在这里兜住。
        //   否则改名成空 → 企业表 Name='' 而机构节点保留旧名 → 树与列表不一致。
        if (string.IsNullOrWhiteSpace(entity.Name))
            return (false, "企业全称不能为空");

        var ws = _workspace.Resolve(UserContext.UserCode);
        if (!ws.Success || ws.Data == null)
            return (false, ws.Error ?? "无法定位当前工作区");

        var existing = await Entity.GetByCode(entity.Code);
        if (!existing.Success || existing.Data == null)
            return (false, "企业不存在或已删除");

        if (!string.Equals(existing.Data.OrgCode, ws.Data.Code, StringComparison.Ordinal))
            return (false, "无权修改其他工作区的企业");

        // 工作区由服务端强制，不接受客户端传入（防跨工作区搬移数据）
        entity.OrgCode = ws.Data.Code;

        // ★ 企业编号是「服务端生成的业务键」：客户端留空时必须**保留原值**。
        //   否则 UpdateCore 的 updateFields（= 全部 BcFlag 列，含 EnterpriseNo）会把它写成空串
        //   → 静默丢号（列表里「企业编号」变空白，且零报错）。
        //   Enterprise.json 的 Placeholder 写着「留空自动生成」，用户很容易在编辑时清空它。
        if (string.IsNullOrWhiteSpace(entity.EnterpriseNo))
            entity.EnterpriseNo = existing.Data.EnterpriseNo;

        var dupName = await Entity.ExistsAsync(p =>
            p.Code != entity.Code && p.OrgCode == entity.OrgCode && p.Name == entity.Name);
        if (dupName.Data)
            return (false, $"企业名称【{entity.Name}】在当前工作区已存在");

        if (!string.IsNullOrWhiteSpace(entity.CreditCode))
        {
            var dupCredit = await Entity.ExistsAsync(p =>
                p.Code != entity.Code && p.OrgCode == entity.OrgCode && p.CreditCode == entity.CreditCode);
            if (dupCredit.Data)
                return (false, $"统一社会信用代码【{entity.CreditCode}】在当前工作区已被占用");
        }

        return (true, null);
    }

    // ========================================================
    // 三、新增：企业 + 组织节点（2 张表，必须自开事务）
    // ========================================================

    /// <summary>
    /// 覆写新增原子方法：**事务内**写 <c>cert_enterprise</c> + 确保机构树「企业信息 / 企业节点」。
    ///
    /// <para>为什么不走 <c>OnAfterAdd</c>：该钩子返回 <c>Task</c> 无返回值、**不可取消**，
    /// 机构节点失败时企业已落库 → 半成品数据。</para>
    /// </summary>
    public override async Task<Result<Enterprise>> AddCore(Enterprise entity)
    {
        // 1. 工作区
        var ws = _workspace.Resolve(UserContext.UserCode);
        if (!ws.Success || ws.Data == null)
            return Result<Enterprise>.Fail(ws.Error ?? "无法定位当前工作区");
        var workspace = ws.Data;

        // 2. 基类校验（EntityConfig 的 BcFlag/必填）+ 业务校验
        var (valid, validMsg) = ValidateEntity(entity);
        if (!valid) return Result<Enterprise>.Fail(validMsg ?? "校验未通过");

        var (ok, msg) = await OnBeforeAdd(entity);
        if (!ok) return Result<Enterprise>.Fail(msg ?? "操作已取消");

        // 3. 事务写入
        using var tx = _db.BeginTransaction();
        try
        {
            if (string.IsNullOrWhiteSpace(entity.Code))
                entity.Code = Guid.NewGuid().ToString("N");

            var insert = await Entity.Insert(entity, UserContext.ClientIp);
            if (!insert.Success)
            {
                tx.Rollback();
                return Result<Enterprise>.Fail(insert.Error);
            }

            var orgResult = await AttachEnterpriseOrgNodeAsync(workspace, insert.Data!, UserContext.ClientIp);
            if (!orgResult.Success)
            {
                tx.Rollback();
                return Result<Enterprise>.Fail(orgResult.Error);
            }

            tx.Commit();

            await OnAfterCommitted();
            return Result<Enterprise>.Ok(insert.Data!);
        }
        catch (Exception ex)
        {
            tx.Rollback();
            return Result<Enterprise>.Fail($"新增企业失败：{ex.Message}");
        }
    }

    // ========================================================
    // 四、修改：企业 + 组织节点同步（改名 / 改对接人必须同步节点）
    // ========================================================

    /// <summary>
    /// 覆写修改原子方法：**事务内**改 <c>cert_enterprise</c> + 同步机构树企业节点。
    ///
    /// <para>为什么不走 <c>OnAfterUpdate</c>：该钩子在基类里位于事务**之外**（基类 <c>UpdateCore</c> 未开事务），
    /// 且返回 <c>Task</c> 无法回滚 → 节点同步失败时企业名已改、树里还是旧名。</para>
    ///
    /// <para>同步内容：<c>OrgName</c>（企业全称）、<c>LeaderName</c>（对接人）、
    /// <c>LeaderPhone</c>（对接电话）、<c>IsValid</c>、<c>Remark</c>，并顺带修正挂靠层级。</para>
    /// </summary>
    public override async Task<Result<Enterprise>> UpdateCore(Enterprise entity)
    {
        // 1. 基类校验 + 业务校验
        var (valid, validMsg) = ValidateEntity(entity);
        if (!valid) return Result<Enterprise>.Fail(validMsg ?? "校验未通过");

        var (ok, msg) = await OnBeforeUpdate(entity);
        if (!ok) return Result<Enterprise>.Fail(msg ?? "操作已取消");

        // OnBeforeUpdate 已保证 Code 非空；取局部变量让可空流分析通过（BaseEntity.Code 是 string?）
        var entityCode = entity.Code!;

        var ws = _workspace.Resolve(UserContext.UserCode);
        if (!ws.Success || ws.Data == null)
            return Result<Enterprise>.Fail(ws.Error ?? "无法定位当前工作区");
        var workspace = ws.Data;

        // 2. 可保存字段（基于 BcFlag，与基类一致）
        var saveableFields = Config.Columns
            .Where(c => c.BcFlag)
            .Select(c => c.FieldName)
            .ToArray();

        using var tx = _db.BeginTransaction();
        try
        {
            var result = await Entity.Update(
                entity, UserContext.ClientIp,
                updateFields: saveableFields.Length > 0 ? saveableFields : null);

            if (!result.Success)
            {
                tx.Rollback();
                return Result<Enterprise>.Fail(result.Error);
            }

            // ★ 关键：updateFields 可能不含全部字段，故同步时**重新读库**取权威值，
            //   而不是直接用入参 entity（否则 ContactName 未提交时会写成 null）。
            var authoritative = await Entity.GetByCodeAny(entityCode);
            if (!authoritative.Success || authoritative.Data == null)
            {
                tx.Rollback();
                return Result<Enterprise>.Fail("修改后回读企业失败，已回滚");
            }

            var updated = authoritative.Data;

            var sync = await AttachEnterpriseOrgNodeAsync(workspace, updated, UserContext.ClientIp);
            if (!sync.Success)
            {
                tx.Rollback();
                return Result<Enterprise>.Fail(sync.Error);
            }

            tx.Commit();

            await OnAfterUpdate(updated);
            await OnAfterCommitted();
            return Result<Enterprise>.Ok(updated);
        }
        catch (Exception ex)
        {
            tx.Rollback();
            return Result<Enterprise>.Fail($"修改企业失败：{ex.Message}");
        }
    }

    // ========================================================
    // 五、删除：企业 + 组织节点同步（否则树里留「幽灵企业」）
    // ========================================================

    /// <summary>
    /// 覆写删除原子方法：**事务内**软删 <c>cert_enterprise</c> + 软删对应机构树节点。
    ///
    /// <para><b>为什么必须覆写</b>：基类钩子 <c>OnAfterDelete(int count)</c> <b>只给数量、不给 Code</b>，
    /// 无法定位要同步的节点；且基类 <c>DeleteCore</c> 未开事务。故此处整体覆写。</para>
    ///
    /// <para>用户报告的问题：「我删除了企业信息，但真正的机构表中的虚拟体系机构中该企业信息还存在」
    /// —— 即此同步缺失所致。</para>
    /// </summary>
    public override async Task<Result<int>> DeleteCore(params string[] codes)
    {
        if (codes == null || codes.Length == 0)
            return Result<int>.Fail("未指定要删除的记录");

        // 1. 删除前钩子（可取消）
        var (ok, cancelMsg) = await OnBeforeDelete(codes);
        if (!ok) return Result<int>.Fail(cancelMsg ?? "操作已取消");

        using var tx = _db.BeginTransaction();
        try
        {
            // 2. 删企业
            var result = await Entity.DeleteBatch(
                codes, hardDelete: HardDelete, clientIp: UserContext.ClientIp);
            if (!result.Success)
            {
                tx.Rollback();
                return Result<int>.Fail(result.Error);
            }

            // 3. 同步软删机构节点（含已禁用节点 —— 必须 includeDisabled）
            var sync = await DetachEnterpriseOrgNodesAsync(codes, UserContext.ClientIp);
            if (!sync.Success)
            {
                tx.Rollback();
                return Result<int>.Fail(sync.Error);
            }

            tx.Commit();

            await OnAfterDelete(result.Data);
            await OnAfterCommitted();
            return Result<int>.Ok(result.Data);
        }
        catch (Exception ex)
        {
            tx.Rollback();
            return Result<int>.Fail($"删除企业失败：{ex.Message}");
        }
    }

    // ========================================================
    // 六、行操作：禁用 / 启用（与 System/OrganizationController 同一套架构）
    // ========================================================

    /// <summary>
    /// 禁用企业。
    /// <para>POST <c>/api/Auditor/Enterprise/action/disable</c>，请求体 = 行实体 <c>{ "Code": "..." }</c></para>
    /// <para>同步：企业 <c>IsValid=0</c> + 机构树企业节点 <c>IsValid=0</c>（防「列表禁用、树里还启用」的漂移）。</para>
    /// </summary>
    private async Task<Result<ApiResponse<object?>>> DisableEnterpriseAsync(Enterprise entity)
    {
        return await SetEnterpriseValidAsync(entity.Code, 0);
    }

    /// <summary>
    /// 启用企业。
    /// <para>POST <c>/api/Auditor/Enterprise/action/enable</c>，请求体 = 行实体 <c>{ "Code": "..." }</c></para>
    /// </summary>
    private async Task<Result<ApiResponse<object?>>> EnableEnterpriseAsync(Enterprise entity)
    {
        return await SetEnterpriseValidAsync(entity.Code, 1);
    }

    /// <summary>
    /// 禁用 / 启用企业的共用实现（<paramref name="isValid"/>：1 = 启用，0 = 禁用）。
    ///
    /// <para><b>查询用 <c>GetByCodeAny</c></b>：已禁用企业 <c>IsValid=0</c>，
    /// <c>GetByCode</c> 自带 <c>IsValid=1</c> 过滤 → 查不到 → 「启用」永远失败。</para>
    /// </summary>
    private async Task<Result<ApiResponse<object?>>> SetEnterpriseValidAsync(string? code, int isValid)
    {
        if (string.IsNullOrWhiteSpace(code))
            return Result<ApiResponse<object?>>.Fail("缺少业务键 Code");

        var result = await Entity.GetByCodeAny(code);
        if (!result.Success || result.Data == null)
            return Result<ApiResponse<object?>>.Fail("企业不存在");

        var target = result.Data;

        // 工作区归属校验（行操作不经过 OnBuildingFilter，必须自行把关）
        var ws = _workspace.Resolve(UserContext.UserCode);
        if (!ws.Success || ws.Data == null)
            return Result<ApiResponse<object?>>.Fail(ws.Error ?? "无法定位当前工作区");

        if (!string.Equals(target.OrgCode, ws.Data.Code, StringComparison.Ordinal))
            return Result<ApiResponse<object?>>.Fail("无权操作其他工作区的企业");

        if (target.IsValid == isValid)
            return Result<ApiResponse<object?>>.Ok(
                ApiResponse<object?>.Ok(isValid == 1 ? "该企业已是启用状态" : "该企业已是禁用状态"));

        using var tx = _db.BeginTransaction();
        try
        {
            target.IsValid = isValid;
            var upd = await Entity.Update(target, UserContext.ClientIp);
            if (!upd.Success)
            {
                tx.Rollback();
                return Result<ApiResponse<object?>>.Fail(upd.Error);
            }

            var sync = await SetOrgNodeValidAsync(code, isValid, UserContext.ClientIp);
            if (!sync.Success)
            {
                tx.Rollback();
                return Result<ApiResponse<object?>>.Fail(sync.Error);
            }

            tx.Commit();

            await OnAfterCommitted();
            return Result<ApiResponse<object?>>.Ok(
                ApiResponse<object?>.Ok(isValid == 1 ? "已启用该企业" : "已禁用该企业"));
        }
        catch (Exception ex)
        {
            tx.Rollback();
            return Result<ApiResponse<object?>>.Fail($"操作失败：{ex.Message}");
        }
    }

    // ========================================================
    // 七、私有辅助 —— 机构树同步
    // ========================================================

    /// <summary>
    /// 确保企业节点存在且**反映**企业实体（幂等：新增与修改共用同一入口）。
    ///
    /// <list type="number">
    ///   <item>确保工作区下存在「企业信息」文件夹（L3）；旧名「企业用户」自动改名</item>
    ///   <item>按 <c>OrgCode = 企业 Code</c> 找已有企业节点（含已禁用、含历史挂错层的）</item>
    ///   <item>找到 → **就地修正**：名称 / 负责人 / 层级 / 父节点 / 路径 / 有效性 / 备注</item>
    ///   <item>未找到 → 在「企业信息」文件夹下**新建**（L4）</item>
    /// </list>
    ///
    /// <para>第 3 步的「改挂」专门修复历史遗留：早期实现把「企业用户」占位节点就地改造为企业节点，
    /// 导致企业直接挂在工作区（L3）而非「企业信息」下（L4）。</para>
    /// </summary>
    private async Task<Result<bool>> AttachEnterpriseOrgNodeAsync(
        Sys_Organization workspace, Enterprise entity, string? clientIp)
    {
        // 1. 确保「企业信息」文件夹
        var groupResult = await EnsureEnterpriseGroupAsync(workspace, clientIp);
        if (!groupResult.Success) return Result<bool>.Fail(groupResult.Error);
        var group = groupResult.Data!;

        // 2. 找该企业已有的机构节点（★ 必须排除软删；含已禁用）
        var existing = _db.Client.Queryable<Sys_Organization>()
            .Where(x => x.OrgCode == entity.Code
                        && x.OrgType == OrgTypeEnterprise
                        && x.IsDeleted == false)
            .First();

        if (existing != null)
        {
            // 3. 就地修正（含历史挂错层的改挂）
            existing.OrgName = Truncate(entity.Name, 200) ?? existing.OrgName;
            existing.OrgType = OrgTypeEnterprise;
            existing.ParentCode = group.Code;
            existing.OrgLevel = EnterpriseNodeLevel;
            existing.OrgPath = $"{ResolveOrgPath(group)}/{existing.Code}";
            existing.LeaderName = Truncate(entity.ContactName, 50);
            existing.LeaderPhone = Truncate(entity.ContactPhone, 20);
            existing.IsValid = entity.IsValid;
            existing.Remark = $"企业节点｜{entity.Name}（{entity.EnterpriseNo}）";

            var upd = await _orgService.Update(existing, clientIp);
            if (!upd.Success)
                return Result<bool>.Fail($"同步企业节点失败：{upd.Error}");

            return Result<bool>.Ok(true);
        }

        // 4. 新建企业节点（挂在「企业信息」文件夹下）
        var nodeCode = Guid.NewGuid().ToString("N");

        var maxSort = _db.Client.Queryable<Sys_Organization>()
            .Where(x => x.ParentCode == group.Code && x.IsDeleted == false)
            .Max(x => (int?)x.Sort) ?? 0;

        var node = new Sys_Organization
        {
            Code = nodeCode,
            OrgName = Truncate(entity.Name, 200) ?? "未命名企业",
            OrgCode = entity.Code,
            ParentCode = group.Code,
            OrgType = OrgTypeEnterprise,
            OrgLevel = EnterpriseNodeLevel,
            OrgPath = $"{ResolveOrgPath(group)}/{nodeCode}",
            LeaderName = Truncate(entity.ContactName, 50),
            LeaderPhone = Truncate(entity.ContactPhone, 20),
            Sort = maxSort <= 0 ? EnterpriseNodeSortBase : maxSort + 10,
            IsValid = entity.IsValid,
            Remark = $"企业节点｜{entity.Name}（{entity.EnterpriseNo}）"
        };

        var ins = await _orgService.Insert(node, clientIp);
        if (!ins.Success)
            return Result<bool>.Fail($"创建企业节点失败：{ins.Error}");

        return Result<bool>.Ok(true);
    }

    /// <summary>
    /// 确保工作区下存在「企业信息」文件夹（L3）。三级兜底：
    /// <list type="number">
    ///   <item>已有「企业信息」<c>Dept</c> → 直接返回</item>
    ///   <item>有旧名「企业用户」<c>Dept</c> → **原地改名**（保留 Code / OrgPath，不动挂靠其下的人员）</item>
    ///   <item>都没有 → 新建（<c>Sort = 40</c>，与 <c>AuditorRegisterService.DefaultGroups</c> 顺序一致）</item>
    /// </list>
    /// </summary>
    private async Task<Result<Sys_Organization>> EnsureEnterpriseGroupAsync(
        Sys_Organization workspace, string? clientIp)
    {
        // ① 已有「企业信息」
        var group = _db.Client.Queryable<Sys_Organization>()
            .Where(x => x.ParentCode == workspace.Code
                        && x.OrgName == EnterpriseGroupName
                        && x.OrgType == OrgTypeDept
                        && x.IsDeleted == false)
            .OrderBy(x => x.Sort)
            .First();

        if (group != null)
            return Result<Sys_Organization>.Ok(group);

        // ② 旧名「企业用户」→ 原地改名
        var legacy = _db.Client.Queryable<Sys_Organization>()
            .Where(x => x.ParentCode == workspace.Code
                        && x.OrgName == LegacyEnterpriseGroupName
                        && x.OrgType == OrgTypeDept
                        && x.IsDeleted == false)
            .OrderBy(x => x.Sort)
            .First();

        if (legacy != null)
        {
            legacy.OrgName = EnterpriseGroupName;
            legacy.OrgLevel ??= EnterpriseGroupLevel;
            legacy.IsValid = 1;

            var upd = await _orgService.Update(legacy, clientIp);
            if (!upd.Success)
                return Result<Sys_Organization>.Fail($"收敛「企业用户」文件夹失败：{upd.Error}");

            return Result<Sys_Organization>.Ok(legacy);
        }

        // ③ 新建
        var code = Guid.NewGuid().ToString("N");
        var folder = new Sys_Organization
        {
            Code = code,
            OrgName = EnterpriseGroupName,
            OrgCode = code,
            ParentCode = workspace.Code,
            OrgType = OrgTypeDept,
            OrgLevel = EnterpriseGroupLevel,
            OrgPath = $"{ResolveOrgPath(workspace)}/{code}",
            Sort = EnterpriseGroupSort,
            IsValid = 1,
            Remark = "企业文件夹｜企业节点挂靠于此（专家端自动创建）"
        };

        var ins = await _orgService.Insert(folder, clientIp);
        if (!ins.Success)
            return Result<Sys_Organization>.Fail($"创建「企业信息」文件夹失败：{ins.Error}");

        return Result<Sys_Organization>.Ok(folder);
    }

    /// <summary>
    /// 同步机构树企业节点的启用状态（禁用 / 启用企业时调用）。
    /// <para><c>includeDisabled: true</c> —— 已禁用节点也必须能查到并改回 1。</para>
    /// </summary>
    private async Task<Result<bool>> SetOrgNodeValidAsync(
        string enterpriseCode, int isValid, string? clientIp)
    {
        var nodes = await _orgService.GetListAsync(
            p => p.OrgCode == enterpriseCode && p.OrgType == OrgTypeEnterprise,
            includeDisabled: true);

        if (!nodes.Success) return Result<bool>.Fail(nodes.Error);

        foreach (var node in nodes.Data)
        {
            if (node.IsValid == isValid) continue;

            node.IsValid = isValid;
            var upd = await _orgService.Update(node, clientIp);
            if (!upd.Success)
                return Result<bool>.Fail($"同步企业节点状态失败：{upd.Error}");
        }

        return Result<bool>.Ok(true);
    }

    /// <summary>
    /// 软删机构树中对应的企业节点（删除企业时调用），否则树里会留下「幽灵企业」。
    ///
    /// <para><b>不走 <c>_orgService.DeleteByCode</c></b>：其内部用 <c>GetOneAsync</c>（自带 <c>IsValid=1</c> 过滤），
    /// 对**已禁用**的节点会返回「记录不存在或已被删除」→ 连带把企业删除也回滚掉。
    /// 故此处自行置 <c>IsDeleted=1</c> + <c>IsValid=0</c> + 删除审计字段。</para>
    /// </summary>
    private async Task<Result<bool>> DetachEnterpriseOrgNodesAsync(
        IEnumerable<string> enterpriseCodes, string? clientIp)
    {
        var operatorCode = UserContext.UserCode;
        var now = DateTime.Now;

        foreach (var code in enterpriseCodes)
        {
            if (string.IsNullOrWhiteSpace(code)) continue;

            var nodes = await _orgService.GetListAsync(
                p => p.OrgCode == code && p.OrgType == OrgTypeEnterprise,
                includeDisabled: true);

            if (!nodes.Success) return Result<bool>.Fail(nodes.Error);

            foreach (var node in nodes.Data)
            {
                node.IsDeleted = true;
                node.IsValid = 0;
                node.DeleteBy = operatorCode;
                node.DeleteTime = now;

                var upd = await _orgService.Update(node, clientIp);
                if (!upd.Success)
                    return Result<bool>.Fail($"删除企业节点失败：{upd.Error}");
            }
        }

        return Result<bool>.Ok(true);
    }

    // ========================================================
    // 八、私有辅助 —— 杂项
    // ========================================================

    /// <summary>生成工作区内递增的企业编号（ENT-0001 起，跳过已占用）</summary>
    private async Task<string> NextEnterpriseNoAsync(string orgCode)
    {
        var count = await _db.Client.Queryable<Enterprise>()
            .Where(x => x.OrgCode == orgCode)
            .CountAsync();

        var seq = count + 1;
        while (true)
        {
            var candidate = $"ENT-{seq:D4}";
            var taken = await _db.Client.Queryable<Enterprise>()
                .Where(x => x.OrgCode == orgCode && x.EnterpriseNo == candidate)
                .AnyAsync();

            if (!taken) return candidate;
            seq++;
        }
    }

    /// <summary>取机构节点的 OrgPath；缺失时退化为 <c>/{Code}</c>（避免拼出 <c>//xxx</c>）</summary>
    private static string ResolveOrgPath(Sys_Organization node)
        => !string.IsNullOrWhiteSpace(node.OrgPath) ? node.OrgPath! : $"/{node.Code}";

    /// <summary>按列长截断（OrgName 200 / LeaderName 50 / LeaderPhone 20）</summary>
    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Length > maxLength ? value[..maxLength] : value;
    }
}

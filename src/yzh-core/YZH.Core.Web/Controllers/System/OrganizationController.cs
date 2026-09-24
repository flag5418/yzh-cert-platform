using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Controllers;
using YZH.Core.Api.Models.Organization;
using YZH.Core.Api.Models.Users;
using YZH.Core.Api.Services;
using YZH.Core.Stand.Helpers;
using YZH.Core.Stand.Models;
using YZH.Core.Stand.Models.Result;
using YZH.Core.Stand.Models.Request;
using YZH.Core.Stand.Models.Config;
using YZH.Core.Stand.Interfaces;

namespace YZH.Core.Web.Controllers.System;

/// <summary>
///     组织机构-人员管理控制器（左树右表一体页面）
///     
///     左树：Sys_Organization（机构树，懒加载 + 增删改 + 启用/禁用）
///     右表：Sys_User（选中机构后，显示该机构下的人员，分页 + 过滤 + 增删改 + 启用/禁用）
///     
///     前端实现参考：
///     ═══════════════════════════════════════════════════════════════
///     【树节点按钮】（默认 = 增加下级 + 修改 + 删除）
///     ┌─────────────────────────────────────────────────────────────┐
///     │  节点名称                              [+] [✎] [🗑] [禁用] │
///     └─────────────────────────────────────────────────────────────┘
///     
///     默认按钮由 TreeConfig.AllowEdit/AllowDelete 控制
///     禁用按钮 → 调用 tree/action/disable → 递归禁用子机构及人员
///     
///     【表格行按钮】（默认 = 修改 + 删除）
///     ┌─────────────────────────────────────────────────────────────┐
///     │  张三  │ 男 │ 审核员 │ 启用 │    [✎] [🗑] [禁用]            │
///     └─────────────────────────────────────────────────────────────┘
///     
///     禁用按钮 → 调用 action/disable → 仅禁用当前人员
///     
///     【工具栏筛选开关】
///     ┌─────────────────────────────────────────────────────────────┐
///     │  [🔍 搜索...]              ☑ 显示已禁用的企业和用户        │
///     └─────────────────────────────────────────────────────────────┘
///     
///     开关关闭（默认）→ 仅显示 IsValid=1 的记录
///     开关开启 → 显示所有记录（含 IsValid=0 的已禁用记录）
///     前端实现：在 FilterRequest.Filters 中追加 { Field: "ShowDisabled", Value: "true/false" }
///     
///     【禁用逻辑 - 递归】
///     禁用机构 A 时：
///     ├── 机构 A → IsValid = 0
///     ├── 子机构 A1 → IsValid = 0（递归）
///     │   └── 子机构 A1.1 → IsValid = 0（递归）
///     │       └── 用户 U1（A1.1 下）→ IsValid = 0
///     └── 用户 U2（A 下）→ IsValid = 0
///     ═══════════════════════════════════════════════════════════════
///     
///     业务规则：
///     1. 同级机构名称不能重复（已软删除的记录自动排除，EntityService 默认过滤 IsDeleted=true）
///     2. 禁止删除包含子机构的父级机构
///     3. 人员必须归属到具体机构（OrgCode 必填）
///     4. 删除/修改操作默认是软删除（仅标记 IsDeleted=true）
///     5. 启用/禁用操作仅设置 IsValid 字段（1=启用，0=禁用），不删除数据
///     6. 禁用机构时，该机构及所有子机构下的用户全部禁用
///     7. 启用人员时，所属机构必须已启用（否则拒绝）
///     8. 不能禁用超级管理员（RoleId=1）
///     
///     API 路由：
///     --- 树（机构） ---
///     POST   /api/Organization/tree/root             获取根节点
///     POST   /api/Organization/tree/children          懒加载子节点
///     POST   /api/Organization/tree/add               新增机构
///     POST   /api/Organization/tree/update            修改机构
///     POST   /api/Organization/tree/delete            删除机构（软删除）
///     POST   /api/Organization/tree/action/disable    禁用机构（递归子机构+用户）
///     POST   /api/Organization/tree/action/enable     启用机构
///     --- 人员 ---
///     GET    /api/Organization/config                 获取页面配置
///     POST   /api/Organization/filter                 人员分页（自动注入机构过滤）
///     POST   /api/Organization/add                   新增人员
///     POST   /api/Organization/update                修改人员
///     POST   /api/Organization/delete                删除人员（软删除）
///     POST   /api/Organization/action/disable         禁用人员
///     POST   /api/Organization/action/enable          启用人员
/// </summary>
[ApiController]
[Route("api/System/[controller]")]
[Route("api/[controller]")]
public class OrganizationController : TreeTableControllerBase<Sys_Organization, Sys_User>
{
    private readonly PasswordHelper _passwordHelper;
    private readonly IRoleService _roleService;

    public OrganizationController(
        EntityService<Sys_Organization> treeEntityService,
        EntityService<Sys_User> tableEntityService,
        IUserContext userContext,
        PasswordHelper passwordHelper,
        IRoleService roleService)
        : base(treeEntityService, tableEntityService, userContext)
    {
        _passwordHelper = passwordHelper;
        _roleService = roleService;

        // TreeConfig（左树配置）
        TreeConfig.NameField = "OrgName";
        TreeConfig.CodeField = "Code";
        TreeConfig.ParentCodeField = "ParentCode";
        TreeConfig.RelateField = "OrgCode";
        TreeConfig.MaxLevel = 10;
        // EnableField 默认 IsValid（启用/禁用唯一字段）
        TreeConfig.AllowToggle = false;     // 禁用基类 toggle-valid 按钮，使用自定义 disable/enable 操作

        // 树节点表单配置（自动加载 Assets/EntityConfigs/sys_organization_form.json）
        TreeFormConfigName = "System/OrganizationForm";

        // 注册行操作（表格：启用/禁用人员）
        RegisterRowAction("disable", DisableUserAsync);
        RegisterRowAction("enable", EnableUserAsync);

        // 注册树操作（左树：启用/禁用机构）
        RegisterTreeAction("disable", DisableOrgRecursiveAsync);
        RegisterTreeAction("enable", EnableOrgAsync);
    }

    // ========================================================
    // 一、配置获取
    // ========================================================

    /// <summary>
    /// GET /api/Organization/config
    /// 返回 EntityConfig（Sys_User.json）+ TreeConfig
    /// 
    /// 前端使用方式：
    /// 1. TreeConfig → 控制左树行为（AllowEdit=显示增改删按钮）
    /// 2. EntityConfig → 驱动右表表格列和表单字段
    /// </summary>
    protected override EntityConfig LoadConfig()
    {
        return EntityConfigHelper.GetConfig("System/User");
    }

    // ========================================================
    // 二、树节点（机构）生命周期钩子
    // ========================================================

    /// <summary>新增机构前校验</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeAddTree(Sys_Organization entity)
    {
        // 同级机构名称唯一性（EntityService.ExistsAsync 自动过滤 IsDeleted=true）
        var nameExists = await TreeEntity.ExistsAsync(o =>
            o.ParentCode == entity.ParentCode &&
            o.OrgName == entity.OrgName);

        if (nameExists.Data)
            return (false, $"同级机构下已存在名为【{entity.OrgName}】的机构");

        // 机构编码唯一性
        if (!string.IsNullOrEmpty(entity.OrgCode))
        {
            var codeExists = await TreeEntity.ExistsByCodeAsync(entity.OrgCode);
            if (codeExists.Data)
                return (false, $"机构编码 {entity.OrgCode} 已存在");
        }

        if (string.IsNullOrEmpty(entity.Code))
            entity.Code = Guid.NewGuid().ToString("N");

        if (string.IsNullOrEmpty(entity.OrgType))
            entity.OrgType = "Dept";

        return (true, null);
    }

    /// <summary>修改机构前校验</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeUpdateTree(Sys_Organization entity)
    {
        var nameExists = await TreeEntity.ExistsAsync(o =>
            o.Code != entity.Code &&
            o.ParentCode == entity.ParentCode &&
            o.OrgName == entity.OrgName);

        if (nameExists.Data)
            return (false, $"同级机构下已存在名为【{entity.OrgName}】的机构");

        return (true, null);
    }

    /// <summary>删除机构前校验：禁止删除含子机构或人员的父级</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeDeleteTree(string[] codes)
    {
        foreach (var code in codes)
        {
            // 检查是否有子机构
            var childCount = await TreeEntity.CountAsync(o => o.ParentCode == code);
            if (childCount.Data > 0)
                return (false, $"该机构下有 {childCount.Data} 个子机构，请先删除子机构");

            // 检查是否有归属人员
            var userCount = await Entity.CountAsync(u => u.OrgCode == code);
            if (userCount.Data > 0)
                return (false, $"该机构下有 {userCount.Data} 个人员，请先转移或删除人员");
        }

        return (true, null);
    }

    // ========================================================
    // 三、人员（表格）生命周期钩子
    // ========================================================

    /// <summary>新增人员前校验</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeAdd(Sys_User entity)
    {
        if (string.IsNullOrEmpty(entity.OrgCode))
            return (false, "请选择所属机构");

        // 须用 GetByCodeAny：ExistsAsync/GetByCode 过滤 IsValid=1，禁用机构会误报「不存在」
        var orgResult = await TreeEntity.GetByCodeAny(entity.OrgCode);
        if (!orgResult.Success || orgResult.Data == null)
            return (false, "所属机构不存在");
        if (orgResult.Data.IsValid == 0)
            return (false, "所属机构已禁用，无法添加人员");

        var userExists = await Entity.ExistsAsync(u => u.UserName == entity.UserName);
        if (userExists.Data)
            return (false, $"账号 {entity.UserName} 已存在");

        var plainPwd = string.IsNullOrEmpty(entity.UserPwd) ? "123456" : entity.UserPwd;
        entity.UserPwd = _passwordHelper.AesEncrypt(plainPwd);
        entity.IsValid = 1;

        return (true, null);
    }

    /// <summary>修改人员前：如果传了新密码则重新加密</summary>
    protected override Task<(bool ok, string? msg)> OnBeforeUpdate(Sys_User entity)
    {
        if (!string.IsNullOrEmpty(entity.UserPwd) && entity.UserPwd.Length < 50)
        {
            entity.UserPwd = _passwordHelper.AesEncrypt(entity.UserPwd);
        }

        return Task.FromResult<(bool, string?)>((true, null));
    }

    /// <summary>查询后处理：字典翻译 + 手机号脱敏</summary>
    protected override async void OnQueried(PagedResult<Sys_User> result)
    {
        // 批量查询角色名称（通过 Sys_RoleUser 关联表，Code 关联）
        var userCodes = result.Items.Where(u => !string.IsNullOrEmpty(u.Code)).Select(u => u.Code!).ToList();
        var roleDict = new Dictionary<string, string>();
        if (userCodes.Any())
        {
            var roleResult = await _roleService.GetRoleNamesByUserCodesAsync(userCodes);
            if (roleResult.Success && roleResult.Data != null)
                roleDict = roleResult.Data;
        }

        foreach (var item in result.Items)
        {
            // 手机号脱敏：138****5678
            if (!string.IsNullOrEmpty(item.PhoneNo) && item.PhoneNo.Length >= 7)
            {
                item.PhoneNo = $"{item.PhoneNo[..3]}****{item.PhoneNo[^4..]}";
            }

            // 性别字典翻译
            item.GenderDesc = item.Gender switch
            {
                1 => "男",
                2 => "女",
                _ => "未知"
            };

            // 角色名称翻译（从 Sys_RoleUser 关联表查询）
            item.RoleName = !string.IsNullOrEmpty(item.Code) && roleDict.ContainsKey(item.Code)
                ? roleDict[item.Code]
                : "未知";

            // 启用状态显示
            item.IsValidDesc = item.IsValid == 0 ? "已禁用" : "启用";
        }
    }

    /// <summary>
    /// 构建过滤条件：支持 ShowDisabled 开关控制是否显示已禁用的记录。
    /// 基类已按 ShowDisabled 自动注入/跳过 IsValid=1 过滤，本覆盖仅保留 ShowDisabled 提取时序说明。
    /// </summary>
    protected override List<FilterItem> OnBuildingFilter(List<FilterItem> filters)
    {
        // 基类已处理：ShowDisabled=true → 不注入 IsValid=1；否则注入 IsValid=1；最后移除 ShowDisabled。
        // 前端默认不传 ShowDisabled → 仅显示 IsValid=1；传 true → 显示所有（含已禁用）。
        return base.OnBuildingFilter(filters);
    }

    /// <summary>
    /// 行按钮配置：仅 Edit + Delete。
    /// 自定义禁用/启用按钮通过 RegisterRowAction 自动注入到 CustomButtons。
    /// 不设置 Enable = true，避免基类额外追加 toggle-valid 按钮造成重复。
    /// </summary>
    protected override RowButtonConfig GetRowButtons()
    {
        return new RowButtonConfig
        {
            Edit = true,
            Delete = true,
            Enable = false
        };
    }

    // ========================================================
    // 四、机构 启用/禁用操作
    // ========================================================

    /// <summary>
    /// 禁用机构（递归）
    /// POST /api/Organization/tree/action/disable
    /// 请求体：{ Code: "机构编码" }
    /// 
    /// 递归逻辑：
    /// 1. 查找该机构 → 设置 IsValid=0
    /// 2. 递归查找所有子机构 → 全部设置 IsValid=0
    /// 3. 根据机构 Code 查找所有关联用户 → 全部设置 IsValid=0
    /// 
    /// 前端调用时机：点击树节点上的"禁用"按钮
    /// </summary>
    private async Task<object?> DisableOrgRecursiveAsync(Sys_Organization entity)
    {
        // GetByCodeAny：禁用后 IsValid=0，GetByCode 会被过滤掉导致无法再次操作
        var result = await TreeEntity.GetByCodeAny(entity.Code);
        if (!result.Success || result.Data == null)
            return "机构不存在";

        var org = result.Data;
        var affectedCount = 0;

        // 1. 禁用当前机构
        org.IsValid = 0;
        await TreeEntity.Update(org, UserContext.ClientIp);
        affectedCount++;

        // 2. 递归禁用所有子机构及其用户
        var childCount = await DisableChildrenRecursive(org.Code);

        return $"已禁用机构及其 {childCount} 个子机构/用户";
    }

    /// <summary>递归禁用子机构及用户，返回禁用的子节点数量</summary>
    private async Task<int> DisableChildrenRecursive(string parentCode)
    {
        int count = 0;

        // 获取子机构（GetViewList 默认 includeDisabled，已禁用节点仍可继续级联）
        var children = await TreeEntity.GetChildren(parentCode);
        foreach (var child in children)
        {
            // 禁用子机构
            child.IsValid = 0;
            await TreeEntity.Update(child, UserContext.ClientIp);
            count++;

            // 递归处理子机构的子机构
            count += await DisableChildrenRecursive(child.Code);

            // 禁用该子机构下的所有用户（includeDisabled：已禁用用户需能被再次更新）
            var users = await Entity.GetListAsync(u => u.OrgCode == child.Code, includeDisabled: true);
            if (users.Success && users.Data != null)
            {
                foreach (var user in users.Data)
                {
                    if (user.IsValid == 1) // 只更新当前启用的，避免无意义操作
                    {
                        user.IsValid = 0;
                        await Entity.Update(user, UserContext.ClientIp);
                        count++;
                    }
                }
            }
        }

        // 禁用当前父机构下的直属用户
        var directUsers = await Entity.GetListAsync(u => u.OrgCode == parentCode, includeDisabled: true);
        if (directUsers.Success && directUsers.Data != null)
        {
            foreach (var user in directUsers.Data)
            {
                if (user.IsValid == 1)
                {
                    user.IsValid = 0;
                    await Entity.Update(user, UserContext.ClientIp);
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>
    /// 启用机构
    /// POST /api/Organization/tree/action/enable
    /// 请求体：{ Code: "机构编码" }
    /// 
    /// 注意：启用机构不会自动启用已禁用的子机构和用户
    /// 如需批量启用，前端需要逐一下发启用请求
    /// </summary>
    private async Task<object?> EnableOrgAsync(Sys_Organization entity)
    {
        // GetByCodeAny：已禁用机构 IsValid=0，GetByCode 查不到
        var result = await TreeEntity.GetByCodeAny(entity.Code);
        if (!result.Success || result.Data == null)
            return "机构不存在";

        var org = result.Data;
        org.IsValid = 1;
        var updateResult = await TreeEntity.Update(org, UserContext.ClientIp);
        if (!updateResult.Success)
            return updateResult.Error;

        return "已启用该机构";
    }

    // ========================================================
    // 五、人员 启用/禁用操作
    // ========================================================

    /// <summary>
    /// 禁用人员
    /// POST /api/Organization/action/disable
    /// 请求体 = 行实体 { Code: "用户编码" }
    /// 
    /// 业务规则：
    /// - 不能禁用超级管理员（RoleId=1）
    /// - 禁用后该用户无法登录系统
    /// </summary>
    private async Task<Result<ApiResponse<object?>>> DisableUserAsync(Sys_User entity)
    {
        var result = await Entity.GetByCode(entity.Code);
        if (!result.Success || result.Data == null)
            return Result<ApiResponse<object?>>.Fail("人员不存在");

        // 不能禁用超级管理员（通过 Sys_RoleUser 关联表查询角色编码）
        var roleResult = await _roleService.GetRoleCodeByUserCodeAsync(result.Data.Code);
        if (roleResult.Success && roleResult.Data == MenuPermissionService.SuperAdminRoleCode)
            return Result<ApiResponse<object?>>.Fail("不能禁用超级管理员账号");

        var user = result.Data;
        user.IsValid = 0;
        var updateResult = await Entity.Update(user, UserContext.ClientIp);
        if (!updateResult.Success)
            return Result<ApiResponse<object?>>.Fail(updateResult.Error);

        return Result<ApiResponse<object?>>.Ok(ApiResponse<object?>.Ok("已禁用该人员"));
    }

    /// <summary>
    /// 启用人员
    /// POST /api/Organization/action/enable
    /// 请求体 = 行实体 { Code: "用户编码" }
    /// 
    /// 业务规则：
    /// - 所属机构必须已启用（IsValid=1）才能启用人员
    /// </summary>
    private async Task<Result<ApiResponse<object?>>> EnableUserAsync(Sys_User entity)
    {
        // GetByCodeAny：已禁用人员 IsValid=0，GetByCode 查不到
        var result = await Entity.GetByCodeAny(entity.Code);
        if (!result.Success || result.Data == null)
            return Result<ApiResponse<object?>>.Fail("人员不存在");

        var user = result.Data;

        // 检查所属机构是否启用
        if (!string.IsNullOrEmpty(user.OrgCode))
        {
            var orgResult = await TreeEntity.GetByCodeAny(user.OrgCode);
            if (orgResult.Success && orgResult.Data != null && orgResult.Data.IsValid == 0)
                return Result<ApiResponse<object?>>.Fail("所属机构已禁用，请先启用机构");
        }

        user.IsValid = 1;
        var updateResult = await Entity.Update(user, UserContext.ClientIp);
        if (!updateResult.Success)
            return Result<ApiResponse<object?>>.Fail(updateResult.Error);

        return Result<ApiResponse<object?>>.Ok(ApiResponse<object?>.Ok("已启用该人员"));
    }

    /// <summary>
    /// 扩展树节点 Extra 载荷
    /// 写入 IsValid / isValid（启用/禁用唯一字段），前端 Toggle 按钮与状态标签读取。
    /// </summary>
    protected override TreeItemDto MapToTreeItem(Sys_Organization entity, int level)
    {
        var dto = base.MapToTreeItem(entity, level);
        dto.Extra ??= new Dictionary<string, object>();
        dto.Extra["IsValid"] = entity.IsValid;
        dto.Extra["isValid"] = entity.IsValid;
        // 编辑弹窗回填：OrganizationForm 字段白名单从 Extra 取值（OrgCode 基类 TreeMapper 未提取）
        dto.Extra["OrgCode"] = entity.OrgCode ?? string.Empty;
        return dto;
    }
}

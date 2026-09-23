using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Attributes;
using YZH.Core.Api.Controllers;
using YZH.Core.Api.Models.Users;
using YZH.Core.Api.Services;
using YZH.Core.Stand.Helpers;
using YZH.Core.Stand.Models;
using YZH.Core.Stand.Models.Result;
using YZH.Core.Stand.Models.Request;
using YZH.Core.Stand.Interfaces;

namespace YZH.Core.Web.Controllers.System;

/// <summary>
///     用户管理控制器（新架构版）
///     
///     继承 YzhControllerBase 获得：
///     - 标准 CRUD（Add/Update/Delete/GetPage/GetConfig）
///     - 原子方法（AddCore/UpdateCore/DeleteCore/GetPageCore）
///     - 生命周期钩子
///     - 行操作注册
///     
///     路由：api/User（与 Vol 框架原生路由保持一致）
/// </summary>
[ApiController]
[Route("api/System/User")]
[Authorize]
public class UserController : YzhControllerBase<Sys_User>
{
    private readonly PasswordHelper _passwordHelper;
    private readonly ICaptchaService _captchaService;
    private readonly IRoleService _roleService;
    private readonly TokenVersionService _tokenVersion;

    public UserController(
        EntityService<Sys_User> entityService,
        IUserContext userContext,
        PasswordHelper passwordHelper,
        ICaptchaService captchaService,
        IRoleService roleService,
        TokenVersionService tokenVersion)
        : base(entityService, userContext)
    {
        _passwordHelper = passwordHelper;
        _captchaService = captchaService;
        _roleService = roleService;
        _tokenVersion = tokenVersion;

        // 注册行操作（按钮名称 → 处理函数）
        RegisterRowAction("Enable", EnableUser);
        RegisterRowAction("Disable", DisableUser);
        RegisterRowAction("ResetPassword", ResetPassword);
    }

    /// <summary>
    ///     获取登录验证码（允许匿名访问）
    ///     路由：GET api/User/getVierificationCode
    /// </summary>
    [HttpGet("getVierificationCode")]
    [HttpGet("~/api/User/getVierificationCode")]
    [AllowAnonymous]
    [ApiDescription("获取登录验证码", "系统", "系统管理/用户管理", true)]
    public IActionResult GetVierificationCode()
    {
        var imageBase64 = _captchaService.Generate(out var code, out var uuid);
        return new JsonResult(new { img = imageBase64, uuid });
    }

    #region 查询钩子

    /// <summary>查询后处理 - 脱敏手机号、填充角色名称</summary>
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

            // 填充角色名称（从 Sys_RoleUser 关联表查询）
            item.RoleName = !string.IsNullOrEmpty(item.Code) && roleDict.ContainsKey(item.Code)
                ? roleDict[item.Code]
                : "未知";
        }
    }

    #endregion

    #region 新增钩子

    /// <summary>新增前处理 - 校验账号唯一性、加密密码</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeAdd(Sys_User entity)
    {
        // 校验账号唯一性
        var result = await Entity.ExistsByCodeAsync(entity.UserName);
        if (result.Data == true)
        {
            return (false, $"账号 {entity.UserName} 已存在");
        }

        // 加密密码（默认密码 123456）
        var plainPwd = string.IsNullOrEmpty(entity.UserPwd) ? "123456" : entity.UserPwd;
        entity.UserPwd = _passwordHelper.AesEncrypt(plainPwd);

        // 默认启用
        entity.Enable = 1;
        return (true, null);
    }

    #endregion

    #region 修改钩子

    /// <summary>修改前处理 - 如果传了新密码则加密</summary>
    protected override Task<(bool ok, string? msg)> OnBeforeUpdate(Sys_User entity)
    {
        // 如果传了新密码（长度 < 50 认为是明文），则加密
        if (!string.IsNullOrEmpty(entity.UserPwd) && entity.UserPwd.Length < 50)
        {
            entity.UserPwd = _passwordHelper.AesEncrypt(entity.UserPwd);
        }
        return Task.FromResult<(bool, string?)>((true, null));
    }

    #endregion

    #region 行操作

    /// <summary>启用用户（POST api/SysUser/action/Enable）</summary>
    private async Task<Result<ApiResponse<object?>>> EnableUser(Sys_User entity)
    {
        var result = await Entity.GetByCode(entity.Code);
        if (!result.Success || result.Data == null)
            return Result<ApiResponse<object?>>.Fail("用户不存在");

        var user = result.Data;
        user.Enable = 1;
        var updateResult = await Entity.Update(user, UserContext.ClientIp);
        if (!updateResult.Success)
            return Result<ApiResponse<object?>>.Fail(updateResult.Error);

        return Result<ApiResponse<object?>>.Ok(ApiResponse<object?>.Ok("已启用该用户"));
    }

    /// <summary>禁用用户（POST api/SysUser/action/Disable）</summary>
    private async Task<Result<ApiResponse<object?>>> DisableUser(Sys_User entity)
    {
        var result = await Entity.GetByCode(entity.Code);
        if (!result.Success || result.Data == null)
            return Result<ApiResponse<object?>>.Fail("用户不存在");

        var user = result.Data;
        
        // 禁止禁用超级管理员（通过 Sys_RoleUser 关联表查询角色编码）
        var roleResult = await _roleService.GetRoleCodeByUserCodeAsync(user.Code);
        if (roleResult.Success && roleResult.Data == MenuPermissionService.SuperAdminRoleCode)
            return Result<ApiResponse<object?>>.Fail("不能禁用超级管理员账号");

        user.Enable = 0;
        var updateResult = await Entity.Update(user, UserContext.ClientIp);
        if (!updateResult.Success)
            return Result<ApiResponse<object?>>.Fail(updateResult.Error);

        return Result<ApiResponse<object?>>.Ok(ApiResponse<object?>.Ok("已禁用该用户"));
    }

    /// <summary>重置密码（POST api/SysUser/action/ResetPassword）</summary>
    private async Task<Result<ApiResponse<object?>>> ResetPassword(Sys_User entity)
    {
        var result = await Entity.GetByCode(entity.Code);
        if (!result.Success || result.Data == null)
            return Result<ApiResponse<object?>>.Fail("用户不存在");

        var user = result.Data;
        user.UserPwd = _passwordHelper.AesEncrypt("123456");
        user.LastModifyPwdDate = DateTime.UtcNow;
        
        var updateResult = await Entity.Update(user, UserContext.ClientIp);
        if (!updateResult.Success)
            return Result<ApiResponse<object?>>.Fail(updateResult.Error);

        return Result<ApiResponse<object?>>.Ok(ApiResponse<object?>.Ok("密码已重置为 123456"));
    }

    #endregion

    #region 个人中心（Vol 兼容端点）

    /// <summary>
    ///     个人中心 - 获取当前登录用户信息
    ///
    ///     路由：POST / GET `api/User/getCurrentUserInfo`（Vol 兼容绝对路由）
    ///     历史实现：`src/old/.../Sys_UserController.cs` `[HttpPost, Route("getCurrentUserInfo")]`
    ///               → `Sys_UserService.GetCurrentUserInfo()`
    ///
    ///     响应：`ApiResponse&lt;CurrentUserInfoDto&gt;`，data 内全 PascalCase（§16.9 铁律）。
    ///     用途：页面刷新后回填「个人中心」与顶栏用户名（登录响应只给 Token/UserName/RoleCode，
    ///          没有 Email/PhoneNo/Gender 等资料字段，且未持久化）。
    /// </summary>
    [HttpPost("getCurrentUserInfo")]
    [HttpGet("getCurrentUserInfo")]
    [HttpPost("~/api/User/getCurrentUserInfo")]
    [HttpGet("~/api/User/getCurrentUserInfo")]
    [ApiDescription("获取当前用户信息", "系统", "系统管理/个人中心", true)]
    public async Task<IActionResult> GetCurrentUserInfo()
    {
        var userCode = UserContext.UserCode;
        if (string.IsNullOrEmpty(userCode))
            return Unauthorized(ApiResponse.Fail("未登录或登录已失效"));

        var result = await Entity.GetOne(u => u.Code == userCode);
        if (!result.Success || result.Data == null)
            return NotFound(ApiResponse.Fail($"用户不存在：{userCode}"));

        var user = result.Data;

        // RoleName / OrgName 是 v_sys_user 的视图字段；若本次查询走的是物理表则为空，按 Code 关联回填。
        if (string.IsNullOrEmpty(user.RoleName))
        {
            var roleNames = await _roleService.GetRoleNamesByUserCodesAsync(new[] { userCode });
            if (roleNames.Success && roleNames.Data != null && roleNames.Data.TryGetValue(userCode, out var roleName))
                user.RoleName = roleName;
        }

        var roleCodeResult = await _roleService.GetRoleCodeByUserCodeAsync(userCode);

        return Ok(ApiResponse<CurrentUserInfoDto>.Ok(new CurrentUserInfoDto
        {
            UserCode = user.Code,
            UserName = user.UserName,
            UserTrueName = user.UserTrueName,
            Gender = user.Gender,
            PhoneNo = user.PhoneNo,
            Email = user.Email,
            Address = user.Address,
            Remark = user.Remark,
            HeadImageUrl = user.HeadImageUrl,
            OrgCode = user.OrgCode,
            OrgName = user.OrgName,
            RoleCode = roleCodeResult.Success ? roleCodeResult.Data : null,
            RoleName = user.RoleName,
            CreateTime = user.CreateTime,
            LastLoginDate = user.LastLoginDate
        }, "获取成功"));
    }

    /// <summary>
    ///     个人中心 - 修改本人密码（自助改密，需校验旧密码）
    ///
    ///     路由：POST `api/User/modifyPwd`（Vol 兼容绝对路由）
    ///     历史实现：`Sys_UserService.ModifyPwd(oldPwd, newPwd)`
    ///
    ///     请求体（PascalCase，§16.9 铁律）：`{ "OldPwd": "...", "NewPwd": "..." }`
    ///     ⚠️ 历史项目用的是 camelCase `oldPwd`/`newPwd`；新架构统一 PascalCase，前端已同步。
    ///
    ///     改密成功后调用 `TokenVersionService.BumpVersionAsync` 递增 SSO 版本号 —— 与登录时同一机制。
    ///     ⚠️ 实测发现（2026-09-22）：**该版本号目前并未在请求鉴权链路中被校验** ——
    ///        重新登录后旧 Token 依然能通过鉴权（HTTP 200），即「挤号」实际未生效。
    ///        这是框架层的既有缺口（`TokenVersionService` 只写不校验），与本次新增端点无关；
    ///        前端因此仍在改密成功后主动清除本地 Token 并跳转登录页。
    /// </summary>
    [HttpPost("modifyPwd")]
    [HttpPost("~/api/User/modifyPwd")]
    [ApiDescription("修改本人密码", "系统", "系统管理/个人中心", true)]
    public async Task<IActionResult> ModifyPwd([FromBody] ModifyPwdRequest req)
    {
        if (req == null)
            return BadRequest(ApiResponse.Fail("参数不能为空"));

        var oldPwd = req.OldPwd?.Trim();
        var newPwd = req.NewPwd?.Trim();

        if (string.IsNullOrEmpty(oldPwd)) return BadRequest(ApiResponse.Fail("旧密码不能为空"));
        if (string.IsNullOrEmpty(newPwd)) return BadRequest(ApiResponse.Fail("新密码不能为空"));
        if (newPwd.Length < 6) return BadRequest(ApiResponse.Fail("密码不能少于 6 位"));

        var userCode = UserContext.UserCode;
        if (string.IsNullOrEmpty(userCode))
            return Unauthorized(ApiResponse.Fail("未登录或登录已失效"));

        var result = await Entity.GetOne(u => u.Code == userCode);
        if (!result.Success || result.Data == null)
            return NotFound(ApiResponse.Fail("用户不存在"));

        var user = result.Data;
        var currentPwd = user.UserPwd ?? string.Empty;

        if (!_passwordHelper.VerifyDes(oldPwd, currentPwd))
            return BadRequest(ApiResponse.Fail("旧密码不正确"));
        if (_passwordHelper.VerifyDes(newPwd, currentPwd))
            return BadRequest(ApiResponse.Fail("新密码不能与旧密码相同"));

        user.UserPwd = _passwordHelper.AesEncrypt(newPwd);
        user.LastModifyPwdDate = DateTime.Now;

        // 仅允许改这两个字段（+审计字段），避免把整实体回写时误覆盖并发修改的其他列
        var updateResult = await Entity.Update(user, UserContext.ClientIp, new[]
        {
            nameof(Sys_User.UserPwd),
            nameof(Sys_User.LastModifyPwdDate),
            nameof(Sys_User.UpdateTime),
            nameof(Sys_User.UpdateBy)
        });
        if (!updateResult.Success)
            return BadRequest(ApiResponse.Fail(updateResult.Error ?? "密码修改失败"));

        await _tokenVersion.BumpVersionAsync(userCode);

        return Ok(ApiResponse.Ok("密码修改成功，请重新登录"));
    }

    /// <summary>
    ///     个人中心 - 修改本人资料
    ///
    ///     路由：POST `api/User/updateUserInfo`（Vol 兼容绝对路由）
    ///     历史实现：`Sys_UserController.UpdateUserInfo(Sys_User user)`
    ///               → 只更新 `UserTrueName / Gender / Remark / HeadImageUrl`
    ///
    ///     请求体（PascalCase）：`{ UserTrueName, Gender, Remark, HeadImageUrl, Email, PhoneNo }`
    ///
    ///     ⚠️ 与历史实现的差异（有意修正）：
    ///       历史代码的更新字段白名单漏掉了 `Email` / `PhoneNo`，但前端表单却在编辑并提交它们
    ///       —— 保存后邮箱/手机号静默丢失。新实现把这两个字段纳入白名单。
    ///
    ///     安全：只允许改「个人资料」字段；`Code` / `Enable` / `UserPwd` / `OrgCode` 等
    ///          均取自库中原值，不接受请求体传入（防越权与提权）。
    /// </summary>
    [HttpPost("updateUserInfo")]
    [HttpPost("~/api/User/updateUserInfo")]
    [ApiDescription("修改本人资料", "系统", "系统管理/个人中心", true)]
    public async Task<IActionResult> UpdateUserInfo([FromBody] UpdateUserInfoRequest req)
    {
        if (req == null)
            return BadRequest(ApiResponse.Fail("参数不能为空"));

        var userCode = UserContext.UserCode;
        if (string.IsNullOrEmpty(userCode))
            return Unauthorized(ApiResponse.Fail("未登录或登录已失效"));

        var result = await Entity.GetOne(u => u.Code == userCode);
        if (!result.Success || result.Data == null)
            return NotFound(ApiResponse.Fail("用户不存在"));

        var user = result.Data;

        var before = (user.UserTrueName, user.Gender, user.Remark, user.HeadImageUrl, user.Email, user.PhoneNo);

        if (req.UserTrueName != null) user.UserTrueName = req.UserTrueName.Trim();
        if (req.Gender != null) user.Gender = req.Gender;
        if (req.Remark != null) user.Remark = req.Remark;
        if (req.HeadImageUrl != null) user.HeadImageUrl = req.HeadImageUrl;
        if (req.Email != null) user.Email = req.Email.Trim();
        if (req.PhoneNo != null) user.PhoneNo = req.PhoneNo.Trim();

        // 无变化时直接返回：否则 MySQL 影响行数为 0，会被 ORM 判成「更新失败：记录不存在或无变化」
        if (before == (user.UserTrueName, user.Gender, user.Remark, user.HeadImageUrl, user.Email, user.PhoneNo))
            return Ok(ApiResponse.Ok("修改成功"));

        var updateFields = new[]
        {
            nameof(Sys_User.UserTrueName),
            nameof(Sys_User.Gender),
            nameof(Sys_User.Remark),
            nameof(Sys_User.HeadImageUrl),
            nameof(Sys_User.Email),
            nameof(Sys_User.PhoneNo),
            nameof(Sys_User.UpdateTime),
            nameof(Sys_User.UpdateBy)
        };

        var updateResult = await Entity.Update(user, UserContext.ClientIp, updateFields);
        if (!updateResult.Success)
            return BadRequest(ApiResponse.Fail(updateResult.Error ?? "保存失败"));

        return Ok(ApiResponse.Ok("修改成功"));
    }

    #endregion
}

/// <summary>
///     当前用户信息（个人中心）—— 字段名与 `Sys_User` 的 C# 属性名逐字一致（§16.9 铁律）
/// </summary>
public class CurrentUserInfoDto
{
    /// <summary>用户编码（业务主键）</summary>
    public string UserCode { get; set; } = string.Empty;
    /// <summary>登录账号</summary>
    public string UserName { get; set; } = string.Empty;
    /// <summary>真实姓名</summary>
    public string UserTrueName { get; set; } = string.Empty;
    /// <summary>性别（0=未知，1=男，2=女）</summary>
    public int? Gender { get; set; }
    /// <summary>手机号</summary>
    public string? PhoneNo { get; set; }
    /// <summary>邮箱</summary>
    public string? Email { get; set; }
    /// <summary>地址</summary>
    public string? Address { get; set; }
    /// <summary>备注</summary>
    public string? Remark { get; set; }
    /// <summary>头像 URL</summary>
    public string? HeadImageUrl { get; set; }
    /// <summary>所属机构编码</summary>
    public string? OrgCode { get; set; }
    /// <summary>所属机构名称（视图字段）</summary>
    public string? OrgName { get; set; }
    /// <summary>角色编码</summary>
    public string? RoleCode { get; set; }
    /// <summary>角色名称</summary>
    public string? RoleName { get; set; }
    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }
    /// <summary>最后登录时间</summary>
    public DateTime? LastLoginDate { get; set; }
}

/// <summary>修改密码请求（PascalCase）</summary>
public class ModifyPwdRequest
{
    public string? OldPwd { get; set; }
    public string? NewPwd { get; set; }
}

/// <summary>
///     修改本人资料请求（PascalCase）
///     全部可空 —— 只更新显式传入的字段，未传的字段保持库中原值。
/// </summary>
public class UpdateUserInfoRequest
{
    public string? UserTrueName { get; set; }
    public int? Gender { get; set; }
    public string? Remark { get; set; }
    public string? HeadImageUrl { get; set; }
    public string? Email { get; set; }
    public string? PhoneNo { get; set; }
}


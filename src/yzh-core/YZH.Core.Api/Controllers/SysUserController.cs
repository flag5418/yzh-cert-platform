using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Models.Users;
using YZH.Core.Api.Services;
using YZH.Core.Stand.Helpers;
using YZH.Core.Stand.Models;

namespace YZH.Core.Api.Controllers;

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
[Route("api/User")]
[Authorize]
public class SysUserController : YzhControllerBase<Sys_User>
{
    private readonly PasswordHelper _passwordHelper;
    private readonly ICaptchaService _captchaService;

    public SysUserController(
        EntityService<Sys_User> entityService,
        IUserContext userContext,
        PasswordHelper passwordHelper,
        ICaptchaService captchaService)
        : base(entityService, userContext)
    {
        _passwordHelper = passwordHelper;
        _captchaService = captchaService;

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
    [AllowAnonymous]
    public IActionResult GetVierificationCode()
    {
        var imageBase64 = _captchaService.Generate(out var code, out var uuid);
        return new JsonResult(new { img = imageBase64, uuid });
    }

    #region 查询钩子

    /// <summary>查询后处理 - 脱敏手机号、填充角色名称</summary>
    protected override void OnQueried(PagedResult<Sys_User> result)
    {
        foreach (var item in result.Items)
        {
            // 手机号脱敏：138****5678
            if (!string.IsNullOrEmpty(item.PhoneNo) && item.PhoneNo.Length >= 7)
            {
                item.PhoneNo = $"{item.PhoneNo[..3]}****{item.PhoneNo[^4..]}";
            }

            // 填充角色名称（视图字段）
            item.RoleName = item.RoleId switch
            {
                1 => "超级管理员",
                10 => "总管理员",
                20 => "审核管理员",
                21 => "审核组长",
                22 => "普通审核员",
                30 => "企业账号",
                _ => "未知"
            };
        }
    }

    #endregion

    #region 新增钩子

    /// <summary>新增前处理 - 校验账号唯一性、加密密码</summary>
    protected override async Task OnBeforeAdd(Sys_User entity)
    {
        // 校验账号唯一性
        var result = await Entity.ExistsByCodeAsync(entity.UserName);
        if (result.Data == true)
        {
            throw new InvalidOperationException($"账号 {entity.UserName} 已存在");
        }

        // 加密密码（默认密码 123456）
        var plainPwd = string.IsNullOrEmpty(entity.UserPwd) ? "123456" : entity.UserPwd;
        entity.UserPwd = _passwordHelper.AesEncrypt(plainPwd);

        // 默认启用
        entity.Enable = 1;
    }

    #endregion

    #region 修改钩子

    /// <summary>修改前处理 - 如果传了新密码则加密</summary>
    protected override Task OnBeforeUpdate(Sys_User entity)
    {
        // 如果传了新密码（长度 < 50 认为是明文），则加密
        if (!string.IsNullOrEmpty(entity.UserPwd) && entity.UserPwd.Length < 50)
        {
            entity.UserPwd = _passwordHelper.AesEncrypt(entity.UserPwd);
        }
        return Task.CompletedTask;
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
        
        // 禁止禁用超级管理员
        if (user.RoleId == 1)
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
}

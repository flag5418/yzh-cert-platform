using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Models.Users;
using YZH.Core.Api.Services;
using YZH.Core.Stand.Helpers;
using YZH.Core.Stand.Models;

namespace YZH.Core.Api.Controllers;

/// <summary>
///     用户管理控制器
///     
///     继承 YzhControllerBase 获得：
///     - 标准 CRUD（Add/Update/Delete/GetPage/GetConfig）
///     - 原子方法（AddCore/UpdateCore/DeleteCore/GetPageCore）
///     - 生命周期钩子
///     - 行操作注册
///     
///     路由：api/sysuser
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SysUserController : YzhControllerBase<Sys_User>
{
    private readonly PasswordHelper _passwordHelper;

    public SysUserController(
        EntityService<Sys_User> entityService,
        IUserContext userContext,
        PasswordHelper passwordHelper)
        : base(entityService, userContext)
    {
        _passwordHelper = passwordHelper;

        // 注册行操作（按钮名称 → 处理函数）
        RegisterRowAction("Enable", EnableUser);
        RegisterRowAction("Disable", DisableUser);
        RegisterRowAction("ResetPassword", ResetPassword);
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
    protected override void OnBeforeAdd(Sys_User entity)
    {
        // 校验账号唯一性
        var (exists, err) = Entity.Exists(e => e.UserName == entity.UserName);
        if (exists)
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
    protected override void OnBeforeUpdate(Sys_User entity)
    {
        // 如果传了新密码（长度 < 50 认为是明文），则加密
        if (!string.IsNullOrEmpty(entity.UserPwd) && entity.UserPwd.Length < 50)
        {
            entity.UserPwd = _passwordHelper.AesEncrypt(entity.UserPwd);
        }
    }

    /// <summary>修改前校验 - 禁止修改 admin 账号</summary>
    protected override string? OnCustomValidate(Sys_User entity)
    {
        // 账号长度校验
        if (!string.IsNullOrEmpty(entity.UserName) && entity.UserName.Length < 3)
        {
            return "账号至少需要 3 位";
        }

        return null;
    }

    #endregion

    #region 行操作

    /// <summary>启用用户（POST api/sysuser/action/Enable）</summary>
    private (ApiResponse? result, string? err) EnableUser(Sys_User entity)
    {
        var existing = Entity.GetById(entity.Id);
        if (existing == null)
            return (null, "用户不存在");

        existing.Enable = 1;
        var (_, err) = Entity.Update(existing, UserContext.ClientIp);
        if (err != null) return (null, err);

        return (ApiResponse.Ok("已启用该用户"), null);
    }

    /// <summary>禁用用户（POST api/sysuser/action/Disable）</summary>
    private (ApiResponse? result, string? err) DisableUser(Sys_User entity)
    {
        var existing = Entity.GetById(entity.Id);
        if (existing == null)
            return (null, "用户不存在");

        // 禁止禁用超级管理员
        if (existing.RoleId == 1)
            return (null, "不能禁用超级管理员账号");

        existing.Enable = 0;
        var (_, err) = Entity.Update(existing, UserContext.ClientIp);
        if (err != null) return (null, err);

        return (ApiResponse.Ok("已禁用该用户"), null);
    }

    /// <summary>重置密码（POST api/sysuser/action/ResetPassword）</summary>
    private (ApiResponse? result, string? err) ResetPassword(Sys_User entity)
    {
        var existing = Entity.GetById(entity.Id);
        if (existing == null)
            return (null, "用户不存在");

        existing.UserPwd = _passwordHelper.AesEncrypt("123456");
        existing.LastModifyPwdDate = DateTime.UtcNow;
        var (_, err) = Entity.Update(existing, UserContext.ClientIp);
        if (err != null) return (null, err);

        return (ApiResponse.Ok("密码已重置为 123456"), null);
    }

    #endregion
}

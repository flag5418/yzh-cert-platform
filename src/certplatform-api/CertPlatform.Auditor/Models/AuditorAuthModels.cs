namespace CertPlatform.Auditor.Models;

/// <summary>
/// 专家注册请求
///
/// <para>设计前提（一次注册 = 一个工作区）：</para>
/// <list type="bullet">
///   <item>一次注册只能针对**一个**体系认证机构（<see cref="CertBodyCode"/>），
///         注册后该工作区的标准集 / 规则集 / 报告体系即由该机构决定</item>
///   <item>注册人 = 虚拟体系机构管理员，注册成功即默认拥有该角色的全部菜单与接口权限
///         （权限由后台预配在 <c>Sys_RoleMenu</c> + <c>sys_role_api</c> 上，注册只绑 <c>Sys_RoleUser</c>）</item>
/// </list>
/// </summary>
public class AuditorRegisterRequest
{
    /// <summary>所选体系认证机构 Code（来源：cert_certification_body.Code，且 IsValid=1 且 Status=active）</summary>
    public string CertBodyCode { get; set; } = string.Empty;

    /// <summary>登录名（全局唯一，Sys_User.UserName）</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>真实姓名（Sys_User.UserTrueName）</summary>
    public string UserTrueName { get; set; } = string.Empty;

    /// <summary>登录密码（明文传入，后端 AES 加密后落库）</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>手机号（可选）</summary>
    public string? PhoneNo { get; set; }

    /// <summary>邮箱（可选）</summary>
    public string? Email { get; set; }
}

/// <summary>
/// 认证机构下拉项（注册页「选择体系认证机构」的数据源）
///
/// <para>⚠️ 字段名 PascalCase，与 <c>cert_certification_body</c> 列名逐字一致（§16.9 铁律）。</para>
/// </summary>
public class CertBodyOptionDto
{
    /// <summary>机构 Code（提交注册时回传）</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>机构全称（下拉显示）</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>机构简称（可空）</summary>
    public string? ShortName { get; set; }

    /// <summary>CNAS 机构编号（可空，副标题展示）</summary>
    public string? CbCode { get; set; }
}

/// <summary>
/// 专家注册结果
/// </summary>
public class AuditorRegisterResultDto
{
    /// <summary>新建用户 Code（= Sys_User.Code，业务主键）</summary>
    public string UserCode { get; set; } = string.Empty;

    /// <summary>登录名</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>工作区机构 Code（= Sys_Organization.Code，注册人 OrgCode 指向它）</summary>
    public string OrgCode { get; set; } = string.Empty;

    /// <summary>工作区机构名称</summary>
    public string OrgName { get; set; } = string.Empty;

    /// <summary>绑定的角色 Code（固定 ROLE_AUDIT_CLIENT_ADMIN）</summary>
    public string RoleCode { get; set; } = string.Empty;
}

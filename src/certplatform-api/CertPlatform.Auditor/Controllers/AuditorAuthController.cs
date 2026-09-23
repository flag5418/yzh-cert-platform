using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using CertPlatform.Auditor.Models;
using CertPlatform.Auditor.Services;
using YZH.Core.Api.Attributes;
using YZH.Core.Stand.Models.Result;

namespace CertPlatform.Auditor.Controllers;

/// <summary>
/// 专家端认证控制器（注册 / 机构下拉）
///
/// <para><b>路由</b>：<c>api/AuditorAuth</c> —— 与历史项目
/// （<c>src/old/.../Controllers/Auditor/Partial/AuthController.cs</c>）保持一致，
/// 前端调用方式不变：</para>
/// <code>
/// POST /api/AuditorAuth/Register        专家注册（匿名）
/// POST /api/AuditorAuth/GetOrgList      可选体系认证机构下拉（匿名）
/// </code>
///
/// <para><b>为什么不继承 YzhControllerBase&lt;V&gt;</b>：</para>
/// <list type="bullet">
///   <item>注册不是单表 CRUD，而是「机构 + 分组 + 用户 + 角色」四表事务编排，
///         基类的 AddCore/ValidateEntity 链路（依赖 EntityConfig 的 BcFlag 校验）不适用</item>
///   <item>基类带类级 <c>[YZHAuthorize]</c>，而注册必须匿名可访问；
///         直接继承 ControllerBase + <c>[AllowAnonymous]</c> 与框架层
///         <c>AuthController</c>（<c>api/User/login</c>）保持同一风格</item>
/// </list>
///
/// <para><b>登录不在此实现</b>：复用框架层 <c>POST /api/User/login</c>。
/// 登录返回的 <c>RoleCode</c> 来自 <c>Sys_RoleUser</c> 关联表，正是本控制器注册时写入的那条。</para>
/// </summary>
[ApiController]
[Route("api/AuditorAuth")]
public class AuditorAuthController : ControllerBase
{
    private readonly AuditorRegisterService _registerService;
    private readonly ILogger<AuditorAuthController> _logger;

    public AuditorAuthController(
        AuditorRegisterService registerService,
        ILogger<AuditorAuthController> logger)
    {
        _registerService = registerService;
        _logger = logger;
    }

    /// <summary>
    /// 获取可选的体系认证机构列表（注册页下拉数据源）
    /// <para>返回 <c>cert_certification_body</c> 中 <c>IsValid=1</c> 且 <c>Status='active'</c> 的机构。</para>
    /// </summary>
    [HttpPost("GetOrgList")]
    [HttpGet("GetOrgList")]
    [AllowAnonymous]
    [ApiDescription("获取可注册的体系认证机构列表", "认证", "专家端/专家注册", true)]
    public async Task<IActionResult> GetOrgList()
    {
        try
        {
            var list = await _registerService.GetCertBodyOptionsAsync();
            return Ok(ApiResponse<List<CertBodyOptionDto>>.Ok(list, "获取成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取体系认证机构列表失败");
            return BadRequest(ApiResponse.Fail($"获取机构列表失败：{ex.Message}"));
        }
    }

    /// <summary>
    /// 专家注册（匿名）
    ///
    /// <para>一次注册 = 一个工作区，只能针对一个体系认证机构。注册成功即默认拥有
    /// <c>ROLE_AUDIT_CLIENT_ADMIN</c>（体系认证客户端管理员）的全部菜单与接口权限。</para>
    /// </summary>
    [HttpPost("Register")]
    [AllowAnonymous]
    [ApiDescription("专家注册", "认证", "专家端/专家注册", true)]
    public async Task<IActionResult> Register([FromBody] AuditorRegisterRequest request)
    {
        if (request == null)
            return BadRequest(ApiResponse.Fail("请求参数不能为空"));

        try
        {
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            var result = await _registerService.RegisterAsync(request, clientIp);

            if (!result.Success)
                return BadRequest(ApiResponse.Fail(result.Error ?? "注册失败"));

            return Ok(ApiResponse<AuditorRegisterResultDto>.Ok(result.Data!, "注册成功，请使用该账号登录"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "专家注册异常：UserName={UserName}", request.UserName);
            return BadRequest(ApiResponse.Fail($"注册失败：{ex.Message}"));
        }
    }
}

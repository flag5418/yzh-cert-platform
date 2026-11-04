using System.Collections.Generic;
using System.Threading.Tasks;
using CertPlatform.Auditor.Services.Expert;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using YZH.Core.Api.Controllers;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Result;

namespace CertPlatform.Auditor.Controllers;

/// <summary>
/// 入场检查内容控制器（认证准备「检查内容」）
/// <para><b>路由</b>：api/Auditor/CertOnsitePlan</para>
/// <para><b>数据表</b>：cert_onsite_plan_item</para>
/// </summary>
[ApiController]
[Route("api/Auditor/[controller]")]
public class CertOnsitePlanController : WebControllerBase
{
    private readonly CertOnsitePlanService _svc;
    private readonly CertCheckEngineService _engineSvc;
    private readonly IUserContext _userContext;
    private readonly ILogger<CertOnsitePlanController> _logger;

    public CertOnsitePlanController(
        CertOnsitePlanService svc,
        CertCheckEngineService engineSvc,
        IUserContext userContext,
        ILogger<CertOnsitePlanController> logger)
    {
        _svc = svc;
        _engineSvc = engineSvc;
        _userContext = userContext;
        _logger = logger;
    }

    private string UserCode => _userContext.UserCode ?? "system";

    /// <summary>按检查项查询检查内容</summary>
    [HttpPost("list")]
    public async Task<ApiResponse<List<CertOnsitePlanItemRow>>> List([FromBody] CertOnsitePlanListRequest req)
    {
        var result = await _svc.ListAsync(req.CheckItemCode);
        return result.Success
            ? ApiResponse<List<CertOnsitePlanItemRow>>.Ok(result.Data!, "查询成功")
            : ApiResponse<List<CertOnsitePlanItemRow>>.Fail(result.Error!);
    }

    /// <summary>新增 / 更新检查内容（Code 有值 = 更新）</summary>
    [HttpPost("save")]
    public async Task<ApiResponse<CertOnsitePlanItemRow>> Save([FromBody] CertOnsitePlanSaveRequest req)
    {
        var result = await _svc.SaveAsync(UserCode, req);
        return result.Success
            ? ApiResponse<CertOnsitePlanItemRow>.Ok(result.Data!, "保存成功")
            : ApiResponse<CertOnsitePlanItemRow>.Fail(result.Error!);
    }

    /// <summary>删除检查内容（软删）</summary>
    [HttpPost("delete")]
    public async Task<ApiResponse> Delete([FromBody] CertOnsitePlanCodeRequest req)
    {
        var result = await _svc.DeleteAsync(UserCode, req.Code);
        return result.Success ? ApiResponse.Ok("删除成功") : ApiResponse.Fail(result.Error!);
    }

    /// <summary>系统自动生成检查内容（只替换系统生成行，人工行保留）</summary>
    [HttpPost("generate")]
    public async Task<ApiResponse<List<CertOnsitePlanItemRow>>> Generate([FromBody] CertOnsitePlanListRequest req)
    {
        var generated = await _engineSvc.GenerateOnsitePlanAsync(UserCode, req.CheckItemCode);
        if (!generated.Success)
            return ApiResponse<List<CertOnsitePlanItemRow>>.Fail(generated.Error!);

        var list = await _svc.ListAsync(req.CheckItemCode);
        return list.Success
            ? ApiResponse<List<CertOnsitePlanItemRow>>.Ok(list.Data!, $"已生成 {generated.Data!.Count} 条检查内容")
            : ApiResponse<List<CertOnsitePlanItemRow>>.Fail(list.Error!);
    }
}

/// <summary>检查内容查询请求</summary>
public class CertOnsitePlanListRequest
{
    public string CheckItemCode { get; set; } = string.Empty;
}

/// <summary>检查内容 Code 请求</summary>
public class CertOnsitePlanCodeRequest
{
    public string Code { get; set; } = string.Empty;
}

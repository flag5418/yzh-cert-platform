using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Result;
using CertPlatform.Auditor.Services.Expert;

namespace CertPlatform.Auditor.Controllers;

/// <summary>
/// 证据管理控制器（V2 — 证据列表 + 新增 + 内容更新）
/// <para><b>路由</b>：api/Auditor/CertEvidence</para>
/// <para><b>数据表</b>：cert_evidence</para>
/// </summary>
[ApiController]
[Route("api/Auditor/[controller]")]
public class CertEvidenceController : ControllerBase
{
    private readonly CertEvidenceService _evidenceSvc;
    private readonly IUserContext _userContext;

    public CertEvidenceController(
        CertEvidenceService evidenceSvc,
        IUserContext userContext)
    {
        _evidenceSvc = evidenceSvc;
        _userContext = userContext;
    }

    private string UserCode => _userContext.UserCode ?? "system";

    // ============================================================
    // 一、列表
    // ============================================================

    /// <summary>POST /api/Auditor/CertEvidence/list — 分页查询证据列表</summary>
    [HttpPost("list")]
    public async Task<ApiResponse<CertEvidenceListResponse>> List(
        [FromBody] CertEvidenceListRequest req)
    {
        var result = await _evidenceSvc.GetListAsync(UserCode, req);
        return result.Success
            ? ApiResponse<CertEvidenceListResponse>.Ok(result.Data!, result.Data!.Message)
            : ApiResponse<CertEvidenceListResponse>.Fail(result.Error!);
    }

    // ============================================================
    // 二、新增
    // ============================================================

    /// <summary>POST /api/Auditor/CertEvidence/add — 新增证据</summary>
    [HttpPost("add")]
    public async Task<ApiResponse<CertEvidenceAddResponse>> Add(
        [FromBody] CertEvidenceAddRequest req)
    {
        var result = await _evidenceSvc.AddAsync(UserCode, req);
        return result.Success
            ? ApiResponse<CertEvidenceAddResponse>.Ok(result.Data!, result.Data!.Message)
            : ApiResponse<CertEvidenceAddResponse>.Fail(result.Error!);
    }

    // ============================================================
    // 三、详情
    // ============================================================

    /// <summary>GET /api/Auditor/CertEvidence/detail — 证据详情（供预览页使用，含全文）</summary>
    [HttpGet("detail")]
    public async Task<ApiResponse<CertEvidenceRow>> Detail([FromQuery] string code)
    {
        var result = await _evidenceSvc.GetDetailAsync(UserCode, code);
        return result.Success
            ? ApiResponse<CertEvidenceRow>.Ok(result.Data!, result.Data!.Code)
            : ApiResponse<CertEvidenceRow>.Fail(result.Error!);
    }

    // ============================================================
    // 四、更新
    // ============================================================

    /// <summary>POST /api/Auditor/CertEvidence/update — 更新证据内容（自动同步 MD5）</summary>
    [HttpPost("update")]
    public async Task<ApiResponse> Update(
        [FromBody] CertEvidenceUpdateRequest req)
    {
        var result = await _evidenceSvc.UpdateAsync(UserCode, req);
        return result.Success
            ? ApiResponse.Ok("更新成功")
            : ApiResponse.Fail(result.Error!);
    }
}

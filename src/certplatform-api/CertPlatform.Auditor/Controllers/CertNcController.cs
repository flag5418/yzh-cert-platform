using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Result;
using CertPlatform.Auditor.Services.Expert;

namespace CertPlatform.Auditor.Controllers;

/// <summary>
/// 不符合项（NC）控制器（V2 — 签发 + 状态流转 + AI 建议）
/// <para><b>路由</b>：api/Auditor/CertNc</para>
/// <para><b>数据表</b>：cert_nc</para>
/// </summary>
[ApiController]
[Route("api/Auditor/[controller]")]
public class CertNcController : ControllerBase
{
    private readonly CertNcService _ncSvc;
    private readonly IUserContext _userContext;

    public CertNcController(
        CertNcService ncSvc,
        IUserContext userContext)
    {
        _ncSvc = ncSvc;
        _userContext = userContext;
    }

    private string UserCode => _userContext.UserCode ?? "system";

    // ============================================================
    // 一、签发
    // ============================================================

    /// <summary>签发不符合项（审核员创建 NC，初始状态 pending_review）</summary>
    [HttpPost("issue")]
    public async Task<ApiResponse<CertNcResult>> Issue([FromBody] CertNcIssueRequest req)
    {
        var result = await _ncSvc.IssueAsync(UserCode, req);
        return result.Success
            ? ApiResponse<CertNcResult>.Ok(result.Data!, result.Data!.Message)
            : ApiResponse<CertNcResult>.Fail(result.Error!);
    }

    // ============================================================
    // 二、状态流转
    // ============================================================

    /// <summary>驱动 NC 状态流转（issue / rectifying / verifying / closed / escalated）</summary>
    [HttpPost("transition")]
    public async Task<ApiResponse<CertNcResult>> Transition([FromBody] CertNcTransitionRequest req)
    {
        var result = await _ncSvc.TransitionAsync(UserCode, req);
        return result.Success
            ? ApiResponse<CertNcResult>.Ok(result.Data!, result.Data!.Message)
            : ApiResponse<CertNcResult>.Fail(result.Error!);
    }

    // ============================================================
    // 三、AI 建议
    // ============================================================

    /// <summary>AI 生成 NC 描述建议（调 LLM nc_suggest prompt，不调时兜底）</summary>
    [HttpPost("ai-suggest")]
    public async Task<ApiResponse<string>> AiSuggest([FromBody] CertNcAiSuggestRequest req)
    {
        var result = await _ncSvc.SuggestNcDescriptionAsync(UserCode, req);
        return result.Success
            ? ApiResponse<string>.Ok(result.Data!, "AI 建议生成完成")
            : ApiResponse<string>.Fail(result.Error!);
    }
}

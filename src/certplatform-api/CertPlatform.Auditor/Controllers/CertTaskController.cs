using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Result;
using CertPlatform.Auditor.Services.Expert;

namespace CertPlatform.Auditor.Controllers;

/// <summary>
/// 认证任务控制器（V2 — 检查任务生命周期 + 证据变更触发的分析调度）
/// <para><b>路由</b>：api/Auditor/CertTask</para>
/// <para><b>数据表</b>：cert_task + cert_task_item</para>
/// <para>不继承 YzhControllerBase（创建/启动/暂停/证据影响分析走自定义端点）</para>
/// </summary>
[ApiController]
[Route("api/Auditor/[controller]")]
public class CertTaskController : ControllerBase
{
    private readonly CertTaskService _taskSvc;
    private readonly CertCheckEngineService _engineSvc;
    private readonly IUserContext _userContext;

    public CertTaskController(
        CertTaskService taskSvc,
        CertCheckEngineService engineSvc,
        IUserContext userContext)
    {
        _taskSvc = taskSvc;
        _engineSvc = engineSvc;
        _userContext = userContext;
    }

    private string UserCode => _userContext.UserCode ?? "system";

    // ========================================================
    // 一、任务 CRUD
    // ========================================================

    /// <summary>从计划创建任务（cert_task + cert_task_item 批量生成）</summary>
    [HttpPost("create")]
    public async Task<ApiResponse<CertTaskCreateResponse>> Create([FromBody] CertTaskCreateRequest req)
    {
        var result = await _taskSvc.CreateTaskAsync(UserCode, req);
        return result.Success
            ? ApiResponse<CertTaskCreateResponse>.Ok(result.Data!, result.Data!.Message)
            : ApiResponse<CertTaskCreateResponse>.Fail(result.Error!);
    }

    /// <summary>启动任务（pending → running）</summary>
    [HttpPost("start")]
    public async Task<ApiResponse<CertTaskStartResponse>> Start([FromBody] string taskCode)
    {
        var result = await _taskSvc.StartTaskAsync(UserCode, taskCode);
        return result.Success
            ? ApiResponse<CertTaskStartResponse>.Ok(result.Data!, result.Data!.Message)
            : ApiResponse<CertTaskStartResponse>.Fail(result.Error!);
    }

    /// <summary>暂停任务（running → paused）</summary>
    [HttpPost("pause")]
    public async Task<ApiResponse<CertTaskStartResponse>> Pause([FromBody] string taskCode)
    {
        var result = await _taskSvc.PauseTaskAsync(UserCode, taskCode);
        return result.Success
            ? ApiResponse<CertTaskStartResponse>.Ok(result.Data!, result.Data!.Message)
            : ApiResponse<CertTaskStartResponse>.Fail(result.Error!);
    }

    // ========================================================
    // 二、结论锁定
    // ========================================================

    /// <summary>锁定结论（审核员确认后防止重跑覆盖）</summary>
    [HttpPost("lock-result")]
    public async Task<ApiResponse> LockResult([FromBody] LockResultRequest req)
    {
        var result = await _taskSvc.LockConclusionAsync(
            UserCode, req.ResultCode, req.LockReason);
        return result.Success
            ? ApiResponse.Ok("锁定成功")
            : ApiResponse.Fail(result.Error!);
    }

    /// <summary>解锁结论（允许重新分析）</summary>
    [HttpPost("unlock-result")]
    public async Task<ApiResponse> UnlockResult([FromBody] string resultCode)
    {
        var result = await _taskSvc.UnlockConclusionAsync(UserCode, resultCode);
        return result.Success
            ? ApiResponse.Ok("解锁成功")
            : ApiResponse.Fail(result.Error!);
    }

    // ========================================================
    // 三、独立认证检查分析（检查引擎编排）
    // ========================================================

    /// <summary>
    /// 执行单检查项符合性分析（含锁定节流 + 证据指纹 + LLM 调用 + 结论落库）
    /// <para>锁定节流：LockMode=throttle 时跳过已锁定项；=override 时强制重跑</para>
    /// </summary>
    [HttpPost("analyze-item")]
    public async Task<ApiResponse<CertAnalysisResult>> AnalyzeItem(
        [FromBody] CertCheckAnalysisRequest req)
    {
        var result = await _engineSvc.AnalyzeCheckItemAsync(UserCode, req);
        return result.Success
            ? ApiResponse<CertAnalysisResult>.Ok(result.Data!, result.Data!.Message)
            : ApiResponse<CertAnalysisResult>.Fail(result.Error!);
    }

    // ========================================================
    // 四、证据变更触发的分析
    // ========================================================

    /// <summary>
    /// 证据变更通知：前端在证据上传/修改后调用。
    /// <para>流程：更新 ContentMD5 → 比对变化 → 影响分析 → 返回受影响的检查项。</para>
    /// </summary>
    [HttpPost("evidence-changed")]
    public async Task<ApiResponse<ImpactAnalysisResult>> EvidenceChanged(
        [FromBody] EvidenceChangeRequest req)
    {
        // 1. 更新 MD5
        await _taskSvc.DetectEvidenceChangeAsync(UserCode, req.EvidenceCode, req.NewContent);

        // 2. 影响分析
        var newMd5 = CertTaskService.ComputeMd5(req.NewContent);
        var change = new EvidenceChangeResult
        {
            EvidenceCode = req.EvidenceCode,
            CheckItemCode = req.CheckItemCode,
            OldMD5 = req.OldMD5,
            NewMD5 = newMd5,
            HasChanged = req.OldMD5 != newMd5
        };

        var result = await _taskSvc.AnalyzeImpactAsync(UserCode, req.EvidenceCode, change);
        return result.Success
            ? ApiResponse<ImpactAnalysisResult>.Ok(result.Data!, result.Data!.Message)
            : ApiResponse<ImpactAnalysisResult>.Fail(result.Error!);
    }

    // ========================================================
    // 五、分页 + 任务项列表
    // ========================================================

    /// <summary>POST /api/Auditor/CertTask/page — 分页查询任务列表</summary>
    [HttpPost("page")]
    public async Task<ApiResponse<CertTaskPageResponse>> Page([FromBody] CertTaskPageRequest req)
    {
        var result = await _taskSvc.GetPageAsync(UserCode, req);
        return result.Success
            ? ApiResponse<CertTaskPageResponse>.Ok(result.Data!, result.Data!.Message)
            : ApiResponse<CertTaskPageResponse>.Fail(result.Error!);
    }

    /// <summary>POST /api/Auditor/CertTask/items — 任务项列表（含检查结果）</summary>
    [HttpPost("items")]
    public async Task<ApiResponse<CertTaskItemsResponse>> Items([FromBody] string taskCode)
    {
        var result = await _taskSvc.GetItemsWithResultsAsync(UserCode, taskCode);
        return result.Success
            ? ApiResponse<CertTaskItemsResponse>.Ok(result.Data!, result.Data!.Message)
            : ApiResponse<CertTaskItemsResponse>.Fail(result.Error!);
    }

    // ========================================================
    // 六、计划统一视图聚合（V2 准备+检查一体化）
    // ========================================================

    /// <summary>
    /// POST /api/Auditor/CertTask/plan-checklist
    /// <para>按计划查询检查项聚合列表（含部门、联系人、证据计数、结论状态）。</para>
    /// <para>覆盖场景：认证准备（prep）+ 认证检查（onsite）共用同一张大表。</para>
    /// </summary>
    [HttpPost("plan-checklist")]
    public async Task<ApiResponse<CertPlanChecklistResponse>> PlanChecklist(
        [FromBody] string planCode)
    {
        var result = await _taskSvc.GetPlanChecklistAsync(UserCode, planCode);
        return result.Success
            ? ApiResponse<CertPlanChecklistResponse>.Ok(result.Data!, result.Data!.Message)
            : ApiResponse<CertPlanChecklistResponse>.Fail(result.Error!);
    }

    /// <summary>
    /// POST /api/Auditor/CertTask/plan-checkitem-detail
    /// <para>查询单个计划检查项的侧面详情（双层证据 + 部门联系人 + 锁定信息）。</para>
    /// </summary>
    [HttpPost("plan-checkitem-detail")]
    public async Task<ApiResponse<CertPlanCheckItemDetail>> PlanCheckItemDetail(
        [FromBody] string detailCode)
    {
        var result = await _taskSvc.GetPlanCheckItemDetailAsync(UserCode, detailCode);
        return result.Success
            ? ApiResponse<CertPlanCheckItemDetail>.Ok(result.Data!, result.Data!.Message)
            : ApiResponse<CertPlanCheckItemDetail>.Fail(result.Error!);
    }

    /// <summary>
    /// POST /api/Auditor/CertTask/export-report
    /// <para>导出计划报告（占位：返回报告元数据 + 预生成下链；实际异步生成由队列承担）。</para>
    /// </summary>
    [HttpPost("export-report")]
    public async Task<ApiResponse<CertExportReportResponse>> ExportReport(
        [FromBody] string planCode)
    {
        // TODO: 对接实际导出逻辑（Word/PDF 生成 → 上传 MinIO → 返回下载链接）
        return ApiResponse<CertExportReportResponse>.Ok(new CertExportReportResponse
        {
            ExportCode = Guid.NewGuid().ToString("N"),
            PlanCode = planCode,
            Status = "queued",
            Message = "导出已排队"
        });
    }
}

// ══════════════════════════════════════════════════════════════
// DTO 类
// ══════════════════════════════════════════════════════════════

/// <summary>锁定结论请求</summary>
public class LockResultRequest
{
    public string ResultCode { get; set; } = string.Empty;
    public string? LockReason { get; set; }
}

/// <summary>证据变更通知请求</summary>
public class EvidenceChangeRequest
{
    public string EvidenceCode { get; set; } = string.Empty;
    public string CheckItemCode { get; set; } = string.Empty;
    public string? OldMD5 { get; set; }
    public string NewContent { get; set; } = string.Empty;
}

/// <summary>导出报告响应</summary>
public class CertExportReportResponse
{
    public string ExportCode { get; set; } = string.Empty;
    public string PlanCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

// 注意：分页/任务项/结果等 DTO 统一定义在 CertPlatform.Auditor.Services.Expert 命名空间，
// 通过 using CertPlatform.Auditor.Services.Expert; 引入，此处不重复声明。

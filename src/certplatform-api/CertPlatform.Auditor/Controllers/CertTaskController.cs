using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using YZH.Core.DataBase.Interfaces;
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
    private readonly IDbOrm _db;
    private readonly ILogger<CertTaskController> _logger;

    public CertTaskController(
        CertTaskService taskSvc,
        CertCheckEngineService engineSvc,
        IUserContext userContext,
        IDbOrm db,
        ILogger<CertTaskController> logger)
    {
        _taskSvc = taskSvc;
        _engineSvc = engineSvc;
        _userContext = userContext;
        _db = db;
        _logger = logger;
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
    /// <remarks>页面锁（F12）：队列执行期间禁止操作，命中返回 409。</remarks>
    [HttpPost("lock-result")]
    public async Task<ApiResponse> LockResult([FromBody] LockResultRequest req)
    {
        var pageLock = await _taskSvc.AssertPageEditableByResultAsync(req.ResultCode);
        if (!pageLock.Success) return ApiResponse.Fail(pageLock.Error!, pageLock.Code ?? 409);

        var result = await _taskSvc.LockConclusionAsync(
            UserCode, req.ResultCode, req.LockReason);
        return result.Success
            ? ApiResponse.Ok("锁定成功")
            : ApiResponse.Fail(result.Error!);
    }

    /// <summary>解锁结论（允许重新分析）</summary>
    /// <remarks>页面锁（F12）：队列执行期间禁止操作，命中返回 409。</remarks>
    [HttpPost("unlock-result")]
    public async Task<ApiResponse> UnlockResult([FromBody] string resultCode)
    {
        var pageLock = await _taskSvc.AssertPageEditableByResultAsync(resultCode);
        if (!pageLock.Success) return ApiResponse.Fail(pageLock.Error!, pageLock.Code ?? 409);

        var result = await _taskSvc.UnlockConclusionAsync(UserCode, resultCode);
        return result.Success
            ? ApiResponse.Ok("解锁成功")
            : ApiResponse.Fail(result.Error!);
    }

    /// <summary>保存人工结论（结论 + 判定依据）；已锁定则拒绝</summary>
    /// <remarks>页面锁（F12）：队列执行期间禁止操作，命中返回 409。</remarks>
    [HttpPost("save-result")]
    public async Task<ApiResponse> SaveResult([FromBody] SaveResultRequest req)
    {
        var pageLock = await _taskSvc.AssertPageEditableByResultAsync(req.ResultCode);
        if (!pageLock.Success) return ApiResponse.Fail(pageLock.Error!, pageLock.Code ?? 409);

        var result = await _taskSvc.SaveResultAsync(
            UserCode, req.ResultCode, req.Conclusion, req.Reason, req.Severity);
        return result.Success
            ? ApiResponse.Ok("保存成功")
            : ApiResponse.Fail(result.Error!);
    }

    // ========================================================
    // 三、独立认证检查分析（检查引擎编排）
    // ========================================================

    /// <summary>
    /// 执行单检查项符合性分析（含锁定节流 + 证据指纹 + LLM 调用 + 结论落库）
    /// <para>锁定节流：LockMode=throttle 时跳过已锁定项；=override 时强制重跑</para>
    /// </summary>
    /// <remarks>
    /// <b>页面锁只在这一层判</b>（F12）——⛔ 不能下沉到 <see cref="CertCheckEngineService"/>：
    /// 队列执行器也调引擎，而队列跑的时候任务本来就是 <c>running</c>，
    /// 在引擎里判页面锁会让队列把自己锁死（每项都 409）。
    /// </remarks>
    [HttpPost("analyze-item")]
    public async Task<ApiResponse<CertAnalysisResult>> AnalyzeItem(
        [FromBody] CertCheckAnalysisRequest req)
    {
        var pageLock = await _taskSvc.AssertPageEditableByTaskItemAsync(req.TaskItemCode);
        if (!pageLock.Success)
            return ApiResponse<CertAnalysisResult>.Fail(pageLock.Error!, pageLock.Code ?? 409);

        var result = await _engineSvc.AnalyzeCheckItemAsync(UserCode, req);

        // ★ 手动单项重跑会改 task_item.Status（done / skipped），但引擎只写明细不刷汇总 ——
        //   不补这一下，cert_task.ItemDone 会与明细长期不一致（页面上进度条和列表对不上）。
        //   安全性：本端点已被上面的页面锁挡住，能走到这里说明该检查项没有队列在跑，
        //   汇总不会与队列的写入交错。
        if (result.Success)
        {
            var ownerTaskCode = await _taskSvc.FindTaskCodeByItemAsync(req.TaskItemCode);
            if (!string.IsNullOrWhiteSpace(ownerTaskCode))
                await CertTaskAggregator.RefreshAsync(_db, ownerTaskCode, _logger);
        }

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
        // 0. 页面锁（F12）：队列跑的时候证据也不能动 —— 否则队列读到的证据和写库时的不是同一份
        var pageLock = await _taskSvc.AssertPageEditableAsync(req.CheckItemCode);
        if (!pageLock.Success)
            return ApiResponse<ImpactAnalysisResult>.Fail(pageLock.Error!, pageLock.Code ?? 409);

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

    // ========================================================
    // 七、跨计划检查项清单（认证准备 / 认证检查 两个独立菜单）
    // ========================================================

    /// <summary>
    /// POST /api/Auditor/CertTask/stage-list
    /// <para>分支实例列表 —— 认证准备 / 认证检查两个菜单的<b>列表页</b>。
    /// 一行 = 一个「计划 × 分支」的执行实例（企业/计划/阶段/标准/检查项/进度/状态/开启时间）。</para>
    /// </summary>
    [HttpPost("stage-list")]
    public async Task<ApiResponse<CertStageListResponse>> StageList(
        [FromBody] CertStageListRequest req)
    {
        var result = await _taskSvc.GetStageListAsync(UserCode, req);
        return result.Success
            ? ApiResponse<CertStageListResponse>.Ok(result.Data!, result.Data!.Message)
            : ApiResponse<CertStageListResponse>.Fail(result.Error!);
    }

    /// <summary>
    /// POST /api/Auditor/CertTask/worklist
    /// <para>跨计划平铺检查项 —— 认证准备（prep）/ 认证检查（onsite）两个详情页的检查项表数据源。</para>
    /// <para>与 <c>plan-checklist</c> 的区别：那个是「一个计划看自己」，
    /// 这个是「一个分支看全部计划」，支撑两个独立菜单。</para>
    /// </summary>
    [HttpPost("worklist")]
    public async Task<ApiResponse<CertWorklistResponse>> Worklist(
        [FromBody] CertWorklistRequest req)
    {
        var result = await _taskSvc.GetWorklistAsync(UserCode, req);
        return result.Success
            ? ApiResponse<CertWorklistResponse>.Ok(result.Data!, result.Data!.Message)
            : ApiResponse<CertWorklistResponse>.Fail(result.Error!);
    }

    /// <summary>
    /// POST /api/Auditor/CertTask/set-owner
    /// <para>设置检查项负责审核员。</para>
    /// <remarks>
    /// ★ 业务口径（用户 2026-11-01）：<b>只有认证准备阶段可改</b>。
    /// 审核员分工是入场计划的关键信息，由审核组长在准备期定死；
    /// 认证检查阶段只读 —— 检查时改分工会让入场计划与执行对不上。
    /// </remarks>
    /// </summary>
    [HttpPost("set-owner")]
    public async Task<ApiResponse> SetOwner([FromBody] SetOwnerRequest req)
    {
        var result = await _taskSvc.SetCheckItemOwnerAsync(
            UserCode, req.DetailCode, req.OwnerCode, req.OwnerName);
        return result.Success
            ? ApiResponse.Ok("审核员已设置")
            : ApiResponse.Fail(result.Error!);
    }

    /// <summary>
    /// POST /api/Auditor/CertTask/member-options
    /// <para>专家组成员池 —— 「设置负责审核员」下拉的数据源。</para>
    /// <remarks>
    /// ★ 不复用 <c>/api/Auditor/AuditMember/filter</c>：那个走 <c>YzhControllerBase</c> 的
    /// 工作区隔离（要求登录账号挂靠机构），而检查项归属的是计划而非登录人。
    /// </remarks>
    /// </summary>
    [HttpPost("member-options")]
    public async Task<ApiResponse<List<CertMemberOption>>> MemberOptions(
        [FromBody] MemberOptionRequest? req)
    {
        var result = await _taskSvc.GetMemberOptionsAsync(req?.Keyword);
        return result.Success
            ? ApiResponse<List<CertMemberOption>>.Ok(result.Data!, $"{result.Data!.Count} 位审核员")
            : ApiResponse<List<CertMemberOption>>.Fail(result.Error!);
    }
}

/// <summary>专家组成员下拉请求</summary>
public class MemberOptionRequest
{
    /// <summary>关键词（姓名 / 职称 / 专业，空则返回全部）</summary>
    public string? Keyword { get; set; }
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

/// <summary>保存人工结论请求</summary>
public class SaveResultRequest
{
    public string ResultCode { get; set; } = string.Empty;
    /// <summary>审核员判定结论 pass / ofi / nc / na（必填）</summary>
    public string? Conclusion { get; set; }
    /// <summary>不符合程度 major / minor（Conclusion=nc 时必填）</summary>
    public string? Severity { get; set; }
    public string? Reason { get; set; }
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

/// <summary>设置检查项审核员请求</summary>
public class SetOwnerRequest
{
    /// <summary>计划明细 Code（= 检查项 Code）</summary>
    public string DetailCode { get; set; } = string.Empty;
    public string? OwnerCode { get; set; }
    public string? OwnerName { get; set; }
}

// 注意：分页/任务项/结果等 DTO 统一定义在 CertPlatform.Auditor.Services.Expert 命名空间，
// 通过 using CertPlatform.Auditor.Services.Expert; 引入，此处不重复声明。

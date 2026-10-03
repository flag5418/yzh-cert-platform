using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Controllers;
using YZH.Core.Api.Services;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Helpers;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models;
using YZH.Core.Stand.Models.Config;
using YZH.Core.Stand.Models.Request;
using YZH.Core.Stand.Models.Result;
using CertPlatform.Auditor.Services;
using CertPlatform.Auditor.Services.Expert;

namespace CertPlatform.Auditor.Controllers;

/// <summary>
/// 专家任务控制器（专家端 —— 任务系统「建 → 找 → 跑队列」）
///
/// <para><b>形态</b>：任务列表用<b>单表 CRUD 样板</b>（<c>SingleTableCore</c> + <c>YzhTable</c>，
/// 列/搜索/按钮由后端 EntityConfig 驱动）；向导与详情是<b>自定义端点</b>。</para>
///
/// <para><b>★ 继承 <see cref="YzhControllerBase{T}"/> 的理由</b>：任务列表需要
/// <c>/config</c>、<c>/filter</c> 两个标准端点，且 <c>CertExpertTask</c> 是标准业务实体。
/// 与 <c>EnterpriseStageController</c> 同构（后者因左树实体无 <c>ParentCode</c> 才手写端点）。</para>
///
/// <para><b>★ 工作区隔离</b>：本表<b>有</b> <c>OrgCode</c> 列（专家工作区 Code），
/// 故隔离直接走 <see cref="OnBuildingFilter"/> 的等值收敛 —— 比企业关联表更简单也更安全。</para>
///
/// <para><b>⛔ 职责边界（D31）</b>：本控制器<b>不含</b>认可 / 修改 / 批准 / 导出结论 ——
/// 那些在 <c>ExpertResultController</c>。任务系统只管执行。</para>
/// </summary>
[ApiController]
[Route("api/Auditor/[controller]")]
public class ExpertTaskController : YzhControllerBase<CertExpertTask>
{
    private readonly ExpertTaskService _svc;
    private readonly WorkspaceContextService _workspace;

    public ExpertTaskController(
        EntityService<CertExpertTask> entityService,
        ExpertTaskService svc,
        IUserContext userContext,
        WorkspaceContextService workspace)
        : base(entityService, userContext)
    {
        _svc = svc;
        _workspace = workspace;
    }

    /// <summary>缺 EntityConfig 时直接抛错（开发期暴露，避免「页面空白且零报错」）</summary>
    protected override bool StrictConfigLoad => true;

    protected override EntityConfig LoadConfig() => EntityConfigHelper.GetConfig<CertExpertTask>();

    // ========================================================
    // 一、工作区隔离
    // ========================================================

    /// <summary>把查询收敛到「本专家工作区」（<c>OrgCode</c> 等值）。解析失败不静默放行。</summary>
    protected override List<FilterItem> OnBuildingFilter(List<FilterItem> filters)
    {
        filters = base.OnBuildingFilter(filters);

        var ws = _workspace.Resolve(UserContext.UserCode);
        if (!ws.Success || ws.Data == null)
            throw new InvalidOperationException(ws.Error ?? "无法定位当前工作区");

        filters.RemoveAll(f => f.Field == "OrgCode");
        filters.Add(new FilterItem { Field = "OrgCode", Operator = "eq", Value = ws.Data.Code });

        return filters;
    }

    /// <summary>
    /// 查询后补齐展示字段。
    /// <para>任务列表要显示<b>中文状态</b>与<b>操作可用性</b>，而 DB 存的是英文枚举 ——
    /// 在服务端补齐，避免前端硬编码枚举（枚举一变前端就错）。</para>
    /// </summary>
    protected override void OnQueried(PagedResult<CertExpertTask> result)
    {
        if (result?.Items == null) return;
        foreach (var t in result.Items)
        {
            t.ExecStatusLabel = ExpertTaskService.ExecStatusLabel(t.ExecStatus);
            t.TaskTypeLabel = ExpertTaskService.TaskTypeLabel(t.TaskType);
            t.CanSubmit = t.ExecStatus is ExpertTaskConst.Exec.Draft or ExpertTaskConst.Exec.Failed
                          && t.LifecycleStatus == ExpertTaskConst.Lifecycle.Active;
            t.CanRetry = t.ExecStatus == ExpertTaskConst.Exec.Failed;
            t.CanViewResult = t.ExecStatus is ExpertTaskConst.Exec.Completed
                or ExpertTaskConst.Exec.Running or ExpertTaskConst.Exec.Failed;
            t.IsBlocking = ExpertTaskConst.IsBlocking(t.LifecycleStatus, t.ExecStatus);
        }
    }

    // ========================================================
    // 二、向导：业务锁判定 + 候选解析
    // ========================================================

    /// <summary>
    /// 业务锁实时判定（向导第 1 步）。
    /// <para>POST <c>api/Auditor/ExpertTask/lock-check</c>，body <c>{ EnterpriseCode, StageCode, TaskType }</c></para>
    /// <para>返回「可创建 = 绿条 / 被锁 = 红条 + 是哪条任务挡着」+ 生效检查项数 + 历史平均轮次。</para>
    /// </summary>
    [HttpPost("lock-check")]
    public async Task<ActionResult<ApiResponse<LockCheckResult>>> LockCheck([FromBody] LockCheckRequest req)
    {
        try
        {
            var ws = ResolveWorkspace();
            if (ws.Error != null) return Ok(ApiResponse<LockCheckResult>.Fail(ws.Error));

            var r = await _svc.CheckLockAsync(ws.Code!, req);
            return r.Success
                ? Ok(ApiResponse<LockCheckResult>.Ok(r.Data!))
                : Ok(ApiResponse<LockCheckResult>.Fail(r.Error!));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<LockCheckResult>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// 候选检查项（向导第 3 步勾选表）。
    /// <para>POST <c>api/Auditor/ExpertTask/candidates</c>，
    /// body <c>{ EnterpriseCode, StageCode, TaskType, StandardCodes }</c></para>
    /// </summary>
    [HttpPost("candidates")]
    public async Task<ActionResult<ApiResponse<List<TaskCandidateDto>>>> Candidates(
        [FromBody] TaskCreateRequest req)
    {
        try
        {
            var ws = ResolveWorkspace();
            if (ws.Error != null) return Ok(ApiResponse<List<TaskCandidateDto>>.Fail(ws.Error));

            var r = await _svc.GetCandidatesAsync(
                ws.Code!, req.EnterpriseCode, req.StageCode, req.TaskType, req.StandardCodes);
            return r.Success
                ? Ok(ApiResponse<List<TaskCandidateDto>>.Ok(r.Data!))
                : Ok(ApiResponse<List<TaskCandidateDto>>.Fail(r.Error!));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<List<TaskCandidateDto>>.Fail(ex.Message));
        }
    }

    // ========================================================
    // 三、创建任务（向导第 4 步）
    // ========================================================

    /// <summary>
    /// 创建任务。POST <c>api/Auditor/ExpertTask/create</c>
    /// <para>事务内落 6 张表；D36 业务锁应用层先查、DB 唯一键兜底。</para>
    /// </summary>
    [HttpPost("create")]
    public async Task<ActionResult<ApiResponse<TaskCreateResult>>> Create([FromBody] TaskCreateRequest req)
    {
        try
        {
            var ws = ResolveWorkspace();
            if (ws.Error != null) return Ok(ApiResponse<TaskCreateResult>.Fail(ws.Error));

            var r = await _svc.CreateTaskAsync(
                ws.Code!, UserContext.UserCode, DisplayName(), UserContext.ClientIp, req);
            return r.Success
                ? Ok(ApiResponse<TaskCreateResult>.Ok(r.Data!, "任务创建成功"))
                : Ok(ApiResponse<TaskCreateResult>.Fail(r.Error!));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<TaskCreateResult>.Fail(ex.Message));
        }
    }

    // ========================================================
    // 四、详情 / 日志 / 缺口
    // ========================================================

    /// <summary>任务详情（详情页 4 Tab 一次性载荷）。POST <c>api/Auditor/ExpertTask/detail</c></summary>
    [HttpPost("detail")]
    public async Task<ActionResult<ApiResponse<TaskDetailDto>>> Detail([FromBody] TaskCodeRequest req)
    {
        try
        {
            var ws = ResolveWorkspace();
            if (ws.Error != null) return Ok(ApiResponse<TaskDetailDto>.Fail(ws.Error));

            var r = await _svc.GetDetailAsync(ws.Code!, req.TaskCode);
            return r.Success
                ? Ok(ApiResponse<TaskDetailDto>.Ok(r.Data!))
                : Ok(ApiResponse<TaskDetailDto>.Fail(r.Error!));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<TaskDetailDto>.Fail(ex.Message));
        }
    }

    /// <summary>运行日志（详情 Tab 3）。POST <c>api/Auditor/ExpertTask/logs</c></summary>
    [HttpPost("logs")]
    public async Task<ActionResult<ApiResponse<List<TaskLogDto>>>> Logs([FromBody] TaskLogRequest req)
    {
        try
        {
            var ws = ResolveWorkspace();
            if (ws.Error != null) return Ok(ApiResponse<List<TaskLogDto>>.Fail(ws.Error));

            var r = await _svc.GetLogsAsync(ws.Code!, req.TaskCode, req.QueueCode, req.Take);
            return r.Success
                ? Ok(ApiResponse<List<TaskLogDto>>.Ok(r.Data!))
                : Ok(ApiResponse<List<TaskLogDto>>.Fail(r.Error!));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<List<TaskLogDto>>.Fail(ex.Message));
        }
    }

    /// <summary>数据缺口（详情 Tab 4）。POST <c>api/Auditor/ExpertTask/gaps</c></summary>
    [HttpPost("gaps")]
    public async Task<ActionResult<ApiResponse<List<TaskGapDto>>>> Gaps([FromBody] TaskCodeRequest req)
    {
        try
        {
            var ws = ResolveWorkspace();
            if (ws.Error != null) return Ok(ApiResponse<List<TaskGapDto>>.Fail(ws.Error));

            var r = await _svc.GetGapsAsync(ws.Code!, req.TaskCode);
            return r.Success
                ? Ok(ApiResponse<List<TaskGapDto>>.Ok(r.Data!))
                : Ok(ApiResponse<List<TaskGapDto>>.Fail(r.Error!));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<List<TaskGapDto>>.Fail(ex.Message));
        }
    }

    // ========================================================
    // 五、提交执行 / 队列启停 / 重试
    // ========================================================

    /// <summary>提交执行（生成队列 + 队列项）。POST <c>api/Auditor/ExpertTask/submit</c></summary>
    [HttpPost("submit")]
    public async Task<ActionResult<ApiResponse<object?>>> Submit([FromBody] TaskCodeRequest req)
    {
        try
        {
            var ws = ResolveWorkspace();
            if (ws.Error != null) return Ok(ApiResponse.Fail(ws.Error));

            var r = await _svc.SubmitAsync(
                ws.Code!, req.TaskCode, UserContext.UserCode, DisplayName(), UserContext.ClientIp);
            return r.Success
                ? Ok(ApiResponse<object?>.Ok(r.Data, "已生成队列，请在「执行队列」中启动"))
                : Ok(ApiResponse.Fail(r.Error!));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>启动队列。POST <c>api/Auditor/ExpertTask/queue/start</c></summary>
    [HttpPost("queue/start")]
    public async Task<ActionResult<ApiResponse<object?>>> QueueStart([FromBody] QueueCodeRequest req)
    {
        try
        {
            var ws = ResolveWorkspace();
            if (ws.Error != null) return Ok(ApiResponse.Fail(ws.Error));

            var r = await _svc.StartQueueAsync(
                ws.Code!, req.QueueCode, UserContext.UserCode, DisplayName(), UserContext.ClientIp);
            return r.Success ? Ok(ApiResponse<object?>.Ok(r.Data, "队列已启动"))
                             : Ok(ApiResponse.Fail(r.Error!));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>暂停队列。POST <c>api/Auditor/ExpertTask/queue/pause</c></summary>
    [HttpPost("queue/pause")]
    public async Task<ActionResult<ApiResponse<object?>>> QueuePause([FromBody] QueueCodeRequest req)
    {
        try
        {
            var ws = ResolveWorkspace();
            if (ws.Error != null) return Ok(ApiResponse.Fail(ws.Error));

            var r = await _svc.PauseQueueAsync(
                ws.Code!, req.QueueCode, UserContext.UserCode, DisplayName(), UserContext.ClientIp);
            return r.Success ? Ok(ApiResponse<object?>.Ok(r.Data, "队列已暂停"))
                             : Ok(ApiResponse.Fail(r.Error!));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>重试失败项。POST <c>api/Auditor/ExpertTask/retry-failed</c></summary>
    [HttpPost("retry-failed")]
    public async Task<ActionResult<ApiResponse<object?>>> RetryFailed([FromBody] TaskCodeRequest req)
    {
        try
        {
            var ws = ResolveWorkspace();
            if (ws.Error != null) return Ok(ApiResponse.Fail(ws.Error));

            var r = await _svc.RetryFailedAsync(
                ws.Code!, req.TaskCode, UserContext.UserCode, DisplayName(), UserContext.ClientIp);
            return r.Success ? Ok(ApiResponse<object?>.Ok(r.Data, "已重置失败项"))
                             : Ok(ApiResponse.Fail(r.Error!));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse.Fail(ex.Message));
        }
    }

    // ========================================================
    // 六、辅助
    // ========================================================

    /// <summary>解析工作区；失败时返回 (null, 错误文案)</summary>
    private (string? Code, string? Error) ResolveWorkspace()
    {
        var ws = _workspace.Resolve(UserContext.UserCode);
        if (!ws.Success || ws.Data == null)
            return (null, ws.Error ?? "无法定位当前工作区");
        return (ws.Data.Code, null);
    }

    /// <summary>操作人显示名（真实姓名优先，退回账号名）</summary>
    private string DisplayName() =>
        string.IsNullOrWhiteSpace(UserContext.UserTrueName)
            ? UserContext.UserName
            : UserContext.UserTrueName!;

    // ========================================================
    // 七、请求模型
    // ========================================================

    public class TaskCodeRequest
    {
        public string TaskCode { get; set; } = string.Empty;
    }

    public class QueueCodeRequest
    {
        public string QueueCode { get; set; } = string.Empty;
    }

    public class TaskLogRequest
    {
        public string TaskCode { get; set; } = string.Empty;
        public string? QueueCode { get; set; }
        public int Take { get; set; } = 200;
    }
}

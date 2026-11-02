using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CertPlatform.Auditor.Entities.Expert;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Queue;

namespace CertPlatform.Auditor.Services.Expert;

/// <summary>认证分析队列任务类型（与 <c>yzh_queue_task.TaskType</c> 对应）</summary>
public static class CertCheckItemTaskTypes
{
    /// <summary>认证准备分支</summary>
    public const string Prep = "cert_check_prep";

    /// <summary>认证检查分支</summary>
    public const string Onsite = "cert_check_onsite";
}

/// <summary>
/// 队列项载荷（<c>yzh_queue_task.Payload</c> 的结构）。
/// <para>⚠️ 字段名进 JSON 后是执行器唯一能拿到的东西 —— 改字段 = 改契约，
/// 已在跑的队列会解析成空对象（不报错，静默跑错项）。</para>
/// </summary>
public class CertCheckItemPayload
{
    /// <summary>所属任务（cert_task.Code）—— 汇总回写要用</summary>
    public string TaskCode { get; set; } = string.Empty;

    /// <summary>任务项（cert_task_item.Code）—— 引擎按它取数</summary>
    public string TaskItemCode { get; set; } = string.Empty;

    /// <summary>检查项（cert_audit_plan_detail.Code）—— 冗余，便于排查</summary>
    public string CheckItemCode { get; set; } = string.Empty;

    /// <summary>分支：prep / onsite</summary>
    public string StageKind { get; set; } = string.Empty;

    /// <summary>计划 / 企业（冗余，便于排查）</summary>
    public string? PlanCode { get; set; }
    public string? EnterpriseCode { get; set; }

    /// <summary>发起人 Code（写 cert_check_analysis.CreateBy / 各 UpdateBy）</summary>
    public string? UserCode { get; set; }

    private static readonly JsonSerializerOptions Opts = new() { PropertyNameCaseInsensitive = true };

    public static CertCheckItemPayload Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new CertCheckItemPayload();
        try
        {
            return JsonSerializer.Deserialize<CertCheckItemPayload>(json, Opts)
                   ?? new CertCheckItemPayload();
        }
        catch
        {
            return new CertCheckItemPayload();
        }
    }
}

/// <summary>
/// 认证分析队列项执行器（基类）。
///
/// <para><b>为什么两个 TaskType 要两个子类</b>：<c>IYzhTaskExecutor.TaskType</c> 是单值，
/// 而 <c>QueueManager</c> 用 <c>GroupBy(TaskType).First()</c> 建分派字典 ——
/// 一个类只能认领一个类型。准备/检查共用同一套执行逻辑，只差 <see cref="TaskType"/>。</para>
///
/// <para><b>三道锁闸门在这里是第②道</b>（§10.2）：执行前由引擎再查一次锁，
/// 命中就 <c>IsSkipped=true</c> 返回 —— 队列项置 <c>skipped</c>、不计失败、不重试。</para>
/// </summary>
public abstract class CertCheckItemExecutorBase : IYzhTaskExecutor
{
    public abstract string TaskType { get; }

    private readonly ILogger _logger;
    private readonly IServiceProvider _root;

    protected CertCheckItemExecutorBase(ILogger logger, IServiceProvider root)
    {
        _logger = logger;
        _root = root;
    }

    /// <summary>框架统一队列路径：解析 payload → 调引擎 → 汇总回写 → 映射跳过语义</summary>
    public async Task<TaskExecutionResult> ExecuteAsync(YzhQueueTask task, CancellationToken cancellationToken)
    {
        var p = CertCheckItemPayload.Parse(task.Payload);

        if (string.IsNullOrWhiteSpace(p.TaskItemCode))
        {
            // 载荷坏了：永久错误，重试也没用
            return new TaskExecutionResult
            {
                Success = false,
                Retryable = false,
                Message = $"队列载荷缺少 TaskItemCode（task={task.Code}）"
            };
        }

        using var scope = _root.CreateScope();
        var engine = scope.ServiceProvider.GetRequiredService<CertCheckEngineService>();
        var db = scope.ServiceProvider.GetRequiredService<IDbOrm>();

        try
        {
            var res = await engine.AnalyzeCheckItemAsync(
                string.IsNullOrWhiteSpace(p.UserCode) ? "system" : p.UserCode!,
                new CertCheckAnalysisRequest
                {
                    TaskItemCode = p.TaskItemCode,
                    StageKind = string.IsNullOrWhiteSpace(p.StageKind) ? null : p.StageKind
                });

            // ★ 无论成功失败都汇总一次：最后一项跑完那一刻，任务必须能翻成 done
            await CertTaskAggregator.RefreshAsync(db, p.TaskCode, _logger);

            if (!res.Success)
            {
                // 抛出去让 QueueManager 做错误分类 + 退避重试（它按消息关键词判永久/可重试）
                throw new InvalidOperationException(res.Error ?? "认证分析失败");
            }

            var data = res.Data;

            // 闸门②命中：跳过 ≠ 失败（队列项置 skipped，不计 FailedCount，不重试）
            if (data?.LockedSkipped == true)
            {
                return new TaskExecutionResult
                {
                    Success = true,
                    IsSkipped = true,
                    Message = data.Message
                };
            }

            return new TaskExecutionResult
            {
                Success = true,
                Message = data?.Message ?? "分析完成"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[CertCheck] 队列项执行失败 task={Task} item={Item}",
                p.TaskCode, p.TaskItemCode);

            // 失败也要汇总（否则 pending 计数永远不清零，任务卡在 running ⇒ 页面永久锁死）
            await CertTaskAggregator.RefreshAsync(db, p.TaskCode, _logger);
            throw;
        }
    }

    /// <summary>
    /// 终态钩子：QueueManager 只在 <c>failed</c> / <c>cancelled</c> 时调它。
    /// <para>作用：把「这一项」标成终态，否则汇总里它永远是 pending。</para>
    /// </summary>
    public async Task OnTaskStateChangedAsync(YzhQueueTask task, string newStatus, string message)
    {
        if (newStatus != "failed" && newStatus != "cancelled") return;

        var p = CertCheckItemPayload.Parse(task.Payload);
        if (string.IsNullOrWhiteSpace(p.TaskItemCode)) return;

        try
        {
            using var scope = _root.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IDbOrm>();

            var item = (await db.GetOneAsync<CertTaskItem>(i =>
                i.Code == p.TaskItemCode && !i.IsDeleted)).Data;

            if (item != null)
            {
                // 取消（人工暂停）≠ 失败：标 skipped，别在界面上红一片
                item.Status = newStatus == "cancelled" ? "skipped" : "failed";
                item.Message = string.IsNullOrWhiteSpace(message)
                    ? (newStatus == "cancelled" ? "任务已取消" : "执行失败")
                    : (message.Length > 500 ? message[..500] : message);
                item.UpdateTime = DateTime.Now;
                await db.UpdateAsync(item);
            }

            await CertTaskAggregator.RefreshAsync(db, p.TaskCode, _logger);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CertCheck] 终态钩子回写失败 task={Task}", p.TaskCode);
        }
    }
}

/// <summary>认证准备分支执行器（<c>cert_check_prep</c>）</summary>
public class CertCheckPrepExecutor : CertCheckItemExecutorBase
{
    public override string TaskType => CertCheckItemTaskTypes.Prep;

    public CertCheckPrepExecutor(ILogger<CertCheckPrepExecutor> logger, IServiceProvider root)
        : base(logger, root) { }
}

/// <summary>认证检查分支执行器（<c>cert_check_onsite</c>）</summary>
public class CertCheckOnsiteExecutor : CertCheckItemExecutorBase
{
    public override string TaskType => CertCheckItemTaskTypes.Onsite;

    public CertCheckOnsiteExecutor(ILogger<CertCheckOnsiteExecutor> logger, IServiceProvider root)
        : base(logger, root) { }
}

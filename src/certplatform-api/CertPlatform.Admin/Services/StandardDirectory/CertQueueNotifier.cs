using System.Text.Json;
using CertPlatform.Shared.Services.Sys;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Queue;
using YZH.Core.Stand.Models.Push;

namespace CertPlatform.Admin.Services.StandardDirectory;

/// <summary>
/// 队列终态通知器（实现 IYzhQueueNotifier）
///
/// <para>① 落库 cert_message（CertMessageService，Scoped，经 IServiceProvider 建 scope）；
/// ② SignalR 实时推送（IYzhMessagePusher，框架能力）。</para>
///
/// <para>★ 注意：QueueManager 是单例，本类也必须是单例（注册见
/// CertPlatformAdminServiceExtensions）—— Scoped 依赖一律经 scope 自取。</para>
///
/// <para>★ 失败不外抛：通知是旁路，落库/推送各自 try/catch，绝不影响队列状态机。</para>
/// </summary>
public class CertQueueNotifier : IYzhQueueNotifier
{
    private readonly ILogger<CertQueueNotifier> _logger;
    private readonly IYzhMessagePusher _pusher;
    private readonly IServiceProvider _serviceProvider;

    private static readonly Dictionary<string, string> QueueTypeNames = new()
    {
        ["file_convert"] = "文档转换",
        ["auto_verify"] = "自动核验",
        ["report_generate"] = "报告生成",
        // ★ 2026-10-06 企业资料规范化（专家端 P1 编排器，TaskType 见
        //   CertPlatform.Auditor/Services/Ent/Normalize/EnterpriseNormalizeExecutor.TaskTypeName）
        //   ⚠️ 漏登记 = 队列页面显示成原始 task_type 字符串（功能不受影响，但可读性差）
        ["enterprise_normalize"] = "企业资料规范化"
    };

    public CertQueueNotifier(
        ILogger<CertQueueNotifier> logger,
        IYzhMessagePusher pusher,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _pusher = pusher;
        _serviceProvider = serviceProvider;
    }

    public async Task NotifyAsync(YzhQueue queue)
    {
        var typeName = QueueTypeNames.GetValueOrDefault(queue.QueueType, queue.QueueType);
        var statusText = queue.Status switch
        {
            "completed" => "已完成",
            "failed" => "失败",
            "cancelled" => "已取消",
            _ => queue.Status
        };
        var target = string.IsNullOrWhiteSpace(queue.QueueName) ? queue.QueueCode : queue.QueueName!;
        var title = $"{typeName} · {statusText}";
        var content = $"「{target}」{statusText}：完成 {queue.CompletedCount}/{queue.TotalCount}，失败 {queue.FailedCount}。";
        var data = new
        {
            queueCode = queue.QueueCode,
            queueType = queue.QueueType,
            status = queue.Status,
            totalCount = queue.TotalCount,
            completedCount = queue.CompletedCount,
            failedCount = queue.FailedCount,
            cancelledCount = queue.CancelledCount,
            scopeKey = queue.ScopeKey,
        };

        // ① 落库 cert_message（收件人 = 队列创建者登录名，CertMessageService 内换 UserCode）
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var messageService = scope.ServiceProvider.GetRequiredService<CertMessageService>();
            await messageService.CreateForLoginAsync(
                queue.CreateBy, title, content, "queue", JsonSerializer.Serialize(data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "队列终态消息落库失败: {QueueCode}", queue.QueueCode);
        }

        // ② SignalR 实时推送（分组身份由 Hub 从已验证 JWT 派生，与 queue.CreateBy 同源=登录名）
        try
        {
            await _pusher.SendToUserAsync(queue.CreateBy, new YzhPushMessage
            {
                Title = title,
                Message = content,
                Value = YzhPushValues.QueueProgress,
                Data = data,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "队列终态推送失败: {QueueCode}", queue.QueueCode);
        }

        _logger.LogInformation(
            "队列终态通知: {QueueCode} ({TypeName}) → {Status}, " +
            "完成={Completed}/{Total}, 失败={Failed}",
            queue.QueueCode, typeName, queue.Status,
            queue.CompletedCount, queue.TotalCount, queue.FailedCount);
    }
}

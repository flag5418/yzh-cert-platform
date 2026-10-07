using Microsoft.Extensions.Logging;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Queue;

namespace CertPlatform.Admin.Services.StandardDirectory;

/// <summary>
/// 队列终态通知器
/// 实现 IYzhQueueNotifier：队列进入终态时记录日志
/// 注意：QueueManager 是单例，因此本类也必须是单例
/// 扩展：可接入消息服务（cert_message）+ SignalR 推送
/// </summary>
public class CertQueueNotifier : IYzhQueueNotifier
{
    private readonly ILogger<CertQueueNotifier> _logger;

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

    public CertQueueNotifier(ILogger<CertQueueNotifier> logger)
    {
        _logger = logger;
    }

    public Task NotifyAsync(YzhQueue queue)
    {
        var typeName = QueueTypeNames.GetValueOrDefault(queue.QueueType, queue.QueueType);
        _logger.LogInformation(
            "队列终态通知: {QueueCode} ({TypeName}) → {Status}, " +
            "完成={Completed}/{Total}, 失败={Failed}",
            queue.QueueCode, typeName, queue.Status,
            queue.CompletedCount, queue.TotalCount, queue.FailedCount);

        // TODO: 接入消息服务
        // TODO: 接入 SignalR 推送

        return Task.CompletedTask;
    }
}

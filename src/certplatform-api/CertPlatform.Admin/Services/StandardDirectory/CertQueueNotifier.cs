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
/// <para>★ <b>收件人 = <c>queue.CreateBy</c> = 用户 Code</b>（2026-10-11 统一）：落库（<c>CreateForUserAsync</c>）
/// 与推送（<c>SendToUserAsync</c>）两条链同键。⚠️ 该列为空代表「系统发起的队列」⇒ 两条链都会静默跳过
/// （这是有意为之：没有创建人就没有收件人）；若发现某类队列长期收不到通知，应回到入队处补 <c>UserCode</c>。</para>
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
        ["doc_extract"] = "企业资料提取",
        ["auto_verify"] = "自动核验",
        ["report_generate"] = "报告生成",
        // ★ 2026-10-06 企业资料规范化（专家端 P1 编排器，TaskType 见
        //   CertPlatform.Auditor/Services/Ent/Normalize/EnterpriseNormalizeExecutor.TaskTypeName）
        //   ⚠️ 漏登记 = 队列页面显示成原始 task_type 字符串（功能不受影响，但可读性差）
        ["enterprise_normalize"] = "企业资料规范化",
        // ★ 2026-10-14 企业原始资料（一文件一队列；漏登记会以原始类型名进标题/铃铛）
        ["enterprise_original_file"] = "企业原始资料处理",
        ["enterprise_original_ingest"] = "企业原始资料入库",
        ["enterprise_original_analyze"] = "企业原始资料识别",
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

        // ① 落库 cert_message（★ 收件人 = 队列创建者 Code —— 与 CreateBy 契约、推送分组键同键，零映射）
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var messageService = scope.ServiceProvider.GetRequiredService<CertMessageService>();
            await messageService.CreateForUserAsync(
                queue.CreateBy, title, content, "queue", JsonSerializer.Serialize(data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "队列终态消息落库失败: {QueueCode}", queue.QueueCode);
        }

        // ② SignalR 实时推送（分组身份由 Hub 从已验证 JWT 的 code claim 派生，与 queue.CreateBy 同源=用户 Code）
        //    ⚠️ queue.CreateBy 为空（系统发起的队列）⇒ SendToUserAsync 静默跳过，属预期
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

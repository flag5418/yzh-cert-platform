using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VOL.Builder.IServices.CertPlatform;
using YZH.Core.Queue;

namespace VOL.Builder.Services.CertPlatform
{
    /// <summary>
    /// 队列终态通知实现（IYzhQueueNotifier 的认证平台实现）
    /// <para>1. 消息落库 cert_message（完整信息，供消息中心/下次进入汇总）</para>
    /// <para>2. SignalR 推送给队列创建者（value=queue_progress，前端任意页面弹窗）</para>
    /// </summary>
    public class CertQueueNotifier : IYzhQueueNotifier
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CertQueueNotifier> _logger;

        public CertQueueNotifier(IServiceProvider serviceProvider, ILogger<CertQueueNotifier> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task NotifyAsync(YzhQueue queue)
        {
            try
            {
                var typeText = queue.QueueType switch
                {
                    "file_convert" => "文档转换",
                    "auto_verify" => "自动核验",
                    "report_generate" => "报告生成",
                    _ => queue.QueueType
                };
                var statusText = queue.Status switch
                {
                    "completed" => "完成",
                    "failed" => "失败",
                    "cancelled" => "已取消",
                    _ => queue.Status
                };
                var title = queue.Status switch
                {
                    "completed" => $"{typeText}完成",
                    "failed" => $"{typeText}失败",
                    "cancelled" => "队列已取消",
                    _ => "队列通知"
                };
                var content = $"队列 {queue.QueueCode}（{typeText}）{statusText}：共{queue.TotalCount}个任务，成功{queue.CompletedCount}，失败{queue.FailedCount}"
                    + (queue.StartTime != null ? $"；开始 {queue.StartTime:HH:mm:ss}" : "")
                    + (queue.EndTime != null ? $"，结束 {queue.EndTime:HH:mm:ss}" : "");

                var extra = new
                {
                    queueCode = queue.QueueCode,
                    queueType = queue.QueueType,
                    scopeKey = queue.ScopeKey,
                    status = queue.Status,
                    total = queue.TotalCount,
                    completed = queue.CompletedCount,
                    failed = queue.FailedCount,
                    cancelled = queue.CancelledCount,
                    startTime = queue.StartTime?.ToString("yyyy-MM-dd HH:mm:ss"),
                    endTime = queue.EndTime?.ToString("yyyy-MM-dd HH:mm:ss")
                };
                var payload = new
                {
                    title,
                    message = content,
                    date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    value = "queue_progress",
                    data = extra
                };

                // 消息落库（供消息中心 + 下次进入未读汇总）
                if (queue.CreateID != null)
                {
                    using var mScope = _serviceProvider.CreateScope();
                    var messageService = mScope.ServiceProvider.GetRequiredService<IMessageService>();
                    await messageService.CreateAsync(queue.CreateID.Value, queue.Creator, title, content, "queue",
                        JsonSerializer.Serialize(extra));
                }

                // SignalR 推送给创建者
                if (!string.IsNullOrEmpty(queue.Creator))
                {
                    using var nScope = _serviceProvider.CreateScope();
                    var notifier = nScope.ServiceProvider.GetRequiredService<IConvertNotifier>();
                    await notifier.SendToUser(queue.Creator, payload);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[CertQueueNotifier] 终态通知发送失败: {queue.QueueCode}");
            }
        }
    }
}

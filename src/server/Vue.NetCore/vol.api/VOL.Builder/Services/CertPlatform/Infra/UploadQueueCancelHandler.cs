using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using VOL.Builder.IServices.CertPlatform;
using YZH.Core.Queue;

namespace VOL.Builder.Services.CertPlatform
{
    /// <summary>
    /// 上传任务转换队列取消后的业务清理钩子
    /// <para>语义：取消 = 彻底清理本次上传过程（数据库记录 + MinIO 对象），不允许重试</para>
    /// <para>触发条件：file_convert 队列且 SourceType=upload_task（由批量上传确认时创建）</para>
    /// </summary>
    public class UploadQueueCancelHandler : IYzhQueueCancelHandler
    {
        private readonly IServiceProvider _serviceProvider;

        public UploadQueueCancelHandler(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task OnQueueCancelledAsync(YzhQueue queue)
        {
            // 仅处理上传任务产生的文件转换队列
            if (queue == null || queue.QueueType != "file_convert") return;
            if (queue.SourceType != "upload_task" || string.IsNullOrEmpty(queue.SourceId)) return;

            using var scope = _serviceProvider.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IStandardDirectoryService>();
            var result = await service.UploadCancel(queue.SourceId);
            Console.WriteLine($"[UploadQueueCancelHandler] 队列 {queue.QueueCode} 已取消，上传任务 {queue.SourceId} 清理结果: {(result.Status ? (result.Message ?? "ok") : (result.Message ?? "failed"))}");
        }
    }
}

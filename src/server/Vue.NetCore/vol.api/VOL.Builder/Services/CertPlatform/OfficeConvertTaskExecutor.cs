using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VOL.Core.EFDbContext;
using VOL.Entity.CertPlatform.Dir;
using YZH.Core.Queue;

namespace VOL.Builder.Services.CertPlatform
{
    /// <summary>
    /// 文件转换任务执行器（IYzhTaskExecutor 的 file_convert 实现）
    /// <para>解析 yzh_queue_task.payload → 调用 OfficeConvertService 执行转换 → 联动文件记录状态</para>
    /// </summary>
    public class OfficeConvertTaskExecutor : IYzhTaskExecutor
    {
        public string TaskType => "file_convert";

        private readonly IServiceProvider _serviceProvider;

        public OfficeConvertTaskExecutor(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task<YzhTaskExecutionResult> ExecuteAsync(YzhQueueTask task, CancellationToken cancellationToken)
        {
            var payload = ParsePayload(task.Payload);
            if (payload == null || string.IsNullOrEmpty(payload.FileCode))
                return new YzhTaskExecutionResult { Success = false, Message = "任务数据无效（payload 解析失败）", Retryable = false };

            using var scope = _serviceProvider.CreateScope();
            var convertService = scope.ServiceProvider.GetRequiredService<OfficeConvertService>();

            try
            {
                var result = await convertService.ConvertAsync(payload, cancellationToken);
                return new YzhTaskExecutionResult { Success = result, Message = result ? null : "转换失败" };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException?.Message ?? ex.Message;
                return new YzhTaskExecutionResult
                {
                    Success = false,
                    Message = msg,
                    // 文件/格式类问题为永久错误；LibreOffice 不可用、超时等可重试
                    Retryable = !msg.Contains("不存在") && !msg.Contains("不是合法") && !msg.Contains("不支持")
                                && !msg.Contains("损坏") && !msg.Contains("libreoffice 不可用")
                                && !msg.Contains("ObjectNotFound")
                };
            }
        }

        /// <summary>
        /// 任务状态变更联动：退避重试/最终失败/取消时同步文件记录可见性
        /// </summary>
        public async Task OnTaskStateChangedAsync(YzhQueueTask task, string newStatus, string message)
        {
            var payload = ParsePayload(task.Payload);
            if (payload == null || string.IsNullOrEmpty(payload.FileCode)) return;

            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VOLContext>();
            var file = await db.Set<StandardDirectoryFile>().AsTracking()
                .FirstOrDefaultAsync(f => f.FileCode == payload.FileCode);
            if (file == null) return;

            switch (newStatus)
            {
                case "pending":
                    // 退避重试/重新排队：文件保持隐藏，状态回 pending
                    file.ConvertStatus = "pending";
                    file.ConvertMessage = null;
                    file.IsValid = false;
                    break;
                case "failed":
                    // 最终失败：恢复可见（可下载原文件/重试），标记失败原因
                    file.ConvertStatus = "failed";
                    file.ConvertMessage = message;
                    file.ConvertDate = DateTime.Now;
                    file.IsValid = true;
                    break;
                case "cancelled":
                    // 取消：恢复可见，保留原始文件
                    file.ConvertStatus = "pending";
                    file.ConvertMessage = message;
                    file.IsValid = true;
                    break;
            }
            await db.SaveChangesAsync();
        }

        private static readonly JsonSerializerOptions _payloadOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private static FileConvertPayload ParsePayload(string payload)
        {
            if (string.IsNullOrEmpty(payload)) return null;
            try { return JsonSerializer.Deserialize<FileConvertPayload>(payload, _payloadOptions); }
            catch { return null; }
        }
    }
}

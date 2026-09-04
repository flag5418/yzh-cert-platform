/*
 * 队列中心 Controller（通用队列：文件转换/自动核验/报告生成）
 */
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using VOL.Core.Controllers.Basic;
using VOL.Core.Filters;
using VOL.Core.Utilities;
using YZH.Core.Queue;

namespace VOL.WebApi.Controllers.CertPlatform
{
    [Route("api/queue")]
    [ApiController]
    [JWTAuthorize]
    public class QueueController : ApiBaseController<object>
    {
        private readonly YzhQueueManager _queueManager;

        public QueueController(YzhQueueManager queueManager)
        {
            _queueManager = queueManager;
        }

        public class QueueListRequest
        {
            public string Type { get; set; }
            public string Status { get; set; }
            public DateTime? StartTime { get; set; }
            public DateTime? EndTime { get; set; }
            public int Page { get; set; } = 1;
            public int Rows { get; set; } = 20;
        }

        public class QueueResourceLockRequest
        {
            public string Table { get; set; }
            public string Code { get; set; }
        }

        /// <summary>
        /// 队列主表分页（Tabs + 时间过滤）
        /// </summary>
        [HttpPost("list")]
        public async Task<IActionResult> GetList([FromBody] QueueListRequest req)
        {
            var result = await _queueManager.GetQueueListAsync(
                req?.Type, req?.Status, req?.StartTime, req?.EndTime,
                req?.Page ?? 1, req?.Rows ?? 20);
            return JsonNormal(new WebResponseContent().OK(null, result));
        }

        /// <summary>
        /// 队列详情（主表 + 子任务明细 + 资源锁列表）
        /// </summary>
        [HttpPost("detail")]
        public async Task<IActionResult> GetDetail([FromQuery] string queueCode)
        {
            var result = await _queueManager.GetQueueDetailAsync(queueCode);
            if (result == null)
                return JsonNormal(new WebResponseContent().Error("队列不存在"));
            return JsonNormal(new WebResponseContent().OK(null, result));
        }

        /// <summary>
        /// 取消队列
        /// </summary>
        [HttpPost("cancel")]
        public async Task<IActionResult> CancelQueue([FromQuery] string queueCode)
        {
            var (ok, error) = await _queueManager.CancelQueueAsync(queueCode);
            return ok
                ? JsonNormal(new WebResponseContent().OK("队列已取消"))
                : JsonNormal(new WebResponseContent().Error(error));
        }

        /// <summary>
        /// 整队重跑
        /// </summary>
        [HttpPost("retry")]
        public async Task<IActionResult> RetryQueue([FromQuery] string queueCode)
        {
            var (ok, error) = await _queueManager.RetryQueueAsync(queueCode);
            return ok
                ? JsonNormal(new WebResponseContent().OK("队列已重新排队"))
                : JsonNormal(new WebResponseContent().Error(error));
        }

        /// <summary>
        /// 单个子任务重试
        /// </summary>
        [HttpPost("task/retry")]
        public async Task<IActionResult> RetryTask([FromQuery] long taskId)
        {
            var (ok, error) = await _queueManager.RetryTaskAsync(taskId);
            return ok
                ? JsonNormal(new WebResponseContent().OK("任务已重新排队"))
                : JsonNormal(new WebResponseContent().Error(error));
        }

        // TODO: RetryFailedConversions 端点暂时移除，待企业文件服务重建后恢复
        // [HttpPost("file-convert/retry-failed")]
        // public async Task<IActionResult> RetryFailedConversions()
        // {
        //     var result = await _standardDirectoryService.RetryFailedConversionsAsync();
        //     return JsonNormal(result);
        // }

        /// <summary>
        /// 队列监控统计卡
        /// </summary>
        [HttpPost("status")]
        public async Task<IActionResult> GetQueueStatus()
        {
            var status = await _queueManager.GetQueueStatsAsync();
            return JsonNormal(new WebResponseContent().OK(null, status));
        }

        /// <summary>
        /// 通用资源锁查询（页面操作前检查：body: { table, code }）
        /// </summary>
        [HttpPost("resource/locked")]
        public async Task<IActionResult> GetResourceLock([FromBody] QueueResourceLockRequest req)
        {
            var hit = await _queueManager.FindResourceLockAsync(req?.Table, new System.Collections.Generic.List<string> { req?.Code });
            return JsonNormal(new WebResponseContent().OK(null, hit));
        }
    }
}

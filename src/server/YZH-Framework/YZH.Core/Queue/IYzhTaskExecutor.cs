using System.Threading;
using System.Threading.Tasks;

namespace YZH.Core.Queue
{
    /// <summary>
    /// 任务执行结果
    /// </summary>
    public class YzhTaskExecutionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }

        /// <summary>是否可重试：false=永久错误（重试无意义，直接置 failed 不重试）</summary>
        public bool Retryable { get; set; } = true;
    }

    /// <summary>
    /// 任务执行器抽象（按任务类型注册实现）
    /// <para>业务侧实现：文件转换 / 自动核验 / 报告生成等，各自解析 payload 执行</para>
    /// </summary>
    public interface IYzhTaskExecutor
    {
        /// <summary>本执行器支持的任务类型（与 yzh_queue_task.task_type 对应）</summary>
        string TaskType { get; }

        /// <summary>
        /// 执行任务。抛出异常 = 失败，由队列管理器统一做错误分类与退避重试。
        /// 成功时返回 Success=true（Manager 将任务置 completed 并释放锁）。
        /// </summary>
        Task<YzhTaskExecutionResult> ExecuteAsync(YzhQueueTask task, CancellationToken cancellationToken);

        /// <summary>
        /// 任务状态变更钩子（业务侧联动，如恢复资源可见性、清理临时数据）
        /// newStatus: pending(退避重试) / failed(最终失败) / cancelled(取消)
        /// </summary>
        Task OnTaskStateChangedAsync(YzhQueueTask task, string newStatus, string message);
    }
}

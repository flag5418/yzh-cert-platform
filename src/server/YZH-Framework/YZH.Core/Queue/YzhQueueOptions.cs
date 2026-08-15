namespace YZH.Core.Queue
{
    /// <summary>
    /// 队列引擎配置（默认值；可通过 appsettings "YzhQueue" 节覆盖）
    /// </summary>
    public class YzhQueueOptions
    {
        public const string SectionName = "YzhQueue";

        /// <summary>最大并发执行任务数</summary>
        public int MaxConcurrent { get; set; } = 5;

        /// <summary>单任务超时时间（秒）</summary>
        public int TimeoutSeconds { get; set; } = 300;

        /// <summary>领取租约时长（分钟），Worker 到期未完成视为卡死可被重新领取；
        /// 必须大于 TimeoutSeconds（任务超时）的分钟数，否则正常执行中的任务会被误回收；
        /// 8 分钟 > 300s 任务超时，且配合启动时立即回收（ReapStaleTasksOnStartupAsync），
        /// 进程重启导致的孤儿任务不再需要等满租约才能恢复</summary>
        public int LeaseMinutes { get; set; } = 8;

        /// <summary>最大重试次数</summary>
        public int MaxRetryCount { get; set; } = 3;
    }
}

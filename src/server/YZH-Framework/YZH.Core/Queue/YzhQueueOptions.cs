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

        /// <summary>领取租约时长（分钟），Worker 到期未完成视为卡死可被重新领取</summary>
        public int LeaseMinutes { get; set; } = 15;

        /// <summary>最大重试次数</summary>
        public int MaxRetryCount { get; set; } = 3;
    }
}

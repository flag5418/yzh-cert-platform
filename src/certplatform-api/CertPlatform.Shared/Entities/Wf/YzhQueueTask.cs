using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Wf
{
    /// <summary>
    /// 队列任务
    /// <para>表名：yzh_queue_task</para>
    /// </summary>
    [SugarTable("yzh_queue_task")]
    public class YzhQueueTask : BaseEntity, ISoftDelete
    {
        [SugarColumn(Length = 64)]
        public string QueueCode { get; set; } = string.Empty;

        [SugarColumn(Length = 30)]
        public string TaskType { get; set; } = string.Empty;

        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string? Payload { get; set; }

        [SugarColumn(Length = 20)]
        public string Status { get; set; } = "pending";

        [SugarColumn(Length = 20, IsNullable = true)]
        public string? ErrorType { get; set; }

        [SugarColumn(Length = 2000, IsNullable = true)]
        public string? ErrorMessage { get; set; }

        public int RetryCount { get; set; } = 0;

        public int MaxRetryCount { get; set; } = 3;

        public DateTime? NextRetryAt { get; set; }

        public DateTime? LockedUntil { get; set; }

        public DateTime? LockedAt { get; set; }

        [SugarColumn(Length = 100, IsNullable = true)]
        public string? LockedBy { get; set; }

        public DateTime? ProcessTime { get; set; }

        public DateTime? CompleteTime { get; set; }

        public DateTime? CreateTime { get; set; }

        [SugarColumn(Length = 64, IsNullable = true)]
        public string? TaskId { get; set; }

        [SugarColumn(Length = 50, IsNullable = true)]
        public string? UserCode { get; set; }

        [SugarColumn(Length = 100, IsNullable = true)]
        public string? UserName { get; set; }

        [SugarColumn(Length = 50, IsNullable = true)]
        public string? OrgCode { get; set; }

        public int Priority { get; set; } = 0;

        [SugarColumn(Length = 500, IsNullable = true)]
        public string? LockCodes { get; set; }

        // ──── ISoftDelete 接口显式实现 ────
        public bool IsDeleted { get; set; }
        public string? DeleteBy { get; set; }
        public DateTime? DeleteTime { get; set; }
    }
}

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VOL.Entity;
using VOL.Entity.SystemModels;

namespace YZH.Core.Queue
{
    /// <summary>
    /// yzh 队列子任务表（通用任务，跨项目复用）
    /// <para>业务数据统一进 payload JSON，按 task_type 由对应 IYzhTaskExecutor 解析执行</para>
    /// <para>file_convert payload 示例：{"fileCode":"FL-...","fileName":"x.doc","sourcePath":"/...","targetPath":"/.../.converted/x.docx","convertType":"doc2docx"}</para>
    /// </summary>
    [Entity(TableCnName = "yzh队列子任务", TableName = "yzh_queue_task", DBServer = "VOLContext")]
    [Table("yzh_queue_task")]
    public class YzhQueueTask : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public long Id { get; set; }

        /// <summary>全局唯一编码(GUID)</summary>
        [MaxLength(36)]
        [Column("code")]
        public string Code { get; set; }

        /// <summary>所属队列编码(yzh_queue.queue_code)</summary>
        [MaxLength(64)]
        [Column("queue_code")]
        public string QueueCode { get; set; } = string.Empty;

        /// <summary>任务类型：file_convert/auto_verify/report_generate</summary>
        [MaxLength(30)]
        [Column("task_type")]
        public string TaskType { get; set; } = string.Empty;

        /// <summary>业务数据 JSON（可空）</summary>
        [Column("payload", TypeName = "text")]
        public string? Payload { get; set; }

        /// <summary>状态：pending/processing/completed/failed/cancelled</summary>
        [MaxLength(20)]
        [Column("status")]
        public string Status { get; set; } = "pending";

        /// <summary>错误分类：retryable(可重试)/permanent(永久)（可空）</summary>
        [MaxLength(20)]
        [Column("error_type")]
        public string? ErrorType { get; set; }

        /// <summary>错误信息（可空）</summary>
        [MaxLength(2000)]
        [Column("error_message")]
        public string? ErrorMessage { get; set; }

        /// <summary>重试次数</summary>
        [Column("retry_count")]
        public int RetryCount { get; set; }

        /// <summary>最大重试次数</summary>
        [Column("max_retry_count")]
        public int MaxRetryCount { get; set; } = 3;

        /// <summary>下次重试时间（指数退避+抖动，到期才可再次领取）</summary>
        [Column("next_retry_at")]
        public DateTime? NextRetryAt { get; set; }

        /// <summary>领取租约到期时间：worker 领取时设置并续期；到期未完成视为卡死可被重新领取</summary>
        [Column("locked_until")]
        public DateTime? LockedUntil { get; set; }

        /// <summary>领取时间</summary>
        [Column("locked_at")]
        public DateTime? LockedAt { get; set; }

        /// <summary>领取 Worker 标识（可空）</summary>
        [MaxLength(100)]
        [Column("locked_by")]
        public string? LockedBy { get; set; }

        /// <summary>开始处理时间</summary>
        [Column("process_time")]
        public DateTime? ProcessTime { get; set; }

        /// <summary>完成/失败/取消时间</summary>
        [Column("complete_time")]
        public DateTime? CompleteTime { get; set; }

        /// <summary>入队时间</summary>
        [Column("create_time")]
        public DateTime CreateTime { get; set; } = DateTime.Now;

        /// <summary>来源批次ID（如上传任务taskId）（可空）</summary>
        [MaxLength(64)]
        [Column("task_id")]
        public string? TaskId { get; set; }

        /// <summary>发起用户ID</summary>
        [Column("user_id")]
        public int? UserId { get; set; }

        /// <summary>发起用户名（可空）</summary>
        [MaxLength(100)]
        [Column("user_name")]
        public string? UserName { get; set; }

        /// <summary>机构编码（可空）</summary>
        [MaxLength(50)]
        [Column("org_code")]
        public string? OrgCode { get; set; }

        /// <summary>优先级（0=普通，10=高优先）</summary>
        [Column("priority")]
        public int Priority { get; set; }

        /// <summary>本任务持有的资源锁编码（逗号分隔，可空）</summary>
        [MaxLength(500)]
        [Column("lock_codes")]
        public string? LockCodes { get; set; }

        [Column("create_id")]
        public int? CreateID { get; set; }

        [MaxLength(50)]
        [Column("creator")]
        public string? Creator { get; set; }

        [Column("create_date")]
        public DateTime? CreateDate { get; set; } = DateTime.Now;

        [Column("modify_id")]
        public int? ModifyID { get; set; }

        [MaxLength(50)]
        [Column("modifier")]
        public string? Modifier { get; set; }

        [Column("modify_date")]
        public DateTime? ModifyDate { get; set; }

        [Column("delete_id")]
        public int? DeleteID { get; set; }

        [MaxLength(50)]
        [Column("deleter")]
        public string? Deleter { get; set; }

        [Column("delete_time")]
        public DateTime? DeleteTime { get; set; }
    }
}

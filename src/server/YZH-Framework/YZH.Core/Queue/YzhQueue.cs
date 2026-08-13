using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VOL.Entity;
using VOL.Entity.SystemModels;

namespace YZH.Core.Queue
{
    /// <summary>
    /// yzh 队列主表（通用队列中心，跨项目复用）
    /// <para>一次业务动作 = 一个队列实例（如"上传批次 #abc 的 12 个 doc/xls 转换"）</para>
    /// <para>后续自动核验、报告生成等长任务均通过本队列体系执行</para>
    /// </summary>
    [Entity(TableCnName = "yzh队列主表", TableName = "yzh_queue", DBServer = "VOLContext")]
    [Table("yzh_queue")]
    public class YzhQueue : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public long Id { get; set; }

        /// <summary>全局唯一编码(GUID)，表间关联用</summary>
        [MaxLength(36)]
        [Column("code")]
        public string Code { get; set; } = string.Empty;

        /// <summary>队列业务编码：Q-{yyyyMMdd}-{6位随机}</summary>
        [MaxLength(64)]
        [Column("queue_code")]
        public string QueueCode { get; set; } = string.Empty;

        /// <summary>队列类型：file_convert/auto_verify/report_generate</summary>
        [MaxLength(30)]
        [Column("queue_type")]
        public string QueueType { get; set; } = string.Empty;

        /// <summary>队列名称（人话）（可空）</summary>
        [MaxLength(200)]
        [Column("queue_name")]
        public string? QueueName { get; set; }

        /// <summary>范围键（按类型约定格式）（可空）</summary>
        [MaxLength(200)]
        [Column("scope_key")]
        public string? ScopeKey { get; set; }

        /// <summary>冗余展示数据 JSON（可空）</summary>
        [Column("scope_info")]
        public string? ScopeInfo { get; set; }

        /// <summary>来源类型：upload_task/verify_req/report_req（可空）</summary>
        [MaxLength(30)]
        [Column("source_type")]
        public string? SourceType { get; set; }

        /// <summary>来源ID（可空）</summary>
        [MaxLength(64)]
        [Column("source_id")]
        public string? SourceId { get; set; }

        /// <summary>状态：pending/running/completed/failed/cancelled</summary>
        [MaxLength(20)]
        [Column("status")]
        public string Status { get; set; } = "pending";

        [Column("total_count")]
        public int TotalCount { get; set; }

        [Column("pending_count")]
        public int PendingCount { get; set; }

        [Column("processing_count")]
        public int ProcessingCount { get; set; }

        [Column("completed_count")]
        public int CompletedCount { get; set; }

        [Column("failed_count")]
        public int FailedCount { get; set; }

        [Column("cancelled_count")]
        public int CancelledCount { get; set; }

        [Column("progress")]
        public int Progress { get; set; }

        [Column("start_time")]
        public DateTime? StartTime { get; set; }

        [Column("end_time")]
        public DateTime? EndTime { get; set; }

        [MaxLength(500)]
        [Column("remark")]
        public string? Remark { get; set; }

        [MaxLength(50)]
        [Column("org_code")]
        public string? OrgCode { get; set; }

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

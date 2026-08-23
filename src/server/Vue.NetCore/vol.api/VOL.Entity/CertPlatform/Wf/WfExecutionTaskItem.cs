using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VOL.Entity.CertPlatform;

namespace VOL.Entity.CertPlatform.Wf
{
    /// <summary>
    /// WfExecutionTaskItem - 执行项
    /// <para>表名：wf_execution_task_item</para>
    /// <para>定位：一个NC检查项的独立执行单元，对应 yzh_queue_task 层</para>
    /// <para>item 之间默认并行，单 item 内节点按路径驱动串行</para>
    /// </summary>
    [Table("wf_execution_task_item")]
    public class WfExecutionTaskItem : YZHBaseEntity
    {
        /// <summary>wf_execution_task.code（所属任务）</summary>
        [Required]
        [MaxLength(36)]
        [Column("task_code")]
        public string TaskCode { get; set; }

        /// <summary>cert_validation_rule.rule_code（关联配置定义）</summary>
        [Required]
        [MaxLength(64)]
        [Column("rule_code")]
        public string RuleCode { get; set; }

        /// <summary>NC_CHECK | REPORT_GENERATE</summary>
        [Required]
        [MaxLength(20)]
        [Column("item_type")]
        public string ItemType { get; set; }

        /// <summary>queued|executing|completed|failed|cancelled</summary>
        [Required]
        [MaxLength(20)]
        [Column("item_status")]
        public string ItemStatus { get; set; } = "queued";

        /// <summary>业务成功标志：1=成功 0=失败 NULL=未完成（V3 新增）</summary>
        [Column("is_success")]
        public int? IsSuccess { get; set; }

        /// <summary>预热的缓存键列表（JSON数组，任务级缓存方案）</summary>
        [Column("cache_keys")]
        public string CacheKeys { get; set; }

        /// <summary>本项执行结果（end节点输出）</summary>
        [Column("result_summary", TypeName = "json")]
        public string ResultSummary { get; set; }

        /// <summary>失败原因</summary>
        [MaxLength(2000)]
        [Column("error_message")]
        public string ErrorMessage { get; set; }

        /// <summary>开始执行时间</summary>
        [Column("started_at")]
        public DateTime? StartedAt { get; set; }

        /// <summary>完成时间</summary>
        [Column("completed_at")]
        public DateTime? CompletedAt { get; set; }

        /// <summary>执行耗时(ms)</summary>
        [Column("duration_ms")]
        public int? DurationMs { get; set; }
    }
}

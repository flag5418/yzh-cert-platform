using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VOL.Entity.Admin.Platform;

namespace VOL.Entity.Admin.Platform.Wf
{
    /// <summary>
    /// WfExecutionTask - 工作流执行任务
    /// <para>表名：wf_execution_task</para>
    /// <para>定位：一次业务触发 = 一个任务，对应 yzh_queue 层</para>
    /// <para>TEST / NC_CHECK / REPORT_GENERATE 统一建模</para>
    /// </summary>
    [Table("wf_execution_task")]
    public class WfExecutionTask : YZHBaseEntity
    {
        /// <summary>任务类型：TEST | NC_CHECK | REPORT_GENERATE</summary>
        [Required]
        [MaxLength(20)]
        [Column("task_type")]
        public string TaskType { get; set; }

        /// <summary>执行状态：queued|executing|completed|failed|cancelled</summary>
        [Required]
        [MaxLength(20)]
        [Column("task_status")]
        public string TaskStatus { get; set; } = "queued";

        /// <summary>执行时的工作流配置快照（从 cert_validation_rule.rule_json 锁定）</summary>
        [Required]
        [Column("config_snapshot", TypeName = "json")]
        public string ConfigSnapshot { get; set; }

        /// <summary>cert_validation_rule.rule_code（配置来源）</summary>
        [MaxLength(64)]
        [Column("rule_code")]
        public string RuleCode { get; set; }

        /// <summary>企业编码（运行时绑定，TEST 模式使用固定测试企业）</summary>
        [MaxLength(50)]
        [Column("enterprise_code")]
        public string EnterpriseCode { get; set; }

        /// <summary>审核阶段</summary>
        [MaxLength(30)]
        [Column("phase_code")]
        public string PhaseCode { get; set; }

        /// <summary>yzh_queue.queue_code（关联队列层）</summary>
        [MaxLength(64)]
        [Column("queue_code")]
        public string QueueCode { get; set; }

        /// <summary>预热的缓存键列表（JSON数组，任务级缓存方案）</summary>
        [Column("cache_keys")]
        public string CacheKeys { get; set; }

        /// <summary>执行结果摘要（end节点输出）</summary>
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

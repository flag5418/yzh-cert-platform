using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Wf
{
    /// <summary>
    /// WfExecutionTask - 工作流执行任务
    /// <para>表名：wf_execution_task</para>
    /// <para>定位：一次业务触发 = 一个任务，对应 yzh_queue 层</para>
    /// <para>TEST / NC_CHECK / REPORT_GENERATE 统一建模</para>
    ///
    /// 命名规范（YZH 铁律）：DB 列名 = C# 属性名 = PascalCase
    /// </summary>
    [SugarTable("wf_execution_task")]
    public class WfExecutionTask : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / 审计字段由 BaseEntity 基类统一提供 ────
        // ──── ISoftDelete / IIsValid 接口字段由接口提供 ────

        /// <summary>任务类型：TEST | NC_CHECK | REPORT_GENERATE</summary>
        [Required]
        [MaxLength(20)]
        public string TaskType { get; set; } = string.Empty;

        /// <summary>执行状态：queued|executing|completed|failed|cancelled</summary>
        [Required]
        [MaxLength(20)]
        public string TaskStatus { get; set; } = "queued";

        /// <summary>执行时的工作流配置快照（从 cert_validation_rule.workflow_config 锁定）</summary>
        [Required]
        [SugarColumn(ColumnDataType = "json")]
        public string ConfigSnapshot { get; set; } = string.Empty;

        /// <summary>cert_validation_rule.rule_code（配置来源）</summary>
        [MaxLength(64)]
        public string RuleCode { get; set; } = string.Empty;

        /// <summary>企业编码（运行时绑定，TEST 模式使用固定测试企业）</summary>
        [MaxLength(50)]
        public string EnterpriseCode { get; set; } = string.Empty;

        /// <summary>审核阶段</summary>
        [MaxLength(30)]
        public string PhaseCode { get; set; } = string.Empty;

        /// <summary>yzh_queue.QueueCode（关联队列层）</summary>
        [MaxLength(64)]
        public string QueueCode { get; set; } = string.Empty;

        /// <summary>预热的缓存键列表（JSON数组，任务级缓存方案）</summary>
        [SugarColumn(ColumnDataType = "json")]
        public string CacheKeys { get; set; } = "[]";

        /// <summary>执行结果摘要（end节点输出）</summary>
        [SugarColumn(ColumnDataType = "json")]
        public string ResultSummary { get; set; } = "{}";

        /// <summary>失败原因</summary>
        [MaxLength(2000)]
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>开始执行时间</summary>
        public DateTime? StartedAt { get; set; }

        /// <summary>完成时间</summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>执行耗时(ms)</summary>
        public int? DurationMs { get; set; }

        // ──── ISoftDelete + IIsValid 接口显式实现 ────
        public bool IsDeleted { get; set; }
        public string? DeleteBy { get; set; }
        public DateTime? DeleteTime { get; set; }
        public int IsValid { get; set; } = 1;
    }
}

using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Entity.Admin.Platform;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Wf
{
    /// <summary>
    /// WfExecutionTaskItem - 执行项
    /// <para>表名：wf_execution_task_item</para>
    /// <para>定位：一个NC检查项的独立执行单元，对应 yzh_queue_task 层</para>
    /// <para>item 之间默认并行，单 item 内节点按路径驱动串行</para>
    ///
    /// 命名规范（YZH 铁律）：DB 列名 = C# 属性名 = PascalCase
    /// </summary>
    [SugarTable("wf_execution_task_item")]
    public class WfExecutionTaskItem : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / 审计字段由 BaseEntity 基类统一提供 ────
        // ──── ISoftDelete / IIsValid 接口字段由接口提供 ────

        /// <summary>wf_execution_task.Code（所属任务）</summary>
        [Required]
        [MaxLength(36)]
        [UniqueField("任务编码", WithFields = new[] { "RuleCode" })]
        public string TaskCode { get; set; } = string.Empty;

        /// <summary>cert_validation_rule.RuleCode（关联配置定义）</summary>
        [Required]
        [MaxLength(64)]
        public string RuleCode { get; set; } = string.Empty;

        /// <summary>NC_CHECK | REPORT_GENERATE</summary>
        [Required]
        [MaxLength(20)]
        public string ItemType { get; set; } = string.Empty;

        /// <summary>queued|executing|completed|failed|cancelled</summary>
        [Required]
        [MaxLength(20)]
        public string ItemStatus { get; set; } = "queued";

        /// <summary>业务成功标志：1=成功 0=失败 NULL=未完成（V3 新增）</summary>
        public int? IsSuccess { get; set; }

        /// <summary>预热的缓存键列表（JSON数组，任务级缓存方案）</summary>
        [SugarColumn(ColumnDataType = "json")]
        public string CacheKeys { get; set; } = "[]";

        /// <summary>本项执行结果（end节点输出）</summary>
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

using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Entity.Admin.Platform;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Wf
{
    /// <summary>
    /// WfPathExecution - 路径执行记录
    /// <para>表名：wf_path_execution</para>
    /// <para>定位：四层执行模型的第三层 —— 一条路径（start → … → end）的执行事实。</para>
    ///
    /// <para><b>为什么需要这一层</b>：<c>wf_node_execution</c> 的唯一键是
    /// <c>(TaskCode, ItemCode, NodeId)</c>，同一节点在多条路径共享时只能落一行，
    /// 于是「这个节点在本路径是复用还是真跑」的信息在 DB 层被抹平（去重后 <c>IsReused</c> 恒为 0）。
    /// 本表按 <c>(TaskCode, ItemCode, PathIndex)</c> 为唯一键，把每条路径的
    /// 节点序列、失败点、最终输出、耗时完整保留，节点复用信息终于能在 DB 层体现。</para>
    ///
    /// <para>完整层次：</para>
    /// <code>
    /// wf_execution_task          一次触发（TEST / NC_CHECK / REPORT_GENERATE）
    ///   └─ wf_execution_task_item   一个检查项
    ///        └─ wf_path_execution      一条路径        ← 本表
    ///             └─ wf_node_execution     一个节点
    /// </code>
    ///
    /// 命名规范（YZH 铁律）：DB 列名 = C# 属性名 = PascalCase
    /// </summary>
    [SugarTable("wf_path_execution")]
    public class WfPathExecution : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / 审计字段由 BaseEntity 基类统一提供 ────
        // ──── ISoftDelete / IIsValid 接口字段由接口提供 ────

        // ──── 业务字段 ────
        /// <summary>wf_execution_task.Code</summary>
        [Required]
        [MaxLength(36)]
        [UniqueField("路径索引", WithFields = new[] { "ItemCode", "PathIndex" })]
        public string TaskCode { get; set; } = string.Empty;

        /// <summary>wf_execution_task_item.Code</summary>
        [Required]
        [MaxLength(36)]
        public string ItemCode { get; set; } = string.Empty;

        /// <summary>路径索引（从 0 开始，对应 WorkflowInterpreter 枚举出的路径序号）</summary>
        public int PathIndex { get; set; }

        /// <summary>路径状态：pending|executing|completed|failed</summary>
        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "pending";

        /// <summary>路径节点ID列表（按执行顺序，JSON 数组）</summary>
        [SugarColumn(ColumnDataType = "json")]
        public string? NodeIds { get; set; }

        /// <summary>
        /// 本路径内<b>复用</b>（未真跑，取自跨路径结果池）的节点数量
        /// <para>补列理由：本表的存在意义就是让「节点复用」在 DB 层可见。
        /// 仅有 <see cref="NodeIds"/> 时，读者无法判断某条路径里哪些节点是真跑、
        /// 哪些是复用（<c>wf_node_execution</c> 已按节点去重，查不出路径维度）。
        /// 有了这一列，一条 SQL 即可回答「第 2 条路径复用了 2 个节点、耗时 0ms」。</para>
        /// </summary>
        public int ReusedCount { get; set; }

        /// <summary>失败节点ID（成功时为 null）</summary>
        [MaxLength(64)]
        public string? FailedAtNodeId { get; set; }

        /// <summary>失败原因（成功时为 null）</summary>
        [MaxLength(2000)]
        public string? ErrorMessage { get; set; }

        /// <summary>路径最终输出（end 节点输出，JSON；超长按 64KB 截断）</summary>
        [SugarColumn(ColumnDataType = "json")]
        public string? OutputJson { get; set; }

        /// <summary>路径耗时(ms)</summary>
        public int? DurationMs { get; set; }

        /// <summary>路径开始时间</summary>
        public DateTime? StartedAt { get; set; }

        /// <summary>路径完成时间</summary>
        public DateTime? CompletedAt { get; set; }

        // ──── ISoftDelete + IIsValid 接口显式实现 ────
        public bool IsDeleted { get; set; }
        public string? DeleteBy { get; set; }
        public DateTime? DeleteTime { get; set; }
        public int IsValid { get; set; } = 1;
    }
}

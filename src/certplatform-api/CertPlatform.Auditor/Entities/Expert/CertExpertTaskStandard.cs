using System;
using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Auditor.Entities.Expert
{
    /// <summary>
    /// 专家任务-标准子任务（专家任务系统 A 组）
    /// <para>表名：<c>cert_expert_task_standard</c></para>
    ///
    /// <para><b>D02：1 任务 → N 标准子任务 → N 队列</b>。一个任务可能覆盖多个标准
    /// （如同时申请 ISO9001 + ISO14001），每个标准**独立一条子任务 + 一个队列**，
    /// 因为「规则集合 / 提取结果 / 章节集合」都是按标准隔离的，串行执行无法表达进度。</para>
    ///
    /// <para>唯一键 <c>uk_task_std(TaskCode, StandardCode)</c> ⇒ 同一任务内标准不重复。</para>
    /// </summary>
    [SugarTable("cert_expert_task_standard")]
    public class CertExpertTaskStandard : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / 审计字段由 BaseEntity 提供 ────

        /// <summary>★ 专家工作区编码（租户隔离键；★ 禁止为 NULL）</summary>
        [SugarColumn(Length = 50)]
        public string OrgCode { get; set; } = string.Empty;

        /// <summary>创建人姓名（冗余）</summary>
        [SugarColumn(Length = 100, IsNullable = true)]
        public string? CreateName { get; set; }

        /// <summary>业务状态（保留）</summary>
        [SugarColumn(Length = 50, IsNullable = true)]
        public string? Status { get; set; }

        /// <summary>排序号</summary>
        public int Sort { get; set; }

        /// <summary>备注</summary>
        [SugarColumn(Length = 500, IsNullable = true)]
        public string? Remark { get; set; }

        // ──── 归属 ────

        /// <summary>所属任务编码（<c>cert_expert_task.Code</c>）</summary>
        [SugarColumn(Length = 36)]
        public string TaskCode { get; set; } = string.Empty;

        /// <summary>任务编号（冗余，列表免 JOIN）</summary>
        [SugarColumn(Length = 50, IsNullable = true)]
        public string? TaskNumber { get; set; }

        /// <summary>标准编码（<c>cert_iso_standard.Code</c>）</summary>
        [SugarColumn(Length = 36)]
        public string StandardCode { get; set; } = string.Empty;

        /// <summary>标准名称（冗余快照）</summary>
        [SugarColumn(Length = 200, IsNullable = true)]
        public string? StandardName { get; set; }

        // ──── 状态 ────

        /// <summary>★执行状态（与任务头同枚举）：draft | pending_run | running | completed | failed</summary>
        [SugarColumn(Length = 20)]
        public string ExecStatus { get; set; } = "draft";

        /// <summary>★存续状态：active | archived | cancelled | voided</summary>
        [SugarColumn(Length = 20)]
        public string LifecycleStatus { get; set; } = "active";

        /// <summary>队列编码（1 子任务 = 1 队列）</summary>
        [SugarColumn(Length = 36, IsNullable = true)]
        public string? QueueCode { get; set; }

        // ──── 计数与进度 ────

        /// <summary>任务项总数</summary>
        public int ItemCount { get; set; }

        /// <summary>已执行完（含失败/跳过）</summary>
        public int DoneCount { get; set; }

        /// <summary>已认可数</summary>
        public int AckedCount { get; set; }

        /// <summary>已修改数</summary>
        public int ModifiedCount { get; set; }

        /// <summary>已跳过数</summary>
        public int SkippedCount { get; set; }

        /// <summary>待认可数</summary>
        public int PendingCount { get; set; }

        /// <summary>执行失败数</summary>
        public int FailedCount { get; set; }

        /// <summary>未处理缺口数</summary>
        public int GapCount { get; set; }

        /// <summary>执行进度 %</summary>
        [SugarColumn(DecimalDigits = 2, ColumnDataType = "decimal(5,2)")]
        public decimal Progress { get; set; }

        // ──── 时间与错误 ────

        /// <summary>开始时间</summary>
        public DateTime? StartTime { get; set; }

        /// <summary>结束时间</summary>
        public DateTime? FinishTime { get; set; }

        /// <summary>最后一次错误摘要</summary>
        [SugarColumn(Length = 1000, IsNullable = true)]
        public string? LastError { get; set; }

        // ──── ISoftDelete / IIsValid ────

        /// <summary>软删除标记</summary>
        public bool IsDeleted { get; set; }

        /// <summary>删除人 Code</summary>
        public string? DeleteBy { get; set; }

        /// <summary>删除时间</summary>
        public DateTime? DeleteTime { get; set; }

        /// <summary>有效标志（1=有效，0=无效）</summary>
        public int IsValid { get; set; } = 1;
    }
}

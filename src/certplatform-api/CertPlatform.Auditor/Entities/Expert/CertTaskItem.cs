using System;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Auditor.Entities.Expert
{
    /// <summary>
    /// 任务 × 检查项（V2 — 执行状态 + RunMode + RerunState + 锁定节流）
    /// <para>表名：<c>cert_task_item</c></para>
    /// <para>一次任务对每个检查项的执行态 + 重跑控制。</para>
    /// <para><see cref="CheckItemCode"/> = 计划明细 Code（cert_audit_plan_detail.Code），非嵌套主键。</para>
    /// </summary>
    [SugarTable("cert_task_item")]
    public class CertTaskItem : BaseEntity, ISoftDelete, IIsValid
    {
        /// <summary>所属任务（关联 cert_task.Code）</summary>
        [StringLength(50)]
        public string TaskCode { get; set; } = string.Empty;

        /// <summary>检查项 Code = cert_audit_plan_detail.Code</summary>
        [StringLength(50)]
        public string CheckItemCode { get; set; } = string.Empty;

        /// <summary>waiting / ready / analyzing / analyzed / reviewed / failed</summary>
        [StringLength(20)]
        public string Status { get; set; } = "waiting";

        /// <summary>none / partial / ready（按方向：prep 看资料证据，onsite 看现场证据）</summary>
        [StringLength(20)]
        public string EvidenceState { get; set; } = "none";

        /// <summary>none / done / stale（证据变了就 stale）</summary>
        [StringLength(20)]
        public string AnalysisState { get; set; } = "none";

        /// <summary>反复执行计数</summary>
        public int AnalyzeCount { get; set; }

        /// <summary>最近分析时间</summary>
        public DateTime? LastAnalyzedAt { get; set; }

        /// <summary>本方向可见证据条数</summary>
        public int EvidenceCount { get; set; }

        /// <summary>重跑模式：full / incremental / selective</summary>
        [StringLength(20)]
        public string RunMode { get; set; } = "incremental";

        /// <summary>1 = incremental 时跳过 IsLocked=1 的结论项（LLM 节流开关）</summary>
        public bool SkipLocked { get; set; } = true;

        /// <summary>none / suggested / forced — 证据变更触发的建议重跑状态</summary>
        [StringLength(20)]
        public string RerunState { get; set; } = "none";

        /// <summary>原因列表 JSON：[{evidence_code, kind, old_md5, new_md5, snippet}]</summary>
        public string? RerunReason { get; set; }

        /// <summary>触发时间</summary>
        public DateTime? RerunTriggeredAt { get; set; }

        /// <summary>触发来源：evidence_change / doc_reupload / manual</summary>
        [StringLength(20)]
        public string? RerunTriggeredBy { get; set; }

        /// <summary>失败原因（必填当 failed）或提示信息</summary>
        [StringLength(500)]
        public string? Message { get; set; }

        /// <summary>排序号</summary>
        public int Sort { get; set; }

        /// <summary>备注</summary>
        [MaxLength(500)]
        public string? Remark { get; set; }

        // ──── 接口字段（BaseEntity 不包含，由接口继承提供） ────

        /// <summary>有效标志（1=启用，0=停用）—— 铁律九统一用 IsValid</summary>
        public int IsValid { get; set; } = 1;

        /// <summary>软删除标记</summary>
        public bool IsDeleted { get; set; }

        /// <summary>删除人 Code</summary>
        public string? DeleteBy { get; set; }

        /// <summary>删除时间</summary>
        public DateTime? DeleteTime { get; set; }
    }
}

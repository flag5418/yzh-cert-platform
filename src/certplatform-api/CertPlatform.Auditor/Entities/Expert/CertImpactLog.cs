using System;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Auditor.Entities.Expert
{
    /// <summary>
    /// 影响分析日志（V2 — 证据/文档 MD5 变化 → 影响检查项追踪）
    /// <para>表名：<c>cert_impact_log</c></para>
    /// <para>当文档重传或证据内容变更（MD5 变化）时，记录影响的检查项/任务项数量及锁定节流情况。</para>
    /// <para>不实现 ISoftDelete（审计日志不可删），仅 IIsValid 标记有效。</para>
    /// </summary>
    [SugarTable("cert_impact_log")]
    public class CertImpactLog : BaseEntity, IIsValid
    {
        /// <summary>触发类型：doc_reupload / evidence_change / manual</summary>
        [StringLength(30)]
        public string TriggerKind { get; set; } = "doc_reupload";

        /// <summary>触发来源：FileCode（文档重传）/ EvidenceCode（证据修改）/ 操作 Code</summary>
        [StringLength(50)]
        public string TriggerSource { get; set; } = string.Empty;

        /// <summary>变更前 MD5（文档级或证据级，可为空）</summary>
        [StringLength(32)]
        public string? OldMD5 { get; set; }

        /// <summary>变更后 MD5（文档级或证据级，可为空）</summary>
        [StringLength(32)]
        public string? NewMD5 { get; set; }

        /// <summary>受影响的检查项数量</summary>
        public int AffectedCheckItemCount { get; set; }

        /// <summary>受影响的任务项数量</summary>
        public int AffectedTaskItemCount { get; set; }

        /// <summary>因锁定而跳过 LLM 节流的项数</summary>
        public int SkippedLockedCount { get; set; }

        /// <summary>受影响项清单 JSON：[{CheckItemCode, TaskItemCode, Action, LockedState}]</summary>
        public string? AffectedItemsJson { get; set; }

        /// <summary>操作人 Code</summary>
        [StringLength(64)]
        public string? CreatedBy { get; set; }

        /// <summary>创建时间</summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // ──── 接口字段 ────

        /// <summary>有效标志（1=启用，0=停用）—— 铁律九统一用 IsValid</summary>
        public int IsValid { get; set; } = 1;
    }
}

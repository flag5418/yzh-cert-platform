using System;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Auditor.Entities.Expert
{
    /// <summary>
    /// NC 单（V2 — 独立生命周期：AI 建议 → 人签发 → 整改 → 关闭）
    /// <para>表名：<c>cert_nc</c></para>
    /// <para>一个 NC 可跨检查项（合并 NC 时 <see cref="ClauseCodes"/> 存 JSON 数组）。</para>
    /// <para>NC 与检查项是 N:1（CheckItemCode 来源），但可升级到跨条款。</para>
    /// </summary>
    [SugarTable("cert_nc")]
    public class CertNc : BaseEntity, ISoftDelete, IIsValid
    {
        /// <summary>NC 编号（NC-20261030-001，报告引用）</summary>
        [StringLength(50)]
        public string NcNo { get; set; } = string.Empty;

        /// <summary>来源检查项 Code</summary>
        [StringLength(50)]
        public string CheckItemCode { get; set; } = string.Empty;

        /// <summary>主条款 Code（用于报告定位）</summary>
        [StringLength(50)]
        public string? ClauseCode { get; set; }

        /// <summary>涉及多条条款时用的 JSON 条款 Code 数组（同根因跨条款合并）</summary>
        public string? ClauseCodes { get; set; }

        /// <summary>major / minor</summary>
        [StringLength(20)]
        public string? Severity { get; set; }

        /// <summary>不符合事实描述</summary>
        public string? FactDesc { get; set; }

        /// <summary>违反条款原文引用</summary>
        public string? RequirementRef { get; set; }

        /// <summary>整改要求</summary>
        public string? CorrectiveReq { get; set; }

        /// <summary>整改截止日期</summary>
        public DateTime? DueDate { get; set; }

        /// <summary>责任人</summary>
        [StringLength(200)]
        public string? Responsible { get; set; }

        // ═══ 生命周期 ═══

        /// <summary>pending_review / issued / rectifying / verifying / closed / escalated</summary>
        [StringLength(20)]
        public string Status { get; set; } = "pending_review";

        /// <summary>ai_suggest / manual（系统只建议，人签发）</summary>
        [StringLength(20)]
        public string? Origin { get; set; }

        /// <summary>签发人 Code</summary>
        [StringLength(64)]
        public string? IssuedBy { get; set; }

        /// <summary>签发时间</summary>
        public DateTime? IssuedAt { get; set; }

        /// <summary>哪次检查任务开的（跨年可比较）</summary>
        [StringLength(50)]
        public string? CreatedInTaskCode { get; set; }

        /// <summary>关闭证据 JSON：[{evidence_code, desc}]</summary>
        public string? CloseEvidenceJson { get; set; }

        /// <summary>关闭人 Code</summary>
        [StringLength(64)]
        public string? ClosedBy { get; set; }

        /// <summary>关闭时间</summary>
        public DateTime? ClosedAt { get; set; }

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

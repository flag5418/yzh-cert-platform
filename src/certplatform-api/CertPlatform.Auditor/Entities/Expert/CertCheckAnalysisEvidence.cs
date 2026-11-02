using System;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Auditor.Entities.Expert
{
    /// <summary>
    /// 分析 × 证据（证据链 · 含内容快照）
    /// <para>表名：<c>cert_check_analysis_evidence</c></para>
    /// <para>回答"这个结论基于哪几条证据"。</para>
    /// <para><b>为什么必须有快照</b>：证据是资产、会被改（<c>PrevContentMD5</c> 机制就是干这个的）。
    /// 报告三个月后要能回答「当时凭什么判的」——只存 <see cref="EvidenceCode"/> 不够，必须存当时的内容。</para>
    /// <para><b>为什么证据不挂分析</b>：证据一次采到、多次分析复用（准备采的文档切片，检查阶段照样要看）。
    /// 挂分析会导致重跑时证据被"重新归属"，历史分析链断掉 —— 用关联表表达"这次用了它"，而不是"它属于这次"。</para>
    /// </summary>
    [SugarTable("cert_check_analysis_evidence")]
    public class CertCheckAnalysisEvidence : BaseEntity, ISoftDelete, IIsValid
    {
        /// <summary>所属分析（关联 cert_check_analysis.Code）</summary>
        [StringLength(50)]
        public string AnalysisCode { get; set; } = string.Empty;

        /// <summary>证据 Code（关联 cert_evidence.Code）</summary>
        [StringLength(50)]
        public string EvidenceCode { get; set; } = string.Empty;

        /// <summary>0/1：是否真的喂给了模型（Status=dropped 的不喂，但保留关联）</summary>
        public bool UsedInPrompt { get; set; } = true;

        /// <summary>★ 分析当时该证据的 LlmSummary 快照</summary>
        public string? SnippetSnapshot { get; set; }

        /// <summary>★ 分析当时的内容 MD5（证明"当时用的是这版"）</summary>
        [StringLength(64)]
        public string? ContentMD5Snapshot { get; set; }

        /// <summary>该条占多少 token（截断决策留痕）</summary>
        public int? TokenEstimate { get; set; }

        /// <summary>在提示词里的顺序</summary>
        public int OrderNo { get; set; }

        /// <summary>备注</summary>
        [MaxLength(500)]
        public string? Remark { get; set; }

        // ──── 接口字段 ────

        /// <summary>有效标志</summary>
        public int IsValid { get; set; } = 1;

        /// <summary>软删除标记</summary>
        public bool IsDeleted { get; set; }

        /// <summary>删除人 Code</summary>
        public string? DeleteBy { get; set; }

        /// <summary>删除时间</summary>
        public DateTime? DeleteTime { get; set; }
    }
}

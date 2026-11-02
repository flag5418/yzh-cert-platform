using System;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Auditor.Entities.Expert
{
    /// <summary>
    /// 证据（V2 — 挂检查项 + MD5 修改检测 + LlmSummary 标准化文本）
    /// <para>表名：<c>cert_evidence</c></para>
    /// <para>证据类型：doc_slice（来自文档切片）/ pq_result（公共查询结果）/ lead（线索）/ onsite_media（现场媒体）/ manual（人工补充）。</para>
    /// <para>证据跨任务共享（不同任务看到的证据相同），由 <see cref="CapturedIn"/> 区分采集方向。</para>
    /// <para><see cref="ContentMD5"/> 用于修改检测；<see cref="LlmSummary"/> 是实际喂给 LLM 的标准化文本。</para>
    /// </summary>
    [SugarTable("cert_evidence")]
    public class CertEvidence : BaseEntity, ISoftDelete, IIsValid
    {
        /// <summary>检查项 Code（关联 cert_audit_plan_detail.Code）</summary>
        [StringLength(50)]
        public string CheckItemCode { get; set; } = string.Empty;

        /// <summary>条款 Code（可空 = 检查项级）</summary>
        [StringLength(50)]
        public string? ClauseCode { get; set; }

        /// <summary>谁提供的：prep / onsite / manual / doc / pq</summary>
        [StringLength(20)]
        public string CapturedIn { get; set; } = "manual";

        /// <summary>采集它的任务 Code（可空 = 导入/手工）</summary>
        [StringLength(50)]
        public string? CapturedTaskCode { get; set; }

        /// <summary>证据类型：doc_slice / pq_result / lead / onsite_media / interview / sample / manual</summary>
        [StringLength(30)]
        public string EvidenceKind { get; set; } = "manual";

        /// <summary>FileCode / PqResultCode / 人工自填</summary>
        [StringLength(50)]
        public string? SourceCode { get; set; }

        /// <summary>来源名称（冗余便于展示）</summary>
        [StringLength(200)]
        public string? SourceName { get; set; }

        /// <summary>锚点定位（如页码/段落号）</summary>
        [StringLength(500)]
        public string? Anchor { get; set; }

        /// <summary>原文摘录</summary>
        public string? Snippet { get; set; }

        /// <summary>置信度（0-1；doc_slice 用）</summary>
        public decimal? Confidence { get; set; }

        // ═══ LLM 消费字段 ═══

        /// <summary>证据内容 MD5（Snippet 或 LlmSummary 的 MD5；用于修改检测）</summary>
        [StringLength(32)]
        public string? ContentMD5 { get; set; }

        /// <summary>LLM 可消费的标准化文本（OCR 结果 / 转录文本 / 清洗后原文；这是实际喂给模型的内容）</summary>
        public string? LlmSummary { get; set; }

        /// <summary>LlmSummary 的预估 token 数（聚合截断用）</summary>
        public int? TokenEstimate { get; set; }

        /// <summary>上次内容 MD5（修改检测：≠ 新 MD5 则触发影响分析）</summary>
        [StringLength(32)]
        public string? PrevContentMD5 { get; set; }

        // ═══ 媒体与附件 ═══

        /// <summary>audio / video / image</summary>
        [StringLength(20)]
        public string? MediaKind { get; set; }

        /// <summary>媒体时长（秒）</summary>
        public int? DurationSec { get; set; }

        /// <summary>现场录音/视频的转录文本</summary>
        public string? TranscriptText { get; set; }

        /// <summary>附件 JSON 数组：[{path, type, name, size}]（图片/PDF）</summary>
        public string? AttachmentsJson { get; set; }

        // ═══ 取证信息 ═══

        /// <summary>取证时间</summary>
        public DateTime? CapturedAt { get; set; }

        /// <summary>取证地点</summary>
        [StringLength(200)]
        public string? CapturedPlace { get; set; }

        /// <summary>在场人 JSON 数组：[{name, role}]</summary>
        public string? PresentPersons { get; set; }

        // ═══ 状态 ═══

        /// <summary>collected / confirmed / dropped（dropped 不进分析输入）</summary>
        [StringLength(20)]
        public string Status { get; set; } = "collected";

        /// <summary>废弃原因（status=dropped 时必填）</summary>
        [StringLength(500)]
        public string? DroppedReason { get; set; }

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

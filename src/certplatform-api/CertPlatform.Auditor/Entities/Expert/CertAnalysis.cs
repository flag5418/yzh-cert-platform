using System;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Auditor.Entities.Expert
{
    /// <summary>
    /// 分析记录（V2 — 追加型：证据指纹 + LockedSkippedCount LLM 节流指标）
    /// <para>表名：<c>cert_analysis</c></para>
    /// <para>每次 LLM 分析一条记录（追加），不覆盖。</para>
    /// <para>用于：① 结果可追溯（这次跑了什么提示词、花了多少 token）② 指标统计（LockedSkippedCount）。</para>
    /// <para>不实现 ISoftDelete（分析记录不可删，只能新增）；IIsValid 标记数据有效。</para>
    /// </summary>
    [SugarTable("cert_analysis")]
    public class CertAnalysis : BaseEntity, IIsValid
    {
        /// <summary>关联 cert_task.Code</summary>
        [StringLength(50)]
        public string TaskCode { get; set; } = string.Empty;

        /// <summary>关联 cert_task_item.Code</summary>
        [StringLength(50)]
        public string TaskItemCode { get; set; } = string.Empty;

        /// <summary>prep / onsite（冗余便于筛选）</summary>
        [StringLength(20)]
        public string? TaskKind { get; set; }

        /// <summary>本次证据指纹 = MD5(GROUP_CONCAT(CONCAT(e.Code, ':', e.ContentMD5, ':', e.Status) ORDER BY e.Code ASC))</summary>
        [StringLength(64)]
        public string? EvidenceFingerprint { get; set; }

        /// <summary>本次跳过的已锁定结论条数（LLM 节流指标）</summary>
        public int LockedSkippedCount { get; set; }

        /// <summary>本次实际喂给 LLM 的证据条数</summary>
        public int EvidenceUsedCount { get; set; }

        /// <summary>提示词模板 Code</summary>
        [StringLength(50)]
        public string? PromptCode { get; set; }

        /// <summary>提示词版本号</summary>
        [StringLength(20)]
        public string? PromptVersion { get; set; }

        /// <summary>模型名（如 claude-opus-4-6）</summary>
        [StringLength(50)]
        public string? ModelName { get; set; }

        /// <summary>本次分析输入 token 数</summary>
        public int? InputTokenCount { get; set; }

        /// <summary>本次分析输出 token 数</summary>
        public int? OutputTokenCount { get; set; }

        /// <summary>产出摘要 JSON：{point_count, nc_suggested_count, prep_state_distribution, conclusion_distribution}</summary>
        public string? ResultSummaryJson { get; set; }

        /// <summary>本次分析耗时（毫秒）</summary>
        public int? DurationMs { get; set; }

        /// <summary>失败原因（若分析失败）</summary>
        public string? ErrorMessage { get; set; }

        // ──── 接口字段 ────

        /// <summary>有效标志（1=启用，0=停用）—— 铁律九统一用 IsValid</summary>
        public int IsValid { get; set; } = 1;
    }
}

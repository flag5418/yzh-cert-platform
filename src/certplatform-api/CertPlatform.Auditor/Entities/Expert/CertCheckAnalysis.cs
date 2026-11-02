using System;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Auditor.Entities.Expert
{
    /// <summary>
    /// 入场分析（一次分析一行 · 分支隔离）
    /// <para>表名：<c>cert_check_analysis</c></para>
    /// <para><b>这是"一次分析"的容器，不是"当前结论"的家</b>（46 号 §4.5.1）。</para>
    /// <para>一行 = 「某检查项 × 某分支 × 第 N 次分析」；结果行（<see cref="CertCheckResult"/>）、
    /// 证据链（<see cref="CertCheckAnalysisEvidence"/>）、疑点（cert_point）都挂在它下面。</para>
    /// <para>为什么必须独立：准备分支与检查分支各有自己的结果、自己的证据链、自己的疑点，
    /// 挤在「检查项 × 条款」一行里必然互相覆盖（F13）。</para>
    /// </summary>
    [SugarTable("cert_check_analysis")]
    public class CertCheckAnalysis : BaseEntity, ISoftDelete, IIsValid
    {
        /// <summary>检查项 Code（= cert_audit_plan_detail.Code）</summary>
        [StringLength(50)]
        public string CheckItemCode { get; set; } = string.Empty;

        /// <summary>分支：prep=认证准备 / onsite=认证检查</summary>
        [StringLength(20)]
        public string StageKind { get; set; } = "onsite";

        /// <summary>企业编码（冗余，免 join）</summary>
        [StringLength(50)]
        public string? EnterpriseCode { get; set; }

        /// <summary>计划 Code（冗余，免 join）</summary>
        [StringLength(50)]
        public string? PlanCode { get; set; }

        /// <summary>哪次任务跑的（关联 cert_task.Code；NULL = 手工发起）</summary>
        [StringLength(50)]
        public string? TaskCode { get; set; }

        /// <summary>任务项 Code（关联 cert_task_item.Code）</summary>
        [StringLength(50)]
        public string? TaskItemCode { get; set; }

        /// <summary>
        /// 1 = 当前生效；<c>NULL</c> = 历史。
        /// <para>⚠️ <b>必须用 NULL 表历史，不要用 0</b>：唯一键 <c>uk_current (CheckItemCode, StageKind, IsCurrent)</c>
        /// 依赖"MySQL 唯一索引允许多个 NULL"这一特性 —— 用 0 的话 <c>(item, prep, 0)</c> 只能有一条，
        /// 重跑两次就唯一键冲突（46 号 R10）。</para>
        /// </summary>
        public byte? IsCurrent { get; set; }

        /// <summary>该 (检查项, 分支) 的第几次分析，从 1 起</summary>
        public int RunNo { get; set; } = 1;

        /// <summary>running / done / failed / stale（stale = 锁定项证据变更，见 §10.4）</summary>
        [StringLength(20)]
        public string Status { get; set; } = "running";

        /// <summary>用的提示词类型：audit_prep / audit_check</summary>
        [StringLength(50)]
        public string? PromptType { get; set; }

        /// <summary>提示词模板 Code（可追溯）</summary>
        [StringLength(50)]
        public string? PromptCode { get; set; }

        /// <summary>提示词版本号</summary>
        [StringLength(20)]
        public string? PromptVersion { get; set; }

        /// <summary>模型名</summary>
        [StringLength(100)]
        public string? ModelName { get; set; }

        /// <summary>
        /// 输入证据指纹（判定"证据变没变"）。
        /// <para>⚠️ 只用于<b>过期告警</b>（<see cref="Status"/>=stale + 界面橙标），
        /// <b>不参与跳过判断</b> —— 跳过只看锁（46 号 §10.4）。</para>
        /// </summary>
        [StringLength(64)]
        public string? EvidenceFingerprint { get; set; }

        /// <summary>★ 装配包快照（条款 + 属性 + 摘录 + 现场证据），回放用</summary>
        public string? InputSnapshotJson { get; set; }

        /// <summary>产出摘要（几条结果 / 几条疑点 / 状态分布）</summary>
        public string? ResultSummaryJson { get; set; }

        /// <summary>分析完成时间</summary>
        public DateTime? AnalyzedAt { get; set; }

        /// <summary>耗时（毫秒）</summary>
        public int? DurationMs { get; set; }

        /// <summary>输入 token</summary>
        public int? InputTokenCount { get; set; }

        /// <summary>输出 token</summary>
        public int? OutputTokenCount { get; set; }

        /// <summary>失败原因（Status=failed 时必填）</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>排序号</summary>
        public int Sort { get; set; }

        /// <summary>备注</summary>
        [MaxLength(500)]
        public string? Remark { get; set; }

        // ──── 接口字段（BaseEntity 不包含，由接口继承提供） ────

        /// <summary>有效标志（1=启用，0=停用）</summary>
        public int IsValid { get; set; } = 1;

        /// <summary>软删除标记</summary>
        public bool IsDeleted { get; set; }

        /// <summary>删除人 Code</summary>
        public string? DeleteBy { get; set; }

        /// <summary>删除时间</summary>
        public DateTime? DeleteTime { get; set; }
    }
}

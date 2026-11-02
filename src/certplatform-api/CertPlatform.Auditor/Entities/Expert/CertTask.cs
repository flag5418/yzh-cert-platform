using System;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Auditor.Entities.Expert
{
    /// <summary>
    /// 认证任务头（V2 — 合并 prep/onsite + TaskKind；由计划派生）
    /// <para>表名：<c>cert_task</c></para>
    /// <para>一个计划可多次执行（反复检查），每次执行 = 一条任务。</para>
    /// <para>方向由 <see cref="TaskKind"/> 区分：prep=认证准备 / onsite=认证检查。</para>
    /// </summary>
    [SugarTable("cert_task")]
    public class CertTask : BaseEntity, ISoftDelete, IIsValid
    {
        /// <summary>展示号（P-20261030-01 / C-20261030-01，自动生成）</summary>
        [StringLength(50)]
        public string TaskNo { get; set; } = string.Empty;

        /// <summary>方向：prep=认证准备 / onsite=认证检查</summary>
        [StringLength(20)]
        public string TaskKind { get; set; } = "onsite";

        /// <summary>所属计划（关联 cert_audit_plan.Code）</summary>
        [StringLength(50)]
        public string PlanCode { get; set; } = string.Empty;

        /// <summary>企业编码（快照，便于列表筛选）</summary>
        [StringLength(50)]
        public string EnterpriseCode { get; set; } = string.Empty;

        /// <summary>阶段编码（快照）</summary>
        [StringLength(50)]
        public string? StageCode { get; set; }

        /// <summary>审核类型：initial / surveillance / recert / special（仅 onsite）</summary>
        [StringLength(20)]
        public string? AuditType { get; set; }

        /// <summary>审核开始日（仅 onsite）</summary>
        public DateTime? AuditDateFrom { get; set; }

        /// <summary>审核截止日（仅 onsite）</summary>
        public DateTime? AuditDateTo { get; set; }

        /// <summary>审核组（JSON 数组：{code, name, role}；仅 onsite）</summary>
        public string? TeamJson { get; set; }

        /// <summary>pending / running / paused / done / partial / failed</summary>
        [StringLength(20)]
        public string Status { get; set; } = "pending";

        /// <summary>检查项总数（进度口径）</summary>
        public int ItemTotal { get; set; }

        /// <summary>已完成项数</summary>
        public int ItemDone { get; set; }

        /// <summary>失败项数</summary>
        public int ItemFailed { get; set; }

        /// <summary>
        /// 跳过项数（★ 2026-11-01 · 46 号 §10）。
        /// <para>两个来源：① 入队前已被锁定（闸门①）② 执行时发现被锁定（闸门②）。</para>
        /// <para>⛔ 跳过 ≠ 失败：不计 <see cref="ItemFailed"/>、不重试、不进 NC，界面上不标红。</para>
        /// </summary>
        public int ItemSkipped { get; set; }

        /// <summary>本次提示词模板 Code（结果可追溯）</summary>
        [StringLength(50)]
        public string? PromptCode { get; set; }

        /// <summary>提示词版本号</summary>
        [StringLength(20)]
        public string? PromptVersion { get; set; }

        /// <summary>启动前校验快照（计划唯一/有条款/有部门/属性覆盖/证据源）</summary>
        public string? GuardJson { get; set; }

        /// <summary>汇总消息（⛔ 不许静默跳过）</summary>
        [StringLength(500)]
        public string? Message { get; set; }

        /// <summary>
        /// 本次执行投递的队列（关联 <c>yzh_queue.QueueCode</c>）。
        /// <para>用途：暂停要 <c>CancelQueueAsync(queueCode)</c>、页面锁要查队列进度、排查要能反查。</para>
        /// <para>⚠️ 重跑会覆盖成本次的 —— 历史队列靠 <see cref="QueueAttempt"/> + <c>yzh_queue.SourceId</c> 反查。</para>
        /// </summary>
        [StringLength(50)]
        public string? QueueCode { get; set; }

        /// <summary>第几次入队（重跑 +1；<see cref="QueueCode"/> 只留最后一次）</summary>
        public int QueueAttempt { get; set; }

        /// <summary>启动时间</summary>
        public DateTime? StartedAt { get; set; }

        /// <summary>完成时间</summary>
        public DateTime? FinishedAt { get; set; }

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

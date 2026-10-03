using System;
using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Auditor.Entities.Expert
{
    /// <summary>
    /// 专家任务队列头（专家任务系统 A 组）
    /// <para>表名：<c>cert_expert_task_queue</c></para>
    ///
    /// <para><b>★ 为什么不复用框架的 <c>yzh_queue</c>（D10）</b>：</para>
    /// <list type="number">
    ///   <item><c>yzh_queue</c> 的读侧（<c>QueueManager.GetQueueListAsync</c> 等）<b>无 OrgCode 过滤</b>
    ///         ⇒ 跨租户泄露（评审 H12）。</item>
    ///   <item><c>yzh_queue.ScopeKey</c> 是<b>双口径</b>（file_convert 存 ConfigCode / doc_extract 存 EnterpriseCode），
    ///         表达不了「1 子任务 = 1 队列」的单一互斥键。</item>
    /// </list>
    /// <para>⇒ 本表 <see cref="ScopeKey"/> <b>单一口径 = SubTaskCode</b>。</para>
    ///
    /// <para><b>唯一键 <c>uk_subtask(SubTaskCode)</c></b> 从结构上保证「1 子任务只有 1 个队列」，
    /// 是 D36 之外的第二道并发防线。</para>
    ///
    /// <para>调度机制（领取 / 租约 / 退避重试）<b>照搬框架已验证的实现</b>
    /// （<c>FOR UPDATE SKIP LOCKED</c> + 10 分钟租约 + 指数退避），但读写走本表 + 强制 OrgCode 过滤。</para>
    /// </summary>
    [SugarTable("cert_expert_task_queue")]
    public class CertExpertTaskQueue : BaseEntity, ISoftDelete, IIsValid
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

        /// <summary>所属任务编码</summary>
        [SugarColumn(Length = 36)]
        public string TaskCode { get; set; } = string.Empty;

        /// <summary>所属标准子任务编码（<c>cert_expert_task_standard.Code</c>）</summary>
        [SugarColumn(Length = 36)]
        public string SubTaskCode { get; set; } = string.Empty;

        /// <summary>标准编码</summary>
        [SugarColumn(Length = 36)]
        public string StandardCode { get; set; } = string.Empty;

        /// <summary>队列类型：<c>nc_check</c> | <c>report_generate</c></summary>
        [SugarColumn(Length = 20)]
        public string QueueType { get; set; } = string.Empty;

        /// <summary>★互斥键，<b>单一口径 = SubTaskCode</b></summary>
        [SugarColumn(Length = 100)]
        public string ScopeKey { get; set; } = string.Empty;

        // ──── 状态 ────

        /// <summary>队列状态：pending | running | completed | failed | cancelled</summary>
        [SugarColumn(Length = 20)]
        public string QueueStatus { get; set; } = "pending";

        // ──── 计数与进度 ────

        /// <summary>总项数</summary>
        public int TotalCount { get; set; }

        /// <summary>已完成数</summary>
        public int DoneCount { get; set; }

        /// <summary>失败数</summary>
        public int FailedCount { get; set; }

        /// <summary>跳过数</summary>
        public int SkippedCount { get; set; }

        /// <summary>进度 %</summary>
        [SugarColumn(DecimalDigits = 2, ColumnDataType = "decimal(5,2)")]
        public decimal Progress { get; set; }

        // ──── 调度 ────

        /// <summary>优先级（越大越先）</summary>
        public int Priority { get; set; }

        /// <summary>当前持有者（worker 标识）</summary>
        [SugarColumn(Length = 50, IsNullable = true)]
        public string? LockCode { get; set; }

        /// <summary>锁租约到期</summary>
        public DateTime? LockedUntil { get; set; }

        /// <summary>队列级重试次数</summary>
        public int RetryCount { get; set; }

        /// <summary>队列级最大重试</summary>
        public int MaxRetryCount { get; set; } = 3;

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

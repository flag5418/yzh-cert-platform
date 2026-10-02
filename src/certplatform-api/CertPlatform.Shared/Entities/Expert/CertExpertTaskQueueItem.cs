using System;
using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Expert
{
    /// <summary>
    /// 专家任务队列项（专家任务系统 A 组）—— <b>1 任务项 = 1 队列项</b>
    /// <para>表名：<c>cert_expert_task_queue_item</c></para>
    ///
    /// <para><b>唯一键 <c>uk_queue_item(QueueCode, TaskItemCode)</c></b> ⇒ 同一队列内同一任务项只入队一次
    /// （「局部更新 / 重跑」不会产生重复项）。</para>
    ///
    /// <para><b>★ <see cref="TaskItemCode"/> 是「实体层行 Code」</b>
    /// （<c>cert_expert_nc_item.Code</c> / <c>cert_expert_report_section_item.Code</c>），
    /// <b>⛔ 不是结果行 Code</b> —— 实体层是长期实体，结果层是多轮，
    /// 队列指向实体才保证「同一检查项跑第 N 轮」不会新建实体。</para>
    ///
    /// <para><b>★ <see cref="ExtractVersionStamp"/>（版本护栏）</b>：入队时记录企业资料的提取版本戳，
    /// 执行时比对 —— 若中途文件被重新上传/重新提取，则本轮判定基于旧数据，必须失败重跑而不是写脏结论。</para>
    /// </summary>
    [SugarTable("cert_expert_task_queue_item")]
    public class CertExpertTaskQueueItem : BaseEntity, ISoftDelete, IIsValid
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

        /// <summary>所属队列编码</summary>
        [SugarColumn(Length = 36)]
        public string QueueCode { get; set; } = string.Empty;

        /// <summary>所属任务编码（冗余，免 JOIN）</summary>
        [SugarColumn(Length = 36)]
        public string TaskCode { get; set; } = string.Empty;

        /// <summary>★任务项编码 = 实体层行 Code（<b>⛔ 不是结果行 Code</b>）</summary>
        [SugarColumn(Length = 36)]
        public string TaskItemCode { get; set; } = string.Empty;

        /// <summary>项类型：<c>nc_check</c> | <c>report_section</c></summary>
        [SugarColumn(Length = 20)]
        public string ItemType { get; set; } = string.Empty;

        /// <summary>规则/章节业务键（冗余，免 JOIN 定位）</summary>
        [SugarColumn(Length = 36)]
        public string ItemCode { get; set; } = string.Empty;

        /// <summary>执行顺序（同标准内串行）</summary>
        public int Seq { get; set; }

        // ──── 载荷与版本护栏 ────

        /// <summary>★执行载荷（规则 Code / 工作流 Code / 企业 / 标准 / 阶段 / 版本戳）</summary>
        [SugarColumn(ColumnDataType = "json", IsNullable = true)]
        public string? Payload { get; set; }

        /// <summary>★提取结果版本戳（入队时记录，执行时比对，防文件中途变更）</summary>
        [SugarColumn(Length = 64, IsNullable = true)]
        public string? ExtractVersionStamp { get; set; }

        // ──── 状态与重试 ────

        /// <summary>队列项状态：pending | running | completed | failed | skipped | cancelled</summary>
        [SugarColumn(Length = 20)]
        public string ItemStatus { get; set; } = "pending";

        /// <summary>错误分类：retryable | permanent</summary>
        [SugarColumn(Length = 20, IsNullable = true)]
        public string? ErrorType { get; set; }

        /// <summary>错误摘要</summary>
        [SugarColumn(Length = 2000, IsNullable = true)]
        public string? ErrorMessage { get; set; }

        /// <summary>已重试次数</summary>
        public int RetryCount { get; set; }

        /// <summary>最大重试次数</summary>
        public int MaxRetryCount { get; set; } = 3;

        /// <summary>下次重试时间（指数退避）</summary>
        public DateTime? NextRetryAt { get; set; }

        // ──── 调度锁 ────

        /// <summary>当前持有者（worker 标识）</summary>
        [SugarColumn(Length = 50, IsNullable = true)]
        public string? LockCode { get; set; }

        /// <summary>锁租约到期（10 分钟 &gt; 单项 LLM 超时上限）</summary>
        public DateTime? LockedUntil { get; set; }

        // ──── 溯源 ────

        /// <summary>★关联的执行任务（<c>wf_execution_task.Code</c>），执行后回填，可下钻引擎细节</summary>
        [SugarColumn(Length = 36, IsNullable = true)]
        public string? WorkflowExecTaskCode { get; set; }

        /// <summary>开始时间</summary>
        public DateTime? StartTime { get; set; }

        /// <summary>结束时间</summary>
        public DateTime? FinishTime { get; set; }

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

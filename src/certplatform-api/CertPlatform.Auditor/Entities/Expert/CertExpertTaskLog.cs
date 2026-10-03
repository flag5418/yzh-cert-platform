using System;
using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Auditor.Entities.Expert
{
    /// <summary>
    /// 专家任务日志（专家任务系统 C 组）—— <b>★ 不可变：只允许 INSERT / SELECT，⛔ 禁止 UPDATE / DELETE</b>
    /// <para>表名：<c>cert_expert_task_log</c></para>
    ///
    /// <para><b>为什么单独一张表而不是复用通用操作日志</b>：任务系统的核心痛点是
    /// 「<b>跑完了但结果不对</b>」—— 必须能回答：哪一步、哪一轮、哪个队列项、什么耗时、
    /// 用了哪个版本的数据、当时的状态是什么。这些字段通用日志表都没有。</para>
    ///
    /// <para><b>埋点分层（23 号 §四）</b>：</para>
    /// <list type="bullet">
    ///   <item>任务级：<c>task.created</c> / <c>task.lock.acquired</c> / <c>task.submitted</c> /
    ///         <c>task.progress</c> / <c>task.finished</c> / <c>task.cancelled</c></item>
    ///   <item>队列级：<c>queue.created</c> / <c>queue.started</c> / <c>queue.progress</c> / <c>queue.finished</c></item>
    ///   <item>项级：<c>item.extract.ok</c> / <c>item.extract.retry</c> / <c>item.extract.fail</c> /
    ///         <c>item.judge.ok</c> / <c>item.judge.nc</c> / <c>item.skip</c> / <c>gap.created</c></item>
    ///   <item>人工级：<c>ACKNOWLEDGE</c> / <c>MODIFY</c> / <c>SKIP</c> / <c>EXPORT</c> …</item>
    /// </list>
    ///
    /// <para><b>★ <see cref="CreateTime"/> = <see cref="OperateTime"/></b>（冗余两列）：
    /// 前者由 BaseEntity 自动填，后者是业务语义字段且 DDL 为 NOT NULL，
    /// 冗余是为了「按列排序」与「按业务语义查询」都能走索引。</para>
    ///
    /// <para><b>⛔ 本表不参与软删除</b>：<see cref="IsDeleted"/> 恒为 <c>false</c>，
    /// <c>UpdateBy/UpdateTime/DeleteBy/DeleteTime</c> 恒为 NULL。</para>
    /// </summary>
    [SugarTable("cert_expert_task_log")]
    public class CertExpertTaskLog : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / 审计字段由 BaseEntity 提供（★ UpdateBy/UpdateTime 恒 NULL） ────

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

        // ──── 归属（哪一层） ────

        /// <summary>所属任务编码</summary>
        [SugarColumn(Length = 36, IsNullable = true)]
        public string? TaskCode { get; set; }

        /// <summary>所属标准子任务编码</summary>
        [SugarColumn(Length = 36, IsNullable = true)]
        public string? SubTaskCode { get; set; }

        /// <summary>★所属队列编码（23 号 §四 埋点需要）</summary>
        [SugarColumn(Length = 36, IsNullable = true)]
        public string? QueueCode { get; set; }

        /// <summary>★任务项编码；NULL = 任务级动作</summary>
        [SugarColumn(Length = 36, IsNullable = true)]
        public string? TaskItemCode { get; set; }

        /// <summary><c>nc_check</c> | <c>report_section</c> | NULL</summary>
        [SugarColumn(Length = 20, IsNullable = true)]
        public string? ItemType { get; set; }

        /// <summary>标准编码（冗余）</summary>
        [SugarColumn(Length = 36, IsNullable = true)]
        public string? StandardCode { get; set; }

        /// <summary>项名称（冗余快照）</summary>
        [SugarColumn(Length = 200, IsNullable = true)]
        public string? ItemName { get; set; }

        // ──── 动作与级别 ────

        /// <summary>
        /// 动作（如 <c>task.created</c> / <c>queue.started</c> / <c>item.judge.nc</c> /
        /// <c>ACKNOWLEDGE</c> / <c>MODIFY</c> / <c>EXPORT</c>）。完整值域见 DDL 注释。
        /// </summary>
        [SugarColumn(Length = 40)]
        public string LogAction { get; set; } = string.Empty;

        /// <summary>★日志级别（23 号 §四）：<c>info</c> | <c>warn</c> | <c>error</c></summary>
        [SugarColumn(Length = 10)]
        public string LogLevel { get; set; } = "info";

        // ──── 字段级变更 ────

        /// <summary>改的字段名（MODIFY 逐字段）</summary>
        [SugarColumn(Length = 50, IsNullable = true)]
        public string? FieldName { get; set; }

        /// <summary>字段中文名</summary>
        [SugarColumn(Length = 100, IsNullable = true)]
        public string? FieldLabel { get; set; }

        /// <summary>旧值（标量/短文本）</summary>
        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string? OldValue { get; set; }

        /// <summary>新值（标量/短文本）</summary>
        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string? NewValue { get; set; }

        /// <summary>★旧值（长文本，章节正文用）</summary>
        [SugarColumn(ColumnDataType = "longtext", IsNullable = true)]
        public string? OldValueText { get; set; }

        /// <summary>★新值（长文本，章节正文用）</summary>
        [SugarColumn(ColumnDataType = "longtext", IsNullable = true)]
        public string? NewValueText { get; set; }

        // ──── 状态迁移与上下文 ────

        /// <summary>改前 ReviewStatus</summary>
        [SugarColumn(Length = 20, IsNullable = true)]
        public string? BeforeStatus { get; set; }

        /// <summary>改后 ReviewStatus</summary>
        [SugarColumn(Length = 20, IsNullable = true)]
        public string? AfterStatus { get; set; }

        /// <summary>跳过分类（LogAction=SKIP 时）</summary>
        [SugarColumn(Length = 30, IsNullable = true)]
        public string? SkipCategory { get; set; }

        /// <summary>AI 置信度（记录自动结果时）</summary>
        [SugarColumn(DecimalDigits = 2, ColumnDataType = "decimal(3,2)", IsNullable = true)]
        public decimal? Confidence { get; set; }

        /// <summary>★人读消息（23 号 §四）</summary>
        [SugarColumn(Length = 2000, IsNullable = true)]
        public string? Message { get; set; }

        /// <summary>★结构化载荷（关键入参，⛔ 不存文件内容）</summary>
        [SugarColumn(ColumnDataType = "json", IsNullable = true)]
        public string? Payload { get; set; }

        /// <summary>★耗时毫秒（任务/队列/项级均记录）</summary>
        public int? DurationMs { get; set; }

        // ──── 操作人 ────

        /// <summary>针对的是否自动生成的结果</summary>
        public bool IsAutoResult { get; set; }

        /// <summary>操作人 Code</summary>
        [SugarColumn(Length = 50, IsNullable = true)]
        public string? OperatorCode { get; set; }

        /// <summary>★操作人姓名（冗余）</summary>
        [SugarColumn(Length = 100, IsNullable = true)]
        public string? OperatorName { get; set; }

        /// <summary>操作时间（★ NOT NULL）</summary>
        public DateTime OperateTime { get; set; } = DateTime.Now;

        /// <summary>客户端 IP（可选）</summary>
        [SugarColumn(Length = 50, IsNullable = true)]
        public string? ClientIp { get; set; }

        // ──── ISoftDelete / IIsValid ────

        /// <summary>软删除标记（★ 恒为 false —— 日志不删）</summary>
        public bool IsDeleted { get; set; }

        /// <summary>删除人 Code（★ 恒 NULL）</summary>
        public string? DeleteBy { get; set; }

        /// <summary>删除时间（★ 恒 NULL）</summary>
        public DateTime? DeleteTime { get; set; }

        /// <summary>有效标志（1=有效，0=无效）</summary>
        public int IsValid { get; set; } = 1;
    }
}

using System;
using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Auditor.Entities.Expert
{
    /// <summary>
    /// 报告章节结果 —— <b>多轮</b>（专家任务系统 B 组 · 结果域）
    /// <para>表名：<c>cert_expert_report_result</c></para>
    ///
    /// <para>与 <see cref="CertExpertNcResult"/> 同构：唯一键 <c>uk_item_round(ItemCode, RoundNo)</c>，
    /// 重跑追加新轮次。</para>
    ///
    /// <para><b>★ <see cref="ContentFormat"/> 恒为 <c>plain</c>（D19：不用富文本）</b>。
    /// 前端渲染 <see cref="ContentText"/> 时<b>必须先转义</b> —— 章节正文可能包含
    /// AI 输出的 <c>&lt;script&gt;</c> 或表格标记。</para>
    ///
    /// <para><b>★ <see cref="InheritFromCode"/>（沿用机制，D30）</b>：本轮沿用了哪一轮的结果。
    /// 非空即表示「本轮没重新生成」，界面须明确标注，避免专家以为是新结论。</para>
    /// </summary>
    [SugarTable("cert_expert_report_result")]
    public class CertExpertReportResult : BaseEntity, ISoftDelete, IIsValid
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

        /// <summary>★所属章节实体（<c>cert_expert_report_section_item.Code</c>）</summary>
        [SugarColumn(Length = 36)]
        public string ItemCode { get; set; } = string.Empty;

        /// <summary>企业编码</summary>
        [SugarColumn(Length = 36)]
        public string EnterpriseCode { get; set; } = string.Empty;

        /// <summary>阶段编码</summary>
        [SugarColumn(Length = 36)]
        public string StageCode { get; set; } = string.Empty;

        /// <summary>标准编码</summary>
        [SugarColumn(Length = 36)]
        public string StandardCode { get; set; } = string.Empty;

        /// <summary>★业务轮次</summary>
        public int RoundNo { get; set; } = 1;

        /// <summary>★产生本轮的任务；<b>沿用轮次为空</b></summary>
        [SugarColumn(Length = 36, IsNullable = true)]
        public string? TaskCode { get; set; }

        // ──── 机器产出（Auto* 组） ────

        /// <summary>自动结果：<c>none</c> | <c>ok</c> | <c>skipped</c> | <c>failed</c> | <c>degraded</c>（章节未配 DAG，用模板示例）</summary>
        [SugarColumn(Length = 20)]
        public string AutoStatus { get; set; } = "none";

        /// <summary>业务侧结论快照</summary>
        [SugarColumn(ColumnDataType = "json", IsNullable = true)]
        public string? AutoResult { get; set; }

        /// <summary>★AI 生成的章节正文</summary>
        [SugarColumn(ColumnDataType = "longtext", IsNullable = true)]
        public string? AutoContent { get; set; }

        /// <summary>AI 置信度（&lt;0.5 时界面黄色提示）</summary>
        [SugarColumn(DecimalDigits = 2, ColumnDataType = "decimal(3,2)", IsNullable = true)]
        public decimal? AutoConfidence { get; set; }

        /// <summary>★引擎执行任务（可下钻）</summary>
        [SugarColumn(Length = 36, IsNullable = true)]
        public string? ExecutionTaskCode { get; set; }

        /// <summary>自动判定时间</summary>
        public DateTime? AutoEvaluatedAt { get; set; }

        /// <summary>跳过分类</summary>
        [SugarColumn(Length = 30, IsNullable = true)]
        public string? SkipCategory { get; set; }

        /// <summary>跳过原因</summary>
        [SugarColumn(Length = 1000, IsNullable = true)]
        public string? SkipReason { get; set; }

        // ──── 专家结论（机器永不写） ────

        /// <summary>★章节正文（纯文本，★前端 HTML 渲染须先转义）</summary>
        [SugarColumn(ColumnDataType = "longtext", IsNullable = true)]
        public string? ContentText { get; set; }

        /// <summary>正文格式：<c>plain</c>（D19，不用富文本）</summary>
        [SugarColumn(Length = 20, IsNullable = true)]
        public string? ContentFormat { get; set; } = "plain";

        /// <summary>★本轮沿用了哪一轮（沿用机制，D30）</summary>
        [SugarColumn(Length = 36, IsNullable = true)]
        public string? InheritFromCode { get; set; }

        // ──── 复核 ────

        /// <summary>★not_started | pending_review | reviewed | modified | skipped | frozen</summary>
        [SugarColumn(Length = 20)]
        public string ReviewStatus { get; set; } = "not_started";

        /// <summary>★是否被专家改过</summary>
        public bool IsModified { get; set; }

        /// <summary>复核人 Code</summary>
        [SugarColumn(Length = 50, IsNullable = true)]
        public string? ReviewBy { get; set; }

        /// <summary>复核人姓名（冗余）</summary>
        [SugarColumn(Length = 100, IsNullable = true)]
        public string? ReviewName { get; set; }

        /// <summary>复核时间</summary>
        public DateTime? ReviewTime { get; set; }

        /// <summary>复核备注</summary>
        [SugarColumn(Length = 500, IsNullable = true)]
        public string? ReviewRemark { get; set; }

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

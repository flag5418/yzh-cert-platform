using System;
using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Auditor.Entities.Expert
{
    /// <summary>
    /// 报告章节 —— <b>企业级长期实体</b>（专家任务系统 B 组 · 结果域）
    /// <para>表名：<c>cert_expert_report_section_item</c></para>
    ///
    /// <para>与 <see cref="CertExpertNcItem"/> <b>同构</b>（实体层长期 + 结果层多轮）。
    /// 唯一键 <c>uk_scope_sec(OrgCode, EnterpriseCode, StageCode, StandardCode, SectionCode)</c>。</para>
    ///
    /// <para><b>★ <see cref="SectionTemplateContent"/>（模板示例正文快照，只读）</b>：
    /// 配置层章节可以带示例正文，专家编辑时<b>对照用</b>。
    /// ⛔ 它是<b>只读参照</b>，不是初值 —— 把它当作 AI 生成的初稿会让专家误以为系统已产出内容。</para>
    /// </summary>
    [SugarTable("cert_expert_report_section_item")]
    public class CertExpertReportSectionItem : BaseEntity, ISoftDelete, IIsValid
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

        /// <summary>排序号（SortOrder 快照）</summary>
        public int Sort { get; set; }

        /// <summary>备注</summary>
        [SugarColumn(Length = 500, IsNullable = true)]
        public string? Remark { get; set; }

        // ──── 作用域（唯一键四元组） ────

        /// <summary>企业编码</summary>
        [SugarColumn(Length = 36)]
        public string EnterpriseCode { get; set; } = string.Empty;

        /// <summary>阶段编码（GUID）</summary>
        [SugarColumn(Length = 36)]
        public string StageCode { get; set; } = string.Empty;

        /// <summary>标准编码</summary>
        [SugarColumn(Length = 36)]
        public string StandardCode { get; set; } = string.Empty;

        // ──── 章节快照 ────

        /// <summary>★配置层章节（<c>cert_report_section.Code</c>）</summary>
        [SugarColumn(Length = 36)]
        public string SectionCode { get; set; } = string.Empty;

        /// <summary>章节名称（快照）</summary>
        [SugarColumn(Length = 200)]
        public string SectionName { get; set; } = string.Empty;

        /// <summary>章节名称英文（快照）</summary>
        [SugarColumn(Length = 200, IsNullable = true)]
        public string? SectionNameEn { get; set; }

        /// <summary>★模板示例正文快照（★专家编辑时对照用，<b>只读</b>）</summary>
        [SugarColumn(ColumnDataType = "longtext", IsNullable = true)]
        public string? SectionTemplateContent { get; set; }

        /// <summary>★条款编码（<b>可空</b> —— 概述/结论章节不映射条款）</summary>
        [SugarColumn(Length = 36, IsNullable = true)]
        public string? ClauseCode { get; set; }

        /// <summary>条款号（快照）</summary>
        [SugarColumn(Length = 50, IsNullable = true)]
        public string? ClauseNumber { get; set; }

        /// <summary>条款标题（快照）</summary>
        [SugarColumn(Length = 200, IsNullable = true)]
        public string? ClauseTitle { get; set; }

        /// <summary>工作流编码</summary>
        [SugarColumn(Length = 36, IsNullable = true)]
        public string? WorkflowCode { get; set; }

        /// <summary>★规则版本</summary>
        public int RuleVersion { get; set; } = 1;

        /// <summary>★DAG 指纹</summary>
        [SugarColumn(Length = 64, IsNullable = true)]
        public string? RuleContextHash { get; set; }

        // ──── 跨轮次状态 ────

        /// <summary>★当前生效结果（<c>cert_expert_report_result.Code</c>）</summary>
        [SugarColumn(Length = 36, IsNullable = true)]
        public string? CurrentResultCode { get; set; }

        /// <summary>★累计结果轮次数</summary>
        public int RoundCount { get; set; }

        /// <summary>★上次检查时间（沿用不更新，D30）</summary>
        public DateTime? LastAuditedTime { get; set; }

        /// <summary>上次检查的任务</summary>
        [SugarColumn(Length = 36, IsNullable = true)]
        public string? LastAuditedTaskCode { get; set; }

        /// <summary>上次专家认可/修改时间</summary>
        public DateTime? LastReviewTime { get; set; }

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

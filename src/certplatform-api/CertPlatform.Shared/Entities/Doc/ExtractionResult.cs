using System;
using YZH.Entity.Admin.Platform;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Doc
{
    /// <summary>
    /// 文档字段提取结果（字段级）
    /// <para>表名：cert_extraction_result（原 ent_extraction_result，按命名约定 ent→cert）</para>
    /// <para>归属：管理端「文档提取规则」——SaveExtractionRuleAsync 落库、GetRuleDetailAsync 回显</para>
    /// </summary>
    [SugarTable("cert_extraction_result")]
    public class ExtractionResult : BaseEntity
    {
        // ──── Id / 审计字段由 BaseEntity 基类统一提供 ────

        // ──── 业务字段 ────
        /// <summary>标准企业标识（多租户隔离，映射到 DB OrgCode 列）</summary>
        [Required, StringLength(36)]
        [SugarColumn(ColumnName = "OrgCode")]
        public string EnterpriseCode { get; set; }

        /// <summary>标准文件编码（规则键：实际文件 FileCode 或文件要求模板 Code，最长 200）</summary>
        [StringLength(200)]
        public string? StandardFileCode { get; set; }

        /// <summary>标准编码（冗余，关联 cert_iso_standard.Code）</summary>
        [StringLength(36)]
        public string? StandardCode { get; set; }

        /// <summary>阶段编码（冗余，方便过滤；决策 ⑩ 统一为 StageCode → cert_cert_stage.Code）</summary>
        [StringLength(36)]
        public string? StageCode { get; set; }

        [Required, StringLength(200)]
        [UniqueField("文件编码", WithFields = new[] { "EnterpriseCode", "FieldCode" })]
        public string FileCode { get; set; }

        [Required]
        public int VersionNumber { get; set; }

        /// <summary>提取规则编码（<c>cert_doc_extraction_rule.Code</c>）
        /// <para>★ 2026-09-30 起是<b>取数与清理的主键成分</b>（裁决 J4：1 文件 = 1 规则，
        /// 由 <c>uk_rule_scope(OrgCode,StandardCode,StageCode,StandardFileCode)</c> 唯一索引强制）。
        /// 补录写入的行走本列，与 <see cref="FileCode"/> 无关。</para>
        /// </summary>
        [Required, StringLength(200)]
        public string RuleCode { get; set; }

        [Required, StringLength(36)]
        public string FieldCode { get; set; }

        /// <summary>字段名称（中文名，展示用）</summary>
        [StringLength(200)]
        public string? FieldName { get; set; }

        public string? ExtractedValue { get; set; }

        public decimal? Confidence { get; set; }

        public string? PositionInfo { get; set; }

        /// <summary>是否被人工修改（布尔标志，取值与 <see cref="ValueSource"/> 保持一致）</summary>
        public bool IsManualEdited { get; set; } = false;

        /// <summary>值来源（★ 2026-09-30 落库，裁决 J2）
        /// <para><c>auto</c>=系统自动提取（内容提取规则产出）｜<c>manual</c>=人工录入（补录）</para>
        /// <para>★ 取数时<b>人工值优先</b>：同一 (OrgCode, RuleCode, FieldCode) 下
        /// <c>auto</c> 行与 <c>manual</c> 行并存，<c>ORDER BY (ValueSource='manual') DESC</c>。
        /// 两者都保留 ⇒ 提取时间线可完整还原（配合 <c>cert_extraction_change_log</c>）。</para>
        /// </summary>
        [Required, StringLength(20)]
        public string ValueSource { get; set; } = ExtractionValueSource.Auto;

        [Required]
        public DateTime ExtractedAt { get; set; }

        /// <summary>标签（与 FieldCode 相同值，兼容旧数据）</summary>
        [StringLength(500)]
        [SugarColumn(IsNullable = true)]
        public string? LabelTag { get; set; }

        /// <summary>软删除标记</summary>
        public bool IsDeleted { get; set; }

        /// <summary>
        /// 有效标志（DB 列 IsValid 本就存在，此前实体未声明 → ORM 自动过滤失效）。
        /// <para>企业域版本链（02 号 V-P1）：1=当前版本行，0=归档旧版本（**不物理删**）。</para>
        /// </summary>
        public int IsValid { get; set; } = 1;
    }
}

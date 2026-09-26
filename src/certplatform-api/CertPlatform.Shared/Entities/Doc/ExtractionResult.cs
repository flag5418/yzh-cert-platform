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

        /// <summary>阶段编码（冗余，方便过滤）</summary>
        [StringLength(36)]
        public string? PhaseCode { get; set; }

        [Required, StringLength(200)]
        [UniqueField("文件编码", WithFields = new[] { "EnterpriseCode", "FieldCode" })]
        public string FileCode { get; set; }

        [Required]
        public int VersionNumber { get; set; }

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

        public bool IsManualEdited { get; set; } = false;

        [Required]
        public DateTime ExtractedAt { get; set; }

        /// <summary>标签（与 FieldCode 相同值，兼容旧数据）</summary>
        [StringLength(500)]
        [SugarColumn(IsNullable = true)]
        public string? LabelTag { get; set; }

        /// <summary>软删除标记</summary>
        public bool IsDeleted { get; set; }
    }
}

using System;
using YZH.Entity.Admin.Platform;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Ent
{
    /// <summary>
    /// ExtractionResult 提取结果（字段级）
    /// <para>表名：ent_extraction_result</para>
    /// </summary>
    [SugarTable("ent_extraction_result")]
    public class ExtractionResult : BaseEntity
    {
        // ──── Id / 审计字段由 BaseEntity 基类统一提供 ────

        // ──── 业务字段 ────
        /// <summary>机构编码（多租户隔离，此表需要机构级数据隔离）</summary>
        [StringLength(50)]
        public string? OrgCode { get; set; }

        [Required, StringLength(36)]
        public string EnterpriseCode { get; set; }

        /// <summary>标准文件编码（规则键：实际文件 FileCode 或文件要求模板 Code，最长 200）</summary>
        [StringLength(200)]
        public string? StandardFileCode { get; set; }

        /// <summary>标准编码（冗余，关联 cert_iso_standard.code）</summary>
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
    }
}

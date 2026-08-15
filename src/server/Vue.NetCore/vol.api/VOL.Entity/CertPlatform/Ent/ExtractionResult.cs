using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Ent
{
    /// <summary>
    /// ExtractionResult 提取结果（字段级）
    /// <para>表名：ent_extraction_result</para>
    /// <para>核心关联：standard_file_code → cert_file_requirement.code</para>
    /// <para>冗余字段：org_code / standard_code / phase_code 方便过滤和数据提取</para>
    /// </summary>
    [Table("ent_extraction_result")]
    public class ExtractionResult : YZHBaseEntity
    {
        /// <summary>机构编码（多租户隔离，此表需要机构级数据隔离）</summary>
        [StringLength(50)]
        [Column("org_code")]
        public string OrgCode { get; set; }

        [Required, StringLength(36)]
        [Column("enterprise_code")]
        public string EnterpriseCode { get; set; }

        /// <summary>标准文件编码（关联 cert_file_requirement.code，核心枢纽）</summary>
        [StringLength(36)]
        [Column("standard_file_code")]
        public string StandardFileCode { get; set; }

        /// <summary>标准编码（冗余，关联 cert_iso_standard.code）</summary>
        [StringLength(36)]
        [Column("standard_code")]
        public string StandardCode { get; set; }

        /// <summary>阶段编码（冗余，方便过滤）</summary>
        [StringLength(36)]
        [Column("phase_code")]
        public string PhaseCode { get; set; }

        [Required, StringLength(36)]
        [Column("file_code")]
        public string FileCode { get; set; }

        [Required]
        [Column("version_number")]
        public int VersionNumber { get; set; }

        [Required, StringLength(36)]
        [Column("rule_code")]
        public string RuleCode { get; set; }

        [Required, StringLength(36)]
        [Column("field_code")]
        public string FieldCode { get; set; }

        [StringLength(500)]
        [Column("label_tag")]
        public string LabelTag { get; set; }

        [Column("extracted_value")]
        public string ExtractedValue { get; set; }

        [Column("confidence")]
        public decimal? Confidence { get; set; }

        [Column("position_info")]
        public string PositionInfo { get; set; }

        [Column("is_manual_edited")]
        public bool IsManualEdited { get; set; } = false;

        [Required]
        [Column("extracted_at")]
        public DateTime ExtractedAt { get; set; }

        // org_code 继承自 YZHBaseEntity
    }
}

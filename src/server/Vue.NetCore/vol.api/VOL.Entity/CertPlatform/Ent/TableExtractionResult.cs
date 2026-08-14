using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Ent
{
    /// <summary>
    /// TableExtractionResult 表格提取结果
    /// <para>表名：ent_table_extraction_result</para>
    /// <para>注意：此表仅有 create_date 审计字段，不继承完整审计字段</para>
    /// </summary>
    [Table("ent_table_extraction_result")]
    public class TableExtractionResult : YZHBaseEntity
    {
        [Required, StringLength(36)]
        [Column("enterprise_code")]
        public string EnterpriseCode { get; set; }

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

        [Column("table_index")]
        public int TableIndex { get; set; } = 1;

        [Required]
        [Column("extracted_json")]
        public string ExtractedJson { get; set; }

        [Column("confidence")]
        public decimal? Confidence { get; set; }

        [Column("position_info")]
        public string PositionInfo { get; set; }

        [Required]
        [Column("extracted_at")]
        public DateTime ExtractedAt { get; set; }
    }
}

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Rpt
{
    /// <summary>
    /// ReportSectionSource 报告章节来源
    /// <para>表名：rpt_report_section_source</para>
    /// <para>注意：此表仅有 create_date 审计字段</para>
    /// </summary>
    [Table("rpt_report_section_source")]
    public class ReportSectionSource : YZHBaseEntity
    {
        /// <summary>机构编码（多租户隔离，此表需要机构级数据隔离）</summary>
        [StringLength(50)]
        [Column("org_code")]
        public string OrgCode { get; set; }

        [Required, StringLength(36)]
        [Column("section_code")]
        public string SectionCode { get; set; }

        [Required, StringLength(20)]
        [Column("source_type")]
        public string SourceType { get; set; }

        [Required, StringLength(36)]
        [Column("source_code")]
        public string SourceCode { get; set; }

        [Column("source_summary")]
        public string SourceSummary { get; set; }

        [Column("sort_order")]
        public int SortOrder { get; set; } = 0;
    }
}

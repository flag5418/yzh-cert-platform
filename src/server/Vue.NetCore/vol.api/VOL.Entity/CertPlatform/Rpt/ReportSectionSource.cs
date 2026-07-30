using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Rpt
{
    /// <summary>
    /// ReportSectionSource
    /// <para>表名：rpt_report_section_source</para>
    /// </summary>
    [Table("rpt_report_section_source")]
    public class ReportSectionSource : BaseEntity
    {

    [Required][StringLength(36)][Column("section_code")]
    public string SectionCode { get; set; }
    [Required][Column("source_type")]
    public string SourceType { get; set; }
    [StringLength(36)][Column("source_code")]
    public string SourceCode { get; set; }
    [Column("source_description")]
    public string SourceDescription { get; set; }
    [Column("confidence")]
    public decimal? Confidence { get; set; }

    }
}

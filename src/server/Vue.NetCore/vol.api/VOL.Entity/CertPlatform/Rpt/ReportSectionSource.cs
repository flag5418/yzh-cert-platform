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
    public class ReportSectionSource : YZHBaseEntity
    {

    [Required][StringLength(36)]
    public string SectionCode { get; set; }
    [Required]
    public string SourceType { get; set; }
    [StringLength(36)]
    public string SourceCode { get; set; }
    
    public string SourceDescription { get; set; }
    
    public decimal? Confidence { get; set; }

    }
}

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Rpt
{
    /// <summary>
    /// ReportSection
    /// <para>表名：rpt_report_section</para>
    /// </summary>
    [Table("rpt_report_section")]
    public class ReportSection : YZHBaseEntity
    {

    [Required][StringLength(36)]
    public string ReportCode { get; set; }
    [StringLength(36)]
    public string ClauseCode { get; set; }
    [Required][StringLength(200)]
    public string SectionName { get; set; }
    
    public string SectionContent { get; set; }
    [StringLength(36)]
    public string WorkflowCode { get; set; }
    
    public int SortOrder { get; set; } = 0;

    }
}

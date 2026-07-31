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

    [Required][StringLength(36)][Column("report_code")]
    public string ReportCode { get; set; }
    [StringLength(36)][Column("clause_code")]
    public string ClauseCode { get; set; }
    [Required][StringLength(200)][Column("section_name")]
    public string SectionName { get; set; }
    [Column("section_content")]
    public string SectionContent { get; set; }
    [StringLength(36)][Column("workflow_code")]
    public string WorkflowCode { get; set; }
    [Column("sort_order")]
    public int SortOrder { get; set; } = 0;

    }
}

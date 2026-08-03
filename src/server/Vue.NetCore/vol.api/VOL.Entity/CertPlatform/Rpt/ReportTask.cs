using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Rpt
{
    /// <summary>
    /// ReportTask
    /// <para>表名：rpt_report_task</para>
    /// </summary>
    [Table("rpt_report_task")]
    public class ReportTask : YZHBaseEntity
    {

    [Required][StringLength(36)]
    public string PhaseCode { get; set; }
    [StringLength(36)]
    public string BasedOnAuditTaskCode { get; set; }
    [Required][StringLength(36)]
    public string TemplateCode { get; set; }
    [Required][StringLength(50)]
    public string TaskNumber { get; set; }
    
    public DateTime? GeneratedAt { get; set; }
    
    public DateTime? LockedAt { get; set; }
    
    public long? LockedBy { get; set; }

    }
}

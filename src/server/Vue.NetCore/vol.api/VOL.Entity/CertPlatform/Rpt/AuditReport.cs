using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Rpt
{
    /// <summary>
    /// AuditReport
    /// <para>表名：rpt_audit_report</para>
    /// </summary>
    [Table("rpt_audit_report")]
    public class AuditReport : YZHBaseEntity
    {

    [Required][StringLength(36)]
    public string TaskCode { get; set; }
    
    public int VersionNumber { get; set; } = 1;
    [Required][StringLength(500)]
    public string ReportTitle { get; set; }
    
    public string FullContent { get; set; }
    [StringLength(500)]
    public string ExportPath { get; set; }
    
    public long? EditedBy { get; set; }

    }
}

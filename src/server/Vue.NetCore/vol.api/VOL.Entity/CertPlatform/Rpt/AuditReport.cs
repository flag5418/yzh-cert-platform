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

    [Required][StringLength(36)][Column("task_code")]
    public string TaskCode { get; set; }
    [Column("version_number")]
    public int VersionNumber { get; set; } = 1;
    [Required][StringLength(500)][Column("report_title")]
    public string ReportTitle { get; set; }
    [Column("full_content")]
    public string FullContent { get; set; }
    [StringLength(500)][Column("export_path")]
    public string ExportPath { get; set; }
    [Column("edited_by")]
    public long? EditedBy { get; set; }

    }
}

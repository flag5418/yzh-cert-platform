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
    public class ReportTask : BaseEntity
    {

    [Required][StringLength(36)][Column("phase_code")]
    public string PhaseCode { get; set; }
    [StringLength(36)][Column("based_on_audit_task_code")]
    public string BasedOnAuditTaskCode { get; set; }
    [Required][StringLength(36)][Column("template_code")]
    public string TemplateCode { get; set; }
    [Required][StringLength(50)][Column("task_number")]
    public string TaskNumber { get; set; }
    [Column("status")]
    public string Status { get; set; } = "draft";
    [Column("generated_at")]
    public DateTime? GeneratedAt { get; set; }
    [Column("locked_at")]
    public DateTime? LockedAt { get; set; }
    [Column("locked_by")]
    public long? LockedBy { get; set; }

    }
}

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Audit
{
    /// <summary>
    /// AuditTask
    /// <para>表名：audit_task</para>
    /// </summary>
    [Table("audit_task")]
    public class AuditTask : YZHBaseEntity
    {

    [Required][StringLength(36)][Column("phase_code")]
    public string PhaseCode { get; set; }
    [Required][StringLength(50)][Column("task_number")]
    public string TaskNumber { get; set; }
    [Required][Column("auditor_id")]
    public long AuditorId { get; set; }
    [Column("status")]
    public string Status { get; set; } = "pending";
    [Column("planned_date")]
    public DateTime? PlannedDate { get; set; }
    [Column("actual_start_date")]
    public DateTime? ActualStartDate { get; set; }
    [Column("actual_complete_date")]
    public DateTime? ActualCompleteDate { get; set; }
    [Column("audit_scope")]
    public string AuditScope { get; set; }
    [Column("notes")]
    public string Notes { get; set; }

    }
}

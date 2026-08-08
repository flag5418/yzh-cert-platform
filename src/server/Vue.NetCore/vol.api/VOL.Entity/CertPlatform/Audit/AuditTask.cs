using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VOL.Entity;

namespace VOL.Entity.CertPlatform.Audit
{
    /// <summary>
    /// AuditTask
    /// <para>表名：audit_task</para>
    /// </summary>
    [Entity(TableCnName = "审计任务管理", TableName = "audit_task", DBServer = "VOLContext")]
    [Table("audit_task")]
    public class AuditTask : YZHBaseEntity
    {

    [Required][StringLength(36)]
    public string PhaseCode { get; set; }
    [Required][StringLength(50)]
    public string TaskNumber { get; set; }
    [Required]
    public long AuditorId { get; set; }
    
    public DateTime? PlannedDate { get; set; }
    
    public DateTime? ActualStartDate { get; set; }
    
    public DateTime? ActualCompleteDate { get; set; }
    
    public string AuditScope { get; set; }
    

    }
}

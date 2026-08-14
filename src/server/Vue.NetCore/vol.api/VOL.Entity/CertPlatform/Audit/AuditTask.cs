using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VOL.Entity;

namespace VOL.Entity.CertPlatform.Audit
{
    /// <summary>
    /// AuditTask 审核任务
    /// <para>表名：audit_task</para>
    /// </summary>
    [Entity(TableCnName = "审核任务", TableName = "audit_task", DBServer = "VOLContext")]
    [Table("audit_task")]
    public class AuditTask : YZHBaseEntity
    {
        [Required, StringLength(36)]
        [Column("phase_code")]
        public string PhaseCode { get; set; }

        [Required, StringLength(50)]
        [Column("task_number")]
        public string TaskNumber { get; set; }

        [Required]
        [Column("auditor_id")]
        public long AuditorId { get; set; }

        [Column("planned_date")]
        public DateTime? PlannedDate { get; set; }

        [Column("actual_start_date")]
        public DateTime? ActualStartDate { get; set; }

        [Column("actual_complete_date")]
        public DateTime? ActualCompleteDate { get; set; }

        [Column("audit_scope")]
        public string AuditScope { get; set; }

        // Status 继承自 YZHBaseEntity
    }
}

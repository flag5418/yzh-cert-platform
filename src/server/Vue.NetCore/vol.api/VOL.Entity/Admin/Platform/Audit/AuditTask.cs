using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VOL.Entity;
using VOL.Entity.Admin.Platform;

namespace VOL.Entity.Admin.Platform.Audit
{
    /// <summary>
    /// AuditTask 审核任务
    /// <para>表名：audit_task（列名为 snake_case，需覆盖审计字段）</para>
    /// </summary>
    [Entity(TableCnName = "审核任务", TableName = "audit_task", DBServer = "VOLContext")]
    [Table("audit_task")]
    public class AuditTask : YZHBaseEntity
    {
        // ===== snake_case 审计字段覆盖 =====
        [Column("create_id")] public new int? CreateID { get; set; }
        [Column("creator")] [MaxLength(50)] public new string Creator { get; set; }
        [Column("create_date")] public new DateTime? CreateDate { get; set; } = DateTime.Now;
        [Column("modify_id")] public new int? ModifyID { get; set; }
        [Column("modifier")] [MaxLength(50)] public new string Modifier { get; set; }
        [Column("modify_date")] public new DateTime? ModifyDate { get; set; }
        [Column("delete_id")] public new int? DeleteID { get; set; }
        [Column("deleter")] [MaxLength(50)] public new string Deleter { get; set; }
        [Column("delete_time")] public new DateTime? DeleteTime { get; set; }
        [Column("code")] public new string Code { get; set; } = Guid.NewGuid().ToString("N");
        [Column("status")] public new string Status { get; set; } = "active";
        [Column("enable")] public new bool Enable { get; set; } = true;
        [Column("sort")] public new int Sort { get; set; }

        /// <summary>机构编码（多租户隔离，此表需要机构级数据隔离）</summary>
        [StringLength(50)]
        [Column("org_code")]
        public string OrgCode { get; set; }

        [Required, StringLength(36)]
        [Column("phase_code")]
        public string PhaseCode { get; set; }

        [Required, StringLength(50)]
        [UniqueField("任务编号")]
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

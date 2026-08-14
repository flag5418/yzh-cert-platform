using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Audit
{
    /// <summary>
    /// NonConformity 不符合项
    /// <para>表名：audit_nonconformity</para>
    /// <para>注意：此表有 status + create_date + modify_date 审计字段</para>
    /// </summary>
    [Table("audit_nonconformity")]
    public class NonConformity : YZHBaseEntity
    {
        [Required, StringLength(36)]
        [Column("task_code")]
        public string TaskCode { get; set; }

        [Required, StringLength(36)]
        [Column("clause_code")]
        public string ClauseCode { get; set; }

        [Required, StringLength(50)]
        [Column("nc_number")]
        public string NcNumber { get; set; }

        [Required, StringLength(20)]
        [Column("severity")]
        public string Severity { get; set; }

        [Required]
        [Column("description")]
        public string Description { get; set; }

        [Column("requirement_ref")]
        public string RequirementRef { get; set; }

        [Column("evidence_ref")]
        public string EvidenceRef { get; set; }

        [StringLength(20)]
        [Column("source_type")]
        public string SourceType { get; set; } = "manual";

        [StringLength(36)]
        [Column("source_check_code")]
        public string SourceCheckCode { get; set; }

        [StringLength(36)]
        [Column("rule_code")]
        public string RuleCode { get; set; }

        [Column("due_date")]
        public DateTime? DueDate { get; set; }

        [Required]
        [Column("opened_by")]
        public int OpenedBy { get; set; }

        [Required]
        [Column("opened_at")]
        public DateTime OpenedAt { get; set; }

        [Column("closed_at")]
        public DateTime? ClosedAt { get; set; }
    }
}

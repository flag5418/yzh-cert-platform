using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Audit
{
    /// <summary>
    /// AuditEvidence 审核证据
    /// <para>表名：audit_evidence</para>
    /// <para>注意：此表仅有 create_date 审计字段</para>
    /// </summary>
    [Table("audit_evidence")]
    public class AuditEvidence : YZHBaseEntity
    {
        /// <summary>机构编码（多租户隔离，此表需要机构级数据隔离）</summary>
        [StringLength(50)]
        [Column("org_code")]
        public string OrgCode { get; set; }

        [Required, StringLength(36)]
        [Column("task_code")]
        public string TaskCode { get; set; }

        [StringLength(36)]
        [Column("clause_code")]
        public string ClauseCode { get; set; }

        [Required, StringLength(20)]
        [Column("evidence_type")]
        public string EvidenceType { get; set; }

        [Required, StringLength(500)]
        [Column("storage_path")]
        public string StoragePath { get; set; }

        [Required, StringLength(64)]
        [Column("file_hash")]
        public string FileHash { get; set; }

        [Column("is_voided")]
        public bool IsVoided { get; set; } = false;

        [Column("voided_at")]
        public DateTime? VoidedAt { get; set; }

        [Column("voided_by")]
        public int? VoidedBy { get; set; }

        [Column("captured_at")]
        public DateTime? CapturedAt { get; set; }

        [Required]
        [Column("captured_by")]
        public int CapturedBy { get; set; }
    }
}

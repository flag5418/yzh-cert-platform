using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Audit
{
    /// <summary>
    /// AuditEvidence
    /// <para>表名：audit_evidence</para>
    /// </summary>
    [Table("audit_evidence")]
    public class AuditEvidence : BaseEntity
    {

    [Required][StringLength(36)][Column("task_code")]
    public string TaskCode { get; set; }
    [StringLength(36)][Column("clause_code")]
    public string ClauseCode { get; set; }
    [Required][Column("evidence_type")]
    public string EvidenceType { get; set; }
    [Required][StringLength(500)][Column("storage_path")]
    public string StoragePath { get; set; }
    [Required][StringLength(64)][Column("file_hash")]
    public string FileHash { get; set; }
    [Column("is_voided")]
    public bool IsVoided { get; set; } = false;
    [Column("voided_at")]
    public DateTime? VoidedAt { get; set; }
    [Column("voided_by")]
    public long? VoidedBy { get; set; }
    [Column("captured_at")]
    public DateTime? CapturedAt { get; set; }
    [Required][Column("captured_by")]
    public long CapturedBy { get; set; }
    [Column("notes")]
    public string Notes { get; set; }

    }
}

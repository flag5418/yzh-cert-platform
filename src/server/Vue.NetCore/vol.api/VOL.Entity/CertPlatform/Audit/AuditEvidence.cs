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
    public class AuditEvidence : YZHBaseEntity
    {

    [Required][StringLength(36)]
    public string TaskCode { get; set; }
    [StringLength(36)]
    public string ClauseCode { get; set; }
    [Required]
    public string EvidenceType { get; set; }
    [Required][StringLength(500)]
    public string StoragePath { get; set; }
    [Required][StringLength(64)]
    public string FileHash { get; set; }
    
    public bool IsVoided { get; set; } = false;
    
    public DateTime? VoidedAt { get; set; }
    
    public long? VoidedBy { get; set; }
    
    public DateTime? CapturedAt { get; set; }
    [Required]
    public long CapturedBy { get; set; }
    
    public string Notes { get; set; }

    }
}

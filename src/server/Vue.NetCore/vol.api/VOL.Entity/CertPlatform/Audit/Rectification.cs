using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Audit
{
    /// <summary>
    /// Rectification
    /// <para>表名：audit_rectification</para>
    /// </summary>
    [Table("audit_rectification")]
    public class Rectification : YZHBaseEntity
    {

    [Required][StringLength(36)]
    public string NcCode { get; set; }
    [Required]
    public string Correction { get; set; }
    
    public string CorrectiveAction { get; set; }
    
    public string EvidenceFiles { get; set; }
    [Required]
    public long SubmittedBy { get; set; }
    [Required]
    public DateTime SubmittedAt { get; set; }
    
    public long? VerifiedBy { get; set; }
    
    public DateTime? VerifiedAt { get; set; }
    
    public string VerifyResult { get; set; }
    
    public string VerifyNotes { get; set; }

    }
}

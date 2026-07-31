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

    [Required][StringLength(36)][Column("nc_code")]
    public string NcCode { get; set; }
    [Required][Column("correction")]
    public string Correction { get; set; }
    [Column("corrective_action")]
    public string CorrectiveAction { get; set; }
    [Column("evidence_files")]
    public string EvidenceFiles { get; set; }
    [Required][Column("submitted_by")]
    public long SubmittedBy { get; set; }
    [Required][Column("submitted_at")]
    public DateTime SubmittedAt { get; set; }
    [Column("verified_by")]
    public long? VerifiedBy { get; set; }
    [Column("verified_at")]
    public DateTime? VerifiedAt { get; set; }
    [Column("verify_result")]
    public string VerifyResult { get; set; }
    [Column("verify_notes")]
    public string VerifyNotes { get; set; }

    }
}

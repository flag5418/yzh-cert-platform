using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Audit
{
    /// <summary>
    /// NonConformity
    /// <para>表名：audit_nonconformity</para>
    /// </summary>
    [Table("audit_nonconformity")]
    public class NonConformity : YZHBaseEntity
    {

    [Required][StringLength(36)]
    public string TaskCode { get; set; }
    [Required][StringLength(36)]
    public string ClauseCode { get; set; }
    [Required][StringLength(50)]
    public string NcNumber { get; set; }
    [Required]
    public string Severity { get; set; }
    [Required]
    public string Description { get; set; }
    
    public string RequirementRef { get; set; }
    
    public string EvidenceRef { get; set; }
    
    public string SourceType { get; set; } = "manual";
    [StringLength(36)]
    public string SourceCheckCode { get; set; }
    [StringLength(36)]
    public string RuleCode { get; set; }
    
    public DateTime? DueDate { get; set; }
    [Required]
    public long OpenedBy { get; set; }
    [Required]
    public DateTime OpenedAt { get; set; }
    
    public DateTime? ClosedAt { get; set; }

    }
}

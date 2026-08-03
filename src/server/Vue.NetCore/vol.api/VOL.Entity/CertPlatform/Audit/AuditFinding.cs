using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Audit
{
    /// <summary>
    /// AuditFinding
    /// <para>表名：audit_finding</para>
    /// </summary>
    [Table("audit_finding")]
    public class AuditFinding : YZHBaseEntity
    {

    [Required][StringLength(36)]
    public string ChecklistItemCode { get; set; }
    [StringLength(36)]
    public string NcCode { get; set; }
    [StringLength(36)]
    public string SourceFileCode { get; set; }
    [StringLength(200)]
    public string SourcePosition { get; set; }
    
    public string SourceContent { get; set; }
    [Required]
    public string FindingType { get; set; }
    [Required]
    public string Description { get; set; }
    
    public decimal? Confidence { get; set; }
    
    public bool IsManual { get; set; } = false;

    }
}

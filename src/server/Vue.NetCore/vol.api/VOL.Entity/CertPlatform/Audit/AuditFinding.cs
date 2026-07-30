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
    public class AuditFinding : BaseEntity
    {

    [Required][StringLength(36)][Column("checklist_item_code")]
    public string ChecklistItemCode { get; set; }
    [StringLength(36)][Column("nc_code")]
    public string NcCode { get; set; }
    [StringLength(36)][Column("source_file_code")]
    public string SourceFileCode { get; set; }
    [StringLength(200)][Column("source_position")]
    public string SourcePosition { get; set; }
    [Column("source_content")]
    public string SourceContent { get; set; }
    [Required][Column("finding_type")]
    public string FindingType { get; set; }
    [Required][Column("description")]
    public string Description { get; set; }
    [Column("confidence")]
    public decimal? Confidence { get; set; }
    [Column("is_manual")]
    public bool IsManual { get; set; } = false;

    }
}

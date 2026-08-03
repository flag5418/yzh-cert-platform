using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Audit
{
    /// <summary>
    /// ChecklistItem
    /// <para>表名：audit_checklist_item</para>
    /// </summary>
    [Table("audit_checklist_item")]
    public class ChecklistItem : YZHBaseEntity
    {

    [Required][StringLength(36)]
    public string TaskCode { get; set; }
    [Required][StringLength(36)]
    public string ClauseCode { get; set; }
    
    public string AuditCriteria { get; set; }
    
    public string FindingDescription { get; set; }
    
    public string Conformity { get; set; } = "pending";
    
    public int NcsFound { get; set; } = 0;
    
    public long? CheckedBy { get; set; }
    
    public DateTime? CheckedAt { get; set; }
    
    public int SortOrder { get; set; } = 0;

    }
}

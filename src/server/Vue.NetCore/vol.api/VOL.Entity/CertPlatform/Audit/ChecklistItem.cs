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

    [Required][StringLength(36)][Column("task_code")]
    public string TaskCode { get; set; }
    [Required][StringLength(36)][Column("clause_code")]
    public string ClauseCode { get; set; }
    [Column("audit_criteria")]
    public string AuditCriteria { get; set; }
    [Column("finding_description")]
    public string FindingDescription { get; set; }
    [Column("conformity")]
    public string Conformity { get; set; } = "pending";
    [Column("ncs_found")]
    public int NcsFound { get; set; } = 0;
    [Column("checked_by")]
    public long? CheckedBy { get; set; }
    [Column("checked_at")]
    public DateTime? CheckedAt { get; set; }
    [Column("sort_order")]
    public int SortOrder { get; set; } = 0;

    }
}

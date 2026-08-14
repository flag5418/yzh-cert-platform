using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Audit
{
    /// <summary>
    /// ChecklistItem 检查清单项
    /// <para>表名：audit_checklist_item</para>
    /// <para>注意：此表仅有 create_date + modify_date 审计字段</para>
    /// </summary>
    [Table("audit_checklist_item")]
    public class ChecklistItem : YZHBaseEntity
    {
        [Required, StringLength(36)]
        [Column("task_code")]
        public string TaskCode { get; set; }

        [Required, StringLength(36)]
        [Column("clause_code")]
        public string ClauseCode { get; set; }

        [Column("audit_criteria")]
        public string AuditCriteria { get; set; }

        [Column("finding_description")]
        public string FindingDescription { get; set; }

        [StringLength(20)]
        [Column("conformity")]
        public string Conformity { get; set; } = "pending";

        [Column("ncs_found")]
        public int NcsFound { get; set; } = 0;

        [Column("checked_by")]
        public int? CheckedBy { get; set; }

        [Column("checked_at")]
        public DateTime? CheckedAt { get; set; }

        [Column("sort_order")]
        public int SortOrder { get; set; } = 0;
    }
}

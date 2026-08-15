using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Audit
{
    /// <summary>
    /// AuditFinding 审核发现
    /// <para>表名：audit_finding</para>
    /// <para>注意：此表仅有 create_date 审计字段</para>
    /// </summary>
    [Table("audit_finding")]
    public class AuditFinding : YZHBaseEntity
    {
        /// <summary>机构编码（多租户隔离，此表需要机构级数据隔离）</summary>
        [StringLength(50)]
        [Column("org_code")]
        public string OrgCode { get; set; }

        [Required, StringLength(36)]
        [Column("checklist_item_code")]
        public string ChecklistItemCode { get; set; }

        [StringLength(36)]
        [Column("nc_code")]
        public string NcCode { get; set; }

        [StringLength(36)]
        [Column("source_file_code")]
        public string SourceFileCode { get; set; }

        [StringLength(200)]
        [Column("source_position")]
        public string SourcePosition { get; set; }

        [Column("source_content")]
        public string SourceContent { get; set; }

        [Required, StringLength(20)]
        [Column("finding_type")]
        public string FindingType { get; set; }

        [Required]
        [Column("description")]
        public string Description { get; set; }

        [Column("confidence")]
        public decimal? Confidence { get; set; }

        [Column("is_manual")]
        public bool IsManual { get; set; } = false;

        [Required]
        [Column("created_by")]
        public int CreatedBy { get; set; }
    }
}

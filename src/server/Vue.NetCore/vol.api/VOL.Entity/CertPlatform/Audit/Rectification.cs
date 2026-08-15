using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Audit
{
    /// <summary>
    /// Rectification 整改
    /// <para>表名：audit_rectification</para>
    /// <para>注意：此表仅有 create_date 审计字段</para>
    /// </summary>
    [Table("audit_rectification")]
    public class Rectification : YZHBaseEntity
    {
        /// <summary>机构编码（多租户隔离，此表需要机构级数据隔离）</summary>
        [StringLength(50)]
        [Column("org_code")]
        public string OrgCode { get; set; }

        [Required, StringLength(36)]
        [Column("nc_code")]
        public string NcCode { get; set; }

        [Required]
        [Column("correction")]
        public string Correction { get; set; }

        [Column("corrective_action")]
        public string CorrectiveAction { get; set; }

        [Column("evidence_files")]
        public string EvidenceFiles { get; set; }

        [Required]
        [Column("submitted_by")]
        public int SubmittedBy { get; set; }

        [Required]
        [Column("submitted_at")]
        public DateTime SubmittedAt { get; set; }

        [Column("verified_by")]
        public int? VerifiedBy { get; set; }

        [Column("verified_at")]
        public DateTime? VerifiedAt { get; set; }

        [StringLength(20)]
        [Column("verify_result")]
        public string VerifyResult { get; set; }

        [Column("verify_notes")]
        public string VerifyNotes { get; set; }
    }
}

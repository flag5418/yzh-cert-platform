using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Ent
{
    /// <summary>
    /// FileComplianceCheck 文件合规检查
    /// <para>表名：ent_file_compliance_check</para>
    /// <para>注意：此表仅有 create_date 审计字段，不继承完整审计字段</para>
    /// </summary>
    [Table("ent_file_compliance_check")]
    public class FileComplianceCheck : YZHBaseEntity
    {
        /// <summary>机构编码（多租户隔离，此表需要机构级数据隔离）</summary>
        [StringLength(50)]
        [Column("org_code")]
        public string OrgCode { get; set; }

        [Required, StringLength(36)]
        [Column("file_code")]
        public string FileCode { get; set; }

        [Required]
        [Column("version_number")]
        public int VersionNumber { get; set; }

        [Required, StringLength(36)]
        [Column("rule_code")]
        public string RuleCode { get; set; }

        [StringLength(36)]
        [Column("workflow_execution_code")]
        public string WorkflowExecutionCode { get; set; }

        [Required, StringLength(20)]
        [Column("check_status")]
        public string CheckStatus { get; set; }

        [Column("message")]
        public string Message { get; set; }

        [Column("detail")]
        public string Detail { get; set; }

        [Required]
        [Column("checked_at")]
        public DateTime CheckedAt { get; set; }
    }
}

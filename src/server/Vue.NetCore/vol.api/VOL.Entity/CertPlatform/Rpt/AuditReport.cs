using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Rpt
{
    /// <summary>
    /// AuditReport 审核报告
    /// <para>表名：rpt_audit_report</para>
    /// <para>注意：此表有 status + create_date + modify_date 审计字段</para>
    /// </summary>
    [Table("rpt_audit_report")]
    public class AuditReport : YZHBaseEntity
    {
        /// <summary>机构编码（多租户隔离，此表需要机构级数据隔离）</summary>
        [StringLength(50)]
        [Column("org_code")]
        public string OrgCode { get; set; }

        [Required, StringLength(36)]
        [Column("report_task_code")]
        public string ReportTaskCode { get; set; }

        [Required, StringLength(50)]
        [Column("report_number")]
        public string ReportNumber { get; set; }

        [StringLength(500)]
        [Column("file_path")]
        public string FilePath { get; set; }

        [Required]
        [Column("created_by")]
        public int CreatedBy { get; set; }
    }
}

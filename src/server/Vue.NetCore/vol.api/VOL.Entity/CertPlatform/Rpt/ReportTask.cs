using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Rpt
{
    /// <summary>
    /// ReportTask 报告任务
    /// <para>表名：rpt_report_task</para>
    /// <para>注意：此表有 status + create_id/creator/create_date + modify_date 审计字段</para>
    /// </summary>
    [Table("rpt_report_task")]
    public class ReportTask : YZHBaseEntity
    {
        /// <summary>机构编码（多租户隔离，此表需要机构级数据隔离）</summary>
        [StringLength(50)]
        [Column("org_code")]
        public string OrgCode { get; set; }

        [Required, StringLength(36)]
        [Column("phase_code")]
        public string PhaseCode { get; set; }

        [StringLength(36)]
        [Column("based_on_audit_task_code")]
        public string BasedOnAuditTaskCode { get; set; }

        [Required, StringLength(36)]
        [Column("template_code")]
        public string TemplateCode { get; set; }

        [Required, StringLength(50)]
        [Column("task_number")]
        public string TaskNumber { get; set; }

        [Column("started_at")]
        public DateTime? StartedAt { get; set; }

        [Column("completed_at")]
        public DateTime? CompletedAt { get; set; }
    }
}

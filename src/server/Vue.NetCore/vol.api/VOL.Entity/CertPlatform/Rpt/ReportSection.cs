using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Rpt
{
    /// <summary>
    /// ReportSection 报告章节
    /// <para>表名：rpt_report_section</para>
    /// <para>注意：此表仅有 create_date 审计字段</para>
    /// </summary>
    [Table("rpt_report_section")]
    public class ReportSection : YZHBaseEntity
    {
        /// <summary>机构编码（多租户隔离，此表需要机构级数据隔离）</summary>
        [StringLength(50)]
        [Column("org_code")]
        public string OrgCode { get; set; }

        [Required, StringLength(36)]
        [Column("report_code")]
        public string ReportCode { get; set; }

        [StringLength(36)]
        [Column("clause_code")]
        public string ClauseCode { get; set; }

        [StringLength(36)]
        [Column("workflow_code")]
        public string WorkflowCode { get; set; }

        [Required, StringLength(200)]
        [Column("section_name")]
        public string SectionName { get; set; }

        [Column("content")]
        public string Content { get; set; }

        [Column("sort_order")]
        public int SortOrder { get; set; } = 0;
    }
}

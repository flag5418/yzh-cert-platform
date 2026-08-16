using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Rpt
{
    /// <summary>
    /// ReportSection 报告章节
    /// <para>表名：rpt_report_section</para>
    /// </summary>
    [Table("rpt_report_section")]
    public class ReportSection : YZHBaseEntity
    {
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

        [StringLength(200)]
        [Column("section_name_en")]
        public string SectionNameEn { get; set; }

        [Column("section_json")]
        public string SectionJson { get; set; }

        [StringLength(500)]
        [Column("remark")]
        public string Remark { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("content")]
        public string Content { get; set; }

        [Column("sort_order")]
        public int SortOrder { get; set; } = 0;
    }
}

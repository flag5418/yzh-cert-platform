using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YZH.Entity.Admin.Platform.Rpt
{
    /// <summary>
    /// ReportSection 报告章节
    /// <para>表名：rpt_report_section（列名为 snake_case，需覆盖审计字段）</para>
    /// </summary>
    [Table("rpt_report_section")]
    public class ReportSection : EntityBase
    {
        // ===== snake_case 审计字段覆盖 =====
        [Column("create_by")] public new string CreateBy { get; set; }
        [Column("creator")] [MaxLength(50)] public new string Creator { get; set; }
        [Column("create_date")] public new DateTime? CreateDate { get; set; } = DateTime.Now;
        [Column("update_by")] public new string UpdateBy { get; set; }
        [Column("modifier")] [MaxLength(50)] public new string Modifier { get; set; }
        [Column("modify_date")] public new DateTime? ModifyDate { get; set; }
        [Column("delete_by")] public new string DeleteBy { get; set; }
        [Column("deleter")] [MaxLength(50)] public new string Deleter { get; set; }
        [Column("delete_time")] public new DateTime? DeleteTime { get; set; }
        [Column("code")] public new string Code { get; set; } = Guid.NewGuid().ToString("N");
        [Column("status")] public new string Status { get; set; } = "active";
        [Column("enable")] public new bool Enable { get; set; } = true;
        [Column("sort")] public new int Sort { get; set; }

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

        /// <summary>章节工作流 DAG JSON（图形化设计器导出的 workflow_config）</summary>
        [Column("workflow_config")]
        public string WorkflowConfig { get; set; }

        /// <summary>章节工作流布局 JSON（节点坐标）</summary>
        [Column("layout_json")]
        public string LayoutJson { get; set; }

        [Required, StringLength(200)]
        [UniqueField("章节名称", WithFields = new[] { "ReportCode" })]
        [Column("section_name")]
        public string SectionName { get; set; }

        [StringLength(200)]
        [Column("section_name_en")]
        public string SectionNameEn { get; set; }

        [Column("section_json")]
        public string SectionJson { get; set; }

        [StringLength(500)]
        [Column("remark")]
        public new string Remark { get; set; }  // 章节备注（覆盖基类）

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("content")]
        public string Content { get; set; }

        [Column("sort_order")]
        public int SortOrder { get; set; } = 0;
    }
}

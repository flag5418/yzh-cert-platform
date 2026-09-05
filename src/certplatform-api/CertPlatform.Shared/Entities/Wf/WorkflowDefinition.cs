using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using YZH.Entity.Admin.Platform;

namespace YZH.Entity.Admin.Platform.Wf
{
    /// <summary>
    /// WorkflowDefinition - 工作流定义
    /// <para>表名：wf_workflow_definition（列名为 snake_case）</para>
    /// </summary>
    [Table("wf_workflow_definition")]
    public class WorkflowDefinition : EntityBase
    {
        // ===== snake_case 审计字段覆盖 =====
        [Column("create_id")] public new int? CreateID { get; set; }
        [Column("creator")] [MaxLength(50)] public new string Creator { get; set; }
        [Column("create_date")] public new DateTime? CreateDate { get; set; } = DateTime.Now;
        [Column("modify_id")] public new int? ModifyID { get; set; }
        [Column("modifier")] [MaxLength(50)] public new string Modifier { get; set; }
        [Column("modify_date")] public new DateTime? ModifyDate { get; set; }
        [Column("delete_id")] public new int? DeleteID { get; set; }
        [Column("deleter")] [MaxLength(50)] public new string Deleter { get; set; }
        [Column("delete_time")] public new DateTime? DeleteTime { get; set; }
        [Column("code")] public new string Code { get; set; } = Guid.NewGuid().ToString("N");
        [Column("status")] public new string Status { get; set; } = "active";
        [Column("enable")] public new bool Enable { get; set; } = true;
        [Column("sort")] public new int Sort { get; set; }
        [Column("remark")] public new string Remark { get; set; }

        [Required][StringLength(100)][UniqueField("工作流编码")][Column("workflow_code")]
        public string WorkflowCode { get; set; }

        [Required][StringLength(200)]
        [Column("workflow_name")]
        public string WorkflowName { get; set; }

        [Required]
        [Column("workflow_type")]
        public string WorkflowType { get; set; }

        [Column("workflow_config")]
        public string WorkflowConfig { get; set; }

        [Column("version")] public int Version { get; set; } = 1;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("description")]
        public string Description { get; set; }
    }
}

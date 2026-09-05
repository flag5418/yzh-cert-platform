using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using YZH.Entity.Admin.Platform;

namespace YZH.Entity.Admin.Platform.Wf
{
    /// <summary>
    /// WorkflowDefinition - 工作流定义
    /// <para>表名：wf_workflow_definition（业务字段 PascalCase，审计字段 snake_case）</para>
    /// <para>视图名：v_workflow</para>
    /// </summary>
    [Entity(TableCnName = "工作流定义", TableName = "wf_workflow_definition", ViewName = "v_workflow", DBServer = "VOLContext")]
    [Table("wf_workflow_definition")]
    public class WorkflowDefinition : EntityBase
    {
        // ===== 业务字段（DB 列名为 PascalCase：WorkflowCode/WorkflowName/WorkflowType/WorkflowConfig）=====
        [Required][StringLength(100)][UniqueField("工作流编码")]
        public string WorkflowCode { get; set; }

        [Required][StringLength(200)]
        public string WorkflowName { get; set; }

        [Required]
        public string WorkflowType { get; set; }

        public string WorkflowConfig { get; set; }

        [Column("version")] public int Version { get; set; } = 1;

        public bool IsActive { get; set; } = true;

        public string Description { get; set; }

        // ===== 视图扩展字段 =====
        [NotMapped] public string WorkflowTypeName { get; set; }
        [NotMapped] public string IsActiveName { get; set; }
        [NotMapped] public string StatusName { get; set; }
    }
}

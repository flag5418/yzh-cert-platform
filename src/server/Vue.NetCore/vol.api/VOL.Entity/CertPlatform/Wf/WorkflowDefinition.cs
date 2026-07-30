using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Wf
{
    /// <summary>
    /// WorkflowDefinition
    /// <para>表名：wf_workflow_definition</para>
    /// </summary>
    [Table("wf_workflow_definition")]
    public class WorkflowDefinition : BaseEntity
    {

    [Required][StringLength(100)][Column("workflow_code")]
    public string WorkflowCode { get; set; }
    [Required][StringLength(200)][Column("workflow_name")]
    public string WorkflowName { get; set; }
    [Required][Column("workflow_type")]
    public string WorkflowType { get; set; }
    [Required][Column("workflow_config")]
    public string WorkflowConfig { get; set; }
    [Column("version")]
    public int Version { get; set; } = 1;
    [Column("is_active")]
    public bool IsActive { get; set; } = true;
    [Column("description")]
    public string Description { get; set; }

    }
}

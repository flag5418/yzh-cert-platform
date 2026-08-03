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
    public class WorkflowDefinition : YZHBaseEntity
    {

    [Required][StringLength(100)]
    public string WorkflowCode { get; set; }
    [Required][StringLength(200)]
    public string WorkflowName { get; set; }
    [Required]
    public string WorkflowType { get; set; }
    [Required]
    public string WorkflowConfig { get; set; }
    
    public int Version { get; set; } = 1;
    
    public bool IsActive { get; set; } = true;
    
    public string Description { get; set; }

    }
}

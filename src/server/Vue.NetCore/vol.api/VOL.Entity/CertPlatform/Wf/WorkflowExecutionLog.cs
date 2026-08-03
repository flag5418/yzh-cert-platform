using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Wf
{
    /// <summary>
    /// WorkflowExecutionLog
    /// <para>表名：wf_workflow_execution_log</para>
    /// </summary>
    [Table("wf_workflow_execution_log")]
    public class WorkflowExecutionLog : YZHBaseEntity
    {

    [Required][StringLength(36)]
    public string WorkflowCode { get; set; }
    [Required]
    public int WorkflowVersion { get; set; }
    [Required]
    public string BusinessType { get; set; }
    [Required]
    public long BusinessId { get; set; }
    [Required][StringLength(50)]
    public string NodeId { get; set; }
    [Required][StringLength(100)]
    public string SkillCode { get; set; }
    
    public string InputData { get; set; }
    
    public string OutputData { get; set; }
    
    public string ErrorMsg { get; set; }
    
    public int? DurationMs { get; set; }
    [Required]
    public DateTime StartedAt { get; set; }
    
    public DateTime? CompletedAt { get; set; }

    }
}

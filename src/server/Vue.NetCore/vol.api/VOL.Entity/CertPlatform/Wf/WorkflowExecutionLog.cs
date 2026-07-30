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
    public class WorkflowExecutionLog : BaseEntity
    {

    [Required][StringLength(36)][Column("workflow_code")]
    public string WorkflowCode { get; set; }
    [Required][Column("workflow_version")]
    public int WorkflowVersion { get; set; }
    [Required][Column("business_type")]
    public string BusinessType { get; set; }
    [Required][Column("business_id")]
    public long BusinessId { get; set; }
    [Required][StringLength(50)][Column("node_id")]
    public string NodeId { get; set; }
    [Required][StringLength(100)][Column("skill_code")]
    public string SkillCode { get; set; }
    [Column("input_data")]
    public string InputData { get; set; }
    [Column("output_data")]
    public string OutputData { get; set; }
    [Required][Column("status")]
    public string Status { get; set; }
    [Column("error_msg")]
    public string ErrorMsg { get; set; }
    [Column("duration_ms")]
    public int? DurationMs { get; set; }
    [Required][Column("started_at")]
    public DateTime StartedAt { get; set; }
    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }

    }
}

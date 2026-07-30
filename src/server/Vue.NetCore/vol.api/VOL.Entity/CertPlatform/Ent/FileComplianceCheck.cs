using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Ent
{
    /// <summary>
    /// FileComplianceCheck
    /// <para>表名：ent_file_compliance_check</para>
    /// </summary>
    [Table("ent_file_compliance_check")]
    public class FileComplianceCheck : BaseEntity
    {

    [Required][StringLength(36)][Column("file_code")]
    public string FileCode { get; set; }
    [Required][Column("version_number")]
    public int VersionNumber { get; set; }
    [Required][StringLength(36)][Column("rule_code")]
    public string RuleCode { get; set; }
    [StringLength(36)][Column("workflow_execution_code")]
    public string WorkflowExecutionCode { get; set; }
    [Required][Column("check_status")]
    public string CheckStatus { get; set; }
    [Column("message")]
    public string Message { get; set; }
    [Column("detail")]
    public string Detail { get; set; }
    [Required][Column("checked_at")]
    public DateTime CheckedAt { get; set; }

    }
}

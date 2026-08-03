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
    public class FileComplianceCheck : YZHBaseEntity
    {

    [Required][StringLength(36)]
    public string FileCode { get; set; }
    [Required]
    public int VersionNumber { get; set; }
    [Required][StringLength(36)]
    public string RuleCode { get; set; }
    [StringLength(36)]
    public string WorkflowExecutionCode { get; set; }
    [Required]
    public string CheckStatus { get; set; }
    
    public string Message { get; set; }
    
    public string Detail { get; set; }
    [Required]
    public DateTime CheckedAt { get; set; }

    }
}

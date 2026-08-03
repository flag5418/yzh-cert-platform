using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Cert
{
    /// <summary>
    /// ValidationRule
    /// <para>表名：cert_validation_rule</para>
    /// </summary>
    [Table("cert_validation_rule")]
    public class ValidationRule : YZHBaseEntity
    {

    [Required][StringLength(36)]
    public string StandardCode { get; set; }
    [Required][StringLength(36)]
    public string PhaseCode { get; set; }
    [Required][StringLength(36)]
    public string ClauseCode { get; set; }
    [Required][StringLength(36)]
    public string WorkflowCode { get; set; }
    [Required][StringLength(50)]
    public string RuleCode { get; set; }
    [Required][StringLength(200)]
    public string RuleName { get; set; }
    [Required]
    public string SeverityIfViolated { get; set; }
    
    public string NcDescriptionTemplate { get; set; }
    
    public bool IsActive { get; set; } = true;

    }
}

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Cert
{
    /// <summary>
    /// ValidationRuleSource
    /// <para>表名：cert_validation_rule_source</para>
    /// </summary>
    [Table("cert_validation_rule_source")]
    public class ValidationRuleSource : YZHBaseEntity
    {

    [Required][StringLength(36)]
    public string RuleCode { get; set; }
    [Required][StringLength(36)]
    public string FileRequirementCode { get; set; }
    [StringLength(500)]
    public string SourcePath { get; set; }
    
    public string Notes { get; set; }

    }
}

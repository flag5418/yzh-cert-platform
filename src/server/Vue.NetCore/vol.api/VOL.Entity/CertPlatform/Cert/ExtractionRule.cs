using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Cert
{
    /// <summary>
    /// ExtractionRule
    /// <para>表名：cert_extraction_rule</para>
    /// </summary>
    [Table("cert_extraction_rule")]
    public class ExtractionRule : YZHBaseEntity
    {

    [Required][StringLength(36)]
    public string FileRequirementCode { get; set; }
    [Required][StringLength(36)]
    public string SkillCode { get; set; }
    [Required]
    public string RuleType { get; set; }
    [Required]
    public string RuleConfig { get; set; }
    
    public string Description { get; set; }
    
    public bool IsActive { get; set; } = true;

    }
}

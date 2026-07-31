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

    [Required][StringLength(36)][Column("file_requirement_code")]
    public string FileRequirementCode { get; set; }
    [Required][StringLength(36)][Column("skill_code")]
    public string SkillCode { get; set; }
    [Required][Column("rule_type")]
    public string RuleType { get; set; }
    [Required][Column("rule_config")]
    public string RuleConfig { get; set; }
    [Column("description")]
    public string Description { get; set; }
    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    }
}

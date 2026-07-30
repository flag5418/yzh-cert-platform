using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Wf
{
    /// <summary>
    /// Skill
    /// <para>表名：wf_skill</para>
    /// </summary>
    [Table("wf_skill")]
    public class Skill : BaseEntity
    {

    [Required][StringLength(100)][Column("skill_code")]
    public string SkillCode { get; set; }
    [Required][StringLength(200)][Column("skill_name")]
    public string SkillName { get; set; }
    [Required][Column("skill_type")]
    public string SkillType { get; set; }
    [Column("input_schema")]
    public string InputSchema { get; set; }
    [Column("output_schema")]
    public string OutputSchema { get; set; }
    [Column("endpoint_config")]
    public string EndpointConfig { get; set; }
    [Column("description")]
    public string Description { get; set; }
    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    }
}

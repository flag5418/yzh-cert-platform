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
    public class Skill : YZHBaseEntity
    {

    [Required][StringLength(100)]
    public string SkillCode { get; set; }
    [Required][StringLength(200)]
    public string SkillName { get; set; }
    [Required]
    public string SkillType { get; set; }
    
    public string InputSchema { get; set; }
    
    public string OutputSchema { get; set; }
    
    public string EndpointConfig { get; set; }
    
    public string Description { get; set; }
    
    public bool IsActive { get; set; } = true;

    }
}

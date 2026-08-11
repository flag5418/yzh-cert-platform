using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Wf
{
    [Table("wf_prompt_template")]
    public class PromptTemplate : YZHBaseEntity
    {
        [Required][StringLength(100)][Column("prompt_code")]
        public string PromptCode { get; set; }

        [Required][StringLength(200)][Column("prompt_name")]
        public string PromptName { get; set; }

        [Required][StringLength(50)][Column("prompt_type")]
        public string PromptType { get; set; }

        [StringLength(50)][Column("skill_target")]
        public string? SkillTarget { get; set; }

        [Column("template")]
        public string? Template { get; set; }

        [Column("description")]
        public string? Description { get; set; }

        [Column("version")]
        public int Version { get; set; } = 1;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("last_test_result")]
        public string? LastTestResult { get; set; }
    }
}

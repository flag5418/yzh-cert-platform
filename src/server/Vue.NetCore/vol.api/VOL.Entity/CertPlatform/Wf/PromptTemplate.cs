using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Wf
{
    [Table("wf_prompt_template")]
    public class PromptTemplate : YZHBaseEntity
    {
        /// <summary>覆盖基类审计字段，适配snake_case列名</summary>
        [Column("create_id")] public new int? CreateID { get; set; }
        [Column("creator")]   [MaxLength(50)] public new string Creator { get; set; }
        [Column("create_date")] public new DateTime? CreateDate { get; set; } = DateTime.Now;
        [Column("modify_id")] public new int? ModifyID { get; set; }
        [Column("modifier")]  [MaxLength(50)] public new string Modifier { get; set; }
        [Column("modify_date")] public new DateTime? ModifyDate { get; set; }
        [Column("delete_id")] public new int? DeleteID { get; set; }
        [Column("deleter")]   [MaxLength(50)] public new string Deleter { get; set; }
        [Column("delete_time")] public new DateTime? DeleteTime { get; set; }
        /// <summary>覆盖基类字段，适配snake_case列名</summary>
        [Column("code")]        public new string Code { get; set; }
        [Column("org_code")]    public string OrgCode { get; set; }
        [Column("status")]      public new string Status { get; set; }
        [Column("enable")]      public new bool Enable { get; set; }
        [Column("sort")]        public new int Sort { get; set; }
        [Column("remark")]      public new string Remark { get; set; }

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

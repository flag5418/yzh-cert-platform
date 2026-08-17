using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Wf
{
    /// <summary>
    /// WfSkillOutput — 强约束 Skill 输出契约（自定义工作流引擎 V1.2 §5.4）
    /// <para>表名：wf_skill_output</para>
    /// <para>output_strict=1 的 Skill 解释器按此表强校验输出端口</para>
    /// </summary>
    [Table("wf_skill_output")]
    public class WfSkillOutput : YZHBaseEntity
    {
        // ===== snake_case 审计字段覆盖 =====
        [Column("create_id")] public new int? CreateID { get; set; }
        [Column("creator")] [MaxLength(50)] public new string Creator { get; set; }
        [Column("create_date")] public new DateTime? CreateDate { get; set; } = DateTime.Now;
        [Column("modify_id")] public new int? ModifyID { get; set; }
        [Column("modifier")] [MaxLength(50)] public new string Modifier { get; set; }
        [Column("modify_date")] public new DateTime? ModifyDate { get; set; }
        [Column("delete_id")] public new int? DeleteID { get; set; }
        [Column("deleter")] [MaxLength(50)] public new string Deleter { get; set; }
        [Column("delete_time")] public new DateTime? DeleteTime { get; set; }
        [Column("code")] public new string Code { get; set; } = Guid.NewGuid().ToString("N");
        [Column("status")] public new string Status { get; set; }
        [Column("enable")] public new bool Enable { get; set; } = true;
        [Column("remark")] public new string Remark { get; set; }

        [Required][StringLength(100)][Column("skill_code")]
        public string SkillCode { get; set; }

        [Required][StringLength(100)][Column("output_name")]
        public string OutputName { get; set; }

        /// <summary>string / number / date / boolean / json</summary>
        [StringLength(20)][Column("output_type")]
        public string OutputType { get; set; } = "json";

        [Column("output_prompt")]
        public string OutputPrompt { get; set; }

        [StringLength(500)][Column("description")]
        public string Description { get; set; }

        [Column("sort_order")]
        public int SortOrder { get; set; }
    }
}

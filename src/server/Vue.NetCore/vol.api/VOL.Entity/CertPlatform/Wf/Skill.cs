using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Wf
{
    /// <summary>
    /// Skill — 工作流节点技能（自定义工作流引擎 V1.3 §5.2）
    /// <para>表名：wf_skill</para>
    /// <para>skill_type：固定为 method（反射执行）；api 型已废弃，第三方 API 调用封装在方法内部</para>
    /// <para>category：data_access / data_process / ai_judge / ai_generate / output</para>
    /// <para>output_strict：1=强约束（按 wf_skill_output 校验） 0=弱约束（ai_node 放行）</para>
    /// <para>V1.3：input_schema/output_schema/endpoint_config 三个旧列已删除</para>
    /// </summary>
    [Table("wf_skill")]
    public class Skill : YZHBaseEntity
    {
        // ===== snake_case 审计字段覆盖（2026-08-12 修复模式，PromptTemplate 同款）=====
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

        [Required][StringLength(200)][Column("skill_name")]
        public string SkillName { get; set; }

        [Required][StringLength(20)][Column("skill_type")]
        public string SkillType { get; set; } = "method";

        [StringLength(50)][Column("category")]
        public string Category { get; set; } = "data_process";

        [Column("side_effect")]
        public bool SideEffect { get; set; }

        [Column("description")]
        public string Description { get; set; }

        [Column("skill_prompt")]
        public string SkillPrompt { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("output_strict")]
        public bool OutputStrict { get; set; } = true;

        [StringLength(20)][Column("return_type")]
        public string ReturnType { get; set; } = "json";

        [StringLength(20)][Column("version")]
        public string Version { get; set; } = "1.0";

        [StringLength(50)][Column("icon")]
        public string Icon { get; set; }

        [StringLength(20)][Column("color")]
        public string Color { get; set; }

        [Column("sort_order")]
        public int SortOrder { get; set; }
    }
}

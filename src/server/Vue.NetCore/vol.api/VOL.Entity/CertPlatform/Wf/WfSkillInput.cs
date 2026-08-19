using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Wf
{
    /// <summary>
    /// WfSkillInput — Skill 输入表单模板（自定义工作流引擎 V1.2 §5.3）
    /// <para>表名：wf_skill_input</para>
    /// <para>作用：画布生成输入表单用，非硬校验；节点实例 inputs 是运行时真相</para>
    /// </summary>
    [Table("wf_skill_input")]
    public class WfSkillInput : YZHBaseEntity
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

        [Required][StringLength(100)][Column("input_name")]
        public string InputName { get; set; }

        [StringLength(200)][Column("input_label")]
        public string InputLabel { get; set; }

        /// <summary>text / number / date / boolean / enum / field_ref / table_ref / json</summary>
        [StringLength(20)][Column("input_type")]
        public string InputType { get; set; } = "text";

        /// <summary>绑定模式：Link / LinkOrConstant / Enum</summary>
        [StringLength(20)][Column("bind_mode")]
        public string BindMode { get; set; } = "LinkOrConstant";

        /// <summary>字典编码（BindMode=Enum 时必填），对应 Sys_Dictionary.DicNo</summary>
        [StringLength(100)][Column("enum_source")]
        public string EnumSource { get; set; }

        [Column("is_required")]
        public bool IsRequired { get; set; }

        [StringLength(500)][Column("default_value")]
        public string DefaultValue { get; set; }

        [Column("sort_order")]
        public int SortOrder { get; set; }
    }
}

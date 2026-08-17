using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Wf
{
    /// <summary>
    /// WfSkillReflection — method 型 Skill 反射信息（自定义工作流引擎 V1.2 §5.5，1:1）
    /// <para>表名：wf_skill_reflection</para>
    /// </summary>
    [Table("wf_skill_reflection")]
    public class WfSkillReflection : YZHBaseEntity
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

        /// <summary>反射的地址（类型全名，ReflectionSkillLoader 按此加载）</summary>
        [Required][StringLength(500)][Column("class_path")]
        public string ClassPath { get; set; }

        /// <summary>反射的方法（默认 ExecuteAsync）</summary>
        [StringLength(200)][Column("method_name")]
        public string MethodName { get; set; } = "ExecuteAsync";

        /// <summary>参数绑定 JSON：{"输入项名":"方法参数名或顺序"}</summary>
        [Column("param_binding")]
        public string ParamBinding { get; set; }
    }
}

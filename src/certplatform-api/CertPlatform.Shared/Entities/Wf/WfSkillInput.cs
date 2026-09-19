using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Entity.Admin.Platform;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Wf
{
    /// <summary>
    /// WfSkillInput — Skill 输入表单模板（自定义工作流引擎 V1.2 §5.3）
    /// <para>表名：wf_skill_input</para>
    /// <para>作用：画布生成输入表单用，非硬校验；节点实例 inputs 是运行时真相</para>
    ///
    /// 命名规范（YZH 铁律）：DB 列名 = C# 属性名 = PascalCase
    /// </summary>
    [SugarTable("wf_skill_input")]
    public class WfSkillInput : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / 审计字段由 BaseEntity 基类统一提供 ────
        // ──── ISoftDelete / IIsValid 接口字段由接口提供 ────

        // ──── 业务字段 ────
        [Required]
        [StringLength(100)]
        [UniqueField("技能编码", WithFields = new[] { "InputName" })]
        public string SkillCode { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string InputName { get; set; } = string.Empty;

        [StringLength(200)]
        public string? InputLabel { get; set; }

        /// <summary>text / number / date / boolean / enum / field_ref / table_ref / json</summary>
        [StringLength(20)]
        public string InputType { get; set; } = "text";

        /// <summary>绑定模式：Link / LinkOrConstant / Enum</summary>
        [StringLength(20)]
        public string BindMode { get; set; } = "LinkOrConstant";

        /// <summary>字典编码（BindMode=Enum 时必填）</summary>
        [StringLength(100)]
        public string? EnumSource { get; set; }

        public bool IsRequired { get; set; }

        [StringLength(500)]
        public string? DefaultValue { get; set; }

        public int SortOrder { get; set; }

        // ──── ISoftDelete + IIsValid 接口显式实现 ────
        public bool IsDeleted { get; set; }
        public string? DeleteBy { get; set; }
        public DateTime? DeleteTime { get; set; }
        public int IsValid { get; set; } = 1;
    }
}

using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Entity.Admin.Platform;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Wf
{
    /// <summary>
    /// WfSkillOutput — 强约束 Skill 输出契约（自定义工作流引擎 V1.2 §5.4）
    /// <para>表名：wf_skill_output</para>
    /// <para>output_strict=1 的 Skill 解释器按此表强校验输出端口</para>
    ///
    /// 命名规范（YZH 铁律）：DB 列名 = C# 属性名 = PascalCase
    /// </summary>
    [SugarTable("wf_skill_output")]
    public class WfSkillOutput : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / 审计字段由 BaseEntity 基类统一提供 ────
        // ──── ISoftDelete / IIsValid 接口字段由接口提供 ────

        // ──── 业务字段 ────
        [Required]
        [StringLength(100)]
        [UniqueField("技能编码", WithFields = new[] { "OutputName" })]
        public string SkillCode { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string OutputName { get; set; } = string.Empty;

        /// <summary>string / number / date / boolean / json</summary>
        [StringLength(20)]
        public string OutputType { get; set; } = "json";

        public string? OutputPrompt { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        public int SortOrder { get; set; }

        // ──── ISoftDelete + IIsValid 接口显式实现 ────
        public bool IsDeleted { get; set; }
        public string? DeleteBy { get; set; }
        public DateTime? DeleteTime { get; set; }
        public int IsValid { get; set; } = 1;
    }
}

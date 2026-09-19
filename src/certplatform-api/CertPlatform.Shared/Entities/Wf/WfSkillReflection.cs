using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Entity.Admin.Platform;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Wf
{
    /// <summary>
    /// WfSkillReflection — method 型 Skill 反射信息（自定义工作流引擎 V1.2 §5.5，1:1）
    /// <para>表名：wf_skill_reflection</para>
    ///
    /// 命名规范（YZH 铁律）：DB 列名 = C# 属性名 = PascalCase
    /// </summary>
    [SugarTable("wf_skill_reflection")]
    public class WfSkillReflection : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / 审计字段由 BaseEntity 基类统一提供 ────
        // ──── ISoftDelete / IIsValid 接口字段由接口提供 ────

        // ──── 业务字段 ────
        [Required]
        [StringLength(100)]
        [UniqueField("技能编码")]
        public string SkillCode { get; set; } = string.Empty;

        /// <summary>反射的地址（类型全名，ReflectionSkillLoader 按此加载）</summary>
        [Required]
        [StringLength(500)]
        [UniqueField("类路径", WithFields = new[] { "MethodName" })]
        public string ClassPath { get; set; } = string.Empty;

        /// <summary>反射的方法（默认 ExecuteAsync）</summary>
        [StringLength(200)]
        public string MethodName { get; set; } = "ExecuteAsync";

        /// <summary>参数绑定 JSON：{"输入项名":"方法参数名或顺序"}</summary>
        public string? ParamBinding { get; set; }

        // ──── ISoftDelete + IIsValid 接口显式实现 ────
        public bool IsDeleted { get; set; }
        public string? DeleteBy { get; set; }
        public DateTime? DeleteTime { get; set; }
        public int IsValid { get; set; } = 1;
    }
}

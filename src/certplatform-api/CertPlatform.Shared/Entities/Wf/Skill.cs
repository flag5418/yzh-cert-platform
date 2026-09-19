using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Attributes;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Wf
{
    /// <summary>
    /// 技能实体 - 用于工作流技能管理
    /// 继承 BaseEntity，映射 wf_skill 表
    ///
    /// 命名规范（YZH 铁律）：
    ///   DB 列名 = C# 属性名 = PascalCase
    ///   审计字段和接口字段从 BaseEntity + ISoftDelete/IIsValid 继承，禁止重复声明
    /// </summary>
    [SugarTable("wf_skill")]
    [YZHDeleteStrategy(Mode = DeleteMode.Soft)]
    public class Skill : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / 审计字段由 BaseEntity 基类统一提供 ────
        // ──── ISoftDelete / IIsValid 接口字段由接口提供 ────

        // === 业务字段 ===

        /// <summary>技能编码（DB: SkillCode）</summary>
        [StringLength(100)]
        public string SkillCode { get; set; } = string.Empty;

        /// <summary>技能名称（DB: Name）</summary>
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        /// <summary>技能类型（DB: SkillType，如 method/api/llm）</summary>
        [StringLength(50)]
        public string SkillType { get; set; } = "manual";

        /// <summary>分类编码（DB: CategoryCode，关联 wf_skill_category.Code）</summary>
        [StringLength(64)]
        public string? CategoryCode { get; set; }

        /// <summary>是否有副作用（DB: SideEffect）</summary>
        public bool SideEffect { get; set; } = false;

        /// <summary>描述（DB: Description）</summary>
        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string? Description { get; set; }

        /// <summary>Prompt模板（DB: PromptTemplate）</summary>
        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string? PromptTemplate { get; set; }

        /// <summary>是否激活（DB: IsActive）</summary>
        public bool IsActive { get; set; } = true;

        /// <summary>输出严格模式（DB: OutputStrict）</summary>
        public bool OutputStrict { get; set; } = true;

        /// <summary>返回类型（DB: ReturnType，如 json/text）</summary>
        [StringLength(20)]
        public string? ReturnType { get; set; } = "json";

        /// <summary>版本号（DB: Version）</summary>
        [StringLength(20)]
        public string? Version { get; set; } = "1.0";

        /// <summary>图标（DB: Icon）</summary>
        [StringLength(50)]
        public string? Icon { get; set; }

        /// <summary>颜色（DB: Color）</summary>
        [StringLength(20)]
        public string? Color { get; set; }

        /// <summary>排序号（DB: SortOrder）</summary>
        public int SortOrder { get; set; } = 0;

        /// <summary>备注（DB: Remark）</summary>
        [StringLength(500)]
        public string? Remark { get; set; }

        // ──── ISoftDelete + IIsValid 接口显式实现 ────
        public bool IsDeleted { get; set; }
        public string? DeleteBy { get; set; }
        public DateTime? DeleteTime { get; set; }
        public int IsValid { get; set; } = 1;
    }
}

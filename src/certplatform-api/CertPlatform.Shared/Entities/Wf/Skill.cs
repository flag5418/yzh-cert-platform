using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Attributes;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Wf
{
    /// <summary>
    /// 技能实体 - 用于工作流技能管理
    /// 继承 BaseEntity，映射 wf_skill 表
    /// 
    /// 命名规范（YZH 铁律）：
    ///   DB 列名 = C# 属性名 = PascalCase
    /// </summary>
    [SugarTable("wf_skill")]
    [YZHDeleteStrategy(Mode = DeleteMode.Soft)]
    public class Skill : BaseEntity
    {
        // === 主键 ===

        /// <summary>物理主键（DB: Id）</summary>
        [SugarColumn(ColumnName = "Id", IsPrimaryKey = true, IsIdentity = true)]
        public new long Id { get; set; }

        /// <summary>业务编码（DB: Code）</summary>
        [SugarColumn(ColumnName = "Code")]
        [StringLength(100)]
        public new string Code { get; set; } = string.Empty;

        // === 业务字段 ===

        /// <summary>技能编码（DB: skill_code）</summary>
        [SugarColumn(ColumnName = "skill_code")]
        [StringLength(100)]
        public string SkillCode { get; set; } = string.Empty;

        /// <summary>技能名称（DB: name）</summary>
        [SugarColumn(ColumnName = "name")]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        /// <summary>技能类型（DB: skill_type，如 method/api/llm）</summary>
        [SugarColumn(ColumnName = "skill_type")]
        [StringLength(50)]
        public string SkillType { get; set; } = "manual";

        /// <summary>分类编码（DB: category_code，关联 wf_skill_category.Code）</summary>
        [SugarColumn(ColumnName = "category_code", IsNullable = true)]
        [StringLength(64)]
        public string? CategoryCode { get; set; }

        /// <summary>是否有副作用（DB: side_effect）</summary>
        [SugarColumn(ColumnName = "side_effect")]
        public bool SideEffect { get; set; } = false;

        /// <summary>描述（DB: description）</summary>
        [SugarColumn(ColumnName = "description", ColumnDataType = "text", IsNullable = true)]
        public string? Description { get; set; }

        /// <summary>Prompt模板（DB: prompt_template）</summary>
        [SugarColumn(ColumnName = "prompt_template", ColumnDataType = "text", IsNullable = true)]
        public string? PromptTemplate { get; set; }

        /// <summary>是否激活（DB: is_active）</summary>
        [SugarColumn(ColumnName = "is_active")]
        public bool IsActive { get; set; } = true;

        /// <summary>输出严格模式（DB: output_strict）</summary>
        [SugarColumn(ColumnName = "output_strict")]
        public bool OutputStrict { get; set; } = true;

        /// <summary>返回类型（DB: return_type，如 json/text）</summary>
        [SugarColumn(ColumnName = "return_type", IsNullable = true)]
        [StringLength(20)]
        public string? ReturnType { get; set; } = "json";

        /// <summary>版本号（DB: version）</summary>
        [SugarColumn(ColumnName = "version", IsNullable = true)]
        [StringLength(20)]
        public string? Version { get; set; } = "1.0";

        /// <summary>图标（DB: icon）</summary>
        [SugarColumn(ColumnName = "icon", IsNullable = true)]
        [StringLength(50)]
        public string? Icon { get; set; }

        /// <summary>颜色（DB: color）</summary>
        [SugarColumn(ColumnName = "color", IsNullable = true)]
        [StringLength(20)]
        public string? Color { get; set; }

        /// <summary>排序号（DB: sort_order）</summary>
        [SugarColumn(ColumnName = "sort_order")]
        public int SortOrder { get; set; } = 0;

        /// <summary>备注（DB: remark）</summary>
        [SugarColumn(ColumnName = "remark", IsNullable = true)]
        [StringLength(500)]
        public string? Remark { get; set; }

        // === 审计字段（覆盖 BaseEntity 的 IsIgnore，映射到 DB PascalCase 列） ===

        /// <summary>有效标志（DB: IsValid，1=有效，0=无效）</summary>
        [SugarColumn(ColumnName = "IsValid")]
        public new int IsValid { get; set; } = 1;

        /// <summary>是否删除（DB: IsDeleted）</summary>
        [SugarColumn(ColumnName = "IsDeleted")]
        public new bool IsDeleted { get; set; }

        /// <summary>创建时间（DB: CreateTime）</summary>
        [SugarColumn(ColumnName = "CreateTime")]
        public new DateTime CreateTime { get; set; } = DateTime.UtcNow;

        /// <summary>创建人Code（DB: CreateBy）</summary>
        [SugarColumn(ColumnName = "CreateBy", IsNullable = true)]
        [StringLength(64)]
        public new string? CreateBy { get; set; }

        /// <summary>更新时间（DB: UpdateTime）</summary>
        [SugarColumn(ColumnName = "UpdateTime", IsNullable = true)]
        public new DateTime? UpdateTime { get; set; }

        /// <summary>更新人Code（DB: UpdateBy）</summary>
        [SugarColumn(ColumnName = "UpdateBy", IsNullable = true)]
        [StringLength(64)]
        public new string? UpdateBy { get; set; }

        /// <summary>删除时间（DB: DeleteTime）</summary>
        [SugarColumn(ColumnName = "DeleteTime", IsNullable = true)]
        public new DateTime? DeleteTime { get; set; }

        /// <summary>删除人Code（DB: DeleteBy）</summary>
        [SugarColumn(ColumnName = "DeleteBy", IsNullable = true)]
        [StringLength(64)]
        public new string? DeleteBy { get; set; }

        // === 忽略 BaseEntity 中本表不存在的列 ===

        [SugarColumn(IsIgnore = true)]
        public new bool CheckFlag { get; set; }

        [SugarColumn(IsIgnore = true)]
        public new bool DeleteFlag { get; set; }

        [SugarColumn(IsIgnore = true)]
        public new byte[]? RowVersion { get; set; }
    }
}

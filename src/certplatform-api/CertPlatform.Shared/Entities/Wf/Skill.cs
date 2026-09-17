using SqlSugar;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Wf
{
    /// <summary>
    /// 技能实体 - 用于工作流技能管理
    /// 继承 BaseEntity，Override IsValid 映射到 is_active 列
    /// </summary>
    [SugarTable("wf_skill")]
    public class Skill : BaseEntity
    {
        [SugarColumn(ColumnName = "code", IsPrimaryKey = true, Length = 100)]
        public new string Code { get; set; } = string.Empty;

        [SugarColumn(ColumnName = "name", Length = 200)]
        public string Name { get; set; } = string.Empty;

        [SugarColumn(ColumnName = "description", ColumnDataType = "text", IsNullable = true)]
        public string? Description { get; set; }

        [SugarColumn(ColumnName = "category_code", Length = 100, IsNullable = true)]
        public string? CategoryCode { get; set; }

        [SugarColumn(ColumnName = "skill_type", Length = 50)]
        public string SkillType { get; set; } = "manual";

        [SugarColumn(ColumnName = "prompt_template", ColumnDataType = "text", IsNullable = true)]
        public string? PromptTemplate { get; set; }

        [SugarColumn(ColumnName = "sort_order")]
        public int SortOrder { get; set; } = 0;

        /// <summary>
        /// Override 基类 IsValid，映射到 is_active 列，基类 ToggleIsValid 直接可用
        /// </summary>
        [SugarColumn(ColumnName = "is_valid")]
        public int IsValid { get; set; } = 1;
    }
}

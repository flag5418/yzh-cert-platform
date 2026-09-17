using SqlSugar;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Wf
{
    /// <summary>
    /// 技能分类实体 - 用于技能分类管理
    /// 继承 BaseEntity，Override IsValid 映射到 is_valid 列
    /// </summary>
    [SugarTable("wf_skill_category")]
    public class WfSkillCategory : BaseEntity
    {
        [SugarColumn(ColumnName = "code", IsPrimaryKey = true, Length = 100)]
        public new string Code { get; set; } = string.Empty;

        [SugarColumn(ColumnName = "category_name", Length = 200)]
        public string Name { get; set; } = string.Empty;

        [SugarColumn(ColumnName = "icon", Length = 50, IsNullable = true)]
        public string? Icon { get; set; }

        [SugarColumn(ColumnName = "color", Length = 20, IsNullable = true)]
        public string? Color { get; set; }

        [SugarColumn(ColumnName = "sort_order")]
        public int SortOrder { get; set; } = 0;

        /// <summary>
        /// Override 基类 IsValid，映射到 is_valid 列，基类 ToggleIsValid 直接可用
        /// </summary>
        [SugarColumn(ColumnName = "is_valid")]
        public int IsValid { get; set; } = 1;
    }
}

using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Attributes;
using YZH.Core.Stand.Models;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Wf
{
    /// <summary>
    /// 技能分类实体 - 用于技能分类管理（左树节点）
    /// 继承 BaseEntity + ITreeEntity（支持 TreeTable 左树架构）
    /// 
    /// 命名规范（YZH 铁律）：
    ///   DB 列名 = C# 属性名 = PascalCase
    ///   审计字段通过 new + SugarColumn 覆盖基类 IsIgnore
    /// </summary>
    [SugarTable("wf_skill_category")]
    [YZHDeleteStrategy(Mode = DeleteMode.Soft)]
    public class WfSkillCategory : BaseEntity, ITreeEntity
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

        /// <summary>分类名称（DB: category_name）</summary>
        [SugarColumn(ColumnName = "category_name")]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        /// <summary>分类编号（DB: category_code，唯一标识符）</summary>
        [SugarColumn(ColumnName = "category_code")]
        [StringLength(50)]
        public string CategoryCode { get; set; } = string.Empty;

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

        // === ITreeEntity 实现 ===

        /// <summary>父节点Code（DB: ParentCode）。根节点为 null。</summary>
        [SugarColumn(ColumnName = "ParentCode", IsNullable = true)]
        [StringLength(64)]
        public new string? ParentCode { get; set; }

        /// <summary>是否叶子节点（框架批量计算，非持久化）</summary>
        [SugarColumn(IsIgnore = true)]
        public new bool? IsLeaf { get; set; }
    }
}

using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Attributes;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Wf
{
    /// <summary>
    /// 技能分类实体 - 用于技能分类管理（左树节点）
    /// 继承 BaseEntity + ITreeEntity（支持 TreeTable 左树架构）
    ///
    /// 命名规范（YZH 铁律）：DB 列名 = C# 属性名 = PascalCase
    /// </summary>
    [SugarTable("wf_skill_category")]
    [YZHDeleteStrategy(Mode = DeleteMode.Soft)]
    public class WfSkillCategory : BaseEntity, ISoftDelete, IIsValid, ITreeEntity
    {
        // ──── Id / Code / 审计字段由 BaseEntity 基类统一提供 ────
        // ──── ISoftDelete / IIsValid 接口字段由接口提供 ────

        // === 业务字段 ===

        /// <summary>分类名称（DB: CategoryName）</summary>
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        /// <summary>分类编号（DB: CategoryCode，唯一标识符）</summary>
        [StringLength(50)]
        public string CategoryCode { get; set; } = string.Empty;

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

        // === ITreeEntity 实现 ===

        /// <summary>父节点Code（DB: ParentCode）。根节点为 null。</summary>
        public string? ParentCode { get; set; }

        /// <summary>是否叶子节点（框架批量计算，非持久化）</summary>
        [SugarColumn(IsIgnore = true)]
        public bool? IsLeaf { get; set; }
    }
}

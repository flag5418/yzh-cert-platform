using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Attributes;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Admin.Entities.Wf
{
    /// <summary>
    /// 技能分类树节点 —— 数据源 = 字典「技能分类」（DicNo='skill_category'）的字典项
    ///
    /// 背景（2026-09-26 分类字典化；2026-10-09 改为就地维护）：
    ///   · 原 wf_skill_category 表废弃（分类统一用字典项）
    ///   · 分类增删改在「技能管理」页面就地维护（不再跳去字典管理页）
    ///
    /// 关联设计：
    ///   · 树节点 Code（TreeConfig.CodeField="DicValue"）= 分类业务编码 data_access 等
    ///   · wf_skill.CategoryCode 存的即此 DicValue（iso_category 的 Category=quality 同款先例）
    ///   · ⚠️ 字典项的 Code 列（GUID 行标识）不参与关联 —— DicValue 才是业务值，
    ///     因此修改 DicValue 后会使既有技能关联悬空，需谨慎
    ///
    /// 命名规范（YZH 铁律）：DB 列名 = C# 属性名（PascalCase 例外见各 SugarColumn.Column_name，
    /// Sys_DictionaryList 为历史字典表，列名为 DicName/DicValue 等驼峰，此处按真实列名映射）
    /// </summary>
    [SugarTable("Sys_DictionaryList")]
    [YZHDeleteStrategy(Mode = DeleteMode.Soft)]
    public class SkillCategoryDict : BaseEntity, ISoftDelete, IIsValid, ITreeEntity
    {
        // ──── Id / Code / 审计字段由 BaseEntity 提供（列名与 Sys_DictionaryList 一致） ────
        // ──── Code = 字典项行标识（GUID），树不使用它（树 Code 取 DicValue） ────

        // === 字典项业务字段 ===

        /// <summary>所属字典 Code（DB: DicCode，固定为 skill_category 字典）</summary>
        [StringLength(100)]
        [SugarColumn(ColumnName = "DicCode")]
        public string DicCode { get; set; } = string.Empty;

        /// <summary>分类名称（DB: DicName）—— 树节点显示名</summary>
        [StringLength(100)]
        [SugarColumn(ColumnName = "DicName")]
        public string DicName { get; set; } = string.Empty;

        /// <summary>分类业务编码（DB: DicValue，如 data_access）—— 树节点 Code / wf_skill.CategoryCode 关联值，必填</summary>
        [StringLength(100)]
        [SugarColumn(ColumnName = "DicValue", IsNullable = true)]
        public string? DicValue { get; set; }

        /// <summary>标签颜色（DB: Color）</summary>
        [StringLength(100)]
        [SugarColumn(ColumnName = "Color", IsNullable = true)]
        public string? Color { get; set; }

        /// <summary>排序号（DB: OrderNo）</summary>
        [SugarColumn(ColumnName = "OrderNo", IsNullable = true)]
        public int? OrderNo { get; set; }

        /// <summary>备注（DB: Remark）</summary>
        [StringLength(2000)]
        [SugarColumn(ColumnName = "Remark", IsNullable = true)]
        public string? Remark { get; set; }

        // === 框架契约字段（真实列名非 PascalCase，显式映射） ===

        /// <summary>启用/禁用唯一字段（DB: IsValid）。字典项被禁用时不出现在技能分类树。</summary>
        [SugarColumn(ColumnName = "IsValid")]
        public int IsValid { get; set; } = 1;

        /// <summary>软删除标志（DB: IsDeleted）</summary>
        [SugarColumn(ColumnName = "IsDeleted")]
        public bool IsDeleted { get; set; }

        /// <summary>删除人（DB: DeleteBy）</summary>
        public string? DeleteBy { get; set; }

        /// <summary>删除时间（DB: DeleteTime）</summary>
        public DateTime? DeleteTime { get; set; }

        // === ITreeEntity 实现 ===

        /// <summary>父节点编码（DB: ParentCode）。根级分类 = null，子级分类 = 父级分类的 DicValue。</summary>
        [SugarColumn(ColumnName = "ParentCode", IsNullable = true)]
        public string? ParentCode { get; set; } = null;

        /// <summary>是否叶子节点（框架 FillIsLeafBatch 按 ParentCode 查子级数量计算）</summary>
        [SugarColumn(IsIgnore = true)]
        public bool? IsLeaf { get; set; }
    }
}

using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Attributes;
using YZH.Core.Stand.Models.Entity;

namespace YZH.Core.Api.Models.System;

/// <summary>
///     字典项实体 —— 右表明细
///     对应数据库表 Sys_DictionaryList
///
///     架构铁律：
///     ① 关联一律走 Code。父字典通过 DicCode（= Sys_Dictionary.Code）关联，
///        原 id 型关联列 Dic_ID / CreateID / ModifyID 已在
///        add_dictionary_tree_fields_v1.sql 中删除。
///     ② 字典项【不设业务编码字段】。字典项没有"项编码"，关联与定位只依赖 Code，
///        因此"没有具体项的编码也能进行关联"这一要求天然成立。
///
///     ⚠️ DicName 在主表是"字典名称"、在明细表是"选项显示文本"——同名不同义的历史遗留列名，
///        不改列名以免影响历史代码与历史数据。
///
///     框架契约：见 Sys_Dictionary.cs 顶部说明（类名=表名 / IsValid 为 int / IsDeleted 为 bool）。
/// </summary>
[SugarTable("Sys_DictionaryList")]
[YZHDeleteStrategy(Mode = DeleteMode.Soft)]
public class Sys_DictionaryList : BaseEntity
{
    /// <summary>物理主键（DB: DicList_ID）。非关联字段。</summary>
    [SugarColumn(ColumnName = "DicList_ID", IsPrimaryKey = true, IsIdentity = true)]
    public new string Id { get; set; } = string.Empty;

    /// <summary>稳定标识（DB: Code）。随机唯一，插入前生成，之后不可修改。前端下拉框的 value 即此值。</summary>
    [SugarColumn(ColumnName = "Code")]
    [StringLength(50)]
    public new string Code { get; set; } = string.Empty;

    /// <summary>所属字典 Code（DB: DicCode = Sys_Dictionary.Code）。TreeConfig.RelateField 即此字段。</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(100)]
    [Display(Name = "所属字典")]
    [SugarColumn(ColumnName = "DicCode")]
    public string DicCode { get; set; } = string.Empty;

    /// <summary>显示文本（DB: DicName）—— 前端下拉框的 label 即此值</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(100)]
    [Display(Name = "显示文本")]
    [SugarColumn(ColumnName = "DicName")]
    public string DicName { get; set; } = string.Empty;

    /// <summary>历史取值列（DB: DicValue）。新架构关联走 Code，本列降级为普通属性（可为空）。</summary>
    [StringLength(100)]
    [Display(Name = "值")]
    [SugarColumn(ColumnName = "DicValue", IsNullable = true)]
    public string? DicValue { get; set; }

    /// <summary>标签颜色（DB: Color）</summary>
    [StringLength(100)]
    [Display(Name = "颜色")]
    [SugarColumn(ColumnName = "Color", IsNullable = true)]
    public string? Color { get; set; }

    /// <summary>排序号（DB: OrderNo）</summary>
    [Display(Name = "排序")]
    [SugarColumn(ColumnName = "OrderNo", IsNullable = true)]
    public int? OrderNo { get; set; }

    /// <summary>备注（DB: Remark）</summary>
    [StringLength(2000)]
    [Display(Name = "备注")]
    [SugarColumn(ColumnName = "Remark", IsNullable = true)]
    public string? Remark { get; set; }

    /// <summary>历史启用列（DB: Enable）。已被 IsValid 取代，保留仅为兼容旧数据。</summary>
    [Display(Name = "历史启用")]
    [SugarColumn(ColumnName = "Enable", IsNullable = true)]
    public byte? Enable { get; set; } = 1;

    // === 框架必需：显式重映射到真实列 ===

    /// <summary>有效标志（1=有效，0=无效）。被禁用（IsValid=0）的字典项不会出现在下拉框中。</summary>
    [Required]
    [Display(Name = "是否有效")]
    [SugarColumn(ColumnName = "IsValid")]
    public new int IsValid { get; set; } = 1;

    /// <summary>软删除标志。必须为 bool —— IsDeletedCondition 用 Expression.Constant(false) 比较。</summary>
    [SugarColumn(ColumnName = "IsDeleted")]
    public new bool IsDeleted { get; set; }

    // === 审计字段覆盖（本表用非标准列名） ===

    /// <summary>创建人（DB: Creator）</summary>
    [SugarColumn(ColumnName = "Creator", IsNullable = true)]
    public new string? CreateBy { get; set; }

    /// <summary>创建时间（DB: CreateDate）</summary>
    [SugarColumn(ColumnName = "CreateDate")]
    public new DateTime CreateTime { get; set; }

    /// <summary>修改人（DB: Modifier）</summary>
    [SugarColumn(ColumnName = "Modifier", IsNullable = true)]
    public new string? UpdateBy { get; set; }

    /// <summary>修改时间（DB: ModifyDate）</summary>
    [SugarColumn(ColumnName = "ModifyDate", IsNullable = true)]
    public new DateTime? UpdateTime { get; set; }

    /// <summary>删除时间（DB: DeleteTime）</summary>
    [SugarColumn(ColumnName = "DeleteTime", IsNullable = true)]
    public new DateTime? DeleteTime { get; set; }

    /// <summary>删除人（DB: DeleteBy）</summary>
    [SugarColumn(ColumnName = "DeleteBy", IsNullable = true)]
    public new string? DeleteBy { get; set; }

    // === 忽略 BaseEntity 中本表不存在的列 ===

    /// <summary>数据版本号（本表无此列）</summary>
    [SugarColumn(IsIgnore = true)]
    public new byte[]? RowVersion { get; set; }
}

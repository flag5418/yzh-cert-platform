using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Attributes;
using YZH.Core.Stand.Models;
using YZH.Core.Stand.Models.Entity;

namespace YZH.Core.Api.Models.System;

/// <summary>
///     字典（分类）实体 —— 左树节点
///     对应数据库表 Sys_Dictionary
///
///     架构铁律：
///     ① 关联一律走 Code。定位记录的核心字段是 Code；不存在 ParentId / CreateID / ModifyID
///        等一切以 Id 建立关联的字段（已在 add_dictionary_tree_fields_V1.sql 中删除）。
///     ② Code 与业务编码分离。Code 是随机生成、全局唯一、永不重复的稳定标识，也是唯一关联键；
///        字典编码 DicNo 只是普通属性，可为空、仅用于显示，不参与任何关联。
///
///     框架契约（不可随意改动，否则框架静默失效）：
///     - 类名必须等于表名：EntityService.GetByCodeAny / GetChildrenCountBatch 用
///       typeof(T).Name 当表名（不读 [SugarTable]），改名会导致 SQL 报错。
///     - IsValid 必须为 int 且显式映射真实列：SqlSugarDbOrm.IsValidCondition 用
///       Expression.Constant(1)，类型不符会抛表达式异常；标记 IsIgnore 则过滤被静默跳过。
///     - IsDeleted 必须为 bool 且显式映射真实列：IsDeletedCondition 用 Expression.Constant(false)，
///       且 EntityService.SoftDelete 执行 dyn.IsDeleted = true。
///     - ParentCode 必须在本类【声明】（DeclaredOnly）：GetChildrenCountBatch 用它取列名。
///     - Code 插入后不可 UPDATE（SqlSugarDbOrm.GetIgnoreColumnsForUpdate 含 "Code"），
///       故必须在插入前定值；框架已自动填充 32 位无连字符 GUID，控制器无需干预。
/// </summary>
[SugarTable("Sys_Dictionary")]
[YZHDeleteStrategy(Mode = DeleteMode.Soft)]
public class Sys_Dictionary : BaseEntity, ITreeEntity
{
    /// <summary>物理主键（DB: Dic_ID）。非关联字段，仅作表主键存在。</summary>
    [SugarColumn(ColumnName = "Dic_ID", IsPrimaryKey = true, IsIdentity = true)]
    public new string Id { get; set; } = string.Empty;

    /// <summary>稳定标识 + 关联键（DB: Code）。随机唯一，插入前生成，之后不可修改。</summary>
    [SugarColumn(ColumnName = "Code")]
    [StringLength(50)]
    public new string Code { get; set; } = string.Empty;

    /// <summary>父节点 Code（DB: ParentCode）。树结构的唯一关联方式，根节点为 null。</summary>
    [SugarColumn(ColumnName = "ParentCode")]
    [StringLength(64)]
    public new string? ParentCode { get; set; }

    /// <summary>字典名称（DB: DicName）—— 树节点显示名</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(100)]
    [Display(Name = "字典名称")]
    [SugarColumn(ColumnName = "DicName")]
    public string DicName { get; set; } = string.Empty;

    /// <summary>
    ///     字典编码（DB: DicNo）—— 可选属性，仅用于显示
    ///     非必填：允许为空；填写时须唯一（uk_sys_dictionary_dicno 兜底，MySQL 唯一索引允许多个 NULL）
    /// </summary>
    [StringLength(100)]
    [Display(Name = "字典编码")]
    [SugarColumn(ColumnName = "DicNo", IsNullable = true)]
    public string? DicNo { get; set; }

    /// <summary>配置项（JSON 格式，DB: Config）</summary>
    [StringLength(10000)]
    [Display(Name = "配置项")]
    [SugarColumn(ColumnName = "Config", IsNullable = true)]
    public string? Config { get; set; }

    /// <summary>数据源 SQL（DB: DbSql）—— 二期动态字典用</summary>
    [StringLength(10000)]
    [Display(Name = "数据源SQL")]
    [SugarColumn(ColumnName = "DbSql", IsNullable = true)]
    public string? DbSql { get; set; }

    /// <summary>数据库服务器（DB: DBServer）—— 二期动态字典用</summary>
    [StringLength(10000)]
    [Display(Name = "数据库服务器")]
    [SugarColumn(ColumnName = "DBServer", IsNullable = true)]
    public string? DBServer { get; set; }

    /// <summary>排序号（DB: OrderNo）</summary>
    [Display(Name = "排序")]
    [SugarColumn(ColumnName = "OrderNo", IsNullable = true)]
    public int? OrderNo { get; set; }

    /// <summary>备注（DB: Remark）</summary>
    [StringLength(2000)]
    [Display(Name = "备注")]
    [SugarColumn(ColumnName = "Remark", IsNullable = true)]
    public string? Remark { get; set; }

    /// <summary>历史启用列（DB: Enable）。已被 IsValid 取代，保留仅为兼容旧数据，新架构不再写入。</summary>
    [Display(Name = "历史启用")]
    [SugarColumn(ColumnName = "Enable")]
    public byte Enable { get; set; } = 1;

    // === 框架必需：显式重映射到真实列 ===

    /// <summary>有效标志（1=有效，0=无效）。tree/toggle-valid 反射读写此属性。</summary>
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

    // === ITreeEntity 实现 ===

    /// <summary>是否叶子节点（框架批量计算，非持久化）</summary>
    [SugarColumn(IsIgnore = true)]
    public new bool? IsLeaf { get; set; }
}

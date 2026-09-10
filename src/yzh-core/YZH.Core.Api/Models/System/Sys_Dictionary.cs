using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using SqlSugar;
using YZH.Core.Stand.Attributes;
using YZH.Core.Stand.Models.Entity;

namespace YZH.Core.Api.Models.System;

/// <summary>
///     字典数据实体（新架构版）
///     对应数据库表 Sys_Dictionary
///     兼容 Vol 框架表结构
/// </summary>
[SugarTable("Sys_Dictionary")]
[YZHDeleteStrategy(Mode = DeleteMode.Soft)]
public class Sys_Dictionary : BaseEntity
{
    /// <summary>字典编号</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(100)]
    [Display(Name = "字典编号")]
    [SugarColumn(ColumnName = "DicNo")]
    public string DicNo { get; set; } = string.Empty;

    /// <summary>字典名称</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(100)]
    [Display(Name = "字典名称")]
    [SugarColumn(ColumnName = "DicName")]
    public string DicName { get; set; } = string.Empty;

    /// <summary>父级ID</summary>
    [Required]
    [Display(Name = "父级ID")]
    [SugarColumn(ColumnName = "ParentId")]
    public int ParentId { get; set; }

    /// <summary>配置项（JSON格式）</summary>
    [StringLength(10000)]
    [Display(Name = "配置项")]
    [SugarColumn(ColumnName = "Config")]
    public string? Config { get; set; }

    /// <summary>SQL语句（动态数据源）</summary>
    [StringLength(10000)]
    [Display(Name = "sql语句")]
    [SugarColumn(ColumnName = "DbSql")]
    public string? DbSql { get; set; }

    /// <summary>数据库服务器（动态数据源）</summary>
    [StringLength(10000)]
    [Display(Name = "DBServer")]
    [SugarColumn(ColumnName = "DBServer")]
    public string? DBServer { get; set; }

    /// <summary>排序号</summary>
    [Display(Name = "排序号")]
    [SugarColumn(ColumnName = "OrderNo")]
    public int? OrderNo { get; set; }

    /// <summary>备注</summary>
    [StringLength(2000)]
    [Display(Name = "备注")]
    [SugarColumn(ColumnName = "Remark")]
    public string? Remark { get; set; }

    /// <summary>是否启用（1=启用，0=禁用）</summary>
    [Required]
    [Display(Name = "是否启用")]
    [SugarColumn(ColumnName = "Enable")]
    public byte Enable { get; set; } = 1;

    /// <summary>创建人ID</summary>
    [Display(Name = "CreateID")]
    [SugarColumn(ColumnName = "CreateID")]
    public int? CreateID { get; set; }

    /// <summary>创建人</summary>
    [StringLength(30)]
    [SugarColumn(ColumnName = "Creator")]
    public string? Creator { get; set; }

    /// <summary>创建时间</summary>
    [SugarColumn(ColumnName = "CreateDate")]
    public DateTime? CreateDate { get; set; }

    /// <summary>修改人ID</summary>
    [Display(Name = "ModifyID")]
    [SugarColumn(ColumnName = "ModifyID")]
    public int? ModifyID { get; set; }

    /// <summary>修改人</summary>
    [StringLength(30)]
    [SugarColumn(ColumnName = "Modifier")]
    public string? Modifier { get; set; }

    /// <summary>修改时间</summary>
    [SugarColumn(ColumnName = "ModifyDate")]
    public DateTime? ModifyDate { get; set; }
}

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using SqlSugar;
using YZH.Core.Stand.Attributes;
using YZH.Core.Stand.Models.Entity;

namespace YZH.Core.Api.Models.System;

/// <summary>
///     菜单配置实体（新架构版）
///     对应数据库表 Sys_Menu
///     兼容 Vol 框架表结构
/// </summary>
[SugarTable("Sys_Menu")]
[YZHDeleteStrategy(Mode = DeleteMode.Soft)]
public class Sys_Menu : BaseEntity
{
    /// <summary>菜单分类标签：admin/auditor/enterprise/common</summary>
    [StringLength(20)]
    [Display(Name = "Tag")]
    [SugarColumn(ColumnName = "Tag")]
    public string? Tag { get; set; }

    /// <summary>父级ID</summary>
    [Required]
    [Display(Name = "父级ID")]
    [SugarColumn(ColumnName = "ParentId")]
    public int ParentId { get; set; }

    /// <summary>菜单名称</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(50)]
    [Display(Name = "菜单名称")]
    [SugarColumn(ColumnName = "MenuName")]
    public string MenuName { get; set; } = string.Empty;

    /// <summary>表名（关联实体）</summary>
    [StringLength(200)]
    [Display(Name = "TableName")]
    [SugarColumn(ColumnName = "TableName")]
    public string? TableName { get; set; }

    /// <summary>菜单URL</summary>
    [StringLength(10000)]
    [Display(Name = "Url")]
    [SugarColumn(ColumnName = "Url")]
    public string? Url { get; set; }

    /// <summary>权限标识</summary>
    [StringLength(10000)]
    [Display(Name = "权限")]
    [SugarColumn(ColumnName = "Auth")]
    public string? Auth { get; set; }

    /// <summary>描述</summary>
    [StringLength(200)]
    [Display(Name = "Description")]
    [SugarColumn(ColumnName = "Description")]
    public string? Description { get; set; }

    /// <summary>图标</summary>
    [StringLength(50)]
    [Display(Name = "图标")]
    [SugarColumn(ColumnName = "Icon")]
    public string? Icon { get; set; }

    /// <summary>排序号</summary>
    [Display(Name = "排序号")]
    [SugarColumn(ColumnName = "OrderNo")]
    public int? OrderNo { get; set; }

    /// <summary>是否启用（1=启用，0=禁用）</summary>
    [Required]
    [Display(Name = "是否启用")]
    [SugarColumn(ColumnName = "Enable")]
    public byte? Enable { get; set; } = 1;

    /// <summary>菜单类型（0=PC端，1=移动端）</summary>
    [Display(Name = "菜单类型")]
    [SugarColumn(ColumnName = "MenuType")]
    public int? MenuType { get; set; }

    /// <summary>创建人</summary>
    [StringLength(50)]
    [SugarColumn(ColumnName = "Creator")]
    public string? Creator { get; set; }

    /// <summary>创建时间</summary>
    [SugarColumn(ColumnName = "CreateDate")]
    public DateTime? CreateDate { get; set; }

    /// <summary>修改人</summary>
    [StringLength(50)]
    [SugarColumn(ColumnName = "Modifier")]
    public string? Modifier { get; set; }

    /// <summary>修改时间</summary>
    [SugarColumn(ColumnName = "ModifyDate")]
    public DateTime? ModifyDate { get; set; }
}

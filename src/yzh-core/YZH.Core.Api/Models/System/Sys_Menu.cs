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
    /// <summary>主键（DB: Menu_Id）</summary>
    [SugarColumn(ColumnName = "Menu_Id", IsPrimaryKey = true, IsIdentity = true)]
    public new string Id { get; set; } = string.Empty;

    /// <summary>菜单编码（DB: Code）</summary>
    [SugarColumn(ColumnName = "Code")]
    [StringLength(50)]
    public new string Code { get; set; } = string.Empty;

    /// <summary>菜单分类标签：admin/auditor/enterprise/common</summary>
    [StringLength(20)]
    [Display(Name = "Tag")]
    [SugarColumn(ColumnName = "Tag")]
    public string? Tag { get; set; }

    /// <summary>父级编码（Code关联，0表示根节点）</summary>
    [Required]
    [Display(Name = "父级编码")]
    [SugarColumn(ColumnName = "ParentCode")]
    [StringLength(50)]
    public string ParentCode { get; set; } = "0";

    /// <summary>菜单名称</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(50)]
    [Display(Name = "菜单名称")]
    [SugarColumn(ColumnName = "MenuName")]
    public string MenuName { get; set; } = string.Empty;

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

    /// <summary>创建人（DB: Creator）</summary>
    [SugarColumn(ColumnName = "Creator")]
    [StringLength(50)]
    public new string? Creator { get; set; }

    /// <summary>创建时间（DB: CreateDate）</summary>
    [SugarColumn(ColumnName = "CreateDate")]
    public new DateTime? CreateDate { get; set; }

    /// <summary>修改时间（DB: ModifyDate）</summary>
    [SugarColumn(ColumnName = "ModifyDate")]
    public new DateTime? ModifyDate { get; set; }

    /// <summary>修改人（DB: Modifier）</summary>
    [SugarColumn(ColumnName = "Modifier")]
    [StringLength(50)]
    public new string? Modifier { get; set; }
}

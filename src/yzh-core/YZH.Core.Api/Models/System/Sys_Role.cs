using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using SqlSugar;
using YZH.Core.Stand.Attributes;
using YZH.Core.Stand.Models.Entity;

namespace YZH.Core.Api.Models.System;

/// <summary>
///     角色管理实体（新架构版）
///     对应数据库表 Sys_Role
///     兼容 Vol 框架表结构
/// </summary>
[SugarTable("Sys_Role")]
[YZHDeleteStrategy(Mode = DeleteMode.Soft)]
public class Sys_Role : BaseEntity
{
    /// <summary>角色名称</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(50)]
    [Display(Name = "角色名称")]
    [SugarColumn(ColumnName = "RoleName")]
    public string RoleName { get; set; } = string.Empty;

    /// <summary>父级ID</summary>
    [Required]
    [Display(Name = "父级ID")]
    [SugarColumn(ColumnName = "ParentId")]
    public int ParentId { get; set; }

    /// <summary>部门ID</summary>
    [Display(Name = "部门ID")]
    [SugarColumn(ColumnName = "Dept_Id")]
    public int? Dept_Id { get; set; }

    /// <summary>部门名称</summary>
    [StringLength(50)]
    [Display(Name = "部门名称")]
    [SugarColumn(ColumnName = "DeptName")]
    public string? DeptName { get; set; }

    /// <summary>排序号</summary>
    [Display(Name = "排序")]
    [SugarColumn(ColumnName = "OrderNo")]
    public int? OrderNo { get; set; }

    /// <summary>是否启用（1=启用，0=禁用）</summary>
    [Required]
    [Display(Name = "是否启用")]
    [SugarColumn(ColumnName = "Enable")]
    public byte? Enable { get; set; } = 1;

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

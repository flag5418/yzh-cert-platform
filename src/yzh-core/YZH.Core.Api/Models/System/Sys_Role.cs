using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using SqlSugar;
using YZH.Core.Stand.Attributes;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;
using YZH.Core.Stand.Models;

namespace YZH.Core.Api.Models.System;

/// <summary>
///     角色管理实体（新架构版）
///     对应数据库表 Sys_Role
///     兼容 Vol 框架表结构
///     支持树形结构：通过 ParentCode 构建层级关系
/// </summary>
[SugarTable("Sys_Role")]
[YZHDeleteStrategy(Mode = DeleteMode.Soft)]
public class Sys_Role : BaseEntity, ISoftDelete, IIsValid, ITreeEntity
{
    /// <summary>角色编码（DB: Code）</summary>
    [SugarColumn(ColumnName = "Code")]
    [StringLength(64)]
    public new string Code { get; set; } = string.Empty;

    /// <summary>角色名称</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(50)]
    [Display(Name = "角色名称")]
    [SugarColumn(ColumnName = "RoleName")]
    public string RoleName { get; set; } = string.Empty;

    /// <summary>父节点编码（树结构，根节点为 null）</summary>
    [StringLength(64)]
    [Display(Name = "上级角色")]
    [SugarColumn(ColumnName = "ParentCode")]
    public string? ParentCode { get; set; }

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

    /// <summary>创建人（DB: CreateBy）</summary>
    [StringLength(50)]
    public new string? CreateBy { get; set; }

    /// <summary>创建时间（DB: CreateTime）</summary>
    public new DateTime CreateTime { get; set; }

    /// <summary>更新人（DB: UpdateBy）</summary>
    [StringLength(50)]
    public new string? UpdateBy { get; set; }

    /// <summary>更新时间（DB: UpdateTime）</summary>
    public new DateTime? UpdateTime { get; set; }

    /// <summary>删除人（DB: DeleteBy）</summary>
    [StringLength(50)]
    public new string? DeleteBy { get; set; }

    // === ITreeEntity 实现 ===

    /// <summary>是否叶子节点（后端批量计算，非持久化）</summary>
    [SugarColumn(IsIgnore = true)]
    public new bool? IsLeaf { get; set; }

    /// <summary>Sys_Role 使用 Role_Id (int) 作为 PK，映射 BaseEntity.Id</summary>
    [SugarColumn(ColumnName = "Role_Id", IsPrimaryKey = true, IsIdentity = true)]
    public new string Id { get; set; } = string.Empty;

    /// <summary>是否删除（DB: IsDeleted）</summary>
    public bool IsDeleted { get; set; }

    /// <summary>删除时间（本表无此列，标记 IsIgnore）</summary>
    [SugarColumn(IsIgnore = true)]
    public new DateTime? DeleteTime { get; set; }

    /// <summary>是否有效（DB: IsValid）</summary>
    public int IsValid { get; set; } = 1;
}

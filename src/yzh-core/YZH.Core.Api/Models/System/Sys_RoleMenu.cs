using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Attributes;
using YZH.Core.Stand.Models.Entity;

namespace YZH.Core.Api.Models.System;

/// <summary>
///     角色-菜单关联表（Code 关联）
///     多对多关系：一个角色可关联多个菜单，一个菜单可属于多个角色
///
///     关联方式：
///     - RoleCode → Sys_Role.Code
///     - MenuCode → Sys_Menu.Code
///
///     设计原则：Code 是唯一业务键，所有关联必须用 Code
/// </summary>
[SugarTable("Sys_RoleMenu")]
[YZHDeleteStrategy(Mode = DeleteMode.Hard)]
public class Sys_RoleMenu : BaseEntity
{
    /// <summary>主键（DB: Id）</summary>
    [SugarColumn(ColumnName = "Id", IsPrimaryKey = true)]
    public new string Id { get; set; } = string.Empty;

    /// <summary>角色编码（关联 Sys_Role.Code）</summary>
    [SugarColumn(ColumnName = "RoleCode")]
    public string RoleCode { get; set; } = string.Empty;

    /// <summary>菜单编码（关联 Sys_Menu.Code）</summary>
    [SugarColumn(ColumnName = "MenuCode")]
    public string MenuCode { get; set; } = string.Empty;

    /// <summary>排序号</summary>
    [SugarColumn(ColumnName = "OrderNo")]
    public int? OrderNo { get; set; }

    /// <summary>创建时间（DB: CreateTime）</summary>
    [SugarColumn(ColumnName = "CreateTime")]
    public new DateTime CreateTime { get; set; } = DateTime.UtcNow;

    /// <summary>创建人（DB: CreateBy）</summary>
    [SugarColumn(ColumnName = "CreateBy")]
    [StringLength(64)]
    public new string? CreateBy { get; set; }
}

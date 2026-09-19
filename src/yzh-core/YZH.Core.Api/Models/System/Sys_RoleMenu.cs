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

    // ========================================================
    // 本表缺少的 BaseEntity 强制列 —— 必须 IsIgnore
    // ========================================================
    // BaseEntity 无 IsIgnore 的成员会被 SqlSugar 拼进 SELECT / INSERT / UPDATE。
    // Sys_RoleMenu 是「角色↔菜单」中间表（表列：Id/RoleCode/MenuCode/OrderNo/CreateTime/CreateBy），
    // 不存在 Code / UpdateTime / UpdateBy，若不屏蔽会报：
    //   MySqlException: Unknown column 'Code' in 'field list'
    // → GetListAsync 静默返回空、InsertAsync 全部失败，整个角色-菜单页读写均不可用。

    /// <summary>中间表无业务 Code 列（用 RoleCode + MenuCode 做关联）</summary>
    [SugarColumn(IsIgnore = true)]
    public new string Code { get; set; } = string.Empty;

    /// <summary>本表无 UpdateTime 列</summary>
    [SugarColumn(IsIgnore = true)]
    public new DateTime? UpdateTime { get; set; }

    /// <summary>本表无 UpdateBy 列</summary>
    [SugarColumn(IsIgnore = true)]
    public new string? UpdateBy { get; set; }
}

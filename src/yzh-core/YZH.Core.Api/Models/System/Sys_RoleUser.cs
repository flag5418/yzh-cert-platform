using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Attributes;
using YZH.Core.Stand.Models.Entity;

namespace YZH.Core.Api.Models.System;

/// <summary>
///     角色-用户关联表（Code 关联）
///     多对多关系：一个角色可关联多个用户，一个用户可属于多个角色
///     
///     关联方式：
///     - RoleCode → Sys_Role.Code
///     - UserCode → Sys_User.Code
///     
///     设计原则：Code 是唯一业务键，所有关联必须用 Code
/// </summary>
[SugarTable("Sys_RoleUser")]
[YZHDeleteStrategy(Mode = DeleteMode.Hard)]
public class Sys_RoleUser : BaseEntity
{
    /// <summary>主键（DB: Id）</summary>
    [SugarColumn(ColumnName = "Id", IsPrimaryKey = true)]
    public new string Id { get; set; } = string.Empty;

    /// <summary>角色编码（关联 Sys_Role.Code）</summary>
    [SugarColumn(ColumnName = "RoleCode")]
    public string RoleCode { get; set; } = string.Empty;

    /// <summary>用户编码（关联 Sys_User.Code）</summary>
    [SugarColumn(ColumnName = "UserCode")]
    public string UserCode { get; set; } = string.Empty;

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
    // 同 Sys_RoleMenu：本表列仅 Id/RoleCode/UserCode/OrderNo/CreateTime/CreateBy，
    // 不存在 Code / UpdateTime / UpdateBy，不屏蔽会报
    //   Unknown column 'Code' in 'field list' → 角色-人员页读写全废。

    /// <summary>中间表无业务 Code 列（用 RoleCode + UserCode 做关联）</summary>
    [SugarColumn(IsIgnore = true)]
    public new string Code { get; set; } = string.Empty;

    /// <summary>本表无 UpdateTime 列</summary>
    [SugarColumn(IsIgnore = true)]
    public new DateTime? UpdateTime { get; set; }

    /// <summary>本表无 UpdateBy 列</summary>
    [SugarColumn(IsIgnore = true)]
    public new string? UpdateBy { get; set; }
}

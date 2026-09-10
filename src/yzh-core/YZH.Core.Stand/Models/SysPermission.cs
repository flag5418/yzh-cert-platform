using System;
using SqlSugar;

namespace YZH.Core.Stand.Models;

/// <summary>
/// 接口表实体
/// </summary>
[SugarTable("sys_api")]
public class SysApi
{
    /// <summary>主键ID</summary>
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }
    
    /// <summary>接口编码（SHA256 Hash）</summary>
    [SugarColumn(Length = 64)]
    public string Code { get; set; } = "";
    
    /// <summary>HTTP方法</summary>
    [SugarColumn(Length = 10)]
    public string Method { get; set; } = "";
    
    /// <summary>接口路径</summary>
    [SugarColumn(Length = 200)]
    public string Path { get; set; } = "";
    
    /// <summary>树形路径（如 System|User|filter）</summary>
    [SugarColumn(Length = 500)]
    public string? TreePath { get; set; }
    
    /// <summary>接口名称</summary>
    [SugarColumn(Length = 200)]
    public string Name { get; set; } = "";
    
    /// <summary>负责人</summary>
    [SugarColumn(Length = 50)]
    public string? Author { get; set; }
    
    /// <summary>是否启用</summary>
    public bool Enable { get; set; } = true;
    
    /// <summary>创建时间</summary>
    public DateTime CreateDate { get; set; } = DateTime.UtcNow;
    
    /// <summary>更新时间</summary>
    public DateTime? UpdateDate { get; set; }
}

/// <summary>
/// 角色-接口关联表实体
/// </summary>
[SugarTable("sys_role_api")]
public class SysRoleApi
{
    /// <summary>主键ID</summary>
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }
    
    /// <summary>角色编码</summary>
    [SugarColumn(Length = 50)]
    public string RoleCode { get; set; } = "";
    
    /// <summary>接口编码</summary>
    [SugarColumn(Length = 64)]
    public string ApiCode { get; set; } = "";
    
    /// <summary>创建时间</summary>
    public DateTime CreateDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 用户权限缓存表实体
/// </summary>
[SugarTable("sys_user_permission")]
public class SysUserPermission
{
    /// <summary>主键ID</summary>
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }
    
    /// <summary>用户编码</summary>
    [SugarColumn(Length = 36)]
    public string UserCode { get; set; } = "";
    
    /// <summary>接口编码</summary>
    [SugarColumn(Length = 64)]
    public string ApiCode { get; set; } = "";
    
    /// <summary>创建时间</summary>
    public DateTime CreateDate { get; set; } = DateTime.UtcNow;
    
    /// <summary>更新时间</summary>
    public DateTime? UpdateDate { get; set; }
}

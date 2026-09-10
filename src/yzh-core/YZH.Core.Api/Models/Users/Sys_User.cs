using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using SqlSugar;
using YZH.Core.Stand.Annotations;
using YZH.Core.Stand.Attributes;
using YZH.Core.Stand.Models;
using YZH.Core.Stand.Models.Entity;

namespace YZH.Core.Api.Models.Users;

/// <summary>
///     系统用户实体（兼容 Vol 框架表结构）
///     对应数据库表 Sys_User
///     注意：Sys_User 是 Vol 遗留表，列名混合了 PascalCase 和 snake_case
///
///     关键映射：
///     - BaseEntity.Id (string) → DB: User_Id (int) 需特殊处理
///     - RoleId → Role_Id (int)
///     - OrgCode → OrgCode (varchar, 直接匹配)
///     - CreateTime → CreateDate, CreateBy → Creator
///     - UpdateTime → ModifyDate, UpdateBy → Modifier
///     - DeleteTime → DeleteTime ✓, DeleteBy → DeleteBy ✓, IsDeleted → IsDeleted ✓
///
///     视图路由：
///     - 查询走 v_sys_user（含 OrgName、RoleName 关联字段）
///     - 增删改走 Sys_User 物理表（[NotMapped] 字段自动忽略）
/// </summary>
[SugarTable("Sys_User")]
[ViewName("v_sys_user")]
[YZHDeleteStrategy(Mode = DeleteMode.Soft)]
public class Sys_User : BaseEntity
{
    /// <summary>用户账号（登录名，唯一）</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(100)]
    [Display(Name = "帐号")]
    [SugarColumn(ColumnName = "UserName")]
    public string UserName { get; set; } = string.Empty;

    /// <summary>真实姓名</summary>
    [StringLength(20)]
    [Display(Name = "姓名")]
    [SugarColumn(ColumnName = "UserTrueName")]
    public string UserTrueName { get; set; } = string.Empty;

    /// <summary>密码（加密存储，仅输入时绑定，输出时忽略）</summary>
    [StringLength(200)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    [SugarColumn(ColumnName = "UserPwd")]
    public string UserPwd { get; set; } = string.Empty;

    /// <summary>角色 ID（关联 Sys_Role，Vol 兼容）→ DB: Role_Id</summary>
    [Required]
    [Display(Name = "角色")]
    [SugarColumn(ColumnName = "Role_Id")]
    public int RoleId { get; set; }

    /// <summary>是否启用（1=启用，0=禁用）</summary>
    [Required]
    [Display(Name = "是否可用")]
    [SugarColumn(ColumnName = "Enable")]
    public byte Enable { get; set; } = 1;

    /// <summary>性别（0=未知，1=男，2=女）</summary>
    [Display(Name = "性别")]
    [SugarColumn(ColumnName = "Gender")]
    public int? Gender { get; set; }

    /// <summary>手机号</summary>
    [StringLength(11)]
    [Display(Name = "手机号")]
    [SugarColumn(ColumnName = "PhoneNo")]
    public string? PhoneNo { get; set; }

    /// <summary>邮箱</summary>
    [StringLength(100)]
    [Display(Name = "邮箱")]
    [SugarColumn(ColumnName = "Email")]
    public string? Email { get; set; }

    /// <summary>头像 URL</summary>
    [StringLength(500)]
    [Display(Name = "头像")]
    [SugarColumn(ColumnName = "HeadImageUrl")]
    public string? HeadImageUrl { get; set; }

    /// <summary>地址</summary>
    [StringLength(200)]
    [SugarColumn(ColumnName = "Address")]
    public string? Address { get; set; }

    /// <summary>所属机构编码（关联 Sys_Organization.Code）</summary>
    [StringLength(64)]
    [Display(Name = "所属机构")]
    [SugarColumn(ColumnName = "OrgCode")]
    public string? OrgCode { get; set; }

    /// <summary>备注</summary>
    [StringLength(200)]
    [SugarColumn(ColumnName = "Remark")]
    public string? Remark { get; set; }

    /// <summary>最后登录时间</summary>
    [SugarColumn(ColumnName = "LastLoginDate")]
    public DateTime? LastLoginDate { get; set; }

    /// <summary>最后密码修改时间</summary>
    [SugarColumn(ColumnName = "LastModifyPwdDate")]
    public DateTime? LastModifyPwdDate { get; set; }

    /// <summary>排序号</summary>
    [SugarColumn(ColumnName = "OrderNo")]
    public int? OrderNo { get; set; }

    /// <summary>Token（最近一次登录，Vol 兼容）</summary>
    [StringLength(500)]
    [JsonIgnore]
    [SugarColumn(ColumnName = "Token")]
    public string? Token { get; set; }

    // === 视图扩展字段（[NotMapped]，仅查询时由视图填充，增删改自动忽略） ===

    /// <summary>角色名称（视图字段，来自 v_sys_user JOIN Sys_Role）</summary>
    [SugarColumn(IsIgnore = true)]
    public string? RoleName { get; set; }

    /// <summary>机构名称（视图字段，来自 v_sys_user JOIN Sys_Organization）</summary>
    [SugarColumn(IsIgnore = true)]
    public string? OrgName { get; set; }

    /// <summary>性别描述（运行时翻译）</summary>
    [SugarColumn(IsIgnore = true)]
    public string? GenderDesc { get; set; }

    /// <summary>启用状态描述（运行时翻译）</summary>
    [SugarColumn(IsIgnore = true)]
    public string? EnableDesc { get; set; }

    // === 审计字段覆盖（适配 Vol 表结构） ===

    /// <summary>创建时间 → DB: CreateDate</summary>
    [SugarColumn(ColumnName = "CreateDate")]
    public new DateTime CreateTime { get; set; }

    /// <summary>创建人 → DB: Creator</summary>
    [SugarColumn(ColumnName = "Creator"), StringLength(200)]
    public new string? CreateBy { get; set; }

    /// <summary>更新时间 → DB: ModifyDate</summary>
    [SugarColumn(ColumnName = "ModifyDate")]
    public new DateTime? UpdateTime { get; set; }

    /// <summary>更新人 → DB: Modifier</summary>
    [SugarColumn(ColumnName = "Modifier"), StringLength(200)]
    public new string? UpdateBy { get; set; }

    // === 忽略 BaseEntity 中不存在的列 ===

    [SugarColumn(IsIgnore = true)]
    public new byte[]? RowVersion { get; set; }

    [SugarColumn(IsIgnore = true)]
    public new bool CheckFlag { get; set; }

    [SugarColumn(IsIgnore = true)]
    public new bool DeleteFlag { get; set; }

    /// <summary>Sys_User 使用 User_Id (int) 作为 PK，而非 BaseEntity.Id (string)</summary>
    [SugarColumn(ColumnName = "User_Id", IsIgnore = true)]
    public new string Id { get; set; } = string.Empty;
}

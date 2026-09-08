using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using YZH.Core.Stand.Attributes;
using YZH.Core.Stand.Models;

namespace YZH.Core.Api.Models.Users;

/// <summary>
///     系统用户实体
///     对应数据库表 Sys_User（Vol 兼容表）
///     存储登录账号基本信息、状态、角色关联
///     
///     审计字段映射（兼容 Vol 表结构）：
///     - CreateBy → Creator 列（varchar(200)，存 UserCode）
///     - CreateTime → CreateDate 列（datetime）
///     - UpdateBy → Modifier 列（varchar(200)，存 UserCode）
///     - UpdateTime → ModifyDate 列（datetime）
///     - DeleteBy/IsDeleted/DeleteTime → 新增列，通过 migration 脚本添加
///     
///     注：Vol 原生审计列 CreateID/ModifyID（int）保留但不映射，仅供历史数据追溯
/// </summary>
[Table("Sys_User")]
[YZHDeleteStrategy(Mode = DeleteMode.Soft)]
public class Sys_User : BaseEntity
{
    /// <summary>用户账号（登录名，唯一）</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(100)]
    [Display(Name = "帐号")]
    public string UserName { get; set; } = string.Empty;

    /// <summary>真实姓名</summary>
    [StringLength(20)]
    [Display(Name = "姓名")]
    public string UserTrueName { get; set; } = string.Empty;

    /// <summary>密码（加密存储）</summary>
    [StringLength(200)]
    [JsonIgnore]
    public string UserPwd { get; set; } = string.Empty;

    /// <summary>角色 ID（关联 Sys_Role，Vol 兼容）</summary>
    [Required]
    [Display(Name = "角色")]
    public int RoleId { get; set; }

    /// <summary>角色名称（视图扩展字段，不持久化）</summary>
    [NotMapped]
    public string? RoleName { get; set; }

    /// <summary>是否启用（1=启用，0=禁用）</summary>
    [Required]
    [Display(Name = "是否可用")]
    public byte Enable { get; set; } = 1;

    /// <summary>性别（0=未知，1=男，2=女）</summary>
    [Display(Name = "性别")]
    public int? Gender { get; set; }

    /// <summary>性别描述（视图字段，OnQueried 中翻译）</summary>
    [NotMapped]
    public string? GenderDesc { get; set; }

    /// <summary>启用状态描述（视图字段，OnQueried 中翻译）</summary>
    [NotMapped]
    public string? EnableDesc { get; set; }

    /// <summary>手机号</summary>
    [StringLength(11)]
    [Display(Name = "手机号")]
    public string? PhoneNo { get; set; }

    /// <summary>邮箱</summary>
    [StringLength(100)]
    [Display(Name = "邮箱")]
    public string? Email { get; set; }

    /// <summary>头像 URL</summary>
    [StringLength(500)]
    [Display(Name = "头像")]
    public string? HeadImageUrl { get; set; }

    /// <summary>地址</summary>
    [StringLength(200)]
    public string? Address { get; set; }

    /// <summary>所属机构编码（关联 Sys_Organization.Code，用于组织-人员联动过滤）</summary>
    [StringLength(64)]
    [Display(Name = "所属机构")]
    [Column("org_code")]
    public string? OrgCode { get; set; }

    /// <summary>备注</summary>
    [StringLength(200)]
    public string? Remark { get; set; }

    /// <summary>最后登录时间</summary>
    public DateTime? LastLoginDate { get; set; }

    /// <summary>最后密码修改时间</summary>
    public DateTime? LastModifyPwdDate { get; set; }

    /// <summary>排序号</summary>
    public int? OrderNo { get; set; }

    /// <summary>Token（最近一次登录，Vol 兼容）</summary>
    [StringLength(500)]
    [JsonIgnore]
    public string? Token { get; set; }

    // === 审计字段映射（覆盖 BaseEntity 以适配 Vol 表结构） ===

    /// <summary>创建时间 → DB列 CreateDate</summary>
    [Column("CreateDate")]
    public new DateTime? CreateTime { get; set; }

    /// <summary>创建人 Code → DB列 Creator（存 UserCode）</summary>
    [Column("Creator")]
    [StringLength(200)]
    public new string? CreateBy { get; set; }

    /// <summary>更新时间 → DB列 ModifyDate</summary>
    [Column("ModifyDate")]
    public new DateTime? UpdateTime { get; set; }

    /// <summary>更新人 Code → DB列 Modifier（存 UserCode）</summary>
    [Column("Modifier")]
    [StringLength(200)]
    public new string? UpdateBy { get; set; }
}

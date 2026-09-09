using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using YZH.Core.Stand.Attributes;
using YZH.Core.Stand.Models;
using YZH.Core.Stand.Models.Entity;

namespace YZH.Core.Api.Models.Organization;

/// <summary>
///     组织机构实体
///     对应数据库表 Sys_Organization
///     支持树形结构：通过 ParentCode 构建层级关系
///     
///     预初始化三类根节点：
///     1. 系统管理员组（Platform）- 平台维护人员
///     2. 体系认证机构（CertBody）- 真实认证的机构
///     3. 虚拟机构（VirtualOrg）- 审核员注册形成的企业/审核机构
/// </summary>
[Table("Sys_Organization")]
[YZHDeleteStrategy(Mode = DeleteMode.Soft)]
public class Sys_Organization : BaseEntity, ITreeEntity
{
    /// <summary>机构名称</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(200)]
    [Display(Name = "机构名称")]
    [Column("org_name")]
    public string OrgName { get; set; } = string.Empty;

    /// <summary>机构编码（业务编号）</summary>
    [StringLength(100)]
    [Display(Name = "机构编码")]
    [Column("org_code")]
    public string? OrgCode { get; set; }

    /// <summary>父机构编码（根节点为 null）</summary>
    [StringLength(64)]
    [Display(Name = "上级机构")]
    [Column("parent_code")]
    public new string? ParentCode { get; set; }

    /// <summary>机构类型（Platform/CertBody/VirtualOrg/Dept）</summary>
    [StringLength(50)]
    [Display(Name = "机构类型")]
    [Column("org_type")]
    public string? OrgType { get; set; }

    /// <summary>机构层级（1=根，2=一级子机构，...）</summary>
    [Display(Name = "层级")]
    [Column("org_level")]
    public int? OrgLevel { get; set; }

    /// <summary>机构路径（如：/rootCode/parentCode/currentCode）</summary>
    [StringLength(500)]
    [Column("org_path")]
    public string? OrgPath { get; set; }

    /// <summary>负责人姓名</summary>
    [StringLength(50)]
    [Display(Name = "负责人")]
    [Column("leader_name")]
    public string? LeaderName { get; set; }

    /// <summary>负责人电话</summary>
    [StringLength(20)]
    [Display(Name = "联系电话")]
    [Column("leader_phone")]
    public string? LeaderPhone { get; set; }

    /// <summary>排序号</summary>
    [Display(Name = "排序")]
    [Column("sort")]
    public int? Sort { get; set; }

    /// <summary>是否启用（1=启用，0=禁用）</summary>
    [Required]
    [Display(Name = "是否启用")]
    [Column("enable")]
    public byte Enable { get; set; } = 1;

    /// <summary>备注</summary>
    [StringLength(500)]
    [Display(Name = "备注")]
    [Column("remark")]
    public string? Remark { get; set; }

    // === ITreeEntity 实现 ===

    /// <summary>是否叶子节点（后端批量计算，非持久化）</summary>
    [NotMapped]
    public new bool? IsLeaf { get; set; }
}

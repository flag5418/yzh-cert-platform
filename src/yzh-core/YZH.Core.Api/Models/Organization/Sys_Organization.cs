using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SqlSugar;
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
[SugarTable("Sys_Organization")]
[YZHDeleteStrategy(Mode = DeleteMode.Soft)]
public class Sys_Organization : BaseEntity, ITreeEntity
{
    /// <summary>机构名称</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(200)]
    [Display(Name = "机构名称")]
    public string OrgName { get; set; } = string.Empty;

    /// <summary>机构编码（业务编号）</summary>
    [StringLength(100)]
    [Display(Name = "机构编码")]
    public string? OrgCode { get; set; }

    /// <summary>父机构编码（根节点为 null）</summary>
    [StringLength(64)]
    [Display(Name = "上级机构")]
    public new string? ParentCode { get; set; }

    /// <summary>机构类型（Platform/CertBody/VirtualOrg/Dept）</summary>
    [StringLength(50)]
    [Display(Name = "机构类型")]
    public string? OrgType { get; set; }

    /// <summary>机构层级（1=根，2=一级子机构，...）</summary>
    [Display(Name = "层级")]
    public int? OrgLevel { get; set; }

    /// <summary>机构路径（如：/rootCode/parentCode/currentCode）</summary>
    [StringLength(500)]
    public string? OrgPath { get; set; }

    /// <summary>负责人姓名</summary>
    [StringLength(50)]
    [Display(Name = "负责人")]
    public string? LeaderName { get; set; }

    /// <summary>负责人电话</summary>
    [StringLength(20)]
    [Display(Name = "联系电话")]
    public string? LeaderPhone { get; set; }

    /// <summary>排序号</summary>
    [Display(Name = "排序")]
    public int? Sort { get; set; }

    /// <summary>是否启用（1=启用，0=禁用）</summary>
    [Required]
    [Display(Name = "是否启用")]
    public byte Enable { get; set; } = 1;

    /// <summary>备注</summary>
    [StringLength(500)]
    [Display(Name = "备注")]
    public string? Remark { get; set; }

    // === 审计字段覆盖（Sys_Organization 使用非标准列名） ===
    // 数据库列: CreateID, Creator, CreateDate, ModifyID, Modifier, ModifyDate, DeleteID, Deleter, DeleteTime
    // BaseEntity属性: Id, CreateBy, CreateTime, UpdateBy, UpdateTime, DeleteBy, DeleteTime, IsDeleted

    /// <summary>创建人（DB列: Creator）</summary>
    [SugarColumn(ColumnName = "Creator")]
    public new string? CreateBy { get; set; }

    /// <summary>创建时间（DB列: CreateDate）</summary>
    [SugarColumn(ColumnName = "CreateDate")]
    public new DateTime CreateTime { get; set; }

    /// <summary>更新人（DB列: Modifier）</summary>
    [SugarColumn(ColumnName = "Modifier")]
    public new string? UpdateBy { get; set; }

    /// <summary>更新时间（DB列: ModifyDate）</summary>
    [SugarColumn(ColumnName = "ModifyDate")]
    public new DateTime? UpdateTime { get; set; }

    /// <summary>删除人（DB列: Deleter）</summary>
    [SugarColumn(ColumnName = "Deleter")]
    public new string? DeleteBy { get; set; }

    // === 忽略 BaseEntity 中不存在的数据库列 ===

    /// <summary>PK - Sys_Organization 使用 Id (int auto_increment)，忽略 BaseEntity.Id (string)</summary>
    [SugarColumn(ColumnName = "Id", IsIgnore = true)]
    public new string Id { get; set; } = string.Empty;

    /// <summary>前端选中标记（忽略）</summary>
    [SugarColumn(IsIgnore = true)]
    public new bool CheckFlag { get; set; }

    /// <summary>前端删除标记（忽略）</summary>
    [SugarColumn(IsIgnore = true)]
    public new bool DeleteFlag { get; set; }

    /// <summary>数据版本号（忽略 - Sys_Organization 无此列）</summary>
    [SugarColumn(IsIgnore = true)]
    public new byte[]? RowVersion { get; set; }

    // === ITreeEntity 实现 ===

    /// <summary>是否叶子节点（后端批量计算，非持久化）</summary>
    [SugarColumn(IsIgnore = true)]
    public new bool? IsLeaf { get; set; }
}

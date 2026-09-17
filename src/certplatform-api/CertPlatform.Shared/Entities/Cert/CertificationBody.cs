using System;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Attributes;
using YZH.Core.Stand.Models.Entity;

namespace YZH.Entity.Admin.Platform.Cert
{
    /// <summary>
    /// 认证机构（业务主体 = 机构-Attach）
    ///
    /// <para>表名：cert_certification_body</para>
    /// <para>ORM：SqlSugar。DB 列名 == C# 属性名（PascalCase），符合《项目全局规则》§十六 命名铁律</para>
    ///
    /// <para>★ 机构-Attach 契约（数据权限试点）：</para>
    /// <list type="bullet">
    ///   <item><c>Sys_Organization</c> 是权限主体，本实体是业务扩展（Attach）</item>
    ///   <item><c>Code</c> == <c>OrgCode</c> == <c>Sys_Organization.Code</c>（单 Code 策略）</item>
    ///   <item>挂载点：<c>Sys_Organization</c> 中 <c>OrgType='CertBody'</c> 且 <c>ParentCode</c> 为空的根节点之下</item>
    ///   <item>同步规则：<c>Name→OrgName</c>、<c>CbCode→OrgCode</c>、<c>IsValid→Enable</c>（1↔1，0↔0）</item>
    /// </list>
    /// </summary>
    [SugarTable("cert_certification_body")]
    [YZHDeleteStrategy(Mode = DeleteMode.Soft)]
    public class CertificationBody : BaseEntity
    {
        // ========================================================
        // 主键 / 业务码
        // ========================================================

        /// <summary>主键（DB: bigint AUTO_INCREMENT）</summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public new long Id { get; set; }

        /// <summary>业务编码（GUID）。机构-Attach 契约：同时作为 Sys_Organization.Code</summary>
        [StringLength(36)]
        public new string Code { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>数据权限隔离键 → Sys_Organization.Code（单 Code 策略下 == Code）</summary>
        [StringLength(50)]
        [Display(Name = "机构主体")]
        public string? OrgCode { get; set; }

        // ========================================================
        // 业务字段
        // ========================================================

        /// <summary>机构名称</summary>
        [Required(AllowEmptyStrings = false)]
        [StringLength(200)]
        [Display(Name = "机构名称")]
        [UniqueField("机构名称")]
        public string Name { get; set; } = string.Empty;

        /// <summary>简称</summary>
        [StringLength(100)]
        [Display(Name = "简称")]
        public string? ShortName { get; set; }

        /// <summary>机构编号（CNAS 认可编号）</summary>
        [StringLength(50)]
        [Display(Name = "机构编号")]
        [UniqueField("机构编号")]
        public string? CbCode { get; set; }

        /// <summary>法人代表</summary>
        [StringLength(100)]
        [Display(Name = "法人代表")]
        public string? LegalPerson { get; set; }

        /// <summary>联系人</summary>
        [StringLength(50)]
        [Display(Name = "联系人")]
        public string? ContactName { get; set; }

        /// <summary>联系电话</summary>
        [StringLength(20)]
        [Display(Name = "联系电话")]
        public string? ContactPhone { get; set; }

        /// <summary>联系邮箱</summary>
        [StringLength(200)]
        [Display(Name = "联系邮箱")]
        public string? ContactEmail { get; set; }

        /// <summary>地址</summary>
        [StringLength(500)]
        [Display(Name = "地址")]
        public string? Address { get; set; }

        /// <summary>Logo 地址</summary>
        [StringLength(500)]
        [Display(Name = "Logo")]
        public string? LogoUrl { get; set; }

        /// <summary>认证范围</summary>
        [Display(Name = "认证范围")]
        public string? ScopeText { get; set; }

        /// <summary>主题配置（JSON）</summary>
        public string? ThemeConfig { get; set; }

        /// <summary>登录页配置（JSON）</summary>
        public string? LoginConfig { get; set; }

        /// <summary>最大用户数</summary>
        [Display(Name = "最大用户数")]
        public int MaxUsers { get; set; } = 100;

        /// <summary>最大企业数</summary>
        [Display(Name = "最大企业数")]
        public int MaxEnterprises { get; set; } = 1000;

        /// <summary>到期日期</summary>
        [Display(Name = "到期日期")]
        public DateTime? ExpireDate { get; set; }

        /// <summary>运营状态：active / suspended / inactive</summary>
        [StringLength(50)]
        [Display(Name = "运营状态")]
        public string Status { get; set; } = "active";

        /// <summary>排序号</summary>
        [Display(Name = "排序")]
        public int Sort { get; set; }

        /// <summary>备注</summary>
        [StringLength(500)]
        [Display(Name = "备注")]
        public string? Remark { get; set; }

        // ========================================================
        // 审计与软删除字段（必须用 new 重声明）
        //
        // ★ BaseEntity 把这些属性标了 [SugarColumn(IsIgnore = true)]，
        //   不重声明则：软删除列不落库、查询不过滤已删数据（§十六 16.5）
        // ========================================================

        /// <summary>有效标志（1=启用，0=禁用）。同步 Sys_Organization.Enable</summary>
        public int IsValid { get; set; } = 1;

        /// <summary>创建人 Code</summary>
        [StringLength(50)]
        public new string? CreateBy { get; set; }

        /// <summary>创建时间</summary>
        public new DateTime CreateTime { get; set; } = DateTime.UtcNow;

        /// <summary>更新人 Code</summary>
        [StringLength(50)]
        public new string? UpdateBy { get; set; }

        /// <summary>更新时间</summary>
        public new DateTime? UpdateTime { get; set; }

        /// <summary>删除人 Code（软删除）</summary>
        [StringLength(50)]
        public new string? DeleteBy { get; set; }

        /// <summary>删除时间（软删除）</summary>
        public new DateTime? DeleteTime { get; set; }

        /// <summary>软删除标记</summary>
        public bool IsDeleted { get; set; }
    }
}

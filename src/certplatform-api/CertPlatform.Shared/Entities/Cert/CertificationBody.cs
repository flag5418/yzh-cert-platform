using System;
using YZH.Entity.Admin.Platform;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Attributes;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Cert
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
    ///   <item>同步规则：<c>Name→OrgName</c>、<c>CbCode→OrgCode</c>、<c>IsValid→IsValid</c>（1↔1，0↔0）</item>
    /// </list>
    /// </summary>
    [SugarTable("cert_certification_body")]
    [YZHDeleteStrategy(Mode = DeleteMode.Soft)]
    public class CertificationBody : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id 已由 BaseEntity 基类统一提供 ────

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
        // 接口字段（BaseEntity 不包含，由接口继承提供）
        // ========================================================

        /// <summary>有效标志（1=启用，0=禁用）。同步 Sys_Organization.IsValid</summary>
        public int IsValid { get; set; } = 1;

        /// <summary>软删除标记（false=正常，true=已删除）</summary>
        public bool IsDeleted { get; set; }

        /// <summary>删除人 Code（仅软删除时赋值）</summary>
        public string? DeleteBy { get; set; }

        /// <summary>删除时间（仅软删除时赋值）</summary>
        public DateTime? DeleteTime { get; set; }
    }
}

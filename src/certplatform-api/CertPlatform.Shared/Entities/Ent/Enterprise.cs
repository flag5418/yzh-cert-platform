using System;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Models.Entity;

namespace YZH.Entity.Admin.Platform.Ent
{
    /// <summary>
    /// 企业
    /// <para>表名：ent_enterprise</para>
    /// <para>ORM：SqlSugar（§16 铁律：DB列名 == C#属性名，PascalCase）</para>
    /// </summary>
    [SugarTable("ent_enterprise")]
    public class Enterprise : BaseEntity
    {
        /// <summary>机构编码（所属认证机构，多租户隔离）</summary>
        [Required, StringLength(50)]
        public string OrgCode { get; set; }

        /// <summary>企业编号（如 ENT-2026-0001，用于OSS路径）</summary>
        [Required, StringLength(20)]
        [UniqueField("企业编号")]
        public string EnterpriseNo { get; set; }

        /// <summary>企业全称</summary>
        [Required, StringLength(200)]
        public string Name { get; set; }

        /// <summary>简称</summary>
        [StringLength(100)]
        public string ShortName { get; set; }

        /// <summary>统一社会信用代码</summary>
        [StringLength(50)]
        [UniqueField("统一社会信用代码")]
        public string CreditCode { get; set; }

        /// <summary>法人代表</summary>
        [StringLength(50)]
        public string LegalPerson { get; set; }

        /// <summary>省份</summary>
        [StringLength(50)]
        public string Province { get; set; }

        /// <summary>城市</summary>
        [StringLength(50)]
        public string City { get; set; }

        /// <summary>企业地址</summary>
        [StringLength(500)]
        public string Address { get; set; }

        /// <summary>行业类型</summary>
        [StringLength(100)]
        public string IndustryType { get; set; }

        /// <summary>员工人数</summary>
        public int? EmployeeCount { get; set; }

        /// <summary>认证范围描述</summary>
        public string CertScope { get; set; }

        /// <summary>对接人姓名</summary>
        [StringLength(50)]
        public string ContactName { get; set; }

        /// <summary>对接人电话</summary>
        [StringLength(20)]
        public string ContactPhone { get; set; }

        /// <summary>对接人邮箱</summary>
        [StringLength(200)]
        public string ContactEmail { get; set; }

        /// <summary>归档日期</summary>
        public DateTime? ArchiveDate { get; set; }
    }
}

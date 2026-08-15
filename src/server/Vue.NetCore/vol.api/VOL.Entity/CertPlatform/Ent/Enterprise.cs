using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Ent
{
    /// <summary>
    /// Enterprise 企业
    /// <para>表名：ent_enterprise</para>
    /// <para>列名规范：snake_case，通过 [Column] 特性映射</para>
    /// </summary>
    [Table("ent_enterprise")]
    public class Enterprise : YZHBaseEntity
    {
        /// <summary>机构编码（所属认证机构，多租户隔离）</summary>
        [Required, StringLength(50)]
        [Column("org_code")]
        public string OrgCode { get; set; }

        /// <summary>企业短编码(如 ENT-2026-0001，用于OSS路径)</summary>
        [Required, StringLength(20)]
        [Column("enterprise_no")]
        public string EnterpriseNo { get; set; }

        /// <summary>企业全称</summary>
        [Required, StringLength(200)]
        [Column("name")]
        public string Name { get; set; }

        /// <summary>简称</summary>
        [StringLength(100)]
        [Column("short_name")]
        public string ShortName { get; set; }

        /// <summary>统一社会信用代码</summary>
        [StringLength(50)]
        [Column("credit_code")]
        public string CreditCode { get; set; }

        /// <summary>法人代表</summary>
        [StringLength(50)]
        [Column("legal_person")]
        public string LegalPerson { get; set; }

        /// <summary>省份</summary>
        [StringLength(50)]
        [Column("province")]
        public string Province { get; set; }

        /// <summary>城市</summary>
        [StringLength(50)]
        [Column("city")]
        public string City { get; set; }

        /// <summary>企业地址</summary>
        [StringLength(500)]
        [Column("address")]
        public string Address { get; set; }

        /// <summary>行业类型</summary>
        [StringLength(100)]
        [Column("industry_type")]
        public string IndustryType { get; set; }

        /// <summary>员工人数</summary>
        [Column("employee_count")]
        public int? EmployeeCount { get; set; }

        /// <summary>认证范围描述</summary>
        [Column("cert_scope")]
        public string CertScope { get; set; }

        /// <summary>对接人姓名</summary>
        [StringLength(50)]
        [Column("contact_name")]
        public string ContactName { get; set; }

        /// <summary>对接人电话</summary>
        [StringLength(20)]
        [Column("contact_phone")]
        public string ContactPhone { get; set; }

        /// <summary>对接人邮箱</summary>
        [StringLength(200)]
        [Column("contact_email")]
        public string ContactEmail { get; set; }

        /// <summary>归档日期</summary>
        [Column("archive_date")]
        public DateTime? ArchiveDate { get; set; }

        // 注：Id, Code, OrgCode, CreateID, Creator, CreateDate, ModifyID, Modifier, ModifyDate,
        // DeleteID, Deleter, DeleteTime, Status, Enable, Remark 继承自 YZHBaseEntity
        // YZHBaseEntity 属性名是 PascalCase，但数据库列名是 snake_case
        // 需要在 YZHBaseEntity 中添加 [Column] 特性，或在子类中 override
    }
}

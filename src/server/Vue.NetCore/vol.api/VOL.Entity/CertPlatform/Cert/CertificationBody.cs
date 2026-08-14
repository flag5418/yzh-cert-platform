using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VOL.Entity;

namespace VOL.Entity.CertPlatform.Cert
{
    /// <summary>
    /// 认证机构
    /// <para>表名：cert_certification_body</para>
    /// <para>域：A - 认证体系配置</para>
    /// </summary>
    [Entity(TableCnName = "认证机构管理", TableName = "cert_certification_body", DBServer = "VOLContext")]
    [Table("cert_certification_body")]
    public class CertificationBody : YZHBaseEntity
    {
        [Required, StringLength(200)]
        [Editable(true)]
        [Column("name")]
        public string Name { get; set; }

        [StringLength(100)]
        [Editable(true)]
        [Column("short_name")]
        public string ShortName { get; set; }

        [StringLength(50)]
        [Editable(true)]
        [Column("cb_code")]
        public string CbCode { get; set; }

        [StringLength(100)]
        [Editable(true)]
        [Column("legal_person")]
        public string LegalPerson { get; set; }

        [StringLength(50)]
        [Editable(true)]
        [Column("contact_name")]
        public string ContactName { get; set; }

        [StringLength(20)]
        [Editable(true)]
        [Column("contact_phone")]
        public string ContactPhone { get; set; }

        [StringLength(200)]
        [Editable(true)]
        [Column("contact_email")]
        public string ContactEmail { get; set; }

        [StringLength(500)]
        [Editable(true)]
        [Column("address")]
        public string Address { get; set; }

        [StringLength(500)]
        [Editable(true)]
        [Column("logo_url")]
        public string LogoUrl { get; set; }

        [Column("scope_text")]
        public string ScopeText { get; set; }

        [Column("theme_config")]
        public string ThemeConfig { get; set; }

        [Column("login_config")]
        public string LoginConfig { get; set; }

        [Editable(true)]
        [Column("max_users")]
        public int MaxUsers { get; set; } = 100;

        [Editable(true)]
        [Column("max_enterprises")]
        public int MaxEnterprises { get; set; } = 1000;

        [Editable(true)]
        [Column("expire_date")]
        public DateTime? ExpireDate { get; set; }

        // Status, OrgCode, Code, CreateID, Creator, CreateDate, ModifyID, Modifier, ModifyDate,
        // DeleteID, Deleter, DeleteTime, Enable, Remark 继承自 YZHBaseEntity
    }
}

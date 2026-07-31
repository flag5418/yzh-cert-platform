using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Cert
{
    /// <summary>
    /// 认证机构
    /// <para>表名：cert_certification_body</para>
    /// <para>域：A - 认证体系配置</para>
    /// <para>继承：YZHBaseEntity（统一审计字段）</para>
    /// </summary>
    [Table("cert_certification_body")]
    public class CertificationBody : YZHBaseEntity
    {
        /// <summary>
        /// 机构全称
        /// </summary>
        [Required]
        [StringLength(200)]
        [Column("name")]
        public string Name { get; set; }

        /// <summary>
        /// 简称
        /// </summary>
        [StringLength(100)]
        [Column("short_name")]
        public string ShortName { get; set; }

        /// <summary>
        /// CNAS 认可编号
        /// </summary>
        [StringLength(50)]
        [Column("cb_code")]
        public string CbCode { get; set; }

        /// <summary>
        /// 是否启用
        /// </summary>
        [Column("status")]
        public string Status { get; set; } = "active";

        /// <summary>
        /// 联系人
        /// </summary>
        [StringLength(50)]
        [Column("contact_name")]
        public string ContactName { get; set; }

        /// <summary>
        /// 联系电话
        /// </summary>
        [StringLength(20)]
        [Column("contact_phone")]
        public string ContactPhone { get; set; }

        /// <summary>
        /// 备注
        /// </summary>
        [Column("notes")]
        public string Notes { get; set; }
    }
}

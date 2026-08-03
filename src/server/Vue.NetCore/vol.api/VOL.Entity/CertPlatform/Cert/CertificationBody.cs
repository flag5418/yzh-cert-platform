using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VOL.Entity;
using VOL.Entity.CertPlatform;

namespace VOL.Entity.CertPlatform.Cert
{
    /// <summary>
    /// 认证机构
    /// <para>表名：cert_certification_body</para>
    /// <para>域：A - 认证体系配置</para>
    /// <para>继承：YZHBaseEntity（统一审计字段）</para>
    /// </summary>
    [Entity(TableCnName = "认证机构管理", TableName = "cert_certification_body", DBServer = "VOLContext")]
    [Table("cert_certification_body")]
    public class CertificationBody : YZHBaseEntity
    {
        /// <summary>
        /// 机构全称
        /// </summary>
        [Required]
        [StringLength(200)]
        public string Name { get; set; }

        /// <summary>
        /// 简称
        /// </summary>
        [StringLength(100)]
        public string ShortName { get; set; }

        /// <summary>
        /// CNAS 认可编号
        /// </summary>
        [StringLength(50)]
        public string CbCode { get; set; }

        /// <summary>
        /// 联系人
        /// </summary>
        [StringLength(50)]
        public string ContactName { get; set; }

        /// <summary>
        /// 联系电话
        /// </summary>
        [StringLength(20)]
        public string ContactPhone { get; set; }
    }
}

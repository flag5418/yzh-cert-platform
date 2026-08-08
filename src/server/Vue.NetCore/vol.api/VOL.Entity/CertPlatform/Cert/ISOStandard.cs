using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VOL.Entity;

namespace VOL.Entity.CertPlatform.Cert
{
    /// <summary>
    /// ISO 标准
    /// <para>表名：cert_iso_standard</para>
    /// <para>域：A - 认证体系配置</para>
    /// </summary>
    [Entity(TableCnName = "ISO标准管理", TableName = "cert_iso_standard", DBServer = "VOLContext")]
    [Table("cert_iso_standard")]
    public class ISOStandard : YZHBaseEntity
    {
        /// <summary>
        /// 标准编号（如 ISO 9001:2015, ISO 13485:2016）
        /// </summary>
        [Required]
        [StringLength(50)]
        [Editable(true)]
        public string StandardCode { get; set; }

        /// <summary>
        /// 标准中文名称
        /// </summary>
        [Required]
        [StringLength(200)]
        [Editable(true)]
        public string StandardName { get; set; }

        /// <summary>
        /// 版本年份
        /// </summary>
        [Editable(true)]
        public int VersionYear { get; set; }
    }
}

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VOL.Entity;
using VOL.Entity.CertPlatform;

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
        /// 所属认证机构编码（关联 CertificationBody.Code）
        /// </summary>
        [Required]
        [StringLength(36)]
        
        public string CbCode { get; set; }

        /// <summary>
        /// 标准编号（如 ISO 9001:2015）
        /// </summary>
        [Required]
        [StringLength(50)]
        
        public string StandardCode { get; set; }

        /// <summary>
        /// 标准中文名称
        /// </summary>
        [Required]
        [StringLength(200)]
        
        public string StandardName { get; set; }

        /// <summary>
        /// 版本年份
        /// </summary>
        
        public int VersionYear { get; set; }
    }
}

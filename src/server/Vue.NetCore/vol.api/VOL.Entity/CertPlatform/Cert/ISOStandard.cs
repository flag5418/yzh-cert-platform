using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Cert
{
    /// <summary>
    /// ISO 标准
    /// <para>表名：cert_iso_standard</para>
    /// <para>域：A - 认证体系配置</para>
    /// </summary>
    [Table("cert_iso_standard")]
    public class ISOStandard : BaseEntity
    {
        /// <summary>
        /// 所属认证机构编码（关联 CertificationBody.Code）
        /// </summary>
        [Required]
        [StringLength(36)]
        [Column("cb_code")]
        public string CbCode { get; set; }

        /// <summary>
        /// 标准编号（如 ISO 9001:2015）
        /// </summary>
        [Required]
        [StringLength(50)]
        [Column("standard_code")]
        public string StandardCode { get; set; }

        /// <summary>
        /// 标准中文名称
        /// </summary>
        [Required]
        [StringLength(200)]
        [Column("standard_name")]
        public string StandardName { get; set; }

        /// <summary>
        /// 版本年份
        /// </summary>
        [Column("version_year")]
        public int VersionYear { get; set; }

        /// <summary>
        /// 实施状态：implemented-已实施, pending-待实施, deprecated-已废弃
        /// </summary>
        [Column("status")]
        public string Status { get; set; } = "pending";

        /// <summary>
        /// 备注
        /// </summary>
        [Column("notes")]
        public string Notes { get; set; }
    }
}

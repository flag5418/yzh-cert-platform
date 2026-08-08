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
    /// <para>继承：YZHBaseEntity（统一审计字段）</para>
    /// </summary>
    [Entity(TableCnName = "认证机构管理", TableName = "cert_certification_body", DBServer = "VOLContext")]
    [Table("cert_certification_body")]
    public class CertificationBody : YZHBaseEntity
    {
        /// <summary>
        /// 机构全称（必填）
        /// </summary>
        [Required]
        [StringLength(200)]
        [Editable(true)]  // Vol 框架要求：标记为可编辑列，否则保存时 ValidateDicInEntity 会报"没有配置好Model为编辑列"
        public string Name { get; set; }

        /// <summary>
        /// 简称
        /// </summary>
        [StringLength(100)]
        [Editable(true)]
        public string ShortName { get; set; }

        /// <summary>
        /// CNAS 认可编号
        /// </summary>
        [StringLength(50)]
        [Editable(true)]
        public string CbCode { get; set; }

        /// <summary>
        /// 联系人
        /// </summary>
        [StringLength(50)]
        [Editable(true)]
        public string ContactName { get; set; }

        /// <summary>
        /// 联系电话
        /// </summary>
        [StringLength(20)]
        [Editable(true)]
        public string ContactPhone { get; set; }

        /// <summary>
        /// 排序号（数字越小越靠前）
        /// </summary>
        [Editable(true)]
        public int Sort { get; set; } = 0;
    }
}

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VOL.Entity;

namespace VOL.Entity.CertPlatform.Sys
{
    /// <summary>
    /// 机构-标准关联表（多对多）
    /// <para>表名：cert_org_standard</para>
    /// </summary>
    [Entity(TableCnName = "机构-标准关联", TableName = "cert_org_standard", DBServer = "VOLContext")]
    [Table("cert_org_standard")]
    public class CertOrgStandard : YZHBaseEntity
    {
        /// <summary>
        /// 标准编码（关联 cert_iso_standard.code）
        /// </summary>
        [Required]
        [StringLength(100)]
        [Column("standard_code")]
        public string StdCode { get; set; }
    }
}

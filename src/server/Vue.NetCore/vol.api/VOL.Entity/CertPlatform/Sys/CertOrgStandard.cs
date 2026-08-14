using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VOL.Entity;

namespace VOL.Entity.CertPlatform.Sys
{
    /// <summary>
    /// 机构-标准关联表（多对多）
    /// <para>表名：cert_org_standard</para>
    /// <para>V3 架构：用 org_code + standard_code 关联，不再用 cb_code + std_id</para>
    /// </summary>
    [Entity(TableCnName = "机构-标准关联", TableName = "cert_org_standard", DBServer = "VOLContext")]
    [Table("cert_org_standard")]
    public class CertOrgStandard : YZHBaseEntity
    {
        /// <summary>
        /// 机构编码（关联 cert_certification_body.code）
        /// </summary>
        [Required]
        [StringLength(50)]
        [Column("org_code")]
        public string OrgCode { get; set; }

        /// <summary>
        /// 标准编码（关联 cert_iso_standard.code）
        /// </summary>
        [Required]
        [StringLength(50)]
        [Column("standard_code")]
        public string StdCode { get; set; }
    }
}

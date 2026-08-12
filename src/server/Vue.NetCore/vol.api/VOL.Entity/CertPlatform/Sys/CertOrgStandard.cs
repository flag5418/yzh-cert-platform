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
        [Required]
        [StringLength(50)]
        public string CbCode { get; set; }

        [Required]
        public long StdId { get; set; }

        [Required]
        [StringLength(100)]
        public string StdCode { get; set; }

        public DateTime EnabledAt { get; set; } = DateTime.Now;
    }
}

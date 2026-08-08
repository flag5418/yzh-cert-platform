using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VOL.Entity;
using VOL.Entity.SystemModels;

namespace VOL.Entity.CertPlatform.Sys
{
    /// <summary>
    /// 机构-标准关联表（多对多）
    /// <para>表名：cert_org_standard</para>
    /// </summary>
    [Entity(TableCnName = "机构-标准关联", TableName = "cert_org_standard", DBServer = "VOLContext")]
    [Table("cert_org_standard")]
    public class CertOrgStandard : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Required]
        [StringLength(50)]
        public string CbCode { get; set; }

        [Required]
        public long StdId { get; set; }

        [Required]
        [StringLength(100)]
        public string StdCode { get; set; }

        public DateTime EnabledAt { get; set; } = DateTime.Now;

        [StringLength(500)]
        public string Remark { get; set; }
    }
}

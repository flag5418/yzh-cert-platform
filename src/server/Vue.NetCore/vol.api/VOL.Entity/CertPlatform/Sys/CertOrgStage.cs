using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VOL.Entity;
using VOL.Entity.SystemModels;

namespace VOL.Entity.CertPlatform.Sys
{
    /// <summary>
    /// 机构-阶段关联表（多对多）
    /// <para>表名：cert_org_stage</para>
    /// <para>新建机构时自动插入全部阶段（默认全选策略）</para>
    /// </summary>
    [Entity(TableCnName = "机构-阶段关联", TableName = "cert_org_stage", DBServer = "VOLContext")]
    [Table("cert_org_stage")]
    public class CertOrgStage : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Required]
        [StringLength(50)]
        public string CbCode { get; set; }

        [Required]
        public long StageId { get; set; }

        [Required]
        [StringLength(50)]
        public string StageCode { get; set; }

        public DateTime EnabledAt { get; set; } = DateTime.Now;

        [StringLength(500)]
        public string Remark { get; set; }
    }
}

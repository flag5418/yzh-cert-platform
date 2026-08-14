using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VOL.Entity;

namespace VOL.Entity.CertPlatform.Sys
{
    /// <summary>
    /// 机构-阶段关联表（多对多）
    /// <para>表名：cert_org_stage</para>
    /// </summary>
    [Entity(TableCnName = "机构-阶段关联", TableName = "cert_org_stage", DBServer = "VOLContext")]
    [Table("cert_org_stage")]
    public class CertOrgStage : YZHBaseEntity
    {
        /// <summary>
        /// 标准编码
        /// </summary>
        [Required]
        [StringLength(100)]
        [Column("standard_code")]
        public string StdCode { get; set; }

        /// <summary>
        /// 阶段编码
        /// </summary>
        [Required]
        [StringLength(50)]
        [Column("phase_code")]
        public string StageCode { get; set; }
    }
}

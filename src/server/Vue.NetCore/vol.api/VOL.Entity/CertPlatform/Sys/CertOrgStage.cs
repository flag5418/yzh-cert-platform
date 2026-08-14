using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VOL.Entity;

namespace VOL.Entity.CertPlatform.Sys
{
    /// <summary>
    /// 机构-阶段关联表（多对多）
    /// <para>表名：cert_org_stage</para>
    /// <para>V3 架构：用 org_code + phase_code 关联，不再用 cb_code + stage_id</para>
    /// </summary>
    [Entity(TableCnName = "机构-阶段关联", TableName = "cert_org_stage", DBServer = "VOLContext")]
    [Table("cert_org_stage")]
    public class CertOrgStage : YZHBaseEntity
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
        [StringLength(50)]
        [Column("standard_code")]
        public string StdCode { get; set; }

        /// <summary>
        /// 阶段编码（关联 cert_cert_stage.phase_code）
        /// </summary>
        [Required]
        [StringLength(50)]
        [Column("phase_code")]
        public string StageCode { get; set; }
    }
}

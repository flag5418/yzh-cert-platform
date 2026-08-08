using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VOL.Entity;

namespace VOL.Entity.CertPlatform.Cert
{
    /// <summary>
    /// 认证阶段（全局基础资料）
    /// <para>表名：cert_cert_stage</para>
    /// <para>域：A - 认证体系配置</para>
    /// <para>设计依据：ISO/IEC 17021-1:2015 规定的认证流程阶段</para>
    /// </summary>
    [Entity(TableCnName = "认证阶段", TableName = "cert_cert_stage", DBServer = "VOLContext")]
    [Table("cert_cert_stage")]
    public class CertStage : YZHBaseEntity
    {
        /// <summary>
        /// 阶段编码（如 STAGE-01 ~ STAGE-09）
        /// </summary>
        [Required]
        [StringLength(50)]
        [Editable(true)]
        public string StageCode { get; set; }

        /// <summary>
        /// 阶段名称（如 申请受理、合同评审、审核方案策划、第一阶段审核、第二阶段审核、认证决定、颁发证书、监督审核、再认证）
        /// </summary>
        [Required]
        [StringLength(200)]
        [Editable(true)]
        public string StageName { get; set; }

        /// <summary>
        /// 排序号（决定流程顺序，越小越靠前）
        /// </summary>
        [Editable(true)]
        public int SortOrder { get; set; }

        /// <summary>
        /// 分类：process=流程阶段, audit=审核类型, post=证后
        /// </summary>
        [StringLength(50)]
        [Editable(true)]
        public string Category { get; set; } = "process";
    }
}

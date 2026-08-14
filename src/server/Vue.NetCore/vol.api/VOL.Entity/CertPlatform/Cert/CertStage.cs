using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VOL.Entity;

namespace VOL.Entity.CertPlatform.Cert
{
    /// <summary>
    /// 认证阶段（全局基础资料）
    /// <para>表名：cert_cert_stage</para>
    /// </summary>
    [Entity(TableCnName = "认证阶段", TableName = "cert_cert_stage", DBServer = "VOLContext")]
    [Table("cert_cert_stage")]
    public class CertStage : YZHBaseEntity
    {
        /// <summary>
        /// 阶段编码（对应数据库 phase_code 列）
        /// </summary>
        [Required]
        [StringLength(50)]
        [Editable(true)]
        [Column("phase_code")]
        public string StageCode { get; set; }

        /// <summary>
        /// 阶段名称（对应数据库 phase_name 列）
        /// </summary>
        [Required]
        [StringLength(200)]
        [Editable(true)]
        [Column("phase_name")]
        public string StageName { get; set; }

        /// <summary>
        /// 描述
        /// </summary>
        [Column("description")]
        public string Description { get; set; }

        /// <summary>
        /// 排序号
        /// </summary>
        [Editable(true)]
        [Column("sort_order")]
        public int SortOrder { get; set; }

        /// <summary>
        /// 分类
        /// </summary>
        [StringLength(50)]
        [Editable(true)]
        [Column("category")]
        public string Category { get; set; } = "process";
    }
}

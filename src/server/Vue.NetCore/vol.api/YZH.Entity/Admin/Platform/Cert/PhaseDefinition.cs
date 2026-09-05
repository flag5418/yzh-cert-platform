using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using YZH.Entity.Admin.Platform;

namespace YZH.Entity.Admin.Platform.Cert
{
    /// <summary>
    /// 阶段定义
    /// <para>表名：cert_phase_definition</para>
    /// <para>域：A - 认证体系配置</para>
    /// </summary>
    [Table("cert_phase_definition")]
    public class PhaseDefinition : EntityBase
    {
        /// <summary>
        /// 阶段编码（S1/S2/Surv1/Surv2/Recert）
        /// </summary>
        [Required]
        [StringLength(20)]
        [UniqueField("阶段编码")]
        public string PhaseCode { get; set; }

        /// <summary>
        /// 中文名称
        /// </summary>
        [Required]
        [StringLength(100)]
        public string PhaseName { get; set; }

        /// <summary>
        /// 顺序（1=S1 2=S2 3=一监 4=二监 5=再认证）
        /// </summary>
        public int SequenceOrder { get; set; }

        /// <summary>
        /// 阶段说明
        /// </summary>
        public string Description { get; set; }
    }
}

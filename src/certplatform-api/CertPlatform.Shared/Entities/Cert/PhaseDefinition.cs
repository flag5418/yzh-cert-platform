using System;
using YZH.Entity.Admin.Platform;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Cert
{
    /// <summary>
    /// 阶段定义
    /// <para>表名：cert_phase_definition</para>
    /// <para>ORM：SqlSugar（§16 铁律：DB列名 == C#属性名，PascalCase）</para>
    /// </summary>
    [SugarTable("cert_phase_definition")]
    public class PhaseDefinition : BaseEntity
    {
        /// <summary>阶段编码（S1/S2/Surv1/Surv2/Recert）</summary>
        [Required]
        [StringLength(20)]
        [UniqueField("阶段编码")]
        public string PhaseCode { get; set; }

        /// <summary>中文名称</summary>
        [Required]
        [StringLength(100)]
        public string PhaseName { get; set; }

        /// <summary>顺序（1=S1 2=S2 3=一监 4=二监 5=再认证）</summary>
        public int SequenceOrder { get; set; }

        /// <summary>阶段说明</summary>
        public string Description { get; set; }
    }
}

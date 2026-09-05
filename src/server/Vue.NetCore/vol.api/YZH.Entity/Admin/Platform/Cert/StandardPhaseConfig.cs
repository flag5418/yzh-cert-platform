using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using YZH.Entity.Admin.Platform;

namespace YZH.Entity.Admin.Platform.Cert
{
    /// <summary>
    /// StandardPhaseConfig 标准-阶段配置
    /// <para>表名：cert_standard_phase_config</para>
    /// </summary>
    [Table("cert_standard_phase_config")]
    public class StandardPhaseConfig : EntityBase
    {
        [Required, StringLength(36)]
        [UniqueField("标准编码", WithFields = new[] { "PhaseCode" })]
        public string StandardCode { get; set; }

        [Required, StringLength(36)]
        public string PhaseCode { get; set; }

        public string RequiredClauses { get; set; }

        public string RequiredFiles { get; set; }
    }
}

using System;
using YZH.Entity.Admin.Platform;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Cert
{
    /// <summary>
    /// StandardPhaseConfig 标准-阶段配置
    /// <para>表名：cert_standard_phase_config</para>
    /// </summary>
    [SugarTable("cert_standard_phase_config")]
    public class StandardPhaseConfig : BaseEntity
    {
        // ──── Id / 审计字段由 BaseEntity 基类统一提供 ────

        // ──── 业务字段 ────
        [Required, StringLength(36)]
        [UniqueField("标准编码", WithFields = new[] { "PhaseCode" })]
        public string StandardCode { get; set; }

        [Required, StringLength(36)]
        public string PhaseCode { get; set; }

        public string? RequiredClauses { get; set; }

        public string? RequiredFiles { get; set; }
    }
}

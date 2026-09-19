using System;
using YZH.Entity.Admin.Platform;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Cert
{
    /// <summary>
    /// ExtractionRule 提取规则
    /// <para>表名：cert_extraction_rule</para>
    /// </summary>
    [SugarTable("cert_extraction_rule")]
    public class ExtractionRule : BaseEntity
    {
        // ──── Id / 审计字段由 BaseEntity 基类统一提供 ────

        // ──── 业务字段 ────
        [Required, StringLength(36)]
        public string FileRequirementCode { get; set; }

        [Required, StringLength(36)]
        [UniqueField("技能编码", WithFields = new[] { "FileRequirementCode" })]
        public string SkillCode { get; set; }

        [Required, StringLength(20)]
        public string RuleType { get; set; }

        [Required]
        public string RuleConfig { get; set; }

        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;
    }
}

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YZH.Entity.Admin.Platform.Cert
{
    /// <summary>
    /// ExtractionRule 提取规则
    /// <para>表名：cert_extraction_rule</para>
    /// </summary>
    [Table("cert_extraction_rule")]
    public class ExtractionRule : EntityBase
    {
        [Required, StringLength(36)]
        public string FileRequirementCode { get; set; }

        [Required, StringLength(36)]
        [UniqueField("技能编码", WithFields = new[] { "FileRequirementCode" })]
        public string SkillCode { get; set; }

        [Required, StringLength(20)]
        public string RuleType { get; set; }

        [Required]
        public string RuleConfig { get; set; }

        public string Description { get; set; }

        public bool IsActive { get; set; } = true;
    }
}

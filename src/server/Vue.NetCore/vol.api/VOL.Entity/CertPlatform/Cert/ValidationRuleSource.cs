using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Cert
{
    /// <summary>
    /// ValidationRuleSource 验证规则来源
    /// <para>表名：cert_validation_rule_source</para>
    /// </summary>
    [Table("cert_validation_rule_source")]
    public class ValidationRuleSource : YZHBaseEntity
    {
        [Required, StringLength(36)]
        [Column("rule_code")]
        public string RuleCode { get; set; }

        [Required, StringLength(36)]
        [Column("file_requirement_code")]
        public string FileRequirementCode { get; set; }

        [StringLength(500)]
        [Column("source_path")]
        public string SourcePath { get; set; }
    }
}

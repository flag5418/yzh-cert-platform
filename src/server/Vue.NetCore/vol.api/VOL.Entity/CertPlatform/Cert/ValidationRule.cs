using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Cert
{
    /// <summary>
    /// ValidationRule 验证规则
    /// <para>表名：cert_validation_rule</para>
    /// </summary>
    [Table("cert_validation_rule")]
    public class ValidationRule : YZHBaseEntity
    {
        [Required, StringLength(36)]
        [Column("standard_code")]
        public string StandardCode { get; set; }

        [Required, StringLength(36)]
        [Column("phase_code")]
        public string PhaseCode { get; set; }

        [Required, StringLength(36)]
        [Column("clause_code")]
        public string ClauseCode { get; set; }

        [Required, StringLength(36)]
        [Column("workflow_code")]
        public string WorkflowCode { get; set; }

        [Required, StringLength(50)]
        [Column("rule_code")]
        public string RuleCode { get; set; }

        [Required, StringLength(200)]
        [Column("rule_name")]
        public string RuleName { get; set; }

        [Required, StringLength(20)]
        [Column("severity_if_violated")]
        public string SeverityIfViolated { get; set; }

        [Column("nc_description_template")]
        public string NcDescriptionTemplate { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;
    }
}

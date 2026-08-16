using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Cert
{
    /// <summary>
    /// ValidationRule 审核规则（NC检查项 + 报告章节共用基础表）
    /// <para>表名：cert_validation_rule</para>
    /// </summary>
    [Table("cert_validation_rule")]
    public class ValidationRule : YZHBaseEntity
    {
        [StringLength(50)]
        [Column("org_code")]
        public string OrgCode { get; set; }

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

        [StringLength(200)]
        [Column("rule_name_en")]
        public string RuleNameEn { get; set; }

        [Required, StringLength(20)]
        [Column("severity_if_violated")]
        public string SeverityIfViolated { get; set; }

        [Column("rule_json")]
        public string RuleJson { get; set; }

        [Column("nc_description_template")]
        public string NcDescriptionTemplate { get; set; }

        [Column("remark")]
        public string Remark { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;
    }
}

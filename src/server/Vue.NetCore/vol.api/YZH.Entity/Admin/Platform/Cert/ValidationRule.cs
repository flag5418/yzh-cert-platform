using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using YZH.Entity.Admin.Platform;

namespace YZH.Entity.Admin.Platform.Cert
{
    /// <summary>
    /// ValidationRule 审核规则（NC检查项 + 报告章节共用基础表）
    /// <para>表名：cert_validation_rule</para>
    /// </summary>
    [Table("cert_validation_rule")]
    public class ValidationRule : EntityBase
    {
        [StringLength(50)]
        public string OrgCode { get; set; }

        [Required, StringLength(36)]
        public string StandardCode { get; set; }

        [Required, StringLength(36)]
        public string PhaseCode { get; set; }

        [Required, StringLength(36)]
        public string ClauseCode { get; set; }

        [Required, StringLength(36)]
        public string WorkflowCode { get; set; }

        [Required, StringLength(50)]
        [UniqueField("规则编码")]
        public string RuleCode { get; set; }

        [Required, StringLength(200)]
        public string RuleName { get; set; }

        [StringLength(200)]
        public string RuleNameEn { get; set; }

        [Required, StringLength(20)]
        public string SeverityIfViolated { get; set; }

        public string RuleJson { get; set; }

        /// <summary>画布布局 JSON（节点坐标/缩放/平移，UI 恢复用，解释器不读）</summary>
        public string LayoutJson { get; set; }

        public string NcDescriptionTemplate { get; set; }

        public string Remark { get; set; }

        public bool IsActive { get; set; } = true;
    }
}

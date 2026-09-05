using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YZH.Entity.Admin.Platform.Cert
{
    /// <summary>
    /// ReportTemplate 报告模板
    /// <para>表名：cert_report_template</para>
    /// </summary>
    [Table("cert_report_template")]
    public class ReportTemplate : EntityBase
    {
        [Required, StringLength(36)]
        public string CbCode { get; set; }

        [StringLength(50)]
        public string OrgCode { get; set; }

        [Required, StringLength(36)]
        public string StandardCode { get; set; }

        [Required, StringLength(36)]
        public string PhaseCode { get; set; }

        [Required, StringLength(200)]
        [UniqueField("模板名称", WithFields = new[] { "CbCode", "StandardCode", "PhaseCode" })]
        public string TemplateName { get; set; }

        [StringLength(500)]
        public string TemplateFilePath { get; set; }

        public string SectionConfig { get; set; }

        public bool IsDefault { get; set; } = false;
    }
}

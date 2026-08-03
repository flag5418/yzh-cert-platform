using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Cert
{
    /// <summary>
    /// ReportTemplate
    /// <para>表名：cert_report_template</para>
    /// </summary>
    [Table("cert_report_template")]
    public class ReportTemplate : YZHBaseEntity
    {

    [Required][StringLength(36)]
    public string CbCode { get; set; }
    [Required][StringLength(36)]
    public string StandardCode { get; set; }
    [Required][StringLength(36)]
    public string PhaseCode { get; set; }
    [Required][StringLength(200)]
    public string TemplateName { get; set; }
    [StringLength(500)]
    public string TemplateFilePath { get; set; }
    
    public string SectionConfig { get; set; }
    
    public bool IsDefault { get; set; } = false;

    }
}

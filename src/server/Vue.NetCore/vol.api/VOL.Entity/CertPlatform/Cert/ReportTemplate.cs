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

    [Required][StringLength(36)][Column("cb_code")]
    public string CbCode { get; set; }
    [Required][StringLength(36)][Column("standard_code")]
    public string StandardCode { get; set; }
    [Required][StringLength(36)][Column("phase_code")]
    public string PhaseCode { get; set; }
    [Required][StringLength(200)][Column("template_name")]
    public string TemplateName { get; set; }
    [StringLength(500)][Column("template_file_path")]
    public string TemplateFilePath { get; set; }
    [Column("section_config")]
    public string SectionConfig { get; set; }
    [Column("is_default")]
    public bool IsDefault { get; set; } = false;

    }
}

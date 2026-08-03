using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Cert
{
    /// <summary>
    /// ExtractionField
    /// <para>表名：cert_extraction_field</para>
    /// </summary>
    [Table("cert_extraction_field")]
    public class ExtractionField : YZHBaseEntity
    {

    [Required][StringLength(36)]
    public string RuleCode { get; set; }
    [StringLength(36)]
    public string SkillCode { get; set; }
    [Required][StringLength(100)]
    public string FieldCode { get; set; }
    [Required][StringLength(500)]
    public string LabelTag { get; set; }
    [Required][StringLength(100)]
    public string FieldName { get; set; }
    
    public string FieldType { get; set; } = "string";
    
    public string EnumValues { get; set; }
    
    public int SortOrder { get; set; } = 0;

    }
}

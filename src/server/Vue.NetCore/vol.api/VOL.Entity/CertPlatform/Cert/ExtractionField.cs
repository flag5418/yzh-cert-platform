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
    public class ExtractionField : BaseEntity
    {

    [Required][StringLength(36)][Column("rule_code")]
    public string RuleCode { get; set; }
    [StringLength(36)][Column("skill_code")]
    public string SkillCode { get; set; }
    [Required][StringLength(100)][Column("field_code")]
    public string FieldCode { get; set; }
    [Required][StringLength(500)][Column("label_tag")]
    public string LabelTag { get; set; }
    [Required][StringLength(100)][Column("field_name")]
    public string FieldName { get; set; }
    [Column("field_type")]
    public string FieldType { get; set; } = "string";
    [Column("enum_values")]
    public string EnumValues { get; set; }
    [Column("sort_order")]
    public int SortOrder { get; set; } = 0;

    }
}

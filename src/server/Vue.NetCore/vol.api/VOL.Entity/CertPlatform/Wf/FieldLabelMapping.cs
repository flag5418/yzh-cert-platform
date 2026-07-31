using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Wf
{
    /// <summary>
    /// FieldLabelMapping
    /// <para>表名：wf_field_label_mapping</para>
    /// </summary>
    [Table("wf_field_label_mapping")]
    public class FieldLabelMapping : YZHBaseEntity
    {

    [Required][StringLength(500)][Column("label_tag")]
    public string LabelTag { get; set; }
    [Required][StringLength(200)][Column("field_code")]
    public string FieldCode { get; set; }
    [Required][StringLength(36)][Column("standard_code")]
    public string StandardCode { get; set; }
    [StringLength(100)][Column("scope_level")]
    public string ScopeLevel { get; set; }
    [StringLength(200)][Column("document_name")]
    public string DocumentName { get; set; }
    [StringLength(100)][Column("field_name")]
    public string FieldName { get; set; }
    [StringLength(50)][Column("data_type")]
    public string DataType { get; set; }
    [StringLength(36)][Column("skill_code")]
    public string SkillCode { get; set; }
    [Column("description")]
    public string Description { get; set; }

    }
}

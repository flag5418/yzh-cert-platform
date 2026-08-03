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

    [Required][StringLength(500)]
    public string LabelTag { get; set; }
    [Required][StringLength(200)]
    public string FieldCode { get; set; }
    [Required][StringLength(36)]
    public string StandardCode { get; set; }
    [StringLength(100)]
    public string ScopeLevel { get; set; }
    [StringLength(200)]
    public string DocumentName { get; set; }
    [StringLength(100)]
    public string FieldName { get; set; }
    [StringLength(50)]
    public string DataType { get; set; }
    [StringLength(36)]
    public string SkillCode { get; set; }
    
    public string Description { get; set; }

    }
}

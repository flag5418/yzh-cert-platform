using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Wf
{
    /// <summary>
    /// FieldLabelMapping — 已废弃（V4 评审报告 §3.1 / §四 #1）
    /// <para>表名：wf_field_label_mapping</para>
    /// <para>废弃原因：V4 定调不建此表；label_tag 概念已废弃，改用 field_code 作为查询键</para>
    /// </summary>
    [Obsolete("V4 废弃：wf_field_label_mapping 不建，label_tag 改为 field_name，查询改用 field_code")]
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

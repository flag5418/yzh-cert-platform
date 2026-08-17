using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Cert
{
    /// <summary>
    /// ExtractionField 提取字段定义
    /// <para>表名：cert_extraction_field</para>
    /// </summary>
    [Table("cert_extraction_field")]
    public class ExtractionField : YZHBaseEntity
    {
        [Required, StringLength(36)]
        [Column("rule_code")]
        public string RuleCode { get; set; }

        [StringLength(36)]
        [Column("skill_code")]
        public string SkillCode { get; set; }

        [Required, StringLength(100)]
        [Column("field_code")]
        public string FieldCode { get; set; }

        // label_tag 列已删除（方案 C 整改）：原 label_tag 与 field_code 语义冗余，
        // 工作流 get_field 节点改用 field_code 作为查询键，field_name 存中文名展示用

        [Required, StringLength(100)]
        [Column("field_name")]
        public string FieldName { get; set; }

        [StringLength(20)]
        [Column("field_type")]
        public string FieldType { get; set; } = "string";

        [Column("enum_values")]
        public string EnumValues { get; set; }

        [Column("sort_order")]
        public int SortOrder { get; set; } = 0;
    }
}

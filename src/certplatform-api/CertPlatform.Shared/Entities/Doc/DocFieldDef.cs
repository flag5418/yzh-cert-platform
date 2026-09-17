using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SqlSugar;
using YZH.Entity.Admin.Platform;

namespace YZH.Entity.Admin.Platform.Doc
{
    /// <summary>
    /// DocFieldDef 文档字段定义
    /// <para>表名：cert_doc_field_def（唯一键 uk_rule_field: rule_code + field_code）</para>
    /// </summary>
    [Table("cert_doc_field_def")]
    [SugarTable("cert_doc_field_def")]
    public class DocFieldDef : EntityBase
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true, ColumnName = "id")]
        public long Id { get; set; }

        [SugarColumn(ColumnName = "code", Length = 100)]
        public new string Code { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>规则编码（关联 cert_doc_extraction_rule.code）</summary>
        [SugarColumn(ColumnName = "rule_code", Length = 100)]
        public string RuleCode { get; set; } = "";

        [SugarColumn(ColumnName = "field_name", Length = 100)]
        public string FieldName { get; set; } = "";

        /// <summary>字段编码（用于工作流引用，英文驼峰）</summary>
        [SugarColumn(ColumnName = "field_code", Length = 100)]
        public string FieldCode { get; set; } = "";

        /// <summary>数据类型：string/number/date/boolean</summary>
        [SugarColumn(ColumnName = "data_type", Length = 20)]
        public string DataType { get; set; } = "string";

        /// <summary>字段描述（AI 提取依据）</summary>
        [SugarColumn(ColumnName = "description", Length = 500, IsNullable = true)]
        public string? Description { get; set; }

        /// <summary>是否需手动补充：0-否 1-是</summary>
        [SugarColumn(ColumnName = "is_manual")]
        public bool IsManual { get; set; }

        /// <summary>是否 AI 推荐字段（1=是，0=手动添加）</summary>
        [SugarColumn(ColumnName = "is_ai_recommended", IsNullable = true)]
        public bool? IsAiRecommended { get; set; } = true;

        [SugarColumn(ColumnName = "Sort")]
        public new int Sort { get; set; }

        [SugarColumn(ColumnName = "Remark", Length = 500, IsNullable = true)]
        public new string? Remark { get; set; }

        [SugarColumn(ColumnName = "status", Length = 50)]
        public new string Status { get; set; } = "active";

        [SugarColumn(ColumnName = "CreateBy", Length = 50, IsNullable = true)]
        public new string? CreateBy { get; set; }

        [SugarColumn(ColumnName = "CreateTime")]
        public new DateTime? CreateTime { get; set; } = DateTime.Now;

        [SugarColumn(ColumnName = "UpdateBy", Length = 50, IsNullable = true)]
        public new string? UpdateBy { get; set; }

        [SugarColumn(ColumnName = "UpdateTime", IsNullable = true)]
        public new DateTime? UpdateTime { get; set; }

        [SugarColumn(ColumnName = "DeleteBy", Length = 50, IsNullable = true)]
        public new string? DeleteBy { get; set; }

        [SugarColumn(ColumnName = "DeleteTime", IsNullable = true)]
        public new DateTime? DeleteTime { get; set; }

        [SugarColumn(ColumnName = "IsDeleted")]
        public bool IsDeleted { get; set; }

        [SugarColumn(ColumnName = "IsValid")]
        public int IsValid { get; set; } = 1;
    }
}

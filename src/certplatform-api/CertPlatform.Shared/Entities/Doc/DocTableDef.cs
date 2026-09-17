using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SqlSugar;
using YZH.Entity.Admin.Platform;

namespace YZH.Entity.Admin.Platform.Doc
{
    /// <summary>
    /// DocTableDef 文档表格定义
    /// <para>表名：cert_doc_table_def（唯一键 uk_rule_table: rule_code + table_code）</para>
    /// </summary>
    [Table("cert_doc_table_def")]
    [SugarTable("cert_doc_table_def")]
    public class DocTableDef : EntityBase
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true, ColumnName = "id")]
        public long Id { get; set; }

        [SugarColumn(ColumnName = "code", Length = 100)]
        public new string Code { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>规则编码（关联 cert_doc_extraction_rule.code）</summary>
        [SugarColumn(ColumnName = "rule_code", Length = 100)]
        public string RuleCode { get; set; } = "";

        [SugarColumn(ColumnName = "table_name", Length = 100)]
        public string TableName { get; set; } = "";

        /// <summary>表格编码（用于工作流引用，英文驼峰）</summary>
        [SugarColumn(ColumnName = "table_code", Length = 100)]
        public string TableCode { get; set; } = "";

        /// <summary>表格描述（AI 提取依据）</summary>
        [SugarColumn(ColumnName = "description", Length = 500, IsNullable = true)]
        public string? Description { get; set; }

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

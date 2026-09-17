using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SqlSugar;
using YZH.Entity.Admin.Platform;

namespace YZH.Entity.Admin.Platform.Doc
{
    /// <summary>
    /// DocTableFieldDef 文档表格字段定义（表格的列）
    /// <para>表名：cert_doc_table_field_def（唯一键 uk_table_column: table_code + column_code）</para>
    /// </summary>
    [Table("cert_doc_table_field_def")]
    [SugarTable("cert_doc_table_field_def")]
    public class DocTableFieldDef : EntityBase
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true, ColumnName = "id")]
        public long Id { get; set; }

        [SugarColumn(ColumnName = "code", Length = 100)]
        public new string Code { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>表格编码（关联 cert_doc_table_def.code）</summary>
        [SugarColumn(ColumnName = "table_code", Length = 100)]
        public string TableCode { get; set; } = "";

        [SugarColumn(ColumnName = "column_name", Length = 100)]
        public string ColumnName { get; set; } = "";

        /// <summary>列编码（英文驼峰）</summary>
        [SugarColumn(ColumnName = "column_code", Length = 100)]
        public string ColumnCode { get; set; } = "";

        /// <summary>数据类型：string/number/date</summary>
        [SugarColumn(ColumnName = "data_type", Length = 20)]
        public string DataType { get; set; } = "string";

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

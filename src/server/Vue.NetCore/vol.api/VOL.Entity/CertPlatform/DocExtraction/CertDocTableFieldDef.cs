using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VOL.Entity.CertPlatform;
using VOL.Entity.SystemModels;

namespace VOL.Entity.CertPlatform.DocExtraction
{
    /// <summary>
    /// 文档表格字段定义表
    /// 定义表格中的列
    /// </summary>
    [Table("cert_doc_table_field_def")]
    [Entity(TableCnName = "文档表格字段定义")]
    public class CertDocTableFieldDef : YZHBaseEntity
    {
        /// <summary>
        /// 表格编码（关联cert_doc_table_def.Code）
        /// </summary>
        [Column("table_code")]
        [Display(Name = "表格编码")]
        [Required(ErrorMessage = "表格编码不能为空")]
        [MaxLength(100)]
        public string TableCode { get; set; }

        /// <summary>
        /// 列名称
        /// </summary>
        [Column("column_name")]
        [Display(Name = "列名称")]
        [Required(ErrorMessage = "列名称不能为空")]
        [MaxLength(100)]
        public string ColumnName { get; set; }

        /// <summary>
        /// 列编码
        /// </summary>
        [Column("column_code")]
        [Display(Name = "列编码")]
        [Required(ErrorMessage = "列编码不能为空")]
        [MaxLength(100)]
        public string ColumnCode { get; set; }

        /// <summary>
        /// 数据类型：string/number/date
        /// </summary>
        [Column("data_type")]
        [Display(Name = "数据类型")]
        [Required(ErrorMessage = "数据类型不能为空")]
        [MaxLength(20)]
        public string DataType { get; set; } = "string";

        /// <summary>
        /// 显示顺序
        /// </summary>
        [Column("sort_order")]
        [Display(Name = "显示顺序")]
        public int SortOrder { get; set; } = 0;

        /// <summary>
        /// 备注（覆盖基类Remark）
        /// </summary>
        [Column("remark")]
        [Display(Name = "备注")]
        [MaxLength(500)]
        public new string Remark { get; set; }
    }
}

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using YZH.Entity.Admin.Platform;
using YZH.Entity.SystemModels;

namespace YZH.Entity.Admin.Platform.DocExtraction
{
    /// <summary>
    /// 文档表格字段定义表
    /// 定义表格中的列
    /// </summary>
    [Table("cert_doc_table_field_def")]
    [Entity(TableCnName = "文档表格字段定义")]
    public class CertDocTableFieldDef : YZHBaseEntity
    {
        // 覆盖基类审计/通用字段，适配 snake_case 列名
        [Column("create_id")] public new int? CreateID { get; set; }
        [Column("creator")] [MaxLength(50)] public new string Creator { get; set; }
        [Column("create_date")] public new DateTime? CreateDate { get; set; } = DateTime.Now;
        [Column("modify_id")] public new int? ModifyID { get; set; }
        [Column("modifier")] [MaxLength(50)] public new string Modifier { get; set; }
        [Column("modify_date")] public new DateTime? ModifyDate { get; set; }
        [Column("delete_id")] public new int? DeleteID { get; set; }
        [Column("deleter")] [MaxLength(50)] public new string Deleter { get; set; }
        [Column("delete_time")] public new DateTime? DeleteTime { get; set; }
        [Column("code")] public new string Code { get; set; } = Guid.NewGuid().ToString("N");
        [Column("status")] public new string Status { get; set; } = "active";
        [Column("enable")] public new bool Enable { get; set; } = true;
        [Column("sort")] public new int Sort { get; set; }
        [Column("remark")] public new string Remark { get; set; }

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
        [UniqueField("列编码", WithFields = new[] { "TableCode" })]
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

        // 审计字段及 Code/Status/Enable/Remark 已通过 new + [Column] 覆盖，适配 snake_case
    }
}

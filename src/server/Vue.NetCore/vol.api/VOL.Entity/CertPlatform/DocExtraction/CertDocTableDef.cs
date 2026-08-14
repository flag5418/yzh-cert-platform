using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VOL.Entity.CertPlatform;
using VOL.Entity.SystemModels;

namespace VOL.Entity.CertPlatform.DocExtraction
{
    /// <summary>
    /// 文档表格定义表
    /// 定义从文档中提取的表格
    /// </summary>
    [Table("cert_doc_table_def")]
    [Entity(TableCnName = "文档表格定义")]
    public class CertDocTableDef : YZHBaseEntity
    {
        /// <summary>覆盖基类审计字段，适配snake_case列名</summary>
        [Column("create_id")] public new int? CreateID { get; set; }
        [NotMapped] public new string Creator { get; set; }
        [Column("create_date")] public new DateTime? CreateDate { get; set; } = DateTime.Now;
        [Column("update_id")] public new int? ModifyID { get; set; }
        [NotMapped] public new string Modifier { get; set; }
        [Column("update_date")] public new DateTime? ModifyDate { get; set; } = DateTime.Now;
        [NotMapped] public new int? DeleteID { get; set; }
        [NotMapped] public new string Deleter { get; set; }
        [NotMapped] public new DateTime? DeleteTime { get; set; }
        /// <summary>覆盖基类字段，适配snake_case列名</summary>
        [Column("code")]        public new string Code { get; set; }
        [NotMapped] public new string OrgCode { get; set; }
        [NotMapped] public new string Status { get; set; }
        [NotMapped] public new bool Enable { get; set; }
        [NotMapped] public new int Sort { get; set; }
        [Column("remark")]      public new string Remark { get; set; }

        /// <summary>
        /// 规则编码（关联cert_doc_extraction_rule.Code）
        /// </summary>
        [Column("rule_code")]
        [Display(Name = "规则编码")]
        [Required(ErrorMessage = "规则编码不能为空")]
        [MaxLength(100)]
        public string RuleCode { get; set; }

        /// <summary>
        /// 表格名称
        /// </summary>
        [Column("table_name")]
        [Display(Name = "表格名称")]
        [Required(ErrorMessage = "表格名称不能为空")]
        [MaxLength(100)]
        public string TableName { get; set; }

        /// <summary>
        /// 表格编码（用于工作流引用）
        /// </summary>
        [Column("table_code")]
        [Display(Name = "表格编码")]
        [Required(ErrorMessage = "表格编码不能为空")]
        [MaxLength(100)]
        public string TableCode { get; set; }

        /// <summary>
        /// 表格描述（AI提取依据）
        /// </summary>
        [Column("description")]
        [Display(Name = "表格描述")]
        [MaxLength(500)]
        public string Description { get; set; }

        /// <summary>
        /// 显示顺序
        /// </summary>
        [Column("sort_order")]
        [Display(Name = "显示顺序")]
        public int SortOrder { get; set; } = 0;
    }
}

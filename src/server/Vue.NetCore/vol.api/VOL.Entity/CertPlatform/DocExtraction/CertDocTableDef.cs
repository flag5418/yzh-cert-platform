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

        // 审计字段及 Code/OrgCode/Status/Enable/Remark 继承自 YZHBaseEntity，无需 new 覆盖
    }
}

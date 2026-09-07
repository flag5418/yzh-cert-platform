using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using YZH.Entity.Admin.Platform;
using YZH.Entity.SystemModels;

namespace YZH.Entity.Admin.Platform.DocExtraction
{
    /// <summary>
    /// 文档表格定义表
    /// 定义从文档中提取的表格
    /// </summary>
    [Table("cert_doc_table_def")]
    [Entity(TableCnName = "文档表格定义")]
    public class CertDocTableDef : EntityBase
    {
        // 覆盖基类审计/通用字段，适配 snake_case 列名
        [Column("create_by")] public new string CreateBy { get; set; }
        [Column("creator")] [MaxLength(50)] public new string Creator { get; set; }
        [Column("create_date")] public new DateTime? CreateDate { get; set; } = DateTime.Now;
        [Column("update_by")] public new string UpdateBy { get; set; }
        [Column("modifier")] [MaxLength(50)] public new string Modifier { get; set; }
        [Column("modify_date")] public new DateTime? ModifyDate { get; set; }
        [Column("delete_by")] public new string DeleteBy { get; set; }
        [Column("deleter")] [MaxLength(50)] public new string Deleter { get; set; }
        [Column("delete_time")] public new DateTime? DeleteTime { get; set; }
        [Column("code")] public new string Code { get; set; } = Guid.NewGuid().ToString("N");
        [Column("status")] public new string Status { get; set; } = "active";
        [Column("enable")] public new bool Enable { get; set; } = true;
        [Column("sort")] public new int Sort { get; set; }
        [Column("remark")] public new string Remark { get; set; }

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
        [UniqueField("表格编码", WithFields = new[] { "RuleCode" })]
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

        // 审计字段及 Code/Status/Enable/Remark 已通过 new + [Column] 覆盖，适配 snake_case
    }
}

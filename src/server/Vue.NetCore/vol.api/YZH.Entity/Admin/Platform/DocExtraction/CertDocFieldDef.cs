using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using YZH.Entity.Admin.Platform;
using YZH.Entity.SystemModels;

namespace YZH.Entity.Admin.Platform.DocExtraction
{
    /// <summary>
    /// 文档字段定义表
    /// 定义从文档中提取的字段
    /// </summary>
    [Table("cert_doc_field_def")]
    [Entity(TableCnName = "文档字段定义")]
    public class CertDocFieldDef : YZHBaseEntity
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
        /// 规则编码（关联cert_doc_extraction_rule.Code）
        /// </summary>
        [Column("rule_code")]
        [Display(Name = "规则编码")]
        [Required(ErrorMessage = "规则编码不能为空")]
        [MaxLength(100)]
        public string RuleCode { get; set; }

        /// <summary>
        /// 字段名称
        /// </summary>
        [Column("field_name")]
        [Display(Name = "字段名称")]
        [Required(ErrorMessage = "字段名称不能为空")]
        [MaxLength(100)]
        public string FieldName { get; set; }

        /// <summary>
        /// 字段编码（用于工作流引用）
        /// </summary>
        [Column("field_code")]
        [Display(Name = "字段编码")]
        [Required(ErrorMessage = "字段编码不能为空")]
        [MaxLength(100)]
        [UniqueField("字段编码", WithFields = new[] { "RuleCode" })]
        public string FieldCode { get; set; }

        /// <summary>
        /// 数据类型：string/number/date/boolean
        /// </summary>
        [Column("data_type")]
        [Display(Name = "数据类型")]
        [Required(ErrorMessage = "数据类型不能为空")]
        [MaxLength(20)]
        public string DataType { get; set; } = "string";

        /// <summary>
        /// 字段描述（AI提取依据）
        /// </summary>
        [Column("description")]
        [Display(Name = "字段描述")]
        [MaxLength(500)]
        public string Description { get; set; }

        /// <summary>
        /// 是否需手动补充
        /// </summary>
        [Column("is_manual")]
        [Display(Name = "需手动补充")]
        public bool IsManual { get; set; } = false;

        /// <summary>
        /// 是否 AI 推荐字段（true=可自动提取，false=手动添加/审核员必填字段，生成Prompt时过滤掉）
        /// </summary>
        [Column("is_ai_recommended")]
        [Display(Name = "AI推荐")]
        public bool IsAiRecommended { get; set; } = true;

        /// <summary>
        /// 显示顺序
        /// </summary>
        [Column("sort_order")]
        [Display(Name = "显示顺序")]
        public int SortOrder { get; set; } = 0;

        // 审计字段及 Code/Status/Enable/Remark 已通过 new + [Column] 覆盖，适配 snake_case
    }
}

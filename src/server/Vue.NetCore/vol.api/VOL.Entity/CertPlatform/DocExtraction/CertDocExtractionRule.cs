using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VOL.Entity.CertPlatform;
using VOL.Entity.SystemModels;

namespace VOL.Entity.CertPlatform.DocExtraction
{
    /// <summary>
    /// 文档提取规则主表
    /// 一个标准文件对应一个规则
    /// </summary>
    [Table("cert_doc_extraction_rule")]
    [Entity(TableCnName = "文档提取规则")]
    public class CertDocExtractionRule : YZHBaseEntity
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
        [Column("org_code")]    public new string OrgCode { get; set; }
        [Column("status")]      public new string Status { get; set; }
        [Column("remark")]      public new string Remark { get; set; }
        [NotMapped] public new bool Enable { get; set; }
        [NotMapped] public new int Sort { get; set; }

        /// <summary>
        /// 文件编码（关联标准目录文件）
        /// </summary>
        [Column("file_code")]
        [Display(Name = "文件编码")]
        [Required(ErrorMessage = "文件编码不能为空")]
        [MaxLength(100)]
        public string FileCode { get; set; }

        /// <summary>
        /// 技能类型（word/excel/pdf）
        /// </summary>
        [Column("skill")]
        [Display(Name = "技能类型")]
        [Required(ErrorMessage = "技能类型不能为空")]
        [MaxLength(50)]
        public string Skill { get; set; }

        /// <summary>
        /// 提取Prompt
        /// </summary>
        [Column("prompt")]
        [Display(Name = "提取Prompt")]
        public string Prompt { get; set; }

        /// <summary>
        /// 是否验证通过
        /// </summary>
        [Column("is_valid")]
        [Display(Name = "验证通过")]
        public bool IsValid { get; set; } = false;

        /// <summary>
        /// 验证结果信息
        /// </summary>
        [Column("verify_message")]
        [Display(Name = "验证信息")]
        [MaxLength(500)]
        public string VerifyMessage { get; set; }

        /// <summary>
        /// 验证时提取的样本数据（JSON格式）
        /// </summary>
        [Column("sample_data")]
        [Display(Name = "样本数据")]
        public string SampleData { get; set; }

        /// <summary>
        /// 提取的文档内容缓存（结构化文本，避免每次验证都重新提取文档）
        /// </summary>
        [Column("doc_content")]
        [Display(Name = "文档内容缓存")]
        public string DocContent { get; set; }
    }
}

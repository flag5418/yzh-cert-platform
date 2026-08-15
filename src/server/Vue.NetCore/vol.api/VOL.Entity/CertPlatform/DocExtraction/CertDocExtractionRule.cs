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
    /// <para>核心关联：standard_file_code → cert_file_requirement.code</para>
    /// <para>冗余字段：org_code / standard_code / phase_code 方便过滤</para>
    /// </summary>
    [Table("cert_doc_extraction_rule")]
    [Entity(TableCnName = "文档提取规则")]
    public class CertDocExtractionRule : YZHBaseEntity
    {
        /// <summary>机构编码（冗余，方便多租户过滤）</summary>
        [StringLength(50)]
        [Column("org_code")]
        public string OrgCode { get; set; }

        /// <summary>
        /// 文件编码（旧字段，保留向后兼容，新代码不再使用）
        /// </summary>
        [Column("file_code")]
        [Display(Name = "文件编码")]
        [MaxLength(100)]
        public string FileCode { get; set; }

        /// <summary>
        /// 标准文件编码（关联 cert_file_requirement.code，核心枢纽）
        /// 一个标准文件对应一个提取规则
        /// </summary>
        [Column("standard_file_code")]
        [Display(Name = "标准文件编码")]
        [MaxLength(36)]
        public string StandardFileCode { get; set; }

        /// <summary>
        /// 标准编码（冗余，关联 cert_iso_standard.code）
        /// </summary>
        [Column("standard_code")]
        [Display(Name = "标准编码")]
        [MaxLength(36)]
        public string StandardCode { get; set; }

        /// <summary>
        /// 阶段编码（冗余，方便过滤）
        /// </summary>
        [Column("phase_code")]
        [Display(Name = "阶段编码")]
        [MaxLength(36)]
        public string PhaseCode { get; set; }

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

        // 审计字段及 Code/OrgCode/Status/Enable/Remark 继承自 YZHBaseEntity
    }
}

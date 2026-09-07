using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using YZH.Entity.Admin.Platform;
using YZH.Entity.SystemModels;

namespace YZH.Entity.Admin.Platform.DocExtraction
{
    /// <summary>
    /// 文档提取规则主表
    /// 一个标准文件对应一个规则
    /// <para>规则键：standard_file_code — 存实际标准目录文件的 FileCode（FL-xxx，前端目录树流程）</para>
    /// <para>或文件要求的模板 Code（FR-xxx，模板流程）</para>
    /// <para>冗余字段：standard_code / phase_code 方便过滤</para>
    /// </summary>
    [Table("cert_doc_extraction_rule")]
    [Entity(TableCnName = "文档提取规则")]
    public class CertDocExtractionRule : EntityBase
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
        /// 文件编码（历史字段，保留兼容；当前流程规则键统一用 standard_file_code）
        /// </summary>
        [Column("file_code")]
        [Display(Name = "文件编码")]
        [MaxLength(100)]
        public string FileCode { get; set; }

        /// <summary>
        /// 规则键：实际文件 FileCode（FL-FD-...|文件名，前端目录树 AI 分析流程）
        /// 或文件要求模板 Code（FR-xxx，模板流程）
        /// GetFileInfoAsync 按此值两级查询（模板优先、实际文件兜底）
        /// </summary>
        [Column("standard_file_code")]
        [Display(Name = "规则文件编码")]
        [MaxLength(200)]
        [UniqueField("规则文件编码")]
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

        // 审计字段及 Code/Status/Enable/Remark 已通过 new + [Column] 覆盖，适配 snake_case
    }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Doc
{
    /// <summary>
    /// DocExtractionRule 文档提取规则主表
    /// 一个标准文件对应一个规则
    /// <para>表名：cert_doc_extraction_rule</para>
    ///
    /// 命名规范（YZH 铁律）：DB 列名 = C# 属性名 = PascalCase
    /// </summary>
    [SugarTable("cert_doc_extraction_rule")]
    public class DocExtractionRule : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / 审计字段由 BaseEntity + 接口统一提供 ────
        // ──── ISoftDelete / IIsValid 接口字段由接口提供 ────

        // ──── 业务字段 ────
        /// <summary>文件编码（历史字段，保留兼容）</summary>
        [Display(Name = "文件编码")]
        [StringLength(100)]
        [SugarColumn(IsNullable = true)]
        public string? FileCode { get; set; }

        /// <summary>规则键：实际文件 FileCode 或文件要求模板 Code</summary>
        [Display(Name = "规则文件编码")]
        [StringLength(200)]
        [SugarColumn(IsNullable = true)]
        public string? StandardFileCode { get; set; }

        /// <summary>标准编码（冗余，关联 cert_iso_standard.Code）</summary>
        [Display(Name = "标准编码")]
        [StringLength(36)]
        [SugarColumn(IsNullable = true)]
        public string? StandardCode { get; set; }

        /// <summary>阶段编码（冗余，方便过滤）</summary>
        [Display(Name = "阶段编码")]
        [StringLength(36)]
        [SugarColumn(IsNullable = true)]
        public string? PhaseCode { get; set; }

        /// <summary>技能类型（word/excel/pdf，按文件扩展名权威推导）</summary>
        [Display(Name = "技能类型")]
        [Required(ErrorMessage = "技能类型不能为空")]
        [StringLength(50)]
        public string Skill { get; set; } = "word";

        /// <summary>提取 Prompt</summary>
        [Display(Name = "提取Prompt")]
        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string? Prompt { get; set; }

        /// <summary>是否验证通过</summary>
        [Display(Name = "是否验证通过")]
        public bool DocIsValid { get; set; } = false;

        /// <summary>验证结果信息</summary>
        [Display(Name = "验证信息")]
        [StringLength(500)]
        [SugarColumn(IsNullable = true)]
        public string? VerifyMessage { get; set; }

        /// <summary>验证时提取的样本数据（JSON 格式）</summary>
        [Display(Name = "样本数据")]
        [SugarColumn(ColumnDataType = "json", IsNullable = true)]
        public string? SampleData { get; set; }

        /// <summary>提取的文档内容缓存（Markdown）</summary>
        [Display(Name = "文档内容缓存")]
        [SugarColumn(ColumnDataType = "longtext", IsNullable = true)]
        public string? DocContent { get; set; }

        /// <summary>规则状态：none/configured/failed</summary>
        public string Status { get; set; } = "none";

        /// <summary>备注</summary>
        [StringLength(500)]
        [SugarColumn(IsNullable = true)]
        public string? Remark { get; set; }

        // ──── ISoftDelete + IIsValid 接口显式实现 ────
        public bool IsDeleted { get; set; }
        public string? DeleteBy { get; set; }
        public DateTime? DeleteTime { get; set; }
        public int IsValid { get; set; } = 1;
    }
}

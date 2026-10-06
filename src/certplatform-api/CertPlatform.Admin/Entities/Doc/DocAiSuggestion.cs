using System;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Attributes;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Admin.Entities.Doc
{
    /// <summary>
    /// AI 填写建议（多候选 + 人工裁决）
    /// <para>表名：cert_doc_ai_suggestion</para>
    /// </summary>
    [SugarTable("cert_doc_ai_suggestion")]
    [YZHDeleteStrategy(Mode = DeleteMode.Soft)]
    public class DocAiSuggestion : BaseEntity, ISoftDelete, IIsValid
    {
        [StringLength(36)]
        public string OrgCode { get; set; } = string.Empty;

        [StringLength(36)]
        public string EnterpriseCode { get; set; } = string.Empty;

        [StringLength(36)]
        public string StageCode { get; set; } = string.Empty;

        [StringLength(36)]
        public string StandardCode { get; set; } = string.Empty;

        /// <summary>目标模板（cert_standard_directory_file.Code）</summary>
        [StringLength(36)]
        public string TemplateFileCode { get; set; } = string.Empty;

        /// <summary>目标单元格锚点</summary>
        [StringLength(128)]
        public string AnchorCode { get; set; } = string.Empty;

        /// <summary>形式：scalar / table</summary>
        [StringLength(20)]
        public string FormKind { get; set; } = "scalar";

        /// <summary>一次 AI 分析的批次编码</summary>
        [StringLength(36)]
        public string BatchCode { get; set; } = string.Empty;

        /// <summary>同锚点候选索引 (0起)</summary>
        public int SuggestionIndex { get; set; }

        /// <summary>AI 建议值（表格为 JSON）</summary>
        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string? SuggestedValue { get; set; }

        /// <summary>值类型：text/number/date/bool</summary>
        [StringLength(20)]
        public string ValueKind { get; set; } = "text";

        /// <summary>数字/日期格式串</summary>
        [StringLength(64)]
        public string? NumberFormat { get; set; }

        /// <summary>置信度 0~1</summary>
        public decimal? Confidence { get; set; }

        /// <summary>数据源文档 Code</summary>
        [StringLength(36)]
        public string? SourceDocCode { get; set; }

        /// <summary>数据源文档内位置</summary>
        [StringLength(256)]
        public string? SourceLocation { get; set; }

        /// <summary>证据原文片段</summary>
        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string? SourceSnippet { get; set; }

        /// <summary>AI 给出的理由</summary>
        [StringLength(1000)]
        public string? Reason { get; set; }

        /// <summary>人工是否选定</summary>
        public bool IsSelected { get; set; }

        /// <summary>人工改写后的值</summary>
        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string? ManualValue { get; set; }

        [StringLength(64)]
        public string? SelectedBy { get; set; }

        public DateTime? SelectedTime { get; set; }

        /// <summary>状态：pending/accepted/rejected/edited</summary>
        [StringLength(20)]
        public string Status { get; set; } = "pending";

        [StringLength(50)]
        public string? SkillCode { get; set; }

        [StringLength(100)]
        public string? ModelName { get; set; }

        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int DurationMs { get; set; }

        public bool IsDeleted { get; set; }
        public string? DeleteBy { get; set; }
        public DateTime? DeleteTime { get; set; }
        public int IsValid { get; set; } = 1;
    }
}

using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Doc
{
    /// <summary>
    /// AI 调用日志（用于费用统计）
    /// <para>表名：cert_ai_usage_log</para>
    /// </summary>
    [SugarTable("cert_ai_usage_log")]
    public class AiUsageLog : BaseEntity, ISoftDelete, IIsValid
    {
        [SugarColumn(Length = 64)]
        public string CallId { get; set; } = string.Empty;

        [SugarColumn(Length = 50)]
        public string BusinessType { get; set; } = "doc_extraction";

        [SugarColumn(Length = 100, IsNullable = true)]
        public string? BusinessRef { get; set; }

        [SugarColumn(Length = 50, IsNullable = true)]
        public string? Skill { get; set; }

        [SugarColumn(Length = 50, IsNullable = true)]
        public string? Provider { get; set; }

        [SugarColumn(Length = 100, IsNullable = true)]
        public string? Model { get; set; }

        public int PromptTokens { get; set; } = 0;

        public int CompletionTokens { get; set; } = 0;

        public int TotalTokens { get; set; } = 0;

        [SugarColumn(DecimalDigits = 6, ColumnDataType = "decimal(10,6)")]
        public decimal CostUsd { get; set; } = 0;

        public long DurationMs { get; set; } = 0;

        public bool Success { get; set; } = true;

        [SugarColumn(Length = 500, IsNullable = true)]
        public string? ErrorMessage { get; set; }

        // ──── ISoftDelete + IIsValid 接口显式实现 ────
        public bool IsDeleted { get; set; }
        public string? DeleteBy { get; set; }
        public DateTime? DeleteTime { get; set; }
        public int IsValid { get; set; } = 1;
    }
}

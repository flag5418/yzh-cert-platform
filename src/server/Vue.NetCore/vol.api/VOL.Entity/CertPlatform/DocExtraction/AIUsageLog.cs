using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VOL.Entity.SystemModels;

namespace VOL.Entity.CertPlatform.DocExtraction
{
    [Table("cert_ai_usage_log")]
    [Entity(TableCnName = "AI调用日志", DBServer = "VOLContext")]
    public class AIUsageLog : BaseEntity
    {
        [Key]
        [Column(TypeName = "bigint")]
        public long Id { get; set; }

        [Column("call_id")]
        [MaxLength(64)]
        public string CallId { get; set; }

        [Column("business_type")]
        [MaxLength(50)]
        public string BusinessType { get; set; } = "doc_extraction";

        [Column("business_ref")]
        [MaxLength(100)]
        public string BusinessRef { get; set; }

        [Column("skill")]
        [MaxLength(50)]
        public string Skill { get; set; }

        [Column("provider")]
        [MaxLength(50)]
        public string Provider { get; set; }

        [Column("model")]
        [MaxLength(100)]
        public string Model { get; set; }

        [Column("prompt_tokens")]
        public int PromptTokens { get; set; }

        [Column("completion_tokens")]
        public int CompletionTokens { get; set; }

        [Column("total_tokens")]
        public int TotalTokens { get; set; }

        [Column("cost_usd")]
        public decimal CostUsd { get; set; }

        [Column("duration_ms")]
        public long DurationMs { get; set; }

        [Column("success")]
        public bool Success { get; set; }

        [Column("error_message")]
        [MaxLength(500)]
        public string ErrorMessage { get; set; }

        [Column("create_date")]
        public DateTime CreateDate { get; set; } = DateTime.Now;
    }
}

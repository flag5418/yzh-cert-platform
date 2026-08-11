using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VOL.Entity.CertPlatform;
using VOL.Entity.SystemModels;

namespace VOL.Entity.CertPlatform.DocExtraction
{
    [Table("cert_ai_config")]
    [Entity(TableCnName = "AI配置")]
    public class AIConfig : YZHBaseEntity
    {
        [Column("provider")]
        [Required]
        [MaxLength(50)]
        public string Provider { get; set; } = "qwen";

        [Column("api_key")]
        [Required]
        [MaxLength(500)]
        public string ApiKey { get; set; }

        [Column("model")]
        [Required]
        [MaxLength(100)]
        public string Model { get; set; } = "qwen-turbo";

        [Column("temperature")]
        public float Temperature { get; set; } = 0.7f;

        [Column("max_tokens")]
        public int MaxTokens { get; set; } = 4096;

        [Column("is_enabled")]
        public bool IsEnabled { get; set; } = true;
    }
}

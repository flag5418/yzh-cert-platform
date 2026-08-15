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
        /// <summary>覆盖基类审计字段，适配snake_case列名</summary>
        [Column("create_id")] public new int? CreateID { get; set; }
        [Column("creator")]   [MaxLength(50)] public new string Creator { get; set; }
        [Column("create_date")] public new DateTime? CreateDate { get; set; } = DateTime.Now;
        [Column("modify_id")] public new int? ModifyID { get; set; }
        [Column("modifier")]  [MaxLength(50)] public new string Modifier { get; set; }
        [Column("modify_date")] public new DateTime? ModifyDate { get; set; }
        [Column("delete_id")] public new int? DeleteID { get; set; }
        [Column("deleter")]   [MaxLength(50)] public new string Deleter { get; set; }
        [Column("delete_time")] public new DateTime? DeleteTime { get; set; }
        /// <summary>覆盖基类字段，适配snake_case列名</summary>
        [Column("code")]        public new string Code { get; set; }
        [Column("org_code")]    public string OrgCode { get; set; }
        [Column("status")]      public new string Status { get; set; }
        [Column("enable")]      public new bool Enable { get; set; }
        [Column("sort")]        public new int Sort { get; set; }
        [Column("remark")]      public new string Remark { get; set; }

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

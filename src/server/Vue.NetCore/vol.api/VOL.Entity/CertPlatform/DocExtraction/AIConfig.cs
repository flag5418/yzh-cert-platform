using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VOL.Entity.CertPlatform;
using VOL.Entity.SystemModels;

namespace VOL.Entity.CertPlatform.DocExtraction
{
    /// <summary>
    /// AI配置表
    /// 存储AI服务配置信息
    /// </summary>
    [Table("cert_ai_config")]
    [Entity(TableCnName = "AI配置")]
    public class AIConfig : YZHBaseEntity
    {
        /// <summary>
        /// AI提供商：qwen/deepseek等
        /// </summary>
        [Column("provider")]
        [Display(Name = "AI提供商")]
        [Required(ErrorMessage = "AI提供商不能为空")]
        [MaxLength(50)]
        public string Provider { get; set; } = "qwen";

        /// <summary>
        /// API Key（加密存储）
        /// </summary>
        [Column("api_key")]
        [Display(Name = "API Key")]
        [Required(ErrorMessage = "API Key不能为空")]
        [MaxLength(500)]
        public string ApiKey { get; set; }

        /// <summary>
        /// 模型名称
        /// </summary>
        [Column("model")]
        [Display(Name = "模型名称")]
        [Required(ErrorMessage = "模型名称不能为空")]
        [MaxLength(100)]
        public string Model { get; set; } = "qwen-turbo";

        /// <summary>
        /// 温度参数
        /// </summary>
        [Column("temperature")]
        [Display(Name = "温度参数")]
        public float Temperature { get; set; } = 0.7f;

        /// <summary>
        /// 最大Token数
        /// </summary>
        [Column("max_tokens")]
        [Display(Name = "最大Token数")]
        public int MaxTokens { get; set; } = 4096;

        /// <summary>
        /// 是否启用
        /// </summary>
        [Column("is_enabled")]
        [Display(Name = "是否启用")]
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// 备注（覆盖基类Remark）
        /// </summary>
        [Column("remark")]
        [Display(Name = "备注")]
        [MaxLength(500)]
        public new string Remark { get; set; }
    }
}

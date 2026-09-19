using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Doc
{
    /// <summary>
    /// AiConfig AI 配置（页面级：provider/model/temperature/max_tokens）
    /// <para>表名：cert_ai_config</para>
    ///
    /// 命名规范（YZH 铁律）：DB 列名 = C# 属性名 = PascalCase
    /// </summary>
    [SugarTable("cert_ai_config")]
    public class AiConfig : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / 审计字段由 BaseEntity + 接口统一提供 ────
        // ──── ISoftDelete / IIsValid 接口字段由接口提供 ────

        [SugarColumn(Length = 50)]
        public string? OrgCode { get; set; }

        /// <summary>AI 提供商：qwen/deepseek 等</summary>
        [SugarColumn(Length = 50)]
        public string Provider { get; set; } = "qwen";

        /// <summary>API Key</summary>
        [SugarColumn(Length = 500)]
        public string ApiKey { get; set; } = "";

        [SugarColumn(Length = 100)]
        public string Model { get; set; } = "qwen-turbo";

        public float Temperature { get; set; } = 0.7f;

        public int MaxTokens { get; set; } = 4096;

        /// <summary>是否启用：0-否 1-是</summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>备注</summary>
        [SugarColumn(Length = 500, IsNullable = true)]
        public string? Remark { get; set; }

        /// <summary>状态</summary>
        [SugarColumn(Length = 50, IsNullable = true)]
        public string? Status { get; set; } = "active";

        public int? Sort { get; set; }

        // ──── ISoftDelete + IIsValid 接口显式实现 ────
        public bool IsDeleted { get; set; }
        public string? DeleteBy { get; set; }
        public DateTime? DeleteTime { get; set; }
        public int IsValid { get; set; } = 1;
    }
}

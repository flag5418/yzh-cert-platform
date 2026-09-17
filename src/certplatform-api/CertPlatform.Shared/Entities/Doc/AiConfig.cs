using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SqlSugar;
using YZH.Entity.Admin.Platform;

namespace YZH.Entity.Admin.Platform.Doc
{
    /// <summary>
    /// AiConfig AI 配置（页面级：provider/model/temperature/max_tokens）
    /// <para>表名：cert_ai_config</para>
    /// <para>连接参数（api_key/base_url）权威源是 cert_sys_config 六键（V1.1 §9.3），本表只存页面级偏好</para>
    /// </summary>
    [Table("cert_ai_config")]
    [SugarTable("cert_ai_config")]
    public class AiConfig : EntityBase
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true, ColumnName = "id")]
        public long Id { get; set; }

        [SugarColumn(ColumnName = "code", Length = 100)]
        public new string Code { get; set; } = "default-ai-config";

        [SugarColumn(ColumnName = "OrgCode", Length = 50, IsNullable = true)]
        public string? OrgCode { get; set; }

        /// <summary>AI 提供商：qwen/deepseek 等</summary>
        [SugarColumn(ColumnName = "provider", Length = 50)]
        public string Provider { get; set; } = "qwen";

        /// <summary>API Key（页面级覆盖；权威源 cert_sys_config.ai_api_key）</summary>
        [SugarColumn(ColumnName = "api_key", Length = 500)]
        public string ApiKey { get; set; } = "";

        [SugarColumn(ColumnName = "model", Length = 100)]
        public string Model { get; set; } = "qwen-turbo";

        [SugarColumn(ColumnName = "temperature")]
        public float Temperature { get; set; } = 0.7f;

        [SugarColumn(ColumnName = "max_tokens")]
        public int MaxTokens { get; set; } = 4096;

        /// <summary>是否启用：0-否 1-是</summary>
        [SugarColumn(ColumnName = "is_enabled")]
        public bool IsEnabled { get; set; } = true;

        [SugarColumn(ColumnName = "Remark", Length = 500, IsNullable = true)]
        public new string? Remark { get; set; }

        [SugarColumn(ColumnName = "status", Length = 50, IsNullable = true)]
        public new string? Status { get; set; } = "active";

        [SugarColumn(ColumnName = "Sort", IsNullable = true)]
        public new int? Sort { get; set; }

        [SugarColumn(ColumnName = "CreateBy", Length = 50, IsNullable = true)]
        public new string? CreateBy { get; set; }

        [SugarColumn(ColumnName = "CreateTime")]
        public new DateTime? CreateTime { get; set; } = DateTime.Now;

        [SugarColumn(ColumnName = "UpdateBy", Length = 50, IsNullable = true)]
        public new string? UpdateBy { get; set; }

        [SugarColumn(ColumnName = "UpdateTime", IsNullable = true)]
        public new DateTime? UpdateTime { get; set; }

        [SugarColumn(ColumnName = "DeleteBy", Length = 50, IsNullable = true)]
        public new string? DeleteBy { get; set; }

        [SugarColumn(ColumnName = "DeleteTime", IsNullable = true)]
        public new DateTime? DeleteTime { get; set; }

        [SugarColumn(ColumnName = "IsDeleted")]
        public bool IsDeleted { get; set; }

        [SugarColumn(ColumnName = "IsValid", IsNullable = true)]
        public new int? IsValid { get; set; } = 1;
    }
}

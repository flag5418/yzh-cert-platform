using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Admin.Entities.Wf
{
    /// <summary>
    /// Prompt 模板实体（映射 wf_prompt_template）
    ///
    /// 命名规范（YZH 铁律）：DB 列名 = C# 属性名 = PascalCase
    /// </summary>
    [SugarTable("wf_prompt_template")]
    public class PromptTemplate : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / 审计字段已由 BaseEntity 提供 ────
        // ──── ISoftDelete / IIsValid 接口字段由接口提供 ────

        [StringLength(100)]
        public string PromptCode { get; set; } = string.Empty;

        [StringLength(200)]
        public string PromptName { get; set; } = string.Empty;

        [StringLength(50)]
        public string PromptType { get; set; } = string.Empty;

        [StringLength(50)]
        public string? SkillTarget { get; set; }

        /// <summary>
        /// ★ 适用标准（<c>cert_iso_standard.Code</c>，GUID）。NULL = 不限（平台默认 / 跨标准通用）。
        /// <para>⚠️ 存 <b>GUID</b>，不是可读编码（如 <c>iso9001-2015</c>）—— 与
        /// <c>cert_enterprise_stage.StandardCode</c> 同口径。</para>
        /// <para>定位口径：标准级 → 平台级，逐层回退（与全局参数同构）。</para>
        /// </summary>
        [StringLength(36)]
        public string? StandardCode { get; set; }

        [SugarColumn(ColumnDataType = "mediumtext", IsNullable = true)]
        public string? Template { get; set; }

        /// <summary>
        /// 本提示词专用模型名（如 <c>qwen-flash</c> / <c>qwen-plus</c>）。NULL = 用系统默认 <c>ai_model_name</c>。
        /// <para>★ 动机：「支持在控制成本情况下合理切换模型」—— 分类用 flash（便宜），提取用 plus（更稳）。</para>
        /// </summary>
        [StringLength(50)]
        public string? ModelName { get; set; }

        /// <summary>本提示词专用输出上限。NULL = 用系统默认 <c>ai_max_tokens</c>（当前 4096，偏小）。</summary>
        public int? MaxTokens { get; set; }

        /// <summary>本提示词专用温度（0.00~1.00）。NULL = 用系统默认 <c>ai_temperature</c>。</summary>
        [SugarColumn(ColumnDataType = "decimal(3,2)", IsNullable = true)]
        public decimal? Temperature { get; set; }

        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string? Description { get; set; }

        public int Version { get; set; } = 1;

        public bool IsActive { get; set; } = true;

        [StringLength(20)]
        public string? Status { get; set; } = "active";

        // Creator/Deleter 已并入 BaseEntity 的 CreateBy/DeleteBy（YZH 统一审计列），
        // 保留同名属性会生成不存在的列 Creator/Deleter → Unknown column。

        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string? LastTestResult { get; set; }

        [StringLength(50)]
        public string? OrgCode { get; set; }

        // ──── ISoftDelete + IIsValid 接口显式实现 ────
        public bool IsDeleted { get; set; }
        public string? DeleteBy { get; set; }
        public DateTime? DeleteTime { get; set; }
        public int IsValid { get; set; } = 1;
    }
}

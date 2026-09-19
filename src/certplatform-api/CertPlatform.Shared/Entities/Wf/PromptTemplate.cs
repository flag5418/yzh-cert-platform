using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Wf
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

        [SugarColumn(ColumnDataType = "mediumtext", IsNullable = true)]
        public string? Template { get; set; }

        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string? Description { get; set; }

        public int Version { get; set; } = 1;

        public bool IsActive { get; set; } = true;

        public bool Enable { get; set; } = true;

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

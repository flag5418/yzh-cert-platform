using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Entity.Admin.Platform;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Wf
{
    /// <summary>
    /// WorkflowDefinition - 工作流定义
    /// <para>表名：wf_workflow_definition</para>
    ///
    /// 命名规范（YZH 铁律）：DB 列名 = C# 属性名 = PascalCase
    /// </summary>
    [SugarTable("wf_workflow_definition")]
    public class WorkflowDefinition : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / 审计字段由 BaseEntity 基类统一提供 ────
        // ──── ISoftDelete / IIsValid 接口字段由接口提供 ────

        // ──── 业务字段 ────
        public string? Status { get; set; } = "active";

        public bool Enable { get; set; } = true;

        public int Sort { get; set; }

        public string? Remark { get; set; }

        [Required]
        [StringLength(100)]
        [UniqueField("工作流编码")]
        public string WorkflowCode { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string WorkflowName { get; set; } = string.Empty;

        [Required]
        public string WorkflowType { get; set; } = string.Empty;

        [SugarColumn(ColumnDataType = "json")]
        public string? WorkflowConfig { get; set; }

        public int Version { get; set; } = 1;

        public bool IsActive { get; set; } = true;

        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string? Description { get; set; }

        // ──── ISoftDelete + IIsValid 接口显式实现 ────
        public bool IsDeleted { get; set; }
        public string? DeleteBy { get; set; }
        public DateTime? DeleteTime { get; set; }
        public int IsValid { get; set; } = 1;
    }
}

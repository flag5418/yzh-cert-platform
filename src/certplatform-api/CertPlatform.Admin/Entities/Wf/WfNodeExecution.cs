using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Entity.Admin.Platform;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Admin.Entities.Wf
{
    /// <summary>
    /// WfNodeExecution - 节点执行状态
    /// <para>表名：wf_node_execution</para>
    /// <para>定位：每个节点一次执行的状态记录，是结果复用和断点续跑的核心载体</para>
    ///
    /// 命名规范（YZH 铁律）：DB 列名 = C# 属性名 = PascalCase
    /// </summary>
    [SugarTable("wf_node_execution")]
    public class WfNodeExecution : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / 审计字段由 BaseEntity 基类统一提供 ────
        // ──── ISoftDelete / IIsValid 接口字段由接口提供 ────

        // ──── 业务字段 ────
        /// <summary>wf_execution_task.Code</summary>
        [Required]
        [MaxLength(36)]
        [UniqueField("任务编码", WithFields = new[] { "ItemCode", "NodeId" })]
        public string TaskCode { get; set; } = string.Empty;

        /// <summary>wf_execution_task_item.Code</summary>
        [Required]
        [MaxLength(36)]
        public string ItemCode { get; set; } = string.Empty;

        /// <summary>节点ID（前端生成的 classCode_n序号）</summary>
        [Required]
        [MaxLength(64)]
        public string NodeId { get; set; } = string.Empty;

        /// <summary>节点类型：start|end|skill|ai_node|logic|branch|docField|docTable</summary>
        [MaxLength(30)]
        public string NodeType { get; set; } = string.Empty;

        /// <summary>节点名称快照（用户在设计器中填写的中文名称）</summary>
        [MaxLength(128)]
        public string NodeTitle { get; set; } = string.Empty;

        /// <summary>该节点在此工作流中的具体作用描述（由设计器填写，专家可见）</summary>
        [MaxLength(500)]
        public string? NodeDescription { get; set; }

        /// <summary>Skill编码（功能节点）</summary>
        [MaxLength(64)]
        public string SkillCode { get; set; } = string.Empty;

        /// <summary>执行状态：pending|executing|completed|failed|skipped</summary>
        [Required]
        [MaxLength(20)]
        public string ExecStatus { get; set; } = "pending";

        /// <summary>节点输出（所有端口的JSON）</summary>
        [SugarColumn(ColumnDataType = "json")]
        public string? OutputJson { get; set; }

        /// <summary>执行错误信息</summary>
        [MaxLength(1000)]
        public string? ErrorMessage { get; set; }

        /// <summary>开始执行时间</summary>
        public DateTime? StartedAt { get; set; }

        /// <summary>完成时间</summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>执行耗时(ms)</summary>
        public int? ExecutionTimeMs { get; set; }

        /// <summary>LLM Prompt Tokens（仅 ai_node 有值）</summary>
        public int? PromptTokens { get; set; }

        /// <summary>LLM Completion Tokens（仅 ai_node 有值）</summary>
        public int? CompletionTokens { get; set; }

        /// <summary>纯 LLM API 调用耗时(ms)（不含参数解析、模板渲染、类型转换）</summary>
        public int? LlmDurationMs { get; set; }

        /// <summary>是否复用了历史结果：0=新执行 1=复用</summary>
        public int IsReused { get; set; } = 0;

        /// <summary>源文件编码 cert_extraction_result.FileCode（docField/docTable 有值）</summary>
        [MaxLength(200)]
        public string? SourceFileCode { get; set; }

        /// <summary>源字段中文名（docField 有值）</summary>
        [MaxLength(200)]
        public string? SourceFieldName { get; set; }

        /// <summary>源文件版本号（cert_extraction_result.VersionNumber）</summary>
        public int? SourceVersion { get; set; }

        /// <summary>LLM 模型名（ai_node 有值，如 qwen-turbo）</summary>
        [MaxLength(100)]
        public string? AiModel { get; set; }

        /// <summary>渲染后送 API 的完整 prompt（8KB 截断，ai_node 有值）</summary>
        [SugarColumn(ColumnDataType = "longtext")]
        public string? AiPrompt { get; set; }

        /// <summary>专家审批后最终输出（null=未审批或无修改）</summary>
        [SugarColumn(ColumnDataType = "json")]
        public string? ApprovedOutputJson { get; set; }

        // ──── ISoftDelete + IIsValid 接口显式实现 ────
        public bool IsDeleted { get; set; }
        public string? DeleteBy { get; set; }
        public DateTime? DeleteTime { get; set; }
        public int IsValid { get; set; } = 1;
    }
}

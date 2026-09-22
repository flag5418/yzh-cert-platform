using System;
using System.Collections.Generic;
using System.Text.Json;

namespace CertPlatform.Admin.Services.Workflow.Models
{
    /// <summary>
    /// 测试历史查询请求（阶段四）
    /// <para>字段命名遵循 YZH 命名铁律：C# 属性名 = JSON 字段名 = TS 字段名（PascalCase）。</para>
    /// <para>全部条件均为可选，彼此 AND；不传即「最近的全部测试」。</para>
    /// </summary>
    public class TaskHistoryRequest
    {
        /// <summary>页码（从 1 开始，&lt;=0 时按 1 处理）</summary>
        public int Page { get; set; } = 1;

        /// <summary>页大小（&lt;=0 时按 20 处理；上限 <see cref="TaskHistoryPage.MaxPageSize"/>=200）</summary>
        public int PageSize { get; set; } = 20;

        /// <summary>规则编码精确匹配（cert_validation_rule.RuleCode）</summary>
        public string? RuleCode { get; set; }

        /// <summary>任务类型：TEST | NC_CHECK | REPORT_GENERATE</summary>
        public string? TaskType { get; set; }

        /// <summary>测试范围：FULL | NODE | AI_NODE</summary>
        public string? TestScope { get; set; }

        /// <summary>任务状态：queued | executing | completed | failed | cancelled</summary>
        public string? TaskStatus { get; set; }

        /// <summary>起始时间（按 wf_execution_task.CreateTime，含）</summary>
        public DateTime? StartTime { get; set; }

        /// <summary>结束时间（按 wf_execution_task.CreateTime，含当天则传次日 00:00 之前）</summary>
        public DateTime? EndTime { get; set; }
    }

    /// <summary>
    /// 测试历史列表行 —— 一条执行任务的摘要
    /// <para>额外带 <see cref="PathCount"/> / <see cref="NodeCount"/>，让列表页无需再发 N 次请求就能看出「这次跑了几条路径、几个节点」。</para>
    /// </summary>
    public class TaskHistoryItem
    {
        public string TaskCode { get; set; } = "";

        public string TaskType { get; set; } = "";

        /// <summary>测试范围：FULL | NODE | AI_NODE（阶段二新增列）</summary>
        public string TestScope { get; set; } = "";

        public string TaskStatus { get; set; } = "";

        public string RuleCode { get; set; } = "";

        public string EnterpriseCode { get; set; } = "";

        public string PhaseCode { get; set; } = "";

        public int? DurationMs { get; set; }

        public DateTime? StartedAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        public string ErrorMessage { get; set; } = "";

        public DateTime? CreateTime { get; set; }

        /// <summary>路径条数（wf_path_execution 行数，阶段三新增表）</summary>
        public int PathCount { get; set; }

        /// <summary>节点条数（wf_node_execution 去重后行数）</summary>
        public int NodeCount { get; set; }
    }

    /// <summary>测试历史分页结果</summary>
    public class TaskHistoryPage
    {
        /// <summary>页大小硬上限，防止前端传超大 PageSize 拖垮库</summary>
        public const int MaxPageSize = 200;

        public List<TaskHistoryItem> Items { get; set; } = new();

        public int TotalCount { get; set; }
    }

    /// <summary>
    /// 一次执行的四层聚合详情（阶段四）
    /// <para>一次请求拿齐 task / item / path / node，供「测试历史」抽屉直接渲染，避免前端发 4 次请求。</para>
    /// </summary>
    public class TaskExecutionDetail
    {
        /// <summary>第一层：任务</summary>
        public TaskHistoryItem Task { get; set; } = new();

        /// <summary>第二层：执行项（未来支持一任务多项）</summary>
        public List<TaskDetailItem> Items { get; set; } = new();

        /// <summary>第三层：路径（按 PathIndex 升序）</summary>
        public List<TaskPathDetail> Paths { get; set; } = new();

        /// <summary>第四层：节点（按 StartedAt, Id 升序 —— 与执行顺序一致）</summary>
        public List<TaskNodeDetail> Nodes { get; set; } = new();
    }

    /// <summary>第二层：执行项明细</summary>
    public class TaskDetailItem
    {
        public string ItemCode { get; set; } = "";

        public string RuleCode { get; set; } = "";

        public string ItemType { get; set; } = "";

        public string ItemStatus { get; set; } = "";

        public int? IsSuccess { get; set; }

        public string ErrorMessage { get; set; } = "";

        public int? DurationMs { get; set; }

        public DateTime? StartedAt { get; set; }

        public DateTime? CompletedAt { get; set; }
    }

    /// <summary>
    /// 第三层：路径明细
    /// <para><see cref="ReusedCount"/> 是「节点复用」在 DB 层的唯一证据 —— 节点表已按 NodeId 去重，
    /// 查不出路径维度（见 WfPathExecution 类注释）。</para>
    /// </summary>
    public class TaskPathDetail
    {
        public int PathIndex { get; set; }

        public string Status { get; set; } = "";

        /// <summary>本路径复用的节点数（未真跑，取自跨路径结果池）</summary>
        public int ReusedCount { get; set; }

        /// <summary>路径节点ID列表（按执行顺序，DB 里是 JSON 字符串，此处已解析为数组）</summary>
        public List<string> NodeIds { get; set; } = new();

        public string? FailedAtNodeId { get; set; }

        public string? ErrorMessage { get; set; }

        /// <summary>路径最终输出（已从 JSON 字符串解析为对象；超 64KB 时是 _truncated 信封）</summary>
        public JsonElement? Output { get; set; }

        public int? DurationMs { get; set; }

        public DateTime? StartedAt { get; set; }

        public DateTime? CompletedAt { get; set; }
    }

    /// <summary>第四层：节点明细</summary>
    public class TaskNodeDetail
    {
        public string NodeId { get; set; } = "";

        public string NodeType { get; set; } = "";

        public string NodeTitle { get; set; } = "";

        public string SkillCode { get; set; } = "";

        public string ExecStatus { get; set; } = "";

        /// <summary>节点输出（已解析为对象；超 64KB 时是 _truncated 信封）</summary>
        public JsonElement? Output { get; set; }

        public string? ErrorMessage { get; set; }

        public DateTime? StartedAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        public int? ExecutionTimeMs { get; set; }

        /// <summary>LLM Prompt Tokens（仅 ai_node 有值）</summary>
        public int? PromptTokens { get; set; }

        /// <summary>LLM Completion Tokens（仅 ai_node 有值）</summary>
        public int? CompletionTokens { get; set; }

        /// <summary>纯 LLM API 调用耗时(ms)</summary>
        public int? LlmDurationMs { get; set; }

        /// <summary>0=新执行 1=复用（注意：去重后 DB 里恒为 0，见 WfExecutionTaskService.SaveNodeExecutionsAsync 注释）</summary>
        public int IsReused { get; set; }
    }
}

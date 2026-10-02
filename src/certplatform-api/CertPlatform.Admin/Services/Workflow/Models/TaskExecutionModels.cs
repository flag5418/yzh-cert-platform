using System;
using System.Collections.Generic;

namespace CertPlatform.Admin.Services.Workflow.Models
{
    /// <summary>
    /// 任务执行请求 — 工作流执行 API 契约（前端 designer.vue test/run 现有零改动）
    /// <para>移植自：旧 WfExecutionTaskService.cs 内 TaskExecutionRequest</para>
    /// </summary>
    public class TaskExecutionRequest
    {
        /// <summary>任务类型：TEST | NC_CHECK | REPORT_GENERATE</summary>
        public string TaskType { get; set; } = "TEST";

        /// <summary>
        /// 测试范围：FULL | NODE | AI_NODE（仅 TaskType=TEST 时有效）
        /// <para>由各测试入口显式赋值，最终落到 wf_execution_task.TestScope。
        /// 不传时按 FULL 处理（兼容存量前端调用）。</para>
        /// </summary>
        public string? TestScope { get; set; }

        /// <summary>规则编码</summary>
        public string? RuleCode { get; set; }

        /// <summary>企业编码</summary>
        public string? EnterpriseCode { get; set; }

        /// <summary>标准编码</summary>
        public string? StandardCode { get; set; }

        /// <summary>阶段编码（★GUID，关联 <c>cert_cert_stage.Code</c>）
        /// <para>⚠️ 2026-09-30 起<b>统一传 GUID</b>。<c>wf_execution_task.PhaseCode</c> 已由
        /// <c>varchar(30)</c> 扩到 <c>varchar(36)</c>，装得下 32 位 GUID；人读短码不再走本字段。
        /// 传短码会导致 <c>NodeExecutor</c> 的 <c>StageCode</c> 过滤永不匹配
        /// （<c>cert_extraction_result.StageCode</c> 存 GUID）⇒ docfield 恒抛「缺失必要数据」。</para>
        /// </summary>
        public string? PhaseCode { get; set; }

        /// <summary>工作流配置 JSON（rule_json）</summary>
        public string? ConfigJson { get; set; }
    }

    /// <summary>
    /// 路径执行结果 — 对外 JSON 契约
    /// <para>字段命名遵循 YZH 命名铁律：C# 属性名 = JSON 字段名 = TS 字段名（PascalCase）。
    /// 全局序列化策略为 PropertyNamingPolicy = null，故属性名即 JSON 名，无需映射注解。</para>
    /// </summary>
    public class TaskPathResult
    {
        public int PathIndex { get; set; }
        public string Status { get; set; } = "pending";
        public string? FailedAtNodeId { get; set; }
        public string? Error { get; set; }
        public Dictionary<string, object> Output { get; set; } = new();
        public List<string> NodeIds { get; set; } = new();

        /// <summary>
        /// 路径内每个节点的执行明细（按执行顺序）
        /// <para>2026-09-22 新增：让前端能渲染「节点级子表」，不必为每条路径再发一次查询。
        /// 中间节点的输出/耗时/时序/复用标记都在这里，是「分支为何走这条路」的直接证据。</para>
        /// </summary>
        public List<NodeExecutionRecord> NodeResults { get; set; } = new();

        /// <summary>路径耗时(ms)</summary>
        public int DurationMs { get; set; }

        /// <summary>路径开始时间</summary>
        public DateTime StartedAt { get; set; }

        /// <summary>路径完成时间</summary>
        public DateTime CompletedAt { get; set; }
    }

    /// <summary>
    /// 任务执行响应 — 对外 JSON 契约
    /// <para>字段命名遵循 YZH 命名铁律：C# 属性名 = JSON 字段名 = TS 字段名（PascalCase）。</para>
    /// </summary>
    public class TaskExecutionResponse
    {
        public string TaskCode { get; set; } = "";

        public string ItemCode { get; set; } = "";

        public string Status { get; set; } = "";

        public bool IsSuccess { get; set; }

        /// <summary>★ 失败分类码（成功时为 null）
        /// <para>2026-09-30 新增。<c>DATA_MISSING</c>=引用的字段/表格无值（专家可补录解决）｜
        /// <c>SYSTEM_ERROR</c>=系统/配置错误（不是补录能解决的）。</para>
        /// </summary>
        public string? ErrorCode { get; set; }

        /// <summary>失败原因（成功时为 null）</summary>
        public string? Error { get; set; }

        /// <summary>★ 缺失必要数据的结构化详情（<c>ErrorCode == DATA_MISSING</c> 时非空）
        /// <para>2026-09-30 新增。任务执行器据此落 <c>cert_expert_task_data_gap</c> 一行并把
        /// 任务项标记为「数据不足，未检查」。<b>刻意不把异常对象本身透出</b>（跨层传异常会耦合）。</para>
        /// </summary>
        public MissingDataInfo? MissingData { get; set; }

        /// <summary>NC 执行结果（键为运行时动态键，由 Skill 执行器产出，不参与命名规约）</summary>
        public Dictionary<string, object> NcResult { get; set; } = new();

        public List<TaskPathResult> PathResults { get; set; } = new();

        public int DurationMs { get; set; }
    }

    /// <summary>
    /// ★ 缺失必要数据的结构化详情（扁平 DTO，不跨层传异常对象）
    /// </summary>
    public class MissingDataInfo
    {
        /// <summary><c>field</c> | <c>table</c></summary>
        public string DataKind { get; set; } = "";
        /// <summary>FieldCode 或 TableCode</summary>
        public string DataCode { get; set; } = "";
        /// <summary><c>not_found</c> | <c>is_empty</c></summary>
        public string Reason { get; set; } = "";
        /// <summary>★ 规则 Code（裁决 J4：补录回写的主键成分）</summary>
        public string? RuleCode { get; set; }
        public string? StandardFileCode { get; set; }
        public string? EnterpriseCode { get; set; }
        public string? StandardCode { get; set; }
        public string? StageCode { get; set; }

        /// <summary>由 <c>WorkflowDataMissingException</c> 投影（唯一的转换点）</summary>
        public static MissingDataInfo From(CertPlatform.Shared.Exceptions.WorkflowDataMissingException ex) => new()
        {
            DataKind = ex.DataKind,
            DataCode = ex.DataCode,
            Reason = ex.Reason,
            RuleCode = ex.RuleCode,
            StandardFileCode = ex.StandardFileCode,
            EnterpriseCode = ex.EnterpriseCode,
            StandardCode = ex.StandardCode,
            StageCode = ex.StageCode
        };
    }

    /// <summary>
    /// 单节点测试请求 — 前端 designer.vue test/node 契约
    /// </summary>
    public class NodeTestRequest
    {
        /// <summary>节点 ID</summary>
        public string? NodeId { get; set; }

        /// <summary>节点类型：skill / ai_node / docField / docTable / branch</summary>
        public string? NodeType { get; set; }

        /// <summary>节点标题</summary>
        public string? Title { get; set; }

        /// <summary>Skill 编码（功能节点必填）</summary>
        public string? SkillCode { get; set; }

        /// <summary>节点配置</summary>
        public Dictionary<string, object>? Config { get; set; }

        /// <summary>输入参数（portName → value）</summary>
        public Dictionary<string, string>? Inputs { get; set; }

        /// <summary>输入类型（portName → "link" | "constant"）</summary>
        public Dictionary<string, string>? InputTypes { get; set; }

        /// <summary>输入端口声明</summary>
        public List<PortConfig>? InputPorts { get; set; }

        /// <summary>输出端口声明</summary>
        public List<PortConfig>? OutputPorts { get; set; }

        // ──── 测试上下文元数据（2026-09-22 阶段二新增，全部可选）────
        // 用途：单节点测试改为走 WfExecutionTaskService 落库后，需要这些字段填充
        //      wf_execution_task / wf_node_execution 的归属信息，否则测试任务无法回溯。
        // 兼容性：均为可选，前端不发也能正常执行（落库时为空串）。

        /// <summary>规则编码（cert_validation_rule.Code），用于落库归属</summary>
        public string? RuleCode { get; set; }

        /// <summary>企业编码（运行时绑定，默认 YZH-STD-ENT 测试企业）</summary>
        public string? EnterpriseCode { get; set; }

        /// <summary>标准编码</summary>
        public string? StandardCode { get; set; }

        /// <summary>审核阶段编码</summary>
        public string? PhaseCode { get; set; }
    }

    /// <summary>
    /// 单节点测试响应 data
    /// <para>字段命名遵循 YZH 命名铁律：C# 属性名 = JSON 字段名 = TS 字段名（PascalCase）。</para>
    /// </summary>
    public class NodeTestResponse
    {
        /// <summary>
        /// 任务编码 —— 本次节点测试在 wf_execution_task 里的 Code
        /// <para>2026-09-22 阶段二新增：节点测试改为落库后，前端可凭此查
        /// wf_execution_task / wf_node_execution 回溯本次测试（阶段四「测试历史」入口依赖它）。</para>
        /// </summary>
        public string TaskCode { get; set; } = "";

        public bool Success { get; set; }

        public string? Error { get; set; }

        public Dictionary<string, object> Output { get; set; } = new();

        public int DurationMs { get; set; }
    }

    /// <summary>
    /// 节点审批请求
    /// </summary>
    public class NodeApprovalRequest
    {
        /// <summary>任务编码（wf_execution_task.Code）</summary>
        public string TaskCode { get; set; } = "";

        /// <summary>节点 ID</summary>
        public string NodeId { get; set; } = "";

        /// <summary>审批状态：approved / rejected</summary>
        public string ApprovalStatus { get; set; } = "approved";

        /// <summary>审批意见</summary>
        public string? Comment { get; set; }

        /// <summary>专家可信度评分 0.00~1.00</summary>
        public decimal? Confidence { get; set; }

        /// <summary>专家手动修改后的节点输出（JSON 字符串）</summary>
        public string? ManualResult { get; set; }
    }

}

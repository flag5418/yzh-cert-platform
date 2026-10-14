using System;
using System.Collections.Generic;

namespace CertPlatform.Shared.Workflow.Models
{
    /// <summary>
    /// 节点执行结果模型 — 单个节点执行后的标准输出
    /// <para>移植自：旧 Models/NodeExecutionResult.cs（含 PathResult/ItemExecutionResult/ParsedWorkflow）</para>
    /// <para>统一格式：{ success, error, result, durationMs }</para>
    /// </summary>
    public class NodeExecutionResult
    {
        /// <summary>该节点是否成功执行</summary>
        public bool Success { get; set; } = true;

        /// <summary>失败原因（成功时为 null）</summary>
        public string? Error { get; set; }

        /// <summary>★ 失败分类码（成功时为 null）
        /// <para>2026-09-30 新增。<b>为什么必须有</b>：缺陷 B1 未修时，「故意造的缺口」与
        /// 「取数口径错配造成的缺口」报的是同一句「缺失必要数据」⇒ 补录夹具<b>没有信号</b>。
        /// 分成两码后 UI 才能分别显示「去补录」（红）与「报 bug」（灰）。</para>
        /// <para>取值见 <see cref="WorkflowErrorCodes"/>：<c>DATA_MISSING</c> / <c>SYSTEM_ERROR</c>。</para>
        /// </summary>
        public string? ErrorCode { get; set; }

        /// <summary>节点输出（所有输出端口的值，key=端口名，value=输出值）</summary>
        public Dictionary<string, object> Output { get; set; } = new();

        /// <summary>执行耗时(ms)</summary>
        public int DurationMs { get; set; }

        /// <summary>是否复用了历史结果</summary>
        public bool IsReused { get; set; }

        /// <summary>开始执行时间（由 NodeExecutor 在真实执行前后写入，用于 wf_node_execution 时序还原）</summary>
        public DateTime StartedAt { get; set; }

        /// <summary>完成时间（与 StartedAt 同源，保证 StartedAt &lt;= CompletedAt）</summary>
        public DateTime CompletedAt { get; set; }

        // ── AI 节点专属元数据（阶段五：单节点执行详情增强）──

        /// <summary>LLM Prompt Tokens（仅 ai_node 有值）</summary>
        public int? PromptTokens { get; set; }

        /// <summary>LLM Completion Tokens（仅 ai_node 有值）</summary>
        public int? CompletionTokens { get; set; }

        /// <summary>纯 LLM API 调用耗时(ms)（不含参数解析、模板渲染、类型转换）</summary>
        public int? LlmDurationMs { get; set; }

        /// <summary>LLM 模型名（如 qwen-turbo，仅 ai_node 有值）</summary>
        public string? AiModel { get; set; }

        /// <summary>渲染后送 API 的完整 prompt（8KB 截断，仅 ai_node 有值）</summary>
        public string? AiPrompt { get; set; }

        /// <summary>
        /// ★ 2026-10-14 新增：渲染提示词时**实际使用**的参数池（参数名 → 值，仅 ai_node 有值）。
        ///
        /// <para><b>为什么必须有</b>：此前测试页的「参数池」是<b>控制器自己重算</b>出来的第二套结果
        /// （<c>WorkflowTestController.BuildAiNodeDebugInfo</c>），与引擎实际渲染用的池<b>可能不一致</b> ——
        /// 诊断面说谎，把排查方向带偏。现在改为由引擎<b>唯一产出</b>、原样回传。</para>
        /// </summary>
        public Dictionary<string, object>? AiParamPool { get; set; }

        /// <summary>
        /// ★ 2026-10-14 新增：渲染后**仍未解析**的模板引用（如 <c>文档字段.result</c>，仅 ai_node 有值）。
        ///
        /// <para>空列表 = 全部解析成功。<b>配置期测试</b>（<c>ExecuteWithMockAsync</c>）刻意不中断，
        /// 但必须把这份清单<b>显式回传前端</b>（原实现只写一条 <c>LogWarning</c>，界面上完全看不见
        /// ⇒ 用户以为「提示词没问题」）；<b>真实执行期</b>（<c>ExecuteAsync</c>）则据此抛
        /// <c>WorkflowDataMissingException</c> 中断当前检查项。</para>
        /// </summary>
        public List<string>? AiUnresolvedRefs { get; set; }

        /// <summary>快速创建成功结果</summary>
        public static NodeExecutionResult Ok(Dictionary<string, object> output, int durationMs = 0)
            => new() { Success = true, Output = output ?? new(), DurationMs = durationMs };

        /// <summary>快速创建失败结果</summary>
        public static NodeExecutionResult Fail(string error, int durationMs = 0)
            => new() { Success = false, Error = error, DurationMs = durationMs };

        /// <summary>快速创建失败结果（带输出字典，可携带 errorCode 等结构化信息）</summary>
        public static NodeExecutionResult Fail(string error, int durationMs, Dictionary<string, object> output)
            => new() { Success = false, Error = error, DurationMs = durationMs, Output = output ?? new() };

        /// <summary>快速创建失败结果（带分类码）</summary>
        public static NodeExecutionResult Fail(string error, int durationMs, string errorCode)
            => new() { Success = false, Error = error, DurationMs = durationMs, ErrorCode = errorCode };
    }

    /// <summary>
    /// ★ 工作流失败分类码（2026-09-30）
    /// </summary>
    /// <para><b>为什么必须有</b>：补录链路的验证依赖「故意造的缺口」与「代码 bug」可区分。
    /// 只有一句 <c>Error</c> 文本时，UI 上两者长得一样 ⇒ 夹具失效（25 号 §6.3）。</para>
    /// </summary>
    public static class WorkflowErrorCodes
    {
        /// <summary>★ 缺失必要数据 —— 引用的字段/表格没有可用值（<c>WorkflowDataMissingException</c>）
        /// <para>UI：红色 · 「去补录」</para>
        /// </summary>
        public const string DataMissing = "DATA_MISSING";

        /// <summary>系统错误 —— 取数异常 / 节点配置错 / Skill 内部异常
        /// <para>UI：灰色 · 「报 bug」（<b>不是</b>专家能补录解决的）</para>
        /// </summary>
        public const string SystemError = "SYSTEM_ERROR";

        /// <summary>
        /// ★ <b>前置为空</b>（2026-10-12 新增）—— 节点的入参为空，而该参数声明了
        /// <c>[SkillParam(AllowEmpty = SkillParamEmptyPolicy.Deny)]</c>（即「本节点不接受空值」）。
        ///
        /// <para><b>语义</b>：这不是「系统故障」，也不是「不符合」，而是<b>无法判定</b> ——
        /// 上游没给值，本节点没法算。⛔ 不得拿着空值静默算出一个看起来正常的结论。</para>
        ///
        /// <para><b>引擎动作</b>：<b>全局中断当前检查项 / 章节</b>（走
        /// <c>WorkflowDataMissingException</c>）+ 落补录缺口 + 标「不可判定」，
        /// 让用户能回到「启动前补录」通道 —— 闭环。</para>
        ///
        /// <para><b>UI</b>：红/黄 · 「去补录」（与 <see cref="DataMissing"/> 同处置人 = 企业）。</para>
        ///
        /// <para><b>⚠️ 与 <see cref="DataMissing"/> 的分工</b>：<c>DATA_MISSING</c> 是
        /// <b>取数节点</b>（docfield / doctable / is_std_file_missing）自己发现数据没有；
        /// <c>PRECONDITION_EMPTY</c> 是 <b>消费节点</b>（compare / ai_node / assemble …）
        /// 发现上游没给值。前者由取数方判定，后者由消费方判定（用户 2026-10-12 裁定：
        /// 「控制空值还是节点自身的行为来判断」）。</para>
        /// </summary>
        public const string PreconditionEmpty = "PRECONDITION_EMPTY";

        /// <summary>
        /// ★ <b>配置错误</b>（2026-10-12 新增）—— 规则 / 字段 / 表格 Code 不存在，
        /// 或 skill 的反射定位失败（找不到类型 / 缺 <c>[Skill]</c> / 找不到静态方法）。
        ///
        /// <para><b>与 <see cref="SystemError"/> 的区别（唯一判据 = 谁能修）</b>：
        /// 配置错由<b>规则作者</b>改配置即可解决（改 ruleCode / 重连端口）；
        /// 系统异常要<b>报开发</b>。两者界面上都是灰色，但提示语不同
        /// （「改配置」 vs 「报 bug」）。</para>
        ///
        /// <para><b>引擎动作</b>：终止当前<b>路径</b>（⛔ 不落补录缺口 —— 补录解决不了配置错）。</para>
        /// </summary>
        public const string ConfigError = "CONFIG_ERROR";
    }

    /// <summary>
    /// 节点执行明细 — 路径内单个节点的一次执行记录
    /// <para>存在的意义：<see cref="PathResult.NodeIds"/> 只保留节点 ID 列表，
    /// 中间节点的输出/耗时/时序在路径聚合时会被丢弃，导致 wf_node_execution 只能靠路径级状态反推
    /// （所有节点同状态、耗时硬编码 0、只有最后一个节点有输出）。本类型把 per-node 事实原样带出来。</para>
    /// </summary>
    public class NodeExecutionRecord
    {
        /// <summary>节点 ID</summary>
        public string NodeId { get; set; } = "";

        /// <summary>该节点本次是否执行成功</summary>
        public bool Success { get; set; }

        /// <summary>失败原因（成功时为 null）</summary>
        public string? Error { get; set; }

        /// <summary>★ 失败分类码（见 <see cref="WorkflowErrorCodes"/>）</summary>
        public string? ErrorCode { get; set; }

        /// <summary>节点输出（所有输出端口的 JSON；中间节点同样保留）</summary>
        public Dictionary<string, object> Output { get; set; } = new();

        /// <summary>该节点耗时(ms)</summary>
        public int DurationMs { get; set; }

        /// <summary>是否为跨路径复用（true=本路径未真跑，取自结果池）</summary>
        public bool IsReused { get; set; }

        /// <summary>开始时间</summary>
        public DateTime StartedAt { get; set; }

        /// <summary>完成时间</summary>
        public DateTime CompletedAt { get; set; }

        /// <summary>LLM Prompt Tokens（仅 ai_node 有值）</summary>
        public int? PromptTokens { get; set; }

        /// <summary>LLM Completion Tokens（仅 ai_node 有值）</summary>
        public int? CompletionTokens { get; set; }

        /// <summary>纯 LLM API 调用耗时(ms)</summary>
        public int? LlmDurationMs { get; set; }

        /// <summary>LLM 模型名（仅 ai_node 有值）</summary>
        public string? AiModel { get; set; }

        /// <summary>渲染后送 API 的完整 prompt（仅 ai_node 有值）</summary>
        public string? AiPrompt { get; set; }

        /// <summary>由一次节点执行结果构造记录（isReused=true 表示本次未真跑，取自结果池）</summary>
        public static NodeExecutionRecord From(string nodeId, NodeExecutionResult result, bool isReused)
            => new()
            {
                NodeId = nodeId,
                Success = result.Success,
                Error = result.Error,
                ErrorCode = result.ErrorCode,
                Output = result.Output ?? new(),
                DurationMs = result.DurationMs,
                IsReused = isReused,
                StartedAt = result.StartedAt,
                CompletedAt = result.CompletedAt,
                PromptTokens = result.PromptTokens,
                CompletionTokens = result.CompletionTokens,
                LlmDurationMs = result.LlmDurationMs,
                AiModel = result.AiModel,
                AiPrompt = result.AiPrompt
            };
    }

    /// <summary>
    /// 路径执行结果 — 一条路径（start → ... → end）执行完成后的状态
    /// </summary>
    public class PathResult
    {
        /// <summary>路径索引（从 0 开始）</summary>
        public int PathIndex { get; set; }

        /// <summary>
        /// 路径状态：<c>pending</c> / <c>executing</c> / <c>completed</c> / <c>failed</c> / <c>skipped</c>
        ///
        /// <para>★ <b><c>skipped</c>（2026-10-12 新增）</b>：本路径走的是某个 <c>branch</c> 节点
        /// <b>未被选中</b>的那条分支 ⇒ <b>整条路径不执行</b>。</para>
        ///
        /// <para><b>为什么必须有这个状态</b>：路径在<b>解析期</b>静态枚举
        /// （<c>WorkflowConfigParser.EnumeratePaths</c> 把 branch 的两条臂都枚举成独立路径），
        /// 而 branch 的判定只有<b>运行期</b>才知道。修复前解释器无条件执行全部路径 ⇒
        /// <c>if/else</c> 的<b>两个分支都会跑</b>，分支节点输出的 <c>decision</c> 成了死数据。</para>
        ///
        /// <para>计入 <c>skippedPaths</c>，⛔ <b>不计入</b> <c>successPaths</c> / <c>failedPaths</c>
        /// （跳过不是失败，也不是成功）。</para>
        /// </summary>
        public string Status { get; set; } = "pending";

        /// <summary>
        /// ★ <b>跳过原因</b>（仅 <c>Status == "skipped"</c> 时非空）——
        /// 由哪个 branch 节点、判定为什么、本路径实际走的是哪条臂。
        ///
        /// <para>⛔ <b>刻意与 <see cref="Error"/> 分开</b>：前端把 <c>Error</c> 渲染成红色
        /// 「失败节点 / 错误」块，而「跳过」是**正常业务行为**（条件不满足），
        /// 复用 <c>Error</c> 会让规则作者以为工作流坏了。</para>
        /// </summary>
        public string? SkipReason { get; set; }

        /// <summary>失败时，失败的节点 ID</summary>
        public string? FailedAtNodeId { get; set; }

        /// <summary>失败原因</summary>
        public string? Error { get; set; }

        /// <summary>
        /// ★ <b>失败分类码</b>（2026-10-12 新增）—— 取自失败节点的
        /// <see cref="NodeExecutionResult.ErrorCode"/>。
        ///
        /// <para><b>为什么路径也要带</b>：修复前路径级失败只带一句 <c>Error</c> 文本，
        /// <see cref="ItemExecutionResult.ErrorCode"/> 也只在 <c>DATA_MISSING</c>
        /// 那条 catch 分支里被赋值 ⇒ 其余失败（<c>CONFIG_ERROR</c> / <c>SYSTEM_ERROR</c> /
        /// <c>PRECONDITION_EMPTY</c>）到不了 UI，界面只能显示「工作流执行失败」。
        /// 带上它之后 <c>WorkflowInterpreter</c> 才能把它冒泡到 item 级。</para>
        /// </summary>
        public string? ErrorCode { get; set; }

        /// <summary>路径最终输出（end 节点的输出）</summary>
        public Dictionary<string, object> Output { get; set; } = new();

        /// <summary>路径中所有节点的 ID 列表（按执行顺序）</summary>
        public List<string> NodeIds { get; set; } = new();

        /// <summary>
        /// 路径内每个节点的执行明细（按执行顺序）
        /// <para>与 <see cref="NodeIds"/> 的区别：这里携带 per-node 的真实状态/输出/耗时/时序/复用标记。
        /// 落库时以本列表为准，不再用路径级 Status 反推节点状态。</para>
        /// </summary>
        public List<NodeExecutionRecord> NodeResults { get; set; } = new();

        /// <summary>路径耗时(ms)——为 wf_path_execution 表（阶段三）预留，当前仅内存/响应可用</summary>
        public int DurationMs { get; set; }

        /// <summary>路径开始时间</summary>
        public DateTime StartedAt { get; set; }

        /// <summary>路径完成时间</summary>
        public DateTime CompletedAt { get; set; }
    }

    /// <summary>
    /// Item 执行结果 — 一个 NC 检查项所有路径执行完成后的汇总
    /// </summary>
    public class ItemExecutionResult
    {
        /// <summary>是否成功（至少一条路径到达 end 且成功）</summary>
        public bool Success { get; set; }

        /// <summary>失败原因（所有失败路径的原因汇总）</summary>
        public string? Error { get; set; }

        /// <summary>★ 失败分类码（见 <see cref="WorkflowErrorCodes"/>）；成功时为 null</summary>
        public string? ErrorCode { get; set; }

        /// <summary>★ 缺失必要数据的结构化详情（<see cref="WorkflowErrorCodes.DataMissing"/> 时非空）
        /// <para>2026-09-30 新增。上层据此生成 <c>cert_expert_task_data_gap</c> 补录记录
        /// （<c>DataKind</c> / <c>DataCode</c> / <c>Reason</c> / <c>RuleCode</c> / 作用域四元组）。</para>
        /// </summary>
        public CertPlatform.Shared.Exceptions.WorkflowDataMissingException? MissingData { get; set; }

        /// <summary>业务成功标志（对应 wf_execution_task_item.is_success）</summary>
        public bool IsSuccess { get; set; }

        /// <summary>所有路径的执行结果</summary>
        public List<PathResult> PathResults { get; set; } = new();

        /// <summary>执行耗时(ms)</summary>
        public int DurationMs { get; set; }

        /// <summary>NC 最终结果（统一格式：success + error + result）</summary>
        public Dictionary<string, object> NcResult { get; set; } = new();
    }

    /// <summary>
    /// 解析后的工作流模型 — WorkflowConfigParser.Parse 的返回值
    /// 包含拓扑分析后的可执行结构
    /// </summary>
    public class ParsedWorkflow
    {
        /// <summary>原始配置</summary>
        public WorkflowConfig Config { get; set; } = new();

        /// <summary>起始节点</summary>
        public WorkflowNodeConfig StartNode { get; set; } = new();

        /// <summary>节点映射：nodeId → 节点配置</summary>
        public Dictionary<string, WorkflowNodeConfig> NodeMap { get; set; } = new();

        /// <summary>邻接表：nodeId → 出边列表</summary>
        public Dictionary<string, List<WorkflowEdgeConfig>> Adjacency { get; set; } = new();

        /// <summary>入边表：nodeId → 入边列表</summary>
        public Dictionary<string, List<WorkflowEdgeConfig>> InEdges { get; set; } = new();

        /// <summary>所有路径（从 start 到 end 的完整路径列表）</summary>
        public List<List<WorkflowNodeConfig>> Paths { get; set; } = new();
    }
}

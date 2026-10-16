using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using CertPlatform.Admin.Services.Workflow;
using CertPlatform.Shared.Workflow.Models;
using YZH.Core.Stand.Models.Result;

namespace CertPlatform.Admin.Controllers.Workflow
{
    /// <summary>
    /// 工作流执行引擎 — 测试/执行 API
    /// <para>移植自：旧 WorkflowTestController.cs（676 行，git 历史恢复件 e5ae841 为底稿）</para>
    /// <para>路由决策 D-1 方案 A：[Route("api/Workflow")] 固定前缀，URL 与前端现有调用完全一致（零前端破坏）</para>
    /// <para>功能分组：</para>
    /// <para>1. 执行：POST test/run（整流验证）/ POST test/node（单节点）/ POST test/ai-node（AI 节点）</para>
    /// <para>2. 查询（阶段四 2026-09-22 新增，只读）：POST test/history（分页历史）/ GET test/detail/{taskCode}（四层聚合详情）</para>
    /// <para>3. 健康：GET health</para>
    /// <para>裁剪说明（决策 D-6）：正式执行 run 端点与 config 读写端点本期不移植（正式执行涉及 OutputTarget 与队列，范围外；
    /// <para>config 读写前端走 ValidationRule 控制器）</para>
    ///
    /// <para><b>2026-09-22 阶段二：测试入口统一</b></para>
    /// <para>本控制器<b>不再直接调用</b> <c>NodeExecutor</c> / <c>AiNodeExecutor</c>，三个入口全部委派
    /// <see cref="WfExecutionTaskService"/>，从而共享同一套落库（wf_execution_task / wf_node_execution）
    /// 与日志链。控制器只剩两件事：</para>
    /// <list type="number">
    ///   <item>参数校验 + 委派</item>
    ///   <item>把服务层结果翻译成对外响应契约（含 AI 节点的 Debug 展示信息）</item>
    /// </list>
    /// <para>对外响应契约保持不变，前端零改动。</para>
    /// </summary>
/// <para><b>★ 端标记（2026-10-03）</b>：路由加 <c>Admin/</c> 段，与专家端 <c>/api/Auditor/*</c> 对称。
/// <para>背景：后台端 20 个 Controller 此前零端标记，4 个连业务域前缀都没有（<c>api/AIUsage</c>
/// <c>api/PromptTemplate</c> <c>api/ValidationRule</c> <c>api/ReportDefinition</c>），
/// 且 <c>api/System/[controller]</c> 与框架层 <c>YZH.Core.Web</c> 的 <c>api/System/*</c> 撞前缀。</para>
/// <para><b>不影响授权</b>：<c>ApiCode = Sha256("{METHOD}|{路由末段}|{动作名}")</c>（ApiScanner.cs:326-331）
/// 只取路由<b>末段</b>作控制器名，本 Controller 的末段未变 ⇒ <c>ApiCode</c> 不变 ⇒
/// <b>角色-接口关联不断裂</b>，无需重跑 ApiSync。</para>
    [Route("api/Admin/Workflow")]
    [Authorize]
    public class WorkflowTestController : ControllerBase
    {
        private readonly WfExecutionTaskService _taskService;
        private readonly ILogger<WorkflowTestController> _logger;

        public WorkflowTestController(
            WfExecutionTaskService taskService,
            ILogger<WorkflowTestController> logger)
        {
            _taskService = taskService;
            _logger = logger;
        }

        // ════════════════════════════════════════
        // 执行相关
        // ════════════════════════════════════════

        /// <summary>
        /// 配置验证 — 前端传 rule_json，同步执行，返回完整结果
        /// <para>用途：NC 配置页面点击「运行」按钮</para>
        /// <para>TestScope=FULL：穷举所有路径的整流测试</para>
        /// </summary>
        [HttpPost("test/run")]
        public async Task<IActionResult> TestRun([FromBody] TaskExecutionRequest request, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(request?.ConfigJson))
                return Ok(ApiResponse.Fail("configJson 不能为空"));

            request.TaskType = "TEST";
            request.TestScope = "FULL";

            try
            {
                var response = await _taskService.CreateAndRunAsync(request, ct);

                // 即使 status=failed 也返回 200，让前端能拿到错误信息
                return Ok(ApiResponse<TaskExecutionResponse>.Ok(response, "执行完成"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TEST_RUN_FAIL] ruleCode={RuleCode}", request.RuleCode);
                return Ok(ApiResponse.Fail($"工作流执行失败: {ex.Message}"));
            }
        }

        /// <summary>
        /// 单节点测试 — 执行单个节点并返回结果
        /// <para>用途：NC 配置页面点击「测试节点」按钮</para>
        /// <para>说明：连线输入在测试时按常量处理（无需上游节点）</para>
        /// <para>TestScope=NODE：落库（wf_execution_task.TestScope='NODE'），可回溯</para>
        /// </summary>
        [HttpPost("test/node")]
        public async Task<IActionResult> TestNode([FromBody] NodeTestRequest request, CancellationToken ct)
        {
            if (request == null)
                return Ok(ApiResponse.Fail("请求体不能为空"));

            try
            {
                var outcome = await _taskService.RunSingleNodeAsync(request, ct);

                _logger.LogInformation(
                    "[TEST_NODE] taskCode={TaskCode}, nodeType={NodeType}, skillCode={SkillCode}, success={Success}",
                    outcome.TaskCode, request.NodeType, request.SkillCode, outcome.Success);

                return Ok(ApiResponse<NodeTestResponse>.Ok(new NodeTestResponse
                {
                    TaskCode = outcome.TaskCode,
                    Success = outcome.Success,
                    Error = outcome.Error,
                    Output = outcome.Output,
                    // ★ 声明为连线、但本次取不到上游值的端口 —— 单节点测试不执行上游，
                    //   这是**正常的能力边界**而非节点错配，必须显式告诉用户（⛔ 不静默）。
                    UnresolvedInputs = outcome.UnresolvedInputs,
                    DurationMs = outcome.DurationMs
                }, "节点测试完成"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TEST_NODE_FAIL] nodeType={NodeType}, skillCode={SkillCode}",
                    request.NodeType, request.SkillCode);
                return Ok(ApiResponse.Fail($"节点执行失败: {ex.Message}"));
            }
        }

        /// <summary>
        /// AI 节点测试 — 执行单个 AI 节点并返回结果
        /// <para>用途：NC 配置页面点击 AI 节点「测试」按钮</para>
        /// <para>特殊说明：AI 节点需要完整工作流上下文（上游节点输出），通过 mockOutputs 传入；
        /// mockOutputs 为空时自动解析 ruleJson 并执行上游节点（旧版关键逻辑，已迁入服务层，完整保留）</para>
        /// <para>TestScope=AI_NODE：落库路径 = [实际上游节点…, 目标 AI 节点]</para>
        /// <para>文档：AI节点-详细设计-V1 §7</para>
        /// </summary>
        [HttpPost("test/ai-node")]
        public async Task<IActionResult> TestAiNode([FromBody] AiNodeTestRequest request, CancellationToken ct)
        {
            if (request == null)
                return Ok(ApiResponse.Fail("请求体不能为空"));

            try
            {
                var outcome = await _taskService.RunAiNodeAsync(request, ct);

                _logger.LogInformation(
                    "[TEST_AI_NODE] taskCode={TaskCode}, nodeId={NodeId}, success={Success}",
                    outcome.TaskCode, request.NodeId, outcome.Success);

                return Ok(ApiResponse<AiNodeTestResponse>.Ok(new AiNodeTestResponse
                {
                    TaskCode = outcome.TaskCode,
                    Success = outcome.Success,
                    Error = outcome.Error,
                    Result = outcome.Success ? outcome.Output.GetValueOrDefault("result") : null,
                    DurationMs = outcome.DurationMs,
                    Debug = BuildAiNodeDebugInfo(request, outcome)
                }, "AI 节点测试完成"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TEST_AI_NODE_FAIL] nodeId={NodeId}", request.NodeId);
                return Ok(ApiResponse.Fail($"AI 节点执行失败: {ex.Message}"));
            }
        }

        // ════════════════════════════════════════
        // 测试历史（只读，阶段四 2026-09-22）
        // ════════════════════════════════════════

        /// <summary>
        /// 分页查询测试历史
        /// <para>用途：NC / 报告规则配置页的「测试历史」抽屉列表。</para>
        /// <para>查询对象是四层模型第一层 <c>wf_execution_task</c>；命名虽在 test 域下，
        /// 但 NC_CHECK / REPORT_GENERATE 产生的任务同样可查（靠 <c>TaskType</c> 筛选）。</para>
        /// <para>全部筛选条件可选，彼此 AND；不传即「最近的测试」。</para>
        /// </summary>
        [HttpPost("test/history")]
        public async Task<IActionResult> TestHistory([FromBody] TaskHistoryRequest request)
        {
            try
            {
                var page = await _taskService.QueryHistoryAsync(request ?? new TaskHistoryRequest());
                return Ok(ApiResponse<TaskHistoryPage>.Ok(page, "查询完成"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TEST_HISTORY_FAIL] ruleCode={RuleCode}", request?.RuleCode);
                return Ok(ApiResponse.Fail($"测试历史查询失败: {ex.Message}"));
            }
        }

        /// <summary>
        /// 查询一次执行的四层聚合详情（task + item + path + node）
        /// <para>用途：「测试历史」抽屉展开某次记录时，一次请求渲染完整日志，前端不必串行发 4 次。</para>
        /// <para>返回 <c>TaskExecutionDetail</c>；任务不存在时返回 404（而非空对象），让前端能区分「查不到」与「查到了但没数据」。</para>
        /// </summary>
        [HttpGet("test/detail/{taskCode}")]
        public async Task<IActionResult> TestDetail(string taskCode)
        {
            try
            {
                var detail = await _taskService.GetExecutionDetailAsync(taskCode);
                if (detail == null)
                    return Ok(ApiResponse.Fail($"执行任务不存在: {taskCode}"));

                return Ok(ApiResponse<TaskExecutionDetail>.Ok(detail, "查询完成"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TEST_DETAIL_FAIL] taskCode={TaskCode}", taskCode);
                return Ok(ApiResponse.Fail($"执行详情查询失败: {ex.Message}"));
            }
        }

        /// <summary>
        /// 健康检查 — 验证引擎是否正常启动
        /// </summary>
        [HttpGet("health")]
        [AllowAnonymous]
        public IActionResult Health()
        {
            return Ok(new
            {
                success = true,
                status = "healthy",
                engine = "WorkflowEngine-V3-Migrated",
                modules = new[]
                {
                    "WorkflowConfigParser",
                    "NodeExecutor",
                    "AiNodeExecutor",
                    "WorkflowInterpreter",
                    "TaskCacheService",
                    "WorkflowLogger",
                    "WfExecutionTaskService",
                    "CertSkillRegistry",
                    "SkillExecutor"
                }
            });
        }

        // ════════════════════════════════════════
        // 辅助方法
        // ════════════════════════════════════════

        /// <summary>
        /// 构造 AI 节点的 Debug 展示信息（纯展示层逻辑，不参与执行）
        ///
        /// <para><b>★ 2026-10-14 重写（用户裁定 · 第 0 批止血）—— 删除「自算渲染」逻辑。</b></para>
        ///
        /// <para><b>旧实现的三宗罪</b>（缺陷 D2，是用户排查被带偏的直接原因）：</para>
        /// <list type="number">
        ///   <item>它用<b>第二套渲染实现</b>重算提示词（自己写正则、自己拼 <c>paramPool</c>），
        ///     与引擎 <c>AiNodeExecutor</c> 的口径可能不一致 ⇒ 显示的未必是真正送 LLM 的那份；</item>
        ///   <item>渲染被 <c>if (!string.IsNullOrEmpty(template) &amp;&amp; debugInfo.ParamPool.Count &gt; 0)</c>
        ///     包住 ⇒ <c>ParamPool</c> 为空时 <c>RenderedPrompt</c> <b>恒为 <c>""</c></b>，
        ///     界面显示「提示词是空的」，而引擎其实<b>照常把未替换的花括号原文送给了大模型</b>；</item>
        ///   <item>未解析引用只写一条 <c>LogWarning</c>，<b>界面上完全看不见</b>。</item>
        /// </list>
        ///
        /// <para><b>现在的纪律</b>：引擎怎么渲的就怎么显示 —— <c>RenderedPrompt</c> / <c>ParamPool</c> /
        /// <c>UnresolvedRefs</c> 全部取自 <see cref="NodeScopeRunOutcome"/>（由
        /// <c>NodeExecutionResult.AiPrompt / AiParamPool / AiUnresolvedRefs</c> 带出）。
        /// <b>同一逻辑只允许一处实现</b> —— 这是 D2 的根因，只修一处必然再分叉。</para>
        ///
        /// <para><c>ParamsUsed</c> 是唯一例外：它描述「本节点<b>声明了</b>哪些参数」，
        /// 属<b>配置事实</b>而非执行结果，故在此从 <c>request.Config</c> 读取。</para>
        /// </summary>
        private static AiNodeDebugInfo BuildAiNodeDebugInfo(AiNodeTestRequest request, NodeScopeRunOutcome outcome)
        {
            return new AiNodeDebugInfo
            {
                // ── 引擎真实产物：唯一数据源，⛔ 控制器不再重算 ──
                RenderedPrompt = outcome.AiPrompt ?? string.Empty,
                ParamPool = outcome.AiParamPool ?? new Dictionary<string, object>(),
                UnresolvedRefs = outcome.AiUnresolvedRefs ?? new List<string>(),

                // ── 配置事实：本节点声明了哪些参数 ──
                ParamsUsed = ExtractDeclaredParams(request),

                // ── LLM 返回 ──
                LlmResponse = outcome.Output?.GetValueOrDefault("result")?.ToString() ?? string.Empty,
                ConvertedResult = outcome.Output?.GetValueOrDefault("result")
            };
        }

        /// <summary>
        /// 提取 AI 节点<b>声明的参数清单</b>（纯配置事实，与执行结果无关）
        ///
        /// <para>两种来源，<b>新格式优先</b>：</para>
        /// <list type="number">
        ///   <item><c>config.params</c> —— 新方案（参数表 schema：<c>name/label/sourceType/nodeId/portName</c>）</item>
        ///   <item><c>config.customParams</c> —— 旧方案兜底（字符串化 JSON 或数组；无 <c>label</c>）</item>
        /// </list>
        ///
        /// <para>⚠️ 这里只<b>读</b>，不做任何校验 —— 配置期强校验属第 1 批（设计文档 §2.7）。
        /// 解析失败只让 Debug 少显示一块，<b>绝不影响主流程</b>。</para>
        /// </summary>
        private static List<AiParamUsage> ExtractDeclaredParams(AiNodeTestRequest request)
        {
            var list = new List<AiParamUsage>();
            var config = request.Config ?? new Dictionary<string, object>();

            // ① 新方案：config.params
            if (config.TryGetValue("params", out var paramsValue) && paramsValue != null)
            {
                try
                {
                    var raw = paramsValue is string s ? s : JsonSerializer.Serialize(paramsValue);
                    var parsed = JsonSerializer.Deserialize<List<AiParamUsage>>(
                        raw, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (parsed != null && parsed.Count > 0)
                        return parsed;
                }
                catch { /* Debug 展示失败不影响主流程 */ }
            }

            // ② 旧方案兜底：config.customParams
            if (config.TryGetValue("customParams", out var cpValue) && cpValue != null)
            {
                try
                {
                    var raw = cpValue is string cs ? cs : JsonSerializer.Serialize(cpValue);
                    var oldParams = JsonSerializer.Deserialize<List<CustomParam>>(
                        raw, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (oldParams != null)
                    {
                        foreach (var p in oldParams)
                        {
                            list.Add(new AiParamUsage
                            {
                                Name = p.ParamName ?? "",
                                Label = null,   // 旧格式没有中文说明字段
                                SourceType = p.SourceType ?? "",
                                NodeId = p.SourceConfig?.GetValueOrDefault("nodeId")?.ToString(),
                                PortName = p.SourceConfig?.GetValueOrDefault("portName")?.ToString()
                            });
                        }
                    }
                }
                catch { /* 同上 */ }
            }

            return list;
        }

        // ════════════════════════════════════════
        // 节点审批
        // ════════════════════════════════════════

        /// <summary>
        /// 提交/更新节点审批
        /// <para>用途：审核员对单条执行记录中的节点结果进行认可/驳回/修改。</para>
        /// </summary>
        [HttpPost("approve")]
        public async Task<IActionResult> ApproveNode([FromBody] NodeApprovalRequest request, CancellationToken ct)
        {
            if (request == null || string.IsNullOrEmpty(request.TaskCode) || string.IsNullOrEmpty(request.NodeId))
                return Ok(ApiResponse.Fail("TaskCode 和 NodeId 不能为空"));

            if (request.ApprovalStatus != "approved" && request.ApprovalStatus != "rejected")
                return Ok(ApiResponse.Fail("ApprovalStatus 必须为 approved 或 rejected"));

            try
            {
                var result = await _taskService.ApproveNodeAsync(request, ct);
                return Ok(ApiResponse<object>.Ok(new { result }, "审批成功"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[APPROVE_FAIL] taskCode={TaskCode}, nodeId={NodeId}", request.TaskCode, request.NodeId);
                return Ok(ApiResponse.Fail($"审批失败: {ex.Message}"));
            }
        }
    }
}

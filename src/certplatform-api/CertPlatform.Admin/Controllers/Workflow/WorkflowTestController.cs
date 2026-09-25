using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using CertPlatform.Admin.Services.Workflow;
using CertPlatform.Admin.Services.Workflow.Models;
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
    [Route("api/Workflow")]
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
        /// <para>展示内容：模板中 <c>{{xxx.yyy}}</c> 引用解析后的 paramPool、渲染后的提示词、LLM 原始返回。</para>
        /// </summary>
        private static AiNodeDebugInfo BuildAiNodeDebugInfo(AiNodeTestRequest request, NodeScopeRunOutcome outcome)
        {
            var debugInfo = new AiNodeDebugInfo();

            if (!outcome.Success || outcome.Output == null)
                return debugInfo;

            var config = request.Config ?? new Dictionary<string, object>();
            var template = config.GetValueOrDefault("promptTemplate")?.ToString() ?? "";
            debugInfo.ParamPool = new Dictionary<string, object>();

            // 1. 兼容旧 customParams 配置
            if (config.TryGetValue("customParams", out var cpValue))
            {
                try
                {
                    var customParams = JsonSerializer.Deserialize<List<CustomParam>>(
                        cpValue.ToString() ?? "[]",
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (customParams != null)
                    {
                        foreach (var param in customParams)
                        {
                            if (param.SourceType == "link")
                            {
                                var nodeId = param.SourceConfig?.GetValueOrDefault("nodeId")?.ToString();
                                if (nodeId != null && outcome.MockOutputs.TryGetValue(nodeId, out var mo))
                                {
                                    var portName = param.SourceConfig?.GetValueOrDefault("portName")?.ToString() ?? "result";
                                    if (mo.TryGetValue(portName, out var portValue))
                                        debugInfo.ParamPool[param.ParamName] = portValue;
                                    else if (mo.TryGetValue("result", out var resultValue))
                                        debugInfo.ParamPool[param.ParamName] = resultValue;
                                    else
                                        debugInfo.ParamPool[param.ParamName] = mo;
                                }
                            }
                            else if (param.SourceType == "constant")
                            {
                                debugInfo.ParamPool[param.ParamName] =
                                    param.SourceConfig?.GetValueOrDefault("value")?.ToString() ?? string.Empty;
                            }
                        }
                    }
                }
                catch { /* ignore debug parse errors */ }
            }

            // 2. 从模板 {{节点名.端口}} 提取引用（新方案）
            if (!string.IsNullOrEmpty(template))
            {
                var refRegex = new Regex(@"\{\{([\w\u4e00-\u9fff][\w\u4e00-\u9fff.]*)\}\}");
                var matches = refRegex.Matches(template);
                foreach (Match m in matches)
                {
                    var fullKey = m.Groups[1].Value;
                    if (debugInfo.ParamPool.ContainsKey(fullKey))
                        continue;

                    var lastDot = fullKey.LastIndexOf('.');
                    string refKey = lastDot >= 0 ? fullKey[..lastDot] : fullKey;
                    string portName = lastDot >= 0 ? fullKey[(lastDot + 1)..] : "";

                    if (outcome.FlattenedOutputs.TryGetValue(refKey, out var nodeOutput))
                    {
                        object? val;
                        if (nodeOutput is Dictionary<string, object> dict)
                        {
                            if (!string.IsNullOrEmpty(portName) && dict.TryGetValue(portName, out var pv))
                                val = pv;
                            else if (dict.TryGetValue("result", out var rv))
                                val = rv;
                            else if (dict.Count == 1)
                                val = dict.Values.First();
                            else
                                val = dict;
                        }
                        else
                        {
                            val = nodeOutput;
                        }
                        debugInfo.ParamPool[fullKey] = val ?? "";
                    }
                }
            }

            // 3. 计算 renderedPrompt
            if (!string.IsNullOrEmpty(template) && debugInfo.ParamPool.Count > 0)
            {
                var rendered = template;
                foreach (var (k, v) in debugInfo.ParamPool)
                {
                    var vStr = v switch
                    {
                        string s => s,
                        _ => JsonSerializer.Serialize(v)
                    };
                    rendered = rendered.Replace($"{{{{{k}}}}}", vStr);
                }
                debugInfo.RenderedPrompt = rendered;
            }

            debugInfo.LlmResponse = outcome.Output.GetValueOrDefault("result")?.ToString() ?? string.Empty;
            debugInfo.ConvertedResult = outcome.Output.GetValueOrDefault("result");

            return debugInfo;
        }
    }
}

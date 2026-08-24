using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VOL.Builder.Services.CertPlatform.WorkflowEngine;
using VOL.Builder.Services.CertPlatform.WorkflowEngine.Models;
using VOL.Core.EFDbContext;
using VOL.Entity.CertPlatform.Cert;
using YZH.Core.AI.Clients;
using YZH.Core.AI.Prompt;
using YZH.Core.Workflow;

namespace VOL.WebApi.Controllers.CertPlatform
{
    /// <summary>
    /// 工作流执行引擎 — 测试/执行 API
    /// <para>文档：工作流执行引擎-数据模型与接口设计-V3 §十一</para>
    /// <para>路由：api/workflow</para>
    /// <para>功能分组：</para>
    /// <para>1. 执行：POST test/run | POST run</para>
    /// <para>2. 配置接口：POST config/{ruleCode} | GET config/{ruleCode}</para>
    /// </summary>
    [Route("api/workflow")]
    [Authorize]
    public class WorkflowTestController : ControllerBase
    {
        private readonly WfExecutionTaskService _taskService;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<WorkflowTestController> _logger;

        public WorkflowTestController(
            WfExecutionTaskService taskService,
            IServiceProvider serviceProvider,
            ILogger<WorkflowTestController> logger)
        {
            _taskService = taskService;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        // ════════════════════════════════════════
        // 执行相关
        // ════════════════════════════════════════

        /// <summary>
        /// 配置验证 — 前端传 rule_json，同步执行，返回完整结果
        /// <para>用途：NC 配置页面点击「验证」按钮</para>
        /// </summary>
        [HttpPost("test/run")]
        public async Task<IActionResult> TestRun([FromBody] TaskExecutionRequest request, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(request?.ConfigJson))
                return BadRequest(new { success = false, error = "configJson 不能为空" });

            request.TaskType = "TEST";

            try
            {
                var response = await _taskService.CreateAndRunAsync(request, ct);

                // 即使 status=failed 也返回 200，让前端能拿到错误信息
                return Ok(new { success = true, data = response });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TEST_RUN_FAIL] ruleCode={RuleCode}", request.RuleCode);
                return BadRequest(new { success = false, error = $"工作流执行失败: {ex.Message}" });
            }
        }

        /// <summary>
        /// 正式执行 — 从数据库读取 rule_json，异步入队
        /// <para>用途：审核员点击「开始审核」</para>
        /// <para>当前版本：同步执行（TODO 后续改为入队 + SignalR 推送）</para>
        /// </summary>
        [HttpPost("run")]
        public async Task<IActionResult> Run([FromBody] TaskExecutionRequest request, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(request?.RuleCode))
                return BadRequest(new { success = false, error = "ruleCode 不能为空" });

            request.TaskType = "NC_CHECK";

            try
            {
                var response = await _taskService.CreateAndRunAsync(request, ct);
                return Ok(new { success = true, data = response });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[RUN_FAIL] ruleCode={RuleCode}", request.RuleCode);
                return BadRequest(new { success = false, error = $"工作流执行失败: {ex.Message}" });
            }
        }

        /// <summary>
        /// 单节点测试 — 执行单个节点并返回结果
        /// <para>用途：NC 配置页面点击「测试节点」按钮</para>
        /// <para>说明：连线输入在测试时按常量处理（无需上游节点）</para>
        /// </summary>
        [HttpPost("test/node")]
        public async Task<IActionResult> TestNode([FromBody] NodeTestRequest request, CancellationToken ct)
        {
            if (request == null)
                return BadRequest(new { success = false, error = "请求体不能为空" });

            // 构建 WorkflowNodeConfig
            var nodeConfig = new WorkflowNodeConfig
            {
                NodeId = request.NodeId ?? "test_node",
                NodeType = request.NodeType ?? "skill",
                Title = request.Title ?? "测试节点",
                SkillCode = request.SkillCode,
                Config = request.Config ?? new Dictionary<string, object>(),
                Inputs = request.Inputs ?? new Dictionary<string, string>(),
                InputTypes = request.InputTypes ?? new Dictionary<string, string>(),
                InputPorts = request.InputPorts ?? new List<PortConfig>(),
                OutputPorts = request.OutputPorts ?? new List<PortConfig>()
            };

            try
            {
                // 创建 NodeExecutor（使用 scope 内的服务）
                using var scope = _serviceProvider.CreateScope();
                var skillRegistry = scope.ServiceProvider.GetRequiredService<ISkillRegistry>();
                var llmClient = scope.ServiceProvider.GetRequiredService<ILlmClient>();
                var promptInterpreter = scope.ServiceProvider.GetRequiredService<IPromptInterpreter>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<NodeExecutor>>();
                var aiNodeLogger = scope.ServiceProvider.GetRequiredService<ILogger<AiNodeExecutor>>();
                var nodeExecutor = new NodeExecutor(skillRegistry, llmClient, promptInterpreter, logger, aiNodeLogger);

                // 执行节点（空上下文，连线输入会回退到原始值）
                var result = await nodeExecutor.ExecuteAsync(
                    nodeConfig,
                    "TEST",
                    "SINGLE_NODE",
                    new Dictionary<string, object>(), // sharedOutputs（空）
                    new Dictionary<string, object>(), // contextParams（空）
                    ct);

                _logger.LogInformation(
                    "[TEST_NODE] nodeType={NodeType}, skillCode={SkillCode}, success={Success}, output={Output}",
                    nodeConfig.NodeType, nodeConfig.SkillCode, result.Success,
                    JsonSerializer.Serialize(result.Output));

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        success = result.Success,
                        error = result.Error,
                        output = result.Output,
                        durationMs = result.DurationMs
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TEST_NODE_FAIL] nodeType={NodeType}, skillCode={SkillCode}", nodeConfig.NodeType, nodeConfig.SkillCode);
                return BadRequest(new { success = false, error = $"节点执行失败: {ex.Message}" });
            }
        }

        /// <summary>
        /// AI 节点测试 — 执行单个 AI 节点并返回结果
        /// <para>用途：NC 配置页面点击 AI 节点「测试」按钮</para>
        /// <para>特殊说明：AI 节点需要完整工作流上下文（上游节点输出），通过 mockOutputs 传入</para>
        /// <para>文档：AI节点-详细设计-V1 §7</para>
        /// </summary>
        [HttpPost("test/ai-node")]
        public async Task<IActionResult> TestAiNode([FromBody] AiNodeTestRequest request, CancellationToken ct)
        {
            if (request == null)
                return BadRequest(new { success = false, error = "请求体不能为空" });

            // 构建 WorkflowNodeConfig
            var nodeConfig = new WorkflowNodeConfig
            {
                NodeId = request.NodeId ?? "test_ai_node",
                NodeType = "ai_node",
                Title = request.Title ?? "AI 测试节点",
                Config = request.Config ?? new Dictionary<string, object>(),
                Inputs = request.Inputs ?? new Dictionary<string, string>(),
                InputTypes = request.InputTypes ?? new Dictionary<string, string>(),
                InputPorts = request.InputPorts ?? new List<PortConfig>(),
                OutputPorts = request.OutputPorts ?? new List<PortConfig>()
            };

            try
            {
                // 创建 scope 内的服务
                using var scope = _serviceProvider.CreateScope();
                var llmClient = scope.ServiceProvider.GetRequiredService<ILlmClient>();
                var skillRegistry = scope.ServiceProvider.GetRequiredService<ISkillRegistry>();
                var promptInterpreter = scope.ServiceProvider.GetRequiredService<IPromptInterpreter>();
                var aiLogger = scope.ServiceProvider.GetRequiredService<ILogger<AiNodeExecutor>>();
                var nodeLogger = scope.ServiceProvider.GetRequiredService<ILogger<NodeExecutor>>();

                var aiExecutor = new AiNodeExecutor(llmClient, skillRegistry, promptInterpreter, aiLogger);
                var nodeExecutor = new NodeExecutor(skillRegistry, llmClient, promptInterpreter, nodeLogger, aiLogger);

                // 准备 mockOutputs + contextParams
                var mockOutputs = request.WorkflowContext?.MockOutputs
                    ?? new Dictionary<string, Dictionary<string, object>>();
                var contextParams = request.WorkflowContext?.ContextParams
                    ?? new Dictionary<string, object>();

                // 构建 flattenedOutputs（同时以 nodeId 和 title 作为 key）
                var flattenedOutputs = new Dictionary<string, object>();

                // 1. 先使用前端传入的 mockOutputs
                foreach (var (nodeKey, nodeOutput) in mockOutputs)
                {
                    object flatValue;
                    if (nodeOutput.TryGetValue("result", out var res))
                        flatValue = res;
                    else
                        flatValue = nodeOutput;

                    flattenedOutputs[nodeKey] = flatValue;
                }

                // 2. 如果 mockOutputs 为空且有 ruleJson，自动解析并执行上游节点
                if (mockOutputs.Count == 0 && !string.IsNullOrEmpty(request.WorkflowContext?.RuleJson))
                {
                    _logger.LogInformation("[TEST_AI_NODE] mockOutputs 为空，尝试解析 ruleJson 并执行上游节点");

                    try
                    {
                        // 直接反序列化 ruleJson（跳过拓扑校验，因为测试时工作流可能不完整）
                        var wfConfig = JsonSerializer.Deserialize<WorkflowConfig>(
                            request.WorkflowContext.RuleJson,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                        if (wfConfig?.Nodes != null && wfConfig.Nodes.Count > 0)
                        {
                            var nodeMap = wfConfig.Nodes.ToDictionary(n => n.NodeId, n => n);

                            // 构建入边表
                            var inEdges = new Dictionary<string, List<WorkflowEdgeConfig>>();
                            foreach (var node in wfConfig.Nodes)
                                inEdges[node.NodeId] = new List<WorkflowEdgeConfig>();

                            if (wfConfig.Edges != null)
                            {
                                foreach (var edge in wfConfig.Edges)
                                {
                                    if (inEdges.TryGetValue(edge.Target, out var list))
                                        list.Add(edge);
                                }
                            }

                            // 从 AI 节点开始反向 BFS 找到所有上游节点
                            var upstreamNodeIds = FindUpstreamNodes(inEdges, nodeConfig.NodeId);

                            _logger.LogInformation("[TEST_AI_NODE] 上游节点: {NodeIds}",
                                string.Join(", ", upstreamNodeIds));

                            // 按拓扑顺序执行上游节点
                            var sharedOutputs = new Dictionary<string, object>();

                            // 先注入 contextParams（模拟 start 节点）
                            foreach (var (k, v) in contextParams)
                                sharedOutputs[k] = v;

                            foreach (var upNodeId in upstreamNodeIds)
                            {
                                if (!nodeMap.TryGetValue(upNodeId, out var upNode))
                                    continue;

                                var nt = upNode.NodeType?.ToLowerInvariant();

                                // start 节点：注入 contextParams
                                if (nt == "start")
                                {
                                    sharedOutputs[upNode.NodeId] = contextParams;
                                    if (!string.IsNullOrEmpty(upNode.Title))
                                        sharedOutputs[upNode.Title] = contextParams;
                                    continue;
                                }

                                // 跳过 end/branch/ai_node（不执行）
                                if (nt == "end" || nt == "branch" || nt == "ai_node")
                                    continue;

                                // 根据入边填充 inputs（确保上游数据可用）
                                if (inEdges.TryGetValue(upNodeId, out var upInEdges) && upInEdges.Count > 0)
                                {
                                    upNode.Inputs ??= new Dictionary<string, string>();
                                    upNode.InputTypes ??= new Dictionary<string, string>();
                                    foreach (var e in upInEdges)
                                    {
                                        var portName = !string.IsNullOrEmpty(e.TargetHandle)
                                            ? e.TargetHandle
                                            : (upNode.InputPorts?.FirstOrDefault()?.Name ?? "result");
                                        if (!upNode.Inputs.ContainsKey(portName))
                                        {
                                            upNode.Inputs[portName] = e.Source;
                                            if (!upNode.InputTypes.ContainsKey(portName))
                                                upNode.InputTypes[portName] = "link";
                                        }
                                    }
                                }

                                // 执行节点
                                _logger.LogInformation("[TEST_AI_NODE] 执行上游节点: {NodeId} ({Title}), type={Type}",
                                    upNode.NodeId, upNode.Title, upNode.NodeType);

                                var upResult = await nodeExecutor.ExecuteAsync(
                                    upNode, "TEST", "AI_NODE_TEST",
                                    sharedOutputs, contextParams, ct);

                                if (upResult.Success)
                                {
                                    sharedOutputs[upNode.NodeId] = upResult.Output;
                                    if (!string.IsNullOrEmpty(upNode.Title))
                                        sharedOutputs[upNode.Title] = upResult.Output;

                                    _logger.LogInformation("[TEST_AI_NODE] 上游节点 {NodeId} 执行成功: {Output}",
                                        upNode.NodeId, JsonSerializer.Serialize(upResult.Output));
                                }
                                else
                                {
                                    _logger.LogWarning("[TEST_AI_NODE] 上游节点 {NodeId} 执行失败: {Error}",
                                        upNode.NodeId, upResult.Error);
                                }
                            }

                            // 合并到 flattenedOutputs
                            foreach (var (k, v) in sharedOutputs)
                                flattenedOutputs[k] = v;
                        }
                    }
                    catch (Exception parseEx)
                    {
                        _logger.LogWarning(parseEx, "[TEST_AI_NODE] 解析 ruleJson 或执行上游节点失败，回退到空上下文");
                    }
                }

                var result = await aiExecutor.ExecuteWithMockAsync(
                    nodeConfig,
                    flattenedOutputs,
                    contextParams,
                    precomputedParams: null,
                    ct);

                _logger.LogInformation(
                    "[TEST_AI_NODE] nodeId={NodeId}, success={Success}, output={Output}",
                    nodeConfig.NodeId, result.Success,
                    result.Success ? JsonSerializer.Serialize(result.Output) : result.Error);

                // 构建 debug 信息
                var outputType = request.Config?.GetValueOrDefault("outputType")?.ToString() ?? "string";
                var debugInfo = new AiNodeDebugInfo();

                if (result.Success && result.Output != null)
                {
                    // 构建 debug 信息：从模板中提取 {{xxx.yyy}} 引用，展示解析后的 paramPool
                    var template = request.Config?.GetValueOrDefault("promptTemplate")?.ToString() ?? "";
                    debugInfo.ParamPool = new Dictionary<string, object>();

                    // 1. 兼容旧 customParams
                    if (nodeConfig.Config.TryGetValue("customParams", out var cpValue))
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
                                        if (nodeId != null && mockOutputs.TryGetValue(nodeId, out var mo))
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
                        var refRegex = new System.Text.RegularExpressions.Regex(
                            @"\{\{([\w\u4e00-\u9fff][\w\u4e00-\u9fff.]*)\}\}");
                        var matches = refRegex.Matches(template);
                        foreach (System.Text.RegularExpressions.Match m in matches)
                        {
                            var fullKey = m.Groups[1].Value;
                            if (debugInfo.ParamPool.ContainsKey(fullKey))
                                continue;

                            var lastDot = fullKey.LastIndexOf('.');
                            string refKey = lastDot >= 0 ? fullKey[..lastDot] : fullKey;
                            string portName = lastDot >= 0 ? fullKey[(lastDot + 1)..] : "";

                            if (flattenedOutputs.TryGetValue(refKey, out var nodeOutput))
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

                    // 计算 renderedPrompt
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

                    debugInfo.LlmResponse = result.Output.GetValueOrDefault("result")?.ToString() ?? string.Empty;
                    debugInfo.ConvertedResult = result.Output.GetValueOrDefault("result");
                }

                return Ok(new
                {
                    success = true,
                    data = new AiNodeTestResponse
                    {
                        Success = result.Success,
                        Error = result.Error,
                        Result = result.Success ? result.Output.GetValueOrDefault("result") : null,
                        DurationMs = result.DurationMs,
                        Debug = debugInfo
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TEST_AI_NODE_FAIL] nodeId={NodeId}", nodeConfig.NodeId);
                return BadRequest(new { success = false, error = $"AI 节点执行失败: {ex.Message}" });
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
                engine = "WorkflowEngine-V3",
                modules = new[]
                {
                    "WorkflowConfigParser",
                    "NodeExecutor",
                    "WorkflowInterpreter",
                    "TaskCacheService",
                    "WorkflowLogger",
                    "WfExecutionTaskService"
                }
            });
        }

        // ════════════════════════════════════════
        // 辅助方法
        // ════════════════════════════════════════

        /// <summary>
        /// 从指定节点开始反向 BFS，找到所有上游节点（按拓扑排序）
        /// <para>用于 AI 节点测试时自动执行上游节点</para>
        /// </summary>
        private static List<string> FindUpstreamNodes(
            Dictionary<string, List<WorkflowEdgeConfig>> inEdges,
            string targetNodeId)
        {
            var result = new List<string>();
            var visited = new HashSet<string>();
            var queue = new Queue<string>();

            queue.Enqueue(targetNodeId);
            visited.Add(targetNodeId);

            while (queue.Count > 0)
            {
                var currentId = queue.Dequeue();

                if (!inEdges.TryGetValue(currentId, out var edges))
                    continue;

                foreach (var edge in edges)
                {
                    if (visited.Contains(edge.Source))
                        continue;

                    visited.Add(edge.Source);
                    result.Add(edge.Source);
                    queue.Enqueue(edge.Source);
                }
            }

            result.Reverse();
            return result;
        }

        // ════════════════════════════════════════
        // 配置接口（直接操作 cert_validation_rule 的 rule_json / layout_json）
        // ════════════════════════════════════════

        /// <summary>
        /// 设置指定检查项的工作流配置（覆盖 rule_json + layout_json）
        /// <para>用途：通过接口直接设置固定配置信息，供后端测试</para>
        /// <para>操作对象：cert_validation_rule 表</para>
        /// </summary>
        [HttpPost("config/{ruleCode}")]
        public async Task<IActionResult> SetConfig(string ruleCode, [FromBody] SetConfigRequest request)
        {
            if (string.IsNullOrWhiteSpace(ruleCode))
                return Ok(new { status = false, message = "ruleCode 不能为空" });
            if (request == null || string.IsNullOrWhiteSpace(request.ConfigJson))
                return Ok(new { status = false, message = "configJson 不能为空" });

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<VOLContext>();

                var entity = await db.Set<ValidationRule>()
                    .FirstOrDefaultAsync(x => x.RuleCode == ruleCode);

                if (entity == null)
                    return Ok(new { status = false, message = $"检查项 '{ruleCode}' 不存在" });

                // 覆盖配置字段
                entity.RuleJson = request.ConfigJson;
                entity.LayoutJson = request.LayoutJson ?? entity.LayoutJson;
                db.Set<ValidationRule>().Update(entity);
                await db.SaveChangesAsync();

                return Ok(new { status = true, message = "配置已更新", data = new { ruleCode, entity.RuleName } });
            }
            catch (Exception ex)
            {
                return Ok(new { status = false, message = $"保存失败: {ex.Message}" });
            }
        }

        /// <summary>
        /// 获取指定检查项的工作流配置（rule_json + layout_json）
        /// <para>用途：后端引擎引用 / 前端加载配置</para>
        /// </summary>
        [HttpGet("config/{ruleCode}")]
        public async Task<IActionResult> GetConfig(string ruleCode)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<VOLContext>();

                var entity = await db.Set<ValidationRule>()
                    .FirstOrDefaultAsync(x => x.RuleCode == ruleCode);

                if (entity == null)
                    return Ok(new { status = false, message = $"检查项 '{ruleCode}' 不存在" });

                return Ok(new
                {
                    status = true,
                    data = new
                    {
                        entity.Id,
                        entity.RuleCode,
                        entity.RuleName,
                        entity.RuleJson,
                        entity.LayoutJson
                    }
                });
            }
            catch (Exception ex)
            {
                return Ok(new { status = false, message = $"查询失败: {ex.Message}" });
            }
        }
    }

    /// <summary>
    /// 设置配置请求（直接覆盖 rule_json + layout_json）
    /// </summary>
    public class SetConfigRequest
    {
        /// <summary>完整配置 JSON（serialize 输出：nodes/edges/outputConfig/glossary）</summary>
        public string ConfigJson { get; set; }

        /// <summary>画布布局 JSON（extractLayout 输出：nodePositions）</summary>
        public string LayoutJson { get; set; }
    }

    /// <summary>
    /// 单节点测试请求
    /// </summary>
    public class NodeTestRequest
    {
        /// <summary>节点 ID</summary>
        public string NodeId { get; set; }

        /// <summary>节点类型：skill / ai_node / docField / docTable / branch</summary>
        public string NodeType { get; set; }

        /// <summary>节点标题</summary>
        public string Title { get; set; }

        /// <summary>Skill 编码（功能节点必填）</summary>
        public string SkillCode { get; set; }

        /// <summary>节点配置</summary>
        public Dictionary<string, object> Config { get; set; }

        /// <summary>输入参数（portName → value）</summary>
        public Dictionary<string, string> Inputs { get; set; }

        /// <summary>输入类型（portName → "link" | "constant"）</summary>
        public Dictionary<string, string> InputTypes { get; set; }

        /// <summary>输入端口声明</summary>
        public List<PortConfig> InputPorts { get; set; }

        /// <summary>输出端口声明</summary>
        public List<PortConfig> OutputPorts { get; set; }
    }
}

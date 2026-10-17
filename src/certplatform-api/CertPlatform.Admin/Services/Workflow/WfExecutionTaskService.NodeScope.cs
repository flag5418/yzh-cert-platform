using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using CertPlatform.Shared.Workflow.Models;
using CertPlatform.Shared.Domain.Scope;
using CertPlatform.Shared.Exceptions;

namespace CertPlatform.Admin.Services.Workflow
{
    /// <summary>
    /// 单节点 / AI 节点测试结果（服务层内部返回，供控制器构造 API 响应）
    /// <para>不是对外契约 —— 对外契约仍是 <see cref="NodeTestResponse"/> / <see cref="AiNodeTestResponse"/>，
    /// 保持前端零破坏。</para>
    /// </summary>
    public class NodeScopeRunOutcome
    {
        /// <summary>任务编码（已落库，可用于查 wf_node_execution）</summary>
        public string TaskCode { get; set; } = "";

        /// <summary>执行项编码</summary>
        public string ItemCode { get; set; } = "";

        /// <summary>目标节点是否执行成功</summary>
        public bool Success { get; set; }

        /// <summary>失败原因</summary>
        public string? Error { get; set; }

        /// <summary>执行耗时(ms)</summary>
        public int DurationMs { get; set; }

        /// <summary>目标节点输出</summary>
        public Dictionary<string, object> Output { get; set; } = new();

        /// <summary>
        /// ★ 2026-10-15 新增：声明为连线、但本次取不到上游值的端口名
        /// （= <c>NodeExecutionResult.UnresolvedInputs</c>）。
        /// <para>配置期单节点测试刻意不执行上游 ⇒ 连线端口本来就没值；必须显式回传前端，
        /// ⛔ 不能只写日志（否则用户只会看到下游报「verdict 非法」，把测试的能力边界
        /// 误当成节点配置错误）。</para>
        /// </summary>
        public List<string> UnresolvedInputs { get; set; } = new();

        /// <summary>AI 节点测试专用：打平后的输出池（nodeId / title → 输出），供控制器构造 Debug 信息</summary>
        public Dictionary<string, object> FlattenedOutputs { get; set; } = new();

        /// <summary>AI 节点测试专用：前端传入的原始 mockOutputs（nodeId → 输出）</summary>
        public Dictionary<string, Dictionary<string, object>> MockOutputs { get; set; } = new();

        // ──── ★ 2026-10-14 新增：引擎真实产物（测试页 Debug 唯一数据源）────
        //
        // 为什么放在这里而不是让控制器自己算：控制器的旧实现用**第二套渲染逻辑**重算提示词，
        // 且被 `if (ParamPool.Count > 0)` 包住 ⇒ 参数池为空时 RenderedPrompt 恒为 ""
        // ⇒ 用户在测试页看到「提示词是空的」，排查方向被带偏（缺陷 D2）。
        // 现在：引擎怎么渲的，就怎么显示，**同一逻辑只允许一处实现**。

        /// <summary>★ 引擎真实渲染后送 LLM 的提示词（= <c>NodeExecutionResult.AiPrompt</c>）</summary>
        public string AiPrompt { get; set; } = "";

        /// <summary>★ 引擎渲染时**实际使用**的参数池（参数名 → 值）</summary>
        public Dictionary<string, object> AiParamPool { get; set; } = new();

        /// <summary>★ 渲染后仍未解析的模板引用（空 = 全部解析成功）</summary>
        public List<string> AiUnresolvedRefs { get; set; } = new();
    }

    /// <summary>
    /// WfExecutionTaskService — 单节点 / AI 节点测试（TestScope = NODE / AI_NODE）
    ///
    /// <para><b>2026-09-22 阶段二：测试入口统一</b></para>
    /// <para>原实现（<c>WorkflowTestController.TestNode</c> / <c>TestAiNode</c>）直接调
    /// <c>NodeExecutor</c> / <c>AiNodeExecutor</c>，只打 ILogger，<b>完全不落库</b>（缺口 G6）——
    /// 于是「点一下节点测试」这件事在系统里不留任何痕迹：没有 wf_execution_task、
    /// 没有 wf_node_execution、没有可机器解析的执行日志，也就无法回溯「上次测的是什么、结果如何」。</para>
    ///
    /// <para>现改为与整流测试共用同一条管线：</para>
    /// <list type="number">
    ///   <item>建 <c>wf_execution_task</c>（TestScope=NODE / AI_NODE）+ <c>wf_execution_task_item</c></item>
    ///   <item>预热任务级缓存</item>
    ///   <item>执行节点（真实 taskCode/itemCode 进入日志，可与 DB 行对齐）</item>
    ///   <item>复用 <see cref="SaveNodeExecutionsAsync"/> 落 <c>wf_node_execution</c></item>
    ///   <item>复用 <see cref="FinishRunAsync"/> 收口 + 清缓存</item>
    /// </list>
    ///
    /// <para><b>伪路径设计</b>：单节点测试的 PathResults 只有一条路径 ——</para>
    /// <list type="bullet">
    ///   <item>TestScope=NODE：<c>[目标节点]</c></item>
    ///   <item>TestScope=AI_NODE：<c>[上游节点…, 目标 AI 节点]</c>（上游由 ruleJson 反向 BFS 得到）</item>
    /// </list>
    /// <para>路径虽短，但 <see cref="PathResult.NodeResults"/> / 落库行 / 日志 Tag 与整流测试<b>完全同构</b>，
    /// 因此前端 <c>ExecutionResultPanel</c> 无需为「节点测试」写分支判断。</para>
    ///
    /// <para><b>不走 WorkflowConfigParser.Parse</b>：单节点测试前端只传目标节点，
    /// AI 节点测试传的 ruleJson 也可能拓扑不完整，走解析器会被拓扑校验挡掉。
    /// 这里用 <see cref="BuildSyntheticWorkflow"/> 直接合成 NodeMap（落库与缓存预热只依赖 NodeMap）。</para>
    /// </summary>
    public partial class WfExecutionTaskService
    {
        // ════════════════════════════════════════════════════════════
        // 入口 1：任意单节点测试（TestScope=NODE）
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// 执行单个节点并落库（前端 NC/报告配置页「测试节点」按钮）
        /// <para>连线输入在测试时按常量处理（无需上游节点），故 sharedOutputs 传空。</para>
        /// </summary>
        public async Task<NodeScopeRunOutcome> RunSingleNodeAsync(
            NodeTestRequest request,
            CancellationToken ct = default)
        {
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

            var ruleCode = request.RuleCode ?? "";
            var enterpriseCode = ResolveEnterpriseCode(request.EnterpriseCode);
            var phaseCode = request.PhaseCode ?? "";
            var standardCode = request.StandardCode ?? "";

            var taskCode = Guid.NewGuid().ToString("N");
            var itemCode = Guid.NewGuid().ToString("N");

            _wfLogger.TaskCreate(taskCode, "TEST", ruleCode, enterpriseCode);

            var parsed = BuildSyntheticWorkflow(new[] { nodeConfig });
            var cacheKeys = await BeginRunAsync(
                taskCode, itemCode, "TEST", "NODE",
                BuildSnapshotJson(new[] { nodeConfig }),
                ruleCode, enterpriseCode, phaseCode, parsed, ct);

            // ★ 机构编码：与整流测试同一推导链（规则 → 企业 → 登录人 → 空）
            var orgCode = await ResolveOrgCodeAsync(ruleCode, enterpriseCode, ct);
            var contextParams = BuildContextParams(enterpriseCode, phaseCode, standardCode, orgCode, ruleCode);

            _wfLogger.ItemStart(taskCode, itemCode, ruleCode);
            _wfLogger.PathEnum(taskCode, itemCode, 1);
            _wfLogger.PathStart(taskCode, itemCode, 0, 1);

            var sw = Stopwatch.StartNew();

            // NodeExecutor 内部 try/catch 全吞，恒返回 NodeExecutionResult（不抛异常）
            // ⚠️ 唯一的例外是 WorkflowDataMissingException —— 它**按设计穿透**（要把补录缺口
            //    交给 WorkflowInterpreter）。而单节点测试没有解释器兜底，没人接它 ⇒ 必须在本入口收口（见下方 catch）。
            NodeExecutionResult result;
            try
            {
                result = await _nodeExecutor.ExecuteAsync(
                    nodeConfig, taskCode, itemCode,
                    new Dictionary<string, object>(),   // sharedOutputs：单节点测试无上游
                    contextParams, ct);
            }
            catch (WorkflowDataMissingException ex)
            {
                // ★ 2026-10-16：**在本入口收口，⛔ 不让它穿透到控制器**。
                //
                //   穿透的实测后果（用户 2026-10-16 上报：测试「聚合多路judge」节点）：
                //   控制器 catch(Exception) ⇒ 响应体只剩一句
                //     「节点执行失败: Skill 'collect_judges' 执行失败[PRECONDITION_EMPTY]: …」
                //   ⇒ ① UnresolvedInputs 丢失（界面看不到「哪些端口因为没执行上游而无值」）
                //      ② 本次测试**完全不落库**（wf_node_execution 无行 ⇒ 「测试历史」查不到）
                //   而这两样正是 2026-10-15 刚补上的诊断面 —— 被异常路径整条绕过。
                //
                //   语义上这也不是「系统故障」：单节点测试刻意不执行上游 ⇒ 连线端口本来就没值。
                //   故如实归类（skill 入参为空 = PRECONDITION_EMPTY；其余 = DATA_MISSING），
                //   并走同一条落库路径，让「测试历史」能查到这次失败。
                result = NodeExecutionResult.Fail(ex.Message, (int)sw.ElapsedMilliseconds);
                result.ErrorCode = string.Equals(ex.DataKind, "skill_param", StringComparison.Ordinal)
                    ? WorkflowErrorCodes.PreconditionEmpty
                    : WorkflowErrorCodes.DataMissing;
                result.UnresolvedInputs = ex.UnresolvedInputs ?? new List<string>();
            }

            sw.Stop();

            // ★ 失败且存在未取到值的连线端口 ⇒ 把「测试的能力边界」写进错误里。
            //   仅靠 UnresolvedInputs 数组，前端只能笼统提示；点出端口名 + 下一步怎么做，才不用人猜。
            var unresolvedInputs = result.UnresolvedInputs ?? new List<string>();
            var error = result.Success ? result.Error : AppendUnresolvedUpstreamHint(result.Error, unresolvedInputs);

            var itemResult = await PersistNodeScopeAsync(
                taskCode, itemCode, parsed,
                new List<(WorkflowNodeConfig, NodeExecutionResult)> { (nodeConfig, result) },
                sw.ElapsedMilliseconds, cacheKeys, ct);

            return new NodeScopeRunOutcome
            {
                TaskCode = taskCode,
                ItemCode = itemCode,
                Success = result.Success,
                Error = error,
                DurationMs = result.DurationMs,
                Output = result.Output,
                // ★ 连线端口本次取不到值 ⇒ 显式带给控制器（前端据此提示「未执行上游」）
                UnresolvedInputs = unresolvedInputs,
                FlattenedOutputs = new Dictionary<string, object>(),
                MockOutputs = new Dictionary<string, Dictionary<string, object>>()
            };
        }

        /// <summary>
        /// 失败原因后追加「本次测试未执行上游」的说明（★ 2026-10-16）。
        ///
        /// <para><b>解的是什么</b>：用户 2026-10-16 在 NC 配置页测「聚合多路judge」节点，
        /// 只看到一句 <c>Skill 'collect_judges' 执行失败[PRECONDITION_EMPTY]: …未收到任何 judge 结果</c>
        /// ⇒ 看起来像「节点配错了」，实际是<b>单节点测试不执行上游</b>这条**能力边界**。
        /// 指令本身没变（仍然失败），变的是「用户能不能看懂」。</para>
        ///
        /// <para>⛔ 端口清单为空时原样返回 —— ⛔ 不得凭空断言「是上游的问题」。</para>
        /// </summary>
        private static string? AppendUnresolvedUpstreamHint(string? error, List<string> unresolvedInputs)
        {
            if (unresolvedInputs == null || unresolvedInputs.Count == 0) return error;

            var hint = $"（本次单节点测试未执行上游：{string.Join("、", unresolvedInputs)} 端口没有值" +
                       "—— 这是单节点测试的能力边界，⛔ 不代表节点配置错；" +
                       "请点「运行测试」整图执行，或给这些端口改填常量。）";

            return string.IsNullOrWhiteSpace(error) ? hint : error + hint;
        }

        // ════════════════════════════════════════════════════════════
        // 入口 2：AI 节点测试（TestScope=AI_NODE）
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// 执行单个 AI 节点并落库（前端 AI 节点「测试」按钮）
        /// <para>AI 节点需要完整工作流上下文（上游节点输出）：</para>
        /// <list type="bullet">
        ///   <item>前端已传 <c>MockOutputs</c> → 直接用（不打平上游执行）</item>
        ///   <item>MockOutputs 为空且有 <c>RuleJson</c> → 从目标节点反向 BFS 出上游链，按拓扑顺序真实执行</item>
        /// </list>
        /// <para>落库路径 = <c>[实际执行过的上游节点…, 目标 AI 节点]</c>，
        /// 于是「AI 节点测试顺手跑了哪些上游节点」也一并留痕（原实现只在日志里，DB 无记录）。</para>
        /// </summary>
        public async Task<NodeScopeRunOutcome> RunAiNodeAsync(
            AiNodeTestRequest request,
            CancellationToken ct = default)
        {
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

            var ruleCode = request.RuleCode ?? "";
            var enterpriseCode = ResolveEnterpriseCode(
                request.EnterpriseCode ?? ReadContextParam(request, WorkflowContextKeys.EnterpriseCode));
            var phaseCode = request.PhaseCode ?? ReadContextParam(request, WorkflowContextKeys.PhaseCode) ?? "";
            var standardCode = request.StandardCode ?? ReadContextParam(request, WorkflowContextKeys.StandardCode) ?? "";

            var mockOutputs = request.WorkflowContext?.MockOutputs
                ?? new Dictionary<string, Dictionary<string, object>>();
            var contextParams = request.WorkflowContext?.ContextParams
                ?? new Dictionary<string, object>();

            // 1. 打平 mockOutputs（同时以 nodeId 与 title 作为 key，供 {{节点标题.端口}} 引用）
            var flattenedOutputs = new Dictionary<string, object>();
            foreach (var (nodeKey, nodeOutput) in mockOutputs)
            {
                flattenedOutputs[nodeKey] = nodeOutput.TryGetValue("result", out var res)
                    ? res
                    : nodeOutput;
            }

            // 2. 解析上游执行计划（纯计算，不碰 DB；要在 BeginRunAsync 之前完成，因为 NodeMap 决定落库行）
            var upstreamPlan = new List<WorkflowNodeConfig>();

            if (mockOutputs.Count == 0 && !string.IsNullOrEmpty(request.WorkflowContext?.RuleJson))
            {
                upstreamPlan = ResolveUpstreamPlan(request.WorkflowContext.RuleJson, nodeConfig.NodeId);
            }

            // 3. 合成 ParsedWorkflow：NodeMap 必须覆盖「所有会落库的节点」（上游 + 目标）
            var persistedNodes = new List<WorkflowNodeConfig>(upstreamPlan) { nodeConfig };
            var parsed = BuildSyntheticWorkflow(persistedNodes);

            var taskCode = Guid.NewGuid().ToString("N");
            var itemCode = Guid.NewGuid().ToString("N");

            _wfLogger.TaskCreate(taskCode, "TEST", ruleCode, enterpriseCode);

            var cacheKeys = await BeginRunAsync(
                taskCode, itemCode, "TEST", "AI_NODE",
                BuildSnapshotJson(persistedNodes),
                ruleCode, enterpriseCode, phaseCode, parsed, ct);

            _wfLogger.ItemStart(taskCode, itemCode, ruleCode);
            _wfLogger.PathEnum(taskCode, itemCode, 1);
            _wfLogger.PathStart(taskCode, itemCode, 0, persistedNodes.Count);

            var executed = new List<(WorkflowNodeConfig, NodeExecutionResult)>();

            // 4. 先注入 contextParams（模拟 start 节点），与整流测试语义一致
            foreach (var (k, v) in contextParams)
                flattenedOutputs[k] = v;

            var sw = Stopwatch.StartNew();

            // 5. 按拓扑顺序执行上游节点（start/end/branch/ai_node 不执行，与旧实现一致）
            foreach (var upNode in upstreamPlan)
            {
                ct.ThrowIfCancellationRequested();

                var upResult = await _nodeExecutor.ExecuteAsync(
                    upNode, taskCode, itemCode, flattenedOutputs, contextParams, ct);

                executed.Add((upNode, upResult));

                if (upResult.Success)
                {
                    flattenedOutputs[upNode.NodeId] = upResult.Output;
                    if (!string.IsNullOrEmpty(upNode.Title))
                        flattenedOutputs[upNode.Title] = upResult.Output;
                }
                else
                {
                    _logger.LogWarning(
                        "[WorkflowEngine] AI 节点测试：上游节点 {NodeId} 执行失败: {Error}",
                        upNode.NodeId, upResult.Error);
                }
            }

            // 6. 执行目标 AI 节点
            //    ⚠️ 这里直接调 AiNodeExecutor.ExecuteWithMockAsync（它内部不走 NodeExecutor，
            //       因此不会自动产生 NODE_* 日志，需在此手工补齐，否则 AI 节点测试的日志链有断口）
            _wfLogger.NodeStart(taskCode, itemCode, nodeConfig.NodeId, "ai_node", nodeConfig.Title);
            _wfLogger.NodeExec(taskCode, itemCode, nodeConfig.NodeId, "invoke_llm");

            var aiStartedAt = DateTime.Now;
            var aiSw = Stopwatch.StartNew();

            // ExecuteWithMockAsync 内部 try/catch 全吞，恒返回结果（不抛异常），但不会写 StartedAt/CompletedAt
            var aiResult = await _aiNodeExecutor.ExecuteWithMockAsync(
                nodeConfig, flattenedOutputs, contextParams, precomputedParams: null, ct);

            aiSw.Stop();
            aiResult.StartedAt = aiStartedAt;
            aiResult.CompletedAt = DateTime.Now;

            if (aiResult.Success)
            {
                _wfLogger.NodeOutput(taskCode, itemCode, nodeConfig.NodeId,
                    JsonSerializer.Serialize(aiResult.Output), (int)aiSw.ElapsedMilliseconds);
                _wfLogger.NodeDone(taskCode, itemCode, nodeConfig.NodeId, (int)aiSw.ElapsedMilliseconds);
            }
            else
            {
                _wfLogger.NodeFail(taskCode, itemCode, nodeConfig.NodeId,
                    aiResult.Error ?? "AI 节点执行失败", (int)aiSw.ElapsedMilliseconds);
            }

            executed.Add((nodeConfig, aiResult));
            sw.Stop();

            await PersistNodeScopeAsync(taskCode, itemCode, parsed, executed, sw.ElapsedMilliseconds, cacheKeys, ct);

            return new NodeScopeRunOutcome
            {
                TaskCode = taskCode,
                ItemCode = itemCode,
                Success = aiResult.Success,
                Error = aiResult.Error,
                DurationMs = aiResult.DurationMs,
                Output = aiResult.Output,
                FlattenedOutputs = flattenedOutputs,
                MockOutputs = mockOutputs,
                // ★ 2026-10-14：引擎真实产物原样带出 —— 测试页 Debug 只认这一份，⛔ 不重算。
                //   失败时 aiResult 同样带 AiPrompt / AiUnresolvedRefs（见 AiNodeExecutor.ExecuteWithMockAsync），
                //   因此「LLM 调用失败」的场景下测试页依然能看到「渲染后的提示词 + 还差哪些引用」。
                AiPrompt = aiResult.AiPrompt ?? "",
                AiParamPool = aiResult.AiParamPool ?? new Dictionary<string, object>(),
                AiUnresolvedRefs = aiResult.AiUnresolvedRefs ?? new List<string>()
            };
        }

        // ════════════════════════════════════════════════════════════
        // 内部辅助
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// 落库 + 收口（单节点 / AI 节点共用），返回聚合后的 ItemExecutionResult
        /// <para>把「节点执行序列」翻译成一条伪路径，从而复用整流测试的落库与日志路径。</para>
        /// </summary>
        private async Task<ItemExecutionResult> PersistNodeScopeAsync(
            string taskCode,
            string itemCode,
            ParsedWorkflow parsed,
            List<(WorkflowNodeConfig Node, NodeExecutionResult Result)> executed,
            long elapsedMs,
            List<string> cacheKeys,
            CancellationToken ct)
        {
            var pathStartedAt = executed.Count > 0 ? executed[0].Result.StartedAt : DateTime.Now;
            var pathCompletedAt = executed.Count > 0 ? executed[^1].Result.CompletedAt : DateTime.Now;
            var failed = executed.FirstOrDefault(e => !e.Result.Success);
            var allSuccess = executed.Count > 0 && executed.All(e => e.Result.Success);
            var targetOutput = executed.Count > 0 ? executed[^1].Result.Output : new Dictionary<string, object>();

            var pathResult = new PathResult
            {
                PathIndex = 0,
                Status = allSuccess ? "completed" : "failed",
                FailedAtNodeId = failed.Node?.NodeId,
                Error = failed.Result?.Error,
                Output = targetOutput,
                NodeIds = executed.Select(e => e.Node.NodeId).ToList(),
                NodeResults = executed
                    .Select(e => NodeExecutionRecord.From(e.Node.NodeId, e.Result, isReused: false))
                    .ToList(),
                DurationMs = (int)elapsedMs,
                StartedAt = pathStartedAt,
                CompletedAt = pathCompletedAt
            };

            // 与 WorkflowInterpreter.AggregateNcResult 保持同构（含 executedPaths/successPaths/failedPaths），
            // 使前端对两种测试的结果解析逻辑完全一致
            // ★ 2026-10-12：补 skippedPaths —— 单节点测试只有一条路径、且没有 branch 参与，
            //   恒为空数组；存在的意义是**响应形状与整图执行完全一致**（前端一套解析逻辑）。
            var ncResult = new Dictionary<string, object>
            {
                ["success"] = allSuccess,
                ["error"] = allSuccess ? null! : (failed.Result?.Error ?? "节点执行失败"),
                ["result"] = allSuccess
                    ? targetOutput.GetValueOrDefault("result") ?? targetOutput
                    : null!,
                ["executedPaths"] = new[] { 0 },
                ["successPaths"] = allSuccess ? new[] { 0 } : Array.Empty<int>(),
                ["failedPaths"] = allSuccess ? Array.Empty<int>() : new[] { 0 },
                ["skippedPaths"] = Array.Empty<int>()
            };

            var itemResult = new ItemExecutionResult
            {
                Success = allSuccess,
                IsSuccess = allSuccess,
                Error = ncResult["error"] as string,
                PathResults = new List<PathResult> { pathResult },
                DurationMs = (int)elapsedMs,
                NcResult = ncResult
            };

            _wfLogger.PathDone(taskCode, itemCode, 0, pathResult.Status);
            if (pathResult.Status == "failed")
                _wfLogger.PathFail(taskCode, itemCode, 0, pathResult.FailedAtNodeId, pathResult.Error);

            // 四层落库：路径（本表，PathIndex=0 的伪路径）→ 节点
            await SavePathExecutionsAsync(taskCode, itemCode, itemResult, ct);
            await SaveNodeExecutionsAsync(taskCode, itemCode, parsed, itemResult, ct);

            _wfLogger.ItemDone(taskCode, itemCode, allSuccess, (int)elapsedMs);

            await FinishRunAsync(taskCode, itemCode, itemResult, elapsedMs, cacheKeys, ct);

            return itemResult;
        }

        /// <summary>
        /// 从 ruleJson 反向 BFS 出目标节点的上游链，并按拓扑顺序返回「需要真实执行」的节点
        /// <para>start / end / branch / ai_node 不执行（与旧 <c>WorkflowTestController.TestAiNode</c> 行为一致）：
        /// start 由 contextParams 注入代替，end/branch 在单节点测试里无意义，
        /// ai_node 不递归执行（只测目标那一个）。</para>
        /// <para><b>★ 2026-10-14 参数即边（§2.2）</b>：入边 BFS 的结果还要与目标节点
        /// <c>config.params[].nodeId</c> <b>取并集</b> —— 数据源节点没有物理连线，
        /// BFS 走不到它；不并进来测试期就永远不执行（真实路径的同款逻辑见
        /// <c>WorkflowInterpreter.PreExecuteAiParamSourcesAsync</c>）。</para>
        /// <para>解析失败时返回空列表（退回空上下文），不阻断目标节点测试 —— 与旧实现一致。</para>
        /// </summary>
        private List<WorkflowNodeConfig> ResolveUpstreamPlan(string ruleJson, string targetNodeId)
        {
            var plan = new List<WorkflowNodeConfig>();

            WorkflowConfig? wfConfig;
            try
            {
                // 跳过拓扑校验：测试期工作流可能不完整（与旧实现一致）
                wfConfig = JsonSerializer.Deserialize<WorkflowConfig>(
                    ruleJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "[WorkflowEngine] AI 节点测试：ruleJson 解析失败，跳过上游执行（目标节点仍会执行）");
                return plan;
            }

            if (wfConfig?.Nodes == null || wfConfig.Nodes.Count == 0)
                return plan;

            // ★ 重复节点 ID 防御（2026-10-15）：坏 rule_json（同 nodeId 两个节点）会让
            //   ToDictionary 抛 ArgumentException「same key already added」，且此处**在** try/catch 之外
            //   ⇒ 违反本方法「解析失败不阻断目标节点测试」的契约。按首个节点去重 + 告警。
            var dupNodeIds = wfConfig.Nodes
                .GroupBy(n => n.NodeId ?? string.Empty, StringComparer.Ordinal)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            if (dupNodeIds.Count > 0)
            {
                _logger.LogWarning(
                    "[WorkflowEngine] AI 节点测试：rule_json 存在重复节点 ID（{Dup}），已按首个节点执行",
                    string.Join("、", dupNodeIds));
            }

            var allMap = wfConfig.Nodes
                .GroupBy(n => n.NodeId ?? string.Empty, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

            // 入边表
            var inEdges = allMap.Keys.ToDictionary(
                k => k, _ => new List<WorkflowEdgeConfig>(), StringComparer.Ordinal);
            foreach (var edge in wfConfig.Edges ?? new List<WorkflowEdgeConfig>())
            {
                if (inEdges.TryGetValue(edge.Target, out var list))
                    list.Add(edge);
            }

            // 反向 BFS：目标节点 → 所有上游（返回顺序为拓扑序）
            var upstreamIds = FindUpstreamNodes(inEdges, targetNodeId);

            // ★★★ 参数即边（设计 §2.2 / 用户裁决 ②）：`params[].nodeId` 与「反向 BFS 出边」**取并集**。
            //
            //   数据源节点（docField / docTable）按设计**不需要手工连线** ⇒ 反向 BFS 的入边表里
            //   根本没有它 ⇒ 不并进来的话，测试期它永远不会被执行，AI 拿到的永远是「上游不存在」
            //   （前端已改为 MockOutputs 恒空，就是为了把这条路走通 —— 见 WorkflowDesigner §1.3）。
            //
            //   ⚠️ 并集只影响「**执行顺序 / 需要跑谁**」，⛔ 不改 ruleJson 的 edges、不改路径枚举。
            if (allMap.TryGetValue(targetNodeId, out var targetNode))
            {
                // 双键索引：refKey（节点 ID ∪ 节点标题）→ 节点 ID（老写法 {{中文标题.端口}} 要能反查）
                var refIndex = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var kv in allMap)
                {
                    refIndex[kv.Key] = kv.Key;
                    if (!string.IsNullOrEmpty(kv.Value.Title))
                        refIndex.TryAdd(kv.Value.Title!, kv.Key);
                }

                var paramIds = AiParams.CollectSourceNodeIds(
                    targetNode, refKey => refIndex.TryGetValue(refKey, out var id) ? id : null);
                if (paramIds.Count > 0)
                {
                    var ordered = new List<string>(upstreamIds);
                    var seen = new HashSet<string>(upstreamIds, StringComparer.Ordinal);

                    // ① 先补「参数来源自己的上游」（它同样可能没被 BFS 到）—— 在前 ⇒ 拓扑序成立
                    foreach (var pid in paramIds)
                    {
                        foreach (var upOfParam in FindUpstreamNodes(inEdges, pid))
                        {
                            if (upOfParam != targetNodeId && seen.Add(upOfParam))
                                ordered.Add(upOfParam);
                        }
                    }

                    // ② 再追加参数来源本身（已在上游里的不重复加）
                    foreach (var pid in paramIds)
                    {
                        if (pid != targetNodeId && seen.Add(pid))
                            ordered.Add(pid);
                    }

                    upstreamIds = ordered;
                }
            }

            foreach (var upId in upstreamIds)
            {
                if (!allMap.TryGetValue(upId, out var upNode))
                    continue;

                var nt = upNode.NodeType?.ToLowerInvariant();
                if (nt is "start" or "end" or "branch" or "ai_node")
                    continue;

                // 按入边补齐 inputs（确保上游数据可用），与旧实现一致
                if (inEdges.TryGetValue(upId, out var upInEdges) && upInEdges.Count > 0)
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

                plan.Add(upNode);
            }

            return plan;
        }

        /// <summary>
        /// 从指定节点开始反向 BFS，找到所有上游节点（按拓扑排序）
        /// <para>迁移自旧 <c>WorkflowTestController.FindUpstreamNodes</c>，逻辑零改动。</para>
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

        /// <summary>
        /// 构造节点级测试的上下文参数（与整流测试的键名逐字一致：camelCase）。
        /// <para>★ 2026-10-15：键名统一走 <see cref="WorkflowContextKeys"/>；新增
        /// <c>orgCode</c> / <c>ruleCode</c> —— 机构编码自动装配，⛔ 不再手填。</para>
        /// </summary>
        private Dictionary<string, object> BuildContextParams(
            string enterpriseCode, string phaseCode, string standardCode,
            string orgCode, string ruleCode)
            => new()
            {
                [WorkflowContextKeys.EnterpriseCode] = enterpriseCode,
                [WorkflowContextKeys.PhaseCode] = phaseCode,
                [WorkflowContextKeys.StandardCode] = standardCode,
                [WorkflowContextKeys.OrgCode] = orgCode,
                [WorkflowContextKeys.RuleCode] = ruleCode
            };

        /// <summary>企业编码缺省值：单节点测试默认打标准企业（与 NodeExecutor 内部兜底一致）</summary>
        private static string ResolveEnterpriseCode(string? enterpriseCode)
            => string.IsNullOrWhiteSpace(enterpriseCode) ? "" : enterpriseCode!;

        /// <summary>从 AI 节点的 WorkflowContext.ContextParams 读一个键（键名一律 PascalCase，见 WorkflowContextKeys）</summary>
        private static string? ReadContextParam(AiNodeTestRequest request, string key)
        {
            var ctx = request.WorkflowContext?.ContextParams;
            if (ctx == null) return null;

            if (ctx.TryGetValue(key, out var v) && v != null)
                return v.ToString();
            return null;
        }

        /// <summary>把节点列表序列化为 wf_execution_task.ConfigSnapshot（快照，便于事后还原「测的是什么」）</summary>
        private static string BuildSnapshotJson(IEnumerable<WorkflowNodeConfig> nodes)
        {
            var config = new WorkflowConfig
            {
                Version = 1,
                WorkflowType = "validation",
                Nodes = nodes.ToList(),
                Edges = new List<WorkflowEdgeConfig>()
            };
            return JsonSerializer.Serialize(config);
        }
    }
}

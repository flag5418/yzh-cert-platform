using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using CertPlatform.Shared.Workflow.Models;
using CertPlatform.Shared.Exceptions;

namespace CertPlatform.Admin.Services.Workflow
{
    /// <summary>
    /// 工作流解释器 — 引擎核心，负责路径驱动、节点串行阻塞执行、跨路径复用、NC 结果聚合
    /// <para>移植自：旧 WorkflowInterpreter.cs（317 行，逻辑零改动，仅 namespace 调整）</para>
    /// <para>职责：</para>
    /// <para>1. 接收 ParsedWorkflow（已解析的拓扑+路径），驱动执行</para>
    /// <para>2. 路径内节点串行阻塞：一个节点完成后再执行下一个</para>
    /// <para>3. 路径间可并行（本版本先串行，后续可改为并行）</para>
    /// <para>4. 跨路径复用：同一 node_id 已执行 → 从结果池读取，不重跑</para>
    /// <para>5. 节点失败 → 当前路径终止，继续下一路径</para>
    /// <para>6. 所有路径终结后聚合 NC 结果</para>
    ///
    /// <para>2026-09-22 修复（G1–G5）：原实现只把节点 ID 列表塞进 <see cref="PathResult.NodeIds"/>，
    /// per-node 的<b>输出/耗时/时序/复用标记</b>在路径聚合时被丢弃，
    /// 导致 wf_node_execution 只能靠路径级 Status 反推 —— 所有节点同状态、耗时恒 0、
    /// 只有路径最后一个节点有输出。现改为逐节点记录 <see cref="NodeExecutionRecord"/>。</para>
    /// <para>2026-09-22 修复（G11）：日志全部改走 <see cref="WorkflowLogger"/>，不再裸用 ILogger 拼字符串。</para>
    /// </summary>
    public class WorkflowInterpreter
    {
        private readonly NodeExecutor _nodeExecutor;
        private readonly WorkflowLogger _wfLogger;
        private readonly ILogger<WorkflowInterpreter> _logger;

        public WorkflowInterpreter(
            NodeExecutor nodeExecutor,
            WorkflowLogger wfLogger,
            ILogger<WorkflowInterpreter> logger)
        {
            _nodeExecutor = nodeExecutor;
            _wfLogger = wfLogger;
            _logger = logger;
        }

        /// <summary>
        /// 执行一个 Item（一个 NC 检查项的所有路径）
        /// </summary>
        /// <param name="parsed">解析后的工作流模型</param>
        /// <param name="taskCode">任务编码</param>
        /// <param name="itemCode">执行项编码</param>
        /// <param name="ruleCode">规则编码（仅用于日志，不参与执行）</param>
        /// <param name="contextParams">start 节点注入的上下文</param>
        /// <param name="ct">取消令牌</param>
        public async Task<ItemExecutionResult> ExecuteItemAsync(
            ParsedWorkflow parsed,
            string taskCode,
            string itemCode,
            string ruleCode,
            Dictionary<string, object> contextParams,
            CancellationToken ct = default)
        {
            var sw = Stopwatch.StartNew();

            _wfLogger.ItemStart(taskCode, itemCode, ruleCode);
            _wfLogger.PathEnum(taskCode, itemCode, parsed.Paths.Count);

            // 共享输出池：nodeId → 输出（跨路径复用的核心）
            // 所有路径共享同一份已执行节点的输出
            var sharedOutputs = new Dictionary<string, object>();

            // 已执行节点记录：nodeId → NodeExecutionResult（用于跨路径复用判断）
            var executedNodes = new Dictionary<string, NodeExecutionResult>();

            var pathResults = new List<PathResult>();

            // ★★★ 2026-10-15：item 级运行上下文 —— 必须在**本方法内克隆**，⛔ 不能直接改调用方
            //   传进来的 contextParams：那是调用方复用的字典，在里面建池会让池**跨 item 累积**
            //   （上一次检查项的判定漏进这一次 ⇒ 结论静默算错）。
            var runCtx = new Dictionary<string, object>(
                contextParams ?? new Dictionary<string, object>());

            // ★ 运行级登记池（判定清单）：声明了
            //   [Skill(RegistersTo = WorkflowContextKeys.JudgePool)] 的节点，成功执行后由
            //   SkillExecutor.RegisterToPool 把主输出追加进来。
            //   ⇒ 「汇总」从画布节点降级为运行级副作用：分支跳过不登记、跨路径复用不重复登记，
            //     从而**绕开**「汇聚节点在多路径下只看到第一条路径」的引擎限制。
            runCtx[WorkflowContextKeys.JudgePool] = new List<object>();

            // ★ 2026-09-30：缺失必要数据（本项无法判定）—— 不逐路径吞掉，直接上抛。
            //   语义：这不是「某条路径失败」，而是【整个检查项缺少必要数据】⇒ 自动失败。
            //   NodeExecutor 已把该异常放行出来（此前会被 catch(Exception) 压成字符串，
            //   八个结构化属性全丢 ⇒ 上层无法生成补录缺口）。
            try
            {
                // 逐路径执行（当前版本串行，后续可改为并行）
                for (int pathIndex = 0; pathIndex < parsed.Paths.Count; pathIndex++)
                {
                    ct.ThrowIfCancellationRequested();

                    var path = parsed.Paths[pathIndex];
                    var pathResult = await ExecutePathAsync(
                        path, pathIndex, taskCode, itemCode,
                        sharedOutputs, executedNodes, runCtx,
                        parsed.Adjacency, parsed.NodeMap, parsed.InEdges, ct);

                    pathResults.Add(pathResult);

                    _wfLogger.PathDone(taskCode, itemCode, pathIndex, pathResult.Status);

                    if (pathResult.Status == "failed")
                    {
                        _wfLogger.PathFail(taskCode, itemCode, pathIndex, pathResult.FailedAtNodeId, pathResult.Error);
                    }
                }
            }
            catch (WorkflowDataMissingException ex)
            {
                sw.Stop();
                _wfLogger.ItemDone(taskCode, itemCode, false, (int)sw.ElapsedMilliseconds);
                _logger.LogWarning(
                    "[Workflow] 缺失必要数据 task={TaskCode} item={ItemCode} rule={RuleCode} " +
                    "kind={Kind} code={DataCode} reason={Reason}",
                    taskCode, itemCode, ruleCode, ex.DataKind, ex.DataCode, ex.Reason);

                // ★ 附在 ErrorCode 上，让 WfExecutionTaskService 能落 gap + 标记任务项 failed
                return new ItemExecutionResult
                {
                    Success = false,
                    IsSuccess = false,
                    Error = ex.Message,
                    ErrorCode = WorkflowErrorCodes.DataMissing,
                    MissingData = ex,
                    PathResults = pathResults,
                    DurationMs = (int)sw.ElapsedMilliseconds,
                    NcResult = new Dictionary<string, object>
                    {
                        ["success"] = false,
                        ["error"] = ex.Message,
                        ["errorCode"] = WorkflowErrorCodes.DataMissing,
                        ["result"] = null!
                    }
                };
            }

            // 聚合 NC 结果
            var ncResult = AggregateNcResult(pathResults);
            var isSuccess = DetermineItemSuccess(pathResults);

            sw.Stop();

            _wfLogger.ItemDone(taskCode, itemCode, isSuccess, (int)sw.ElapsedMilliseconds);

            return new ItemExecutionResult
            {
                Success = ncResult.GetValueOrDefault("success") as bool? ?? false,
                Error = ncResult.GetValueOrDefault("error") as string,
                // ★ 2026-10-12：失败分类码从聚合结果冒泡上来（见 AggregateNcResult）。
                //   修复前这里恒为 null（除 DATA_MISSING 分支），UI 分不清「配置错」与「报 bug」。
                ErrorCode = ncResult.GetValueOrDefault("errorCode") as string,
                IsSuccess = isSuccess,
                PathResults = pathResults,
                DurationMs = (int)sw.ElapsedMilliseconds,
                NcResult = ncResult
            };
        }

        // ── 路径执行（串行阻塞） ──

        /// <summary>
        /// 执行单条路径（串行阻塞）
        /// </summary>
        private async Task<PathResult> ExecutePathAsync(
            List<WorkflowNodeConfig> path,
            int pathIndex,
            string taskCode,
            string itemCode,
            Dictionary<string, object> sharedOutputs,
            Dictionary<string, NodeExecutionResult> executedNodes,
            Dictionary<string, object> contextParams,
            Dictionary<string, List<WorkflowEdgeConfig>> adjacency,
            Dictionary<string, WorkflowNodeConfig> nodeMap,
            Dictionary<string, List<WorkflowEdgeConfig>> inEdges,
            CancellationToken ct)
        {
            var nodeIds = path.Select(n => n.NodeId).ToList();

            _wfLogger.PathStart(taskCode, itemCode, pathIndex, path.Count);

            var pathStartedAt = DateTime.Now;
            var pathSw = Stopwatch.StartNew();

            var pathResult = new PathResult
            {
                PathIndex = pathIndex,
                Status = "executing",
                NodeIds = nodeIds,
                StartedAt = pathStartedAt
            };

            // 逐节点串行执行
            // ★ preInserted：预执行的来源节点（参数即边）会插进 NodeIds，位置在所属 AI 节点之前，
            //   后续插入要按累计位移算，否则索引会错位。
            var preInserted = 0;

            for (var i = 0; i < path.Count; i++)
            {
                var node = path[i];
                ct.ThrowIfCancellationRequested();

                // 本节点执行后的结果（复用或真执行）—— branch 裁剪判定要用
                NodeExecutionResult branchCandidate;

                // 跨路径复用检查
                if (executedNodes.TryGetValue(node.NodeId, out var existingResult))
                {
                    _wfLogger.NodeReuse(taskCode, itemCode, node.NodeId, node.NodeId);

                    // 复用也要留下明细：IsReused=true，输出/耗时/时序取自原始执行那次
                    pathResult.NodeResults.Add(
                        NodeExecutionRecord.From(node.NodeId, existingResult, isReused: true));

                    // 已执行过的节点，直接使用结果（不重跑）
                    // 失败的节点也需要记录，让路径终止
                    if (!existingResult.Success)
                    {
                        return FinishPath(pathResult, "failed", node.NodeId,
                            existingResult.Error, existingResult.ErrorCode, pathSw);
                    }

                    branchCandidate = existingResult;
                }
                else
                {
                    // ★★★ 参数即边（设计 §2.2 / 用户裁决 ②）：AI 节点开跑前，先把
                    //   `config.params[].nodeId` 引用的来源节点（docField / docTable 等数据源）
                    //   预执行掉。这些节点**不在任何路径上**（没有物理连线），不预执行就永远是空。
                    //
                    //   ⚠️ 两个关键约束：
                    //   ① 预执行结果**同时写 sharedOutputs（nodeId + title 双键）与 executedNodes** ——
                    //      后者让后续路径走「跨路径复用」，⛔ 不会重复执行；
                    //   ② 预执行**不改 Paths**：它只加 NodeResults/NodeIds 明细，
                    //      「执行了几条路径」仍是物理边枚举的结果（不产生伪路径）。
                    if (IsAiNode(node))
                    {
                        var pre = await PreExecuteAiParamSourcesAsync(
                            node, nodeMap, inEdges, taskCode, itemCode,
                            sharedOutputs, executedNodes, contextParams, ct);

                        if (pre.Records.Count > 0)
                        {
                            var preIds = pre.Records.Select(r => r.NodeId).ToList();
                            // 插在本 AI 节点之前，保持 NodeIds 与 NodeResults 一一对应
                            pathResult.NodeIds.InsertRange(i + preInserted, preIds);
                            preInserted += preIds.Count;
                            pathResult.NodeResults.AddRange(pre.Records);
                        }

                        // 来源节点失败 ⇒ 路径直接终止在它身上（与「它本来就在路径里」同一语义）
                        if (pre.Blocker != null)
                        {
                            var (blockNodeId, blockResult) = pre.Blocker.Value;
                            return FinishPath(pathResult, "failed", blockNodeId,
                                blockResult.Error, blockResult.ErrorCode, pathSw);
                        }
                    }

                    // 执行节点
                    var result = await _nodeExecutor.ExecuteAsync(
                        node, taskCode, itemCode, sharedOutputs, contextParams, ct);

                    // 记录到已执行池
                    executedNodes[node.NodeId] = result;

                    // 记录 per-node 明细（真执行，IsReused=false）——中间节点的输出在此保留，不再丢失
                    pathResult.NodeResults.Add(
                        NodeExecutionRecord.From(node.NodeId, result, isReused: false));

                    // 节点失败 → 当前路径终止
                    if (!result.Success)
                    {
                        return FinishPath(pathResult, "failed", node.NodeId,
                            result.Error, result.ErrorCode, pathSw);
                    }

                    // 节点成功 → 输出写入共享池（同时写入 nodeId 和 title 作为 key）
                    // AI 节点通过 {{节点标题.端口}} 引用上游输出时，需要 title 作为 key
                    sharedOutputs[node.NodeId] = result.Output;
                    if (!string.IsNullOrEmpty(node.Title))
                        sharedOutputs[node.Title] = result.Output;

                    branchCandidate = result;
                }

                // ★★ 2026-10-12：branch 判定裁剪 —— 本路径走的是「未被选中」的那条臂 ⇒ 到此为止。
                //
                //   背景：路径是**解析期**静态枚举的（WorkflowConfigParser.EnumeratePaths 把 branch
                //   两条臂都枚举成独立路径），而 branch 的判定只有**运行期**才知道 ⇒ 修复前两条臂
                //   **都会执行**，分支节点输出的 decision 成了死数据。
                //   实测（用户 2026-10-12）：is_empty 判出 true ⇒ branch decision=success，
                //   但 failure 臂上的节点照样跑完 ⇒ 与「值判断 + 分流」的定稿设计相矛盾。
                //
                //   判定方式：取 branch 输出里的 decision（success/failure）→ 查邻接表得到该臂的
                //   **真实目标节点** → 与本路径的下一个节点比对，不一致即跳过。
                //
                //   ⚠️ 保守策略：拿不到 decision、或该臂没接线（查不到目标）时**不跳过** ——
                //      宁可多跑一条路径，也不因判据缺失而漏跑（漏跑会静默少算一个维度）。
                if (IsBranchNode(node) && i + 1 < path.Count)
                {
                    var decision = branchCandidate.Output?.GetValueOrDefault("decision")?.ToString();
                    var takenTarget = ResolveBranchTarget(adjacency, node.NodeId, decision);
                    var nextNodeId = path[i + 1].NodeId;

                    if (takenTarget != null &&
                        !string.Equals(takenTarget, nextNodeId, StringComparison.Ordinal))
                    {
                        var skipped = FinishPath(pathResult, "skipped", null, null, null, pathSw);
                        skipped.SkipReason =
                            $"条件分支「{node.Title}」判定为 {decision}，本路径走的是另一条分支" +
                            $"（应走 {takenTarget}，本路径为 {nextNodeId}）⇒ 整条路径跳过";

                        _logger.LogInformation(
                            "[Workflow] 路径跳过 task={TaskCode} item={ItemCode} path={PathIndex} reason={Reason}",
                            taskCode, itemCode, pathIndex, skipped.SkipReason);

                        return skipped;
                    }
                }
            }

            // 路径中所有节点执行成功
            var finished = FinishPath(pathResult, "completed", null, null, null, pathSw);

            // 路径最终输出 = 最后一个节点（通常是 end）的输出
            var lastNode = path.Last();
            if (executedNodes.TryGetValue(lastNode.NodeId, out var lastResult) && lastResult.Success)
            {
                finished.Output = lastResult.Output;
            }

            return finished;
        }

        /// <summary>
        /// 收口路径结果：写入终态、失败点、失败分类码、耗时与时序（成功/失败两条出口共用，避免时序漏写）
        /// <para>★ 2026-10-12 增 <paramref name="errorCode"/> —— 路径级失败此前只带一句 Error 文本，
        /// 分类码在 <c>NodeExecutionResult</c> 上就丢了，UI 只能显示「工作流执行失败」。</para>
        /// </summary>
        private static PathResult FinishPath(
            PathResult pathResult, string status, string? failedAtNodeId,
            string? error, string? errorCode, Stopwatch pathSw)
        {
            pathSw.Stop();
            pathResult.Status = status;
            pathResult.FailedAtNodeId = failedAtNodeId;
            pathResult.Error = error;
            pathResult.ErrorCode = errorCode;
            pathResult.DurationMs = (int)pathSw.ElapsedMilliseconds;
            pathResult.CompletedAt = DateTime.Now;
            return pathResult;
        }

        // ── 参数即边：AI 节点来源预执行（设计 §2.2，2026-10-14） ──

        /// <summary>是否 AI 节点 —— 只有它带 <c>config.params</c>，也只有它会触发预执行。</summary>
        private static bool IsAiNode(WorkflowNodeConfig node)
            => string.Equals(node.NodeType, "ai_node", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// ★ 参数即边（用户裁决 ②）：在 AI 节点开跑前，把 <c>config.params[].nodeId</c> 引用的
        /// 来源节点（<c>docField</c> / <c>docTable</c> 等<b>数据源</b>）预执行掉。
        ///
        /// <para><b>为什么必须有这一步：</b>数据源节点<b>不需要用户手工连线</b>（设计 §2.2），
        /// 因此它们不在任何一条路径上 —— 路径枚举只看物理边，压根走不到它们。
        /// 不预执行 ⇒ AI 节点取到的永远是「上游不存在」⇒ 真实路径抛
        /// <c>WorkflowDataMissingException</c> 中断，而画布上明明配了参数。</para>
        ///
        /// <para><b>三条纪律（改前必读）：</b></para>
        /// <list type="number">
        /// <item><b>DFS 后序 = 拓扑序</b>：先递归确保来源节点自己的入边上游跑完，再跑它自己；
        ///   <c>visiting</c> 集合防环（解析器本就禁环，这里只是兜底）。</item>
        /// <item><b>结果三处落位</b>：<c>executedNodes</c>（跨路径复用池）+ <c>sharedOutputs</c>
        ///   （nodeId 与 title <b>双键</b>，供 <c>{{中文标题.端口}}</c> 取值）+ 返回的
        ///   <see cref="NodeExecutionRecord"/>（落 <c>wf_node_execution</c>，验收判据要看到行）。</item>
        /// <item><b>⛔ 不改 Paths</b>：只加执行明细，不加路径 —— 否则会出现
        ///   「start → docField → ai」这类伪路径，让「执行了几条路径」失真（§2.2 关键约束）。</item>
        /// </list>
        ///
        /// <para><b>节点类型过滤</b>与测试路径 <c>ResolveUpstreamPlan</c> 完全一致：
        /// <c>start / end / branch / ai_node</c> 不预执行（start 由 contextParams 注入代替，
        /// ai_node 不递归执行 —— 只测目标那一个）。</para>
        ///
        /// <para><b>返回</b>：<c>Records</c> = 本轮真正新执行的节点明细（按拓扑序）；
        /// <c>Blocker</c> = 拦住本路径的失败节点（null = 无阻塞，调用方继续跑 AI 节点）。</para>
        /// </summary>
        private async Task<(List<NodeExecutionRecord> Records, (string NodeId, NodeExecutionResult Result)? Blocker)>
            PreExecuteAiParamSourcesAsync(
                WorkflowNodeConfig aiNode,
                Dictionary<string, WorkflowNodeConfig> nodeMap,
                Dictionary<string, List<WorkflowEdgeConfig>> inEdges,
                string taskCode,
                string itemCode,
                Dictionary<string, object> sharedOutputs,
                Dictionary<string, NodeExecutionResult> executedNodes,
                Dictionary<string, object> contextParams,
                CancellationToken ct)
        {
            var records = new List<NodeExecutionRecord>();
            if (nodeMap == null || nodeMap.Count == 0) return (records, null);

            // 双键索引：refKey（节点 ID ∪ 节点标题）→ 节点 ID
            // 提示词里 {{中文标题.端口}} 的老写法要能反查出**节点 ID**，否则预执行找不到节点
            var refIndex = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var kv in nodeMap)
            {
                refIndex[kv.Key] = kv.Key;
                if (!string.IsNullOrEmpty(kv.Value.Title))
                    refIndex.TryAdd(kv.Value.Title!, kv.Key);
            }

            var sourceIds = AiParams.CollectSourceNodeIds(
                aiNode, refKey => refIndex.TryGetValue(refKey, out var id) ? id : null);
            if (sourceIds.Count == 0) return (records, null);

            var visiting = new HashSet<string>(StringComparer.Ordinal);

            async Task<(string NodeId, NodeExecutionResult Result)?> EnsureAsync(string nodeId)
            {
                // ① 已执行过（本路径前段 / 其它路径 / 上一轮预执行）⇒ 复用；
                //    只有「上次是失败的」才拦住本路径（成功则直接用）
                if (executedNodes.TryGetValue(nodeId, out var done))
                    return done.Success ? null : (nodeId, done);

                // ② 节点已被删除 / 不在本图里 ⇒ 不预执行，交给 ResolveTemplateRefs 报 DataMissing
                //    （点名缺失的引用比在这里静默吞掉更有用）
                if (!nodeMap.TryGetValue(nodeId, out var src))
                {
                    _logger.LogWarning(
                        "[AI_PREEXEC] 参数引用的节点 {NodeId} 不在本工作流中（可能已删除），跳过预执行",
                        nodeId);
                    return null;
                }

                // ③ 防环
                if (!visiting.Add(nodeId)) return null;

                // ④ 先跑它自己的上游（DFS 后序 ⇒ 拓扑序）
                if (inEdges != null && inEdges.TryGetValue(nodeId, out var edges))
                {
                    foreach (var e in edges)
                    {
                        if (string.IsNullOrEmpty(e.Source) || e.Source == nodeId) continue;
                        var up = await EnsureAsync(e.Source);
                        if (up != null) return up;
                    }
                }

                // ⑤ 与测试路径同一套过滤：start/end/branch/ai_node 不预执行
                var type = src.NodeType?.ToLowerInvariant();
                if (type is "start" or "end" or "branch" or "ai_node")
                {
                    visiting.Remove(nodeId);
                    return null;
                }

                // ⑥ 打点：说明「这个数据源是因为 AI 参数被拉起来跑的」（NODE_EXEC 是既有契约 TAG）
                _wfLogger.NodeExec(taskCode, itemCode, aiNode.NodeId, $"ai_param_preexec:{nodeId}");

                var result = await _nodeExecutor.ExecuteAsync(
                    src, taskCode, itemCode, sharedOutputs, contextParams, ct);

                visiting.Remove(nodeId);

                // ⑦ 三处落位：复用池 + 共享输出池（双键）+ 执行明细（落 wf_node_execution）
                executedNodes[src.NodeId] = result;
                if (result.Success)
                {
                    sharedOutputs[src.NodeId] = result.Output;
                    if (!string.IsNullOrEmpty(src.Title))
                        sharedOutputs[src.Title] = result.Output;
                }

                records.Add(NodeExecutionRecord.From(src.NodeId, result, isReused: false));

                return result.Success ? null : (src.NodeId, result);
            }

            foreach (var srcId in sourceIds)
            {
                var blocker = await EnsureAsync(srcId);
                if (blocker != null) return (records, blocker.Value);
            }

            return (records, null);
        }

        // ── 分支裁剪判据（2026-10-12） ──

        /// <summary>
        /// 节点是否为条件分支 —— 全项目<b>唯一</b>会改变「本路径该不该继续跑」的节点类型。
        /// </summary>
        private static bool IsBranchNode(WorkflowNodeConfig node)
            => string.Equals(node.NodeType, "branch", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// 由 branch 的判定结果解析出该臂的<b>真实目标节点 ID</b>。
        ///
        /// <para>边上的 <c>SourceHandle</c> 就是臂名（前端 branch 节点的
        /// <c>outputPorts:[{name:'success'},{name:'failure'}]</c> 即此契约，
        /// <c>NodeExecutor.ExecuteBranch</c> 写出的 <c>decision</c> 取值同源）。</para>
        ///
        /// <para><b>返回 <c>null</c> 的三种情况</b>（调用方据此走保守策略「不跳过」）：
        /// ① <paramref name="decision"/> 为空（branch 输出异常）；
        /// ② 邻接表里查不到该 branch 的出边；
        /// ③ 该臂没接线（找不到 <c>SourceHandle</c> 匹配的边）。</para>
        /// </summary>
        private static string? ResolveBranchTarget(
            Dictionary<string, List<WorkflowEdgeConfig>> adjacency,
            string branchNodeId,
            string? decision)
        {
            if (string.IsNullOrWhiteSpace(decision)) return null;
            if (adjacency == null || !adjacency.TryGetValue(branchNodeId, out var outEdges)) return null;

            var edge = outEdges.FirstOrDefault(e =>
                string.Equals(e.SourceHandle, decision, StringComparison.OrdinalIgnoreCase));

            return string.IsNullOrEmpty(edge?.Target) ? null : edge.Target;
        }

        // ── NC 结果聚合 ──

        /// <summary>
        /// 聚合所有路径的结果为 NC 最终结果
        /// <summary>
        /// 汇总多路径执行结果。
        /// <para>★ 2026-09-29 修正（18 号 §七 B1）：原实现只取 <b>第一条</b>成功路径的输出
        /// （<c>successPaths[0]</c>），其余路径的结果被静默丢弃。
        /// 这在「多维度全覆盖」型工作流（NC 检查）里会导致结论失真。</para>
        /// <para><b>修正后</b>：
        ///   ① 新增 <c>pathResults</c> —— <b>全部</b>路径的结构化结果数组，供上层聚合判定
        ///   ② <c>result</c> 字段保持「第一条成功路径」的语义（向后兼容，勿破坏既有工作流）
        ///   ③ 成功判定保持「任一路径成功即成功」</para>
        /// <para><b>成功判定规则（不变）</b>：任一路径到达 end 且成功 → NC 通过；所有路径失败 → NC 失败。</para>
        /// </summary>
        private Dictionary<string, object> AggregateNcResult(List<PathResult> pathResults)
        {
            var successPaths = pathResults
                .Where(r => r.Status == "completed")
                .ToList();

            var failedPaths = pathResults
                .Where(r => r.Status == "failed")
                .ToList();

            // ★ 2026-10-12：被 branch 判定裁剪掉的路径（走的是未被选中的那条臂）。
            //   ⛔ 既不算成功也不算失败 —— 「条件不满足」是正常业务行为。
            var skippedPaths = pathResults
                .Where(r => r.Status == "skipped")
                .ToList();

            // ★ 2026-10-12：executedPaths 只含「真正跑过」的路径（completed + failed）。
            //   修复前它等于「全部枚举出的路径」⇒ 分支裁剪后会把跳过的路径也算进「已执行」，
            //   界面显示「执行了 2 条」而实际只跑了 1 条。
            var executedPaths = pathResults
                .Where(r => r.Status is "completed" or "failed")
                .Select(r => r.PathIndex)
                .ToArray();

            // ★ 全量路径结果（供 nc_conclusion_calc 之类做多维度聚合判定）
            var allPathResults = pathResults
                .OrderBy(p => p.PathIndex)
                .Select(p => new Dictionary<string, object?>
                {
                    ["pathIndex"]    = p.PathIndex,
                    ["status"]       = p.Status,
                    ["output"]       = p.Output,
                    ["result"]       = ExtractSingleResult(p.Output?.GetValueOrDefault("result") ?? p.Output),
                    ["failedAtNode"] = p.FailedAtNodeId,
                    ["error"]        = p.Error,
                    // ★ 2026-10-12：跳过路径的说明（与 error 分开，跳过不是失败）
                    ["skipped"]      = p.Status == "skipped",
                    ["skipReason"]   = p.SkipReason
                })
                .ToList();

            if (successPaths.Count > 0)
            {
                // ★ result 仍是第一条成功路径（向后兼容）；★ 完整结果走 pathResults
                var firstSuccess = successPaths[0];
                var rawResult = firstSuccess.Output?.GetValueOrDefault("result") ?? firstSuccess.Output;
                var result = ExtractSingleResult(rawResult);

                return new Dictionary<string, object>
                {
                    ["success"] = true,
                    ["error"] = null!,
                    ["result"] = result ?? new { },
                    // ★ 新增：全部路径的结构化结果，避免上层丢失多维度信息
                    ["pathResults"] = allPathResults,
                    ["executedPaths"] = executedPaths,
                    ["successPaths"] = successPaths.Select(p => p.PathIndex).ToArray(),
                    ["failedPaths"] = failedPaths.Select(p => p.PathIndex).ToArray(),
                    ["skippedPaths"] = skippedPaths.Select(p => p.PathIndex).ToArray()
                };
            }

            if (failedPaths.Count > 0)
            {
                var errors = failedPaths
                    .Where(f => !string.IsNullOrEmpty(f.Error))
                    .Select(f => $"路径{f.PathIndex}@{f.FailedAtNodeId}: {f.Error}")
                    .ToList();

                var errorMsg = errors.Count > 0
                    ? string.Join("; ", errors)
                    : "所有路径均失败";

                // ★ 2026-10-12：把失败分类码冒泡到 item 级 —— 取第一条「有分类码」的失败路径。
                //   修复前 item.ErrorCode 只在 DATA_MISSING 那条 catch 分支里被赋值，
                //   其余失败（CONFIG_ERROR / SYSTEM_ERROR / PRECONDITION_EMPTY）到不了 UI。
                var errorCode = failedPaths
                    .Select(f => f.ErrorCode)
                    .FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));

                return new Dictionary<string, object>
                {
                    ["success"] = false,
                    ["error"] = errorMsg,
                    ["errorCode"] = errorCode!,
                    ["result"] = null!,
                    ["pathResults"] = allPathResults,
                    ["executedPaths"] = executedPaths,
                    ["successPaths"] = Array.Empty<int>(),
                    ["failedPaths"] = failedPaths.Select(p => p.PathIndex).ToArray(),
                    ["skippedPaths"] = skippedPaths.Select(p => p.PathIndex).ToArray()
                };
            }

            // ★ 2026-10-12：全部路径都被条件分支跳过（既没成功也没失败）。
            //   单列一条，⛔ 不与下面「配置可能不合理」混为一谈 —— 拓扑本身可能是对的，
            //   只是本次数据下 branch 选中的那条臂没接到结束节点。
            if (skippedPaths.Count > 0)
            {
                return new Dictionary<string, object>
                {
                    ["success"] = false,
                    ["error"] = $"全部 {skippedPaths.Count} 条路径都被条件分支跳过（判定命中的分支未接线到结束节点）",
                    ["errorCode"] = WorkflowErrorCodes.ConfigError,
                    ["result"] = null!,
                    ["pathResults"] = allPathResults,
                    ["executedPaths"] = executedPaths,
                    ["successPaths"] = Array.Empty<int>(),
                    ["failedPaths"] = Array.Empty<int>(),
                    ["skippedPaths"] = skippedPaths.Select(p => p.PathIndex).ToArray()
                };
            }

            // 所有路径都走完了但没有有效结果
            return new Dictionary<string, object>
            {
                ["success"] = false,
                ["error"] = "所有路径均无有效结果，工作流配置可能不合理",
                // ★ 2026-10-12：这确实是一类「配置错」（图上没有任何一条路径能到达 end）
                ["errorCode"] = WorkflowErrorCodes.ConfigError,
                ["result"] = null!,
                ["pathResults"] = allPathResults,
                ["executedPaths"] = executedPaths,
                ["successPaths"] = Array.Empty<int>(),
                ["failedPaths"] = Array.Empty<int>(),
                ["skippedPaths"] = Array.Empty<int>()
            };
        }

        /// <summary>
        /// 判断 Item 的业务成功标志
        /// <para>规则：至少一条路径到达 end 且成功 → true</para>
        /// </summary>
        private static bool DetermineItemSuccess(List<PathResult> pathResults)
        {
            return pathResults.Any(r => r.Status == "completed");
        }

        /// <summary>
        /// 从结果对象中提取单值结果
        /// <para>规则：</para>
        /// <para>  非字典 → 直接返回</para>
        /// <para>  单 key 字典 → 返回其 value</para>
        /// <para>  多 key 字典（上游节点完整输出）→ 优先返回 result 字段，其次 compare_result 字段</para>
        /// </summary>
        private static object? ExtractSingleResult(object? rawResult)
        {
            if (rawResult is not Dictionary<string, object> dict)
                return rawResult;

            // 单 key → 直接返回其 value
            if (dict.Count == 1)
                return dict.Values.First();

            // 多 key → 优先取 result，其次 compare_result
            if (dict.TryGetValue("result", out var resultValue))
                return resultValue;

            if (dict.TryGetValue("compare_result", out var compareResultValue))
                return compareResultValue;

            // 没有已知 key → 返回整个字典
            return dict;
        }
    }
}

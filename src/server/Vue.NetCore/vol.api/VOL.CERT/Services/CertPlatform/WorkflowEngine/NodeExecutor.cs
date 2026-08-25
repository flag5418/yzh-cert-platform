using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VOL.CERT.Services.CertPlatform.WorkflowEngine.Models;
using YZH.Core.AI.Clients;
using YZH.Core.AI.Prompt;
using YZH.Core.Workflow;

namespace VOL.CERT.Services.CertPlatform.WorkflowEngine
{
    /// <summary>
    /// 节点执行器 — 负责单个节点的执行逻辑
    /// <para>文档：工作流执行引擎-数据模型与接口设计-V3 §十（节点执行规则）</para>
    /// <para>职责：</para>
    /// <para>1. 解析节点输入参数（constant: 前缀截取 / 引用上游节点输出）</para>
    /// <para>2. 按 nodeType 分发执行：start/docField/docTable/skill/branch/end/ai_node</para>
    /// <para>3. 返回统一的 NodeExecutionResult</para>
    /// <para>4. 不负责路径驱动、跨路径复用、缓存读写（由上层 WorkflowInterpreter 管理）</para>
    /// </summary>
    public class NodeExecutor
    {
        private readonly ISkillRegistry _skillRegistry;
        private readonly AiNodeExecutor _aiNodeExecutor;
        private readonly ILogger<NodeExecutor> _logger;

        public NodeExecutor(
            ISkillRegistry skillRegistry,
            ILlmClient llmClient,
            IPromptInterpreter promptInterpreter,
            ILogger<NodeExecutor> logger,
            ILogger<AiNodeExecutor> aiNodeLogger)
        {
            _skillRegistry = skillRegistry;
            _logger = logger;
            _aiNodeExecutor = new AiNodeExecutor(llmClient, skillRegistry, promptInterpreter, aiNodeLogger);
        }

        /// <summary>
        /// 执行单个节点
        /// </summary>
        /// <param name="node">节点配置</param>
        /// <param name="taskCode">任务编码</param>
        /// <param name="itemCode">执行项编码</param>
        /// <param name="sharedOutputs">当前路径中已完成节点的输出（nodeId → output）</param>
        /// <param name="contextParams">start 节点注入的上下文参数</param>
        /// <param name="ct">取消令牌</param>
        public async Task<NodeExecutionResult> ExecuteAsync(
            WorkflowNodeConfig node,
            string taskCode,
            string itemCode,
            Dictionary<string, object> sharedOutputs,
            Dictionary<string, object> contextParams,
            CancellationToken ct = default)
        {
            var sw = Stopwatch.StartNew();

            _logger.LogInformation(
                "[NODE_START] taskCode={TaskCode}, itemCode={ItemCode}, nodeId={NodeId}, nodeType={NodeType}, title={Title}",
                taskCode, itemCode, node.NodeId, node.NodeType, node.Title);

            try
            {
                // 解析输入参数（使用 inputTypes 精确判断类型）
                var resolvedInputs = ResolveInputs(node.Inputs, node.InputTypes, sharedOutputs, contextParams);

                _logger.LogInformation(
                    "[NODE_INPUTS] taskCode={TaskCode}, itemCode={ItemCode}, nodeId={NodeId}, inputs={Inputs}",
                    taskCode, itemCode, node.NodeId,
                    JsonSerializer.Serialize(resolvedInputs));

                // 按 nodeType 分发执行
                var output = node.NodeType.ToLowerInvariant() switch
                {
                    "start" => await ExecuteStartAsync(node, contextParams),
                    "end" => ExecuteEnd(node, resolvedInputs, sharedOutputs),
                    "branch" => ExecuteBranch(node, resolvedInputs, sharedOutputs),
                    "docfield" => await ExecuteDocFieldAsync(node, taskCode, ct),
                    "doctable" => await ExecuteDocTableAsync(node, taskCode, ct),
                    "skill" => await ExecuteSkillAsync(node, taskCode, itemCode, resolvedInputs, ct),
                    "ai_node" => await ExecuteAiNodeAsync(node, taskCode, itemCode, sharedOutputs, contextParams, ct),
                    _ => throw new NotSupportedException($"不支持的节点类型: {node.NodeType}")
                };

                sw.Stop();

                _logger.LogInformation(
                    "[NODE_OUTPUT] taskCode={TaskCode}, itemCode={ItemCode}, nodeId={NodeId}, output={Output}, durationMs={DurationMs}",
                    taskCode, itemCode, node.NodeId,
                    JsonSerializer.Serialize(output), sw.ElapsedMilliseconds);

                _logger.LogInformation(
                    "[NODE_DONE] taskCode={TaskCode}, itemCode={ItemCode}, nodeId={NodeId}, status=completed, durationMs={DurationMs}",
                    taskCode, itemCode, node.NodeId, sw.ElapsedMilliseconds);

                return NodeExecutionResult.Ok(output, (int)sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                sw.Stop();

                _logger.LogError(
                    "[NODE_FAIL] taskCode={TaskCode}, itemCode={ItemCode}, nodeId={NodeId}, error={Error}, durationMs={DurationMs}",
                    taskCode, itemCode, node.NodeId, ex.Message, sw.ElapsedMilliseconds);

                return NodeExecutionResult.Fail(ex.Message, (int)sw.ElapsedMilliseconds);
            }
        }

        // ── 输入参数解析 ──

        /// <summary>
        /// 解析节点输入参数
        /// <para>文档：V3 §2.4 输入参数绑定</para>
        /// <para>规则：</para>
        /// <para>  inputTypes[portName] = "constant" → 值为常量字符串，直接使用</para>
        /// <para>  inputTypes[portName] = "link" → 值为上游节点 ID，从 sharedOutputs 读取</para>
        /// <para>  inputTypes 不存在或未声明 → 回退旧逻辑（constant: 前缀 / 节点 ID 猜测）</para>
        /// </summary>
        private Dictionary<string, object> ResolveInputs(
            Dictionary<string, string> inputs,
            Dictionary<string, string> inputTypes,
            Dictionary<string, object> sharedOutputs,
            Dictionary<string, object> contextParams)
        {
            var resolved = new Dictionary<string, object>();

            if (inputs == null || inputs.Count == 0)
                return resolved;

            foreach (var (portName, rawValue) in inputs)
            {
                var inputType = inputTypes?.GetValueOrDefault(portName);
                resolved[portName] = ResolveInputValue(rawValue, inputType, sharedOutputs, contextParams);
            }

            return resolved;
        }

        /// <summary>
        /// 解析单个输入值
        /// </summary>
        /// <param name="rawValue">原始值</param>
        /// <param name="inputType">输入类型："link" / "constant" / null（未知）</param>
        /// <param name="sharedOutputs">已执行节点输出池</param>
        /// <param name="contextParams">上下文参数</param>
        private object ResolveInputValue(
            string rawValue,
            string inputType,
            Dictionary<string, object> sharedOutputs,
            Dictionary<string, object> contextParams)
        {
            if (string.IsNullOrEmpty(rawValue))
                return null;

            // 显式声明为常量 → 直接返回原始值
            if (string.Equals(inputType, "constant", StringComparison.OrdinalIgnoreCase))
            {
                // 兼容旧 constant: 前缀
                if (rawValue.StartsWith("constant:", StringComparison.Ordinal))
                    return rawValue["constant:".Length..];
                return rawValue;
            }

            // 显式声明为连线 → 从 sharedOutputs 读取
            if (string.Equals(inputType, "link", StringComparison.OrdinalIgnoreCase))
            {
                // 尝试从 contextParams 读取（start 节点注入的上下文）
                if (contextParams != null && contextParams.TryGetValue(rawValue, out var ctxValue))
                    return ctxValue;

                // 从已执行节点输出读取
                if (sharedOutputs != null && sharedOutputs.TryGetValue(rawValue, out var output))
                {
                    if (output is Dictionary<string, object> outputDict)
                    {
                        if (outputDict.Count == 1)
                            return outputDict.Values.First();
                        return outputDict;
                    }
                    return output;
                }

                _logger.LogWarning("连线输入值 '{RawValue}' 未找到对应节点输出，返回原始值", rawValue);
                return rawValue;
            }

            // 旧逻辑回退：inputType 为 null 或未声明
            // 1. constant: 前缀 → 常量
            if (rawValue.StartsWith("constant:", StringComparison.Ordinal))
            {
                return rawValue["constant:".Length..];
            }

            // 2. 尝试从 contextParams 读取
            if (contextParams != null && contextParams.TryGetValue(rawValue, out var ctxValue2))
            {
                return ctxValue2;
            }

            // 3. 尝试从已执行节点的输出读取
            if (sharedOutputs != null && sharedOutputs.TryGetValue(rawValue, out var output2))
            {
                if (output2 is Dictionary<string, object> outputDict2)
                {
                    if (outputDict2.Count == 1)
                        return outputDict2.Values.First();
                    return outputDict2;
                }
                return output2;
            }

            // 4. 既不是 constant: 也不是已执行节点 → 作为原始字符串返回
            _logger.LogWarning("输入值 '{RawValue}' 无法解析为常量或已执行节点输出，作为原始字符串使用", rawValue);
            return rawValue;
        }

        // ── 各节点类型执行逻辑 ──

        /// <summary>
        /// start 节点：注入上下文参数
        /// <para>文档：V3 §10.2 — 返回 { enterpriseCode, phaseCode, standardCode, fileCode }</para>
        /// </summary>
        private Task<Dictionary<string, object>> ExecuteStartAsync(
            WorkflowNodeConfig node,
            Dictionary<string, object> contextParams)
        {
            _logger.LogInformation(
                "[START_INJECT] enterpriseCode={Enterprise}, phaseCode={Phase}, standardCode={Standard}",
                contextParams?.GetValueOrDefault("enterpriseCode"),
                contextParams?.GetValueOrDefault("phaseCode"),
                contextParams?.GetValueOrDefault("standardCode"));

            return Task.FromResult(new Dictionary<string, object>(contextParams ?? new()));
        }

        /// <summary>
        /// end 节点：汇聚上游结果，输出统一格式
        /// <para>文档：V3 §10.2 — 返回 { success, error, result }</para>
        /// </summary>
        private Dictionary<string, object> ExecuteEnd(
            WorkflowNodeConfig node,
            Dictionary<string, object> resolvedInputs,
            Dictionary<string, object> sharedOutputs)
        {
            // 从 inputs 中找到 result 端口绑定的上游节点
            var result = resolvedInputs?.GetValueOrDefault("result");

            _logger.LogInformation(
                "[END_COLLECT] title={Title}, upstreamOutput={Output}",
                node.Title, result != null ? JsonSerializer.Serialize(result) : "null");

            return new Dictionary<string, object>
            {
                ["success"] = true,
                ["error"] = null!,
                ["result"] = result ?? new { }
            };
        }

        /// <summary>
        /// branch 节点：读取 condition 输入的 bool 值进行分流
        /// <para>文档：V3 §10.2 — 返回 { decision: "success" | "failure" }</para>
        /// <para>注意：condition 可能是 compare 节点返回的多 key 字典，需要从中提取布尔值</para>
        /// </summary>
        private Dictionary<string, object> ExecuteBranch(
            WorkflowNodeConfig node,
            Dictionary<string, object> resolvedInputs,
            Dictionary<string, object> sharedOutputs)
        {
            // 从 resolvedInputs 中读取 condition
            var conditionValue = resolvedInputs?.GetValueOrDefault("condition");

            // 如果 condition 是字典（如 compare 节点的输出），尝试从中提取布尔值
            if (conditionValue is Dictionary<string, object> dict)
            {
                // 优先取 result / compare_result / success 字段
                if (dict.TryGetValue("result", out var r) && r is bool br)
                    conditionValue = br;
                else if (dict.TryGetValue("compare_result", out var cr) && cr is bool bcr)
                    conditionValue = bcr;
                else if (dict.TryGetValue("success", out var s) && s is bool bs)
                    conditionValue = bs;
            }

            bool condition = conditionValue switch
            {
                bool b => b,
                string s when bool.TryParse(s, out var parsed) => parsed,
                _ => false
            };

            var decision = condition ? "success" : "failure";

            _logger.LogInformation(
                "[BRANCH_DECISION] condition={Condition}, decision={Decision}",
                condition, decision);

            return new Dictionary<string, object>
            {
                ["decision"] = decision
            };
        }

        /// <summary>
        /// docField 节点：按 docType 解析文件来源，提取字段值
        /// <para>文档：V3 §5.4 — standard → 从缓存或 sample_data 读取</para>
        /// <para>当前阶段：TODO 模拟实现，后续接入真实数据提取</para>
        /// </summary>
        private Task<Dictionary<string, object>> ExecuteDocFieldAsync(
            WorkflowNodeConfig node,
            string taskCode,
            CancellationToken ct)
        {
            var config = node.Config ?? new();
            var docType = config.GetValueOrDefault("docType")?.ToString() ?? "standard";
            var ruleCode = config.GetValueOrDefault("ruleCode")?.ToString() ?? "";
            var fieldCode = config.GetValueOrDefault("fieldCode")?.ToString() ?? "";

            _logger.LogInformation(
                "[DOCFIELD_QUERY] ruleCode={RuleCode}, fieldCode={FieldCode}, docType={DocType}",
                ruleCode, fieldCode, docType);

            // TODO: 接入真实数据提取
            // standard → 从 cert_doc_extraction_rule.sample_data 读取
            // enterprise → 通过 task.enterpriseCode 匹配企业文档
            // 当前阶段：返回模拟数据
            var fieldValue = config.GetValueOrDefault("fieldCode")?.ToString() ?? "模拟字段值";
            var confidence = 1.0;
            var source = docType == "standard" ? "sample_data" : "enterprise_doc";

            _logger.LogInformation(
                "[DOCFIELD_RESULT] value={Value}, confidence={Confidence}, source={Source}",
                fieldValue, confidence, source);

            return Task.FromResult(new Dictionary<string, object>
            {
                ["fieldValue"] = fieldValue,
                ["confidence"] = confidence,
                ["source"] = source
            });
        }

        /// <summary>
        /// docTable 节点：按 docType 解析文件来源，提取表格数据
        /// <para>文档：V3 §5.5 — 与 docField 类似，区别在于提取表格</para>
        /// <para>当前阶段：TODO 模拟实现</para>
        /// </summary>
        private Task<Dictionary<string, object>> ExecuteDocTableAsync(
            WorkflowNodeConfig node,
            string taskCode,
            CancellationToken ct)
        {
            var config = node.Config ?? new();
            var docType = config.GetValueOrDefault("docType")?.ToString() ?? "standard";
            var ruleCode = config.GetValueOrDefault("ruleCode")?.ToString() ?? "";
            var tableCode = config.GetValueOrDefault("tableCode")?.ToString() ?? "";

            _logger.LogInformation(
                "[DOCTABLE_QUERY] ruleCode={RuleCode}, tableCode={TableCode}, docType={DocType}",
                ruleCode, tableCode, docType);

            // TODO: 接入真实表格提取
            var rowCount = 3;
            var confidence = 1.0;
            var source = docType == "standard" ? "sample_data" : "enterprise_doc";

            _logger.LogInformation(
                "[DOCTABLE_RESULT] rowCount={RowCount}, confidence={Confidence}, source={Source}",
                rowCount, confidence, source);

            return Task.FromResult(new Dictionary<string, object>
            {
                ["rows"] = new[] { new { col1 = "row1" }, new { col1 = "row2" }, new { col1 = "row3" } },
                ["rowCount"] = rowCount,
                ["confidence"] = confidence,
                ["source"] = source
            });
        }

        /// <summary>
        /// ai_node 节点：解析自定义参数 → 预计算 → 渲染提示词 → LLM 调用 → 结果转换
        /// <para>文档：AI节点-详细设计-V1 §4</para>
        /// </summary>
        private async Task<Dictionary<string, object>> ExecuteAiNodeAsync(
            WorkflowNodeConfig node,
            string taskCode,
            string itemCode,
            Dictionary<string, object> sharedOutputs,
            Dictionary<string, object> contextParams,
            CancellationToken ct)
        {
            var result = await _aiNodeExecutor.ExecuteAsync(
                node, taskCode, itemCode, sharedOutputs, contextParams, ct);

            if (!result.Success)
            {
                throw new InvalidOperationException(result.Error ?? "AI 节点执行失败");
            }

            return result.Output;
        }

        /// <summary>
        /// skill 节点：通过 ISkillRegistry 反射执行 Skill
        /// <para>文档：V3 §10.2 — 解析 inputs → 反射调用 SkillExecutor.ExecuteAsync()</para>
        /// </summary>
        private async Task<Dictionary<string, object>> ExecuteSkillAsync(
            WorkflowNodeConfig node,
            string taskCode,
            string itemCode,
            Dictionary<string, object> resolvedInputs,
            CancellationToken ct)
        {
            var skillCode = node.SkillCode ?? "";

            _logger.LogInformation(
                "[SKILL_EXEC] skillCode={SkillCode}, method=ExecuteAsync",
                skillCode);

            // 构造 SkillContext
            var context = new SkillContext
            {
                Inputs = resolvedInputs ?? new Dictionary<string, object>(),
                WorkflowInstanceId = $"{taskCode}:{itemCode}",
                NodeId = node.NodeId,
                Logger = _logger as ILogger
            };

            // 通过 ISkillRegistry 执行
            var skillResult = await _skillRegistry.ExecuteAsync(skillCode, context, ct);

            _logger.LogInformation(
                "[SKILL_RESULT] success={Success}, result={Result}",
                skillResult.Success, JsonSerializer.Serialize(skillResult.Outputs));

            if (!skillResult.Success)
            {
                throw new InvalidOperationException(
                    $"Skill '{skillCode}' 执行失败: {skillResult.Error}");
            }

            // SkillResult.Outputs → Dictionary<string, object>
            var output = new Dictionary<string, object>();
            foreach (var (key, value) in skillResult.Outputs)
            {
                output[key] = value;
            }

            // 补充元数据
            if (skillResult.Confidence.HasValue)
                output["confidence"] = skillResult.Confidence.Value;

            return output;
        }
    }
}

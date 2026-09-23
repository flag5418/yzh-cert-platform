
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using CertPlatform.Admin.Services.Workflow.Models;
using CertPlatform.Admin.Services.Workflow.Skills;
using YZH.Core.DataBase.Interfaces;
using CertPlatform.Shared.Entities.Ent;

namespace CertPlatform.Admin.Services.Workflow
{
    /// <summary>
    /// 节点执行器 — 负责单个节点的执行逻辑
    /// <para>移植自：旧 NodeExecutor.cs（461 行）</para>
    /// <para>职责：</para>
    /// <para>1. 解析节点输入参数（constant: 前缀截取 / 引用上游节点输出）</para>
    /// <para>2. 按 nodeType 分发执行：start/docField/docTable/skill/branch/end/ai_node</para>
    /// <para>3. 返回统一的 NodeExecutionResult</para>
    /// <para>迁移改写：docField/docTable 从"模拟数据"升级为按 ent_extraction_result / ent_table_extraction_result 真实取数（D-4）</para>
    ///
    /// <para>2026-09-22：① 返回值补写 StartedAt/CompletedAt，与 DurationMs 同源（G5 时序还原）；
    /// ② 日志全部改走 <see cref="WorkflowLogger"/>，并按文档 §8.2.4 修正 DOCFIELD_QUERY/DOCTABLE_QUERY
    /// 的字段（原用 enterpriseCode 占位、漏了 docType）与 RESULT 的 source/confidence（G11）。</para>
    /// </summary>
    public class NodeExecutor
    {
        private readonly ISkillRegistry _skillRegistry;
        private readonly AiNodeExecutor _aiNodeExecutor;
        private readonly IDbOrm _db;
        private readonly WorkflowLogger _wfLogger;
        private readonly ILogger<NodeExecutor> _logger;

        public NodeExecutor(
            ISkillRegistry skillRegistry,
            AiNodeExecutor aiNodeExecutor,
            IDbOrm db,
            WorkflowLogger wfLogger,
            ILogger<NodeExecutor> logger)
        {
            _skillRegistry = skillRegistry;
            _aiNodeExecutor = aiNodeExecutor;
            _db = db;
            _wfLogger = wfLogger;
            _logger = logger;
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
            var startedAt = DateTime.Now;
            var sw = Stopwatch.StartNew();

            _wfLogger.NodeStart(taskCode, itemCode, node.NodeId, node.NodeType, node.Title);

            try
            {
                // 解析输入参数（使用 inputTypes 精确判断类型）
                var resolvedInputs = ResolveInputs(node.Inputs, node.InputTypes, sharedOutputs, contextParams);

                _wfLogger.NodeInputs(taskCode, itemCode, node.NodeId,
                    JsonSerializer.Serialize(resolvedInputs));

                // 按 nodeType 分发执行
                NodeExecutionResult nodeResult;
                if (node.NodeType.Equals("ai_node", StringComparison.OrdinalIgnoreCase))
                {
                    // AI 节点返回完整的 NodeExecutionResult（含 PromptTokens/CompletionTokens/LlmDurationMs）
                    nodeResult = await ExecuteAiNodeAsync(node, taskCode, itemCode, sharedOutputs, contextParams, ct);
                    nodeResult.DurationMs = (int)sw.ElapsedMilliseconds;
                }
                else
                {
                    var output = node.NodeType.ToLowerInvariant() switch
                    {
                        "start" => await ExecuteStartAsync(node, contextParams),
                        "end" => ExecuteEnd(node, resolvedInputs, sharedOutputs),
                        "branch" => ExecuteBranch(node, resolvedInputs, sharedOutputs),
                        "docfield" => await ExecuteDocFieldAsync(node, taskCode, contextParams, ct),
                        "doctable" => await ExecuteDocTableAsync(node, taskCode, contextParams, ct),
                        "skill" => await ExecuteSkillAsync(node, taskCode, itemCode, resolvedInputs, ct),
                        _ => throw new NotSupportedException($"不支持的节点类型: {node.NodeType}")
                    };
                    nodeResult = NodeExecutionResult.Ok(output, (int)sw.ElapsedMilliseconds);
                }

                sw.Stop();

                _wfLogger.NodeOutput(taskCode, itemCode, node.NodeId,
                    JsonSerializer.Serialize(nodeResult.Output), (int)sw.ElapsedMilliseconds);

                _wfLogger.NodeDone(taskCode, itemCode, node.NodeId, (int)sw.ElapsedMilliseconds);

                nodeResult.StartedAt = startedAt;
                nodeResult.CompletedAt = DateTime.Now;
                return nodeResult;
            }
            catch (Exception ex)
            {
                sw.Stop();

                _wfLogger.NodeFail(taskCode, itemCode, node.NodeId, ex.Message, (int)sw.ElapsedMilliseconds);

                var failResult = NodeExecutionResult.Fail(ex.Message, (int)sw.ElapsedMilliseconds);
                failResult.StartedAt = startedAt;
                failResult.CompletedAt = DateTime.Now;
                return failResult;
            }
        }

        // ── 输入参数解析 ──

        /// <summary>
        /// 解析节点输入参数
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
        private object? ResolveInputValue(
            string? rawValue,
            string? inputType,
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
        /// </summary>
        private Task<Dictionary<string, object>> ExecuteStartAsync(
            WorkflowNodeConfig node,
            Dictionary<string, object> contextParams)
        {
            _wfLogger.StartInject(
                contextParams?.GetValueOrDefault("enterpriseCode")?.ToString(),
                contextParams?.GetValueOrDefault("phaseCode")?.ToString(),
                contextParams?.GetValueOrDefault("standardCode")?.ToString());

            return Task.FromResult(new Dictionary<string, object>(contextParams ?? new()));
        }

        /// <summary>
        /// end 节点：汇聚上游结果，输出统一格式 { success, error, result }
        /// </summary>
        private Dictionary<string, object> ExecuteEnd(
            WorkflowNodeConfig node,
            Dictionary<string, object> resolvedInputs,
            Dictionary<string, object> sharedOutputs)
        {
            // 从 inputs 中找到 result 端口绑定的上游节点
            var result = resolvedInputs?.GetValueOrDefault("result");

            _wfLogger.EndCollect(node.Title, result);

            return new Dictionary<string, object>
            {
                ["success"] = true,
                ["error"] = null!,
                ["result"] = result ?? new { }
            };
        }

        /// <summary>
        /// branch 节点：读取 condition 输入的 bool 值进行分流
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

            _wfLogger.BranchDecision(condition, decision);

            return new Dictionary<string, object>
            {
                ["decision"] = decision
            };
        }

        /// <summary>
        /// docField 节点：按 fieldCode + enterpriseCode 查询提取结果（真实取数，D-4）
        /// <para>数据源：ent_extraction_result（enterpriseCode 为空时默认 YZH-STD-ENT 标准企业）</para>
        /// </summary>
        private async Task<Dictionary<string, object>> ExecuteDocFieldAsync(
            WorkflowNodeConfig node,
            string taskCode,
            Dictionary<string, object> contextParams,
            CancellationToken ct)
        {
            var config = node.Config ?? new();
            var ruleCode = config.GetValueOrDefault("ruleCode")?.ToString() ?? "";
            var fieldCode = config.GetValueOrDefault("fieldCode")?.ToString() ?? "";
            var docType = config.GetValueOrDefault("docType")?.ToString() ?? "standard";
            var enterpriseCode = contextParams?.GetValueOrDefault("enterpriseCode")?.ToString() ?? "";
            if (string.IsNullOrEmpty(enterpriseCode))
                enterpriseCode = "YZH-STD-ENT";

            var source = enterpriseCode == "YZH-STD-ENT" ? "sample_data" : "enterprise_doc";

            _wfLogger.DocFieldQuery(ruleCode, fieldCode, docType, enterpriseCode);

            if (string.IsNullOrEmpty(fieldCode))
                throw new InvalidOperationException($"docField 节点 {node.NodeId} 缺少 fieldCode 配置");

            // 真实取数：ent_extraction_result 按 field_code + enterprise_code，取最新版本
            var field = (await _db.GetOneAsync<CertPlatform.Shared.Entities.Ent.ExtractionResult>(x =>
                x.FieldCode == fieldCode && x.EnterpriseCode == enterpriseCode)).Data;

            if (field == null)
                throw new InvalidOperationException(
                    $"未找到提取字段：field_code={fieldCode}, enterprise_code={enterpriseCode}（请确认提取规则已保存且数据已落库）");

            var confidence = (double?)(field.Confidence ?? 0m) ?? 0d;

            _wfLogger.DocFieldResult(field.ExtractedValue, confidence, source);

            return new Dictionary<string, object>
            {
                ["fieldValue"] = field.ExtractedValue ?? string.Empty,
                ["confidence"] = confidence,
                ["source"] = source
            };
        }

        /// <summary>
        /// docTable 节点：按 tableCode + enterpriseCode 查询表格提取结果（真实取数，D-4）
        /// <para>数据源：ent_table_extraction_result（enterpriseCode 为空时默认 YZH-STD-ENT 标准企业）</para>
        /// </summary>
        private async Task<Dictionary<string, object>> ExecuteDocTableAsync(
            WorkflowNodeConfig node,
            string taskCode,
            Dictionary<string, object> contextParams,
            CancellationToken ct)
        {
            var config = node.Config ?? new();
            var ruleCode = config.GetValueOrDefault("ruleCode")?.ToString() ?? "";
            var tableCode = config.GetValueOrDefault("tableCode")?.ToString() ?? "";
            var docType = config.GetValueOrDefault("docType")?.ToString() ?? "standard";
            var enterpriseCode = contextParams?.GetValueOrDefault("enterpriseCode")?.ToString() ?? "";
            if (string.IsNullOrEmpty(enterpriseCode))
                enterpriseCode = "YZH-STD-ENT";

            var source = enterpriseCode == "YZH-STD-ENT" ? "sample_data" : "enterprise_doc";

            _wfLogger.DocTableQuery(ruleCode, tableCode, docType, enterpriseCode);

            if (string.IsNullOrEmpty(tableCode))
                throw new InvalidOperationException($"docTable 节点 {node.NodeId} 缺少 tableCode 配置");

            // 真实取数：ent_table_extraction_result 按 table_code + enterprise_code，取最新版本
            var table = (await _db.GetOneAsync<CertPlatform.Shared.Entities.Ent.TableExtractionResult>(x =>
                x.TableCode == tableCode && x.EnterpriseCode == enterpriseCode)).Data;

            if (table == null)
                throw new InvalidOperationException(
                    $"未找到提取表格：table_code={tableCode}, enterprise_code={enterpriseCode}（请确认提取规则已保存且数据已落库）");

            var rows = ParseTableRows(table.ExtractedJson);
            var confidence = (double?)(table.Confidence ?? 0m) ?? 0d;

            _wfLogger.DocTableResult(rows.Count, confidence, source);

            return new Dictionary<string, object>
            {
                ["rows"] = rows,
                ["rowCount"] = rows.Count,
                ["confidence"] = confidence,
                ["source"] = source
            };
        }

        /// <summary>
        /// 解析表格数据 JSON 为行数组（TableData 列存 JSON，形如 [[c1,c2],[c1,c2]] 或 {"rows":[...]}）
        /// </summary>
        private static List<object> ParseTableRows(string? tableDataJson)
        {
            var rows = new List<object>();
            if (string.IsNullOrWhiteSpace(tableDataJson))
                return rows;

            try
            {
                using var doc = JsonDocument.Parse(tableDataJson);
                var root = doc.RootElement;

                if (root.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in root.EnumerateArray())
                        rows.Add(JsonElementToObject(item));
                }
                else if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("rows", out var rowsEl) && rowsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in rowsEl.EnumerateArray())
                        rows.Add(JsonElementToObject(item));
                }
            }
            catch
            {
                // JSON 解析失败 → 返回原始文本作为单行
                rows.Add(new Dictionary<string, object> { ["raw"] = tableDataJson });
            }

            return rows;
        }

        /// <summary>JsonElement → 可序列化对象（dict/list/标量）</summary>
        private static object JsonElementToObject(JsonElement el)
        {
            switch (el.ValueKind)
            {
                case JsonValueKind.Object:
                    var dict = new Dictionary<string, object>();
                    foreach (var p in el.EnumerateObject())
                        dict[p.Name] = JsonElementToObject(p.Value);
                    return dict;
                case JsonValueKind.Array:
                    var list = new List<object>();
                    foreach (var item in el.EnumerateArray())
                        list.Add(JsonElementToObject(item));
                    return list;
                case JsonValueKind.String:
                    return el.GetString() ?? "";
                case JsonValueKind.Number:
                    return el.GetDouble();
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                default:
                    return el.GetRawText();
            }
        }

        /// <summary>
        /// ai_node 节点：委托 AiNodeExecutor
        /// <para>返回完整的 NodeExecutionResult（保留 PromptTokens/CompletionTokens/LlmDurationMs）</para>
        /// </summary>
        private async Task<NodeExecutionResult> ExecuteAiNodeAsync(
            WorkflowNodeConfig node,
            string taskCode,
            string itemCode,
            Dictionary<string, object> sharedOutputs,
            Dictionary<string, object> contextParams,
            CancellationToken ct)
        {
            // AI 节点是全流程最耗时的一类（外部大模型调用），按文档 §8.2.3 输出 NODE_EXEC
            // 作为「已进入执行、尚未返回」的分界点，便于在日志里区分「卡在大模型」与「卡在别处」
            _wfLogger.NodeExec(taskCode, itemCode, node.NodeId, "invoke_llm");

            var result = await _aiNodeExecutor.ExecuteAsync(
                node, taskCode, itemCode, sharedOutputs, contextParams, ct);

            if (!result.Success)
            {
                throw new InvalidOperationException(result.Error ?? "AI 节点执行失败");
            }

            return result;
        }

        /// <summary>
        /// skill 节点：通过 ISkillRegistry 反射执行 Skill
        /// </summary>
        private async Task<Dictionary<string, object>> ExecuteSkillAsync(
            WorkflowNodeConfig node,
            string taskCode,
            string itemCode,
            Dictionary<string, object> resolvedInputs,
            CancellationToken ct)
        {
            var skillCode = node.SkillCode ?? "";

            _wfLogger.SkillExec(skillCode, "ExecuteAsync");

            // 构造 SkillContext
            var context = new SkillContext
            {
                Inputs = resolvedInputs ?? new Dictionary<string, object>(),
                WorkflowInstanceId = $"{taskCode}:{itemCode}",
                NodeId = node.NodeId
            };

            // 通过 ISkillRegistry 执行
            var skillResult = await _skillRegistry.ExecuteAsync(skillCode, context, ct);

            _wfLogger.SkillResult(skillResult.Success, skillResult.Outputs);

            if (!skillResult.Success)
            {
                throw new InvalidOperationException(
                    $"Skill '{skillCode}' 执行失败: {skillResult.Error}");
            }

            // SkillResult.Outputs → Dictionary<string, object>
            var output = new Dictionary<string, object>();
            foreach (var kv in skillResult.Outputs)
            {
                output[kv.Key] = kv.Value;
            }

            // 补充元数据
            if (skillResult.Confidence.HasValue)
                output["confidence"] = skillResult.Confidence.Value;

            return output;
        }
    }
}

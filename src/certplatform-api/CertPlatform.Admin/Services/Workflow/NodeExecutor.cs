
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using CertPlatform.Admin.Services.DocExtraction;
using CertPlatform.Admin.Services.Workflow.Models;
using CertPlatform.Admin.Services.Workflow.Skills;
using YZH.Core.DataBase.Interfaces;
using CertPlatform.Shared.Constants;
using CertPlatform.Shared.Exceptions;

namespace CertPlatform.Admin.Services.Workflow
{
    /// <summary>
    /// 节点执行器 — 负责单个节点的执行逻辑
    /// <para>移植自：旧 NodeExecutor.cs（461 行）</para>
    /// <para>职责：</para>
    /// <para>1. 解析节点输入参数（constant: 前缀截取 / 引用上游节点输出）</para>
    /// <para>2. 按 nodeType 分发执行：start/docField/docTable/skill/branch/end/ai_node</para>
    /// <para>3. 返回统一的 NodeExecutionResult</para>
    /// <para>迁移改写：docField/docTable 从"模拟数据"升级为按 cert_extraction_result / cert_table_extraction_result 真实取数（D-4）</para>
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
        // ★ 2026-09-30 裁决 J1/J2/J4：取数唯一口径。⛔ docfield/doctable 不得再自己拼 WHERE。
        private readonly ExtractionDataResolver _resolver;

        public NodeExecutor(
            ISkillRegistry skillRegistry,
            AiNodeExecutor aiNodeExecutor,
            IDbOrm db,
            WorkflowLogger wfLogger,
            ILogger<NodeExecutor> logger,
            ExtractionDataResolver resolver)
        {
            _skillRegistry = skillRegistry;
            _aiNodeExecutor = aiNodeExecutor;
            _db = db;
            _wfLogger = wfLogger;
            _logger = logger;
            _resolver = resolver;
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
                        // ★ 特殊节点（2026-10-11 用户裁定）：配置期选「标准 + 阶段 + 标准文件」，
                        //   执行期的企业/标准/阶段由 start 注入的上下文提供 —— 普通 skill 表达不了。
                        "is_std_file_missing" => await ExecuteStdFileMissingAsync(node, taskCode, itemCode, contextParams, ct),
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
            catch (WorkflowDataMissingException ex)
            {
                // ★★★ 2026-09-30 语义异常【必须穿透】（裁决：执行期空值 ⇒ 自动失败 + 落缺口）
                //
                // ⚠️ 2026-10-11 后续裁定：docField / docTable **不再抛**本异常（改为返回空值，
                //    分流交给 is_empty + branch）。此 catch 分支**保留**，供后续特殊节点
                //    （如 is_std_file_missing）在「确实应自动失败」时复用。
                //
                //   为什么不能吞：WorkflowDataMissingException 带 8 个结构化属性
                //   （DataKind/DataCode/Reason/RuleCode/StandardFileCode/EnterpriseCode/
                //     StandardCode/StageCode）。一旦被下面的 catch(Exception) 捕获并
                //   压成 NodeExecutionResult.Error 字符串，上层就再也分不清
                //   「故意造的缺口」与「取数 bug」—— 补录夹具会失去信号（25 号 §6.3）。
                //
                //   失败分类码：DATA_MISSING（红·去补录） vs SYSTEM_ERROR（灰·报 bug）
                sw.Stop();
                _wfLogger.NodeFail(taskCode, itemCode, node.NodeId, ex.Message, (int)sw.ElapsedMilliseconds);
                throw;
            }
            catch (Exception ex)
            {
                sw.Stop();

                _wfLogger.NodeFail(taskCode, itemCode, node.NodeId, ex.Message, (int)sw.ElapsedMilliseconds);

                var failResult = NodeExecutionResult.Fail(ex.Message, (int)sw.ElapsedMilliseconds);
                failResult.ErrorCode = WorkflowErrorCodes.SystemError;
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
                // ★ targetPort 必须传下去 —— 上游输出是「多键字典」时靠它决定取哪个键
                //   （2026-10-11 用户裁定 A：确立 result 主输出约定）
                resolved[portName] = ResolveInputValue(rawValue, inputType, sharedOutputs, contextParams, portName);
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
        /// <param name="targetPort">本端口的名字 —— 上游输出为多键字典时，优先取同名键（见 <see cref="PickFromOutput"/>）</param>
        private object? ResolveInputValue(
            string? rawValue,
            string? inputType,
            Dictionary<string, object> sharedOutputs,
            Dictionary<string, object> contextParams,
            string? targetPort = null)
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
                        return PickFromOutput(outputDict, targetPort);
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
                    return PickFromOutput(outputDict2, targetPort);
                return output2;
            }

            // 4. 既不是 constant: 也不是已执行节点 → 作为原始字符串返回
            _logger.LogWarning("输入值 '{RawValue}' 无法解析为常量或已执行节点输出，作为原始字符串使用", rawValue);
            return rawValue;
        }

        /// <summary>
        /// 从上游节点的**输出字典**里取出「本端口真正要的那个值」。
        ///
        /// <para><b>为什么需要它</b>（★ 2026-10-11 用户裁定 A）：连线在
        /// <c>useWorkflowStore.connect()</c> 里只把**源节点 ID** 写进 <c>inputs[targetHandle]</c>
        /// （源端口名被丢弃），而所有 skill / docField / docTable 的输出都 ≥3 个键
        /// ⇒ 旧逻辑（<c>Count == 1</c> 才拆包）等于「**永远整包传**」，
        /// 下游 skill 拿到的是一个字典 ⇒ 例如 <c>docField → is_empty</c> 会被判成「有值」（错）。</para>
        ///
        /// <para><b>取值优先级</b>：</para>
        /// <list type="number">
        /// <item>目标端口名在字典里 → 取该键（最精确，如 <c>assembled_text</c> / <c>result</c>）</item>
        /// <item>字典有 <c>result</c> → 取 <c>result</c>（★ <b>项目事实上的「主输出」约定</b>：
        ///       <c>ExecuteBranch</c>、<c>WorkflowTestController</c>、<c>WorkflowInterpreter</c> 都认它）</item>
        /// <item>字典只有一个键 → 取唯一值（保留旧行为）</item>
        /// <item>都不满足 → 返回整个字典（兜底，⛔ 不再抛错）</item>
        /// </list>
        /// </summary>
        private static object? PickFromOutput(Dictionary<string, object> dict, string? targetPort)
        {
            if (dict == null || dict.Count == 0) return dict;

            if (!string.IsNullOrEmpty(targetPort) && dict.TryGetValue(targetPort, out var byPort))
                return byPort;

            if (dict.TryGetValue("result", out var byResult))
                return byResult;

            if (dict.Count == 1)
                return dict.Values.First();

            return dict;
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
        /// <para>数据源：cert_extraction_result（enterpriseCode 为空时默认虚拟企业常量）</para>
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
                enterpriseCode = YzhVirtualEnterprise.Code;
            var standardCode = contextParams?.GetValueOrDefault("standardCode")?.ToString() ?? "";
            // ★ 2026-09-30 修 B1：上下文键是 phaseCode，cert_extraction_result 的列是 StageCode。
            //   值口径已统一为 GUID（wf_execution_task.PhaseCode 已扩到 varchar(36），执行器改传 GUID）。
            var stageCode = contextParams?.GetValueOrDefault("phaseCode")?.ToString() ?? "";

            var source = enterpriseCode == YzhVirtualEnterprise.Code ? "sample_data" : "enterprise_doc";

            _wfLogger.DocFieldQuery(ruleCode, fieldCode, docType, enterpriseCode);

            if (string.IsNullOrEmpty(fieldCode))
                throw new InvalidOperationException($"docField 节点 {node.NodeId} 缺少 fieldCode 配置");

            // ★★ 取数唯一口径 = ExtractionDataResolver（2026-09-30 裁决 J1/J2/J4）。
            //   · 收窄键 (OrgCode, RuleCode, FieldCode)  ← RuleCode 取代 FileCode（B2 修复）
            //   · 人工值优先（ValueSource='manual'）        ← 裁决 J2
            //   · IsBlankText 判空（识别 - / 无 / 待填写…）  ← B3 修复
            //   ⛔ 不得在此另拼 WHERE —— 缺口生成（GapDetector）走同一个 Resolver，
            //     口径分叉会导致「清单说齐了、执行说缺了」。
            var field = await _resolver.GetFieldAsync(
                enterpriseCode, ruleCode, fieldCode, standardCode, stageCode);

            // ★★★ 2026-10-11 用户裁定：取不到【可用值】不再抛异常，改为**返回空值**。
            //
            //   为什么改（原为 2026-09-30「自动失败 + 落缺口」）：
            //   docField/docTable 是「**取数**」动作，判空是「**判断**」动作 —— 两者正交。
            //   抛异常 ⇒ 一次字段缺失就让**整个任务项 failed**，规则作者**无法**用 branch 分流
            //   （例如「字段空 ⇒ 判 NC 不合规」）。改为返回空值后：
            //     取数节点（docField/docTable）→ is_empty（判空 skill）→ branch（分流）
            //   ⚠️ 代价：WorkflowDataMissingException 不再抛出 ⇒ 页面「缺失必要数据 · 去补录」
            //      红徽标（DATA_MISSING）在 docField/docTable 路径上**不再出现**。
            //      异常类与其 catch 分支保留（is_std_file_missing 等特殊节点后续可能复用）。
            //
            //   ⛔⛔ 必须返回 **null**，不能透出 <c>field.RawValue</c>：
            //      StateIsEmpty 时 RawValue 是**占位符原文**（"-" / "待填写" / "N/A" …），
            //      而 IsEmptySkill 的判空口径**不认占位符** ⇒ 原样透出会让 is_empty 误判为「有值」。
            var hasValue = field.State == EmptyJudge.StateHasValue;
            var confidence = (double?)(field.Confidence ?? 0m) ?? 0d;
            // 有值 ⇒ 原值；无值（is_empty / not_found）⇒ null（供 is_empty 判定）
            object? fieldValue = hasValue ? (field.RawValue ?? string.Empty) : null;

            _wfLogger.DocFieldResult(field.RawValue, confidence, source);

            return new Dictionary<string, object>
            {
                ["fieldValue"] = fieldValue!,
                // ★ result = 主输出（2026-10-11 裁定 A）：连线只传节点 ID ⇒ 下游靠 result 取到「这一个值」
                ["result"]     = fieldValue!,
                // ★ 显式暴露三态，便于规则作者 / 排障区分「没提取记录」与「提取到了但为空」
                ["hasValue"]   = hasValue,
                ["dataState"]  = field.State,
                ["confidence"] = confidence,
                ["source"]     = source,
                // ★★ 溯源三元组（2026-09-30 B4 修复）
                //   WfExecutionTaskService:490-492 读的正是这三个键；
                //   修复前 NodeExecutor 只返回 source（值是 "enterprise_doc" 这类字符串，不是文件Code），
                //   导致 wf_node_execution.SourceFileCode/SourceFieldName/SourceVersion 长期为 NULL，
                //   ★ 连带后果：18 号 §3.5 的 C5 证据交叉校验形同虚设。
                ["sourceFileCode"]  = field.StandardFileCode ?? field.FileCode,
                ["sourceFieldName"] = field.FieldName ?? fieldCode,
                ["sourceVersion"]   = field.VersionNumber,
                // ★ 2026-09-30 补：值来源与命中行，供上层区分人工值 / 自动值
                ["valueSource"]     = field.ValueSource ?? ExtractionValueSource.Auto,
                ["resultCode"]      = field.ResultCode ?? string.Empty
            };
        }

        /// <summary>
        /// docTable 节点：按 tableCode + enterpriseCode 查询表格提取结果（真实取数，D-4）
        /// <para>数据源：cert_table_extraction_result（enterpriseCode 为空时默认虚拟企业常量）</para>
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
                enterpriseCode = YzhVirtualEnterprise.Code;
            var standardCode = contextParams?.GetValueOrDefault("standardCode")?.ToString() ?? "";
            var stageCode = contextParams?.GetValueOrDefault("phaseCode")?.ToString() ?? "";

            var source = enterpriseCode == YzhVirtualEnterprise.Code ? "sample_data" : "enterprise_doc";

            _wfLogger.DocTableQuery(ruleCode, tableCode, docType, enterpriseCode);

            if (string.IsNullOrEmpty(tableCode))
                throw new InvalidOperationException($"docTable 节点 {node.NodeId} 缺少 tableCode 配置");

            // ★★ 取数唯一口径 = ExtractionDataResolver（同 docField，裁决 J1/J2/J4）。
            //   · 收窄键 (OrgCode, RuleCode, TableCode)  ← RuleCode 取代 FileCode
            //   · 人工值优先（ValueSource='manual'）
            //   · ★ B3 修复：JSON 非空但【所有单元格都空】（实测 `[{"item":"","value":""}]`）也判 is_empty
            //   · ★ B6 修复：补 !IsDeleted（实体此前未声明该属性，但 DB 列本就存在）
            var table = await _resolver.GetTableAsync(
                enterpriseCode, ruleCode, tableCode, standardCode, stageCode);

            // ★★★ 2026-10-11 用户裁定：无记录 / 空表 ⇒ **返回空表**，⛔ 不再抛异常。
            //   口径与 docField 完全一致（取数与判空正交，分流交给 is_empty + branch）。
            //   ⛔⛔ 必须返回 **空列表 + rowCount=0**，不能透出 StateIsEmpty 时解析出的
            //      「全空单元格行」（实测 [{"item":"","value":""}]）—— 那样 is_empty 会判成「有值」。
            var hasValue = table.State == EmptyJudge.StateHasValue;
            var rows = hasValue ? ParseTableRows(table) : new List<object>();
            var confidence = hasValue ? ((double?)(table.Confidence ?? 0m) ?? 0d) : 0d;

            _wfLogger.DocTableResult(rows.Count, confidence, source);

            return new Dictionary<string, object>
            {
                ["rows"]      = rows,
                ["rowCount"]  = rows.Count,
                // ★ result = 主输出（2026-10-11 裁定 A）：连线只传节点 ID ⇒ 下游靠 result 取到「这个表格」
                ["result"]    = rows,
                // ★ 显式暴露三态，便于规则作者 / 排障区分「没提取记录」与「提取到了但为空」
                ["hasValue"]  = hasValue,
                ["dataState"] = table.State,
                ["confidence"] = confidence,
                ["source"]    = source,
                // ★★ 溯源三元组（2026-09-30 B4 修复）—— 同 docfield
                ["sourceFileCode"]  = table.StandardFileCode ?? table.FileCode,
                ["sourceFieldName"] = tableCode,   // 表格用 tableCode 做标识
                ["sourceVersion"]   = table.VersionNumber,
                // ★ 2026-09-30 补：值来源与命中行
                ["valueSource"]     = table.ValueSource ?? ExtractionValueSource.Auto,
                ["resultCode"]      = table.ResultCode ?? string.Empty
            };
        }

        /// <summary>
        /// Resolver 的已解析行 → 引擎用的 <c>List&lt;object&gt;</c>（保持下游 AI Prompt 模板兼容）。
        /// <para>★ 2026-09-30：解析逻辑下沉到 <see cref="ExtractionDataResolver.ParseRows"/>（唯一口径），
        /// 此处只做形状转换。</para>
        /// </summary>
        private static List<object> ParseTableRows(ExtractionDataResolver.TableValue table)
            => table.Rows.Select(r => (object)r).ToList();

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

        /// <summary>
        /// is_std_file_missing 节点：判断「企业是否缺少某个标准目录要求的文件」。
        ///
        /// <para><b>为什么是特殊节点而不是普通 skill</b>（2026-10-11 用户裁定）：
        /// 配置期必须能<b>按标准 + 阶段挑一个标准文件</b>（下拉选，⛔ 不是手输 GUID），
        /// 而执行期所需的 <c>enterprise_code</c> / <c>standard_code</c> / <c>stage_code</c>
        /// <b>配置期根本不知道</b>（由 start 节点在运行时注入）。
        /// 普通 skill 的参数只能来自画布连线或常量输入 ⇒ 表达不了这个语义。</para>
        ///
        /// <para><b>参数来源</b>：
        ///   · <c>standard_file_code</c> ← 节点 config（面板里选中的标准文件 = 模板行的 Code）
        ///   · <c>enterprise_code</c> ← start 注入的上下文</para>
        ///
        /// <para>⛔ <b>刻意不传 <c>standard_code</c> / <c>stage_code</c></b>：
        /// <see cref="IsStdFileMissingSkill"/> 里这两个参数是「<b>附加过滤</b>」语义
        /// （<c>IsNullOrWhiteSpace(x) || 相等</c>），而 <c>standard_file_code</c> 本身
        /// 已是模板行的 GUID（全局唯一）⇒ 二次过滤没有任何收益，反而一旦上下文的
        /// <c>standardCode</c> 口径（业务码 <c>iso9001</c> vs GUID）与表列不一致，
        /// 就会把<b>本来命中的行整批滤掉</b>，静默退化成 <c>not_configured</c>。
        /// 配置期的「标准 + 阶段」只用于<b>筛选下拉选项</b>，不参与执行期判定。</para>
        ///
        /// <para><b>输出</b>：沿用 <see cref="IsStdFileMissingSkill"/> 的四态
        /// （<c>uploaded</c> / <c>not_required</c> / <c>placeholder_only</c> / <c>not_configured</c>）。
        /// 其中 <c>result</c>(bool) 是<b>主输出</b> —— 可直接连 <c>branch</c> 的 <c>condition</c> 端口
        /// （<c>ExecuteBranch</c> 只认这个键）。</para>
        ///
        /// <para>⚠️ <c>not_configured</c> 时 <c>result=true</c>（视为「不可用」，保守）——
        /// 它表达的是「<b>标准目录没配这个文件要求</b>」，与「企业没交」是两回事，
        /// 界面上靠 <c>state</c> 区分，规则作者需要分流时按 <c>state</c> 判而不是 <c>result</c>。</para>
        /// </summary>
        private async Task<Dictionary<string, object>> ExecuteStdFileMissingAsync(
            WorkflowNodeConfig node,
            string taskCode,
            string itemCode,
            Dictionary<string, object> contextParams,
            CancellationToken ct)
        {
            const string skillCode = "is_std_file_missing";

            var standardFileCode = node.Config?.GetValueOrDefault("standardFileCode")?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(standardFileCode))
                throw new InvalidOperationException(
                    $"is_std_file_missing 节点 {node.NodeId} 缺少 standardFileCode 配置（请在右侧面板选择标准文件）");

            // ★ 与 docField / docTable 同口径：企业从上下文取，取不到回落虚拟企业（样例数据路径）
            var enterpriseCode = contextParams?.GetValueOrDefault("enterpriseCode")?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(enterpriseCode))
                enterpriseCode = YzhVirtualEnterprise.Code;

            // ⛔ 不传 standard_code / stage_code（见方法注释：二次过滤会静默滤掉正确行）
            var inputs = new Dictionary<string, object>
            {
                ["standard_file_code"] = standardFileCode,
                ["enterprise_code"]    = enterpriseCode
            };

            _wfLogger.SkillExec(skillCode, "ExecuteAsync");

            var context = new SkillContext
            {
                Inputs = inputs,
                WorkflowInstanceId = $"{taskCode}:{itemCode}",
                NodeId = node.NodeId
            };

            var skillResult = await _skillRegistry.ExecuteAsync(skillCode, context, ct);

            _wfLogger.SkillResult(skillResult.Success, skillResult.Outputs);

            if (!skillResult.Success)
                throw new InvalidOperationException($"Skill '{skillCode}' 执行失败: {skillResult.Error}");

            var output = new Dictionary<string, object>();
            foreach (var kv in skillResult.Outputs)
            {
                output[kv.Key] = kv.Value;
            }

            if (skillResult.Confidence.HasValue)
                output["confidence"] = skillResult.Confidence.Value;

            // ★ 主输出约定：result 必须是 bool，供 branch 直连（ExecuteBranch 只认这个键）
            if (!output.ContainsKey("result"))
                output["result"] = false;

            return output;
        }
    }
}

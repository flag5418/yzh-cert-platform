using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VOL.CERT.IServices.CertPlatform;
using VOL.CERT.Services.CertPlatform.WorkflowEngine.Models;
using VOL.Core.Extensions.AutofacManager;
using YZH.Core.AI.Clients;
using YZH.Core.AI.Clients.Models;
using YZH.Core.AI.Prompt;
using YZH.Core.Workflow;

namespace VOL.CERT.Services.CertPlatform.WorkflowEngine
{
    /// <summary>
    /// AI 节点执行器 — 负责 AI 节点的完整执行逻辑
    /// <para>文档：AI节点-详细设计-V1 §4</para>
    /// <para>职责：</para>
    /// <para>1. 解析自定义参数（link/skill/constant）</para>
    /// <para>2. 预执行数据获取型 Skill</para>
    /// <para>3. 渲染提示词模板</para>
    /// <para>4. 调用 LLM</para>
    /// <para>5. 按 outputType 转换结果</para>
    /// <para>6. 包装为标准输出格式</para>
    /// </summary>
    public class AiNodeExecutor
    {
        private readonly ILlmClient _llm;
        private readonly ISkillRegistry _skillRegistry;
        private readonly IPromptInterpreter _promptInterpreter;
        private readonly ILogger<AiNodeExecutor> _logger;

        public AiNodeExecutor(
            ILlmClient llm,
            ISkillRegistry skillRegistry,
            IPromptInterpreter promptInterpreter,
            ILogger<AiNodeExecutor> logger)
        {
            _llm = llm;
            _skillRegistry = skillRegistry;
            _promptInterpreter = promptInterpreter;
            _logger = logger;
        }

        /// <summary>
        /// 执行 AI 节点
        /// </summary>
        /// <param name="node">节点配置</param>
        /// <param name="taskCode">任务编码</param>
        /// <param name="itemCode">执行项编码</param>
        /// <param name="sharedOutputs">当前路径中已完成节点的输出</param>
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
                "[AI_START] taskCode={TaskCode}, itemCode={ItemCode}, nodeId={NodeId}, title={Title}",
                taskCode, itemCode, node.NodeId, node.Title);

            try
            {
                var config = node.Config ?? new();
                var template = config.GetValueOrDefault("promptTemplate")?.ToString() ?? "";
                var outputType = config.GetValueOrDefault("outputType")?.ToString() ?? "string";
                // 优先使用节点配置的模型；未配置时从系统参数 cert_sys_config.ai_model_name 读取（受控）
                var model = config.GetValueOrDefault("model")?.ToString();
                if (string.IsNullOrWhiteSpace(model))
                    model = GetActiveModelFromSysConfig();

                double temperature = 0.1;
                if (config.TryGetValue("temperature", out var tVal) &&
                    double.TryParse(tVal?.ToString(), out var tParsed))
                {
                    temperature = tParsed;
                }

                // ① 解析自定义参数（兼容旧 customParams 配置）
                var customParams = ParseCustomParams(node);
                var paramPool = new Dictionary<string, object>();

                foreach (var param in customParams)
                {
                    var value = await ResolveParamAsync(param, sharedOutputs, contextParams, taskCode, itemCode, ct);
                    paramPool[param.ParamName] = value;
                }

                // ①.bis 从模板 {{节点名.端口}} 自动提取引用（新方案：不再需要 customParams）
                ResolveTemplateRefs(template, sharedOutputs, paramPool);

                _logger.LogInformation(
                    "[AI_PARAMS] nodeId={NodeId}, params={Params}",
                    node.NodeId, JsonSerializer.Serialize(paramPool));

                // ② 渲染提示词
                var renderedPrompt = _promptInterpreter.Render(template, paramPool);

                _logger.LogInformation(
                    "[AI_PROMPT] nodeId={NodeId}, rendered={Rendered}",
                    node.NodeId, renderedPrompt);

                // ③ 构建 system prompt
                var systemPrompt = config.GetValueOrDefault("systemPrompt")?.ToString()
                    ?? "你是认证审核AI助手，只需输出结果，不要解释。";

                // ④ LLM 调用
                var response = await _llm.CompleteAsync(new LlmRequest
                {
                    Provider = "qwen",
                    Model = model,
                    Messages = new List<LlmMessage>
                    {
                        new() { Role = "system", Content = systemPrompt },
                        new() { Role = "user", Content = renderedPrompt }
                    },
                    Temperature = temperature,
                    MaxTokens = 4096,
                    JsonMode = false,
                    TimeoutSeconds = 120
                }, ct);

                if (!response.Success)
                {
                    // 区分超时 vs 其他错误
                    var errorMsg = response.Error ?? "未知错误";
                    if (errorMsg.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                        errorMsg.Contains("超时"))
                    {
                        throw new AiNodeException(
                            "LLM_TIMEOUT",
                            $"LLM 调用超时（120s），请稍后重试或换用更快模型。详情: {errorMsg}");
                    }
                    throw new AiNodeException(
                        "LLM_CALL_FAILED",
                        $"LLM 调用失败: {errorMsg}");
                }

                _logger.LogInformation(
                    "[AI_LLM_CALL] nodeId={NodeId}, model={Model}, tokens_in={TokensIn}, tokens_out={TokensOut}, durationMs={DurationMs}",
                    node.NodeId, model, response.PromptTokens, response.CompletionTokens, response.DurationMs);

                // ⑤ 类型转换
                var result = ConvertOutputType(response.Content, outputType);

                _logger.LogInformation(
                    "[AI_CONVERT] nodeId={NodeId}, raw={Raw}, converted={Converted}, targetType={TargetType}",
                    node.NodeId, response.Content, JsonSerializer.Serialize(result), outputType);

                // ⑥ 包装输出
                var output = new Dictionary<string, object>
                {
                    ["success"] = true,
                    ["error"] = null!,
                    ["result"] = result ?? new { }
                };

                sw.Stop();

                _logger.LogInformation(
                    "[AI_DONE] nodeId={NodeId}, result={Result}, durationMs={DurationMs}",
                    node.NodeId, JsonSerializer.Serialize(result), sw.ElapsedMilliseconds);

                return NodeExecutionResult.Ok(output, (int)sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                sw.Stop();

                // 提取错误码（如果是结构化异常）
                var errorCode = ex is AiNodeException aiEx ? aiEx.ErrorCode : "UNKNOWN";

                _logger.LogError(
                    "[AI_FAIL] nodeId={NodeId}, errorCode={ErrorCode}, error={Error}, durationMs={DurationMs}",
                    node.NodeId, errorCode, ex.Message, sw.ElapsedMilliseconds);

                // 返回带错误码的失败结果
                var failOutput = new Dictionary<string, object>
                {
                    ["success"] = false,
                    ["error"] = ex.Message,
                    ["errorCode"] = errorCode
                };
                return NodeExecutionResult.Fail(ex.Message, (int)sw.ElapsedMilliseconds, failOutput);
            }
        }

        /// <summary>
        /// 执行 AI 节点（测试模式）— 支持 mockOutputs
        /// </summary>
        public async Task<NodeExecutionResult> ExecuteWithMockAsync(
            WorkflowNodeConfig node,
            Dictionary<string, object> mockOutputs,
            Dictionary<string, object> contextParams,
            Dictionary<string, object>? precomputedParams,
            CancellationToken ct = default)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                var config = node.Config ?? new();
                var template = config.GetValueOrDefault("promptTemplate")?.ToString() ?? "";
                var outputType = config.GetValueOrDefault("outputType")?.ToString() ?? "string";
                // 优先使用节点配置的模型；未配置时从系统参数 cert_sys_config.ai_model_name 读取（受控）
                var model = config.GetValueOrDefault("model")?.ToString();
                if (string.IsNullOrWhiteSpace(model))
                    model = GetActiveModelFromSysConfig();

                double temperature = 0.1;
                if (config.TryGetValue("temperature", out var tVal) &&
                    double.TryParse(tVal?.ToString(), out var tParsed))
                {
                    temperature = tParsed;
                }

                // 解析自定义参数（兼容旧 customParams 配置）
                var customParams = ParseCustomParams(node);
                var paramPool = new Dictionary<string, object>();

                foreach (var param in customParams)
                {
                    if (precomputedParams != null && precomputedParams.TryGetValue(param.ParamName, out var preVal))
                    {
                        paramPool[param.ParamName] = preVal;
                        continue;
                    }
                    var value = await ResolveParamAsync(param, mockOutputs, contextParams, "TEST", "AI_NODE_TEST", ct);
                    paramPool[param.ParamName] = value;
                }

                // 从模板 {{节点名.端口}} 自动提取引用（新方案）
                ResolveTemplateRefs(template, mockOutputs, paramPool);

                // 渲染提示词
                var renderedPrompt = _promptInterpreter.Render(template, paramPool);

                // LLM 调用
                var systemPrompt = config.GetValueOrDefault("systemPrompt")?.ToString()
                    ?? "你是认证审核AI助手，只需输出结果，不要解释。";

                var response = await _llm.CompleteAsync(new LlmRequest
                {
                    Provider = "qwen",
                    Model = model,
                    Messages = new List<LlmMessage>
                    {
                        new() { Role = "system", Content = systemPrompt },
                        new() { Role = "user", Content = renderedPrompt }
                    },
                    Temperature = temperature,
                    MaxTokens = 4096,
                    JsonMode = false,
                    TimeoutSeconds = 120
                }, ct);

                if (!response.Success)
                {
                    // 区分超时 vs 其他错误
                    var errorMsg = response.Error ?? "未知错误";
                    if (errorMsg.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                        errorMsg.Contains("超时"))
                    {
                        throw new AiNodeException(
                            "LLM_TIMEOUT",
                            $"LLM 调用超时（120s），请稍后重试或换用更快模型。详情: {errorMsg}");
                    }
                    throw new AiNodeException(
                        "LLM_CALL_FAILED",
                        $"LLM 调用失败: {errorMsg}");
                }

                // 类型转换
                var result = ConvertOutputType(response.Content, outputType);

                sw.Stop();

                var output = new Dictionary<string, object>
                {
                    ["success"] = true,
                    ["error"] = null!,
                    ["result"] = result ?? new { }
                };

                return NodeExecutionResult.Ok(output, (int)sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                sw.Stop();

                var errorCode = ex is AiNodeException aiEx ? aiEx.ErrorCode : "UNKNOWN";

                _logger.LogError(
                    "[AI_FAIL] nodeId={NodeId}, errorCode={ErrorCode}, error={Error}, durationMs={DurationMs}",
                    node.NodeId, errorCode, ex.Message, sw.ElapsedMilliseconds);

                var failOutput = new Dictionary<string, object>
                {
                    ["success"] = false,
                    ["error"] = ex.Message,
                    ["errorCode"] = errorCode
                };
                return NodeExecutionResult.Fail(ex.Message, (int)sw.ElapsedMilliseconds, failOutput);
            }
        }

        // ── 私有方法 ──

        /// <summary>
        /// 从节点配置解析自定义参数
        /// </summary>
        private List<CustomParam> ParseCustomParams(WorkflowNodeConfig node)
        {
            var result = new List<CustomParam>();

            if (node.Config == null || !node.Config.TryGetValue("customParams", out var cpValue))
                return result;

            try
            {
                var json = cpValue.ToString();
                if (string.IsNullOrWhiteSpace(json)) return result;

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                var customParams = JsonSerializer.Deserialize<List<CustomParam>>(json, options);
                if (customParams != null) result = customParams;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "解析 customParams 失败");
            }

            return result;
        }

        /// <summary>
        /// 解析单个自定义参数
        /// </summary>
        private async Task<object> ResolveParamAsync(
            CustomParam param,
            Dictionary<string, object> sharedOutputs,
            Dictionary<string, object> contextParams,
            string taskCode,
            string itemCode,
            CancellationToken ct)
        {
            switch (param.SourceType)
            {
                case "link":
                    return ResolveLinkParam(param, sharedOutputs);

                case "skill":
                    return await ResolveSkillParamAsync(param, sharedOutputs, contextParams, taskCode, itemCode, ct);

                case "constant":
                    return param.SourceConfig?.GetValueOrDefault("value");

                default:
                    _logger.LogWarning("不支持的参数来源类型: {SourceType}", param.SourceType);
                    return null;
            }
        }

        /// <summary>
        /// 解析 link 类型参数（从节点输出读取）
        /// </summary>
        private object ResolveLinkParam(CustomParam param, Dictionary<string, object> sharedOutputs)
        {
            var nodeId = param.SourceConfig?.GetValueOrDefault("nodeId")?.ToString();
            var portName = param.SourceConfig?.GetValueOrDefault("portName")?.ToString();

            if (string.IsNullOrEmpty(nodeId))
                return null;

            if (!sharedOutputs.TryGetValue(nodeId, out var nodeOutput))
            {
                _logger.LogWarning("参数 '{ParamName}' 引用的节点 '{NodeId}' 输出不存在", param.ParamName, nodeId);
                return null;
            }

            if (nodeOutput is Dictionary<string, object> dict)
            {
                if (string.IsNullOrEmpty(portName))
                    return dict.GetValueOrDefault("result");

                return dict.GetValueOrDefault(portName);
            }

            return nodeOutput;
        }

        /// <summary>
        /// 解析 skill 类型参数（预执行 Skill）
        /// </summary>
        private async Task<object> ResolveSkillParamAsync(
            CustomParam param,
            Dictionary<string, object> sharedOutputs,
            Dictionary<string, object> contextParams,
            string taskCode,
            string itemCode,
            CancellationToken ct)
        {
            var skillCode = param.SourceConfig?.GetValueOrDefault("skillCode")?.ToString();
            if (string.IsNullOrEmpty(skillCode))
                return null;

            // 解析 Skill 参数
            var skillParams = new Dictionary<string, object>();
            if (param.SourceConfig.TryGetValue("skillParams", out var spValue) &&
                param.SourceConfig.TryGetValue("skillParamsTypes", out var sptValue))
            {
                var spDict = ConvertToDict(spValue);
                var sptDict = ConvertToDict(sptValue);

                if (spDict != null && sptDict != null)
                {
                    foreach (var (key, rawValue) in spDict)
                    {
                        var paramType = sptDict.GetValueOrDefault(key)?.ToString() ?? "constant";
                        var value = ResolveSkillParamValue(rawValue?.ToString(), paramType, sharedOutputs, contextParams);
                        skillParams[key] = value;
                    }
                }
            }

            _logger.LogInformation(
                "[AI_SKILL_PREEXEC] paramName={ParamName}, skillCode={SkillCode}, params={Params}",
                param.ParamName, skillCode, JsonSerializer.Serialize(skillParams));

            // 构造 SkillContext
            var context = new SkillContext
            {
                Inputs = skillParams ?? new Dictionary<string, object>(),
                WorkflowInstanceId = $"{taskCode}:{itemCode}",
                NodeId = param.ParamName,
                Logger = _logger as ILogger
            };

            // 执行 Skill
            var skillResult = await _skillRegistry.ExecuteAsync(skillCode, context, ct);

            if (!skillResult.Success)
            {
                _logger.LogWarning("参数 '{ParamName}' 的 Skill '{SkillCode}' 执行失败: {Error}",
                    param.ParamName, skillCode, skillResult.Error);
                throw new AiNodeException(
                    "SKILL_PREEXEC_FAILED",
                    $"参数 '{param.ParamName}' 的 Skill '{skillCode}' 执行失败: {skillResult.Error}");
            }

            // 从 Outputs 中取 result 或整体
            if (skillResult.Outputs != null && skillResult.Outputs.TryGetValue("result", out var result))
                return result;

            return skillResult.Outputs;
        }

        /// <summary>
        /// 解析 Skill 参数值
        /// </summary>
        private object ResolveSkillParamValue(
            string rawValue,
            string inputType,
            Dictionary<string, object> sharedOutputs,
            Dictionary<string, object> contextParams)
        {
            if (string.IsNullOrEmpty(rawValue))
                return null;

            if (string.Equals(inputType, "constant", StringComparison.OrdinalIgnoreCase))
            {
                if (rawValue.StartsWith("constant:", StringComparison.Ordinal))
                    return rawValue["constant:".Length..];
                return rawValue;
            }

            if (string.Equals(inputType, "link", StringComparison.OrdinalIgnoreCase))
            {
                if (contextParams != null && contextParams.TryGetValue(rawValue, out var ctxValue))
                    return ctxValue;

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
            }

            // 尝试从 contextParams 读取
            if (contextParams != null && contextParams.TryGetValue(rawValue, out var ctxValue2))
                return ctxValue2;

            // 尝试从 sharedOutputs 读取
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

            // 常量和回退逻辑
            if (rawValue.StartsWith("constant:", StringComparison.Ordinal))
                return rawValue["constant:".Length..];

            return rawValue;
        }

        /// <summary>
        /// 渲染提示词模板
        /// </summary>
        private string RenderTemplate(string template, Dictionary<string, object> paramPool)
        {
            if (string.IsNullOrEmpty(template)) return template;
            if (paramPool == null || paramPool.Count == 0) return template;

            var result = template;
            foreach (var (name, value) in paramPool)
            {
                var placeholder = $"{{{{{name}}}}}";
                var valueStr = value?.ToString() ?? string.Empty;
                result = result.Replace(placeholder, valueStr);
            }

            return result;
        }

        /// <summary>
        /// 按 outputType 转换结果
        /// </summary>
        private object ConvertOutputType(string content, string outputType)
        {
            var trimmed = content?.Trim() ?? string.Empty;

            return outputType.ToLowerInvariant() switch
            {
                "string" => trimmed,
                "number" => TryParseNumber(trimmed),
                "boolean" => TryParseBoolean(trimmed),
                "date" => TryParseDate(trimmed),
                _ => trimmed
            };
        }

        private static object TryParseNumber(string s)
        {
            // 尝试从文本中提取数字
            var digits = new string(s?.TakeWhile(c => char.IsDigit(c) || c == '.' || c == '-').ToArray());
            if (double.TryParse(digits, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                return d;

            if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d2))
                return d2;

            return s;
        }

        private static object TryParseBoolean(string s)
        {
            var lower = s?.ToLowerInvariant()?.Trim();
            if (lower == "true" || lower == "是" || lower == "yes" || lower == "1")
                return true;
            if (lower == "false" || lower == "否" || lower == "no" || lower == "0")
                return false;

            if (bool.TryParse(s, out var b))
                return b;

            return s;
        }

        private static object TryParseDate(string s)
        {
            if (DateTime.TryParse(s, out var dt))
                return dt.ToString("yyyy-MM-dd");

            return s;
        }

        /// <summary>
        /// 从模板中提取 {{节点名.端口}} 或 {{节点名}} 引用，自动从 sharedOutputs 中按节点标题/ID 查找输出值
        /// <para>新方案：前端不再需要配置 customParams，直接在提示词中写 {{文档字段.result}} 或 {{常量值}}</para>
        /// <para>sharedOutputs 同时以 nodeId 和 title 作为 key（由 WorkflowInterpreter 注入）</para>
        /// <para>匹配规则：</para>
        /// <para>  {{xxx.yyy}} → 在 sharedOutputs 中查找 key=xxx，再取 yyy 端口的值</para>
        /// <para>  {{xxx}} → 在 sharedOutputs 中查找 key=xxx，直接取值（常量节点或单端口节点）</para>
        /// </summary>
        private void ResolveTemplateRefs(
            string template,
            Dictionary<string, object> sharedOutputs,
            Dictionary<string, object> paramPool)
        {
            if (string.IsNullOrEmpty(template) || sharedOutputs == null || sharedOutputs.Count == 0)
                return;

            // 匹配 {{xxx.yyy}} 或 {{xxx}} 格式（支持中文、字母、数字、下划线、点号）
            // 与 PromptInterpreter 的正则保持一致
            var refRegex = new System.Text.RegularExpressions.Regex(
                @"\{\{([\w\u4e00-\u9fff][\w\u4e00-\u9fff.]*)\}\}");

            var matches = refRegex.Matches(template);
            foreach (System.Text.RegularExpressions.Match m in matches)
            {
                var fullKey = m.Groups[1].Value;  // 完整 key（如 "文档字段.result" 或 "常量值"）

                // 已在 paramPool 中的跳过（customParams 优先）
                if (paramPool.ContainsKey(fullKey))
                    continue;

                // 判断是否含点号：有点号 → refKey.portName；无点号 → 整体作为 refKey
                var lastDot = fullKey.LastIndexOf('.');
                string refKey, portName;
                if (lastDot >= 0)
                {
                    refKey = fullKey[..lastDot];
                    portName = fullKey[(lastDot + 1)..];
                }
                else
                {
                    refKey = fullKey;
                    portName = string.Empty;
                }

                // 在 sharedOutputs 中查找（key 可能是 nodeId 或 title）
                if (!sharedOutputs.TryGetValue(refKey, out var nodeOutput))
                {
                    _logger.LogWarning("[AI_TEMPLATE_REF] 未找到引用 '{RefKey}'，可用 keys: {Keys}",
                        refKey, string.Join(", ", sharedOutputs.Keys));
                    continue;
                }

                // 提取端口值
                object? resolved;
                if (!string.IsNullOrEmpty(portName))
                {
                    resolved = ExtractPortValue(nodeOutput, portName);
                }
                else
                {
                    // 无端口名 → 直接取值或提取默认 result
                    resolved = nodeOutput is Dictionary<string, object> dict
                        ? ExtractPortValue(dict, "result")
                        : nodeOutput;
                }

                if (resolved != null)
                {
                    paramPool[fullKey] = resolved;
                    _logger.LogDebug("[AI_TEMPLATE_REF] resolved {{{{{FullKey}}}}} = {Value}",
                        fullKey, resolved);
                }
            }
        }

        /// <summary>
        /// 从节点输出中提取指定端口的值
        /// </summary>
        private static object? ExtractPortValue(object nodeOutput, string portName)
        {
            if (nodeOutput is Dictionary<string, object> dict)
            {
                // 优先按端口名取
                if (dict.TryGetValue(portName, out var val))
                    return val;
                // 回退到 result
                if (dict.TryGetValue("result", out var result))
                    return result;
                // 如果只有一个值，直接返回
                if (dict.Count == 1)
                    return dict.Values.First();
                return dict;
            }
            // 非 dict 直接返回（适用于 result 本身就是简单值的情况）
            if (portName == "result")
                return nodeOutput;
            return nodeOutput;
        }

        private static Dictionary<string, object>? ConvertToDict(object value)
        {
            if (value is Dictionary<string, object> dict) return dict;
            if (value is JsonElement je && je.ValueKind == JsonValueKind.Object)
            {
                var result = new Dictionary<string, object>();
                foreach (var prop in je.EnumerateObject())
                {
                    result[prop.Name] = prop.Value.ValueKind switch
                    {
                        JsonValueKind.String => (object)(prop.Value.GetString() ?? string.Empty),
                        JsonValueKind.Number => prop.Value.GetDouble(),
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        _ => prop.Value.GetRawText()
                    };
                }
                return result;
            }

            try
            {
                var json = value?.ToString();
                if (string.IsNullOrEmpty(json)) return null;
                return JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 从系统参数 cert_sys_config 读取当前生效的 AI 模型名。
        /// 唯一真相源：ai_model_name 参数。未配置时兜底 qwen-turbo。
        /// </summary>
        private static string GetActiveModelFromSysConfig()
        {
            try
            {
                var sysConfig = AutofacContainerModule.GetService<ISysConfigService>();
                var model = sysConfig?.Get("ai_model_name");
                if (!string.IsNullOrWhiteSpace(model))
                    return model;
            }
            catch (Exception ex)
            {
                Microsoft.Extensions.Logging.LoggerFactory.Create(b => { })
                    .CreateLogger<AiNodeExecutor>()
                    .LogWarning("[GetActiveModel] 读取系统参数失败，使用默认值: {Msg}", ex.Message);
            }
            return "qwen-turbo";
        }
    }
}

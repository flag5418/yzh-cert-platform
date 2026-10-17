
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SqlSugar;
using CertPlatform.Shared.Workflow.Models;
using CertPlatform.Admin.Services.Workflow.Skills;
using YZH.Core.Api.Models.System;
using YZH.Core.DataBase.Interfaces;
using CertPlatform.Shared.DocExtraction;
using CertPlatform.Shared.Exceptions;

namespace CertPlatform.Admin.Services.Workflow
{
    /// <summary>
    /// AI 节点执行器 — 负责 AI 节点的完整执行逻辑
    /// <para>移植自：旧 AiNodeExecutor.cs（755 行）</para>
    /// <para>职责：</para>
    /// <para>1. 解析自定义参数（link/skill/constant）</para>
    /// <para>2. 预执行数据获取型 Skill</para>
    /// <para>3. 渲染提示词模板（含 {{节点名.端口}} 自动引用）</para>
    /// <para>4. 调用 LLM（迁移改写：LlmInvokeService OpenAI 兼容直连）</para>
    /// <para>5. 按 outputType 转换结果</para>
    /// <para>6. 包装为标准输出格式</para>
    /// </summary>
    public class AiNodeExecutor
    {
        private readonly CertPlatform.Shared.DocExtraction.LlmInvokeService _llm;
        private readonly ISkillRegistry _skillRegistry;
        private readonly IDbOrm _db;
        private readonly ILogger<AiNodeExecutor> _logger;

        /// <summary>模型配置缓存（任务执行内复用，避免每个 AI 节点重复查库）</summary>
        private AiSettings? _cachedSettings;

        public AiNodeExecutor(
            CertPlatform.Shared.DocExtraction.LlmInvokeService llm,
            ISkillRegistry skillRegistry,
            IDbOrm db,
            ILogger<AiNodeExecutor> logger)
        {
            _llm = llm;
            _skillRegistry = skillRegistry;
            _db = db;
            _logger = logger;
        }

        /// <summary>AI 连接配置（cert_sys_config 六键，对照 DocExtractionRuleService.AI.cs GetAiSettingsAsync）</summary>
        private class AiSettings
        {
            public string ApiKey { get; set; } = "";
            public string BaseUrl { get; set; } = "https://dashscope.aliyuncs.com/compatible-mode/v1";
            public string Model { get; set; } = "qwen-turbo";
            public int MaxTokens { get; set; } = 4096;
            public float Temperature { get; set; } = 0.7f;
        }

        /// <summary>
        /// 从 cert_sys_config 读取 AI 设置（Category='ai_model' 六键）
        /// </summary>
        private async Task<AiSettings> GetAiSettingsAsync(CancellationToken ct)
        {
            if (_cachedSettings != null) return _cachedSettings;

            var s = new AiSettings();
            try
            {
                var rows = await _db.Client.Queryable<SysConfig>()
                    .Where(x => x.Category == "ai_model" && !x.IsDeleted)
                    .Select(x => new ConfigKV { ConfigKey = x.ConfigKey, ConfigValue = x.ConfigValue })
                    .ToListAsync();
                foreach (var row in rows)
                {
                    switch (row.ConfigKey)
                    {
                        case "ai_api_key": s.ApiKey = row.ConfigValue ?? ""; break;
                        case "ai_base_url": s.BaseUrl = row.ConfigValue ?? s.BaseUrl; break;
                        case "ai_model_name": s.Model = row.ConfigValue ?? s.Model; break;
                        case "ai_max_tokens":
                            if (int.TryParse(row.ConfigValue, out var mt)) s.MaxTokens = mt;
                            break;
                        case "ai_temperature":
                            if (float.TryParse(row.ConfigValue, out var tp)) s.Temperature = tp;
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[AI_SETTINGS] 读取 AI 配置失败，使用默认值: {Msg}", ex.Message);
            }

            _cachedSettings = s;
            return s;
        }

        private class ConfigKV
        {
            public string ConfigKey { get; set; } = "";
            public string? ConfigValue { get; set; }
        }

        /// <summary>
        /// 执行 AI 节点（工作流运行时）
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
                var template = config.GetConfigValue("PromptTemplate")?.ToString() ?? "";
                var outputType = config.GetConfigValue("OutputType")?.ToString() ?? "string";

                // ① 解析参数：★ 新方案 `config.params`（权威）+ 兼容旧 `customParams`（兜底）
                //   ⚠️ 两条路径（真实执行 / 配置期测试）共用同一个 FillParamPoolAsync ——
                //      D2 的教训就是「同一逻辑两处实现」，⛔ 不许再分叉。
                var paramPool = new Dictionary<string, object>();
                await FillParamPoolAsync(node, paramPool, sharedOutputs, contextParams,
                    precomputedParams: null, taskCode, itemCode, ct);

                // ①.bis 从模板 {{节点名.端口}} 自动提取引用（新方案：不再需要 customParams）
                //
                // ★★★ 2026-10-12 用户裁定（第 1 条 + 第 4 条）：<b>上游为空必须中断，⛔ 不能静默送 LLM</b>。
                //
                //   修复前：resolved == null 时只留一条 Debug 日志（生产环境看不到），
                //   提示词里的 {{文档字段.result}} <b>没有被替换</b>，带着占位符原文直接送去问大模型，
                //   节点<b>照常返回成功</b> ⇒ 最典型的「静默产出错误结论」。
                //   用户原话：「ai节点，需要一个上游的结果，结果上游什么都没有输出给ai节点，
                //             ai没办法获取任何参数，提示词没办法跑通」。
                //
                //   现在：有任何一个引用解析不出来（节点输出不存在 / 值为 null）⇒ 抛
                //   WorkflowDataMissingException ⇒ <b>全局中断当前检查项</b> + 落补录缺口 + 标「不可判定」。
                //   ⚠️ 与 compare 等 skill 的区别：这里必须用 DataMissing（而非 PRECONDITION_EMPTY），
                //      因为 AI 节点的入参**就是上游节点输出**，缺的必然是「数据」⇒ 走补录通道才修得好。
                //
                //   ⚠️ <b>只在真实执行路径生效</b>：配置期测试（ExecuteWithMockAsync）不校验 ——
                //      搭图阶段上游本来就可能是空的，那时要求补齐会让 AI 节点根本没法测。
                var unresolvedRefs = ResolveTemplateRefs(template, sharedOutputs, paramPool);
                if (unresolvedRefs.Count > 0)
                {
                    throw new WorkflowDataMissingException(
                        dataKind: "ai_param",
                        dataCode: string.Join(", ", unresolvedRefs),
                        reason: "empty",
                        message:
                            $"AI 节点「{node.Title}」的上游参数未解析：{string.Join("、", unresolvedRefs)}" +
                            " —— 提示词无法跑通，已中断当前检查项" +
                            "（请补录数据，或检查上游连线/节点输出端口是否正确）",
                        standardFileCode: null,
                        ruleCode: null,
                        enterpriseCode: contextParams?.GetValueOrDefault(WorkflowContextKeys.EnterpriseCode)?.ToString(),
                        standardCode: contextParams?.GetValueOrDefault(WorkflowContextKeys.StandardCode)?.ToString(),
                        stageCode: contextParams?.GetValueOrDefault(WorkflowContextKeys.PhaseCode)?.ToString());
                }

                _logger.LogInformation(
                    "[AI_PARAMS] nodeId={NodeId}, params={Params}",
                    node.NodeId, JsonSerializer.Serialize(paramPool));

                // ② 渲染提示词（★ 第 3 批「注释式」，与配置期测试共用 RenderPromptForNode —— 单点实现）
                var rendered = RenderPromptForNode(node, paramPool, out var paramSection);

                // §2.4 渲染自检（断言，非兜底）：正文里不许再有 {{...}}。
                //   正常不可达（上面 unresolvedRefs 已按同源清单抛过），留着是为了让契约有代码背书。
                if (rendered.ResidualRefs.Count > 0)
                {
                    throw new WorkflowDataMissingException(
                        dataKind: "ai_param",
                        dataCode: string.Join(", ", rendered.ResidualRefs),
                        reason: "empty",
                        message:
                            $"AI 节点「{node.Title}」渲染后仍有占位符残留：{string.Join("、", rendered.ResidualRefs)}" +
                            " —— 提示词无法跑通，已中断当前检查项",
                        standardFileCode: null,
                        ruleCode: null,
                        enterpriseCode: contextParams?.GetValueOrDefault(WorkflowContextKeys.EnterpriseCode)?.ToString(),
                        standardCode: contextParams?.GetValueOrDefault(WorkflowContextKeys.StandardCode)?.ToString(),
                        stageCode: contextParams?.GetValueOrDefault(WorkflowContextKeys.PhaseCode)?.ToString());
                }

                var renderedPrompt = rendered.Body;

                _logger.LogInformation(
                    "[AI_PROMPT] nodeId={NodeId}, rendered={Rendered}",
                    node.NodeId, renderedPrompt);

                // ③④ LLM 调用（参数区走 system 侧独立段 —— 与任务正文分属不同角色，§2.3 判据②）
                var swLlm = Stopwatch.StartNew();
                var (success, error, content, result, promptTokens, completionTokens, llmDurationMs) =
                    await CallLlmAsync(node, renderedPrompt, paramSection, ct);
                swLlm.Stop();

                if (!success)
                {
                    // 区分超时 vs 其他错误
                    if (error != null && (error.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                        error.Contains("超时")))
                    {
                        throw new AiNodeException(
                            "LLM_TIMEOUT",
                            $"LLM 调用超时，请稍后重试或换用更快模型。详情: {error}");
                    }
                    throw new AiNodeException("LLM_CALL_FAILED", $"LLM 调用失败: {error}");
                }

                // ⑤ 类型转换（json ⇒ 剥围栏 + 解析 + outputSchema 校验，与测试路径共用单点实现）
                var converted = ConvertAndValidateOutput(config, content, outputType, node.NodeId);

                _logger.LogInformation(
                    "[AI_CONVERT] nodeId={NodeId}, raw={Raw}, converted={Converted}, targetType={TargetType}",
                    node.NodeId, content, JsonSerializer.Serialize(converted), outputType);

                // ⑥ 包装输出
                var output = new Dictionary<string, object>
                {
                    ["success"] = true,
                    ["error"] = null!,
                    ["result"] = converted ?? new { }
                };

                sw.Stop();

                _logger.LogInformation(
                    "[AI_DONE] nodeId={NodeId}, result={Result}, durationMs={DurationMs}, llmMs={LlmMs}, tokens={P}/{C}",
                    node.NodeId, JsonSerializer.Serialize(converted), sw.ElapsedMilliseconds, llmDurationMs, promptTokens, completionTokens);

                var okResult = NodeExecutionResult.Ok(output, (int)sw.ElapsedMilliseconds);
                okResult.PromptTokens = promptTokens;
                okResult.CompletionTokens = completionTokens;
                okResult.LlmDurationMs = llmDurationMs;
                okResult.AiModel = _cachedSettings?.Model;
                // 诊断面 = 模型真正看到的全部内容（正文 + system 侧参数区），⛔ 不要只回传正文
                okResult.AiPrompt = CombinePromptForDisplay(renderedPrompt, paramSection);
                // ★ 2026-10-14：参数池与未解析清单**由引擎唯一产出**，供测试页 Debug 原样回传。
                //   ⛔ 不要再让控制器自己重算一遍 —— 那是「诊断面说谎」的根因（D2）。
                okResult.AiParamPool = paramPool;
                okResult.AiUnresolvedRefs = unresolvedRefs;
                return okResult;
            }
            catch (WorkflowDataMissingException)
            {
                // ★★★ 2026-10-12：语义异常【必须穿透】—— 与 NodeExecutor 同一纪律。
                //   它带 8 个结构化属性（DataKind/DataCode/Reason/RuleCode/作用域四元组），
                //   一旦被下面的 catch(Exception) 压成一句 error 字符串，
                //   上层就再也分不清「故意造的缺口」与「取数 bug」—— 补录夹具会失去信号。
                sw.Stop();
                throw;
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
        /// 执行 AI 节点（测试模式）— 支持 mockOutputs（前端 test/ai-node 契约）
        /// </summary>
        public async Task<NodeExecutionResult> ExecuteWithMockAsync(
            WorkflowNodeConfig node,
            Dictionary<string, object> mockOutputs,
            Dictionary<string, object> contextParams,
            Dictionary<string, object>? precomputedParams,
            CancellationToken ct = default)
        {
            var sw = Stopwatch.StartNew();

            // ★ 2026-10-14：这三个变量**提到 try 之外** —— 失败分支（catch）同样必须回传
            //   「渲染到哪一步、还差哪些引用」。否则 LLM 调用失败时，测试页只会显示一句 error，
            //   恰恰在最需要「渲染后提示词 + 缺失清单」的场景里什么都看不到。
            var paramPool = new Dictionary<string, object>();
            var unresolvedInTest = new List<string>();
            var renderedPrompt = string.Empty;
            var paramSection = string.Empty;

            try
            {
                var config = node.Config ?? new();
                var template = config.GetConfigValue("PromptTemplate")?.ToString() ?? "";
                var outputType = config.GetConfigValue("OutputType")?.ToString() ?? "string";

                // ① 解析参数：★ 新方案 `config.params`（权威）+ 兼容旧 `customParams`（兜底）
                //   与真实执行路径共用 FillParamPoolAsync（⛔ 不许分叉，见 D2）
                await FillParamPoolAsync(node, paramPool, mockOutputs, contextParams,
                    precomputedParams, "TEST", "AI_NODE_TEST", ct);

                // 从模板 {{节点名.端口}} 自动提取引用（新方案）
                //
                // ⚠️ 测试模式**刻意不校验**未解析引用（与真实执行路径不同）：
                //    搭图阶段上游节点本来就可能是空的（例如还没上传企业文件 ⇒ docField 返回空），
                //    若这里也中断，AI 节点在配置期就<b>根本没法测</b>。
                //
                //    ★ 2026-10-14 用户裁决：**「不中断」不等于「不说」** —— 原实现只写一条 LogWarning，
                //      界面上完全看不见，用户以为提示词没问题。现在把清单挂到 NodeExecutionResult
                //      的 AiUnresolvedRefs 上，由测试接口原样回传前端显式展示（红字列出缺失参数）。
                unresolvedInTest = ResolveTemplateRefs(template, mockOutputs, paramPool);
                if (unresolvedInTest.Count > 0)
                {
                    _logger.LogWarning(
                        "[AI_TEST_UNRESOLVED] nodeId={NodeId} 未解析的模板引用: {Refs}（测试模式不中断）",
                        node.NodeId, string.Join(", ", unresolvedInTest));
                }

                // 渲染提示词（★ 注释式，与真实执行共用 RenderPromptForNode —— 单点实现，不许分叉）
                var rendered = RenderPromptForNode(node, paramPool, out paramSection);
                // §2.4 渲染自检：测试路径**不中断**，但残留在「未解析清单」里必须显式可见
                if (rendered.ResidualRefs.Count > 0)
                {
                    foreach (var r in rendered.ResidualRefs)
                        if (!unresolvedInTest.Contains(r)) unresolvedInTest.Add(r);
                }
                renderedPrompt = rendered.Body;

                // LLM 调用（参数区走 system 侧独立段）
                var (success, error, content, _, promptTokens, completionTokens, llmDurationMs) =
                    await CallLlmAsync(node, renderedPrompt, paramSection, ct);

                if (!success)
                {
                    if (error != null && (error.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                        error.Contains("超时")))
                    {
                        throw new AiNodeException(
                            "LLM_TIMEOUT",
                            $"LLM 调用超时，请稍后重试或换用更快模型。详情: {error}");
                    }
                    throw new AiNodeException("LLM_CALL_FAILED", $"LLM 调用失败: {error}");
                }

                // 类型转换（json ⇒ 剥围栏 + 解析 + outputSchema 校验，与真实路径共用单点实现）
                var result = ConvertAndValidateOutput(config, content, outputType, node.NodeId);

                sw.Stop();

                var output = new Dictionary<string, object>
                {
                    ["success"] = true,
                    ["error"] = null!,
                    ["result"] = result ?? new { }
                };

                var testOkResult = NodeExecutionResult.Ok(output, (int)sw.ElapsedMilliseconds);
                testOkResult.PromptTokens = promptTokens;
                testOkResult.CompletionTokens = completionTokens;
                testOkResult.LlmDurationMs = llmDurationMs;
                testOkResult.AiModel = _cachedSettings?.Model;
                testOkResult.AiPrompt = CombinePromptForDisplay(renderedPrompt, paramSection);
                testOkResult.AiParamPool = paramPool;
                testOkResult.AiUnresolvedRefs = unresolvedInTest;
                return testOkResult;
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
                var failResult = NodeExecutionResult.Fail(ex.Message, (int)sw.ElapsedMilliseconds, failOutput);
                // ★ 2026-10-14：失败也要带上「渲染到哪一步」的事实 —— 这正是排查最需要的信息。
                failResult.AiPrompt = CombinePromptForDisplay(renderedPrompt, paramSection);
                failResult.AiParamPool = paramPool;
                failResult.AiUnresolvedRefs = unresolvedInTest;
                return failResult;
            }
        }

        // ── LLM 调用（迁移改写核心：LlmInvokeService 直连） ──

        /// <summary>
        /// ★ 注释式渲染的<b>唯一</b>入口（第 3 批 §2.3 + §2.4）—— 真实执行与配置期测试都走这里，
        /// ⛔ 不许各自拼模板（D2 的根因就是同一逻辑两处实现）。
        ///
        /// <para>渲染出两段：</para>
        /// <list type="bullet">
        /// <item><b>正文</b>（返回值.Body）→ <c>user</c> 侧任务描述：已声明参数只留<b>参数名</b></item>
        /// <item><b>参数区</b>（<paramref name="paramSection"/>, out）→ <c>system</c> 侧独立段：
        ///   <c>【参数说明】p1（标签）= 值【参数说明结束】</c></item>
        /// </list>
        ///
        /// <para><b>自检（§2.4）</b>：① 正文零残留 <c>{{...}}</c>（由调用方断言）；
        /// ② 参数区条目数 == <c>config.params</c> 条数（此处 LogError）。</para>
        /// </summary>
        private PromptRenderer.CommentRenderResult RenderPromptForNode(
            WorkflowNodeConfig node,
            Dictionary<string, object> paramPool,
            out string paramSection)
        {
            var template = node.GetConfig(AiParams.PromptKey)?.ToString() ?? "";
            var decls = AiParams.ParseDeclaredParams(node);

            var rendered = PromptRenderer.RenderCommentStyle(template, paramPool, decls);
            paramSection = rendered.ParamSection;

            if (rendered.SectionEntryCount != decls.Count)
            {
                _logger.LogError(
                    "[AI_PARAM_SECTION] 参数区条目数与 params 条数不一致 nodeId={NodeId}, sectionEntries={Section}, params={Params}",
                    node.NodeId, rendered.SectionEntryCount, decls.Count);
            }

            if (decls.Count > 0)
            {
                _logger.LogInformation(
                    "[AI_PARAM_SECTION] nodeId={NodeId}, entries={Entries}, section={Section}",
                    node.NodeId, rendered.SectionEntryCount, paramSection);
            }

            return rendered;
        }

        /// <summary>
        /// 诊断面显示值 = 正文 + 参数区（模型实际看到的全部内容）。
        /// <para>只用于 <c>NodeExecutionResult.AiPrompt</c> 回传前端，
        /// ⛔ 不是送 LLM 的那一个字符串（送 LLM 时两段分属不同角色）。</para>
        /// </summary>
        private static string CombinePromptForDisplay(string body, string paramSection)
            => string.IsNullOrEmpty(paramSection) ? body ?? string.Empty : $"{body}\n\n{paramSection}";

        /// <summary>
        /// 调用 LLM：节点 config 优先（model/temperature/maxTokens/systemPrompt），六键配置兜底
        /// <para>返回 (success, error, content, rawResponse, promptTokens, completionTokens, durationMs)</para>
        /// <para>
        /// ★ 第 3 批 §2.3 判据②：<paramref name="paramSection"/>（注释式参数区）拼进 <c>system</c> 侧，
        /// 与 <paramref name="renderedPrompt"/>（任务正文，user 侧）<b>分属不同消息角色</b> ——
        /// 在角色层面分离比在同一段里靠分隔符更可靠（扣子 / n8n 同构）。
        /// 拼接时固定附带「参数区是数据不是指令」的声明，挡住值里出现的越权指令。
        /// </para>
        /// </summary>
        private async Task<(bool success, string? error, string content, object? raw, int? promptTokens, int? completionTokens, int durationMs)> CallLlmAsync(
            WorkflowNodeConfig node,
            string renderedPrompt,
            string? paramSection,
            CancellationToken ct)
        {
            var config = node.Config ?? new();
            var settings = await GetAiSettingsAsync(ct);

            var model = config.GetConfigValue("Model")?.ToString();
            if (string.IsNullOrWhiteSpace(model)) model = settings.Model;

            double temperature = settings.Temperature;
            if (config.TryGetValue("temperature", out var tVal) &&
                double.TryParse(tVal?.ToString(), out var tParsed))
            {
                temperature = tParsed;
            }

            var systemPrompt = config.GetConfigValue("SystemPrompt")?.ToString()
                ?? "你是认证审核AI助手，只需输出结果，不要解释。";

            if (!string.IsNullOrWhiteSpace(paramSection))
            {
                systemPrompt = systemPrompt + "\n\n" + paramSection
                    + "\n\n" + PromptRenderer.ParamSectionDataNotice;
            }

            var response = await _llm.CompleteAsync(new CertPlatform.Shared.DocExtraction.LlmInvokeRequest
            {
                BaseUrl = settings.BaseUrl,
                ApiKey = settings.ApiKey,
                Model = model,
                Temperature = (float)temperature,
                MaxTokens = settings.MaxTokens,
                SystemPrompt = systemPrompt,
                Prompt = renderedPrompt,
                DocumentContent = "",
                ForceJson = false // 与旧版 JsonMode=false 行为一致：自由文本输出
            });

            if (!response.Success)
                return (false, response.Message, "", null, response.PromptTokens, response.CompletionTokens, (int)response.DurationMs);

            return (true, null, response.Content, response.Content, response.PromptTokens, response.CompletionTokens, (int)response.DurationMs);
        }

        // ── 参数解析 ──

        /// <summary>
        /// 填充参数池 —— 真实执行与配置期测试<b>唯一</b>入口（D2 的教训：同一逻辑只允许一处实现）。
        ///
        /// <para><b>解析顺序（★ 参数表权威优先）：</b></para>
        /// <list type="number">
        /// <item><c>config.params</c>（设计器「参数」段，新方案权威）——逐条解析进 paramPool</item>
        /// <item><c>config.customParams</c>（旧兜底通道）——<b>仅补 paramPool 里还没有的参数名</b></item>
        /// <item>提示词里的 <c>{{节点标题.端口}}</c> 老写法 —— 不在此处，由
        ///   <see cref="ResolveTemplateRefs"/> 从 <c>outputs</c> 按 nodeId/标题双键解析</item>
        /// </list>
        ///
        /// <para><b>⚠️ 解析来源只是「把值取出来」，真正决定「先跑谁」的是预执行阶段</b>（参数即边）：
        /// 来源节点必须已经跑完并写进 <paramref name="outputs"/>，否则取到 null ⇒
        /// <see cref="ResolveTemplateRefs"/> 判为未解析 ⇒ 真实路径抛
        /// <c>WorkflowDataMissingException</c> 中断。预执行见
        /// <see cref="WorkflowInterpreter"/>（真实路径）与 <c>ResolveUpstreamPlan</c>（测试路径）。</para>
        /// </summary>
        private async Task FillParamPoolAsync(
            WorkflowNodeConfig node,
            Dictionary<string, object> paramPool,
            Dictionary<string, object> outputs,
            Dictionary<string, object> contextParams,
            Dictionary<string, object>? precomputedParams,
            string taskCode,
            string itemCode,
            CancellationToken ct)
        {
            // ① 参数表（权威）
            foreach (var decl in AiParams.ParseDeclaredParams(node))
            {
                if (precomputedParams != null && precomputedParams.TryGetValue(decl.Name, out var preVal))
                {
                    paramPool[decl.Name] = preVal;
                    continue;
                }

                var value = await ResolveParamAsync(
                    ToCustomParam(decl), outputs, contextParams, taskCode, itemCode, ct);

                paramPool[decl.Name] = value!;
            }

            // ② 旧 customParams 兜底（只补缺，不覆盖参数表）
            foreach (var param in ParseCustomParams(node))
            {
                if (paramPool.ContainsKey(param.ParamName)) continue;

                if (precomputedParams != null && precomputedParams.TryGetValue(param.ParamName, out var preVal))
                {
                    paramPool[param.ParamName] = preVal;
                    continue;
                }

                var value = await ResolveParamAsync(param, outputs, contextParams, taskCode, itemCode, ct);
                paramPool[param.ParamName] = value!;
            }
        }

        /// <summary>
        /// 参数表声明 → 旧 <see cref="CustomParam"/> 形状，从而复用 <see cref="ResolveParamAsync"/> 的
        /// link / skill / constant 三条解析分支（⛔ 不复制实现）。
        /// <para><c>sourceType=node</c>（设计器「参数」段的写法）= 旧模型的 <c>link</c>。</para>
        /// </summary>
        private static CustomParam ToCustomParam(AiParamDecl decl) => new()
        {
            ParamName = decl.Name,
            SourceType = decl.IsNodeSource ? "link" : decl.SourceType,
            SourceConfig = new Dictionary<string, object>
            {
                ["NodeId"] = decl.NodeId,
                ["PortName"] = decl.PortName,
                ["Value"] = decl.Value!
            }
        };

        /// <summary>
        /// 从节点配置解析自定义参数
        /// </summary>
        private List<CustomParam> ParseCustomParams(WorkflowNodeConfig node)
        {
            var result = new List<CustomParam>();

            if (node.Config == null || !node.Config.TryGetConfigValue("CustomParams", out var cpValue))
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
        private async Task<object?> ResolveParamAsync(
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
                    return param.SourceConfig.GetConfigValue("Value");

                default:
                    _logger.LogWarning("不支持的参数来源类型: {SourceType}", param.SourceType);
                    return null;
            }
        }

        /// <summary>
        /// 解析 link 类型参数（从节点输出读取）
        /// </summary>
        private object? ResolveLinkParam(CustomParam param, Dictionary<string, object> sharedOutputs)
        {
            var nodeId = param.SourceConfig.GetConfigValue("NodeId")?.ToString();
            var portName = param.SourceConfig.GetConfigValue("PortName")?.ToString();

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
        private async Task<object?> ResolveSkillParamAsync(
            CustomParam param,
            Dictionary<string, object> sharedOutputs,
            Dictionary<string, object> contextParams,
            string taskCode,
            string itemCode,
            CancellationToken ct)
        {
            var skillCode = param.SourceConfig.GetConfigValue("SkillCode")?.ToString();
            if (string.IsNullOrEmpty(skillCode))
                return null;

            // 解析 Skill 参数
            var skillParams = new Dictionary<string, object>();
            if (param.SourceConfig.TryGetConfigValue("SkillParams", out var spValue) &&
                param.SourceConfig.TryGetConfigValue("SkillParamsTypes", out var sptValue))
            {
                var spDict = ConvertToDict(spValue);
                var sptDict = ConvertToDict(sptValue);

                if (spDict != null && sptDict != null)
                {
                    foreach (var (key, rawValue) in spDict)
                    {
                        var paramType = sptDict.GetValueOrDefault(key)?.ToString() ?? "constant";
                        var value = ResolveSkillParamValue(rawValue?.ToString(), paramType, sharedOutputs, contextParams);
                        skillParams[key] = value!;
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
                NodeId = param.ParamName
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
        private object? ResolveSkillParamValue(
            string? rawValue,
            string? inputType,
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

        // ── 模板引用与类型转换 ──

        /// <summary>
        /// 从模板中提取 {{节点名.端口}} 或 {{节点名}} 引用，自动从 sharedOutputs 中按节点标题/ID 查找输出值
        /// <para>匹配规则：</para>
        /// <para>  {{xxx.yyy}} → 在 sharedOutputs 中查找 key=xxx，再取 yyy 端口的值</para>
        /// <para>  {{xxx}} → 在 sharedOutputs 中查找 key=xxx，直接取值（常量节点或单端口节点）</para>
        /// </summary>
        /// <summary>
        /// 把模板里的 <c>{{节点名.端口}}</c> 引用解析进 <paramref name="paramPool"/>。
        ///
        /// <para><b>★ 2026-10-12 改为返回「未解析清单」</b>（原返回 void，解析不出来只留一条
        /// Debug 日志 ⇒ 生产环境完全看不见）：</para>
        /// <list type="bullet">
        /// <item>引用的节点输出<b>不存在</b>（节点名/标题写错，或上游没跑）</item>
        /// <item>引用存在但<b>值为 null</b>（上游返回空 —— 最常见的一类）</item>
        /// </list>
        /// <para>调用方 <c>ExecuteAsync</c> 据此抛 <see cref="WorkflowDataMissingException"/>
        /// ⇒ 全局中断当前检查项 + 落补录缺口，⛔ 不再「带着未替换的占位符去问大模型」。</para>
        /// </summary>
        /// <returns>未解析的完整引用名（如 <c>文档字段.result</c>），空列表 = 全部解析成功</returns>
        private List<string> ResolveTemplateRefs(
            string? template,
            Dictionary<string, object> sharedOutputs,
            Dictionary<string, object> paramPool)
        {
            var unresolved = new List<string>();

            if (string.IsNullOrEmpty(template)) return unresolved;

            var matches = PromptRenderer.Placeholder.Matches(template);

            if (sharedOutputs == null || sharedOutputs.Count == 0)
            {
                // 完全没有上游输出 ⇒ 模板里的每个引用都解析不出来
                foreach (System.Text.RegularExpressions.Match m in matches)
                    unresolved.Add(m.Groups[1].Value);
                return unresolved;
            }

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
                    unresolved.Add(fullKey);
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

                // ★ 空值也算「未解析」—— 原实现只判 null，且 null 时静默跳过。
                //   注意这里必须用「空白/空集合」口径，与 SkillExecutor.IsEmptyValue 保持一致：
                //   上游把「取不到」表达为 ""（如 assemble 拼出空串）时，同样不该送 LLM。
                if (!IsBlank(resolved))
                {
                    paramPool[fullKey] = resolved!;
                    _logger.LogDebug("[AI_TEMPLATE_REF] resolved {{{{{FullKey}}}}} = {Value}",
                        fullKey, resolved);
                }
                else
                {
                    _logger.LogWarning("[AI_TEMPLATE_REF] 引用 '{FullKey}' 的值为空，无法送 LLM", fullKey);
                    unresolved.Add(fullKey);
                }
            }

            return unresolved;
        }

        /// <summary>值是否算「空」（与 <c>SkillExecutor.IsEmptyValue</c> 同口径：null / 空白串 / 空集合）</summary>
        private static bool IsBlank(object? v)
        {
            if (v == null) return true;
            if (v is string s) return string.IsNullOrWhiteSpace(s);
            if (v is System.Collections.ICollection c) return c.Count == 0;
            return false;
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
            return nodeOutput;
        }

        /// <summary>
        /// 按 outputType 转换结果
        /// </summary>
        private object ConvertOutputType(string? content, string outputType)
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

        /// <summary>
        /// 类型转换 + （outputType=json 时）Schema 校验 —— 真实路径与配置期测试<b>共用</b>（§2.4 单点纪律）。
        ///
        /// <para><b>json 分支</b>（第 5 批）：</para>
        /// <list type="number">
        /// <item>先 <see cref="PromptRenderer.ExtractJson"/> 剥 ```json 围栏 + 首尾大括号兜底
        ///   （LLM 极常把 JSON 包在围栏里，不剥 = 解析必失败）。</item>
        /// <item>再 <c>JsonDocument.Parse</c> → 递归归一化成 Dictionary/List/原始值
        ///   （⛔ 不返回 <c>JsonElement</c>：它挂在已 dispose 的 document 上，跨方法持有会抛
        ///    <c>InvalidOperationException</c>；且下游 <c>{{字段}}</c> 取值按 Dictionary 口径）。</item>
        /// <item>若节点配了 <c>config.outputSchema</c> ⇒ 交给 <see cref="AiOutputSchema.Validate"/>，
        ///   有违规即抛 <c>JSON_SCHEMA_VIOLATION</c>（⛔ 不静默放行：下游按字段已存在读取却拿 null）。</item>
        /// </list>
        ///
        /// <para>非 json 类型仍走 <see cref="ConvertOutputType"/>（保持既有行为不变）。</para>
        /// </summary>
        private object ConvertAndValidateOutput(
            Dictionary<string, object> config, string? content, string outputType, string nodeId)
        {
            if (!string.Equals(outputType, "json", StringComparison.OrdinalIgnoreCase))
                return ConvertOutputType(content, outputType);

            var (ok, jsonText, extractError) = PromptRenderer.ExtractJson(content);
            if (!ok)
                throw new AiNodeException("JSON_EXTRACT_FAILED",
                    $"AI 节点「{nodeId}」输出类型为 json 但提取不到 JSON：{extractError}");

            object? parsed;
            try
            {
                using var doc = JsonDocument.Parse(jsonText);
                parsed = NormalizeJsonValue(doc.RootElement);
            }
            catch (JsonException ex)
            {
                throw new AiNodeException("JSON_PARSE_FAILED",
                    $"AI 节点「{nodeId}」输出类型为 json 但内容不是合法 JSON：{ex.Message}\n原文：{Truncate(jsonText, 200)}");
            }

            // Schema 校验（未配 ⇒ 恒通过）
            if (config.TryGetValue("outputSchema", out var schemaObj))
            {
                var violations = AiOutputSchema.Validate(schemaObj, parsed);
                if (violations.Count > 0)
                {
                    throw new AiNodeException("JSON_SCHEMA_VIOLATION",
                        $"AI 节点「{nodeId}」输出不符合 outputSchema：{string.Join("；", violations)}" +
                        " —— 请调整提示词明确输出契约，或修正 outputSchema");
                }
            }

            return parsed ?? new { };
        }

        /// <summary>JsonElement → Dictionary / List / 原始值 递归归一化（保持跨方法安全 + 下游取值口径一致）。</summary>
        private static object? NormalizeJsonValue(JsonElement el) => el.ValueKind switch
        {
            JsonValueKind.Object => (object)el.EnumerateObject().ToDictionary(
                p => p.Name, p => NormalizeJsonValue(p.Value), StringComparer.Ordinal),
            JsonValueKind.Array => el.EnumerateArray().Select(NormalizeJsonValue).ToList(),
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };

        private static string Truncate(string s, int max)
            => s.Length <= max ? s : s[..max] + "...";

        private static object TryParseNumber(string? s)
        {
            // 尝试从文本中提取数字
            var digits = new string(s?.TakeWhile(c => char.IsDigit(c) || c == '.' || c == '-').ToArray());
            if (double.TryParse(digits, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                return d;

            if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d2))
                return d2;

            return s ?? "";
        }

        private static object TryParseBoolean(string? s)
        {
            var lower = s?.ToLowerInvariant()?.Trim();
            if (lower == "true" || lower == "是" || lower == "yes" || lower == "1")
                return true;
            if (lower == "false" || lower == "否" || lower == "no" || lower == "0")
                return false;

            if (bool.TryParse(s, out var b))
                return b;

            return s ?? "";
        }

        private static object TryParseDate(string? s)
        {
            if (DateTime.TryParse(s, out var dt))
                return dt.ToString("yyyy-MM-dd");

            return s ?? "";
        }

        private static Dictionary<string, object>? ConvertToDict(object? value)
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
    }
}

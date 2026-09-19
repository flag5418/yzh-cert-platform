using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace CertPlatform.Shared.DocExtraction
{
    /// <summary>
    /// LLM 调用请求（Admin 层组装：读 cert_sys_config 六键后传入；Shared 层不读库，保持零 DB 依赖）
    /// </summary>
    public class LlmInvokeRequest
    {
        /// <summary>OpenAI 兼容端点 base url（cert_sys_config.ai_base_url）</summary>
        public string BaseUrl { get; set; } = "";

        /// <summary>API Key（cert_sys_config.ai_api_key）</summary>
        public string ApiKey { get; set; } = "";

        /// <summary>模型名（cert_sys_config.ai_model_name，唯一真相源）</summary>
        public string Model { get; set; } = "qwen-turbo";

        public float Temperature { get; set; } = 0.7f;
        public int MaxTokens { get; set; } = 4096;

        /// <summary>系统提示（可选）</summary>
        public string? SystemPrompt { get; set; }

        /// <summary>用户提示（Prompt 模板渲染结果，含 {document_content} 已替换）</summary>
        public string Prompt { get; set; } = "";

        /// <summary>文档上下文（Markdown 全文）</summary>
        public string DocumentContent { get; set; } = "";

        /// <summary>是否强制 JSON 输出（analyze/verify 场景 true）</summary>
        public bool ForceJson { get; set; } = true;
    }

    /// <summary>LLM 调用响应</summary>
    public class LlmInvokeResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        /// <summary>模型原始文本输出</summary>
        public string Content { get; set; } = "";
        /// <summary>JSON 解析结果（ForceJson=true 时）</summary>
        public JsonDocument? Json { get; set; }
        public int? PromptTokens { get; set; }
        public int? CompletionTokens { get; set; }
        public long DurationMs { get; set; }
    }

    /// <summary>
    /// LLM 调用服务（共享层，决策 D-5）
    /// <para>纯 HttpClient 直连 OpenAI 兼容接口（dashscope compatible-mode / deepseek / openai 均兼容）</para>
    /// <para>替代旧架构 IWorkflowEngine + LlmExtractSkill 执行器；业务编排层规则保留在 Admin 层</para>
    /// <para>调用约束：ai_extract_enabled 开关检查在 Admin 层（BusinessHook），本层只负责执行</para>
    /// </summary>
    public class LlmInvokeService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<LlmInvokeService> _logger;

        /// <summary>单次调用超时（秒）</summary>
        public int TimeoutSeconds { get; set; } = 180;

        public LlmInvokeService(IHttpClientFactory httpClientFactory, ILogger<LlmInvokeService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <summary>
        /// 调用 LLM 并解析 JSON 输出
        /// </summary>
        public virtual async Task<LlmInvokeResponse> CompleteAsync(LlmInvokeRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.BaseUrl))
                return new LlmInvokeResponse { Success = false, Message = "AI 端点未配置（cert_sys_config.ai_base_url）" };
            if (string.IsNullOrWhiteSpace(request.ApiKey))
                return new LlmInvokeResponse { Success = false, Message = "AI API Key 未配置（cert_sys_config.ai_api_key）" };
            if (string.IsNullOrWhiteSpace(request.Prompt))
                return new LlmInvokeResponse { Success = false, Message = "Prompt 为空" };

            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var client = _httpClientFactory.CreateClient("LlmInvoke");
                client.Timeout = TimeSpan.FromSeconds(TimeoutSeconds);

                var url = request.BaseUrl.TrimEnd('/') + "/chat/completions";

                // 组装 OpenAI 兼容消息
                var messages = new List<object>();
                if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
                    messages.Add(new { role = "system", content = request.SystemPrompt });

                var userContent = request.DocumentContent.Length > 0
                    ? $"{request.Prompt}\n\n---\n{request.DocumentContent}\n---"
                    : request.Prompt;
                messages.Add(new { role = "user", content = userContent });

                var bodyObj = request.ForceJson
                    ? (object)new
                    {
                        model = request.Model,
                        messages,
                        temperature = request.Temperature,
                        max_tokens = request.MaxTokens,
                        response_format = new { type = "json_object" }
                    }
                    : (object)new
                    {
                        model = request.Model,
                        messages,
                        temperature = request.Temperature,
                        max_tokens = request.MaxTokens
                    };

                var json = JsonSerializer.Serialize(bodyObj);
                using var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, url) { Content = httpContent };
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.ApiKey);

                _logger.LogInformation("[LLM] 调用 {Model} @ {Url}，上下文 {Len} 字符", request.Model, request.BaseUrl, request.DocumentContent.Length);

                var response = await client.SendAsync(httpRequest);
                var responseBody = await response.Content.ReadAsStringAsync();
                sw.Stop();

                if (!response.IsSuccessStatusCode)
                {
                    var brief = responseBody.Length > 300 ? responseBody[..300] : responseBody;
                    _logger.LogWarning("[LLM] 调用失败 {Code}: {Body}", (int)response.StatusCode, brief);
                    return new LlmInvokeResponse
                    {
                        Success = false,
                        Message = $"AI 调用失败（{(int)response.StatusCode}）：{brief}",
                        DurationMs = sw.ElapsedMilliseconds
                    };
                }

                using var doc = JsonDocument.Parse(responseBody);
                var root = doc.RootElement;

                var content = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
                int? promptTokens = root.TryGetProperty("usage", out var usage) && usage.TryGetProperty("prompt_tokens", out var pt) ? pt.GetInt32() : null;
                int? completionTokens = root.TryGetProperty("usage", out var usage2) && usage2.TryGetProperty("completion_tokens", out var ct2) ? ct2.GetInt32() : null;

                _logger.LogInformation("[LLM] 调用完成 {Ms}ms，输出 {Len} 字符，tokens={P}/{C}",
                    sw.ElapsedMilliseconds, content.Length, promptTokens, completionTokens);

                var result = new LlmInvokeResponse
                {
                    Success = true,
                    Content = content,
                    PromptTokens = promptTokens,
                    CompletionTokens = completionTokens,
                    DurationMs = sw.ElapsedMilliseconds
                };

                if (request.ForceJson)
                {
                    var cleaned = CleanJsonText(content);
                    try
                    {
                        result.Json = JsonDocument.Parse(cleaned);
                    }
                    catch (JsonException je)
                    {
                        // 输出被 max_tokens 截断（completion 恰好等于上限）时尽力抢救：
                        // 裁到最后一处完整闭合，再补齐未闭合的容器——能救回大部分字段/表格，
                        // 总比整体报「无法解析为 JSON」好。
                        var truncated = completionTokens != null && completionTokens >= request.MaxTokens;
                        var repaired = truncated ? TryRepairTruncatedJson(cleaned) : null;
                        if (repaired != null)
                        {
                            try
                            {
                                result.Json = JsonDocument.Parse(repaired);
                                _logger.LogWarning("[LLM] 输出疑似被 max_tokens({Max}) 截断，已抢救解析（原文 {Len} 字符，抢救后 {New} 字符）",
                                    request.MaxTokens, content.Length, repaired.Length);
                            }
                            catch (JsonException) { result.Json = null; }
                        }

                        if (result.Json == null)
                        {
                            _logger.LogWarning("[LLM] JSON 解析失败: {Err}，原文前 200 字符: {Head}", je.Message, content.Length > 200 ? content[..200] : content);
                            result.Success = false;
                            result.Message = "AI 返回内容无法解析为 JSON";
                        }
                    }
                }

                return result;
            }
            catch (TaskCanceledException)
            {
                return new LlmInvokeResponse { Success = false, Message = $"AI 调用超时（{TimeoutSeconds}s）", DurationMs = sw.ElapsedMilliseconds };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[LLM] 调用异常");
                return new LlmInvokeResponse { Success = false, Message = $"AI 调用异常：{ex.Message}", DurationMs = sw.ElapsedMilliseconds };
            }
        }

        /// <summary>
        /// 尝试修复被截断的 JSON：裁到最后一处完整的对象/数组结尾，
        /// 再按括号栈倒序补齐未闭合的容器。字符串被截断时返回 null（无法安全补）。
        /// </summary>
        private static string? TryRepairTruncatedJson(string text)
        {
            var end = text.LastIndexOf('}');
            if (end < 0) return null;
            var candidate = text[..(end + 1)];

            var stack = new List<char>();
            var inString = false;
            var escaped = false;
            foreach (var ch in candidate)
            {
                if (inString)
                {
                    if (escaped) escaped = false;
                    else if (ch == '\\') escaped = true;
                    else if (ch == '"') inString = false;
                    continue;
                }
                switch (ch)
                {
                    case '"': inString = true; break;
                    case '{': stack.Add('}'); break;
                    case '[': stack.Add(']'); break;
                    case '}':
                    case ']':
                        if (stack.Count > 0 && stack[^1] == ch) stack.RemoveAt(stack.Count - 1);
                        break;
                }
            }

            if (inString) return null;
            var sb = new StringBuilder(candidate);
            for (int i = stack.Count - 1; i >= 0; i--) sb.Append(stack[i]);
            return sb.ToString();
        }

        /// <summary>剥离 markdown 代码围栏等包装（```json ... ```）</summary>
        private static string CleanJsonText(string content)
        {
            var text = content.Trim();
            if (text.StartsWith("```"))
            {
                var firstLineEnd = text.IndexOf('\n');
                if (firstLineEnd > 0) text = text[(firstLineEnd + 1)..].Trim();
                if (text.EndsWith("```")) text = text[..^3].Trim();
            }
            return text;
        }
    }
}

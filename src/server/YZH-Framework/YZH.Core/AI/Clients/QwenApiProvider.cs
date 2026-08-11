using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using YZH.Core.AI.Clients.Models;

namespace YZH.Core.AI.Clients
{
    /// <summary>
    /// Qwen 云端 Provider（OpenAI 兼容 /chat/completions）。
    /// 默认模型 qwen-turbo，成本控制用；API Key 优先环境变量 AI_QWEN_API_KEY。
    /// </summary>
    public class QwenApiProvider : ILlmProvider
    {
        public string Name => "qwen";

        private static string Endpoint => 
            Environment.GetEnvironmentVariable("AI_QWEN_ENDPOINT") ?? 
            "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions";

        public async Task<LlmResponse> ChatAsync(LlmRequest request, CancellationToken ct = default)
        {
            var apiKey = GetApiKey();
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(request.TimeoutSeconds) };

            // 过滤非文本内容，移除图片引用以避免模型报错
            var cleanedMessages = request.Messages.Select(m => new { 
                role = m.Role, 
                content = CleanContent(m.Content) 
            }).ToArray();
            
            var payload = new
            {
                model = request.Model,
                messages = cleanedMessages,
                temperature = request.Temperature,
                max_tokens = request.MaxTokens,
                response_format = request.JsonMode ? (object)new { type = "json_object" } : null
            };

            using var content = new StringContent(
                JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var resp = await http.PostAsync(Endpoint, content, ct);
                var body = await resp.Content.ReadAsStringAsync(ct);
                sw.Stop();

                if (!resp.IsSuccessStatusCode)
                {
                    var code = (int)resp.StatusCode;
                    var isRetryable = code == 429 || (code >= 500 && code < 600);
                    throw new AI.LlmCallException(
                        $"Qwen HTTP {(int)resp.StatusCode}: {Truncate(body, 500)}", isRetryable);
                }

                return ParseOpenAiResponse(body, Name, request.Model, sw.ElapsedMilliseconds);
            }
            catch (AI.LlmCallException) { throw; }
            catch (TaskCanceledException)
            {
                sw.Stop();
                throw new AI.LlmCallException("Qwen 调用超时", true);
            }
            catch (Exception ex)
            {
                sw.Stop();
                throw new AI.LlmCallException($"Qwen 调用异常: {ex.Message}", true, ex);
            }
        }

        /// <summary>清理内容，移除图片引用等非文本内容</summary>
        private static string CleanContent(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return content;
            // 移除常见的图片引用模式
            var cleaned = System.Text.RegularExpressions.Regex.Replace(content, @"!\[.*?\]\(.*?\)", "[图片已移除]");
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"<img[^>]*>", "[图片已移除]");
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\[\](image|png|jpg|jpeg|gif|bmp)[^\]*", "[图片已移除]");
            return cleaned.Trim();
        }

        private static string GetApiKey()
        {
            var key = Environment.GetEnvironmentVariable("AI_QWEN_API_KEY");
            if (!string.IsNullOrWhiteSpace(key)) return key;
            throw new AI.LlmCallException("未配置 AI_QWEN_API_KEY 环境变量", true);
        }

        private static LlmResponse ParseOpenAiResponse(string body, string provider, string model, long durationMs)
        {
            using var doc = JsonDocument.Parse(body);
            var choices = doc.RootElement.GetProperty("choices");
            var msg = choices[0].GetProperty("message");
            var content = msg.GetProperty("content").GetString() ?? string.Empty;

            int? promptTokens = null, completionTokens = null;
            if (doc.RootElement.TryGetProperty("usage", out var usage))
            {
                promptTokens = usage.GetProperty("prompt_tokens").GetInt32();
                completionTokens = usage.GetProperty("completion_tokens").GetInt32();
            }

            return new LlmResponse
            {
                Success = true,
                Content = content,
                RawJson = body,
                PromptTokens = promptTokens,
                CompletionTokens = completionTokens,
                DurationMs = durationMs,
                Provider = provider,
                Model = model
            };
        }

        private static string Truncate(string s, int max) =>
            s.Length <= max ? s : s.Substring(0, max) + "...";
    }
}

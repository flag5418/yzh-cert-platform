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
    /// Ollama 本地 Provider（/api/chat）。
    /// 断网/免费场景切换；无需 API Key。
    /// </summary>
    public class OllamaProvider : ILlmProvider
    {
        public string Name => "ollama";

        private static readonly string Endpoint = "http://localhost:11434/api/chat";

        public async Task<LlmResponse> ChatAsync(LlmRequest request, CancellationToken ct = default)
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(request.TimeoutSeconds) };

            var payload = new
            {
                model = request.Model,
                messages = request.Messages.Select(m => (object)new { role = m.Role, content = m.Content }),
                stream = false,
                options = new
                {
                    temperature = request.Temperature,
                    num_predict = request.MaxTokens
                },
                format = request.JsonMode ? "json" : (object?)null
            };

            using var content = new StringContent(
                JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var resp = await http.PostAsync(Endpoint, content, ct);
                var body = await resp.Content.ReadAsStringAsync(ct);
                sw.Stop();

                if (!resp.IsSuccessStatusCode)
                    throw new AI.LlmCallException(
                        $"Ollama HTTP {(int)resp.StatusCode}: {Truncate(body, 500)}", true);

                var json = JsonDocument.Parse(body);
                var text = json.RootElement.GetProperty("message")
                               .GetProperty("content").GetString() ?? string.Empty;

                return new LlmResponse
                {
                    Success = true,
                    Content = text,
                    RawJson = body,
                    DurationMs = sw.ElapsedMilliseconds,
                    Provider = Name,
                    Model = request.Model
                };
            }
            catch (AI.LlmCallException) { throw; }
            catch (HttpRequestException ex)
            {
                sw.Stop();
                throw new AI.LlmCallException(
                    $"Ollama 不可达（本地服务未启动?）: {ex.Message}", true, ex);
            }
            catch (TaskCanceledException)
            {
                sw.Stop();
                throw new AI.LlmCallException("Ollama 调用超时", true);
            }
        }

        private static string Truncate(string s, int max) =>
            s.Length <= max ? s : s.Substring(0, max) + "...";
    }
}

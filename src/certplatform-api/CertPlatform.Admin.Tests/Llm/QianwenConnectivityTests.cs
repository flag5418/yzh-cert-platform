using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace CertPlatform.Admin.Tests.Llm;

/// <summary>
/// 千问（DashScope）LLM 连通性诊断测试。
///
/// <para><b>用途</b>：直接调用 DashScope API，验证网络/代理/AI 配置是否正常。
/// 与业务代码无关，纯粹检测「当前运行环境能否连上千问」。</para>
///
/// <para><b>运行方式</b>：
/// <code>
/// cd src/certplatform-api/CertPlatform.Admin.Tests
/// dotnet test --filter "FullyQualifiedName~QianwenConnectivity"
/// </code></para>
///
/// <para><b>配置来源</b>（优先级降序）：
/// <list type="number">
///   <item>环境变量 <c>YZH_AI_BASE_URL</c> / <c>YZH_AI_API_KEY</c> / <c>YZH_AI_MODEL</c></item>
///   <item>自动从 docker 中的 <c>cert_sys_config</c> 读取（要求 docker 容器 <c>yzh-mysql</c> 在运行）</item>
///   <item>硬编码 DashScope 兼容模式 URL，仅 API Key 必须外部提供</item>
/// </list></para>
///
/// <para><b>诊断输出</b>：测试失败时，控制台会打印完整的代理环境、请求 URL、HTTP 状态码、
/// 响应正文，帮助你判断是代理问题、API Key 问题还是服务端问题。</para>
/// </summary>
public class QianwenConnectivityTests
{
    private readonly ITestOutputHelper _output;

    public QianwenConnectivityTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // ════════════════════════════════════════════════════════════════════
    // 主测试：直接调用千问 API
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task 千问API_直接连通性测试_应返回成功()
    {
        PrintProxyEnvironment();

        var (baseUrl, apiKey, model) = await ResolveConfigAsync();
        PrintConfig(baseUrl, apiKey, model);

        Assert.False(string.IsNullOrWhiteSpace(baseUrl),
            "AI BaseUrl 未配置。请设置环境变量 YZH_AI_BASE_URL，或确保 docker yzh-mysql 可访问。");
        Assert.False(string.IsNullOrWhiteSpace(apiKey),
            "AI API Key 未配置。请设置环境变量 YZH_AI_API_KEY，或确保 cert_sys_config.ai_api_key 已填写。");

        var result = await CallDashScopeAsync(baseUrl, apiKey, model);

        PrintResult(result);

        Assert.True(result.Success,
            $"LLM 调用失败：{result.ErrorMessage}。请检查上方「代理环境」与「响应信息」排查根因。");

        Assert.False(string.IsNullOrWhiteSpace(result.Content),
            "LLM 返回内容为空。可能是响应解析异常，请检查上方响应正文。");
    }

    // ════════════════════════════════════════════════════════════════════
    // 配置解析
    // ════════════════════════════════════════════════════════════════════

    private async Task<(string BaseUrl, string ApiKey, string Model)> ResolveConfigAsync()
    {
        // 1) 环境变量优先
        var baseUrl = Environment.GetEnvironmentVariable("YZH_AI_BASE_URL");
        var apiKey  = Environment.GetEnvironmentVariable("YZH_AI_API_KEY");
        var model   = Environment.GetEnvironmentVariable("YZH_AI_MODEL");

        if (!string.IsNullOrWhiteSpace(baseUrl) && !string.IsNullOrWhiteSpace(apiKey))
        {
            _output.WriteLine("[配置来源] 环境变量");
            return (baseUrl, apiKey, model ?? "qwen-flash");
        }

        // 2) 尝试从 docker mysql 读取
        _output.WriteLine("[配置来源] 尝试从 docker yzh-mysql / cert_sys_config 读取…");
        try
        {
            var fromDb = await ReadConfigFromDockerAsync();
            if (!string.IsNullOrWhiteSpace(fromDb.ApiKey))
            {
                _output.WriteLine("[配置来源] 数据库读取成功");
                return (fromDb.BaseUrl, fromDb.ApiKey, fromDb.Model);
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"[配置来源] 数据库读取失败：{ex.Message}");
        }

        // 3) 兜底
        return (
            baseUrl ?? "https://dashscope.aliyuncs.com/compatible-mode/v1",
            apiKey ?? "",
            model ?? "qwen-flash"
        );
    }

    private async Task<(string BaseUrl, string ApiKey, string Model)> ReadConfigFromDockerAsync()
    {
        const string sql =
            "SELECT ConfigKey, ConfigValue FROM cert_sys_config WHERE Category = 'ai_model';";

        var psi = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = $"exec -i yzh-mysql mysql -uroot -pYzh123456. --skip-column-names --silent --default-character-set=utf8mb4 yzh_cert_platform -e \"{sql}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("无法启动 docker 进程");

        var stdout = await proc.StandardOutput.ReadToEndAsync();
        var stderr = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();

        if (proc.ExitCode != 0)
            throw new InvalidOperationException($"docker 退出码 {proc.ExitCode}：{stderr.Trim()}");

        var dict = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('\t', 2);
            if (parts.Length == 2)
                dict[parts[0].Trim()] = parts[1].Trim();
        }

        return (
            dict.GetValueOrDefault("ai_base_url", ""),
            dict.GetValueOrDefault("ai_api_key", ""),
            dict.GetValueOrDefault("ai_model_name", "qwen-flash")
        );
    }

    // ════════════════════════════════════════════════════════════════════
    // HTTP 调用
    // ════════════════════════════════════════════════════════════════════

    private async Task<CallResult> CallDashScopeAsync(string baseUrl, string apiKey, string model)
    {
        var url = baseUrl.TrimEnd('/') + "/chat/completions";

        var body = new
        {
            model = model,
            messages = new[]
            {
                new { role = "system", content = "你是一个简洁的助手。" },
                new { role = "user", content = "请回答：1+1等于几？只输出数字。" }
            },
            max_tokens = 64,
            temperature = 0.1,
        };

        var json = JsonSerializer.Serialize(body);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        _output.WriteLine("");
        _output.WriteLine("=== HTTP 请求 ===");
        _output.WriteLine($"POST {url}");
        _output.WriteLine($"Authorization: Bearer {apiKey[..Math.Min(10, apiKey.Length)]}...");
        _output.WriteLine($"Body: {json}");

        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(30);

        var sw = Stopwatch.StartNew();
        try
        {
            var response = await client.SendAsync(request);
            sw.Stop();

            var respBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine("");
            _output.WriteLine("=== HTTP 响应 ===");
            _output.WriteLine($"StatusCode = {(int)response.StatusCode} {response.ReasonPhrase}");
            _output.WriteLine($"Duration   = {sw.ElapsedMilliseconds}ms");
            _output.WriteLine($"Body(前800)= {Truncate(respBody, 800)}");

            if (!response.IsSuccessStatusCode)
            {
                return new CallResult
                {
                    Success = false,
                    ErrorMessage = $"HTTP {(int)response.StatusCode}：{Truncate(respBody, 500)}",
                };
            }

            // 解析内容
            using var doc = JsonDocument.Parse(respBody);
            var choices = doc.RootElement.GetProperty("choices");
            var contentText = choices[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            var promptTokens = doc.RootElement.TryGetProperty("usage", out var usage)
                && usage.TryGetProperty("prompt_tokens", out var pt)
                ? pt.GetInt32() : (int?)null;

            var completionTokens = usage.TryGetProperty("completion_tokens", out var ct)
                ? ct.GetInt32() : (int?)null;

            return new CallResult
            {
                Success = true,
                Content = contentText ?? "",
                PromptTokens = promptTokens,
                CompletionTokens = completionTokens,
                DurationMs = sw.ElapsedMilliseconds,
            };
        }
        catch (TaskCanceledException)
        {
            sw.Stop();
            return new CallResult
            {
                Success = false,
                ErrorMessage = $"请求超时（30s）。可能是代理不可达或网络阻塞。Duration={sw.ElapsedMilliseconds}ms",
            };
        }
        catch (HttpRequestException ex)
        {
            sw.Stop();
            return new CallResult
            {
                Success = false,
                ErrorMessage = $"HTTP 请求异常：{ex.Message}。请检查代理环境（上方「代理环境」段落）。Duration={sw.ElapsedMilliseconds}ms",
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new CallResult
            {
                Success = false,
                ErrorMessage = $"异常：{ex.GetType().Name}：{ex.Message}。Duration={sw.ElapsedMilliseconds}ms",
            };
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // 输出辅助
    // ════════════════════════════════════════════════════════════════════

    private void PrintProxyEnvironment()
    {
        _output.WriteLine("=== 代理环境（当前进程继承值）===");
        _output.WriteLine($"HTTP_PROXY  = {EnvOrNull("HTTP_PROXY")}");
        _output.WriteLine($"HTTPS_PROXY = {EnvOrNull("HTTPS_PROXY")}");
        _output.WriteLine($"http_proxy  = {EnvOrNull("http_proxy")}");
        _output.WriteLine($"https_proxy = {EnvOrNull("https_proxy")}");
        _output.WriteLine($"NO_PROXY    = {EnvOrNull("NO_PROXY")}");
        _output.WriteLine($"no_proxy    = {EnvOrNull("no_proxy")}");
    }

    private void PrintConfig(string baseUrl, string apiKey, string model)
    {
        _output.WriteLine("");
        _output.WriteLine("=== AI 配置 ===");
        _output.WriteLine($"BaseUrl = {baseUrl}");
        _output.WriteLine($"Model   = {model}");
        _output.WriteLine($"ApiKey  = {(string.IsNullOrEmpty(apiKey) ? "(空)" : apiKey[..Math.Min(10, apiKey.Length)] + "...")}");
    }

    private void PrintResult(CallResult result)
    {
        _output.WriteLine("");
        _output.WriteLine("=== 调用结果 ===");
        _output.WriteLine($"Success          = {result.Success}");
        _output.WriteLine($"ErrorMessage     = {result.ErrorMessage}");
        _output.WriteLine($"Content          = {result.Content}");
        _output.WriteLine($"PromptTokens     = {result.PromptTokens}");
        _output.WriteLine($"CompletionTokens = {result.CompletionTokens}");
        _output.WriteLine($"DurationMs       = {result.DurationMs}");
    }

    private static string EnvOrNull(string key)
        => Environment.GetEnvironmentVariable(key) ?? "(未设置)";

    private static string Truncate(string? s, int max)
        => string.IsNullOrEmpty(s) || s.Length <= max ? s ?? "" : s[..max] + "…";

    // ════════════════════════════════════════════════════════════════════
    // 内部模型
    // ════════════════════════════════════════════════════════════════════

    private sealed class CallResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = "";
        public string Content { get; set; } = "";
        public int? PromptTokens { get; set; }
        public int? CompletionTokens { get; set; }
        public long DurationMs { get; set; }
    }
}

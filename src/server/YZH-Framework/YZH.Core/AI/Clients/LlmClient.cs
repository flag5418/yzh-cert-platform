using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using YZH.Core.AI.Clients.Models;

namespace YZH.Core.AI.Clients
{
    /// <summary>
    /// LLM 网关：Provider 路由 + 指数退避重试 + 熔断 + 全局并发信号量。
    /// <para>设计约束：不修改 Vol 源码；通过 Autofac YZHModule 挂载；默认并发 2 防 Qwen 限流。</para>
    /// </summary>
    public class LlmClient : ILlmClient
    {
        private readonly IEnumerable<ILlmProvider> _providers;
        private readonly IConfiguration _config;
        private readonly ILogger<LlmClient> _logger;

        // 全局并发信号量（防止批量提取触发 Qwen 429 限流 / Ollama GPU 过载）
        private static readonly SemaphoreSlim DefaultGate = new(2, 2);
        private SemaphoreSlim _callGate;

        // 熔断：连续失败 5 次 → 30s 内快速失败
        private static int _consecutiveFailures;
        private static DateTime _circuitBreakerUntil = DateTime.MinValue;
        private static readonly object _circuitLock = new();

        // 重试退避：429 / 5xx / Timeout → 1s / 3s / 7s
        private static readonly int[] RetryDelaysMs = { 1000, 3000, 7000 };

        public LlmClient(
            IEnumerable<ILlmProvider> providers,
            IConfiguration config,
            ILogger<LlmClient> logger)
        {
            _providers = providers;
            _config = config;
            _logger = logger;
            _callGate = CreateGate();
        }

        public string ActiveProvider => _config["Ai:Provider"] ?? "qwen";

        public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default)
        {
            // 1. 熔断快速失败
            lock (_circuitLock)
            {
                if (DateTime.Now < _circuitBreakerUntil)
                    throw new AI.LlmCallException(
                        $"Provider 熔断中（至 {_circuitBreakerUntil:HH:mm:ss}），请稍后重试", true);
            }

            var providerName = string.IsNullOrWhiteSpace(request.Provider) ? ActiveProvider : request.Provider;
            var ordered = ProviderOrder(providerName);
            Exception? lastEx = null;

            foreach (var pName in ordered)
            {
                var provider = _providers.FirstOrDefault(p => p.Name == pName);
                if (provider == null) continue;

                for (var retry = 0; retry <= RetryDelaysMs.Length; retry++)
                {
                    ct.ThrowIfCancellationRequested();
                    await _callGate.WaitAsync(ct);
                    try
                    {
                        var resp = await provider.ChatAsync(request, ct);
                        Interlocked.Exchange(ref _consecutiveFailures, 0);
                        return resp;
                    }
                    catch (AI.LlmCallException ex) when (
                        ex.IsTimeout ||
                        ex.Message.Contains("429") ||
                        (ex.Message.Contains("500") || ex.Message.Contains("502") || ex.Message.Contains("503")))
                    {
                        lastEx = ex;
                        var fails = Interlocked.Increment(ref _consecutiveFailures);
                        _logger.LogWarning(
                            ex, "LlmCall {Provider} 第 {Retry} 次失败（累计 {Fails}）",
                            pName, retry, fails);

                        if (fails >= 5)
                        {
                            lock (_circuitLock)
                                _circuitBreakerUntil = DateTime.Now.AddSeconds(30);
                            _logger.LogError("Provider {Provider} 连续失败 5 次，熔断 30s", pName);
                            break; // 切下一个 Provider
                        }
                        if (retry < RetryDelaysMs.Length)
                            await Task.Delay(RetryDelaysMs[retry], ct);
                    }
                    finally
                    {
                        _callGate.Release();
                    }
                }
            }

            throw new AI.LlmCallException(
                $"所有 Provider 调用均失败: {lastEx?.Message}", true);
        }

        private static SemaphoreSlim CreateGate()
        {
            if (int.TryParse(Environment.GetEnvironmentVariable("AI_MAX_CONCURRENCY")
                            ?? AppContext.GetData("AI_MAX_CONCURRENCY")?.ToString(), out var conc)
                && conc > 0 && conc <= 32)
                return new SemaphoreSlim(conc, conc);
            return new SemaphoreSlim(2, 2);
        }

        // 按当前 Provider 优先，其余按降级链顺序（qwen → ollama → mock）
        private static IReadOnlyList<string> ProviderOrder(string first)
        {
            var list = new List<string> { first };
            foreach (var fallback in new[] { "qwen", "ollama", "mock" })
                if (!list.Contains(fallback)) list.Add(fallback);
            return list;
        }
    }
}

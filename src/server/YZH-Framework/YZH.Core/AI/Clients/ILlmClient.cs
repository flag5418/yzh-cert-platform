using System.Threading;
using System.Threading.Tasks;
using YZH.Core.AI.Clients.Models;

namespace YZH.Core.AI.Clients
{
    /// <summary>
    /// 模型无关 LLM 统一入口。上层只依赖本接口，不感知 Qwen/Ollama 差异。
    /// 内置：Provider 路由、指数退避重试、熔断、全局并发信号量。
    /// </summary>
    public interface ILlmClient
    {
        /// <summary>发起一次补全调用（OpenAI 兼容协议）</summary>
        /// <exception cref="Clients.LlmCallException">网络错误、超时、非 2xx、限流、Provider 不可用</exception>
        Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default);

        /// <summary>当前生效的 Provider 名（"qwen" / "ollama" / "mock"）</summary>
        string ActiveProvider { get; }
    }
}

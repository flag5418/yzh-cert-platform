using System.Threading;
using System.Threading.Tasks;
using YZH.Core.AI.Clients.Models;

namespace YZH.Core.AI.Clients
{
    /// <summary>
    /// Provider 抽象：模型无关的关键。
    /// 实现类负责协议差异（鉴权、端点、消息格式），失败语义统一抛 LlmCallException。
    /// </summary>
    public interface ILlmProvider
    {
        /// <summary>Provider 名称（对应 cert_ai_config.provider 值：qwen / ollama / mock）</summary>
        string Name { get; }

        /// <summary>OpenAI 兼容 /chat/completions 调用；失败抛 LlmCallException</summary>
        Task<LlmResponse> ChatAsync(LlmRequest request, CancellationToken ct = default);
    }
}

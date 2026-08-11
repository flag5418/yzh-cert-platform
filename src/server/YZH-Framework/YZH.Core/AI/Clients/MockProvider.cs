using System;
using System.Threading;
using System.Threading.Tasks;
using YZH.Core.AI.Clients.Models;

namespace YZH.Core.AI.Clients
{
    /// <summary>
    /// Mock Provider：返回固定 JSON，不消耗 token，用于单测与前端联调。
    /// </summary>
    public class MockProvider : ILlmProvider
    {
        public string Name => "mock";

        /// <summary>固定响应内容（可由测试注入）</summary>
        public string FixedContent { get; set; } = @"{""fields"":[],""tables"":[]}";

        public async Task<LlmResponse> ChatAsync(LlmRequest request, CancellationToken ct = default)
        {
            await Task.Yield();
            return new LlmResponse
            {
                Success = true,
                Content = FixedContent,
                RawJson = FixedContent,
                DurationMs = 1,
                Provider = Name,
                Model = request.Model
            };
        }
    }
}

using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace YZH.Core.AI.Plan
{
    public class AiPlanParser
    {
        public static AiPlan Parse(string json)
        {
            return JsonSerializer.Deserialize<AiPlan>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new JsonException("AiPlan JSON 反序列化为 null");
        }

        public static async Task<AiPlan> ParseAsync(string json, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return Parse(json);
        }
    }
}

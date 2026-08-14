using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using YZH.Core.AI.Prompt.Models;

namespace YZH.Core.AI.Prompt
{
    public class PromptInterpreter : IPromptInterpreter
    {
        // 模板占位符使用 {{name}}（双大括号），与 DB 提示词模板（{{document_content}} / {{fields_json}} 等）一致
        private static readonly Regex Placeholder = new(@"\{\{([a-zA-Z_][a-zA-Z0-9_]*)\}\}", RegexOptions.Compiled);
        private static readonly Regex JsonFence = new(@"```(?:json)?\s*(.*?)\s*```", RegexOptions.Singleline | RegexOptions.Compiled);

        public string Render(string template, IDictionary<string, object> context)
        {
            if (string.IsNullOrWhiteSpace(template)) return string.Empty;
            return Placeholder.Replace(template, m =>
            {
                var key = m.Groups[1].Value;
                if (!context.TryGetValue(key, out var value)) return m.Value;
                return value switch
                {
                    string s => s,
                    null => string.Empty,
                    _ => JsonSerializer.Serialize(value)
                };
            });
        }

        public async Task<ParseResult<T>> ParseAsync<T>(string llmOutput, CancellationToken ct = default) where T : class
        {
            var raw = llmOutput?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(raw))
                return new ParseResult<T> { Success = false, Error = "LLM 输出为空", RawText = raw };

            var fence = JsonFence.Match(raw);
            var jsonText = fence.Success ? fence.Groups[1].Value.Trim() : raw;

            // 兜底：取首个 { 到最后一个 } 的子串
            if (!jsonText.StartsWith("{") && !jsonText.StartsWith("["))
            {
                var firstBrace = jsonText.IndexOf('{');
                var lastBrace = jsonText.LastIndexOf('}');
                if (firstBrace >= 0 && lastBrace > firstBrace)
                    jsonText = jsonText.Substring(firstBrace, lastBrace - firstBrace + 1);
            }

            try
            {
                var value = JsonSerializer.Deserialize<T>(jsonText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (value == null)
                    return new ParseResult<T> { Success = false, Error = "反序列化为 null", RawText = jsonText };
                return new ParseResult<T> { Success = true, Value = value, RawText = jsonText };
            }
            catch (JsonException ex)
            {
                return new ParseResult<T> { Success = false, Error = $"JSON 解析失败: {ex.Message}", RawText = jsonText };
            }
        }
    }
}

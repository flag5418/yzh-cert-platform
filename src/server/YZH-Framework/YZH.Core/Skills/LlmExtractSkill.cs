using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using YZH.Core.AI.Clients;
using YZH.Core.AI.Clients.Models;
using YZH.Core.AI.Prompt;
using YZH.Core.AI.Prompt.Models;
using YZH.Core.Workflow;

namespace YZH.Core.Skills
{
    /// <summary>
    /// LLM 文档提取 Skill：渲染提示词 → 调 LLM → 解析结构化输出。
    /// SkillCode = "llm_extract"。
    /// <para>输出 fields/tables 保留 LLM 返回的原始 JSON 结构（字典列表，嵌套值已从 JsonElement
    /// 深转换为普通对象/数组），可同时兼容分析场景（field_name_cn / field_name_en / extracted_value）
    /// 与提取场景（field_code / field_value / rows）。</para>
    /// </summary>
    public class LlmExtractSkill : ISkillNode
    {
        public string SkillCode => "llm_extract";

        private readonly ILlmClient _llm;
        private readonly IPromptInterpreter _interpreter;

        public LlmExtractSkill(ILlmClient llm, IPromptInterpreter interpreter)
        {
            _llm = llm;
            _interpreter = interpreter;
        }

        public async Task<SkillResult> ExecuteAsync(SkillContext context, CancellationToken ct = default)
        {
            var doc = context.Inputs.TryGetValue("document_content", out var d) ? d?.ToString() ?? string.Empty : string.Empty;
            var template = context.Inputs.TryGetValue("prompt", out var p) ? p?.ToString() ?? string.Empty : string.Empty;

            if (string.IsNullOrWhiteSpace(template))
                return new SkillResult { Success = false, Error = "缺少 prompt 入参" };

            var baseRender = _interpreter.Render(template, new RenderContext(new Dictionary<string, object>
            {
                ["document_content"] = doc,
                ["fields_json"] = context.Inputs.TryGetValue("fields_json", out var f) ? f : string.Empty,
                ["tables_json"] = context.Inputs.TryGetValue("tables_json", out var t) ? t : string.Empty
            }));

            LlmResponse? resp = null;
            ParseResult<AiExtractionResult>? parsed = null;

            for (var attempt = 0; attempt < 2; attempt++)
            {
                var prompt = attempt == 0 ? baseRender
                    : baseRender + "\n\n注意：上一轮输出存在 JSON 格式错误。请仅输出符合要求 Schema 的 JSON，不要包含任何解释文字或 Markdown 围栏。";
                resp = await _llm.CompleteAsync(new LlmRequest
                {
                    Messages = new List<LlmMessage>
                    {
                        new() { Role = "system", Content = "你是专业的文档信息提取助手，只输出 JSON。" },
                        new() { Role = "user", Content = prompt }
                    },
                    JsonMode = true,
                    // 分析/提取模式的输出 JSON 可能较大（含 extracted_data / rows 数据预览），
                    // 默认 MaxTokens=4096 会在字符串中间截断，导致 JSON 解析失败（复现报错：
                    // "Expected end of string... Path: $.tables[i].extracted_data"）。
                    // 提高到 8192 并放宽单次调用超时，保证长输出能完整返回。
                    MaxTokens = 8192,
                    TimeoutSeconds = 120
                }, ct);

                if (!resp.Success) return new SkillResult { Success = false, Error = resp.Error };
                parsed = await _interpreter.ParseAsync<AiExtractionResult>(resp.Content, ct);
                if (parsed.Success) break;
            }

            if (parsed == null || !parsed.Success)
                return new SkillResult { Success = false, Error = parsed?.Error ?? "LLM 输出 JSON 解析两次均失败" };

            var fields = (parsed.Value.Fields ?? new List<Dictionary<string, object>>())
                .Select(NormalizeDict).ToList();
            var tables = (parsed.Value.Tables ?? new List<Dictionary<string, object>>())
                .Select(NormalizeDict).ToList();

            var confidence = fields.Count == 0 ? 0.0 :
                fields
                    .Select(f => f.TryGetValue("confidence", out var c) && double.TryParse(c?.ToString(), out var cv) ? (double?)cv : null)
                    .Where(c => c.HasValue).Select(c => c!.Value)
                    .DefaultIfEmpty(1.0)
                    .Min();

            return new SkillResult
            {
                Success = true,
                Outputs = new Dictionary<string, object>
                {
                    ["fields"] = fields,
                    ["tables"] = tables,
                    ["raw_json"] = parsed.RawText ?? string.Empty
                },
                Confidence = confidence
            };
        }

        /// <summary>将反序列化得到的 JsonElement 值深转换为普通对象/数组/字典，供上层直接消费。</summary>
        private static Dictionary<string, object> NormalizeDict(IDictionary<string, object> dict) =>
            dict.ToDictionary(kv => kv.Key, kv => NormalizeValue(kv.Value));

        private static object NormalizeValue(object value) => value switch
        {
            JsonElement je => NormalizeJson(je),
            IDictionary<string, object> d => NormalizeDict(d),
            IEnumerable<object> list => list.Select(NormalizeValue).ToList(),
            _ => value
        };

        private static object NormalizeJson(JsonElement e) => e.ValueKind switch
        {
            JsonValueKind.Object => e.EnumerateObject().ToDictionary(p => p.Name, p => NormalizeJson(p.Value)),
            JsonValueKind.Array => e.EnumerateArray().Select(NormalizeJson).ToList(),
            JsonValueKind.String => e.GetString() ?? string.Empty,
            JsonValueKind.Number => e.TryGetInt64(out var l) ? (object)l : e.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null!,
            _ => e.GetRawText()
        };
    }

    public class AiExtractionResult
    {
        public List<Dictionary<string, object>>? Fields { get; set; }
        public List<Dictionary<string, object>>? Tables { get; set; }
    }
}

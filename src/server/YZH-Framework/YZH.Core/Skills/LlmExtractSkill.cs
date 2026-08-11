using System;
using System.Collections.Generic;
using System.Linq;
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
                    JsonMode = true
                }, ct);

                if (!resp.Success) return new SkillResult { Success = false, Error = resp.Error };
                parsed = await _interpreter.ParseAsync<AiExtractionResult>(resp.Content, ct);
                if (parsed.Success) break;
            }

            if (parsed == null || !parsed.Success)
                return new SkillResult { Success = false, Error = parsed?.Error ?? "LLM 输出 JSON 解析两次均失败" };

            var fields = parsed.Value.Fields ?? new List<AiField>();
            var confidence = fields.Count == 0 ? 0.0 :
                fields.Where(f => f.Confidence.HasValue).DefaultIfEmpty(new AiField { Confidence = 1.0 }).Min(f => f.Confidence!.Value);

            return new SkillResult
            {
                Success = true,
                Outputs = new Dictionary<string, object>
                {
                    ["fields"] = fields,
                    ["tables"] = parsed.Value.Tables ?? new List<AiTable>(),
                    ["raw_json"] = parsed.RawText ?? string.Empty
                },
                Confidence = confidence
            };
        }
    }

    public class AiField
    {
        public string FieldCode { get; set; } = string.Empty;
        public object? FieldValue { get; set; }
        public double? Confidence { get; set; }
        public object? PositionInfo { get; set; }
    }

    public class AiTable
    {
        public string TableCode { get; set; } = string.Empty;
        public List<List<string>> Rows { get; set; } = new();
        public double? Confidence { get; set; }
    }

    public class AiExtractionResult
    {
        public List<AiField>? Fields { get; set; }
        public List<AiTable>? Tables { get; set; }
    }
}


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CertPlatform.Admin.Services.Workflow.Skills;
using CertPlatform.Shared.DocExtraction;

namespace CertPlatform.Admin.Services.Workflow.Skills
{
    /// <summary>
    /// LLM 提取 Skill（DI 容器注册式，ISkillNode 回退通道）
    /// <para>移植自：旧 YZH.Core.Skills.LlmExtractSkill.cs</para>
    /// <para>迁移改写：旧 ILlmClient + IPromptInterpreter → 新 LlmInvokeService（OpenAI 兼容直连，</para>
    /// <para>ForceJson=true 时返回体自带 JSON 解析；两次重试的提示词补正逻辑保留）</para>
    /// <para>模型配置：由调用方从 cert_sys_config 六键注入 __model/__base_url/__api_key（费用可控，与旧版一致）</para>
    /// </summary>
    public class LlmExtractSkill : ISkillNode
    {
        public string SkillCode => "llm_extract";

        private readonly CertPlatform.Shared.DocExtraction.LlmInvokeService _llm;

        public LlmExtractSkill(CertPlatform.Shared.DocExtraction.LlmInvokeService llm)
        {
            _llm = llm;
        }

        public async Task<SkillResult> ExecuteAsync(SkillContext context, CancellationToken ct = default)
        {
            var doc = context.Inputs.TryGetValue("document_content", out var d) ? d?.ToString() ?? string.Empty : string.Empty;
            var template = context.Inputs.TryGetValue("prompt", out var p) ? p?.ToString() ?? string.Empty : string.Empty;

            if (string.IsNullOrWhiteSpace(template))
                return new SkillResult { Success = false, Error = "缺少 prompt 入参" };

            // 从上下文读取外部注入的模型配置（由调用方从系统参数 cert_sys_config 六键注入）
            // 确保实际调用的模型与系统参数一致，费用可控
            var model = context.Inputs.TryGetValue("__model", out var m) && !string.IsNullOrWhiteSpace(m?.ToString())
                ? m.ToString()!
                : "qwen-turbo"; // 兜底默认值
            var baseUrl = context.Inputs.TryGetValue("__base_url", out var b) ? b?.ToString() ?? string.Empty : string.Empty;
            var apiKey = context.Inputs.TryGetValue("__api_key", out var k) ? k?.ToString() ?? string.Empty : string.Empty;

            // 渲染提示词（旧 IPromptInterpreter.Render 的 {key} 替换语义）
            var baseRender = Render(template, new Dictionary<string, object>
            {
                ["document_content"] = doc,
                ["fields_json"] = context.Inputs.TryGetValue("fields_json", out var f) ? f : string.Empty,
                ["tables_json"] = context.Inputs.TryGetValue("tables_json", out var t) ? t : string.Empty
            });

            CertPlatform.Shared.DocExtraction.LlmInvokeResponse? resp = null;
            JsonDocument? parsed = null;
            string? parseError = null;

            for (var attempt = 0; attempt < 2; attempt++)
            {
                var prompt = attempt == 0 ? baseRender
                    : baseRender + "\n\n注意：上一轮输出存在 JSON 格式错误。请仅输出符合要求 Schema 的 JSON，不要包含任何解释文字或 Markdown 围栏。";

                resp = await _llm.CompleteAsync(new CertPlatform.Shared.DocExtraction.LlmInvokeRequest
                {
                    BaseUrl = baseUrl,
                    ApiKey = apiKey,
                    Model = model,
                    Prompt = prompt,
                    DocumentContent = "",
                    // 分析/提取模式的输出 JSON 可能较大（含 extracted_data / rows 数据预览），
                    // 默认 MaxTokens=4096 会在字符串中间截断，导致 JSON 解析失败（旧版注释保留）。
                    // 提高到 8192 保证长输出能完整返回。
                    MaxTokens = 8192,
                    ForceJson = true
                });

                if (!resp.Success) return new SkillResult { Success = false, Error = resp.Message };

                if (resp.Json != null)
                {
                    parsed = JsonDocument.Parse(resp.Json.RootElement.GetRawText());
                    parseError = null;
                    break;
                }

                parseError = resp.Message;
            }

            if (parsed == null)
                return new SkillResult { Success = false, Error = parseError ?? "LLM 输出 JSON 解析两次均失败" };

            // 解析 fields / tables 数组（深转换 JsonElement → 普通对象）
            var fields = ExtractArray(parsed.RootElement, "fields");
            var tables = ExtractArray(parsed.RootElement, "tables");

            var confidence = fields.Count == 0 ? 0.0 :
                fields
                    .Where(f => f is Dictionary<string, object> fd && fd.TryGetValue("confidence", out var c))
                    .Select(f => double.TryParse(((Dictionary<string, object>)f)["confidence"]?.ToString(), out var cv) ? (double?)cv : null)
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
                    ["raw_json"] = parsed.RootElement.GetRawText(),
                    ["prompt_tokens"] = resp?.PromptTokens ?? 0,
                    ["completion_tokens"] = resp?.CompletionTokens ?? 0,
                },
                Confidence = confidence,
                PromptTokens = resp?.PromptTokens,
                CompletionTokens = resp?.CompletionTokens
            };
        }

        /// <summary>旧 IPromptInterpreter.Render 的 {key} 替换语义（缺 key 保留原文占位）</summary>
        private static string Render(string template, Dictionary<string, object> values)
        {
            if (string.IsNullOrEmpty(template)) return template ?? string.Empty;
            var result = template;
            foreach (var (key, value) in values)
            {
                var vStr = value switch
                {
                    string s => s,
                    null => string.Empty,
                    _ => JsonSerializer.Serialize(value)
                };
                result = result.Replace("{" + key + "}", vStr);
            }
            return result;
        }

        /// <summary>从根对象提取指定数组属性，深转换为普通对象列表</summary>
        private static List<object> ExtractArray(JsonElement root, string propertyName)
        {
            var list = new List<object>();
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty(propertyName, out var arr) &&
                arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in arr.EnumerateArray())
                    list.Add(NormalizeJson(item));
            }
            return list;
        }

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
}

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CertPlatform.Admin.Services.Workflow
{
    /// <summary>
    /// 提示词渲染器 — 替代旧架构 YZH.Core.AI.Prompt.IPromptInterpreter
    /// <para>移植自：旧 PromptInterpreter.cs 的 Render + ParseAsync 核心逻辑</para>
    /// <para>占位符 {{name}}：支持中文、字母、数字、下划线、点号（如 {{文档字段.result}}）</para>
    /// </summary>
    public static class PromptRenderer
    {
        /// <summary>{{name}} 占位符正则（与旧版正则一致，AiNodeExecutor.ResolveTemplateRefs 依赖同一规则）</summary>
        public static readonly Regex Placeholder = new(
            @"\{\{([\w\u4e00-\u9fff][\w\u4e00-\u9fff.]*)\}\}", RegexOptions.Compiled);

        private static readonly Regex JsonFence = new(
            @"```(?:json)?\s*(.*?)\s*```", RegexOptions.Singleline | RegexOptions.Compiled);

        /// <summary>
        /// 渲染模板：将 {{key}} 替换为 context 中的值
        /// <para>string 直接替换；null 替换为空串；其他类型 JSON 序列化后替换；未命中保留原文</para>
        /// </summary>
        public static string Render(string? template, IDictionary<string, object> context)
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

        /// <summary>
        /// 从 LLM 输出中提取 JSON 文本（剥 ```json 围栏 + 首尾大括号兜底）
        /// <para>返回 (是否成功, JSON 文本或原始文本, 错误信息)</para>
        /// </summary>
        public static (bool ok, string jsonText, string? error) ExtractJson(string? llmOutput)
        {
            var raw = llmOutput?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(raw))
                return (false, raw, "LLM 输出为空");

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

            return (true, jsonText, null);
        }

        /// <summary>
        /// 从 LLM 输出反序列化为强类型（旧 ParseAsync 等价物）
        /// </summary>
        public static (T? value, string? error, string rawText) ParseJson<T>(string? llmOutput) where T : class
        {
            var (ok, jsonText, extractError) = ExtractJson(llmOutput);
            if (!ok) return (null, extractError, jsonText);

            try
            {
                var value = JsonSerializer.Deserialize<T>(jsonText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (value == null)
                    return (null, "反序列化为 null", jsonText);
                return (value, null, jsonText);
            }
            catch (JsonException ex)
            {
                return (null, $"JSON 解析失败: {ex.Message}", jsonText);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CertPlatform.Shared.DocExtraction
{
    /// <summary>
    /// ★ V4 章节级产物（<c>ModeOutput.Sections[]</c>）的唯一构建器。
    ///
    /// <para><b>为什么骨架由代码建</b>：文档有哪些章节、每章在原文的字符区间，都是画像
    /// <c>Toc</c> 里的客观属性（确定性、零模型成本）。模型只负责给「章节概要」（<c>Summary</c>）；
    /// ⛔ 不让模型自报章节与偏移 —— 那是「少写一节、偏移错位」的根源。</para>
    ///
    /// <para>输出形状：<c>Sections: [{ Label, Summary, CharStart, CharEnd }]</c>。
    /// 审核员视图取 <c>Summary</c>（短）；报告期按 <c>CharStart/CharEnd</c> 回原文逐字引用（长）。
    /// 详见 docs/80-v4/17-文档分类：结构指纹与角色定位-V1.md §七（双轨设计）。</para>
    /// </summary>
    public static class DocSectionsBuilder
    {
        /// <summary>章节上限（防超大目录把投影挤爆）。</summary>
        private const int MaxSections = 40;

        /// <summary>只把「一级/二级」标题当章节（更深的多是列表项/条款行）。</summary>
        private const int MaxLevel = 2;

        /// <summary>
        /// 按画像 <c>Toc</c> 生成 <c>Sections[]</c>；模型已直接给 <c>Sections</c> 则不覆盖。
        /// 无 <c>Toc</c> ⇒ 原样返回（⛔ 不凭空造章节）。
        /// </summary>
        public static JsonElement Attach(JsonElement output, string? docProfileJson)
        {
            if (output.ValueKind != JsonValueKind.Object) return output;
            if (output.TryGetProperty("Sections", out var existing)
                && existing.ValueKind == JsonValueKind.Array && existing.GetArrayLength() > 0)
                return output; // 模型已给章节（新提示词），⛔ 不覆盖

            var toc = ReadToc(docProfileJson);
            if (toc.Count == 0) return output;

            var summaries = ReadSectionSummaries(output);
            var arr = new JsonArray();
            var used = new HashSet<string>(StringComparer.Ordinal);
            foreach (var t in toc)
            {
                if (arr.Count >= MaxSections) break;
                var label = (t.Title ?? string.Empty).Trim();
                if (label.Length == 0 || !used.Add(label)) continue;

                summaries.TryGetValue(label, out var summary);
                arr.Add(new JsonObject
                {
                    ["Label"] = label,
                    ["Summary"] = summary ?? string.Empty,
                    ["CharStart"] = t.CharStart,
                    ["CharEnd"] = t.CharEnd,
                });
            }
            if (arr.Count == 0) return output;

            var node = JsonNode.Parse(output.GetRawText())?.AsObject();
            if (node == null) return output;
            node["Sections"] = arr;
            return JsonSerializer.SerializeToElement(node);
        }

        /// <summary>画像 <c>Toc</c> → 章节骨架（只取 level ≤ 2 且有标题的项）。</summary>
        private static List<TocRow> ReadToc(string? docProfileJson)
        {
            var list = new List<TocRow>();
            if (string.IsNullOrWhiteSpace(docProfileJson)) return list;
            try
            {
                using var doc = JsonDocument.Parse(docProfileJson);
                if (!doc.RootElement.TryGetProperty("Toc", out var toc)
                    || toc.ValueKind != JsonValueKind.Array) return list;

                foreach (var item in toc.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object) continue;
                    var level = item.TryGetProperty("Level", out var lv) && lv.ValueKind == JsonValueKind.Number
                        ? lv.GetInt32() : 1;
                    if (level > MaxLevel) continue;
                    var title = item.TryGetProperty("Title", out var tt) && tt.ValueKind == JsonValueKind.String
                        ? (tt.GetString() ?? string.Empty).Trim() : string.Empty;
                    if (title.Length == 0) continue;
                    var start = item.TryGetProperty("CharStart", out var cs) && cs.ValueKind == JsonValueKind.Number
                        ? cs.GetInt32() : 0;
                    var end = item.TryGetProperty("CharEnd", out var ce) && ce.ValueKind == JsonValueKind.Number
                        ? ce.GetInt32() : 0;
                    list.Add(new TocRow(title, start, end));
                }
            }
            catch (JsonException)
            {
                return new List<TocRow>();
            }
            return list;
        }

        /// <summary>模型产物里的 <c>SectionSummaries</c>（<c>[{Label,Summary}]</c>）→ Label 映射。</summary>
        private static Dictionary<string, string> ReadSectionSummaries(JsonElement output)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var name in new[] { "SectionSummaries", "sectionSummaries" })
            {
                if (!output.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array) continue;
                foreach (var item in arr.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object) continue;
                    var label = Str(item, "Label") ?? Str(item, "label");
                    var summary = Str(item, "Summary") ?? Str(item, "summary");
                    if (label is { Length: > 0 } && summary is { Length: > 0 })
                        map[label] = summary;
                }
                break;
            }
            return map;
        }

        private static string? Str(JsonElement obj, string name)
            => obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
                ? ((v.GetString() ?? string.Empty).Trim() is { Length: > 0 } s ? s : null)
                : null;

        private sealed record TocRow(string Title, int CharStart, int CharEnd);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace CertPlatform.Auditor.Services.Ent
{
    /// <summary>
    /// 文件控制类文档的**确定性目录解析器**。
    ///
    /// <para>模型只负责封面元数据（标题/版本/编制/审核/批准/日期/摘要）；
    /// 目录条目和总数由本类从 Markdown 表格逐行解析，避免模型漏行、算错总数、输出过长。</summary>
    public static class DocControlTableParser
    {
        private static readonly Regex CodeCell = new Regex(@"^[A-Z][A-Z0-9\-]*[0-9]$", RegexOptions.Compiled);
        private static readonly Regex VersionCell = new Regex(@"^[A-Z]/[0-9]+$", RegexOptions.Compiled);
        private static readonly Regex RetentionCell = new Regex(@"^[0-9]+年$|^长期$|^永久$", RegexOptions.Compiled);

        public sealed class Entry
        {
            public int No { get; set; }
            public string Code { get; set; } = "";
            public string Name { get; set; } = "";
            public string Version { get; set; } = "";
            public string Dept { get; set; } = "";
            public string Retention { get; set; } = "";
        }

        /// <summary>把解析出的目录条目和真实总数回填到模型 JSON。</summary>
        public static void Populate(JsonObject root, string markdown)
        {
            var entries = ParseEntries(markdown);
            if (entries.Count == 0) return;

            root["Entries"] = JsonSerializer.SerializeToNode(entries);
            root["EntriesShown"] = entries.Count;

            var counts = root["Counts"] as JsonObject ?? new JsonObject();
            counts["Total"] = entries.Count;
            var kind = root["DocumentKind"]?.GetValue<string>() ?? "";
            if (kind.Contains("记录")) counts["Records"] = entries.Count;
            else if (kind.Contains("程序")) counts["Procedures"] = entries.Count;
            else if (kind.Contains("三阶") || kind.Contains("作业")) counts["WorkInstructions"] = entries.Count;
            root["Counts"] = counts;
        }

        /// <summary>
        /// 解析 Markdown 表格里的目录行：只认带业务编号（KSY-QMS-P-01 / WI-01 等）的行，
        /// 自动识别编号、名称、版本、保存期限；表头/分隔线/重复行跳过。
        /// </summary>
        public static List<Entry> ParseEntries(string markdown)
        {
            var result = new List<Entry>();
            if (string.IsNullOrWhiteSpace(markdown)) return result;

            foreach (var raw in markdown.Replace("\r", "").Split('\n'))
            {
                if (!raw.Contains('|')) continue;

                var cells = raw.Trim().Trim('|').Split('|')
                    .Select(x => x.Trim().Trim('*').Trim())
                    .ToList();
                if (cells.Count < 3) continue;

                var codeIdx = cells.FindIndex(c => CodeCell.IsMatch(c) && c.Any(char.IsDigit));
                if (codeIdx < 0) continue;

                var code = cells[codeIdx];
                var name = PickName(cells, codeIdx);
                var version = cells.FirstOrDefault(c => VersionCell.IsMatch(c)) ?? "未标注";
                var retention = cells.FirstOrDefault(c => RetentionCell.IsMatch(c)) ?? "";
                var dept = cells.FirstOrDefault(c => IsDept(c)) ?? "未标注";

                if (result.Any(e => string.Equals(e.Code, code, StringComparison.OrdinalIgnoreCase)))
                    continue;

                result.Add(new Entry
                {
                    No = result.Count + 1,
                    Code = code,
                    Name = name,
                    Version = version,
                    Dept = dept,
                    Retention = retention,
                });
            }

            return result;
        }

        private static string PickName(List<string> cells, int codeIdx)
        {
            var candidates = new List<int>();
            for (var i = 0; i < cells.Count; i++)
            {
                var c = cells[i];
                if (i == codeIdx || string.IsNullOrWhiteSpace(c)) continue;
                if (!c.Any(ch => ch >= '\u4e00' && ch <= '\u9fff')) continue;
                if (IsHeader(c) || VersionCell.IsMatch(c) || RetentionCell.IsMatch(c)) continue;
                if (IsDept(c) && c.Length <= 6) continue;
                candidates.Add(i);
            }

            if (candidates.Count == 0) return "未标注";
            var best = candidates.OrderBy(i => Math.Abs(i - codeIdx)).First();
            return cells[best];
        }

        private static bool IsHeader(string c) => c is "序号" or "文件名称" or "记录名称" or "版本"
            or "文件编号" or "记录编号" or "保存期限" or "备注" or "保存部门" or "文件类别";

        private static bool IsDept(string c)
        {
            var t = (c ?? string.Empty).Trim();
            if (t.Length is < 2 or > 12) return false;
            return t.EndsWith("部") || t.EndsWith("科") || t.EndsWith("中心")
                   || t.EndsWith("车间") || t.EndsWith("仓库") || t.EndsWith("办公室")
                   || t is "管理层" or "总经理" or "各部门";
        }
    }
}

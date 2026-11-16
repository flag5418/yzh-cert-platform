using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace CertPlatform.Auditor.Services.Ent
{
    /// <summary>
    /// 记录类文档的「表格数据识别」与「条数口径」护栏（后台体系级规则，看不到机构/企业文件/审核计划）。<para>
    /// 三条口径：
    /// ① 一行算不算记录：首列必须有值，且除首列外至少还有一个字段有值；占位符（— / 无 / N/A）不算值；
    ///    合计/小计/备注/编制/审核 这类结构行不是数据行。
    /// ② <c>RowCount</c> 一律由代码重算，不采信模型自报：有 <c>Rows</c> 就数清洗后的行，没有就退到样本条数并告警；
    ///    什么都数不出来 ⇒ <c>RecordState=blank</c> + <c>no_data_rows</c>，不许把空表格当记录。
    /// ③ 摘要里出现「等 X」而实际只有 1 条对象时，只写 <c>summary_plural_mismatch</c> 告警，**不改原文**。
    /// </para>
    /// </summary>
    public static class RecordQualityGuard
    {
        private static readonly string[] Placeholders =
            { "", "—", "-", "－", "/", "\\", "无", "空", "n/a", "N/A", "null", "未填", "未填写", "见附" };

        private static readonly string[] StructuralPrefixes =
            { "合计", "小计", "总计", "累计", "备注", "说明", "注：", "注:", "填表", "编制", "审核人", "批准人", "以下空白" };

        private static readonly Regex PluralClaim =
            new("等(供应商|供方|客户|顾客|部门|批次|产品|物料|材料|设备|人员|记录|项目|合同|订单|证书|岗位|车间|工序)",
                RegexOptions.Compiled);

        private const string PluralWord = "多家";

        public static JsonElement Apply(string mode, JsonElement output)
        {
            if (mode != EnterpriseDocExtractionRouter.RecordProfile) return output;
            if (output.ValueKind != JsonValueKind.Object) return output;
            var node = JsonNode.Parse(output.GetRawText())?.AsObject();
            if (node == null) return output;

            var warnings = node["Warnings"] as JsonArray ?? new JsonArray();
            node["Warnings"] = warnings;

            var records = node["Records"] as JsonArray;
            if (records == null) return JsonSerializer.SerializeToElement(node);

            var summary = node["ReportSummary"]?.ToString() ?? string.Empty;

            foreach (var item in records)
            {
                if (item is not JsonObject record) continue;
                var name = record["RecordName"]?.ToString() ?? "记录";
                // 告警两级都写：记录级便于在单份记录上定位，顶层便于整份文档一眼看完
                var recWarn = record["Warnings"] as JsonArray ?? new JsonArray();
                record["Warnings"] = recWarn;

                var keptRows = Filter(record, "Rows", warnings, recWarn, name);
                var keptSamples = Filter(record, "Samples", warnings, recWarn, name);
                RecomputeRowCount(record, name, keptRows, keptSamples, warnings, recWarn);

                // 该记录的对象条数：优先清洗后的行数，其次样本数
                var objects = keptRows > 0 ? keptRows : keptSamples;
                if (objects == 1 && HasPluralClaim(summary))
                    Warn(warnings, recWarn, "summary_plural_mismatch",
                        $"摘要写「等/多家」暗示多条，但 {name} 实际只有 1 条对象；原文不改，报告引用时按 1 条写");
            }

            return JsonSerializer.SerializeToElement(node);
        }

        /// <summary>清洗 Rows/Samples：丢掉空行、占位行、结构行，返回留下的条数。</summary>
        private static int Filter(JsonObject record, string field, JsonArray warnings, JsonArray recWarn, string recordName)
        {
            if (record[field] is not JsonArray rows) return 0;

            var kept = new JsonArray();
            var dropped = 0;
            var blankFirst = 0;
            foreach (var row in rows)
            {
                var verdict = Judge(row);
                if (verdict == RowVerdict.Keep) kept.Add(row?.DeepClone());
                else
                {
                    dropped++;
                    if (verdict == RowVerdict.NoFirstValue) blankFirst++;
                }
            }

            if (dropped > 0)
            {
                record[field] = kept;
                Warn(warnings, recWarn, field == "Rows" ? "blank_rows_dropped" : "blank_samples_dropped",
                    $"{recordName} 的 {field} 删掉 {dropped} 条无数据行（其中首列空/占位 {blankFirst} 条），保留 {kept.Count} 条");
            }
            return kept.Count;
        }

        private enum RowVerdict { Keep, NoFirstValue, Struct, TooSparse }

        private static RowVerdict Judge(JsonNode? row)
        {
            if (row is not JsonObject obj) return RowVerdict.TooSparse;

            // ⚠️ Values 有两种结构：数组（无表头时按栏位顺序）与对象（有表头时键=字段名）。
            //   只认数组会把「供应商名称/材料类别/总得分…」这种对象结构整行判成空 ⇒ 假阳性清空。
            var values = obj["Values"];
            if (values is JsonArray arr) return JudgeCells(arr.Select(x => x?.ToString()).ToList());
            if (values is JsonObject map)
                return JudgeCells(map.Select(kv => kv.Value?.ToString()).ToList());
            if (values is JsonValue scalar && !string.IsNullOrWhiteSpace(scalar.ToString()) && !string.IsNullOrEmpty(obj["Conclusion"]?.ToString()))
                return RowVerdict.Keep; // 极简记录：只有一个值 + 结论
            return RowVerdict.NoFirstValue;
        }

        private static RowVerdict JudgeCells(List<string?> cells)
        {
            if (cells.Count == 0) return RowVerdict.NoFirstValue;

            var first = (cells[0] ?? string.Empty).Trim();
            if (!HasValue(first)) return RowVerdict.NoFirstValue;
            if (StructuralPrefixes.Any(p => first.StartsWith(p, StringComparison.Ordinal))) return RowVerdict.Struct;

            var filled = cells.Count(c => HasValue(c));

            // 单列表格：首列有值即可；多列表格：至少要再有一个字段有值，否则是模板空行
            var need = cells.Count >= 2 ? 2 : 1;
            return filled >= need ? RowVerdict.Keep : RowVerdict.TooSparse;
        }

        /// <summary>RowCount 由代码定：目录/集合类不重算（它们用 Catalog.Count / RecordSet.Members）。</summary>
        private static void RecomputeRowCount(JsonObject record, string name, int rows, int samples,
            JsonArray warnings, JsonArray recWarn)
        {
            var clsName = record["Catalog"] != null ? "Catalog" : record["RecordSet"] != null ? "RecordSet" : null;
            if (clsName != null) return;

            var claimed = record["RowCount"] is JsonValue v && v.TryGetValue<int>(out var c) ? c : (int?)null;

            if (rows > 0)
            {
                record["RowCount"] = rows;
                if (claimed != null && claimed != rows)
                    Warn(warnings, recWarn, "row_count_recomputed", $"{name} 模型报 {claimed} 条，按有数据的行重算为 {rows} 条");
                return;
            }

            // 没有可核对的 Rows：退到样本条数，并明确标注口径
            if (samples > 0)
            {
                record["RowCount"] = samples;
                Warn(warnings, recWarn, "row_count_from_samples",
                    $"{name} 未给出 Rows，只有 {samples} 条样本 ⇒ RowCount 按样本条数记 {samples}（非总行数）");
                return;
            }

            record["RowCount"] = 0;

            // ⚠️ 判"blank"要克制：没抽到行有两种成因——原文确实是空表格，或者行没被模型抽出来。
            //   只有连结论/签署都没有（整份记录一片空白）才敢断言 blank；否则只把条数归零 + 告警，
            //   由审核员现场核实，避免把"抽取漏行"说成"文档空白"。
            var hasAnyConclusion = HasValue(record["Conclusion"]?.ToString());
            var hasAnySignoff = (record["Signoff"] as JsonArray)?.Any(x => HasValue(x?.ToString())) == true;
            if (!hasAnyConclusion && !hasAnySignoff) record["RecordState"] = "blank";

            Warn(warnings, recWarn, "no_data_rows",
                $"{name} 没有取到有数据的行（原文可能是空表格，也可能行没被抽出）⇒ 条数记 0，需现场核实");
        }

        private static bool HasValue(string? s)
        {
            var t = (s ?? string.Empty).Trim();
            return !Placeholders.Contains(t, StringComparer.Ordinal);
        }

        private static bool HasPluralClaim(string summary)
            => summary.Contains(PluralWord, StringComparison.Ordinal) || PluralClaim.IsMatch(summary);

        private static void Warn(JsonArray warnings, JsonArray recWarn, string code, string message)
        {
            var entry = new JsonObject { ["Code"] = code, ["Message"] = message };
            warnings.Add(entry.DeepClone());
            recWarn.Add(entry);
        }
    }
}

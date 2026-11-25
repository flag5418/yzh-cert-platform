using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using CertPlatform.Auditor.Services.Ent;

namespace CertPlatform.Auditor.Services.Expert
{
    /// <summary>
    /// <c>ContentJson.ModeOutput</c>（记录/过程/制度/索引等模式产物）的**唯一投影器**。
    ///
    /// <para><b>为什么需要单独一类</b>：模式提取把事实放在 <c>ModeOutput</c>，
    /// 而 <c>Clauses[]</c> 是空的。凡是要"给人看 or 给模型看"这两处
    /// （证据行 <c>CertEvidenceRow</c> / 条款上下文 <c>CertCheckContext</c>）都必须走这里，
    /// ⛔ 不许各自 <c>ToString()</c> 再截断 —— 会各截各的，且截的位置不一样。</para>
    ///
    /// <para><b>投影顺序按"审核员要看什么"排</b>：先编号与结论，再抽样与签署；
    /// <c>FormFields</c> 这类长枚举**不进摘录**（它只用于看表结构，不承载事实）。</para>
    /// </summary>
    public static class ModeOutputProjector
    {
        /// <summary>整段摘录上限（给模型看）。</summary>
        public const int MaxExcerptChars = 3000;

        /// <summary>单条记录摘录上限（给模型看）。</summary>
        public const int MaxRecordChars = 1200;

        /// <summary>
        /// 投影前的读时校正。<para>
        /// <b>为什么要有</b>：<c>RecordQualityGuard</c> 只在新抽取时跑，存量产物里模型自报的
        /// <c>RowCount=7</c>（实际 1 家）和空表格照样会流进证据行/模型上下文。这里在读取时补齐，
        /// 不动库、不需要重抽，且与写入时用的是同一套规则（幂等）。
        /// </para>
        /// </summary>
        public static JsonElement ForProjection(JsonElement output, string? mode)
        {
            if (output.ValueKind != JsonValueKind.Object) return output;
            if (!string.Equals(mode, EnterpriseDocExtractionRouter.RecordProfile, StringComparison.Ordinal))
                return output;
            return RecordQualityGuard.Apply(EnterpriseDocExtractionRouter.RecordProfile, output);
        }

        /// <summary>模式产物 → 报告可用摘录。空串表示"这条没有可用内容"。</summary>
        public static string Excerpt(JsonElement output, string? mode)
        {
            var parts = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            void Add(string label, JsonElement value, int maxItems = 3)
            {
                if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return;
                var text = Text(value, maxItems);
                if (string.IsNullOrWhiteSpace(text)) return;
                var body = text.Trim();
                // 同一段事实重复出现（ReportSummary / ReportUse 常见同文）只留一次
                if (!seen.Add(body)) return;
                parts.Add($"{label}：{body}");
            }

            if (output.ValueKind != JsonValueKind.Object) return string.Empty;

            foreach (var name in new[] { "ReportSummary", "reportSummary" })
                if (output.TryGetProperty(name, out var v)) Add("摘要", v);

            // ⛔ 「报告引用口径」不进证据摘录：那是给下游报告索引用的内部口径
            //    （"是否被引用由后续索引判定"），审核员读到只会以为报告没写清。

            foreach (var name in new[] { "DocumentTitle", "documentTitle", "PolicyTitle", "policyTitle", "CatalogSummary", "catalogSummary" })
                if (output.TryGetProperty(name, out var v)) Add("文档信息", v);

            foreach (var name in new[] { "KeyControls", "keyControls", "KeyRules", "keyRules", "KeySteps", "keySteps" })
                if (output.TryGetProperty(name, out var v)) Add("关键要求/步骤", v, 5);

            // 手册章节模式（manual_chapter）：手册「整篇一次」按章节组织，Chapters 是**对象数组**。
            // 逐章印「章号 标题：摘要（要点）」；非对象（其他模式的字符串章节号）由 ChaptersText 返回空，不误伤。
            foreach (var name in new[] { "Chapters", "chapters" })
            {
                if (!output.TryGetProperty(name, out var chEl)) continue;
                var chText = ChaptersText(chEl, 6).Trim();
                if (chText.Length > 0 && seen.Add(chText)) parts.Add(chText);
                break;
            }

            foreach (var name in new[] { "Findings", "findings", "Records", "records", "Entries", "entries", "IndexEntries", "indexEntries" })
                if (output.TryGetProperty(name, out var v)) Add("记录摘录", v, 3);

            if (mode == "document_control")
                if (output.TryGetProperty("Entries", out var entries)) Add("目录摘要", entries, 2);

            // 编号与签署：条款层要拿它做定位与"谁批准的"（97 号 G3 / AF-3）。
            // ⚠️ 全空时跳过：投影一段 {"DocCode":"","DocVersion":""…} 既占字符，又容易被读成"编号提取失败"。
            if (output.TryGetProperty("Metadata", out var meta) && HasAnyValue(meta)) Add("表头", meta, 1);
            if (output.TryGetProperty("ProcessArea", out var area)) Add("过程域", area, 1);

            var text = string.Join("\n", parts);
            return text.Length <= MaxExcerptChars ? text : text[..MaxExcerptChars] + "…";
        }

        /// <summary>
        /// 单条记录 / 单条抽样投影：按 <c>RecordName → 抽样 → 签署 → 结论</c> 拼串。
        /// <para>⛔ 不直接 <c>ToString()</c>：<c>FormFields</c> 一长就把 <c>Samples</c>/<c>Signoff</c>/<c>Conclusion</c>
        /// 挤出截断窗口，于是"文档层明明抽到了三部门签署，下游却永远看不到"（97 号 AF-6/7/8）。</para>
        /// <para>抽样行（<c>Date</c>/<c>Batch</c>/<c>Values</c>）与记录行（<c>RecordName</c>/<c>RowCount</c>）走同一入口，
        /// ⛔ 别让抽样行的 <c>Values</c> 丢在投影外面 —— 那正是"供应商名称/规格/日期"。</para>
        /// </summary>
        public static string? RecordText(JsonElement obj)
        {
            if (obj.ValueKind != JsonValueKind.Object) return null;

            var isSample = obj.TryGetProperty("Values", out _) || obj.TryGetProperty("Date", out _);
            var isRecord = obj.TryGetProperty("RecordName", out _)
                           || obj.TryGetProperty("RecordState", out _)
                           || obj.TryGetProperty("Samples", out _)
                           || obj.TryGetProperty("Signoff", out _);
            if (!isSample && !isRecord) return null;

            if (isSample) return Clip(SampleText(obj));

            // 记录级：按"审核员读到这份记录要知道什么"排 —— 记录名 / 部门 / 过程 / 条数 / 抽查 / 签署 / 结论。
            // ⛔ 不印 RecordName= / RowCount= / RecordState=filled 这类字段名：审核员看不懂，
            //    进报告还得再翻译一遍（2026-11-16 实测证据文本就是这个毛病）。
            var parts = new List<string>();

            void AddLabeled(string label, JsonElement v, int maxItems)
            {
                var t = Text(v, maxItems).Trim();
                if (t.Length > 0) parts.Add($"{label} {t}");
            }

            if (obj.TryGetProperty("RecordName", out var nm)) AddLabeled("记录", nm, 1);
            if (obj.TryGetProperty("OwnerDept", out var od)) AddLabeled("责任部门", od, 1);
            if (obj.TryGetProperty("ProcessName", out var pn)) AddLabeled("所在过程", pn, 1);

            foreach (var name in new[] { "RowCount", "row_count" })
            {
                if (!obj.TryGetProperty(name, out var rc)) continue;
                var count = rc.ValueKind == JsonValueKind.Number ? rc.GetInt32() : 0;
                if (count > 0) parts.Add($"共 {count} 条");
                break;
            }

            foreach (var name in new[] { "Period", "Batch", "Coverage", "DocSection" })
            {
                if (!obj.TryGetProperty(name, out var v)) continue;
                var t = Text(v, 1).Trim();
                if (t.Length == 0) continue;
                var label = name switch
                {
                    "Period" => "期间",
                    "Batch" => "批次",
                    "Coverage" => "覆盖",
                    _ => "章节"
                };
                parts.Add($"{label} {t}");
            }

            // 记录状态只留"有问题"的两种：只有表头 / 行没抽全；filled 不占字（99% 是 filled）
            if (obj.TryGetProperty("RecordState", out var st))
            {
                var s = (st.ValueKind == JsonValueKind.String ? st.GetString() : st.ToString())?.Trim() ?? "";
                if (s.Equals("blank", StringComparison.OrdinalIgnoreCase)) parts.Add("（表内没有数据行，需现场核实）");
                else if (s.StartsWith("partial", StringComparison.OrdinalIgnoreCase)) parts.Add("（仅抽到部分数据行）");
            }

            var hasRecordSignoff = obj.TryGetProperty("Signoff", out var signoff) && HasSignoff(signoff);

            if (obj.TryGetProperty("Samples", out var samples))
            {
                var list = new List<string>();
                if (samples.ValueKind == JsonValueKind.Array)
                {
                    var idx = 0;
                    foreach (var x in samples.EnumerateArray())
                    {
                        if (idx >= 3) break;
                        var seg = x.ValueKind == JsonValueKind.Object ? SampleText(x) : Text(x, 1);
                        if (string.IsNullOrWhiteSpace(seg)) continue;
                        // 记录级签署栏已存在时，样本里的签署不再重复印：同一份记录两处签署
                        // 会让模型读成"签了两次"（97 号 P1 重复问题）。
                        if (hasRecordSignoff) seg = StripSignoff(seg);
                        idx++;
                        list.Add($"{idx}) {seg.Trim()}");
                        if (idx >= 3) break;
                    }
                }
                else
                {
                    var seg = Text(samples, 3);
                    if (!string.IsNullOrWhiteSpace(seg)) list.Add(seg.Trim());
                }
                if (list.Count > 0) parts.Add("抽查 " + string.Join("；", list));
            }

            if (hasRecordSignoff)
            {
                var t = SignoffText(signoff);
                if (!string.IsNullOrWhiteSpace(t)) parts.Add($"签署 {t}");
            }
            if (obj.TryGetProperty("Conclusion", out var cj)) AddLabeled("结论", cj, 1);
            if (obj.TryGetProperty("EvidencePointers", out var ep)) AddLabeled("原文位置", ep, 5);

            if (parts.Count == 0) return null;
            return Clip(string.Join("｜", parts));
        }

        /// <summary>
        /// 单条抽样 → 审核员能读的一行：日期 / 批次 / 值（键值对已拉平）/ 结论 / 签署。
        /// <para>⛔ <c>Values</c> 不许直接 <c>ToString()</c>：那是带 <c>\u987E...</c> 转义的 JSON 原文，
        /// 审核员看到的就是一串编码。</para>
        /// </summary>
        private static string SampleText(JsonElement obj)
        {
            var parts = new List<string>();

            if (obj.TryGetProperty("Date", out var date))
            {
                var t = Text(date, 1).Trim();
                if (t.Length > 0) parts.Add(t);
            }
            foreach (var name in new[] { "Period", "Batch" })
            {
                if (!obj.TryGetProperty(name, out var v)) continue;
                var t = Text(v, 1).Trim();
                if (t.Length > 0) parts.Add($"{(name == "Period" ? "期间" : "批次")} {t}");
            }
            if (obj.TryGetProperty("Values", out var values))
            {
                var t = FlatValues(values);
                if (t.Length > 0) parts.Add(t);
            }
            if (obj.TryGetProperty("Conclusion", out var cj))
            {
                var t = Text(cj, 1).Trim();
                if (t.Length > 0) parts.Add($"结论 {t}");
            }
            if (obj.TryGetProperty("Signoff", out var sg) && HasSignoff(sg))
            {
                var t = SignoffText(sg);
                if (!string.IsNullOrWhiteSpace(t)) parts.Add($"签署 {t}");
            }

            return string.Join("｜", parts);
        }

        /// <summary>样本串里已印过的签署段剔掉（记录级签署只印一次）。</summary>
        private static string StripSignoff(string sampleText)
        {
            var segs = sampleText.Split('｜')
                .Where(x => !x.TrimStart().StartsWith("签署 ", StringComparison.Ordinal))
                .ToArray();
            return string.Join("｜", segs);
        }

        /// <summary>
        /// JSON 值 → 中文键值串（不转义、不带花括号）。
        /// 对象：<c>顾客名称：常州摇旗…；调查日期：2026-08-15</c>；数组：按序用「；」连接。
        /// </summary>
        private static string FlatValues(JsonElement value)
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.String:
                    return (value.GetString() ?? "").Trim();
                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                    return value.ToString();
                case JsonValueKind.Array:
                    var items = value.EnumerateArray()
                        .Select(x => FlatValues(x))
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .ToList();
                    return string.Join("；", items);
                case JsonValueKind.Object:
                    var pairs = new List<string>();
                    foreach (var p in value.EnumerateObject())
                    {
                        var t = FlatValues(p.Value);
                        if (string.IsNullOrWhiteSpace(t)) continue;
                        pairs.Add($"{ChineseKey(p.Name)}：{t}");
                    }
                    return string.Join("；", pairs);
                default:
                    return "";
            }
        }

        /// <summary>
        /// 表头/元数据字段名 → 中文。只翻已知的这十来个，其余原样（不乱猜）。
        /// <para>为什么：<c>DocCode：KSY-QMS-P-15-02；IssuedBy：胥加娟</c> 进报告，
        /// 审核员看到的是英文键名，还得自己猜对应哪一栏。</para>
        /// </summary>
        private static string ChineseKey(string name)
        {
            switch (name)
            {
                case "DocCode": return "文件编号";
                case "DocVersion":
                case "Version": return "版本";
                case "EffectiveDate": return "生效日期";
                case "IssuedBy":
                case "PreparedBy": return "编制";
                case "ReviewedBy": return "审核";
                case "ApprovedBy": return "批准";
                case "IssueDate": return "发布日期";
                case "Revision": return "修订";
                case "Dept":
                case "DeptName": return "部门";
                case "RecordName": return "记录";
                case "Date": return "日期";
                // 过程/指导书产物里的 KeyControls 对象：Control / Clause / Record / Step
                case "Control": return "控制要求";
                case "Clause": return "对应条款";
                case "Record": return "相关记录";
                case "Step": return "步骤";
                case "KeyPoint": return "要点";
                case "EvidencePointers": return "原文位置";
                default: return name;
            }
        }

        /// <summary>
        /// 手册章节模式：<c>Chapters[]</c>（对象数组）→ 人可读的「章号 标题：摘要（要点1；要点2）」。
        /// <para>⛔ 不能直接 <c>ToString()</c>：那会把 JSON 对象原样丢给审核员。
        /// 非对象元素直接跳过 ⇒ 其他模式的「字符串章节号」数组在本投影里自然为空。</para>
        /// </summary>
        private static string ChaptersText(JsonElement arr, int maxItems)
        {
            if (arr.ValueKind != JsonValueKind.Array) return string.Empty;

            var lines = new List<string>();
            foreach (var ch in arr.EnumerateArray())
            {
                if (lines.Count >= maxItems) break;
                if (ch.ValueKind != JsonValueKind.Object) continue;

                var head = string.Join(" ", new[] { Prop(ch, "Section"), Prop(ch, "Title") }
                    .Where(x => x.Length > 0));
                var summary = Prop(ch, "Summary");
                var points = PropPoints(ch);

                var body = summary;
                if (points.Length > 0) body = body.Length > 0 ? $"{body}（{points}）" : $"（{points}）";
                var line = head.Length > 0 ? $"{head}：{body}" : body;
                if (line.Trim().Length > 0) lines.Add(line.Trim());
            }
            return string.Join("\n", lines);
        }

        /// <summary>对象里按名取非空字符串（大小写不敏感）。</summary>
        private static string Prop(JsonElement obj, string name)
        {
            foreach (var p in obj.EnumerateObject())
                if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)
                    && p.Value.ValueKind == JsonValueKind.String)
                    return (p.Value.GetString() ?? "").Trim();
            return string.Empty;
        }

        /// <summary>章要点数组 → 「；」连接的短串（最多 3 条）。</summary>
        private static string PropPoints(JsonElement obj)
        {
            foreach (var p in obj.EnumerateObject())
            {
                if (!string.Equals(p.Name, "Points", StringComparison.OrdinalIgnoreCase)
                    || p.Value.ValueKind != JsonValueKind.Array) continue;
                return string.Join("；", p.Value.EnumerateArray()
                    .Where(x => x.ValueKind == JsonValueKind.String)
                    .Take(3)
                    .Select(x => (x.GetString() ?? "").Trim())
                    .Where(x => x.Length > 0));
            }
            return string.Empty;
        }

        private static string Clip(string text)
            => text.Length <= MaxRecordChars ? text : text[..MaxRecordChars] + "…";

        /// <summary>读 <c>ModeOutput</c> 的条款号（<c>PrimaryClauses</c> + <c>SupportClauses</c>，去重保序）。</summary>
        public static List<string> ClauseNumbers(JsonElement output)
        {
            var list = new List<string>();

            // 兜底/推断出来的条款号一律不进投影：那是代码猜的，不是文档事实（D1 决策）。
            // 存量 JSON 里还留着 ClauseHintInferred=true 的记录，靠这条护栏让它们先失效，等提示词重跑后再按模型给的条款召回。
            if (output.TryGetProperty("ClauseHintInferred", out var inferred) &&
                inferred.ValueKind == JsonValueKind.True)
                return list;

            foreach (var name in new[] { "PrimaryClauses", "primaryClauses", "SupportClauses", "supportClauses" })
            {
                if (!output.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array) continue;
                foreach (var x in arr.EnumerateArray())
                {
                    if (x.ValueKind != JsonValueKind.String) continue;
                    var no = (x.GetString() ?? "").Trim();
                    if (no.Length == 0 || list.Contains(no)) continue;
                    list.Add(no);
                }
            }
            return list;
        }

        /// <summary>
        /// 签署投影：按「部门|姓名」去重，同一个人保留日期最完整的那条。
        /// <para>为什么：清单/评审表的签署区常把制表、审核写成同一人，模型会原样重复输出
        /// （<c>采购部|胥加娟</c> 连出两次、还有一条日期为空），进报告就是"签了两次"（97 号 R3）。</para>
        /// </summary>
        private static bool HasSignoff(JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.Array)
                return value.EnumerateArray().Any(x => !string.IsNullOrWhiteSpace(x.ToString().Trim()));
            if (value.ValueKind == JsonValueKind.String)
                return !string.IsNullOrWhiteSpace(value.GetString());
            return false;
        }

        private static string SignoffText(JsonElement value)
        {
            if (value.ValueKind != JsonValueKind.Array) return Text(value, 8);

            var keep = new List<string>();
            var byPerson = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var x in value.EnumerateArray())
            {
                if (x.ValueKind != JsonValueKind.String) continue;
                var raw = (x.GetString() ?? "").Trim();
                if (raw.Length == 0) continue;

                var segments = raw.Split('|');
                var person = segments.Length >= 2
                    ? segments[0].Trim() + "|" + segments[1].Trim()
                    : raw;
                var dateLen = segments.Length >= 3 ? segments[2].Trim().Length : 0;

                if (!byPerson.TryGetValue(person, out var slot))
                {
                    byPerson[person] = keep.Count;
                    keep.Add(raw);
                    continue;
                }
                // 同一人重复出现：保留日期更完整的那条（空日期"采购部|胥加娟|"被带日期的那条吃掉）
                var existing = keep[slot];
                var existingDateLen = existing.Split('|').Length >= 3
                    ? existing.Split('|')[2].Trim().Length
                    : 0;
                if (dateLen > existingDateLen) keep[slot] = raw;
            }

            // 分隔符换成人能读的空格：审核员看到 "采购部|胥|2026.6.10" 会以为是编码
            var text = string.Join("；", keep.Select(x => x.Replace('|', ' ').Replace("  ", " ").Trim()));
            return text.Length <= MaxRecordChars ? text : text[..MaxRecordChars] + "…";
        }

        /// <summary>对象里是否有任意非空值（全空对象不值得投影）。</summary>
        private static bool HasAnyValue(JsonElement value)
        {
            if (value.ValueKind != JsonValueKind.Object) return value.ValueKind != JsonValueKind.Null
                                                              && value.ValueKind != JsonValueKind.Undefined;
            foreach (var p in value.EnumerateObject())
            {
                switch (p.Value.ValueKind)
                {
                    case JsonValueKind.String when p.Value.GetString()?.Trim().Length > 0:
                    case JsonValueKind.Number:
                    case JsonValueKind.True:
                    case JsonValueKind.False:
                        return true;
                }
            }
            return false;
        }

        private static string Text(JsonElement value, int maxItems)
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.String:
                    return value.GetString() ?? "";
                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                    return value.ToString();
                case JsonValueKind.Array:
                    var items = value.EnumerateArray()
                        .Take(maxItems)
                        .Select(x => Text(x, 1))
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .ToList();
                    return string.Join("；", items);
                case JsonValueKind.Object:
                    var brief = RecordText(value);
                    if (brief != null) return brief;
                    var flat = FlatValues(value);
                    return flat.Length <= 800 ? flat : flat[..800] + "…";
                default:
                    return "";
            }
        }
    }
}

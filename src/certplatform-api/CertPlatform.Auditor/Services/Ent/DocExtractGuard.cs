using System.Text.Json;
using System.Text.Json.Nodes;

namespace CertPlatform.Auditor.Services.Ent
{
    /// <summary>
    /// 提取输出确定性清洗：只处理**结构性污染**，不改原文事实。
    ///
    /// <para>提示词能守住 90%，但模型仍会在长文档/异构表格上漂移。这里做三件事：
    /// ① 审核/评审输出中的 <c>Conclusion</c>/<c>Conclusions</c> 一律移除；
    /// ② 保留 <c>RecordConclusions</c>（原文结论）不动；
    /// ③ 输出无法解析为 JSON 对象时由调用方按失败处理。</para>
    /// </summary>
    public static class DocExtractGuard
    {
        /// <summary>
        /// 按模式清洗模型输出。目前只有审核/评审需要删除“系统结论”字段；
        /// 过程/记录/索引保持原样，避免误删原文。
        /// </summary>
        public static JsonElement Sanitize(string? mode, JsonElement root)
        {
            if (root.ValueKind != JsonValueKind.Object) return root.Clone();

            var node = JsonNode.Parse(root.GetRawText());
            if (node == null) return root.Clone();

            if (mode == EnterpriseDocExtractionRouter.AuditProfile)
            {
                // 审核/评审只保留“记录里写了什么”，删掉系统结论字段
                RemoveKey(node, "Conclusion");
                RemoveKey(node, "Conclusions");
            }
            else if (mode is EnterpriseDocExtractionRouter.ProcessProfile
                     or EnterpriseDocExtractionRouter.RecordProfile)
            {
                // ★ 过程/记录输出里的 ClauseLinks 是模型按语义猜的，实测会把生产程序挂到 4.1。
                //   条款定位由 doc_profile.ClauseLinks + 章节/部门检索负责，这里一律不保留猜测。
                RemoveKey(node, "ClauseLinks");
            }

            if (mode is EnterpriseDocExtractionRouter.WorkInstructionCitation
                or EnterpriseDocExtractionRouter.DocumentControl
                or EnterpriseDocExtractionRouter.PolicyProfile
                or EnterpriseDocExtractionRouter.ContextList)
            {
                // 报告引用状态必须由代码从报告索引算，不允许模型自行写
                RemoveKey(node, "ReportPresence");
            }

            if (mode == EnterpriseDocExtractionRouter.WorkInstructionCitation
                && node is JsonObject wi)
            {
                // 文中没有真实记录时，不得把“无记录”当成证据名
                var pointers = wi["DocumentEvidencePointers"] as JsonArray;
                if (pointers is { Count: 1 } && pointers[0]?.ToString() == "无记录")
                    wi["ReportEvidenceHints"] = new JsonArray();
            }

            // ★ 部门兜底：模型常把“采购/仓库/制造/财务指标/各部门”写成部门。
            //   这里只保留可识别的真实部门，避免 DeptNames 被污染后按部门过滤失效。
            NormalizeDeptFields(node, mode);

            // ★ Chapters 只允许标准父章节：模型常把 7.1.3 / 8.5.1 这类叶子写进来。
            //   这里只做层级归一（7.1.3 → 7.1），不改写具体条款号，也不碰 PrimaryClauses。
            NormalizeChapterFields(node);

            return JsonSerializer.SerializeToElement(node);
        }

        /// <summary>
        /// 校验指导书的证据指针是否真的出现在原文：模型会把其他文档的“来料检验记录/不良标签”
        /// 串到设备操作规程里。代码只保留原文命中项；全部不命中时回“无记录”。
        /// </summary>
        public static JsonElement ValidateEvidenceAgainstText(string mode, JsonElement output, string text)
        {
            if (mode != EnterpriseDocExtractionRouter.WorkInstructionCitation
                || output.ValueKind != JsonValueKind.Object
                || string.IsNullOrWhiteSpace(text)) return output;

            var node = JsonNode.Parse(output.GetRawText())?.AsObject();
            if (node == null) return output;

            var source = text;
            var pointers = node["DocumentEvidencePointers"] as JsonArray;
            var allowed = new HashSet<string>(StringComparer.Ordinal);
            if (pointers != null)
            {
                var kept = new JsonArray();
                foreach (var p in pointers)
                {
                    var name = CleanName(p?.ToString());
                    if (name.Length == 0 || name == "无记录") continue;
                    if (source.Contains(name, StringComparison.Ordinal))
                    {
                        kept.Add(name);
                        allowed.Add(name);
                    }
                }
                node["DocumentEvidencePointers"] = kept.Count > 0 ? kept : new JsonArray { "无记录" };
            }

            if (node["ReportEvidenceHints"] is JsonArray hints)
            {
                var kept = new JsonArray();
                foreach (var h in hints)
                {
                    if (h is not JsonObject obj) continue;
                    var name = CleanName(obj["Name"]?.ToString());
                    if (name.Length > 0 && allowed.Contains(name)) kept.Add(obj.DeepClone());
                }
                node["ReportEvidenceHints"] = kept;
            }

            return JsonSerializer.SerializeToElement(node);
        }

        private static string CleanName(string? value)
            => (value ?? string.Empty).Trim()
                .Trim('《', '》', '“', '”', '"', '\'', ' ', '：', ':');

        private static readonly Dictionary<string, string> DeptAliases = new(StringComparer.Ordinal)
        {
            ["采购"] = "采购部",
            ["采购部"] = "采购部",
            ["仓库"] = "仓库",
            ["仓储"] = "仓库",
            ["制造"] = "生产部",
            ["制造部"] = "生产部",
            ["生产"] = "生产部",
            ["生产部"] = "生产部",
            ["品质"] = "品质部",
            ["品质部"] = "品质部",
            ["品保"] = "品质部",
            ["品保部"] = "品质部",
            ["品管"] = "品质部",
            ["品管部"] = "品质部",
            ["销售"] = "销售部",
            ["销售部"] = "销售部",
            ["综合"] = "综合部",
            ["综合部"] = "综合部",
            ["行政"] = "综合部",
            ["行政部"] = "综合部",
            ["人事"] = "综合部",
            ["人力资源"] = "综合部",
            ["人力资源部"] = "综合部",
            ["行政人事"] = "综合部",
            ["行政人事部"] = "综合部",
            ["体系管理"] = "综合部",
            ["体系管理部"] = "综合部",
            ["技术"] = "技术部",
            ["设备"] = "设备管理部",
            ["设备部"] = "设备管理部",
            ["设备管理部"] = "设备管理部",
            ["最高管理者"] = "管理层",
            ["总经理"] = "管理层",
            ["管理层"] = "管理层",
        };

        /// <summary>部门字段兜底：只保留可识别的真实部门；不做业务推断。</summary>
        private static void NormalizeDeptFields(JsonNode node, string? mode)
        {
            switch (node)
            {
                case JsonObject obj:
                    foreach (var key in obj.Select(x => x.Key).ToList())
                    {
                        var val = obj[key];
                        if (val == null) continue;

                        if (string.Equals(key, "OwnerDept", StringComparison.OrdinalIgnoreCase))
                        {
                            var dept = NormalizeDeptValue(val);
                            obj[key] = dept ?? "未标注";
                            continue;
                        }

                        if (string.Equals(key, "SupportDepts", StringComparison.OrdinalIgnoreCase))
                        {
                            obj[key] = NormalizeDeptArray(val);
                            continue;
                        }

                        // 4.1/4.2 清单里的 Owner 是责任部门；只在这一类输出里清洗，避免误伤其他 Owner 字段
                        if (mode == EnterpriseDocExtractionRouter.ContextList
                            && string.Equals(key, "Owner", StringComparison.OrdinalIgnoreCase))
                        {
                            obj[key] = NormalizeDeptValue(val) ?? "未标注";
                            continue;
                        }

                        NormalizeDeptFields(val, mode);
                    }
                    break;
                case JsonArray arr:
                    foreach (var child in arr)
                        if (child != null) NormalizeDeptFields(child, mode);
                    break;
            }
        }

        private static JsonNode NormalizeDeptArray(JsonNode value)
        {
            var arr = new JsonArray();
            IEnumerable<string?> list = value is JsonArray existing
                ? existing.Select(x => x?.ToString()).ToList()
                : value.ToString().Split(new[] { '\n', '、', '，', ',', '；', ';' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var raw in list)
            {
                foreach (var part in (raw ?? "").Split(new[] { '\n', '、', '，', ',', '；', ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var dept = NormalizeDeptValue(part);
                    if (dept != null && !arr.Any(x => string.Equals(x?.ToString(), dept, StringComparison.Ordinal)))
                        arr.Add(dept);
                }
            }
            return arr;
        }

        private static string? NormalizeDeptValue(JsonNode? value)
        {
            var raw = (value?.ToString() ?? "").Trim();
            if (raw.Length == 0 || raw == "未标注") return raw.Length == 0 ? null : "未标注";

            // 模型可能把多个部门塞进一个字符串（换行/顿号/逗号），只取第一个可识别部门
            foreach (var part in raw.Replace("\\n", "\n").Split(new[] { '\n', '、', '，', ',', '；', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var v = part.Trim().Trim('《', '》', '“', '”', '"', '\'', ' ', '：', ':');
                if (v.Length == 0) continue;

                if (v.Contains("各部门") || v.Contains("相关部门") || v.Contains("有关部门")
                    || v.Contains("公司领导") || v.Contains("全体员工") || v.Contains("所有部门")
                    || v.Contains("财务指标")) continue;

                if (DeptAliases.TryGetValue(v, out var canonical)) return canonical;

                if (v.EndsWith("部", StringComparison.Ordinal)
                    || v.EndsWith("科", StringComparison.Ordinal)
                    || v.EndsWith("室", StringComparison.Ordinal)
                    || v.EndsWith("中心", StringComparison.Ordinal)
                    || v.EndsWith("组", StringComparison.Ordinal)
                    || v == "仓库")
                    return v;
            }
            return null;
        }

        /// <summary>
        /// <c>Chapters</c> 只保留标准父章节。模型的常见漂移是把 <c>7.1.3</c>、<c>8.5.1</c>
        /// 这类叶子条款直接写进 <c>Chapters</c>；这里按层级截到前两段（7.1 / 8.5），
        /// 不改 <c>PrimaryClauses</c>/<c>SupportClauses</c>，避免把标准条款本身改坏。
        /// </summary>
        private static void NormalizeChapterFields(JsonNode node)
        {
            if (node is not JsonObject obj) return;

            foreach (var key in new[] { "Chapters", "chapters" })
            {
                if (obj[key] is not JsonArray arr) continue;

                var kept = new JsonArray();
                foreach (var item in arr)
                {
                    var raw = (item?.ToString() ?? string.Empty).Trim();
                    if (raw.Length == 0) continue;
                    var chapter = NormalizeChapter(raw);
                    if (!kept.Any(x => string.Equals(x?.ToString(), chapter, StringComparison.Ordinal)))
                        kept.Add(chapter);
                }
                obj[key] = kept;
            }
        }

        private static string NormalizeChapter(string raw)
        {
            var parts = raw.Split('.', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 3 ? string.Join('.', parts.Take(2)) : raw;
        }

        private static void RemoveKey(JsonNode node, string key)
        {
            switch (node)
            {
                case JsonObject obj:
                    obj.Remove(key);
                    foreach (var kv in obj)
                        if (kv.Value != null) RemoveKey(kv.Value, key);
                    break;
                case JsonArray arr:
                    foreach (var child in arr)
                        if (child != null) RemoveKey(child, key);
                    break;
            }
        }
    }
}

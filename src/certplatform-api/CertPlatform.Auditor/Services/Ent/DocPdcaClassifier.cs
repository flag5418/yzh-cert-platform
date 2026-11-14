using System.Text.Json;
using System.Text.Json.Nodes;

namespace CertPlatform.Auditor.Services.Ent
{
    /// <summary>
    /// 文档的 PDCA 定位：由代码按 <c>PrimaryClauses</c>/<c>SupportClauses</c> 计算，
    /// 不让模型自由发挥。<para>
    /// 口径：5.2 方针；4/5/6 策划；7/8 执行；9 绩效；10 改进；跨 2 个以上阶段或手册 = 综合。
    /// </para>
    /// </summary>
    public static class DocPdcaClassifier
    {
        public static JsonElement Apply(string mode, JsonElement output)
        {
            if (output.ValueKind != JsonValueKind.Object) return output;
            var node = JsonNode.Parse(output.GetRawText())?.AsObject();
            if (node == null) return output;

            var clauses = CollectClauses(node);
            var phase = PhaseOf(clauses, mode);
            node["PdcaPhase"] = phase;
            node["PdcaBasis"] = clauses.Count == 0
                ? NoClauseBasis(mode)
                : $"{string.Join("、", clauses)} → {phase}";

            // 部门归集：OwnerDept + SupportDepts，供“按部门过滤/按部门看证据”使用
            var depts = new JsonArray();
            foreach (var owner in CollectStrings(node, "OwnerDept"))
                AddUnique(depts, owner);
            foreach (var support in CollectStrings(node, "SupportDepts"))
                AddUnique(depts, support);
            // 内审/评审的受审部门在 AuditProfile.Scope 里，补进 DeptNames 供按部门收窄
            if (mode == EnterpriseDocExtractionRouter.AuditProfile)
                foreach (var scope in CollectStrings(node, "Scope"))
                    AddUnique(depts, scope);
            node["DeptNames"] = depts;

            return JsonSerializer.SerializeToElement(node);
        }

        private static List<string> CollectClauses(JsonObject node)
        {
            var set = new List<string>();
            foreach (var key in new[] { "PrimaryClauses", "SupportClauses", "ClauseNumbers" })
            {
                if (node[key] is not JsonArray arr) continue;
                foreach (var x in arr)
                {
                    var c = x?.ToString()?.Trim().TrimStart('Q', '：', ':', ' ');
                    if (string.IsNullOrWhiteSpace(c)) continue;
                    if (!set.Contains(c!)) set.Add(c!);
                }
            }
            return set;
        }

        private static string PhaseOf(List<string> clauses, string mode)
        {
            if (clauses.Count == 0)
            {
                return mode switch
                {
                    EnterpriseDocExtractionRouter.DocumentControl => "执行",
                    EnterpriseDocExtractionRouter.ContextList => "策划",
                    EnterpriseDocExtractionRouter.WorkInstructionCitation => "执行",
                    EnterpriseDocExtractionRouter.ProcessProfile => "执行",
                    EnterpriseDocExtractionRouter.RecordProfile => "执行",
                    EnterpriseDocExtractionRouter.PolicyProfile => "执行",
                    EnterpriseDocExtractionRouter.AuditProfile => "绩效",
                    _ => "未判定",
                };
            }

            var phases = new HashSet<string>();
            foreach (var c in clauses)
            {
                if (c.StartsWith("5.2")) phases.Add("方针");
                else if (c.StartsWith("4.") || c.StartsWith("5.") || c.StartsWith("6.")) phases.Add("策划");
                else if (c.StartsWith("7.") || c.StartsWith("8.")) phases.Add("执行");
                else if (c.StartsWith("9.")) phases.Add("绩效");
                else if (c.StartsWith("10.")) phases.Add("改进");
            }

            if (phases.Count == 0) return "未判定";
            if (phases.Count > 2) return "综合";
            return string.Join("、", phases.OrderBy(x => x));
        }

        private static string NoClauseBasis(string mode) => mode switch
        {
            EnterpriseDocExtractionRouter.ProcessProfile => "过程画像（实施类）→ 执行",
            EnterpriseDocExtractionRouter.WorkInstructionCitation => "作业指导（实施类）→ 执行",
            EnterpriseDocExtractionRouter.DocumentControl => "文件控制（7.5）→ 执行",
            EnterpriseDocExtractionRouter.ContextList => "组织环境（4.1）→ 策划",
            EnterpriseDocExtractionRouter.RecordProfile => "记录表单（执行层）→ 执行",
            EnterpriseDocExtractionRouter.PolicyProfile => "制度摘要（执行层）→ 执行",
            EnterpriseDocExtractionRouter.AuditProfile => "审核/评审（第 9 章）→ 绩效",
            _ => $"模式 {mode} 无条款",
        };

        private static List<string> CollectStrings(JsonNode node, string name)
        {
            var list = new List<string>();
            switch (node)
            {
                case JsonObject obj:
                    foreach (var kv in obj)
                    {
                        if (string.Equals(kv.Key, name, StringComparison.OrdinalIgnoreCase))
                        {
                            if (kv.Value is JsonArray arr)
                            {
                                foreach (var x in arr)
                                {
                                    var v = x?.ToString()?.Trim();
                                    if (!string.IsNullOrWhiteSpace(v) && !list.Contains(v)) list.Add(v);
                                }
                            }
                            else if (kv.Value is JsonValue val && val.TryGetValue<string>(out var s)
                                     && !string.IsNullOrWhiteSpace(s) && !list.Contains(s.Trim()))
                            {
                                list.Add(s.Trim());
                            }
                        }
                        if (kv.Value != null)
                            foreach (var x in CollectStrings(kv.Value, name))
                                if (!list.Contains(x)) list.Add(x);
                    }
                    break;
                case JsonArray arr2:
                    foreach (var child in arr2)
                        if (child != null)
                            foreach (var x in CollectStrings(child, name))
                                if (!list.Contains(x)) list.Add(x);
                    break;
            }
            return list;
        }

        private static void AddUnique(JsonArray arr, string? value)
        {
            var v = (value ?? "").Trim();
            if (v.Length == 0) return;
            if (arr.Any(x => string.Equals(x?.ToString(), v, StringComparison.OrdinalIgnoreCase))) return;
            arr.Add(v);
        }

        private static string? Str(JsonObject node, string name)
            => node[name] is JsonValue v && v.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s)
                ? s.Trim()
                : null;
    }
}

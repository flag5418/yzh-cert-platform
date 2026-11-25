using System;
using System.Collections.Generic;
using System.Linq;

namespace CertPlatform.Shared.Domain.Clauses
{
    /// <summary>
    /// 条款 → 召回路径的**唯一解析器**（封装 <c>15-章作用与召回路径模型-V1.md</c> §四 自动挂载公式）。
    ///
    /// <para><b>为什么独立成类</b>：现状召回硬编码在 3 处
    /// （<c>CertCheckContextBuilder</c> 的 ModeOutput 单跳、<c>CertTaskService.ProjectDocSlices</c> 的"条款轴优先、过程轴仅兜底"），
    /// 三处判定互相不认。此解析器把这套判定收敛为 1 处（满足"同一个 WHERE 出现第二处=违规"的精神）。</para>
    ///
    /// <para><b>纯函数</b>：不查库、不写库、不产出条款号，输入属性三元组、输出路径清单，可单测、可两端复用。</para>
    ///
    /// <para><b>两条正交轴</b>：证据族/形态 <c>EvidenceFamily/EvidenceForm</c> 决定"从哪条轴召回"；
    /// 章作用 <c>Role</c> 决定"找回什么体裁（DocType 白名单）+ 默认提取方式(E)"。⛔ 章<b>不改轴</b>。</para>
    /// </summary>
    public static class RecallPathResolver
    {
        // ── 章作用（Role）→ 体裁白名单（§一 末列）────────────────────────────
        // ★ 取值 = 系统真实词汇 `ExtractionMode`（与 EnterpriseDocExtractionRouter 一致），
        //   而非 §一 的示意名。映射：manual=clause_full / procedure=process_profile /
        //   work_instruction=work_instruction_citation / policy=policy_profile /
        //   record=record_profile / audit_record=audit_profile / context_list=context_list。
        // 换标准（13485/24001/45001/42061）只改环境，不改本表。
        private static readonly Dictionary<string, string[]> DocTypesByRole = new(StringComparer.OrdinalIgnoreCase)
        {
            // ★ manual_chapter（手册章节模式）与 clause_full 等价占位：手册/方针改为「整篇按章节」后，
            //   旧的 clause_full 记录读时归一为 manual_chapter ⇒ 两者必须同时在白名单，否则 r1/r2 手册证据全被过滤掉。
            ["r1"] = new[] { "clause_full", "manual_chapter", "context_list" },
            ["r2"] = new[] { "clause_full", "manual_chapter", "policy_profile", "document_control" },
            ["r3"] = new[] { "context_list", "record_profile", "policy_profile" },
            ["r4"] = new[] { "record_profile", "work_instruction_citation", "document_control" },
            ["r5"] = new[] { "process_profile", "work_instruction_citation", "record_profile" },
            ["r6"] = new[] { "record_profile", "audit_profile" },
            ["r7"] = new[] { "record_profile", "audit_profile" }
        };

        // ── 章作用（Role）→ 默认提取方式 E（§一 默认提取方式列）──────────────
        private static readonly Dictionary<string, string[]> ExtractByRole = new(StringComparer.OrdinalIgnoreCase)
        {
            ["r1"] = new[] { "E1", "E2" },
            ["r2"] = new[] { "E1", "E4" },
            ["r3"] = new[] { "E2" },
            ["r4"] = new[] { "E2" },
            ["r5"] = new[] { "E2", "E3" },
            ["r6"] = new[] { "E2" },
            ["r7"] = new[] { "E2" }
        };

        /// <summary>识别证据族：f1/f2/f1f2；无法识别时返回空串（调用方按 f1 兜底）。</summary>
        public static string NormalizeFamily(string? family)
        {
            var f = (family ?? string.Empty).Trim().ToLowerInvariant();
            return f switch
            {
                "f1" or "f1f2" or "f2" => f,
                "f1,f2" or "f1 f2" or "f1+f2" => "f1f2",
                _ => string.Empty
            };
        }

        /// <summary>
        /// §四 公式：Family→选轴 ∥ Form→补轴 ∥ Role→体裁白名单，RM4 追加给 7.5.x。
        /// </summary>
        public static List<RecallPath> Resolve(ClauseRecallInput input)
        {
            if (input == null) return new List<RecallPath>();

            var family = NormalizeFamily(input.EvidenceFamily);
            var form = (input.EvidenceForm ?? string.Empty).Trim().ToLowerInvariant();
            var role = NormalizeRole(input.Role);
            var docTypes = docTypesFor(role);
            var extract = extractFor(role);

            var paths = new List<RecallPath>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            void Add(string axis, string? key, string reason)
            {
                if (!seen.Add(axis)) return;
                paths.Add(new RecallPath
                {
                    Axis = axis,
                    Key = key,
                    DocTypes = new List<string>(docTypes),
                    Extract = extract,
                    Role = role,
                    Reason = reason
                });
            }

            // RM7 语义轴：⛔ 不带体裁白名单（不做硬过滤，白名单已降为降权，16 号 §五）——故 DocTypes 留空。
            void AddSemantic(string clauseNumber)
            {
                if (!seen.Add(RecallAxis.Semantic)) return;
                paths.Add(new RecallPath
                {
                    Axis = RecallAxis.Semantic,
                    Key = clauseNumber,
                    DocTypes = new List<string>(),
                    Extract = extract,
                    Role = role,
                    Reason = $"条款作用类型 {ClauseCategoryResolver.Classify(role, clauseNumber)}"
                             + " ⇒ 追加语义轴 RM7（条款释义 BM25 反查，不做体裁硬过滤）"
                });
            }

            // ① 轴选择（证据族）
            switch (family)
            {
                case "f2":
                    Add(RecallAxis.Process, Join(input.ProcessKeys),
                        $"证据族 f2（执行族）⇒ 记录类主轴：过程轴 RM2（过程 {Join(input.ProcessKeys)}）");
                    Add(RecallAxis.Dept, null,
                        "证据族 f2（执行族）⇒ 记录类主轴：部门轴 RM3（按检查项部门匹配 OwnerDept）");
                    break;
                case "f1f2":
                    Add(RecallAxis.Clause, input.ClauseNumber,
                        "证据族 f1f2（混合族）⇒ 制度轴 RM1（按条款号找制度/程序）");
                    Add(RecallAxis.Process, Join(input.ProcessKeys),
                        $"证据族 f1f2（混合族）⇒ 执行轴 RM2（过程 {Join(input.ProcessKeys)}）");
                    break;
                case "f1":
                default:
                    Add(RecallAxis.Clause, input.ClauseNumber,
                        "证据族 f1（制度族）⇒ 条款轴 RM1（按条款号找制度）");
                    break;
            }

            // ② 补充轴（证据形态）
            if (form == "site")
                Add(RecallAxis.Site, null, "证据形态 site ⇒ 追加现场轴 RM5（标识/环境/设备状态须现场看）");
            if (form == "interview")
                Add(RecallAxis.Interview, null, "证据形态 interview ⇒ 追加访谈轴 RM6（承诺类两段式）");

            // ③ 编号轴（仅 7.5.x 文件控制）
            if (IsDocumentControlClause(input.ClauseNumber))
                Add(RecallAxis.DocNo, null, "条款落在 7.5.x 文件控制 ⇒ 追加编号轴 RM4（记录编号→程序→条款）");

            // ④ 语义轴（RM7）：由条款作用类型（ClauseCategory）驱动，非开关（16 号 §三/§八）。
            //   `{框架,承诺}`（含 4 章框架级 / 5 章）不追加；`{策划,支持,运行,改进}` 追加。
            if (ClauseCategoryResolver.NeedsSemantic(role, input.ClauseNumber))
                AddSemantic(input.ClauseNumber);

            return paths;
        }

        /// <summary>角色归一：r1–r7；空/未知统一按 r1（最宽松的制度轴）。</summary>
        public static string NormalizeRole(string? role)
        {
            var r = (role ?? string.Empty).Trim().ToLowerInvariant();
            return DocTypesByRole.ContainsKey(r) ? r : "r1";
        }

        /// <summary>7.5 及其子条款（7.5 / 7.5.1 / 7.5.2 …）。</summary>
        public static bool IsDocumentControlClause(string? clauseNumber)
        {
            var n = (clauseNumber ?? string.Empty).Trim();
            return n == "7.5" || n.StartsWith("7.5.", StringComparison.Ordinal);
        }

        private static List<string> docTypesFor(string role)
            => DocTypesByRole.TryGetValue(role, out var v) ? v.ToList() : new List<string> { "clause_full" };

        private static string extractFor(string role)
            => ExtractByRole.TryGetValue(role, out var v) ? string.Join("+", v) : "E1";

        private static string? Join(List<string>? keys)
        {
            var cleaned = (keys ?? new List<string>())
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Select(k => k.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            return cleaned.Count == 0 ? null : string.Join(",", cleaned);
        }
    }
}

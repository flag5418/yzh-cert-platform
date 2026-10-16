using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CertPlatform.Shared.Workflow.Models;
using CertPlatform.Admin.Services.Workflow.Skills.Nc;

namespace CertPlatform.Admin.Services.Workflow.Skills
{
    /// <summary>
    /// ★ 聚合多路 judge（NC 结论判定 v2 · 纯代码 · 不涉及 AI）
    ///
    /// <para><b>⚠️ 2026-10-15 起：本节点 = 兼容通道，新链路请勿使用</b></para>
    /// <list type="bullet">
    /// <item><b>为什么降级</b>：引擎的<b>跨路径复用</b>（<c>WorkflowInterpreter.ExecutePathAsync</c>
    ///   把 <c>sharedOutputs</c>/<c>executedNodes</c> 在同一次 item 的多条路径间共享）使
    ///   「多条线拉到一个汇聚节点」的节点<b>只看得见第一条路径</b> —— 分支一多，本节点收不齐
    ///   ⇒ 结论基于残缺清单，且<b>两边都不报错</b>。</item>
    /// <item><b>替代做法</b>：<c>stamp_judge_level</c> 已声明
    ///   <c>[Skill(RegistersTo = WorkflowContextKeys.JudgePool)]</c>，成功执行即<b>自动登记</b>
    ///   进本次检查的判定清单；<c>nc_conclusion_v2</c> 直接读清单 ⇒ 画布上<b>不需要本节点、也不需要汇聚线</b>。</item>
    /// <item><b>保留原因</b>：① 存量画布（已连线的规则）⛔ 不能一改就断；② 单节点测试/手工调试时
    ///   需要「把几条 judge 塞进一个数组」的入口。⇒ 保留但<b>不再作为推荐链路</b>。</item>
    /// <item><b>★ 2026-10-16 补：槽位全空时回落判定池</b>（<c>[FromContext(JudgePool)]</c>）——
    ///   与 <c>nc_conclusion_v2</c> <b>同一条口径</b>（「连线优先，否则读判定清单池」）。
    ///   为什么必须补：本节点收的正是 §1.2 那条「只看得见第一条路径」的线，
    ///   而 <c>nc_conclusion_v2</c> 又是「连线优先」⇒ 存量画布会把**残缺清单**盖过刚登记好的完整池
    ///   （结论基于残缺清单，两边都不报错）。回落之后，存量画布照样拿到完整清单。</item>
    /// </list>
    ///
    /// <para><b>作用</b>：把画布上**多条** nc_judge 分支（各自经 <c>stamp_judge_level</c>
    /// 盖章）汇入一个 <c>judges</c> 数组，喂给 <c>nc_conclusion_v2</c>。
    /// 这就是 2026-10-14 用户方案里「多个 nc_judge 传递一个结论计算器」的聚合点。</para>
    ///
    /// <para><b>★ 槽位到达形态</b>：引擎跨节点连线（<c>PickFromOutput</c>）只取上游的 <c>result</c>
    /// 主端口 ⇒ 每槽实际拿到 stamp 的<b>完整 judge 记录</b>（字典 {judge_code,level,verdict,
    /// reason,result,payload,level_defaulted}）。本 skill 归一三种形态：
    /// ① stamp 完整记录（标准路径）② 裸对象/裸标量（漏盖 stamp ⇒ 缺省 major + 告警）③ JSON 字符串（测试/旧链路）。</para>
    ///
    /// <para><b>★ 主输出</b>：<c>judges</c> = JSON 字符串（<c>[{judge_code,level,verdict,reason,result,level_defaulted},…]</c>），
    /// 与 <c>nc_conclusion_v2.judges_json</c> 字符串契约对齐（同 v1 <c>dimensions_json</c> 先例，
    /// 因为引擎参数绑定按名字取标量，数组需序列化为字符串传递）。
    /// ⛔ 槽位全空<b>且</b>本次检查未登记任何判定 ⇒ Fail（中断）—— 两个来源都没有才说明确实无判定可算。</para>
    /// </summary>
    [Skill(
        Code = "collect_judges",
        Name = "聚合多路judge",
        ReturnType = "json",
        ResultPort = "judges",
        Description =
            "⚠️【兼容通道，新链路请勿使用】\n" +
            "【作用】把最多 8 条 stamp_judge_level 记录聚合成一个 judges JSON 数组，喂给 nc_conclusion_v2。\n" +
            "【怎么配】每个槽连一条 stamp_judge_level 的输出（取 result 主端口）；用不到的槽留空即可。" +
            "槽位一个都没接到时，自动改用「本次检查已登记的判定清单」（与 NC结论判定v2 同口径）。\n" +
            "【坑】⛔ 新链路不需要本节点 —— 引擎的跨路径复用使「多条线汇聚到一个节点」**只看得见第一条路径**，" +
            "分支一多就收不齐，而且两边都不报错；" +
            "改用「判定自动登记」：stamp_judge_level 成功即自动登记，nc_conclusion_v2 直接读清单。" +
            "保留本节点只为存量画布不断链 + 手工调试时一次性塞几条 judge。"
    )]
    public static class CollectJudgesSkill
    {
        /// <summary>本 Skill 编码</summary>
        public const string SkillCode = "collect_judges";

        /// <summary>★ 槽位上限（超出需改造，不静默丢弃）</summary>
        public const int MaxSlots = 8;

        public static Task<SkillResult> ExecuteAsync(
            [SkillParam(Description = "judge 槽位 1（连 stamp_judge_level 节点；未使用的槽位留空）",
                        BindMode = SkillParamBindMode.Link, AllowEmpty = SkillParamEmptyPolicy.Allow)]
            object? judge_1 = null,
            [SkillParam(Description = "judge 槽位 2", BindMode = SkillParamBindMode.Link, AllowEmpty = SkillParamEmptyPolicy.Allow)]
            object? judge_2 = null,
            [SkillParam(Description = "judge 槽位 3", BindMode = SkillParamBindMode.Link, AllowEmpty = SkillParamEmptyPolicy.Allow)]
            object? judge_3 = null,
            [SkillParam(Description = "judge 槽位 4", BindMode = SkillParamBindMode.Link, AllowEmpty = SkillParamEmptyPolicy.Allow)]
            object? judge_4 = null,
            [SkillParam(Description = "judge 槽位 5", BindMode = SkillParamBindMode.Link, AllowEmpty = SkillParamEmptyPolicy.Allow)]
            object? judge_5 = null,
            [SkillParam(Description = "judge 槽位 6", BindMode = SkillParamBindMode.Link, AllowEmpty = SkillParamEmptyPolicy.Allow)]
            object? judge_6 = null,
            [SkillParam(Description = "judge 槽位 7", BindMode = SkillParamBindMode.Link, AllowEmpty = SkillParamEmptyPolicy.Allow)]
            object? judge_7 = null,
            [SkillParam(Description = "judge 槽位 8", BindMode = SkillParamBindMode.Link, AllowEmpty = SkillParamEmptyPolicy.Allow)]
            object? judge_8 = null,

            // ★ 2026-10-16：槽位全空时的回落来源 —— 本次检查已登记的判定清单。
            //   ⛔ 不出现在画布上（Analyze 跳过 [FromContext]）；单节点测试没有池 ⇒ 保持原有 Fail 语义。
            [FromContext(WorkflowContextKeys.JudgePool)]
            List<object>? judge_pool = null,

            CancellationToken ct = default)
        {
            object?[] slots = { judge_1, judge_2, judge_3, judge_4, judge_5, judge_6, judge_7, judge_8 };

            var judges = new List<JudgeRecord>();
            var warnings = new List<string>();

            for (int i = 0; i < slots.Length; i++)
            {
                var raw = slots[i];
                if (IsBlank(raw)) continue;   // 未使用的槽位

                var jr = NormalizeSlot(raw, i + 1, out var warn);
                judges.Add(jr);
                if (warn != null) warnings.Add(warn);
            }

            // ★ 槽位一条都没接 ⇒ 回落判定池（与 nc_conclusion_v2 同口径：「连线优先，否则读池」）。
            //   池里的记录由 stamp_judge_level 登记（引擎保证是完整记录）⇒ 仍走同一个归一器，
            //   输出形状与从槽位来的一模一样（下游 judges_json 契约零改动）。
            var usedPool = false;
            if (judges.Count == 0 && judge_pool is { Count: > 0 })
            {
                foreach (var entry in judge_pool)
                {
                    if (IsBlank(entry)) continue;
                    judges.Add(NormalizeSlot(entry, judges.Count + 1, out _));
                }
                usedPool = judges.Count > 0;
                if (usedPool)
                {
                    warnings.Add(
                        $"本聚合节点没有接到任何 judge（槽位全空），已改用本次检查已登记的判定清单（{judges.Count} 条）");
                }
            }

            if (judges.Count == 0)
            {
                // ★ 两个来源都空 = 画布上没接 judge、本次检查也确实没有任何判定登记
                //   ⇒ 该规则本就不该进结论计算 ⇒ 中断
                return Task.FromResult(SkillResult.Fail(
                    $"collect_judges 未收到任何 judge 结果（{MaxSlots} 个槽位全空，" +
                    "且本次检查未登记任何判定），无法计算结论",
                    WorkflowErrorCodes.PreconditionEmpty));
            }

            // ── ★ 主输出 = JSON 字符串（与 nc_conclusion_v2.judges_json 契约对齐）──
            var records = judges.Select(j => new Dictionary<string, object?>
            {
                ["judge_code"]      = j.JudgeCode,
                ["level"]           = j.Level,
                ["verdict"]         = j.Verdict,
                ["reason"]          = j.Reason ?? string.Empty,
                ["result"]          = j.Payload ?? string.Empty,
                ["level_defaulted"] = j.LevelDefaulted
            }).ToList();

            string judgesJson;
            try
            {
                judgesJson = JsonSerializer.Serialize(records);
            }
            catch (JsonException ex)
            {
                return Task.FromResult(SkillResult.Fail(
                    $"judges 序列化失败：{ex.Message}", WorkflowErrorCodes.SystemError));
            }

            var outputs = new Dictionary<string, object>
            {
                ["judges"]        = judgesJson,
                ["judges_count"]  = judges.Count,
                ["judges_array"]  = records,          // ★ 诊断面：结构化数组（测试页可回看）
                // ★ 诊断面：这批判定是从槽位来的还是回落判定池来的（「收得齐不齐」一眼可见）
                ["judges_source"] = usedPool ? "judge_pool" : "slots",
                ["warnings"]      = warnings.ToArray()
            };
            if (warnings.Count > 0)
                outputs["warning"] = string.Join("；", warnings);

            _ = ct;
            return Task.FromResult(SkillResult.Ok(outputs, warnings.Count == 0 ? 1.0 : 0.9));
        }

        /// <summary>归一记录（内部形状，序列化前用）</summary>
        private class JudgeRecord
        {
            public string JudgeCode { get; set; } = string.Empty;
            public string Level { get; set; } = NcJudgeConstants.DefaultLevel;
            public string Verdict { get; set; } = NcJudgeConstants.VerdictUnverifiable;
            public string? Reason { get; set; }
            public object? Payload { get; set; }
            public bool LevelDefaulted { get; set; }
        }

        // ─────────────────────────────────────────────
        // 槽位归一
        // ─────────────────────────────────────────────

        /// <summary>把单个槽位值归一成记录。三种形态：① 完整记录字典 ② 裸标量/对象 ③ JSON 字符串。</summary>
        private static JudgeRecord NormalizeSlot(object? raw, int slotNo, out string? warning)
        {
            warning = null;

            // 形态③：JSON 字符串（测试/旧链路，可能是完整记录也可能是裸值）
            if (raw is string s && s.TrimStart().StartsWith("{"))
            {
                try
                {
                    var el = JsonDocument.Parse(s).RootElement;
                    var fromDict = FromObject(el, slotNo, ref warning);
                    if (fromDict != null) return fromDict;
                }
                catch (JsonException)
                {
                    // 不是合法 JSON ⇒ 当作文本裸值处理（形态②）
                }
            }

            // 形态①②：字典（stamp 记录 或 裸对象）
            if (raw is IDictionary<string, object> dict)
                return FromDict(dict, slotNo, ref warning);

            // 裸标量（比较/存在性 judge 直接连进 collect，没盖 stamp）⇒ 兜底归一
            warning = $"槽位{slotNo} 未接 stamp_judge_level（拿到裸值），已按缺省级别 major 计入，建议补盖章";
            return new JudgeRecord
            {
                JudgeCode    = $"judge_{slotNo}",
                Level        = NcJudgeConstants.DefaultLevel,
                LevelDefaulted = true,
                Verdict      = NcConclusionV2Calculator.InferVerdict(raw),
                Payload      = raw
            };
        }

        private static JudgeRecord? FromDict(IDictionary<string, object> kv, int slotNo, ref string? warning)
        {
            var isStamped = kv.ContainsKey("judge_code") && kv.ContainsKey("level");

            if (!isStamped && !kv.ContainsKey("judge_code"))
            {
                // ★ 裸对象（上游 docField/docTable/compare 直接连进 collect，漏盖 stamp）
                object? payload = kv.TryGetValue("result", out var r) ? r : (object)kv;
                warning = $"槽位{slotNo} 未接 stamp_judge_level（拿到无 judge_code 对象），已按缺省级别 major 计入，建议补盖章";
                return new JudgeRecord
                {
                    JudgeCode    = $"judge_{slotNo}",
                    Level        = NcJudgeConstants.DefaultLevel,
                    LevelDefaulted = true,
                    Verdict      = NcConclusionV2Calculator.InferVerdict(payload),
                    Reason       = kv.TryGetValue("reason", out var rs) ? rs?.ToString() : null,
                    Payload      = payload
                };
            }

            // stamp 完整记录（或自带头的手工字典）
            string? code = kv.TryGetValue("judge_code", out var c) ? c?.ToString()?.Trim() : null;
            string? level = kv.TryGetValue("level", out var l) ? l?.ToString()?.Trim() : null;
            string? verdict = kv.TryGetValue("verdict", out var v) ? v?.ToString()?.Trim() : null;
            string? reason = kv.TryGetValue("reason", out var rs2) ? rs2?.ToString() : null;
            object? result = kv.TryGetValue("result", out var r2) ? r2 : null;
            object? entryPayload = kv.TryGetValue("payload", out var p) ? p : (object)result;

            var effectiveLevel = string.IsNullOrWhiteSpace(level) ? NcJudgeConstants.DefaultLevel : level!.ToLowerInvariant();
            var effectiveVerdict = string.IsNullOrWhiteSpace(verdict)
                ? NcConclusionV2Calculator.InferVerdict(result)
                : verdict!.ToLowerInvariant();

            var defaulted = kv.TryGetValue("level_defaulted", out var d) && d is bool db ? db
                : string.IsNullOrWhiteSpace(level);

            if (string.IsNullOrWhiteSpace(code)) code = $"judge_{slotNo}";

            return new JudgeRecord
            {
                JudgeCode    = code,
                Level        = effectiveLevel,
                LevelDefaulted = defaulted,
                Verdict      = effectiveVerdict,
                Reason       = reason,
                Payload      = entryPayload ?? result
            };
        }

        private static JudgeRecord? FromObject(JsonElement el, int slotNo, ref string? warning)
        {
            if (el.ValueKind != JsonValueKind.Object) return null;

            string? code = el.TryGetProperty("judge_code", out var c) ? c.GetString()?.Trim() : null;
            string? level = el.TryGetProperty("level", out var l) ? l.GetString()?.Trim() : null;
            string? verdict = el.TryGetProperty("verdict", out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()?.Trim() : null;
            string? reason = el.TryGetProperty("reason", out var rs) && rs.ValueKind == JsonValueKind.String ? rs.GetString() : null;
            string? payloadText = el.TryGetProperty("result", out var r) ? r.GetRawText() : null;

            if (string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(level))
            {
                warning = $"槽位{slotNo} 拿到 JSON 字符串但非 stamp 记录，已按缺省级别 major 计入";
                return new JudgeRecord
                {
                    JudgeCode    = $"judge_{slotNo}",
                    Level        = NcJudgeConstants.DefaultLevel,
                    LevelDefaulted = true,
                    Verdict      = NcConclusionV2Calculator.InferVerdict(payloadText),
                    Reason       = reason,
                    Payload      = el.GetRawText()
                };
            }

            string? effLevel = string.IsNullOrWhiteSpace(level) ? NcJudgeConstants.DefaultLevel : level!.ToLowerInvariant();
            string? effVerdict = string.IsNullOrWhiteSpace(verdict)
                ? NcConclusionV2Calculator.InferVerdict(payloadText)
                : verdict!.ToLowerInvariant();

            return new JudgeRecord
            {
                JudgeCode    = string.IsNullOrWhiteSpace(code) ? $"judge_{slotNo}" : code,
                Level        = effLevel,
                LevelDefaulted = string.IsNullOrWhiteSpace(level),
                Verdict      = effVerdict,
                Reason       = reason,
                Payload      = payloadText
            };
        }

        private static bool IsBlank(object? v)
        {
            if (v == null) return true;
            if (v is string s) return string.IsNullOrWhiteSpace(s);
            return false;
        }
    }
}

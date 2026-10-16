using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SqlSugar;
using CertPlatform.Shared.Workflow.Models;
using YZH.Core.DataBase.Interfaces;
using CertPlatform.Admin.Entities.Cert;
using CertPlatform.Admin.Services.Workflow.Skills.Nc;

namespace CertPlatform.Admin.Services.Workflow.Skills
{
    /// <summary>
    /// ★ NC 多 judge 结论判定 v2（★ 纯代码 · 决策矩阵 · ★ 不涉及 AI）
    ///
    /// <para><b>在 v2 链路中的位置</b>（2026-10-14 裁决 + 2026-10-15 落地形态变更）：</para>
    /// <code>
    ///   旧（画布上要摆汇聚节点 + N 条汇聚线）：
    ///     nc_judge_A → stamp(J1) ─┐
    ///     nc_judge_B → stamp(J2) ─┼→ collect_judges → nc_conclusion_v2
    ///     nc_judge_C → stamp(J3) ─┘
    ///
    ///   新（★ 判定自动登记，画布上无汇聚节点、无汇聚线）：
    ///     nc_judge_A → stamp(J1) ─┐
    ///     nc_judge_B → stamp(J2) ─┼─（各自登记进「本次检查的判定清单」）→ ★nc_conclusion_v2★
    ///     nc_judge_C → stamp(J3) ─┘
    /// </code>
    /// <para>★ 为什么必须去掉汇聚节点：引擎的跨路径复用（<c>WorkflowInterpreter.ExecutePathAsync</c>）
    /// 使「多条线拉到一个节点」的汇聚<b>只看得见第一条路径</b> ⇒ 分支一多，汇聚节点收不齐。
    /// 登记池是「节点成功那一刻的副作用」，与「谁来汇总」无关 ⇒ 天然正确。</para>
    ///
    /// <para><b>★ 入参来源（2026-10-15 起）</b>：</para>
    /// <list type="bullet">
    /// <item><c>judges_json</c>（<b>连线，可选</b>）—— <b>兼容通道</b>：连 <c>collect_judges</c> 的
    ///       <c>judges</c> 主输出。留空则自动读<b>判定清单池</b>。</item>
    /// <item><c>judge_pool</c>（<b>引擎注入</b>）—— <c>WorkflowContextKeys.JudgePool</c>：
    ///       本次检查所有 <c>stamp_judge_level</c> 成功登记的记录。
    ///       ⛔ <b>不出现在画布上</b>（<c>[FromContext]</c> 被 <c>Analyze</c> 跳过）。</item>
    /// <item><c>org_code</c> / <c>standard_code</c> / <c>phase_code</c>（<b>引擎注入</b>）——
    ///       ⛔ <b>已从画布上移除</b>。机构编码由 <c>WfExecutionTaskService.ResolveOrgCodeAsync</c>
    ///       按「规则 → 企业 → 登录人 → 空（平台标准级）」推导。</item>
    /// </list>
    ///
    /// <para><b>输出</b>：<c>conclusion</c>（pass/pass_with_nc/improvement_required/fail）
    /// + <c>judges</c>（原样回传，供 nc_conclude / 报告阶段）+ <c>stats</c> + <c>rule_snapshot</c>
    /// （当时用的哪版判定矩阵，审计可回溯）。纯函数，同输入必同输出，⛔ 无任何 AI 参与。</para>
    ///
    /// <para><b>★ judges_json 从哪来 / 节点测试怎么传</b>：</para>
    /// <list type="bullet">
    /// <item><b>生产环境</b>：无需连线 —— 判定清单池自动收齐。</item>
    /// <item><b>节点测试</b>：⛔ 测试接口<b>不执行上游</b> ⇒ 若把 <c>judges_json</c> 连到节点，
    ///       拿到的是<b>上游节点 ID 字面量</b>（如 <c>"collect_judges_n1"</c>）⇒ 解析必然失败，报
    ///       <c>'c' is an invalid start of a value</c>。
    ///       ✅ 正确做法：测试请求里<b>直接传真实 JSON 数组字符串</b>，例如
    ///       <c>[{"judge_code":"J1","level":"minor","verdict":"fail","reason":"缺审批签字"}]</c>。</item>
    /// </list>
    ///
    /// <para><b>★ 每个 judge 元素的语义</b>（由 <c>stamp_judge_level</c> 盖章产出）：
    /// <c>judge_code</c> = 哪一项检查；<c>verdict</c> = 过没过；<c>level</c> = 不过时有多严重
    /// （决定是否影响结论）；<c>reason</c> = 为什么不过。
    /// ⇒ 一条 judge 对最终结论的影响 = <c>verdict</c>（出没出问题）× <c>level</c>（出了多严重）。</para>
    ///
    /// <para><b>✅ 2026-10-15 已修的两处「描述与实现不一致」</b>：</para>
    /// <list type="number">
    /// <item><b>I1 已消解</b>：原 <c>org_code</c> 描述写「空串 = 平台标准级」而
    ///       <c>AllowEmpty = Deny</c> ⇒ 实测传空串直接 <c>PRECONDITION_EMPTY</c>，与
    ///       <c>LoadMatrixAsync</c> 的 <c>(x.OrgCode == orgCode || x.OrgCode == "")</c> 本意冲突。
    ///       现在 <c>org_code</c> 改为<b>引擎注入</b>（<c>[FromContext]</c>）⇒ 不再参与
    ///       <c>CollectMissingInputs</c> ⇒ 「空 = 平台标准级」这条通路<b>可达</b>。</item>
    /// <item><b>I2 已修</b>：原描述写「取不到规则 ⇒ 中断」，实际 <c>LoadMatrixAsync</c>
    ///       <b>不中断</b>：回落全缺省矩阵并标 <c>RuleSource="DEFAULT_MATRIX"</c>。
    ///       现描述与实现一致（见方法注释）。</item>
    /// </list>
    /// </summary>
    [Skill(
        Code = "nc_conclusion_v2",
        Name = "NC结论判定v2(多judge)",
        ReturnType = "json",
        ResultPort = "conclusion",
        Description =
            "【作用】对本次检查已登记的判定清单算权威结论：pass（推荐认证）/ pass_with_nc（带不符合项通过）/" +
            "improvement_required（需整改复审）/ fail（不通过）。纯代码决策矩阵，AI 不参与，同输入必同输出。\n" +
            "【怎么配】正常情况**不需要配任何参数** —— 机构 / 标准 / 阶段由运行上下文自动注入，" +
            "判定清单由 stamp_judge_level 自动登记，画布上只把这个节点接在链路末端即可。\n" +
            "【坑】⛔ 它算的是「结论」，不是「第 3 个检查项」，别当成检查节点串在判定前面；" +
            "⛔ 节点测试时不要把「judge 数组」连到节点（单节点测试不执行上游，拿到的是节点 ID 字面量，解析必失败）" +
            "—— 请在测试请求里直接粘真实 JSON 数组；" +
            "⛔ 0 条判定 ⇒ 结论 unverifiable，**不等于通过**。"
    )]
    public static class NcConclusionV2Skill
    {
        private readonly static NcConclusionV2Calculator _calc = new();

        public static async Task<SkillResult> ExecuteAsync(
            [SkillParam(Description = "judge 数组 JSON（兼容通道：连 collect_judges 的 judges 主输出；留空则自动读取本次检查已登记的判定清单）",
                        BindMode = SkillParamBindMode.Link, AllowEmpty = SkillParamEmptyPolicy.Allow)]
            string? judges_json,

            // ★ 以下 4 个参数由引擎从运行上下文注入 ⇒ ⛔ 不出现在画布上（Analyze 跳过 [FromContext]）
            [FromContext(WorkflowContextKeys.JudgePool)]
            List<object>? judge_pool = null,

            [FromContext(WorkflowContextKeys.OrgCode)]
            string? org_code = null,

            [FromContext(WorkflowContextKeys.StandardCode)]
            string? standard_code = null,

            [FromContext(WorkflowContextKeys.PhaseCode)]
            string? phase_code = null,

            [FromService] IDbOrm db = null!,
            CancellationToken ct = default)
        {
            // ── ① 取 judge 清单：连线优先（兼容 collect_judges），否则读判定清单池 ──
            var payload = judges_json;
            if (string.IsNullOrWhiteSpace(payload) && judge_pool != null && judge_pool.Count > 0)
            {
                try
                {
                    payload = JsonSerializer.Serialize(judge_pool);
                }
                catch (JsonException ex)
                {
                    return SkillResult.Fail(
                        $"判定清单序列化失败：{ex.Message}", WorkflowErrorCodes.SystemError);
                }
            }

            // ── ①' 0 条判定：⛔ 不是配置错（可能本次确实无判定项），但也不能判 pass ──
            if (string.IsNullOrWhiteSpace(payload))
            {
                return SkillResult.Ok(new Dictionary<string, object>
                {
                    ["conclusion"]       = "unverifiable",
                    ["conclusion_level"] = 0,
                    ["conclusion_label"] = "",
                    ["downgrade_reason"] = "本次检查未登记任何判定",
                    ["judges"]           = new List<object>(),
                    ["stats"] = new Dictionary<string, object>
                    {
                        ["total"] = 0, ["pass"] = 0, ["fail"] = 0, ["unverifiable"] = 0,
                        ["fail_by_level"] = new Dictionary<string, object>(),
                        ["core_fail"] = 0, ["levels_defaulted"] = 0
                    },
                    ["rule_source"]   = "NONE",
                    ["matrix_source"] = "none",
                    ["manual_required"] = false,
                    ["warning"] = "★ 本次检查未登记任何判定（stamp_judge_level 未执行或被分支跳过）⇒ 无法给出结论，⛔ 不能当作「通过」"
                }, 1.0);
            }

            // ── ② 解析 judges（严格：非法直接报错，不静默）──
            List<JudgeResult> judges;
            try
            {
                judges = JudgeResultParser.Parse(payload);
            }
            catch (Exception ex)
            {
                return SkillResult.Fail($"judge 结果解析失败：{ex.Message}", WorkflowErrorCodes.ConfigError);
            }

            // ── ③ 取判定矩阵（机构专属 → 平台标准级 回落；找不到 ⇒ 全缺省并标注）──
            var matrix = await LoadMatrixAsync(db, org_code, standard_code, phase_code, ct);

            // ── ④ ★ 计算（纯函数，同输入必同输出）──
            var result = _calc.Calculate(judges, matrix);

            // ── ⑤ 输出（原样 judges + 统计 + 矩阵快照，审计可读）──
            var judgesOut = judges.Select(j => new Dictionary<string, object?>
            {
                ["judge_code"]     = j.JudgeCode,
                ["level"]          = j.Level,
                ["verdict"]        = j.Verdict,
                ["reason"]         = j.Reason,
                ["level_defaulted"]= j.LevelDefaulted
            }).ToList();

            var outputs = new Dictionary<string, object>
            {
                ["conclusion"]         = result.Conclusion,
                ["conclusion_level"]   = result.ConclusionLevel,
                ["conclusion_label"]   = result.ConclusionLabel,
                ["downgrade_reason"]   = result.DowngradeReason,
                ["judges"]             = judgesOut,
                ["stats"] = new Dictionary<string, object>
                {
                    ["total"]             = result.Stats.Total,
                    ["pass"]              = result.Stats.Pass,
                    ["fail"]              = result.Stats.Fail,
                    ["unverifiable"]      = result.Stats.Unverifiable,
                    ["fail_by_level"]     = result.Stats.FailByLevel,
                    ["core_fail"]         = result.Stats.CoreFail,
                    ["levels_defaulted"]  = result.Stats.LevelsDefaulted
                },
                ["rule_snapshot"]    = result.RuleSnapshot,
                ["rule_source"]      = result.RuleSource,
                ["manual_required"]  = result.ManualRequired,
                // ★ 诊断面：矩阵来源标记（是否用了缺省矩阵）
                ["matrix_source"]    = result.RuleSource == "DEFAULT_MATRIX" ? "default" : "cert_conclusion_rule"
            };

            // ★ 配置缺口显式化：有 judge 因漏盖级别被按 major 计算 ⇒ 提示规则作者补配置
            if (result.Stats.LevelsDefaulted > 0)
            {
                outputs["warning"] =
                    $"★ {result.Stats.LevelsDefaulted} 条 judge 未盖级别，已按安全缺省 major 计入结论，请在 stamp_judge_level 节点补配 level";
            }

            return SkillResult.Ok(outputs, 1.0);   // ★ 规则确定性计算，置信度恒 1
        }

        /// <summary>
        /// 取判定矩阵：从 cert_conclusion_rule 投影 v2 列。
        ///
        /// <para><b>取数口径（归属模型 V2 §2.0，与 v1 LoadRuleAsync 一致）</b>：
        /// 机构专属（<c>OrgCode == org</c>）优先 → 平台标准级（<c>OrgCode == ''</c>）回落，再按
        /// (StandardCode, PhaseCode) 精确 → 该标准全阶段 → 全通用 逐级回落。</para>
        ///
        /// <para><b>★ 找不到任何行 ⇒ 返回「全缺省矩阵」并标注 <c>RuleSource="DEFAULT_MATRIX"</c>，
        /// ⛔ 不中断</b>（与 v1「找不到规则即 Fail」口径不同：配置未建属于「可跑通但需补」，
        /// 让链路能继续产出结论供测试，同时用 <c>matrix_source</c> 把缺口显式化）。
        /// ✅ 2026-10-15 修正：本 skill 的对外描述此前写「取不到规则 ⇒ 中断」，与实现不符，已改。</para>
        ///
        /// <para><b>★ 空 <paramref name="orgCode"/> 是合法输入</b>（= 平台标准级，匹配
        /// <c>OrgCode == ''</c> 的通用行）—— 这是正常回落，⛔ 不是缺失。</para>
        /// </summary>
        private static async Task<JudgeConclusionMatrix> LoadMatrixAsync(
            IDbOrm db, string? orgCode, string? standardCode, string? phaseCode, CancellationToken ct)
        {
            var matrix = new JudgeConclusionMatrix();

            // ★ 归一：null / 空白 一律当空串 ⇒ 才能命中平台标准级（`OrgCode == ''`）那一档
            var org = (orgCode ?? string.Empty).Trim();

            try
            {
                var rows = await db.GetListAsync<ConclusionRule>(
                    x => (x.OrgCode == org || x.OrgCode == "") && x.IsDeleted == false,
                    includeDisabled: true);

                var candidates = (rows.Data ?? new List<ConclusionRule>())
                    .Where(x => x.IsValid == 1)
                    .OrderByDescending(r => r.OrgCode == org)   // ★ 机构专属优先
                    .ToList();

                var std = standardCode ?? string.Empty;
                var pha = phaseCode ?? string.Empty;

                var hit =
                        candidates.FirstOrDefault(r => r.StandardCode == std && (r.PhaseCode ?? "") == pha)
                    ?? candidates.FirstOrDefault(r => r.StandardCode == std && string.IsNullOrEmpty(r.PhaseCode))
                    ?? candidates.FirstOrDefault(r => string.IsNullOrEmpty(r.StandardCode))
                    ?? candidates.FirstOrDefault();   // ★ v2 放宽：任何启用行都比全缺省好

                if (hit != null)
                {
                    matrix.CoreLevels          = ParseCoreLevels(hit.CoreLevels);
                    matrix.MinorRejectThreshold = hit.MinorRejectThreshold > 0 ? hit.MinorRejectThreshold : matrix.MinorRejectThreshold;
                    matrix.MinorTolerated      = hit.MinorTolerated;
                    matrix.UnverifiedPolicy    = hit.UnverifiedPolicy;
                    matrix.ManualRequired      = hit.ManualRequired;
                    matrix.ConclusionLabels    = ParseLabels(hit.ConclusionLabels);
                    matrix.RuleSource          = hit.SourceRef;
                }
                else
                {
                    matrix.RuleSource = "DEFAULT_MATRIX";
                }
            }
            catch (Exception)
            {
                // 取数失败 ⇒ 全缺省矩阵（可测通），RuleSource 标注
                matrix.RuleSource = "DEFAULT_MATRIX";
            }

            _ = ct;
            return matrix;
        }

        /// <summary>CoreLevels：JSON 数组（["major","minor"]）→ List；空/非法 = 缺省 [major,minor]</summary>
        private static List<string> ParseCoreLevels(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new(NcJudgeConstants.DefaultCoreLevels);
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    var list = doc.RootElement.EnumerateArray()
                        .Select(e => e.GetString()?.Trim().ToLowerInvariant())
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .ToList();
                    return list.Count > 0 ? list : new(NcJudgeConstants.DefaultCoreLevels);
                }
            }
            catch { /* 非法 ⇒ 缺省 */ }
            return new(NcJudgeConstants.DefaultCoreLevels);
        }

        /// <summary>ConclusionLabels：JSON 对象 {pass:"…",…} → dict；空/非法 = null（计算器用内置词）</summary>
        private static Dictionary<string, string>? ParseLabels(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
                var d = new Dictionary<string, string>();
                foreach (var p in doc.RootElement.EnumerateObject())
                {
                    var v = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(v)) d[p.Name] = v;
                }
                return d.Count > 0 ? d : null;
            }
            catch { return null; }
        }
    }
}

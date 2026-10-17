using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CertPlatform.Shared.Workflow.Models;
using CertPlatform.Admin.Services.Workflow.Skills.Nc;

namespace CertPlatform.Admin.Services.Workflow.Skills
{
    /// <summary>
    /// ★ judge 结果盖章级别（NC 结论判定 v2 · 纯代码 · 不涉及 AI）
    ///
    /// <para><b>作用</b>：把一条判定（判据 + 证据）组织成<b>一条 judge 记录</b>，
    /// 并盖一个级别（critical/major/minor/observation/none），标记「这条 judge 若不过，按什么级别影响最终结论」。
    /// 级别由专家在画布上配置（<b>写在本节点 = 生产者</b>），下游 <c>nc_conclusion_v2</c> 只认记录里的 level，⛔ 不反查。</para>
    ///
    /// <para><b>★ 登记（2026-10-15 新增）</b>：本节点声明
    /// <c>[Skill(RegistersTo = WorkflowContextKeys.JudgePool)]</c> ⇒ 成功执行后主输出
    /// <b>自动追加进「本次检查的判定清单」</b>。下游 <c>nc_conclusion_v2</c> 直接读清单
    /// ⇒ <b>画布上不再需要 <c>collect_judges</c> 节点、也不再需要把 N 条线拉到一个点</b>。
    /// （为什么必须这样：引擎的跨路径复用使「多线汇聚到一个节点」<b>只看得见第一条路径</b>。）</para>
    ///
    /// <para><b>★ 级别缺省方向（安全侧）</b>：level 传空/未知 ⇒ 按 <c>major</c> 计算并打
    /// <c>level_defaulted=true</c> —— 漏盖一条 = 按最严处理，⛔ 不能默认「不影响」。</para>
    ///
    /// <para><b>★★★ 三种接法 + 一条不变量（2026-10-15 用户定稿 · 配置者必读）</b></para>
    /// <list type="table">
    /// <item><term><b>M1 只连证据</b>（<c>result</c> 连线，<c>verdict</c> 不连）</term><description>
    ///       <b>有材料 ⇒ 通过</b>；材料没拿到 ⇒ <b>不可判定</b>（<c>unverifiable</c>）。
    ///       ★ 语义 = 「有材料即视为通过」。<b>不必再单独接一个 <c>is_empty</c> 节点去判空</b> —— 本节点自己会判。</description></item>
    /// <item><term><b>M2 只连判定</b>（<c>verdict</c> 连线，<c>result</c> 不连）</term><description>
    ///       上游 bool 节点说了算：<c>true</c> ⇒ 通过，<c>false</c> ⇒ 不通过。证据可以不接。</description></item>
    /// <item><term><b>M3 两个都连</b></term><description>
    ///       <b>证据优先判空</b>（2026-10-16 用户裁定）：先看证据 —— 证据<b>取不到值</b> ⇒
    ///       <b>不可判定</b>（<c>unverifiable</c>，判定不介入）；证据<b>有值</b> ⇒ 才按判定给
    ///       通过 / 不通过。理由：没有证据时，任何判定都不可信。</description></item>
    /// <item><term>⛔ <b>两个都不接</b></term><description>
    ///       <b>不允许</b> ⇒ <c>Fail(CONFIG_ERROR)</c>「本 judge 未接任何输入」。
    ///       ⚠️ 判据是<b>端口有没有接</b>（<c>[FromContext] connectedPorts</c>），
    ///       ⛔ <b>不是「值是不是空」</b> —— 后者会把 M1 合法的「材料没拿到」误判成配置错。</description></item>
    /// </list>
    ///
    /// <para><b>★ 运行期判定 6 条（按序，唯一口径）</b>：</para>
    /// <code>
    /// ① result 与 verdict 端口**都未连接**          ⇒ Fail(CONFIG_ERROR)
    /// ② verdict 有值但非法                          ⇒ Fail(CONFIG_ERROR)（⛔ 先校验，不静默）
    /// ③ verdict 合法 且 证据已配置但为空            ⇒ unverifiable（★ 证据优先，判定不介入）
    /// ④ verdict 合法 且 （证据未配置 或 有值）      ⇒ 按 verdict：pass / fail / unverifiable
    /// ⑤ verdict 空 且 result 有值                   ⇒ pass
    /// ⑥ verdict 空 且 result 空（null/空白串/空集合）⇒ unverifiable
    /// </code>
    ///
    /// <para><b>⛔ 已废止的旧规则</b>：原「其余任何非空值 ⇒ fail」的静默推断 ——
    /// 它把「上游给了个看不懂的值」猜成 fail，正是「结论不可信」的根源。现改为<b>显式报错</b>。</para>
    ///
    /// <para><b>⚠️ 边界（否则「配了不生效且不报错」）</b>：内置判空只在 <c>result</c> 连
    /// <b>取值类节点</b>（<c>docField</c>/<c>docTable</c>/<c>get_field</c>/<c>get_table</c>）时有意义；
    /// ⛔ 连 <c>ai_node</c> 时 AI <b>总有输出</b> ⇒ 永远非空 ⇒ 判空形同虚设。</para>
    ///
    /// <para><b>★「空 = 不符合」怎么做（2026-10-16 起）</b>（如「检查审批签字是否存在」）：
    /// 把 <c>is_empty</c> 的取反连到 <c>verdict</c>，且 <b>⛔ <c>result</c>（证据）不要连</b>。
    /// 因为 M3 现在是<b>证据优先</b> —— 一旦 <c>result</c> 也连上、且取到空，本节点会先判
    /// <c>unverifiable</c>，<b>盖过</b> <c>is_empty</c> 取反给的 <c>fail</c>。
    /// （M2 只有 <c>verdict</c> 时完全按 <c>verdict</c>，内置判空不介入。）</para>
    /// </summary>
    [Skill(
        Code = "stamp_judge_level",
        Name = "judge盖章级别",
        ReturnType = "json",
        ResultPort = "result",
        RegistersTo = WorkflowContextKeys.JudgePool,
        Description =
            "【作用】把一条判定组织成一条 judge 记录并盖级别（critical/major/minor/observation/none），" +
            "自动登记进「本次检查的判定清单」，下游 nc_conclusion_v2 直接读清单，⛔ 不需要汇聚节点。\n" +
            "【怎么配】judge 标识自动 = 本节点的名称（★ 改节点名称即改标识，⛔ 无需另填）；" +
            "level = 这条不过时有多严重，留空按 major（安全侧）；" +
            "三种接法任选其一：\n" +
            "　① 只连「证据」→ 有值即视为通过，空值 ⇒ 不可判定\n" +
            "　② 只连「判定」→ true 通过 / false 不通过\n" +
            "　③ 两个都连 → ★证据优先：证据空 ⇒ 不可判定（判定不介入），证据有值才按判定\n" +
            "【坑】⛔ 节点名称不能为空（它是 judge 标识的来源）；" +
            "⛔ 两个都不接 ⇒ 直接报配置错（这是硬性不变量）；" +
            "⛔ 「证据」别连 AI 节点（AI 总有输出 ⇒ 判空形同虚设）；" +
            "「空 = 不符合」的场景（如查审批签字是否存在）请把 is_empty 的取反连到「判定」，" +
            "⛔ 且「证据」不要连（证据空会盖成不可判定）。"
    )]
    public static class StampJudgeLevelSkill
    {
        /// <summary>本 Skill 编码</summary>
        public const string SkillCode = "stamp_judge_level";

        /// <summary>「证据」端口名（与参数名一致，用于连接性判据）</summary>
        private const string PortResult = "result";

        /// <summary>「判定」端口名（与参数名一致，用于连接性判据）</summary>
        private const string PortVerdict = "verdict";

        /// <summary>合法级别集合（小写）。critical 与 major 同档（严重），单独计数展示。</summary>
        private static readonly string[] Levels =
        {
            NcJudgeConstants.LevelCritical, NcJudgeConstants.LevelMajor,
            NcJudgeConstants.LevelMinor, NcJudgeConstants.LevelObservation, NcJudgeConstants.LevelNone
        };

        public static Task<SkillResult> ExecuteAsync(
            [SkillParam(Description = "① 证据：上游取值类节点输出（连线；如文档字段/表格）。有值即视为通过；留空 ⇒ 不可判定（unverifiable）",
                        BindMode = SkillParamBindMode.LinkOrConstant, AllowEmpty = SkillParamEmptyPolicy.Allow)]
            object? result,

            [SkillParam(Description = "★ 级别（常量；critical/major/minor/observation/none，空=按 major 安全缺省）",
                        BindMode = SkillParamBindMode.ConstantOnly, AllowEmpty = SkillParamEmptyPolicy.Allow)]
            string? level = null,

            [SkillParam(Description = "② 判定：连线 bool 节点（true=通过 / false=不通过），或手填 pass/fail/unverifiable；留空 ⇒ 按证据判（有值=通过，无值=不可判定）",
                        BindMode = SkillParamBindMode.LinkOrConstant, AllowEmpty = SkillParamEmptyPolicy.Allow)]
            string? verdict = null,

            [SkillParam(Description = "失败原因/证据（常量；报告阶段叙述素材，可空）",
                        BindMode = SkillParamBindMode.ConstantOnly, AllowEmpty = SkillParamEmptyPolicy.Allow)]
            string? reason = null,

            // ★ 引擎注入（2026-10-16）：本节点名称 —— **judge 标识的唯一来源**。
            //   ⛔ 画布上不再有「judge 标识」输入框（改节点名称即改标识，一处赋值）。
            //   ⛔ 不出现在画布上（Analyze 跳过 [FromContext]）。
            [FromContext(WorkflowContextKeys.NodeTitle)]
            string? nodeTitle = null,

            // ★ 引擎注入：本节点已连接的输入端口名。⛔ 不出现在画布上（Analyze 跳过 [FromContext]）。
            //   用途 = 区分「端口没接」（配置错）与「接了但上游返回空」（不可判定）。
            [FromContext(WorkflowContextKeys.ConnectedPorts)]
            ISet<string>? connectedPorts = null,

            CancellationToken ct = default)
        {
            // ★ judge 标识 = 节点名称（画布内唯一，见 WorkflowContextKeys.NodeTitle）。
            //   ⛔ 不再单独手填 —— 否则同一件事要在「节点名称」和「judge 标识」两处赋值。
            var judgeCode = (nodeTitle ?? string.Empty).Trim();
            if (judgeCode.Length == 0)
                return Task.FromResult(SkillResult.Fail(
                    "本节点未命名：judge 标识取自节点名称，请先在画布上给本节点起个名称",
                    WorkflowErrorCodes.ConfigError));

            // ── ① 连接性前置检查（§4.3）──
            //   ⚠️ 保守：拿不到 connectedPorts（null，如直接单测调用）时**不报错** ——
            //   与 branch 裁剪的保守策略一致（判据缺失时不误杀）。
            if (connectedPorts != null
                && !connectedPorts.Contains(PortResult)
                && !connectedPorts.Contains(PortVerdict))
            {
                return Task.FromResult(SkillResult.Fail(
                    "本 judge 未接任何输入：请连『证据』（如文档字段）或『判定』（bool 节点），两者至少其一",
                    WorkflowErrorCodes.ConfigError));
            }

            // ── ② 判定规约（三种模式 + 6 条规则的核心）──
            //   ★ 证据「已配置」= 端口已连 或 有非空常量 —— 用于区分 M2（只连判定，不判证据空）
            //     与 M3（都连，证据空 ⇒ 优先判 unverifiable）。判据与 NodeExecutor.ConnectedPorts 同口径。
            bool evidenceConfigured =
                (connectedPorts != null && connectedPorts.Contains(PortResult))
                || !IsEmptyValue(result);
            var effectiveVerdict = ResolveVerdict(verdict, result, evidenceConfigured, out var verdictError);
            if (verdictError != null)
                return Task.FromResult(SkillResult.Fail(verdictError, WorkflowErrorCodes.ConfigError));

            // ── ③ 级别规约（大小写/空 → 合法集合 or 安全缺省 major）──
            var normalizedLevel = (level ?? string.Empty).Trim().ToLowerInvariant();
            bool levelDefaulted;
            string effectiveLevel;
            if (normalizedLevel.Length == 0)
            {
                effectiveLevel = NcJudgeConstants.DefaultLevel;
                levelDefaulted = true;
            }
            else if (Array.IndexOf(Levels, normalizedLevel) >= 0)
            {
                effectiveLevel = normalizedLevel;
                levelDefaulted = false;
            }
            else
            {
                // ★ 未知级别不按「不影响」漏过，按安全缺省 major + 标注
                effectiveLevel = NcJudgeConstants.DefaultLevel;
                levelDefaulted = true;
            }

            // ── ④ 组装「完整 judge 记录」作为主输出 result ──
            // ★ 注意：业务键 "result" 这里存的是**整条记录**（下游 collect / 判定池直接用它），
            //   原始上游证据另存 "payload"，报告阶段可回看。
            var record = new Dictionary<string, object>
            {
                ["judge_code"]      = judgeCode,
                ["level"]           = effectiveLevel,
                ["verdict"]         = effectiveVerdict,
                ["reason"]          = reason ?? (object)string.Empty,
                ["result"]          = result ?? new object(),   // 原样保留上游证据（供回溯/展示）
                ["payload"]         = result,
                ["level_defaulted"] = levelDefaulted
            };

            var outputs = new Dictionary<string, object>
            {
                ["result"] = record
            };

            _ = ct;
            return Task.FromResult(SkillResult.Ok(outputs, levelDefaulted ? 0.8 : 1.0));
        }

        // ─────────────────────────────────────────────
        // 判定规约（★ 唯一口径，见类注释「运行期判定 4 条」）
        // ─────────────────────────────────────────────

        /// <summary>
        /// 把「判定入参 + 证据」合成一个确定的 verdict。
        /// <para>返回 <c>null</c> 且 <paramref name="error"/> 非空 = 配置错（调用方转 <c>Fail(CONFIG_ERROR)</c>）。</para>
        /// <para><b>★ 2026-10-16 用户裁定（M3 证据优先）</b>：见类注释「运行期判定 6 条」。</para>
        /// </summary>
        /// <param name="evidenceConfigured">证据端口是否<b>已配置</b>（已连线 或 有非空常量）。
        /// <c>false</c> = M2（只连判定）⇒ ⛔ 不对「证据为空」判 unverifiable。</param>
        private static string? ResolveVerdict(
            string? verdict, object? evidence, bool evidenceConfigured, out string? error)
        {
            error = null;

            // ② verdict 有值 ⇒ 先校验/规范化（连线 bool 会以 "True"/"False" 形态到达）
            //   ⛔ 先校验再判证据 —— 非法 verdict 是配置错，不能被「证据为空」静默吞掉。
            var v = (verdict ?? string.Empty).Trim();
            if (v.Length > 0)
            {
                var lower = v.ToLowerInvariant();
                string normalized;

                if (lower == "true")       normalized = NcJudgeConstants.VerdictPass;
                else if (lower == "false") normalized = NcJudgeConstants.VerdictFail;
                else if (lower == NcJudgeConstants.VerdictPass ||
                         lower == NcJudgeConstants.VerdictFail ||
                         lower == NcJudgeConstants.VerdictUnverifiable) normalized = lower;
                else
                {
                    // ⛔ 不静默猜 —— 猜错会直接改结论，且两边都不报错
                    error = $"verdict 非法：{verdict}（允许 true/false，或 pass/fail/unverifiable；留空则按证据推断）";
                    return null;
                }

                // ③ ★ M3 证据优先：证据已配置但取不到值 ⇒ 不可判定（判定不介入）
                if (evidenceConfigured && IsEmptyValue(evidence))
                    return NcJudgeConstants.VerdictUnverifiable;

                // ④ 证据未配置（M2）或证据有值 ⇒ 按判定
                return normalized;
            }

            // ⑤ verdict 空 + 证据有值 ⇒ pass（M1：「有材料即视为通过」）
            if (!IsEmptyValue(evidence))
                return NcJudgeConstants.VerdictPass;

            // ⑥ verdict 空 + 证据空 ⇒ unverifiable（M1：材料没拿到 ⇒ 不可信）
            return NcJudgeConstants.VerdictUnverifiable;
        }

        /// <summary>
        /// 判空 —— 与 <c>SkillExecutor.IsEmptyValue</c> <b>同口径</b>（null / 空白串 / 空集合）。
        /// <para>⚠️ <c>false</c> 与 <c>0</c> <b>不算空</b>（它们是有效判定值）——
        /// 若判成空，「判定为 false」会被误判成「没有材料」。</para>
        /// </summary>
        private static bool IsEmptyValue(object? v)
        {
            if (v == null) return true;
            if (v is string s) return string.IsNullOrWhiteSpace(s);
            if (v is System.Collections.ICollection c) return c.Count == 0;
            return false;
        }
    }
}

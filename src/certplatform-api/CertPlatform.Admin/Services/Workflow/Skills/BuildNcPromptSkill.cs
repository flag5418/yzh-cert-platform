using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CertPlatform.Admin.Services.Workflow.Skills;
using YZH.Core.DataBase.Interfaces;
using CertPlatform.Admin.Entities.Cert;

namespace CertPlatform.Admin.Services.Workflow.Skills
{
    /// <summary>
    /// ★ NC 判定 Prompt 装配器（服务端组装，★ 不依赖引擎改造）
    ///
    /// <para><b>为什么需要它</b>（18 号 §9.3 实测）：</para>
    /// <list type="bullet">
    /// <item><c>ai_node</c> 的 <c>promptTemplate</c> 存在<b>节点 config</b> 里（RuleJson），
    ///       由 WorkflowDesigner 配置 → 每条规则都要重复写一遍判定框架</item>
    /// <item>★ 而我们的设计是「<b>判定框架全局复用一份</b>，专家只写维度判定标准」</item>
    /// <item>引擎的 <c>{{节点名.端口}}</c> 只能引节点输出，<b>无法注入规则维度清单</b></item>
    /// </list>
    /// <para><b>解法</b>：本装配器在<b>服务端</b>把「全局框架 Prompt + 维度清单 + 条款信息」拼好，
    /// 输出一条完整 Prompt 字符串。画布上的 ai_node 只配 <c>promptCode</c>（如 <c>nc_judge</c>），
    /// 由本 Skill 反查 cert_nc_judge_prompt 取模板并装配。</para>
    /// </summary>
    public class NcPromptAssembler
    {
        /// <summary>
        /// 装配 nc_judge 的 Prompt
        /// </summary>
        /// <param name="promptCode">Prompt 编码（通常固定 nc_judge）</param>
        /// <param name="orgCode">认证机构</param>
        /// <param name="ruleJson">规则 JSON（含 dimensions 维度清单）</param>
        /// <param name="clauseNumber">条款号（展示用）</param>
        /// <param name="clauseTitle">条款标题</param>
        /// <param name="clauseContent">条款原文</param>
        /// <param name="dataMaterial">★ 企业材料（由 get_field / get_table / is_* 节点的输出拼装）</param>
        public static (string SystemPrompt, string UserTemplate) BuildJudgePrompt(
            NcJudgePrompt template,
            string ruleJson,
            string? clauseNumber, string? clauseTitle, string? clauseContent,
            string dataMaterial)
        {
            var dims = ExtractDimensions(ruleJson);
            var dimsText = dims.Count == 0
                ? "（★ 规则未配置检查维度，请检查 cert_validation_rule.RuleJson.dimensions）"
                : BuildDimensionText(dims);

            // ★ 只替换 {{__RULE__.*}} 与 {{__DATA__}}；{{节点名.端口}} 保留给引擎后续解析
            var user = template.UserTemplate
                .Replace("{{__RULE__.clause}}",       Safe(clauseNumber, clauseTitle))
                .Replace("{{__RULE__.clauseContent}}", Safe(clauseContent))
                .Replace("{{__RULE__.dimensions}}",    dimsText)
                .Replace("{{__DATA__}}",              string.IsNullOrWhiteSpace(dataMaterial) ? "（★ 未提供任何材料）" : dataMaterial);

            return (template.SystemPrompt ?? string.Empty, user);
        }

        /// <summary>
        /// 装配 nc_conclude 的 Prompt
        /// <para>★ 结论由规则引擎算好后注入，AI 只写理由。</para>
        /// </summary>
        public static (string SystemPrompt, string UserTemplate) BuildConcludePrompt(
            NcConcludePrompt template,
            string conclusion, string basisText, string unverifiedText,
            string dimensionsText)
        {
            var user = template.UserTemplate
                .Replace("{{__RULE__.conclusion}}",  Safe(conclusion))
                .Replace("{{__RULE__.basis}}",       Safe(basisText))
                .Replace("{{__RULE__.unverified}}", Safe(unverifiedText))
                .Replace("{{__NODE__.dimensions}}", dimensionsText);

            return (template.SystemPrompt ?? string.Empty, user);
        }

        // ─────────────────────────────────────────────
        // 维度清单
        // ─────────────────────────────────────────────

        /// <summary>维度定义（从 RuleJson.dimensions 解析）</summary>
        public class DimensionDef
        {
            public int    No    { get; set; }
            public string? Name { get; set; }
            public string? ProcessCode { get; set; }
            public string? Require { get; set; }
            public string? JudgeCriteria { get; set; }
            public string? SeverityIfFailed { get; set; }
        }

        /// <summary>★ 从 RuleJson 提取 dimensions（★ 解析失败返回空列表，不抛异常中断工作流）</summary>
        public static List<DimensionDef> ExtractDimensions(string? ruleJson)
        {
            if (string.IsNullOrWhiteSpace(ruleJson)) return new List<DimensionDef>();
            try
            {
                using var doc = JsonDocument.Parse(ruleJson);
                if (!doc.RootElement.TryGetProperty("dimensions", out var arr)
                    || arr.ValueKind != JsonValueKind.Array) return new List<DimensionDef>();

                return arr.EnumerateArray().Select(e => new DimensionDef
                {
                    No                = e.TryGetProperty("No", out var n) && n.TryGetInt32(out var v) ? v : 0,
                    Name              = Str(e, "Name"),
                    ProcessCode       = Str(e, "ProcessCode"),
                    Require           = Str(e, "Require"),
                    JudgeCriteria     = Str(e, "JudgeCriteria"),
                    SeverityIfFailed  = Str(e, "SeverityIfFailed"),
                }).Where(d => d.No > 0).OrderBy(d => d.No).ToList();
            }
            catch
            {
                return new List<DimensionDef>();
            }
        }

        private static string BuildDimensionText(List<DimensionDef> dims)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < dims.Count; i++)
            {
                var d = dims[i];
                sb.AppendLine($"维度 {d.No}：{Safe(d.Name)}");
                if (!string.IsNullOrWhiteSpace(d.ProcessCode))
                    sb.AppendLine($"  过程代号：{d.ProcessCode}");
                sb.AppendLine($"  条款要求：{Safe(d.Require)}");
                sb.AppendLine($"  判定标准：{Safe(d.JudgeCriteria)}");
                if (!string.IsNullOrWhiteSpace(d.SeverityIfFailed))
                    sb.AppendLine($"  不符合时严重度：{d.SeverityIfFailed}");
                if (i < dims.Count - 1) sb.AppendLine();
            }
            return sb.ToString();
        }

        /// <summary>把上游节点输出拼成材料文本（给 AI 看）</summary>
        public static string BuildDataMaterial(IEnumerable<KeyValuePair<string, string>> items)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var kv in items)
            {
                if (string.IsNullOrWhiteSpace(kv.Value)) continue;
                sb.AppendLine($"【{kv.Key}】");
                sb.AppendLine(kv.Value);
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private static string? Str(JsonElement e, string name) =>
            e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        private static string Safe(string? s) => s ?? "（未提供）";

        /// <summary>条款号 + 标题 组合展示（★ 两者都没有时给明确提示，便于专家发现配置缺失）</summary>
        private static string Safe(string? number, string? title)
        {
            var parts = new[] { number, title }
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!.Trim())
                .ToArray();
            return parts.Length == 0 ? "（未提供条款号与标题）" : string.Join(" ", parts);
        }

        /// <summary>校验模板里是否还有未替换的 {{__*__}} 占位符（★ 用于发现配置错误）</summary>
        public static List<string> FindUnresolvedPlaceholders(string? text)
        {
            if (string.IsNullOrEmpty(text)) return new List<string>();
            return Regex.Matches(text, @"\{\{__[A-Z_]+__\.[^}]+\}\}")
                   .Select(m => m.Value).Distinct().ToList();
        }
    }

    // ══════════════════════════════════════════════════════════
    // Skill · build_nc_prompt —— Prompt 装配（供画布上的 ai_node 引用）
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// ★ NC 判定 Prompt 装配
    ///
    /// <para><b>用法（画布）</b>：</para>
    /// <code>
    ///   build_nc_prompt  →  nc_judge (ai_node)  →  nc_conclusion_calc  →  nc_conclude (ai_node)
    ///        (装配)             (提取事实)            (算结论·权威)            (写理由)
    /// </code>
    /// <para>上游的 get_field / get_table / is_* 节点把企业材料汇入 build_nc_prompt 的 data_material。</para>
    ///
    /// <para><b>关键价值</b>：画布上的 ai_node 只配 promptCode（如 nc_judge），
    /// <b>判定框架由服务端从 cert_nc_judge_prompt 统一装配</b> —— 规则数增加时画布与 Prompt 都不变。</para>
    /// </summary>
    [Skill(
        Code = "build_nc_prompt",
        Name = "NC判定Prompt装配",
        ReturnType = "json",
        Description = "★ 从全局 Prompt 模板 + 规则维度清单 + 企业材料，装配出完整的 nc_judge / nc_conclude 提示词（免在画布上重复写判定框架）"
    )]
    public static class BuildNcPromptSkill
    {
        public static async Task<SkillResult> ExecuteAsync(
            [SkillParam(Description = "★Prompt 编码：nc_judge=维度判定 | nc_conclude=结论理由",
                        BindMode = SkillParamBindMode.Enum, EnumSource = "nc_prompt_kind")]
            string? prompt_code,

            [SkillParam(Description = "认证机构编码", BindMode = SkillParamBindMode.LinkOrConstant)]
            string? org_code,

            [SkillParam(Description = "★规则 JSON（cert_validation_rule.RuleJson，含 dimensions）",
                        BindMode = SkillParamBindMode.LinkOrConstant)]
            string? rule_json,

            [SkillParam(Description = "★企业材料文本（上游 get_field/get_table/is_* 节点的输出拼装）",
                        BindMode = SkillParamBindMode.LinkOrConstant)]
            string? data_material,

            [SkillParam(Description = "条款号（展示用）", BindMode = SkillParamBindMode.LinkOrConstant)]
            string? clause_number,

            [SkillParam(Description = "条款标题", BindMode = SkillParamBindMode.LinkOrConstant)]
            string? clause_title,

            [SkillParam(Description = "条款原文", BindMode = SkillParamBindMode.LinkOrConstant)]
            string? clause_content,

            // ── 以下仅 nc_conclude 用 ──
            [SkillParam(Description = "★[nc_conclude] 规则算出的权威结论",
                        BindMode = SkillParamBindMode.LinkOrConstant)]
            string? rule_conclusion,

            [SkillParam(Description = "★[nc_conclude] 依据维度号（JSON 数组）",
                        BindMode = SkillParamBindMode.LinkOrConstant)]
            string? basis_json,

            [SkillParam(Description = "★[nc_conclude] 未检查维度号（JSON 数组）",
                        BindMode = SkillParamBindMode.LinkOrConstant)]
            string? unverified_json,

            [FromService] IDbOrm db = null!,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(org_code))
                return SkillResult.Fail("org_code 不能为空");

            var code = (prompt_code ?? "nc_judge").Trim();
            var org = org_code!;

            // ── nc_judge ──
            if (string.Equals(code, "nc_judge", StringComparison.OrdinalIgnoreCase))
            {
                var tpl = await LoadJudgeTemplateAsync(db, org);
                if (tpl == null)
                    return SkillResult.Fail($"未找到 Prompt 模板 {code}（机构={org}）");

                var (sys, user) = NcPromptAssembler.BuildJudgePrompt(
                    tpl, rule_json ?? "", clause_number, clause_title, clause_content,
                    data_material ?? "");

                var unresolved = NcPromptAssembler.FindUnresolvedPlaceholders(user);
                var outputs = new Dictionary<string, object>
                {
                    ["system_prompt"] = sys,
                    ["user_prompt"]   = user,
                    ["prompt_code"]   = code,
                    ["prompt_version"] = tpl.Version,
                    ["model"]         = tpl.Model ?? "",
                    ["temperature"]   = (double)tpl.Temperature,
                    ["max_tokens"]    = tpl.MaxTokens,
                    ["dimension_count"] = NcPromptAssembler.ExtractDimensions(rule_json).Count
                };
                if (unresolved.Count > 0)
                {
                    // ★ 装配遗漏要显式报出，不能静默送出残缺 Prompt
                    outputs["unresolved_placeholders"] = unresolved.ToArray();
                    outputs["warning"] =
                        $"★Prompt 模板存在未替换的占位符：{string.Join("、", unresolved)}（模板配置有误）";
                }
                return SkillResult.Ok(outputs, 1.0);
            }

            // ── nc_conclude ──
            if (string.Equals(code, "nc_conclude", StringComparison.OrdinalIgnoreCase))
            {
                var tpl = await LoadConcludeTemplateAsync(db, org);
                if (tpl == null)
                    return SkillResult.Fail($"未找到 Prompt 模板 {code}（机构={org}）");

                var (sys, user) = NcPromptAssembler.BuildConcludePrompt(
                    tpl,
                    rule_conclusion ?? "（未提供）",
                    FormatIntArray(basis_json),
                    FormatIntArray(unverified_json),
                    data_material ?? "（无维度明细）");

                var outputs2 = new Dictionary<string, object>
                {
                    ["system_prompt"] = sys,
                    ["user_prompt"]   = user,
                    ["prompt_code"]   = code,
                    ["prompt_version"] = tpl.Version,
                    ["model"]         = tpl.Model ?? "",
                    ["temperature"]   = (double)tpl.Temperature,
                    ["max_tokens"]    = tpl.MaxTokens,
                    // ★ 结论原样回传 —— 供后续校验 C7
                    ["rule_conclusion"] = rule_conclusion ?? ""
                };
                var un2 = NcPromptAssembler.FindUnresolvedPlaceholders(user);
                if (un2.Count > 0)
                {
                    outputs2["unresolved_placeholders"] = un2.ToArray();
                    outputs2["warning"] = $"★模板存在未替换占位符：{string.Join("、", un2)}";
                }
                return SkillResult.Ok(outputs2, 1.0);
            }

            return SkillResult.Fail($"未知的 prompt_code：{code}（支持 nc_judge / nc_conclude）");
        }

        private static async Task<NcJudgePrompt?> LoadJudgeTemplateAsync(IDbOrm db, string org)
        {
            var r = await db.GetListAsync<NcJudgePrompt>(
                x => x.OrgCode == org && x.PromptCode == "nc_judge" && x.IsDeleted == false,
                includeDisabled: true);
            var rows = r.Data ?? new List<NcJudgePrompt>();
            return rows.Where(x => x.IsValid == 1)
                        .OrderByDescending(x => x.IsDefault).ThenByDescending(x => x.Version)
                        .FirstOrDefault();
        }

        private static async Task<NcConcludePrompt?> LoadConcludeTemplateAsync(IDbOrm db, string org)
        {
            var r = await db.GetListAsync<NcConcludePrompt>(
                x => x.OrgCode == org && x.PromptCode == "nc_conclude" && x.IsDeleted == false,
                includeDisabled: true);
            var rows = r.Data ?? new List<NcConcludePrompt>();
            return rows.Where(x => x.IsValid == 1)
                        .OrderByDescending(x => x.IsDefault).ThenByDescending(x => x.Version)
                        .FirstOrDefault();
        }

        private static string FormatIntArray(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return "（无）";
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Array) return json;
                var arr = doc.RootElement.EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.Number)
                    .Select(e => "维度" + e.GetInt32())
                    .ToList();
                return arr.Count == 0 ? "（无）" : string.Join("、", arr);
            }
            catch
            {
                return json;
            }
        }
    }
}

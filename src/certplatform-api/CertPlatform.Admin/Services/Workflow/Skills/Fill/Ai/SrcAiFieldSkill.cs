using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CertPlatform.Shared.Office;
using YZH.Core.DataBase.Interfaces;

namespace CertPlatform.Admin.Services.Workflow.Skills.Fill.Ai
{
    /// <summary>
    /// AI 单元格填写 —— <b>★ 本系统最核心的数据来源</b>（39 号 §七）。
    ///
    /// <para>给定「<b>这个格子要填什么</b>」（<paramref name="instruction"/>）
    /// +「<b>已过滤的企业文档 markdown</b>」（<paramref name="enterprise_docs"/>），
    /// 让模型<b>动态分析</b>出单元格值。形态确定（单元格）、内容不确定（企业有什么）。</para>
    ///
    /// <para><b>★★ <paramref name="value_kind"/> 由锚点属性给，⛔ 不由 AI 猜</b>（39 号 §7.3）：
    /// <c>38</c> 号 §4.2 已定死 —— Excel 传错 <c>SetCellValue</c> 重载<b>不报错但结果错</b>。
    /// ⇒ <b>AI 只负责「值是什么」，⛔ 不负责「值是什么类型」</b>；
    /// 类型来自 <c>cert_doc_template_anchor.ValueType</c>，由调用方传入。
    /// 转换失败（声明 <c>number</c> 却返回「叁万元」）⇒ <b>Fail</b>，⛔ 不静默降级成文本。</para>
    /// </summary>
    [Skill(
        Code = "src_ai_field",
        Name = "AI 单元格填写",
        ReturnType = "json",
        Description = "提示词 + 已过滤企业文档 → 动态分析出单元格值。"
    )]
    public static class SrcAiFieldSkill
    {
        /// <summary>本 Skill 编码</summary>
        public const string SkillCode = "src_ai_field";

        /// <summary>执行 —— 装配提示词 → 调模型 → 取 <c>fields.{anchor_code}</c> → 按类型转值。</summary>
        /// <param name="anchor_code">锚点编码（必填）</param>
        /// <param name="instruction">这个格子要填什么（必填，39 号 §7.2 无默认值 = 必填）</param>
        /// <param name="enterprise_docs">★ 已过滤的企业文档 markdown（由编排器产出）</param>
        /// <param name="prompt_code">提示词编码；空 ⇒ 用内置默认模板（开箱可跑）</param>
        /// <param name="value_kind">值类型：text/number/date/bool（★ 来自锚点属性，⛔ 不由 AI 猜）</param>
        /// <param name="number_format">Excel 数字/日期格式串（.NET 方言）</param>
        /// <param name="org_code">机构编码（空 ⇒ 只命中全局提示词）</param>
        /// <param name="db">数据访问（DI 注入）</param>
        /// <param name="llm">AI 调用器（DI 注入）</param>
        /// <param name="ct">取消令牌</param>
        public static async Task<SkillResult> ExecuteAsync(
            [SkillParam(Description = "锚点编码")]
            string anchor_code,

            [SkillParam(Description = "这个格子要填什么（模板里这一项的填写说明）",
                        BindMode = SkillParamBindMode.LinkOrConstant)]
            string instruction,

            [SkillParam(Description = "★ 已过滤的企业文档 markdown（由编排器按相关性筛选后传入）",
                        BindMode = SkillParamBindMode.LinkOrConstant)]
            string? enterprise_docs = null,

            [SkillParam(Description = "提示词编码 cert_doc_fill_prompt.PromptCode；空=用内置默认模板")]
            string? prompt_code = null,

            [SkillParam(Description = "值类型：text/number/date/bool（★ 来自锚点属性，不由 AI 猜）")]
            string value_kind = "text",

            [SkillParam(Description = "Excel 数字/日期格式串（.NET 方言），如 #,##0.00")]
            string? number_format = null,

            [SkillParam(Description = "机构编码（空=只命中全局提示词）")]
            string? org_code = null,

            [FromService] IDbOrm db = null!,
            [FromService] IAiFillInvoker llm = null!,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(anchor_code))
                return SkillResult.Fail("anchor_code 不能为空");

            if (string.IsNullOrWhiteSpace(instruction))
                return SkillResult.Fail("instruction 不能为空");

            var anchor = new AiFillAnchorSpec
            {
                AnchorCode = anchor_code,
                Instruction = instruction,
                ValueKind = string.IsNullOrWhiteSpace(value_kind) ? "text" : value_kind,
                NumberFormat = number_format,
                Section = AiFillJsonReader.SectionFields,
            };

            var prompt = await AiFillPromptBuilder.BuildSingleAsync(
                db, prompt_code, SkillCode, anchor,
                enterpriseDocs: enterprise_docs, orgCode: org_code, ct: ct);

            var resp = await llm.InvokeAsync(db, prompt, ct);
            if (!resp.Success) return SkillResult.Fail(resp.Error ?? "AI 调用失败");

            // 约定：fields.{anchor_code} = { "value": …, "confidence": 0.9, "source_doc": "…", "note": "…" }
            var node = AiFillJsonReader.ReadObject(
                resp.Root, AiFillJsonReader.SectionFields, anchor_code);

            if (node == null)
                return SkillResult.Fail($"AI 未返回 fields.{anchor_code}");

            var rawValue = AiFillJsonReader.ReadRaw(node, "value");

            // ★ 值为 null ⇒ Fail（39 号 §7.4）—— ⛔ 不产空值：
            //   「AI 说资料里没有」应由编排器记 Pending，而不是写一个空值冒充「已填」。
            if (rawValue == null)
                return SkillResult.Fail($"fields.{anchor_code} 缺 value（AI 可能认为资料中未提及）");

            // ★ 按**锚点声明的类型**转换（39 号 §7.3）
            var value = FillValueFactory.FromRaw(anchor_code, rawValue, value_kind, number_format);

            if (value == null)
                return SkillResult.Fail(
                    $"fields.{anchor_code} 的值「{AiFillJsonReader.AsString(rawValue)}」" +
                    $"不是合法的 {value_kind}（类型由锚点 ValueType 决定，⛔ 不由 AI 猜）");

            value.Source = AiFillJsonReader.ReadString(node, "source_doc") ?? "AI 单元格填写";

            // ★ 置信度：模型给了就用；没给则保持默认 —— 并在 Outputs 里**明确标注这个数是哪来的**，
            //   ⛔ 不要用 0.5 之类的「魔法中间值」冒充（那会掩盖「模型没给」这个事实）。
            var confidence = AiFillJsonReader.ReadConfidence(node);
            if (confidence.HasValue) value.Confidence = confidence.Value;

            return SkillResult.Ok(new Dictionary<string, object>
            {
                ["value"] = value,
                ["anchor_code"] = anchor_code,
                ["hit"] = true,
                ["source_doc"] = value.Source ?? string.Empty,
                ["confidence"] = value.Confidence,
                ["confidence_from_ai"] = confidence.HasValue,   // ★ 界面据此区分「模型给的」与「默认的」
                ["note"] = AiFillJsonReader.ReadString(node, "note") ?? string.Empty,
            }, value.Confidence);
        }
    }
}

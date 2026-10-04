using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CertPlatform.Shared.Office;
using YZH.Core.DataBase.Interfaces;

namespace CertPlatform.Admin.Services.Workflow.Skills.Fill.Ai
{
    /// <summary>
    /// AI 语义改写 —— 把<b>模板里的标准原文</b>按<b>企业实际情况</b>改写
    /// （39 号 §六）。★ <b>不需要企业文档</b>（38 号 §15.2 已界定）。
    ///
    /// <para><b>★ 与 <c>src_ai_field</c> 的区别（⛔ 别混）</b>：
    /// 本 Skill 的<b>输入含原文</b>（「把这段话改写一下」）；
    /// <c>src_ai_field</c> 的输入只有「这一格要填什么」（「去资料里找这个值」）。
    /// 前者是<b>改写</b>（有素材），后者是<b>检索+判断</b>（无素材）。</para>
    ///
    /// <para><b>★ 为什么走 <c>Section=semantic</c></b>：输出 JSON 的三段中
    /// <c>semantic</c> 段的形态是 <c>{锚点编码: "文本"}</c>（39 号 §12.4 的
    /// <c>additionalProperties: {type: string}</c>）—— 它<b>没有 confidence 字段</b>。
    /// ⇒ 本 Skill ⛔ 不设 <see cref="FillValue.Confidence"/>（保持默认），
    /// 也 ⛔ 不读 <c>confidence</c>（39 号 §6.2 的 <c>ReadConfidence(resp.Json, "semantic", …)</c>
    /// 是把「对象段」的取值写到了「字符串段」上，属笔误）。</para>
    /// </summary>
    [Skill(
        Code = "src_semantic",
        Name = "AI 语义改写",
        ReturnType = "json",
        Description = "按企业实际情况改写标准原文（不依赖企业文档）。"
    )]
    public static class SrcSemanticSkill
    {
        /// <summary>本 Skill 编码</summary>
        public const string SkillCode = "src_semantic";

        /// <summary>执行 —— 装配提示词 → 调模型 → 取 <c>semantic.{anchor_code}</c>。</summary>
        /// <param name="anchor_code">锚点编码（必填）</param>
        /// <param name="source_text">待改写的标准原文（必填）</param>
        /// <param name="instruction">改写要求（空 ⇒ 退化为「润色」，39 号 §6.3）</param>
        /// <param name="prompt_code">提示词编码；空 ⇒ 用内置默认模板（开箱可跑）</param>
        /// <param name="enterprise_context">企业画像/参数摘要（可选，用于风格与事实校正）</param>
        /// <param name="org_code">机构编码（空 ⇒ 只命中全局提示词）</param>
        /// <param name="db">数据访问（DI 注入）</param>
        /// <param name="llm">AI 调用器（DI 注入）</param>
        /// <param name="ct">取消令牌</param>
        public static async Task<SkillResult> ExecuteAsync(
            [SkillParam(Description = "锚点编码")]
            string anchor_code,

            [SkillParam(Description = "待改写的标准原文", BindMode = SkillParamBindMode.LinkOrConstant)]
            string source_text,

            [SkillParam(Description = "改写要求（模板里这一项的填写说明）", BindMode = SkillParamBindMode.LinkOrConstant)]
            string? instruction = null,

            [SkillParam(Description = "提示词编码 cert_doc_fill_prompt.PromptCode；空=用内置默认模板")]
            string? prompt_code = null,

            [SkillParam(Description = "企业画像/参数摘要（可选，用于风格与事实校正）",
                        BindMode = SkillParamBindMode.LinkOrConstant)]
            string? enterprise_context = null,

            [SkillParam(Description = "机构编码（空=只命中全局提示词）")]
            string? org_code = null,

            [FromService] IDbOrm db = null!,
            [FromService] IAiFillInvoker llm = null!,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(anchor_code))
                return SkillResult.Fail("anchor_code 不能为空");

            // ★ 空原文 ⇒ Fail（39 号 §6.3）—— ⛔ 不产空值：本 Skill 的语义就是「改写这段原文」，
            //   没有原文就没有可改的东西，返回空值会让上游误以为「改写结果是空」。
            if (string.IsNullOrWhiteSpace(source_text))
                return SkillResult.Fail("source_text 不能为空");

            var anchor = new AiFillAnchorSpec
            {
                AnchorCode = anchor_code,
                Instruction = instruction ?? string.Empty,
                ValueKind = "text",                          // semantic 段恒为文本
                Section = AiFillJsonReader.SectionSemantic,
                SourceText = source_text,
            };

            var prompt = await AiFillPromptBuilder.BuildSingleAsync(
                db, prompt_code, SkillCode, anchor,
                enterpriseDocs: enterprise_context, orgCode: org_code, ct: ct);

            var resp = await llm.InvokeAsync(db, prompt, ct);
            if (!resp.Success) return SkillResult.Fail(resp.Error ?? "AI 调用失败");

            // 约定：semantic.{anchor_code} = "改写后的文本"（字符串，不是对象）
            var text = AiFillJsonReader.ReadSectionValue(
                resp.Root, AiFillJsonReader.SectionSemantic, anchor_code);

            if (text == null)
                return SkillResult.Fail($"AI 未返回 semantic.{anchor_code}");

            var value = new FillValue
            {
                AnchorCode = anchor_code,
                Kind = FillValueKind.Text,
                Text = text,
                Source = "AI 语义改写",
            };

            // ⚠️ 空字符串是**合法结果**（= AI 认为该改写为空），与「没返回」区分（39 号 §6.3）——
            //    上面判的是 `text == null`，不是 `IsNullOrWhiteSpace`。
            return SkillResult.Ok(new Dictionary<string, object>
            {
                ["value"] = value,
                ["anchor_code"] = anchor_code,
                ["hit"] = true,
            });
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CertPlatform.Admin.Services.Workflow.Skills
{
    /// <summary>
    /// 文本拼接：将前半部分和后半部分按连接符拼接为一个字符串。纯函数，无副作用。
    /// <para>移植自：旧 YZH.Core.Skills.AssembleSkill.cs（零改动，仅 namespace 调整）</para>
    ///
    /// <para><b>★ 2026-10-12 统一返回契约</b>：<c>ResultPort = "assembled_text"</c>
    /// —— 连线下游靠 <c>result</c> 拿到<b>拼接后的那一个字符串</b>。
    /// 修复前 <c>result</c> 是整包字典 <c>{assembled_text:"…"}</c>，
    /// 下游 <c>is_empty</c> 判它「非空」⇒ <b>拼出来是空串也走「有值」分支</b>（实测缺陷）。</para>
    /// </summary>
    [Skill(
        Code = "assemble",
        Name = "文本拼接",
        ReturnType = "string",
        ResultPort = "assembled_text",
        Description =
            "【作用】把「前半部分文本」与「后半部分文本」按连接符拼成一个字符串。\n" +
            "【怎么配】两段文本各连一个取值节点（或手填常量）；连接符留空 = 首尾直接相接。\n" +
            "【坑】⛔ 只做拼接，不做去重、不做格式化；要「先判断再拼」请在前面接条件分支。"
    )]
    public static class AssembleSkill
    {
        public static Task<SkillResult> ExecuteAsync(
            // ★ 2026-10-12：前后两段都声明「不允许为空」—— 拼接节点拿到空值算出的结果
            //   不是「空」而是「半截文本」，静默产出错误材料的风险高于中断。
            //   ⚠️ 若将来确实需要「可选拼接」，把这里改成 AllowEmpty = SkillParamEmptyPolicy.Allow，
            //      由规则作者用 is_empty + branch 自行分流（⛔ 不要在函数里做隐式兜底）。
            [SkillParam(Description = "前半部分文本（合并前）",
                        BindMode = SkillParamBindMode.LinkOrConstant, AllowEmpty = SkillParamEmptyPolicy.Deny)]
            string? prefix_text = null,

            [SkillParam(Description = "后半部分文本（合并后）",
                        BindMode = SkillParamBindMode.LinkOrConstant, AllowEmpty = SkillParamEmptyPolicy.Deny)]
            string? suffix_text = null,

            // ★ 2026-10-12：连接符是静态配置值，业务上不可能是上游节点输出
            //   ⇒ ConstantOnly（界面 = 纯输入框）。原 LinkOrConstant 会让界面默认停在
            //   「连线」页签、占位符显示「选择上游节点」，误导使用者。
            //   ⚠️ 改这里才生效 —— metadata 端点走反射读 [SkillParam]，
            //   ⛔ 改 wf_skill_input.BindMode 无效且不报错。
            //   AllowEmpty=true：空 = 直接拼接（业务语义明确，⛔ 不是「没配」）。
            [SkillParam(Description = "连接符（空=直接拼接）",
                        BindMode = SkillParamBindMode.ConstantOnly, AllowEmpty = SkillParamEmptyPolicy.Allow)]
            string? joiner = null,

            CancellationToken ct = default)
        {
            var prefix = prefix_text ?? string.Empty;
            var suffix = suffix_text ?? string.Empty;
            var sep = joiner ?? string.Empty;

            var result = string.IsNullOrEmpty(sep)
                ? prefix + suffix
                : prefix + sep + suffix;

            return Task.FromResult(SkillResult.Ok(new Dictionary<string, object>
            {
                ["assembled_text"] = result
            }));
        }
    }
}

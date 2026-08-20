using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using YZH.Core.Workflow;

namespace YZH.Core.Skills
{
    /// <summary>
    /// 文本拼接：将前半部分和后半部分按连接符拼接为一个字符串。纯函数，无副作用。
    /// </summary>
    [Skill(
        Code = "assemble",
        Name = "文本拼接",
        ReturnType = "string",
        Description = "将前半部分文本和后半部分文本按连接符拼接为一个字符串"
    )]
    public static class AssembleSkill
    {
        public static Task<SkillResult> ExecuteAsync(
            [SkillParam(Description = "前半部分文本（合并前）", BindMode = SkillParamBindMode.LinkOrConstant)]
            string? prefix_text = null,

            [SkillParam(Description = "后半部分文本（合并后）", BindMode = SkillParamBindMode.LinkOrConstant)]
            string? suffix_text = null,

            [SkillParam(Description = "连接符（空=直接拼接）", BindMode = SkillParamBindMode.LinkOrConstant)]
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

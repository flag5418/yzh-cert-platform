using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CertPlatform.Shared.Office;

namespace CertPlatform.Admin.Services.Workflow.Skills.Fill
{
    /// <summary>
    /// 人工待办声明 —— 声明「该锚点需人工填写」，产出**待办标记**，⛔ <b>不产值</b>。
    ///
    /// <para><b>★ 为什么返回 <c>hit=false</c>（38 号 §5.2 / 39 号 §5.3）</b>：
    /// 来源链的语义是 <c>firstHit</c>（第一个命中的来源胜出）。本 Skill 没有值 ⇒
    /// 必须让组合器<b>继续往下找</b>；只有<b>全部来源都落空</b>时，编排器才把
    /// <see cref="FillSession.Todos"/> 转成 <c>OfficeFillReport.Pendings</c>。
    /// ⇒ <c>hit=false</c> 是<b>正确语义</b>，⛔ 不是 bug。</para>
    ///
    /// <para>⚠️ 若把 <c>hit</c> 改成 <c>true</c>，症状是「来源链在人工待办处提前终止，
    /// 后面还有能取到值的来源也不会被尝试」——而报告里看不出任何异常。</para>
    /// </summary>
    [Skill(
        Code = "src_manual",
        Name = "人工待办声明",
        ReturnType = "json",
        Description = "声明「该锚点需人工填写」，产出待办标记（不产值）。"
    )]
    public static class SrcManualSkill
    {
        /// <summary>默认提示语</summary>
        private const string DefaultHint = "请人工填写";

        /// <summary>
        /// 执行 —— 产出 <see cref="FillTodo"/>，并回显 <c>hit=false</c>。
        /// </summary>
        /// <param name="anchor_code">锚点编码（必填）</param>
        /// <param name="hint">给填写人的提示（空 ⇒ 用默认提示）</param>
        /// <param name="ct">取消令牌</param>
        public static Task<SkillResult> ExecuteAsync(
            [SkillParam(Description = "锚点编码")]
            string anchor_code,

            [SkillParam(Description = "给填写人的提示，如「请填写企业名称」")]
            string hint = "",

            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(anchor_code))
                return Task.FromResult(SkillResult.Fail("anchor_code 不能为空"));

            var todo = new FillTodo
            {
                AnchorCode = anchor_code,
                Hint = string.IsNullOrWhiteSpace(hint) ? DefaultHint : hint,
                Source = "src_manual",
            };

            // ★ 不返回 value ⇒ 上游组合器（firstHit）会继续找下一个来源
            return Task.FromResult(SkillResult.Ok(new Dictionary<string, object>
            {
                ["todo"] = todo,
                ["anchor_code"] = anchor_code,
                ["hit"] = false,
            }));
        }
    }
}

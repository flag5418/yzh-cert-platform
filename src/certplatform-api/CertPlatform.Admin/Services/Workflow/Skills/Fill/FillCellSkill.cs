using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CertPlatform.Shared.Office;

namespace CertPlatform.Admin.Services.Workflow.Skills.Fill
{
    /// <summary>
    /// 单元格填写（操作类）—— 把值**装配**进 <see cref="FillSession.Request"/>，★ <b>⛔ 不落盘</b>。
    ///
    /// <para><b>★ 为什么「不落盘」是硬约束（38 号 §14.5 / §14.8）</b>：
    /// 一份模板上通常有几十个锚点、可能来自不同 Skill。若每个 Skill 各写一次文件，
    /// 后写的会把先写的<b>整份覆盖掉</b>（每次都是「从模板字节重新打开 → 写自己那部分 → 存回」）。
    /// ⇒ 各 Skill <b>只往会话里累积指令</b>，由编排器在最后<b>统一落盘一次</b>。</para>
    ///
    /// <para><b>★ 值形态的两种来源</b>（见 <see cref="FillValueFactory.Coerce"/>）：
    /// ① 编排器直连 —— 放真的 <see cref="FillValue"/> 实例；
    /// ② 规则/JSON 链 —— 放「值描述」字典或 <c>JsonElement</c>。</para>
    ///
    /// <para><b>★ 与 39 号 §10.2 的一处刻意偏离</b>：原文直接
    /// <c>session.AnchorOwners[kv.Key] = "fill_cell"</c>（无冲突检测），
    /// 但 <see cref="FillSession.TryClaim"/> 的注释明确要求「写值前<b>必须</b>调用」。
    /// 本实现改用 <c>TryClaim</c> —— 否则「两个来源都写同一锚点」会静默让后者赢，
    /// 结果依赖执行顺序，<b>不可复现且极难查</b>。</para>
    /// </summary>
    [Skill(
        Code = "fill_cell",
        Name = "单元格填写",
        ReturnType = "json",
        Description = "把值装配成单元格填充指令（含页眉）。★ 不落盘。"
    )]
    public static class FillCellSkill
    {
        /// <summary>本 Skill 编码（写进 <see cref="FillSession.AnchorOwners"/> 与轨迹）</summary>
        public const string SkillCode = "fill_cell";

        /// <summary>执行 —— 归一值 → 认领锚点 → 累积进会话。</summary>
        /// <param name="session">★ 填充会话（编排器直接放对象实例；空 ⇒ 新建，支持单项试跑）</param>
        /// <param name="values">值字典：<c>{AnchorCode: FillValue 或 值描述}</c></param>
        /// <param name="fill_header">是否处理 Word 页眉（★ 只替换，⛔ 不新建）</param>
        /// <param name="keep_unresolved">未命中锚点是否保留原文（仅模板调试用）</param>
        /// <param name="ct">取消令牌</param>
        public static Task<SkillResult> ExecuteAsync(
            [SkillParam(Description = "★ 填充会话（编排器直接放对象实例；空=新建）",
                        BindMode = SkillParamBindMode.LinkOrConstant)]
            FillSession? session = null,

            [SkillParam(Description = "值字典：{AnchorCode: FillValue 或 值描述}",
                        BindMode = SkillParamBindMode.LinkOrConstant)]
            IDictionary<string, object>? values = null,

            [SkillParam(Description = "是否处理 Word 页眉")]
            bool fill_header = true,

            [SkillParam(Description = "未命中锚点是否保留原文（仅模板调试）")]
            bool keep_unresolved = false,

            CancellationToken ct = default)
        {
            if (values == null || values.Count == 0)
                return Task.FromResult(SkillResult.Fail("values 不能为空"));

            session ??= new FillSession();                 // ★ 单项试跑可自行新建
            session.Request.FillHeader = fill_header;
            session.Request.KeepUnresolvedAsIs = keep_unresolved;

            var written = 0;
            foreach (var kv in values)
            {
                // ① 归一：FillValue 实例（编排器直连）或 值描述（JSON 链）⇒ 都转成 FillValue
                var fv = FillValueFactory.Coerce(kv.Key, kv.Value);
                if (fv == null)
                    return Task.FromResult(SkillResult.Fail(
                        $"锚点 {kv.Key} 的值无法识别：{kv.Value?.GetType().Name ?? "null"}"));

                // ② ★ 认领锚点 —— 已被别的来源写过 ⇒ 必须报错（⛔ 不静默覆盖）
                if (!session.TryClaim(kv.Key, SkillCode, out var conflict))
                    return Task.FromResult(SkillResult.Fail(
                        $"锚点 {kv.Key} 已被「{conflict}」赋值（⛔ 不覆盖；请检查来源链是否配重）"));

                session.Request.Values[kv.Key] = fv;
                written++;
            }

            session.Log(SkillCode, $"+{written} anchors");

            return Task.FromResult(SkillResult.Ok(new Dictionary<string, object>
            {
                ["session"] = session,
                ["anchor_count"] = session.Request.Values.Count,
                ["written"] = written,
            }));
        }
    }
}

namespace CertPlatform.Shared.Fill.Resolvers;

/// <summary>
/// 能力① <b>全局参数</b> —— 认领 <c>{{param_code}}</c>（裸参数名，如 <c>{{company_name}}</c>）。
///
/// <para><b>★ 这是文档填充的主干</b>：25 册纸面实验实测 1,271 个企业相关锚点中，
/// <b>98.6% 可由结构化字段确定性推出</b>，而其中绝大多数正是本类处理的裸参数名。
/// 换句话说，<b>「全局参数配好了，文档就填好了大半」</b>。</para>
///
/// <para><b>取值口</b>：<see cref="FillContext.Params"/> —— 由调用方从
/// <c>cert_fill_param_value</c>（企业完善后的值）装配。参数编码与值来自
/// 后台「体系认证全局参数定义」按「机构 × 标准 × 阶段」预定义的那批。</para>
///
/// <para><b>三种未解析原因（必须区分，处置完全不同）</b>：</para>
/// <list type="number">
///   <item><b>模板写了但定义里没有</b> → 该参数没在后台定义过 → 去后台「全局参数定义」补一条；</item>
///   <item><b>定义里有但企业没填</b> → 去企业端「企业全局参数定义」完善；</item>
///   <item><b>参数被禁用（IsValid=0）</b> → 定义存在但不可用 → 去后台启用。</item>
/// </list>
/// <para>本类只能看到第 2、3 种（因为 <see cref="FillContext"/> 只带「已装配的值」）。
/// 第 1 种由调用方在装配前用「定义全集 − 模板锚点集」的差集识别 ——
/// 见 <c>DocumentFillController</c> 的 <c>unknownTokens</c> 字段。</para>
///
/// <para><b>兜底语义</b>：本类是链上最后一个，用 <see cref="FillSyntax.IsClaimedByOthers"/>
/// 主动拒收其它能力的 token（ai / @ / 表达式 / 表格锚点，拒收理由见该方法的注释）。</para>
/// </summary>
public sealed class GlobalParamResolver : FillResolverBase
{
    public override string Kind => "global";

    public override string DisplayName => "全局参数";

    /// <summary>最后跑：前三类更具体的 token 必须先被各自 Resolver 认领</summary>
    public override int Order => 100;

    public override bool Accepts(string key) => !FillSyntax.IsClaimedByOthers(key);

    protected override (bool Ok, string Value, string Source) Evaluate(string key, FillContext ctx)
    {
        if (string.IsNullOrWhiteSpace(key))
            return (false, "空锚点（{{}} 内没有内容）", string.Empty);

        if (ctx.Params.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            var label = ctx.ParamNames.TryGetValue(key, out var n) && !string.IsNullOrWhiteSpace(n) ? n : key;
            return (true, value, $"全局参数 · {label}");
        }

        return (false, $"参数「{key}」尚未完善，请到「企业全局参数定义」填写", string.Empty);
    }
}

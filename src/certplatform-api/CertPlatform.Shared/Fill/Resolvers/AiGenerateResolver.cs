namespace CertPlatform.Shared.Fill.Resolvers;

/// <summary>
/// 能力③ <b>AI 自动生成</b> —— 认领 <c>{{ai:param_code}}</c>。
///
/// <para><b>★ 语义（刻意与数据模型对齐，不要另起一套）</b>：
/// <c>{{ai:quality_policy}}</c> 的含义是「取参数 <c>quality_policy</c> 的值，
/// 而该参数的<b>性质</b>是 AI 生成」—— 对应 <c>cert_fill_param_def.SourceKind = 'ai'</c>，
/// 值同样落在 <c>cert_fill_param_value</c>。</para>
///
/// <para><b>为什么 AI 结果也走参数表</b>：</para>
/// <list type="number">
///   <item>「AI 生成的」和「企业手填的」在<b>文档填充环节</b>没有区别，都是「某参数有一个值」；</item>
///   <item>走参数表 ⇒ 生成结果<b>可被人工覆盖</b>（企业不认可 AI 写的质量方针时直接改），
///         若绕过参数表直连模型，这个覆盖口就不存在了；</item>
///   <item>走参数表 ⇒ 同一份参数可<b>一次生成、多文档复用</b>（167 份文档里质量方针出现 20+ 次）。</item>
/// </list>
///
/// <para><b>本期边界（诚实声明）</b>：本 Resolver <b>不</b>直连大模型。它只做「取值 + 标注来源」。
/// 真正的生成发生在<b>参数完善环节</b>（企业端/AI 服务的 <c>ai-generate</c> 端点产出提示词，
/// 由模型或人工回填）。这样切分的好处是：填充引擎保持<b>纯函数、可复现、可离线</b>，
/// 而模型调用是易变的、需要联网与计费的 —— 两者耦合会让 167 份文档的批量填充不可重跑。</para>
///
/// <para>接真模型 = 实现一个「参数值生成器」并挂到 <c>ai-generate</c> 端点，<b>本类无需改动</b>。</para>
/// </summary>
public sealed class AiGenerateResolver : FillResolverBase
{
    public override string Kind => "ai";

    public override string DisplayName => "AI 自动生成";

    /// <summary>★ 必须最先跑：否则 <c>{{ai:xxx}}</c> 会被 GlobalParamResolver 收走并报「无此参数」</summary>
    public override int Order => 10;

    public override bool Accepts(string key) =>
        key.StartsWith(FillSyntax.AiPrefix, StringComparison.OrdinalIgnoreCase);

    protected override (bool Ok, string Value, string Source) Evaluate(string key, FillContext ctx)
    {
        var paramCode = key[FillSyntax.AiPrefix.Length..].Trim();

        if (!ctx.AiEnabled)
            return (false, "AI 生成未启用（当前按纯替换模式填充）", string.Empty);

        if (string.IsNullOrWhiteSpace(paramCode))
            return (false, "锚点未指定参数编码（正确写法：{{ai:quality_policy}}）", string.Empty);

        if (ctx.Params.TryGetValue(paramCode, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            var label = ctx.ParamNames.TryGetValue(paramCode, out var n) && !string.IsNullOrWhiteSpace(n)
                ? n
                : paramCode;
            return (true, value, $"AI 生成 · {label}");
        }

        return (false, $"待 AI 生成（参数「{paramCode}」尚无值，请先在参数完善页生成或人工填写）", string.Empty);
    }
}

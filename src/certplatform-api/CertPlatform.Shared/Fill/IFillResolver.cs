using System.Text.RegularExpressions;

namespace CertPlatform.Shared.Fill;

/// <summary>
/// 填充解析器 —— 一种「能力」对应一个实现。
///
/// <para><b>★ 能力清单（与用户 2026-10-02 点名的 4 项核心能力 1:1 对应）</b>：</para>
/// <list type="table">
///   <listheader><term>Kind</term><description>实现 / 能力</description></listheader>
///   <item><term><c>ai</c></term><description><see cref="Resolvers.AiGenerateResolver"/> —— AI 自动生成</description></item>
///   <item><term><c>headerFooter</c></term><description><see cref="Resolvers.HeaderFooterResolver"/> —— 页眉页脚</description></item>
///   <item><term><c>replace</c></term><description><see cref="Resolvers.ReplaceResolver"/> —— 替换（表达式直取）</description></item>
///   <item><term><c>global</c></term><description><see cref="Resolvers.GlobalParamResolver"/> —— 全局参数</description></item>
/// </list>
///
/// <para><b>链式契约</b>：引擎按 <see cref="Order"/> 升序依次调用，
/// 每个 Resolver 的 <see cref="Resolve"/> 输入是<b>上一个的输出</b>，返回本次的替换结果。
/// Resolver 之间<b>互不知晓</b>——各自只认自己那类 token，其余原样透传。</para>
///
/// <para>⛔ 新增能力 = 新增一个 Resolver + 注册进 <see cref="DocumentFillEngine"/>，
/// <b>不要</b>修改既有 Resolver 去兼容新语法（否则 4 项能力会重新耦合回一个巨型 switch）。</para>
/// </summary>
public interface IFillResolver
{
    /// <summary>能力标识（写入 <see cref="FillHit.Kind"/>）：global | replace | headerFooter | ai</summary>
    string Kind { get; }

    /// <summary>中文名（界面展示，如「AI 自动生成」）</summary>
    string DisplayName { get; }

    /// <summary>执行顺序（小的先跑）。AI 最先——它要先把 <c>{{ai:*}}</c> 收走，
    /// 否则会落到 GlobalParamResolver 被记成「无此参数」。</summary>
    int Order { get; }

    /// <summary>本 Resolver 是否认领该 token（<b>纯判定</b>，不做替换）</summary>
    bool Accepts(string key);

    /// <summary>
    /// 替换全部认领的 token。
    /// <para>返回：替换后的文本 + 本次命中的记录 + 本次认领但无法解析的待办。</para>
    /// </summary>
    (string Output, List<FillHit> Hits, List<FillPending> Pendings) Resolve(string input, FillContext ctx);

    /// <summary>
    ///     ★ <b>单键求值</b> —— 不经 token 扫描，直接按 <paramref name="key"/> 取值。
    ///     供**锚点驱动**的调用方（编排器真填）复用与 <see cref="Resolve"/> <b>完全相同</b>的
    ///     取值逻辑与错误话术；返回的是**未截断**的完整值。
    ///     <para>实现由 <see cref="FillResolverBase"/> 统一提供，各能力无需重写。</para>
    /// </summary>
    /// <param name="key">要取的键（<c>enterprise.Name</c> / <c>@doc_no</c> / 参数编码 …）</param>
    /// <param name="ctx">填充上下文</param>
    /// <param name="value">取值成功时的<b>完整值</b>（未截断）</param>
    /// <param name="source">来源标注（写入账本 <c>SourceLabel</c>）</param>
    /// <param name="reason">失败原因（**可直接展示给用户**，含「该去哪补」）</param>
    /// <returns>是否取到非空值</returns>
    bool TryEvaluate(
        string? key,
        FillContext ctx,
        out string value,
        out string source,
        out string reason);
}

/// <summary>
/// Resolver 公共基类：统一 token 扫描、统一「值截断 / 来源标注」写法，减少各实现重复。
/// </summary>
public abstract class FillResolverBase : IFillResolver
{
    /// <summary>报告里值展示的最大长度（超长截断，避免报告本身变成文档副本）</summary>
    private const int ValuePreviewLength = 120;

    public abstract string Kind { get; }
    public abstract string DisplayName { get; }
    public abstract int Order { get; }

    /// <summary>本 Resolver 认领的 token 键</summary>
    public abstract bool Accepts(string key);

    /// <summary>
    /// 求值：返回 <c>(ok, value, source)</c>。
    /// <para><c>ok=false</c> 时 <c>value</c> 为「未解析原因」。</para>
    /// </summary>
    protected abstract (bool Ok, string Value, string Source) Evaluate(string key, FillContext ctx);

    public (string Output, List<FillHit> Hits, List<FillPending> Pendings) Resolve(string input, FillContext ctx)
    {
        var hits = new List<FillHit>();
        var pendings = new List<FillPending>();

        if (string.IsNullOrEmpty(input))
            return (input ?? string.Empty, hits, pendings);

        var output = Regex.Replace(input, FillSyntax.TokenPattern, match =>
        {
            var raw = match.Value;              // {{xxx}}
            var key = match.Groups[1].Value.Trim();   // xxx

            if (!Accepts(key))
                return raw;                     // 不认领 → 原样透传（留给下一个 Resolver）

            var (ok, value, source) = Evaluate(key, ctx);

            if (!ok)
            {
                pendings.Add(new FillPending
                {
                    Token = raw,
                    Key = key,
                    Kind = Kind,
                    Reason = string.IsNullOrWhiteSpace(value) ? "无法解析" : value,
                });
                return raw;                     // 未解析 → 保留原文，便于人工定位
            }

            hits.Add(new FillHit
            {
                Token = raw,
                Key = key,
                Kind = Kind,
                Value = Preview(value),
                Source = source,
            });
            return value;
        });

        return (output, hits, pendings);
    }

    /// <summary>报告展示用截断</summary>
    protected static string Preview(string? value)
    {
        var v = value ?? string.Empty;
        v = v.Replace("\r", " ").Replace("\n", " ").Trim();
        return v.Length <= ValuePreviewLength ? v : v[..ValuePreviewLength] + "…";
    }

    /// <summary>
    ///     ★ <b>单键求值</b> —— 不经 token 扫描，直接按 <paramref name="key"/> 取值。
    ///
    ///     <para><b>为什么需要它</b>：本引擎有两种调用方 ——</para>
    ///     <list type="bullet">
    ///         <item><b>文档驱动</b>（<c>DocumentFillEngine.Fill</c>，试填预览）：
    ///               输入是整段模板文本，走 <see cref="Resolve"/> 扫 <c>{{}}</c>。</item>
    ///         <item><b>锚点驱动</b>（编排器 <c>DocumentFillOrchestrator</c>，真填）：
    ///               输入是「一个锚点 = 一个来源」，调用方<b>已知</b>要取哪个键，
    ///               没有 token 文本可扫。</item>
    ///     </list>
    ///
    ///     <para>⛔ <b>不能让锚点驱动的调用方自己重写一套取值逻辑</b> ——
    ///     那样「企业端显示的值」与「文档里填的值」会漂移，且两边都不报错（静默失效）。
    ///     本方法让两条路径共用同一个 <see cref="Evaluate"/>，含<b>完全一致</b>的中文名与错误话术。</para>
    ///
    ///     <para>⚠️ <b>为什么不直接复用 <see cref="Resolve"/></b>：它返回的
    ///     <see cref="FillHit.Value"/> 是 <see cref="Preview"/> 截断后的（120 字 + 折行压平），
    ///     那是**给报告看的**；真填要的是**完整原值**（企业地址 / 认证范围常超 120 字）。</para>
    /// </summary>
    /// <param name="key">要取的键（<c>enterprise.Name</c> / <c>@doc_no</c> / 参数编码 …）</param>
    /// <param name="ctx">填充上下文</param>
    /// <param name="value">取值成功时的<b>完整值</b>（未截断）</param>
    /// <param name="source">来源标注（写入账本 <c>SourceLabel</c>）</param>
    /// <param name="reason">失败原因（**可直接展示给用户**，含「该去哪补」）</param>
    /// <returns>是否取到非空值</returns>
    public bool TryEvaluate(
        string? key,
        FillContext ctx,
        out string value,
        out string source,
        out string reason)
    {
        value = string.Empty;
        source = string.Empty;

        var k = (key ?? string.Empty).Trim();
        if (k.Length == 0)
        {
            reason = "未指定要取的值（来源属性里没选具体项）";
            return false;
        }

        // ★ 先判「这个键归不归我管」—— 前端把 kind 配错（如 kind=replace 但 ref=裸参数名）时，
        //   本判据会**明确报出**「不是本来源能取的值」，⛔ 不会静默走到 Evaluate 里报一个
        //   指错方向的原因（那会把用户引去改模板，而真正要改的是来源配置）。
        if (!Accepts(k))
        {
            reason = $"「{k}」不是「{DisplayName}」来源能取的值，请检查该锚点的来源配置";
            return false;
        }

        var (ok, v, s) = Evaluate(k, ctx);
        if (!ok)
        {
            reason = string.IsNullOrWhiteSpace(v) ? "无法解析" : v;
            return false;
        }

        value = v;
        source = s;
        reason = string.Empty;
        return true;
    }
}

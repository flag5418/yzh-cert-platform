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
}

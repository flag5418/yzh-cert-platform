using System.Text;
using System.Text.RegularExpressions;
using CertPlatform.Shared.Fill.Resolvers;

namespace CertPlatform.Shared.Fill;

/// <summary>
/// 文档填充引擎 —— 把「模板 + 参数 + 企业属性」变成「成文 + 证据报告」。
///
/// <para><b>★ 定位</b>（对应 05 册 <c>21-架构判断-V1.md</c> 的「辅助系统、非正式报告」）：
/// 本引擎<b>不</b>追求生成一份可直接发布的正式文件，它追求的是
/// <b>「一键看证据摘要」</b> —— 每一处企业相关信息，都能回答「填的是什么值、从哪来的、
/// 还有哪几处没填」。这正是 25 册纸面实验验证过的用户价值。</para>
///
/// <para><b>★ 设计约束（为什么是纯函数）</b>：</para>
/// <list type="number">
///   <item>输入全部由 <see cref="FillContext"/> 显式传入，<b>不读 DB、不读时钟、不读环境</b>
///         （时间也在 <c>ctx.Now</c> 里）。⇒ 同一输入必得同一输出，167 份文档批量填充<b>可重跑</b>；</item>
///   <item>不引用 <c>YZH.Core.DataBase</c> ⇒ 可脱离 Web 宿主单测；</item>
///   <item>AI 生成只做「取值 + 标注」，不在此处调模型（理由见 <see cref="AiGenerateResolver"/>）。</item>
/// </list>
///
/// <para><b>链式执行</b>：按 <see cref="IFillResolver.Order"/> 升序，
/// 每个 Resolver 的输入是上一个的输出。正文 / 页眉 / 页脚<b>各跑一遍完整链</b>，
/// 报告则合并统计（页眉页脚锚点计入总数 —— 它们是真实存在的填写单元）。</para>
/// </summary>
public sealed class DocumentFillEngine
{
    private readonly List<IFillResolver> _resolvers;

    /// <summary>
    /// 用默认 4 项能力构造（全局参数 / 替换 / 页眉页脚 / AI 生成）。
    /// <para>★ 这是<b>唯一</b>的能力注册点：新增能力只改这里，不改任何 Resolver。</para>
    /// </summary>
    public DocumentFillEngine()
        : this(new IFillResolver[]
        {
            new AiGenerateResolver(),
            new HeaderFooterResolver(),
            new ReplaceResolver(),
            new GlobalParamResolver(),
        })
    {
    }

    /// <summary>自定义 Resolver 集合（供测试替换 / 未来扩展）</summary>
    public DocumentFillEngine(IEnumerable<IFillResolver> resolvers)
    {
        _resolvers = resolvers.OrderBy(r => r.Order).ToList();
    }

    /// <summary>已注册的能力（顺序即执行顺序），供界面展示「本系统支持哪些填充能力」</summary>
    public IReadOnlyList<(string Kind, string DisplayName, int Order)> Capabilities =>
        _resolvers.Select(r => (r.Kind, r.DisplayName, r.Order)).ToList();

    /// <summary>
    /// 执行填充。
    /// <para>不抛异常：模板里的任何问题都表达为 <see cref="FillReport.Pendings"/> 里的一条，
    /// 而不是让整份文档填充失败 —— 167 份文档批量跑时，一份模板写错不该让批次中断。</para>
    /// </summary>
    public FillResult Fill(FillContext ctx)
    {
        var report = new FillReport();
        var result = new FillResult { Report = report };

        // ── 1. 正文 ──
        var body = ctx.Template ?? string.Empty;
        RunChain(body, ctx, report, out var filledBody);
        result.Output = filledBody;

        // ── 2. 页眉 / 页脚（本身也是模板，走同一条链）──
        if (!string.IsNullOrWhiteSpace(ctx.HeaderTemplate))
        {
            RunChain(ctx.HeaderTemplate!, ctx, report, out var h);
            result.Header = h;
        }

        if (!string.IsNullOrWhiteSpace(ctx.FooterTemplate))
        {
            RunChain(ctx.FooterTemplate!, ctx, report, out var f);
            result.Footer = f;
        }

        // ── 3. 收尾：识别「无归属解析器」的残留锚点 ──
        //    正常情况此处必为空（GlobalParamResolver 是兜底，会认领所有裸参数名）。
        //    若不为空 ⇒ 引擎配置错误（Resolver 未注册 / 顺序被改坏），必须**显式暴露**，
        //    否则症状是「模板里明明写了 {{xxx}}，输出里原样留着，报告却说 100% 完成」。
        CollectOrphans(result.Output, report);
        if (result.Header != null) CollectOrphans(result.Header, report);
        if (result.Footer != null) CollectOrphans(result.Footer, report);

        return result;
    }

    /// <summary>跑一遍完整 Resolver 链</summary>
    private void RunChain(string input, FillContext ctx, FillReport report, out string output)
    {
        var current = input;

        foreach (var resolver in _resolvers)
        {
            var (next, hits, pendings) = resolver.Resolve(current, ctx);
            current = next;

            report.Hits.AddRange(hits);
            report.Pendings.AddRange(pendings);

            // 按能力累计：index 0 = 已解析数，index 1 = 待办数
            if (hits.Count > 0 || pendings.Count > 0)
            {
                if (!report.ByKind.TryGetValue(resolver.Kind, out var acc))
                {
                    acc = new int[2];
                    report.ByKind[resolver.Kind] = acc;
                }
                acc[0] += hits.Count;
                acc[1] += pendings.Count;
            }
        }

        output = current;
    }

    /// <summary>找出链跑完后仍残留、且未被任何 Resolver 记录的锚点</summary>
    private static void CollectOrphans(string text, FillReport report)
    {
        foreach (Match m in Regex.Matches(text, FillSyntax.TokenPattern))
        {
            var raw = m.Value;
            var key = m.Groups[1].Value.Trim();

            var alreadyKnown = report.Pendings.Any(p => p.Token == raw)
                               || report.Hits.Any(h => h.Token == raw);
            if (alreadyKnown) continue;

            report.Pendings.Add(new FillPending
            {
                Token = raw,
                Key = key,
                Kind = "orphan",
                Reason = "锚点无归属解析器（引擎配置错误：Resolver 未注册或执行顺序被改坏）",
            });
        }
    }

    /// <summary>
    /// 把报告渲染成人类可读的「证据摘要」（Markdown）。
    /// <para>用于「一键看证据摘要」：直接贴进预览页 / 归档进任务结果。</para>
    /// </summary>
    public static string RenderReport(FillReport report, bool includeDetail = true)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"**填充完成度：{report.ResolvedCount}/{report.Total}（{report.Completion:P1}）**");
        sb.AppendLine();

        if (report.Total == 0)
        {
            sb.AppendLine("> 本文档不含任何企业相关锚点 —— 属于「通用模板」，无需按企业填充。");
            return sb.ToString();
        }

        // ── 按能力分布 ──
        sb.AppendLine("| 能力 | 已解析 | 待办 |");
        sb.AppendLine("|---|---:|---:|");
        foreach (var (kind, acc) in report.ByKind.OrderByDescending(kv => kv.Value[0] + kv.Value[1]))
        {
            sb.AppendLine($"| {KindLabel(kind)} | {acc[0]} | {acc[1]} |");
        }
        sb.AppendLine();

        if (includeDetail && report.Pendings.Count > 0)
        {
            sb.AppendLine("**待办清单**");
            sb.AppendLine();

            // ★ 按锚点聚合：同一锚点在正文里出现多次（如页眉的 {{@doc_no}} 每页一次）
            //   应合并成一条并标注出现次数 —— 逐个列会把清单淹没，用户反而看不出「到底缺几件事」。
            var grouped = report.Pendings
                .GroupBy(p => p.Token)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key, StringComparer.Ordinal);

            foreach (var g in grouped)
            {
                var reason = g.First().Reason;
                var times = g.Count() > 1 ? $"（出现 {g.Count()} 次）" : string.Empty;
                sb.AppendLine($"- `{g.Key}`{times} —— {reason}");
            }
        }

        return sb.ToString();
    }

    /// <summary>能力 key → 中文名</summary>
    public static string KindLabel(string kind) => kind switch
    {
        "global" => "全局参数",
        "replace" => "替换",
        "headerFooter" => "页眉页脚",
        "ai" => "AI 自动生成",
        "orphan" => "⚠ 无归属",
        _ => kind,
    };
}

using System.Globalization;

namespace CertPlatform.Shared.Fill.Resolvers;

/// <summary>
/// 能力④ <b>页眉页脚</b> —— 认领 <c>{{@var}}</c>（<c>@</c> 开头的系统 / 文档级自动变量）。
///
/// <para><b>★ 为什么页眉页脚要单独一种能力</b>：它与正文的取值规律不同 ——</para>
/// <list type="table">
///   <listheader><term>维度</term><description>正文锚点 vs 页眉页脚锚点</description></listheader>
///   <item><term>出现频次</term><description>正文每处一个值；页眉页脚<b>每页重复</b>（一份 30 页手册 = 30 次）</description></item>
///   <item><term>取值来源</term><description>正文来自参数/表达式；页眉页脚来自<b>文档元信息</b>（编号/版本/日期/页码）</description></item>
///   <item><term>可空性</term><description>正文缺值 = 待办；页眉页脚缺值 = <b>整份文档都不合格</b>（编号缺失是致命的）</description></item>
/// </list>
/// <para>因此页眉页脚必须有<b>独立的变量集与独立的完整性判据</b>，混进全局参数会让「编号没配」
/// 淹没在几十个正文待办里。</para>
///
/// <para><b>★ 页眉页脚本身也是模板</b>：<see cref="FillContext.HeaderTemplate"/> /
/// <see cref="FillContext.FooterTemplate"/> 允许含锚点（如页脚写
/// <c>{{@company}} · {{@doc_no}} · 第 {{@page}} 页</c>），由引擎走同一条 Resolver 链填充，
/// 故本类只需提供变量，不需要知道页眉页脚的存在。</para>
/// </summary>
public sealed class HeaderFooterResolver : FillResolverBase
{
    public override string Kind => "headerFooter";

    public override string DisplayName => "页眉页脚";

    public override int Order => 20;

    public override bool Accepts(string key) =>
        key.StartsWith(FillSyntax.SysPrefix, StringComparison.Ordinal);

    protected override (bool Ok, string Value, string Source) Evaluate(string key, FillContext ctx)
    {
        // 去掉前导 '@'，再统一按小写匹配（锚点大小写不敏感，避免 {{@Doc_No}} 落空）
        var name = key[FillSyntax.SysPrefix.Length..].Trim().ToLowerInvariant();

        // ★ 先判「名字认不认识」，再判「有没有值」——两件事必须分开。
        //   混在一起的后果（本类初版即踩）：`@doc_no` 名字正确但文档未配编号时，
        //   报出「未知的页眉页脚变量 @doc_no」，把用户引去改**模板**（其实模板没错），
        //   而真正要做的是去**补文档编号**。指错方向比不报错更耗时。
        if (!LabelOf(name, out var label))
            return (false, $"未知的页眉页脚变量「@{name}」（可用：@doc_no / @doc_title / @version / " +
                           $"@standard_no / @stage_name / @page / @company / @company_short / @org / " +
                           $"@date / @date_cn / @year / @month / @day）", string.Empty);

        var value = ValueOf(name, ctx);

        if (string.IsNullOrWhiteSpace(value))
            return (false, $"页眉页脚变量「@{name}」（{label}）未提供值 —— 请检查该文档的元信息是否已配置", string.Empty);

        return (true, value, $"页眉页脚 · {label}");
    }

    /// <summary>变量中文名（同时充当「名字是否合法」的判据）</summary>
    private static bool LabelOf(string name, out string label)
    {
        label = name switch
        {
            "doc_no" => "文档编号",
            "doc_title" => "文档名称",
            "version" => "版本号",
            "standard_no" => "标准编号",
            "stage_name" => "认证阶段",
            "page" => "页码",
            "company" => "企业全称",
            "company_short" => "企业简称",
            "org" => "机构名称",
            "date" => "当前日期",
            "date_cn" => "当前日期（中文）",
            "year" => "年份",
            "month" => "月份",
            "day" => "日",
            _ => string.Empty,
        };
        return label.Length > 0;
    }

    /// <summary>取值（仅在 <see cref="LabelOf"/> 确认名字合法后调用）</summary>
    private static string? ValueOf(string name, FillContext ctx) => name switch
    {
        // ── 文档元信息 ──
        "doc_no" => ctx.Doc.No,
        "doc_title" => ctx.Doc.Title,
        "version" => ctx.Doc.Version,
        "standard_no" => ctx.Doc.StandardNo,
        "stage_name" => ctx.Doc.StageName,
        // 页码恒有值：单文档预览不翻页，缺省第 1 页
        "page" => string.IsNullOrWhiteSpace(ctx.Doc.Page) ? "1" : ctx.Doc.Page,

        // ── 主体信息（页眉最常见：企业名 / 机构名）──
        "company" => ctx.Enterprise.Name,
        "company_short" => string.IsNullOrWhiteSpace(ctx.Enterprise.ShortName)
            ? ctx.Enterprise.Name
            : ctx.Enterprise.ShortName,
        "org" => ctx.Org.Name,

        // ── 日期族（恒有值）──
        "date" => ctx.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        "date_cn" => ctx.Now.ToString("yyyy年M月d日", CultureInfo.InvariantCulture),
        "year" => ctx.Now.Year.ToString(CultureInfo.InvariantCulture),
        "month" => ctx.Now.Month.ToString(CultureInfo.InvariantCulture),
        "day" => ctx.Now.Day.ToString(CultureInfo.InvariantCulture),

        _ => null,
    };
}

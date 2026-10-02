using System.Globalization;

namespace CertPlatform.Shared.Fill.Resolvers;

/// <summary>
/// 能力② <b>替换</b> —— 认领 <c>{{enterprise.*}}</c> / <c>{{org.*}}</c> / <c>{{system.*}}</c>。
///
/// <para><b>★ 「替换」与「全局参数」的分界（这是本模块最容易搞错的一处）</b>：</para>
/// <list type="table">
///   <listheader><term></term><description>替换（本类） vs 全局参数（GlobalParamResolver）</description></listheader>
///   <item><term>取值路径</term>
///         <description>替换：<b>直接读企业/机构/系统属性</b>，不经参数表<br/>
///                     全局参数：读 <c>cert_fill_param_value</c>（企业完善后的值）</description></item>
///   <item><term>可否被企业改写</term>
///         <description>替换：<b>不可</b>（永远等于企业档案，改档案即改文档）<br/>
///                     全局参数：<b>可</b>（企业可在参数完善页覆盖）</description></item>
///   <item><term>典型用例</term>
///         <description>替换：<c>{{enterprise.Name}}</c>、<c>{{system.date}}</c>（法律主体名、制表日期）<br/>
///                     全局参数：<c>{{doc_prefix}}</c>、<c>{{quality_policy}}</c>（可配置、可覆盖）</description></item>
/// </list>
///
/// <para><b>判据（写模板时照此选）</b>：这个位置<b>是否允许企业填一个和企业档案不同的值</b>？
/// 允许 → 全局参数；不允许 → 替换。选错的后果：用替换写「文件编号前缀」，
/// 则企业在参数页改了也不生效（改的是 <c>cert_fill_param_value</c>，而文档读的是 <c>cert_enterprise</c>）——
/// <b>两边都不报错</b>，只是文档里一直是旧值。</para>
///
/// <para><b>为什么保留 <c>system.*</c></b>：制表日期、打印日期这类值必须<b>在填充时刻</b>求值，
/// 不能预存进参数表（否则一份 2025 年生成的手册，2026 年复用时会印着 2025 年。</para>
/// </summary>
public sealed class ReplaceResolver : FillResolverBase
{
    public override string Kind => "replace";

    public override string DisplayName => "替换";

    /// <summary>在 headerFooter 之后、global 之前：表达式比裸参数名更具体，应先认领</summary>
    public override int Order => 30;

    public override bool Accepts(string key) =>
        FillSyntax.ExprNamespaces.Any(ns => key.StartsWith(ns, StringComparison.OrdinalIgnoreCase));

    protected override (bool Ok, string Value, string Source) Evaluate(string key, FillContext ctx)
    {
        var dot = key.IndexOf('.');
        var ns = key[..dot].Trim().ToLowerInvariant();
        var attr = key[(dot + 1)..].Trim();

        if (string.IsNullOrWhiteSpace(attr))
            return (false, $"表达式「{key}」缺少属性名", string.Empty);

        return ns switch
        {
            "enterprise" => FromEnterprise(attr, ctx),
            "org" => FromOrg(attr, ctx),
            "system" => FromSystem(attr, ctx),
            _ => (false, $"未知的表达式命名空间「{ns}」", string.Empty),
        };
    }

    private static (bool, string, string) FromEnterprise(string attr, FillContext ctx)
    {
        var value = ctx.Enterprise.Get(attr);

        // ★ 已知属性但值为空 ≠ 未知属性 —— 前者是「企业档案没填」，后者是「模板写错了字段名」。
        //   两种情况的处置完全不同（补档案 vs 改模板），必须在 reason 里区分，
        //   否则用户会去企业档案里找一个根本不存在的字段。
        var known = EnterpriseAttrLabels.TryGetValue(attr, out var label);
        if (!known)
            return (false, $"未知的企业属性「{attr}」（模板写错字段名，请检查 {{enterprise.{attr}}}）", string.Empty);

        if (string.IsNullOrWhiteSpace(value))
            return (false, $"企业档案中「{label}」为空，请先到「企业管理」补齐", string.Empty);

        return (true, value, $"企业基础信息 · {label}");
    }

    private static (bool, string, string) FromOrg(string attr, FillContext ctx)
    {
        var value = ctx.Org.Get(attr);
        if (value == null)
            return (false, $"未知的机构属性「{attr}」", string.Empty);
        if (string.IsNullOrWhiteSpace(value))
            return (false, $"机构信息「{attr}」为空", string.Empty);
        return (true, value, $"机构信息 · {attr}");
    }

    private static (bool, string, string) FromSystem(string attr, FillContext ctx)
    {
        var now = ctx.Now;
        var value = attr.ToLowerInvariant() switch
        {
            "date" => now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            "datetime" => now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            "date_cn" => now.ToString("yyyy年M月d日", CultureInfo.InvariantCulture),
            "year" => now.Year.ToString(CultureInfo.InvariantCulture),
            "month" => now.Month.ToString(CultureInfo.InvariantCulture),
            "day" => now.Day.ToString(CultureInfo.InvariantCulture),
            _ => null,
        };

        if (value == null)
            return (false, $"未知的系统变量「system.{attr}」", string.Empty);

        return (true, value, $"系统 · {attr}");
    }

    /// <summary>
    /// 企业属性的中文名（用于报告来源标注与「字段名写错」的区分）。
    /// <para><b>★ 全项目唯一口径</b>：<c>FillParamDefController.EnterpriseAttrs</c>（后台配置页的属性目录）
    /// 与 <c>FillParamValueController</c>（企业端自动带出）都必须读本表 ——
    /// 新增企业字段时<b>只改这一处</b>，否则会出现「后台能选、企业端带不出」这类两边不报错的错位。</para>
    /// </summary>
    public static readonly Dictionary<string, string> EnterpriseAttrLabels = new(StringComparer.Ordinal)
    {
        ["Code"] = "企业编码",
        ["Name"] = "企业全称",
        ["ShortName"] = "企业简称",
        ["CreditCode"] = "统一社会信用代码",
        ["LegalPerson"] = "法定代表人",
        ["Province"] = "省份",
        ["City"] = "城市",
        ["Address"] = "企业地址",
        ["IndustryType"] = "行业类型",
        ["EmployeeCount"] = "员工人数",
        ["CertScope"] = "认证范围",
        ["ContactName"] = "体系对接人",
        ["ContactPhone"] = "联系电话",
        ["ContactEmail"] = "联系邮箱",
        ["EnterpriseNo"] = "企业编号",
        ["ArchiveDate"] = "归档日期",
    };
}

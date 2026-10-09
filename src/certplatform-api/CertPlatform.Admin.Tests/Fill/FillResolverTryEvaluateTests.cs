using System;
using CertPlatform.Shared.Fill;
using CertPlatform.Shared.Fill.Resolvers;
using Xunit;

namespace CertPlatform.Admin.Tests.Fill;

/// <summary>
///     <see cref="IFillResolver.TryEvaluate"/> 的契约测试。
///
///     <para><b>为什么这个方法值得单测</b>：它让<b>两条互不相干的调用路径</b>共用同一个取值实现 ——</para>
///     <list type="bullet">
///         <item><b>文档驱动</b>：<c>DocumentFillEngine.Fill</c>（试填预览）走 <c>Resolve</c> 扫 <c>{{}}</c>；</item>
///         <item><b>锚点驱动</b>：<c>DocumentFillOrchestrator</c>（真填）走 <c>TryEvaluate</c> 单键取值。</item>
///     </list>
///
///     <para>一旦两者漂移，症状是「<b>预览里填对了、真填出来是空的</b>」，且<b>两边都不报错</b> ——
///     属于本项目最坏的失败形态（静默失效）。所以这里钉三条：</para>
///     <list type="number">
///         <item><b>⛔ 不截断</b>：<c>Resolve</c> 返回的 <c>FillHit.Value</c> 是给报告看的（120 字 + 折行压平）；
///               <c>TryEvaluate</c> 必须给<b>完整原值</b>，否则企业地址 / 认证范围会被砍半印进正式文件。</item>
///         <item><b>⛔ 不越界认领</b>：把 <c>@doc_no</c> 交给 <c>ReplaceResolver</c> 必须**明确报错**，
///               而不是静默返回一个指错方向的原因。</item>
///         <item><b>⛔ 「未知名字」与「已知但没值」必须分开报</b>：前者要改模板，后者要补档案 / 补文档元信息，
///               混在一起会把用户引去改一个本来没错的地方。</item>
///     </list>
/// </summary>
public class FillResolverTryEvaluateTests
{
    /// <summary>固定的基准时间 —— 保证 <c>system.*</c> 断言可复现</summary>
    private static readonly DateTime FixedNow = new(2026, 10, 9, 15, 30, 0, DateTimeKind.Unspecified);

    private static FillContext Ctx() => new()
    {
        Enterprise = new EnterpriseInfo
        {
            Code = "ENT001",
            Name = "某某科技有限公司",
            ShortName = "某某科技",
            CreditCode = "91310000MA1FL0XXXX",
            Address = "上海市浦东新区某某路 100 号",
            // ★ 故意超 120 字 —— 用来验证 TryEvaluate **不截断**
            CertScope = new string('范', 200),
            EmployeeCount = 120,
            ArchiveDate = new DateTime(2026, 3, 1),
        },
        Org = new OrgInfo
        {
            Code = "ORG001",
            Name = "某某认证有限公司",
            ShortName = "某某认证",
            ContactPhone = "021-12345678",
        },
        Doc = new DocInfo
        {
            No = "YZH-QM-2026-001",
            Title = "质量手册",
            Version = "A/0",
            StandardNo = "GB/T 19001-2016",
            StageName = "初次认证",
            Page = "1",
        },
        Now = FixedNow,
    };

    // ════════════════════════════════════════════════════════════════
    //  replace —— enterprise.* / org.* / system.*
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void replace_企业属性_取到值并带中文名来源()
    {
        var ok = new ReplaceResolver().TryEvaluate(
            "enterprise.Name", Ctx(), out var value, out var source, out var reason);

        Assert.True(ok);
        Assert.Equal("某某科技有限公司", value);
        Assert.Equal("企业基础信息 · 企业全称", source);
        Assert.Equal(string.Empty, reason);
    }

    [Fact]
    public void replace_机构属性_取到值()
    {
        var ok = new ReplaceResolver().TryEvaluate(
            "org.Name", Ctx(), out var value, out var source, out _);

        Assert.True(ok);
        Assert.Equal("某某认证有限公司", value);
        Assert.Equal("机构信息 · Name", source);
    }

    [Fact]
    public void replace_系统变量_按基准时间求值_可复现()
    {
        var r = new ReplaceResolver();

        Assert.True(r.TryEvaluate("system.date", Ctx(), out var date, out _, out _));
        Assert.Equal("2026-10-09", date);

        Assert.True(r.TryEvaluate("system.date_cn", Ctx(), out var cn, out _, out _));
        Assert.Equal("2026年10月9日", cn);

        Assert.True(r.TryEvaluate("system.year", Ctx(), out var year, out _, out _));
        Assert.Equal("2026", year);
    }

    [Fact]
    public void replace_未知企业属性_报未知并指向模板()
    {
        var ok = new ReplaceResolver().TryEvaluate(
            "enterprise.NotAField", Ctx(), out var value, out _, out var reason);

        Assert.False(ok);
        Assert.Equal(string.Empty, value);
        Assert.Contains("未知的企业属性", reason);
    }

    [Fact]
    public void replace_已知属性但档案为空_报去补档案而不是未知()
    {
        var ctx = Ctx();
        ctx.Enterprise.ContactEmail = null; // 档案里没填

        var ok = new ReplaceResolver().TryEvaluate(
            "enterprise.ContactEmail", ctx, out _, out _, out var reason);

        Assert.False(ok);
        // ★ 这一条是「指对方向」的关键：不能报「未知的企业属性」
        Assert.DoesNotContain("未知", reason);
        Assert.Contains("为空", reason);
        Assert.Contains("企业管理", reason);
    }

    // ════════════════════════════════════════════════════════════════
    //  headerFooter —— @doc_* / @stage_name / @page …
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void headerFooter_文档信息_取到值()
    {
        var r = new HeaderFooterResolver();

        Assert.True(r.TryEvaluate("@doc_no", Ctx(), out var no, out var src, out _));
        Assert.Equal("YZH-QM-2026-001", no);
        Assert.Equal("页眉页脚 · 文档编号", src);

        Assert.True(r.TryEvaluate("@stage_name", Ctx(), out var stage, out _, out _));
        Assert.Equal("初次认证", stage);

        Assert.True(r.TryEvaluate("@standard_no", Ctx(), out var std, out _, out _));
        Assert.Equal("GB/T 19001-2016", std);
    }

    [Fact]
    public void headerFooter_大小写不敏感()
    {
        // 模板里写成 {{@Doc_No}} 也要认得
        Assert.True(new HeaderFooterResolver().TryEvaluate(
            "@Doc_No", Ctx(), out var value, out _, out _));
        Assert.Equal("YZH-QM-2026-001", value);
    }

    [Fact]
    public void headerFooter_页码恒有值()
    {
        var ctx = Ctx();
        ctx.Doc.Page = null; // 未配 ⇒ 缺省第 1 页

        Assert.True(new HeaderFooterResolver().TryEvaluate("@page", ctx, out var page, out _, out _));
        Assert.Equal("1", page);
    }

    [Fact]
    public void headerFooter_未知变量_报未知变量并列出可用项()
    {
        var ok = new HeaderFooterResolver().TryEvaluate(
            "@zzz", Ctx(), out _, out _, out var reason);

        Assert.False(ok);
        Assert.Contains("未知的页眉页脚变量", reason);
        Assert.Contains("@doc_no", reason); // 可用清单必须给出来
    }

    [Fact]
    public void headerFooter_已知变量但文档元信息为空_报未提供值而不是未知()
    {
        var ctx = Ctx();
        ctx.Doc.No = null; // ★ 当前真实状态：库里没有文档编号来源

        var ok = new HeaderFooterResolver().TryEvaluate(
            "@doc_no", ctx, out _, out _, out var reason);

        Assert.False(ok);
        // ★ 名字是对的、值没有 ⇒ 必须报「未提供值」，⛔ 不能报「未知变量」（那会把用户引去改模板）
        Assert.DoesNotContain("未知", reason);
        Assert.Contains("未提供值", reason);
    }

    // ════════════════════════════════════════════════════════════════
    //  ★ 不越界认领（两条路径共用实现的前提）
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void 跨能力键_明确报错而不是静默返回指错方向的原因()
    {
        // 把 `@doc_no` 交给 replace
        var okReplace = new ReplaceResolver().TryEvaluate(
            "@doc_no", Ctx(), out _, out _, out var whyReplace);
        Assert.False(okReplace);
        Assert.Contains("不是", whyReplace);
        Assert.Contains("替换", whyReplace);

        // 把 `enterprise.Name` 交给 headerFooter
        var okHf = new HeaderFooterResolver().TryEvaluate(
            "enterprise.Name", Ctx(), out _, out _, out var whyHf);
        Assert.False(okHf);
        Assert.Contains("不是", whyHf);
        Assert.Contains("页眉页脚", whyHf);
    }

    [Fact]
    public void 空键_报没选具体项()
    {
        Assert.False(new ReplaceResolver().TryEvaluate(null, Ctx(), out _, out _, out var r1));
        Assert.Contains("未指定", r1);

        Assert.False(new HeaderFooterResolver().TryEvaluate("   ", Ctx(), out _, out _, out var r2));
        Assert.Contains("未指定", r2);
    }

    // ════════════════════════════════════════════════════════════════
    //  ★★ 不截断 —— 本方法存在的**根本理由**
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void 长值不截断_与Resolve的FillHit形成对照()
    {
        const int len = 200;
        var ctx = Ctx();

        // ① TryEvaluate ⇒ 完整原值
        var ok = new ReplaceResolver().TryEvaluate(
            "enterprise.CertScope", ctx, out var full, out _, out _);
        Assert.True(ok);
        Assert.Equal(len, full.Length);

        // ② Resolve ⇒ FillHit.Value 被 Preview 截到 120 + '…'（那是**给报告看的**）
        var (_, hits, _) = new ReplaceResolver().Resolve("{{enterprise.CertScope}}", ctx);
        var hit = Assert.Single(hits);
        Assert.True(hit.Value.Length < len);
        Assert.EndsWith("…", hit.Value);

        // ⇒ 真填若误用 Resolve 的 FillHit.Value，正式文件里就会出现「被砍半 + 带省略号」的认证范围
        Assert.NotEqual(hit.Value, full);
    }

    [Fact]
    public void 换行不压平()
    {
        var ctx = Ctx();
        ctx.Enterprise.Address = "第一行\n第二行";

        Assert.True(new ReplaceResolver().TryEvaluate(
            "enterprise.Address", ctx, out var value, out _, out _));

        // Preview 会把 \n 压成空格；TryEvaluate 必须保留原样（单元格内换行是有意义的）
        Assert.Contains("\n", value);
    }
}

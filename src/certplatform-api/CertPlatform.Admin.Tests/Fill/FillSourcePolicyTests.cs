using CertPlatform.Shared.Fill;
using Xunit;

namespace CertPlatform.Admin.Tests.Fill;

/// <summary>
/// <see cref="FillSourcePolicy"/> 的契约测试 —— 「锚点来源是否依赖企业资料」。
///
/// <para><b>为什么这个纯函数必须有单测</b>：它是「不依赖企业文档」这条路的<b>判据源</b>，
/// 同时被<b>配置期警告</b>与<b>运行期跳过</b>消费。判错的两个方向都是静默失效：</para>
/// <list type="bullet">
///   <item>判<b>漏</b>（该报的不报）⇒ 配置期没提示、运行期照跑 ⇒ 值一直是空的，没人知道为什么；</item>
///   <item>判<b>多</b>（不该报的报）⇒ 每次都弹一堆警告 ⇒ 用户学会无视警告 ⇒ 真问题也被忽略。</item>
/// </list>
///
/// <para>⚠️ 本测试<b>不起 DB、不碰 IO</b> —— 零依赖纯函数，这是把它从两端抽出来的唯一理由。</para>
/// </summary>
public class FillSourcePolicyTests
{
    // ════════════════════════════════════════════════════════════════════
    //  ✅ 依赖企业资料 —— 取值证据来自「企业交上来的东西」
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void profile来源_依赖企业资料()
    {
        // 直接读企业画像（cert_enterprise_doc_profile）
        Assert.True(FillSourcePolicy.DependsOnEnterpriseMaterial("profile", null));
        Assert.True(FillSourcePolicy.DependsOnEnterpriseMaterial("profile", "some_field"));
    }

    [Theory]
    [InlineData("enterprise.Name")]
    [InlineData("enterprise.CreditCode")]
    [InlineData("ENTERPRISE.Name")]      // 命名空间大小写不敏感
    [InlineData("  enterprise.Name  ")]  // 前导空白（人工录入常见）
    public void replace来源_读企业档案_依赖企业资料(string @ref)
    {
        Assert.True(FillSourcePolicy.DependsOnEnterpriseMaterial("replace", @ref));
    }

    [Theory]
    [InlineData("ai_field")]
    [InlineData("ai_table")]
    [InlineData("ai")]        // 旧值：形态未定 ⇒ 保守按依赖处理
    [InlineData("AI_FIELD")]  // 大小写
    [InlineData(" ai_table ")] // 空白
    public void ai字段与表格来源_依赖企业资料(string kind)
    {
        // `SrcAiFieldSkill` 自述「内容不确定（企业有什么）」，且 `enterprise_docs`
        // 是其唯一证据（AllowEmpty = Deny）⇒ 机制上必须有企业文档。
        Assert.True(FillSourcePolicy.DependsOnEnterpriseMaterial(kind, null));
    }

    // ════════════════════════════════════════════════════════════════════
    //  ❌ 不依赖企业资料 —— 证据来自标准原文 / 平台侧
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void ai语义改写来源_不依赖企业资料()
    {
        // `SrcSemanticSkill` 必填参 = source_text（待改写的**标准原文**）
        // ⇒ 这正是「不依赖企业文档」这条路的**推荐来源**，⛔ 绝不能报错。
        Assert.False(FillSourcePolicy.DependsOnEnterpriseMaterial("ai_semantic", null));
        Assert.False(FillSourcePolicy.DependsOnEnterpriseMaterial("AI_SEMANTIC", null));
    }

    [Theory]
    [InlineData("org.Name")]
    [InlineData("org.Code")]
    [InlineData("system.date")]
    [InlineData("system.year")]
    public void replace来源_读机构与系统_不依赖企业资料(string @ref)
    {
        Assert.False(FillSourcePolicy.DependsOnEnterpriseMaterial("replace", @ref));
    }

    [Theory]
    [InlineData("global")]
    [InlineData("manual")]
    [InlineData("headerFooter")]
    [InlineData("headerfooter")]
    public void 参数与人工与文档信息_不依赖企业资料(string kind)
    {
        Assert.False(FillSourcePolicy.DependsOnEnterpriseMaterial(kind, null));
    }

    // ════════════════════════════════════════════════════════════════════
    //  边界
    // ════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void kind为空_不依赖(string? kind)
    {
        Assert.False(FillSourcePolicy.DependsOnEnterpriseMaterial(kind, "enterprise.Name"));
    }

    [Fact]
    public void replace但ref为空_不依赖()
    {
        // 没有 ref 就不知道读哪个命名空间 ⇒ 保守不报（避免把「还没配完」当成「配错」）
        Assert.False(FillSourcePolicy.DependsOnEnterpriseMaterial("replace", null));
        Assert.False(FillSourcePolicy.DependsOnEnterpriseMaterial("replace", ""));
    }

    [Theory]
    [InlineData("self")]
    [InlineData("sibling")]
    [InlineData("compute")]
    [InlineData("未来的新来源")]
    public void 未知kind_不依赖(string kind)
    {
        // ★ 保守取「不报」—— 否则将来新增一个来源类型，会一夜之间对所有文档弹警告。
        Assert.False(FillSourcePolicy.DependsOnEnterpriseMaterial(kind, "x"));
    }

    [Fact]
    public void 判据对大小写与空白不敏感()
    {
        // 同一件事的四种写法必须同判 —— 否则「库里存的是哪种写法」会决定行为
        foreach (var variant in new[] { "profile", "PROFILE", "Profile", " profile " })
            Assert.True(FillSourcePolicy.DependsOnEnterpriseMaterial(variant, null));
    }

    // ════════════════════════════════════════════════════════════════════
    //  话术（用户可读性 —— 本项目最强调的一条）
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void 运行期归因_说清两个修复动作且不指错方向()
    {
        var msg = FillSourcePolicy.DescribeMismatch("ai_field", null);

        Assert.Contains("不依赖企业文档", msg);
        Assert.Contains("ai_field", msg);
        // ★ 必须给出**两个**可执行动作（改文档属性 / 改锚点来源）
        Assert.Contains("①", msg);
        Assert.Contains("②", msg);
        // ⛔ 绝不能出现「来源链未命中」这种把人引向企业端的措辞
        Assert.DoesNotContain("来源链未命中", msg);
    }

    [Fact]
    public void 配置期警告_带上锚点与来源_便于定位()
    {
        var msg = FillSourcePolicy.DescribeConfigWarning("{{company_name}}", "global", "company_name");

        Assert.Contains("{{company_name}}", msg);
        Assert.Contains("global", msg);
        Assert.Contains("company_name", msg);
        Assert.Contains("不依赖企业文档", msg);
    }

    [Fact]
    public void 配置期警告_锚点为空时有占位而非空白()
    {
        var msg = FillSourcePolicy.DescribeConfigWarning(null, "ai_field", null);

        Assert.Contains("(未命名锚点)", msg);
        Assert.Contains("ai_field", msg);
    }
}

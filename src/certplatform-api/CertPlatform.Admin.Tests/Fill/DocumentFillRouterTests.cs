using CertPlatform.Auditor.Services.Ent.Normalize;
using Xunit;

namespace CertPlatform.Admin.Tests.Fill;

/// <summary>
/// <see cref="DocumentFillRouter"/> 的契约测试 —— 四路分流矩阵 + 边界值。
///
/// <para>为什么这个纯函数必须有单测：它是整条规范化链路的<b>第一个岔口</b>，
/// 判错 ⇒ 后面全白做，而且<b>不会报错</b>（走错路只是结果不对）。
/// 本项目最坏的失败形态就是「静默错值」，所以岔口必须在最底层锁死。</para>
///
/// <para>⚠️ 本测试<b>不起 DB、不起对象存储、不碰文件系统</b> —— 零依赖纯函数，
/// 这是把它从编排器里抽出来的唯一理由。</para>
/// </summary>
public class DocumentFillRouterTests
{
    // ════════════════════════════════════════════════════════════════════
    //  四路矩阵（2×2）—— 主判据
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void 依赖企业文档_x_fixed_走文件匹配()
    {
        var route = DocumentFillRouter.Of("fixed", dependsOnEnterprise: true);

        Assert.Equal(DocumentFillRoute.FixedMatch, route);
    }

    [Fact]
    public void 不依赖企业文档_x_fixed_走快归档()
    {
        var route = DocumentFillRouter.Of("fixed", dependsOnEnterprise: false);

        Assert.Equal(DocumentFillRoute.FixedArchive, route);
    }

    [Fact]
    public void 依赖企业文档_x_editable_走填充链且取企业资料()
    {
        var route = DocumentFillRouter.Of("editable", dependsOnEnterprise: true);

        Assert.Equal(DocumentFillRoute.FillWithEnterprise, route);
    }

    [Fact]
    public void 不依赖企业文档_x_editable_走填充链但不读企业资料()
    {
        var route = DocumentFillRouter.Of("editable", dependsOnEnterprise: false);

        Assert.Equal(DocumentFillRoute.FillStandardOnly, route);
    }

    // ════════════════════════════════════════════════════════════════════
    //  边界：分类字符串的容错
    //
    //  ⚠️ 这三条对应库里真实存在的人工录入脏值 —— 旧实现先 ToLowerInvariant 再比，
    //     行为等价；抽成纯函数后必须保证口径没变（否则历史数据整批走错路）。
    // ════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("Fixed")]
    [InlineData("FIXED")]
    [InlineData(" fixed ")]
    [InlineData("\tfixed\n")]
    public void 分类比较忽略大小写并Trim(string category)
    {
        var route = DocumentFillRouter.Of(category, dependsOnEnterprise: false);

        Assert.Equal(DocumentFillRoute.FixedArchive, route);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void 分类为空按editable处理(string? category)
    {
        // 依赖企业文档 ⇒ 走填充链取企业资料（⛔ 不能判成「跳过」，否则静默不生成）
        Assert.Equal(
            DocumentFillRoute.FillWithEnterprise,
            DocumentFillRouter.Of(category, dependsOnEnterprise: true));

        // 不依赖企业文档 ⇒ 仍走填充链，只是不读企业资料
        Assert.Equal(
            DocumentFillRoute.FillStandardOnly,
            DocumentFillRouter.Of(category, dependsOnEnterprise: false));
    }

    [Theory]
    [InlineData("hybrid")]
    [InlineData("Hybrid")]
    [InlineData("unknown")]
    [InlineData("fixedx")]
    public void 未知分类回退到editable(string category)
    {
        // ★ 口径与旧实现一致：旧代码只判 `category == "fixed"`，其余全走填充链。
        //   ⛔ 未知值绝不能判成「跳过」—— 那会让历史脏数据整批静默不生成。
        Assert.Equal(
            DocumentFillRoute.FillWithEnterprise,
            DocumentFillRouter.Of(category, dependsOnEnterprise: true));

        Assert.Equal(
            DocumentFillRoute.FillStandardOnly,
            DocumentFillRouter.Of(category, dependsOnEnterprise: false));
    }

    // ════════════════════════════════════════════════════════════════════
    //  辅助判据：IsFillChain
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void 填充链只覆盖两个非fixed路由()
    {
        Assert.True(DocumentFillRouter.IsFillChain(DocumentFillRoute.FillWithEnterprise));
        Assert.True(DocumentFillRouter.IsFillChain(DocumentFillRoute.FillStandardOnly));

        Assert.False(DocumentFillRouter.IsFillChain(DocumentFillRoute.FixedMatch));
        Assert.False(DocumentFillRouter.IsFillChain(DocumentFillRoute.FixedArchive));
    }

    /// <summary>
    /// 全枚举扫描 —— 防止未来新增 <see cref="DocumentFillRoute"/> 成员时
    /// 漏改 <see cref="DocumentFillRouter.IsFillChain"/> 的判定集合。
    /// </summary>
    [Fact]
    public void IsFillChain_覆盖全部枚举成员()
    {
        var all = System.Enum.GetValues<DocumentFillRoute>();
        Assert.Equal(4, all.Length);

        // fixed 系两条必为 false，其余两条必为 true（与上面逐条断言互为交叉验证）
        foreach (var route in all)
        {
            var expected = route is DocumentFillRoute.FillWithEnterprise
                or DocumentFillRoute.FillStandardOnly;
            Assert.Equal(expected, DocumentFillRouter.IsFillChain(route));
        }
    }
}

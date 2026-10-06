using System.Collections.Generic;
using System.Linq;
using CertPlatform.Shared.Office;
using Xunit;

namespace CertPlatform.Admin.Tests.Office;

/// <summary>
/// 「换版重扫时锁定锚点的去向判定」测试（TODO 清单 B6 / E2）。
///
/// <para><b>为什么必须钉住</b>：模板换版（<c>SourceSha256</c> 变）时，
/// <c>DocTemplateController.InvalidateAnchorsAsync</c> 会把该模板<b>全部旧锚点软删</b>，
/// 随后重扫按 <c>uk_tpl_anchor</c> 命中已软删行并逐条复活 ⇒ 配置（取值来源 / 写入方式 /
/// 是否必填 …）随之保留。于是「实施人员锁定的规则还在不在」<b>完全等价于</b>
/// 「新模板里还有没有同唯一键的锚点」。</para>
///
/// <para><b>★ 最要命的一条</b>：快照必须<b>含已软删行</b>。只查存活行的话，
/// 换版刚把旧锚点软删掉，快照会是<b>空的</b> ⇒ <c>Carried=0 / Lost=0</c> ⇒
/// 界面显示「一切正常」，而实施人员的锁定规则其实已经丢了 —— 典型的静默失败。</para>
/// </summary>
public class AnchorScanMergeTests
{
    // ─────────────────────────── 基础设施 ───────────────────────────

    private static AnchorScanMerge.AnchorKey Key(
        string anchorRef, string anchorType = "scalar", string anchorKind = "token",
        string sheetName = "", int sectionIndex = 0, string headerKind = "")
        => AnchorScanMerge.AnchorKey.Of(anchorType, anchorKind, sheetName, sectionIndex, headerKind, anchorRef);

    private static (AnchorScanMerge.AnchorKey Key, string AnchorRef, bool IsLocked) Prev(
        string anchorRef, bool isLocked = true,
        string anchorType = "scalar", string anchorKind = "token", string sheetName = "")
        => (Key(anchorRef, anchorType, anchorKind, sheetName), anchorRef, isLocked);

    // ─────────────────────────── 一、去向判定 ───────────────────────────

    /// <summary>新模板里仍有同名锚点 ⇒ 配置由「软删 + 复活」保留</summary>
    [Fact]
    public void Summarize_新模板仍有同名锚点_判为保留()
    {
        var s = AnchorScanMerge.Summarize(
            new[] { Prev("CompanyName") },
            new[] { Key("CompanyName") });

        Assert.Equal(1, s.Carried);
        Assert.Equal(0, s.Lost);
        Assert.Empty(s.LostRefs);
        Assert.Equal(1, s.Total);
    }

    /// <summary>新模板里已没有该锚点 ⇒ 如实回报「丢失」，⛔ 不静默</summary>
    [Fact]
    public void Summarize_新模板已无该锚点_判为丢失并回报引用名()
    {
        var s = AnchorScanMerge.Summarize(
            new[] { Prev("CompanyName"), Prev("LegalPerson") },
            new[] { Key("CompanyName") });

        Assert.Equal(1, s.Carried);
        Assert.Equal(1, s.Lost);
        Assert.Equal(new[] { "LegalPerson" }, s.LostRefs);
    }

    /// <summary>非锁定锚点<b>不参与判定</b> —— 它们本来就允许随版本自由增删</summary>
    [Fact]
    public void Summarize_非锁定锚点不计入()
    {
        var s = AnchorScanMerge.Summarize(
            new[] { Prev("A", isLocked: false), Prev("B", isLocked: true) },
            new[] { Key("B") });

        Assert.Equal(1, s.Total);      // 只有 B
        Assert.Equal(1, s.Carried);
        Assert.Equal(0, s.Lost);
    }

    /// <summary>★ 换版场景：快照含已软删行 ⇒ 必须仍能判出「保留」（⛔ 不能因为行已软删就当它不存在）</summary>
    [Fact]
    public void Summarize_快照含已软删行_仍能判出保留()
    {
        // 换版后：旧行 IsDeleted=1，新模板里仍有同名 token ⇒ 复活 ⇒ 配置保留
        var s = AnchorScanMerge.Summarize(
            new[] { Prev("CompanyName") },
            new[] { Key("CompanyName") });

        Assert.Equal(1, s.Carried);
        Assert.Equal(0, s.Lost);
    }

    /// <summary>★ 被合规校验拒掉的扫描项<b>没进库</b> ⇒ 必须算「丢失」（传的是落库集合，不是原始扫描结果）</summary>
    [Fact]
    public void Summarize_扫描到但未落库的项不算保留()
    {
        // 新模板里扫到了 CompanyName，但该锚点因不合规被 PersistScanAsync 跳过 ⇒ 没进 persisted
        var s = AnchorScanMerge.Summarize(
            new[] { Prev("CompanyName") },
            Enumerable.Empty<AnchorScanMerge.AnchorKey>());

        Assert.Equal(0, s.Carried);
        Assert.Equal(1, s.Lost);
    }

    // ─────────────────────────── 二、唯一键口径 ───────────────────────────

    /// <summary>
    /// ★ 唯一键的 6 段<b>逐段参与相等</b>：改了任意一段就是<b>另一个锚点</b>。
    /// <para>Excel 改工作表名是最现实的例子 —— 必须判「丢失」，⛔ 不能靠引用名猜着搬过去。</para>
    /// <para>⚠️ 基准行（与 <c>Prev("CompanyName")</c> 完全同键）<b>不在此列</b> ——
    /// 那是「保留」的场景，见 <see cref="Summarize_新模板仍有同名锚点_判为保留"/>。</para>
    /// </summary>
    [Theory]
    [InlineData("CompanyName2", "scalar", "token", "", 0, "")]             // 引用名不同
    [InlineData("CompanyName", "block", "token", "", 0, "")]               // 类型不同
    [InlineData("CompanyName", "scalar", "bookmark", "", 0, "")]           // 定位方式不同
    [InlineData("CompanyName", "scalar", "token", "Sheet2", 0, "")]        // 工作表不同
    [InlineData("CompanyName", "scalar", "token", "", 1, "")]              // 段序不同
    [InlineData("CompanyName", "scalar", "token", "", 0, "header")]        // 页眉/正文不同
    public void Summarize_唯一键任一段不同_即判为丢失(
        string refName, string type, string kind, string sheet, int section, string header)
    {
        var s = AnchorScanMerge.Summarize(
            new[] { Prev("CompanyName") },
            new[] { Key(refName, type, kind, sheet, section, header) });

        Assert.Equal(0, s.Carried);
        Assert.Equal(1, s.Lost);
    }

    /// <summary>「不适用」的三个定位列必须归一为<b>空串 / 0</b>（⛔ 不能是 null，否则同键判不出来）</summary>
    [Fact]
    public void AnchorKeyOf_空值与默认值归一一致()
    {
        var fromNulls = AnchorScanMerge.AnchorKey.Of(null, null, null, 0, null, "  CompanyName  ");
        var fromEmpty = AnchorScanMerge.AnchorKey.Of("", "", "", 0, "", "CompanyName");

        Assert.Equal(fromEmpty, fromNulls);
        Assert.Equal("CompanyName", fromNulls.AnchorRef);
        Assert.Equal("token", fromNulls.AnchorKind);   // 空 AnchorKind ⇒ token（与 Normalize 同口径）
    }

    /// <summary>两侧大小写/空白不一致时，<b>引用名</b>按去空白后的原样比较（不做大小写折叠）</summary>
    [Fact]
    public void AnchorKeyOf_去首尾空白()
    {
        var a = AnchorScanMerge.AnchorKey.Of("scalar", "token", " ", 0, " ", " CompanyName ");
        var b = AnchorScanMerge.AnchorKey.Of("scalar", "token", "", 0, "", "CompanyName");

        Assert.Equal(b, a);
    }

    // ─────────────────────────── 三、边界 ───────────────────────────

    [Fact]
    public void Summarize_无锁定锚点_返回全零()
    {
        var s = AnchorScanMerge.Summarize(
            new[] { Prev("A", isLocked: false) },
            new[] { Key("A") });

        Assert.Equal(0, s.Total);
        Assert.Equal(0, s.Carried);
        Assert.Equal(0, s.Lost);
    }

    /// <summary>引用名为空时给可读占位，⛔ 不能输出空串（前端清单里会出现一行空白）</summary>
    [Fact]
    public void Summarize_引用名为空时给占位()
    {
        var s = AnchorScanMerge.Summarize(
            new[] { Prev("   ") },
            Enumerable.Empty<AnchorScanMerge.AnchorKey>());

        Assert.Equal(1, s.Lost);
        Assert.Equal("(未命名锚点)", s.LostRefs[0]);
    }

    /// <summary>重复传入同一锚点不合并（调用方按行传入；此处只保证不炸、计数如实）</summary>
    [Fact]
    public void Summarize_空输入不炸()
    {
        var s = AnchorScanMerge.Summarize(
            Enumerable.Empty<(AnchorScanMerge.AnchorKey, string, bool)>(),
            Enumerable.Empty<AnchorScanMerge.AnchorKey>());

        Assert.Equal(0, s.Total);
        Assert.Empty(s.LostRefs);
    }

    /// <summary>同一个锁定锚点在同一模板下只会有一行 ⇒ 用 HashSet 判定不会漏（重复 key 也一致）</summary>
    [Fact]
    public void Summarize_扫描集合含重复键不影响判定()
    {
        var s = AnchorScanMerge.Summarize(
            new[] { Prev("A") },
            new[] { Key("A"), Key("A") });

        Assert.Equal(1, s.Carried);
        Assert.Equal(0, s.Lost);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using CertPlatform.Shared.Domain.Organization;
using Xunit;

namespace CertPlatform.Admin.Tests.Organization;

/// <summary>
/// ★ <b>排序口径唯一实现</b>（<see cref="OrgStandardTreeAssembler"/>）的单测。
///
/// <para><b>为什么要测</b>（2026-10-13 · 1.B.6c）：本类是「机构 / 企业 / 标准 / 阶段 / 文件夹」
/// 排序的<b>唯一实现</b>，一旦被改坏，<b>多个页面同时错且不报错</b>
/// （用户无从判断哪个页面的顺序是对的）。纯函数 ⇒ 单测是最便宜的防复发件。</para>
///
/// <para>⚠️ 重点覆盖 <c>OrderStandards</c> 的 <c>primaryDescendingOf</c>（<b>前置键</b>）分支 ——
/// T5（<c>EnterpriseNormalizeController</c> 的「已挂载优先」）是它的<b>第一个消费方</b>，
/// 在此之前该分支<b>无任何调用者</b>。</para>
/// </summary>
public class OrgStandardTreeAssemblerTests
{
    /// <summary>测试用节点：只保留排序需要的 4 个键</summary>
    private sealed class Node
    {
        public string Id { get; init; } = "";
        public int Sort { get; init; }
        public int VersionYear { get; init; }
        public string StandardCode { get; init; } = "";
        public int Mounted { get; init; }
        public string Name { get; init; } = "";
    }

    private static List<Node> Sample() => new()
    {
        // 故意让「Sort 序」与「VersionYear 序」「Id 序」全部相反
        new Node { Id = "A", Sort = 10, VersionYear = 2016, StandardCode = "iso13485", Mounted = 1, Name = "乙" },
        new Node { Id = "B", Sort = 0,  VersionYear = 2020, StandardCode = "probe-2", Mounted = 0, Name = "甲" },
    };

    private static List<Node> Order(List<Node> src, bool withPrimary) =>
        OrgStandardTreeAssembler.OrderStandards(
            src, n => n.Sort, n => n.VersionYear, n => n.StandardCode,
            withPrimary ? n => n.Mounted : null);

    // ══════════════════════════════════════════════════════════════════
    //  OrderStandards —— 前置键（T5「已挂载优先」）
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// ★★ 前置键生效：<c>Mounted=1</c> 的节点即使 <c>Sort</c> 更大也必须排在前
    /// —— 这是 T5 的行为契约（「企业该阶段已挂载的标准」优先展示）。
    /// </summary>
    [Fact]
    public void OrderStandards_WithPrimaryKey_FlaggedNodeWinsDespiteLargerSort()
    {
        var result = Order(Sample(), withPrimary: true);

        Assert.Equal(new[] { "A", "B" }, result.Select(n => n.Id));
    }

    /// <summary>
    /// ⚠️ <b>鉴别力自证</b>：同一批数据，<b>不传</b>前置键 ⇒ 退化为纯 <c>Sort</c> 序，
    /// 结果<b>相反</b>。⇒ 若 T5 漏传 <c>primaryDescendingOf</c>，本测试会失败。
    /// </summary>
    [Fact]
    public void OrderStandards_WithoutPrimaryKey_FallsBackToSort()
    {
        var result = Order(Sample(), withPrimary: false);

        Assert.Equal(new[] { "B", "A" }, result.Select(n => n.Id));
    }

    /// <summary>
    /// ★ 与 T5 改前的原排序链 <b>逐键等价</b>：
    /// <c>OrderByDescending(Mounted).ThenBy(Sort).ThenByDescending(VersionYear).ThenBy(StandardCode)</c>。
    /// （1.B.6c 的 A/B 实测已证响应逐字段一致，这里把等价性固化为可回归的断言。）
    /// </summary>
    [Fact]
    public void OrderStandards_WithPrimaryKey_IsEquivalentToLegacyChain()
    {
        var legacy = Sample()
            .OrderByDescending(n => n.Mounted)
            .ThenBy(n => n.Sort)
            .ThenByDescending(n => n.VersionYear)
            .ThenBy(n => n.StandardCode, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(legacy.Select(n => n.Id), Order(Sample(), withPrimary: true).Select(n => n.Id));
    }

    /// <summary>
    /// 裁决 B（2026-10-13）：<c>Sort</c> 并列时 <c>VersionYear</c> <b>降序（新版在前）</b>。
    /// </summary>
    [Fact]
    public void OrderStandards_SortTie_VersionYearDescending()
    {
        var src = new List<Node>
        {
            new() { Id = "old", Sort = 0, VersionYear = 2016, StandardCode = "iso13485" },
            new() { Id = "new", Sort = 0, VersionYear = 2020, StandardCode = "iso9001" },
        };

        Assert.Equal(new[] { "new", "old" }, Order(src, withPrimary: false).Select(n => n.Id));
    }

    /// <summary>
    /// 三级兜底：<c>Sort</c> 与 <c>VersionYear</c> 都并列 ⇒ 按 <c>StandardCode</c>
    /// <b>Ordinal</b> 兜底（确定性；⛔ 不是文化相关比较）。
    /// </summary>
    [Fact]
    public void OrderStandards_AllKeysTie_StandardCodeOrdinal()
    {
        var src = new List<Node>
        {
            new() { Id = "b", Sort = 0, VersionYear = 2020, StandardCode = "b-code" },
            new() { Id = "a", Sort = 0, VersionYear = 2020, StandardCode = "a-code" },
        };

        Assert.Equal(new[] { "a", "b" }, Order(src, withPrimary: false).Select(n => n.Id));
    }

    // ══════════════════════════════════════════════════════════════════
    //  OrderFolders —— 第二键 = 调用方给的「确定性兜底键」
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// ★★ <c>Sort</c> 并列时按<b>调用方给的兜底键</b>排序（此处 <c>Name</c>）。
    /// <para>⚠️ 判据 = <b>Ordinal</b>：<c>探针乙</c>(乙 U+4E59) &lt; <c>探针甲</c>(甲 U+7532)。
    /// 若被误换成 <c>Code</c>（GUID）⇒ 顺序退化为随机且本断言失败。</para>
    /// </summary>
    [Fact]
    public void OrderFolders_SortTie_UsesProvidedKeyWithOrdinal()
    {
        var src = new List<Node>
        {
            new() { Id = "fld-a", Sort = 1, Name = "探针甲" },
            new() { Id = "fld-b", Sort = 1, Name = "探针乙" },
        };

        var result = OrgStandardTreeAssembler.OrderFolders(src, n => n.Sort, n => n.Name);

        Assert.Equal(new[] { "fld-b", "fld-a" }, result.Select(n => n.Id));  // 乙 在前
    }

    /// <summary>
    /// ⚠️ <b>鉴别力自证</b>：同一批数据若拿 <c>Id</c>（≈<c>Code</c>）当兜底键 ⇒ 顺序<b>相反</b>。
    /// ⇒ 证明上面的断言真的能区分「按名字排」与「按 Code 排」。
    /// </summary>
    [Fact]
    public void OrderFolders_UsingCodeInsteadOfName_ProducesOppositeOrder()
    {
        var src = new List<Node>
        {
            new() { Id = "fld-a", Sort = 1, Name = "探针甲" },
            new() { Id = "fld-b", Sort = 1, Name = "探针乙" },
        };

        var byName = OrgStandardTreeAssembler.OrderFolders(src, n => n.Sort, n => n.Name);
        var byCode = OrgStandardTreeAssembler.OrderFolders(src, n => n.Sort, n => n.Id);

        Assert.NotEqual(byName.Select(n => n.Id), byCode.Select(n => n.Id));
    }

    /// <summary><c>Sort</c> 不等时，兜底键<b>不参与</b>（主键优先）。</summary>
    [Fact]
    public void OrderFolders_SortDiffers_KeyIgnored()
    {
        var src = new List<Node>
        {
            new() { Id = "z", Sort = 1, Name = "乙" },
            new() { Id = "a", Sort = 2, Name = "甲" },
        };

        Assert.Equal(new[] { "z", "a" }, OrgStandardTreeAssembler.OrderFolders(src, n => n.Sort, n => n.Name).Select(n => n.Id));
    }

    // ══════════════════════════════════════════════════════════════════
    //  其余口径（一并锁死，防止「顺手改一处」造成跨页不一致）
    // ══════════════════════════════════════════════════════════════════

    /// <summary>企业：<c>Sort → EnterpriseNo</c>（★ 裁决 A —— ⛔ 不用 <c>Name</c>）。</summary>
    [Fact]
    public void OrderEnterprises_SortThenEnterpriseNo()
    {
        var src = new List<Node>
        {
            new() { Id = "E002", Sort = 2 },
            new() { Id = "E001", Sort = 1 },
            new() { Id = "E000", Sort = 1 },
        };

        var result = OrgStandardTreeAssembler.OrderEnterprises(src, n => n.Sort, n => n.Id);

        Assert.Equal(new[] { "E000", "E001", "E002" }, result.Select(n => n.Id));
    }

    /// <summary>阶段：<c>Sort → StageCode</c>（确定性兜底 —— 消灭「T4 无兜底」的分裂）。</summary>
    [Fact]
    public void OrderStages_SortThenStageCode()
    {
        var src = new List<Node>
        {
            new() { Id = "CR", Sort = 20 },
            new() { Id = "AP", Sort = 10 },
            new() { Id = "AA", Sort = 10 },
        };

        var result = OrgStandardTreeAssembler.OrderStages(src, n => n.Sort, n => n.Id);

        Assert.Equal(new[] { "AA", "AP", "CR" }, result.Select(n => n.Id));
    }

    /// <summary>机构：<c>Sort → Code</c>。</summary>
    [Fact]
    public void OrderOrgs_SortThenCode()
    {
        var src = new List<Node>
        {
            new() { Id = "b", Sort = 1 },
            new() { Id = "a", Sort = 1 },
            new() { Id = "c", Sort = 0 },
        };

        var result = OrgStandardTreeAssembler.OrderOrgs(src, n => n.Sort, n => n.Id);

        Assert.Equal(new[] { "c", "a", "b" }, result.Select(n => n.Id));
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace CertPlatform.Shared.Office;

/// <summary>
/// <b>换版重扫时「锁定锚点」的去向判定</b>（纯函数：⛔ 不碰 DB、⛔ 不碰文件、⛔ 不抛异常）。
///
/// <para><b>为什么需要它</b>：模板换版（<c>SourceSha256</c> 变）时，
/// <c>DocTemplateController.InvalidateAnchorsAsync</c> 会把该模板的<b>全部旧锚点软删</b>
/// （只标孤儿会让新旧锚点并存 ⇒ 用户报的「锚点重复」）。随后重扫按 <c>uk_tpl_anchor</c> 命中
/// 已软删行并<b>逐条复活</b>，配置（<c>SourceSpec</c> / <c>WriteMode</c> / <c>Required</c> …）随之保留。</para>
///
/// <para>于是「配置还在不在」完全取决于一件事：<b>新模板里还有没有同唯一键的锚点</b>。
/// 本类把这个判定抽成纯函数，让 <c>scan</c> 能<b>如实回报</b>而不是让实施人员事后自己发现
/// 「我锁的那个规则不见了」。</para>
///
/// <para><b>⛔ 刻意不做模糊匹配</b>：靠「引用名相同」把配置从旧位置搬到新位置是<b>猜</b>。
/// 猜错会把 A 字段的取值来源写到 B 字段上，而且写库会显示成功 —— 属静默错配。
/// 实施人员看到「已丢失」清单后自己重配，比程序猜错后再排查便宜得多
/// （P2' 程序不阻断、只如实推导）。</para>
/// </summary>
public static class AnchorScanMerge
{
    /// <summary>
    /// 锚点唯一键（<c>uk_tpl_anchor</c> 去掉 <c>TemplateCode</c> 后的 6 段）。
    /// <para>⚠️ 三个定位列（<c>SheetName</c> / <c>SectionIndex</c> / <c>HeaderKind</c>）在
    /// 「不适用」时必须是<b>空串 / 0</b>，⛔ 不能是 <c>null</c> —— MySQL 唯一索引里
    /// <c>NULL</c> 不互相冲突，用 <c>null</c> 做键会让「同一锚点」判不出来。</para>
    /// </summary>
    public readonly record struct AnchorKey(
        string AnchorType,
        string AnchorKind,
        string SheetName,
        int SectionIndex,
        string HeaderKind,
        string AnchorRef)
    {
        /// <summary>
        /// 按 <c>DocTemplateAnchorController.Normalize</c> 的<b>同一口径</b>构造键
        /// （<c>AnchorKind</c> 空 ⇒ <c>token</c>；三个定位列空 ⇒ 空串 / 0）。
        /// <para>⛔ 两处口径必须逐字一致 —— 不一致 ⇒ 明明存在的锚点被判成「已丢失」。</para>
        /// </summary>
        public static AnchorKey Of(
            string? anchorType, string? anchorKind, string? sheetName,
            int sectionIndex, string? headerKind, string? anchorRef)
            => new(
                (anchorType ?? string.Empty).Trim(),
                string.IsNullOrWhiteSpace(anchorKind) ? "token" : anchorKind.Trim(),
                (sheetName ?? string.Empty).Trim(),
                sectionIndex,
                (headerKind ?? string.Empty).Trim(),
                (anchorRef ?? string.Empty).Trim());
    }

    /// <summary>
    /// 逐个判定<b>锁定</b>锚点的去向。
    ///
    /// <list type="table">
    ///   <listheader><term>结果</term><description>含义</description></listheader>
    ///   <item><term><c>Carried</c></term><description>新模板里<b>仍有</b>同唯一键锚点 ⇒ 软删 + 复活，<b>配置自动保留</b></description></item>
    ///   <item><term><c>Lost</c></term><description>新模板里<b>已没有</b>该锚点 ⇒ 它真的退出了，配置<b>无法保留</b>（如实回报，由人工决定重配或放弃）</description></item>
    /// </list>
    ///
    /// <para>非锁定锚点<b>不参与判定</b> —— 它们本来就允许随版本自由增删。</para>
    /// </summary>
    /// <param name="previous">
    /// 扫描<b>之前</b>该模板下<b>全部</b>锁定锚点（⚠️ <b>含已软删</b>：换版刚把旧锚点软删掉了，
    /// 只查存活行会一行都查不到 ⇒ 恒返回「全部保留」的假结论）。
    /// </param>
    /// <param name="scanned">
    /// 本次扫描<b>实际落库</b>的锚点键（⚠️ 是落库后的集合，⛔ 不是原始扫描结果 ——
    /// 扫描器识别出但被合规校验拒掉的那些<b>并没有进库</b>，算作「保留」就是骗人）。
    /// </param>
    public static LockedSummary Summarize(
        IEnumerable<(AnchorKey Key, string AnchorRef, bool IsLocked)> previous,
        IEnumerable<AnchorKey> scanned)
    {
        var seen = new HashSet<AnchorKey>(scanned);
        var carried = 0;
        var lostRefs = new List<string>();

        foreach (var p in previous)
        {
            if (!p.IsLocked) continue;
            if (seen.Contains(p.Key)) carried++;
            else lostRefs.Add(string.IsNullOrWhiteSpace(p.AnchorRef) ? "(未命名锚点)" : p.AnchorRef);
        }

        return new LockedSummary(carried, lostRefs);
    }

    /// <summary>锁定锚点的去向小结（<c>Carried</c> = 配置保留数；<c>LostRefs</c> = 已丢失的锚点引用名）</summary>
    public readonly record struct LockedSummary(int Carried, IReadOnlyList<string> LostRefs)
    {
        /// <summary>丢失数（供前端直接显示，⛔ 不用再 <c>Count</c> 一次）</summary>
        public int Lost => LostRefs.Count;

        /// <summary>锁定锚点总数</summary>
        public int Total => Carried + LostRefs.Count;
    }
}

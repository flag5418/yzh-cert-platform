using System;
using System.Collections.Generic;
using System.Linq;

namespace CertPlatform.Shared.Domain.Organization;

/// <summary>
/// ★★ <b>「机构 → 标准 → 阶段」树的装配器 + 排序口径唯一实现</b>。
///
/// <para><b>★ 纯函数纪律</b>（守卫 <b>A-R7</b> + <b>G-4</b>）：本类<b>无状态、无 IO、无宿主语义</b>
/// （不注入 <c>IDbOrm</c> / <c>IUserContext</c> / <c>HttpContext</c>）。
/// 取数由 <c>CoreResourceQuery</c> 负责，本类只做「排序 + 父子上溯 + Children 装配」。</para>
///
/// <para><b>★ 为什么排序必须收敛到这一处</b>（2026-10-13 实测的隐蔽不一致）：
/// <list type="bullet">
///   <item><b>标准</b>：T4 用 <c>VersionYear</c> <b>升序</b>、T5 用 <b>降序</b> ⇒ 同一批标准两页顺序相反；</item>
///   <item><b>企业</b>：T3 用 <c>Sort → EnterpriseNo</c>、T4 用 <c>Name</c> ⇒ 主键不同；</item>
///   <item><b>阶段</b>：T3 有 <c>StageCode</c> 兜底、T4 没有。</item>
/// </list>
/// 「排序」和 <c>WHERE</c> 一样是<b>业务判据</b> —— 分散在多处 ⇒ 必然分裂。故在此定义一次。</para>
///
/// <para><b>★ 2026-10-13 用户裁决</b>：企业 = <c>Sort → EnterpriseNo</c>；
/// 标准 <c>VersionYear</c> = <b>降序（新版在前）</b>。</para>
/// </summary>
public static class OrgStandardTreeAssembler
{
    /// <summary>父链上溯的最大跳数 —— 防御 <c>ParentCode</c> 成环导致死循环（守卫 <b>R-C</b>）。</summary>
    private const int MaxAncestorHops = 64;

    // ─────────────────────────────── 排序（口径唯一实现） ───────────────────────────────

    /// <summary>
    /// <b>机构</b>排序：<c>Sort → Code</c>（<c>Code</c> 为确定性兜底）。
    /// </summary>
    public static List<T> OrderOrgs<T>(
        IEnumerable<T> source,
        Func<T, int> sortOf,
        Func<T, string?> codeOf)
        => source.OrderBy(sortOf).ThenBy(codeOf, StringComparer.Ordinal).ToList();

    /// <summary>
    /// <b>企业</b>排序：<c>Sort → EnterpriseNo</c>（★ 2026-10-13 裁决 —— ⛔ 不用 <c>Name</c>，
    /// 中文按字符编码序不是拼音序）。
    /// </summary>
    public static List<T> OrderEnterprises<T>(
        IEnumerable<T> source,
        Func<T, int> sortOf,
        Func<T, string?> enterpriseNoOf)
        => source.OrderBy(sortOf).ThenBy(enterpriseNoOf, StringComparer.Ordinal).ToList();

    /// <summary>
    /// <b>标准</b>排序：<c>Sort → VersionYear(降序) → StandardCode</c>。
    ///
    /// <para>三级兜底的原因（实测）：两个标准的 <c>Sort</c> 都是 <b>0</b>（并列）⇒ 只靠它排序结果不稳定，
    /// 会让「食品标准」排在「9001标准」前面。</para>
    ///
    /// <para>★ 2026-10-13 裁决：<c>VersionYear</c> 取 <b>降序（新版在前）</b>。</para>
    /// </summary>
    /// <param name="primaryDescendingOf">
    /// 可选<b>前置键</b>（降序），如 T5 的「已挂载优先」（<c>Mounted</c>: 1/0）。
    /// 传 <c>null</c> = 无前置键。⚠️ 端点若要「额外优先级」，只能走本参数，
    /// ⛔ 不要自己在端点里重写排序链（那正是口径分裂的来源）。
    /// </param>
    public static List<T> OrderStandards<T>(
        IEnumerable<T> source,
        Func<T, int> sortOf,
        Func<T, int> versionYearOf,
        Func<T, string?> standardCodeOf,
        Func<T, int>? primaryDescendingOf = null)
    {
        IOrderedEnumerable<T> ordered = primaryDescendingOf != null
            ? source.OrderByDescending(primaryDescendingOf)
            : source.OrderBy(_ => 0);

        return ordered
            .ThenBy(sortOf)
            .ThenByDescending(versionYearOf)
            .ThenBy(standardCodeOf, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// <b>阶段</b>排序：<c>Sort → StageCode</c>（<c>StageCode</c> 为确定性兜底）。
    /// </summary>
    public static List<T> OrderStages<T>(
        IEnumerable<T> source,
        Func<T, int> sortOf,
        Func<T, string?> stageCodeOf)
        => source.OrderBy(sortOf).ThenBy(stageCodeOf, StringComparer.Ordinal).ToList();

    /// <summary>
    /// <b>文件夹 / 文件</b>排序：<c>Sort → Code</c>。
    /// </summary>
    public static List<T> OrderFolders<T>(
        IEnumerable<T> source,
        Func<T, int> sortOf,
        Func<T, string?> codeOf)
        => source.OrderBy(sortOf).ThenBy(codeOf, StringComparer.Ordinal).ToList();

    // ─────────────────────────────── 装配（父子上溯 + Children） ───────────────────────────────

    /// <summary>
    /// 把<b>扁平</b>节点列表装配成树（返回根节点集合）。
    ///
    /// <para><b>判定「根」的三条规则</b>：① <see cref="OrgStageStandardNode.ParentCode"/> 为空；
    /// ② 父节点不在本批集合内；③ 父链上溯会回到自己（成环）。</para>
    ///
    /// <para>⚠️ <b>调用前请先排序</b>（<see cref="OrderStandards{T}"/> 等）—— 装配<b>保持</b>输入顺序，
    /// ⛔ 不自行排序（排序口径的唯一实现在上面）。</para>
    ///
    /// <para>⚠️ <b>成环保护</b>（守卫 <b>R-C</b>）：上溯最多 <see cref="MaxAncestorHops"/> 跳，
    /// 环上节点会被降级为根，⛔ 不会死循环、也不会 <c>StackOverflow</c>。</para>
    /// </summary>
    public static List<OrgStageStandardNode> Assemble(IEnumerable<OrgStageStandardNode> flat)
    {
        var list = flat as IList<OrgStageStandardNode> ?? flat.ToList();

        var byCode = new Dictionary<string, OrgStageStandardNode>(StringComparer.Ordinal);
        foreach (var n in list)
        {
            if (!string.IsNullOrEmpty(n.Code)) byCode[n.Code] = n;
        }

        var roots = new List<OrgStageStandardNode>();
        foreach (var n in list)
        {
            if (TryResolveParent(byCode, n, out var parent)) parent.Children.Add(n);
            else roots.Add(n);
        }
        return roots;
    }

    /// <summary>
    /// 解析父节点：父存在、⛔ 非自身、且上溯链不成环（环 ⇒ 降级为根）。
    /// </summary>
    private static bool TryResolveParent(
        Dictionary<string, OrgStageStandardNode> byCode,
        OrgStageStandardNode node,
        out OrgStageStandardNode parent)
    {
        parent = null!;

        var parentCode = node.ParentCode;
        if (string.IsNullOrEmpty(parentCode)) return false;
        if (!byCode.TryGetValue(parentCode, out var p)) return false;
        if (ReferenceEquals(p, node)) return false;

        // 成环检测：沿父链上溯，若回到 node 本身 ⇒ 视为根（防死循环 / 防树中出现环）。
        var cursor = p;
        for (var hops = 0; hops <= MaxAncestorHops && cursor != null; hops++)
        {
            if (ReferenceEquals(cursor, node)) return false;
            if (string.IsNullOrEmpty(cursor.ParentCode)) break;
            byCode.TryGetValue(cursor.ParentCode, out cursor);
        }

        parent = p;
        return true;
    }
}

using System;

namespace CertPlatform.Shared.Domain.Scope;

/// <summary>
/// ★★ <b>`OrgCode` 判据唯一入口</b> —— 回答「这一行数据是谁的」这个问题。
///
/// <para><b>规则</b>：全仓任何地方要判断「这是后台标准级数据吗」/「这是某企业的数据吗」，
/// <b>一律调本类</b>；⛔ <b>不许</b>自己写 <c>OrgCode == ""</c> / <c>IsNullOrEmpty(OrgCode)</c> /
/// <c>IsNullOrWhiteSpace(OrgCode)</c>（守卫 <b>A-R8</b> 会拦，白名单仅
/// <c>CertPlatform.Shared/Domain/Scope/**</c>）。</para>
///
/// <para><b>★ 为什么必须收敛到一处</b>（用户 2026-10-12 原话）：
/// 「<i>避免不同逻辑中，查看到的结果不一致，而且这种问题非常隐蔽，
/// 后续真正正式系统跑起来，系统非常不稳定</i>」。</para>
///
/// <para><b>★ 与 <see cref="CoreOrgCodes.StdEnv"/> 的分工</b>：
/// <see cref="CoreOrgCodes"/> 提供<b>常量</b>（供 SqlSugar 表达式直接比较，如
/// <c>x =&gt; x.OrgCode == CoreOrgCodes.StdEnv</c>）；
/// 本类提供<b>语义判据</b>（内存中拿到一个 <c>orgCode</c> 字符串时判断它的归属）。
/// 两者是同一规则的两面，⛔ 不要各自再实现一遍。</para>
///
/// <para><b>⚠️ 历史形态提醒</b>：库里现存 <c>NULL</c> 与 <c>''</c> 两种物理形态
/// （见 <see cref="IsLegacyUnassigned"/>）。迁移到 <c>yzh-std-env</c> 完成后，
/// 旧形态应清零；过渡期如需兼容，显式调 <see cref="IsBackendBuiltCompat"/>，
/// ⛔ 不要在各调用点各自兼容。</para>
/// </summary>
public static class OrgScope
{
    /// <summary>
    /// ★ <b>唯一判据</b>：该 <paramref name="orgCode"/> 是否属于「后台建立的标准级数据」。
    ///
    /// <para>判据 = <c>Ordinal</c> 精确等于 <see cref="CoreOrgCodes.StdEnv"/>。</para>
    ///
    /// <para>⛔ <b>不把 <c>NULL</c> / <c>''</c> / 空格视为后台</b> —— 那是历史缺陷形态，
    /// 不是语义。需要过渡期兼容请显式调 <see cref="IsBackendBuiltCompat"/>。</para>
    /// </summary>
    public static bool IsBackendBuilt(string? orgCode)
        => string.Equals(orgCode, CoreOrgCodes.StdEnv, StringComparison.Ordinal);

    /// <summary>
    /// 该 <paramref name="orgCode"/> 是否属于指定企业。
    /// <para>企业编码为空 ⇒ 恒 <c>false</c>（⛔ 避免「企业编码为空时匹配到空 orgCode 行」）。</para>
    /// </summary>
    public static bool IsEnterprise(string? orgCode, string? enterpriseCode)
        => !string.IsNullOrEmpty(enterpriseCode)
           && string.Equals(orgCode, enterpriseCode, StringComparison.Ordinal);

    /// <summary>
    /// 过渡期兼容判据：<c>yzh-std-env</c> <b>或</b> 历史空值形态（<c>NULL</c> / <c>''</c> / 空格）
    /// 均视为后台标准级。
    ///
    /// <para><b>⚠️ 仅供数据迁移期使用</b>：数据迁移（196 行 ⇒ <c>yzh-std-env</c>）完成后，
    /// 本方法应<b>零调用</b>，并由守卫 <b>A-R8</b> 统计其使用点逐步清除。
    /// ⛔ 不要用它作为长期判据 —— 那等于把「隐蔽不一致」保留下来。</para>
    /// </summary>
    public static bool IsBackendBuiltCompat(string? orgCode)
        => IsBackendBuilt(orgCode) || IsLegacyUnassigned(orgCode);

    /// <summary>
    /// 该 <paramref name="orgCode"/> 是否是<b>历史未归属形态</b>
    /// （<c>NULL</c> / <c>''</c> / 纯空白）—— <b>仅用于诊断、迁移与体检</b>。
    ///
    /// <para>⛔ <b>不要</b>用它当作「这是后台数据」的业务判据。</para>
    /// </summary>
    public static bool IsLegacyUnassigned(string? orgCode)
        => string.IsNullOrWhiteSpace(orgCode);

    /// <summary>
    /// 人类可读的归属描述 —— 用于错误消息 / 日志 / 待办说明。
    /// <para>例：<c>后台标准级</c> · <c>企业级(906e8b2a…)</c> · <c>未归属(历史空值)</c></para>
    /// </summary>
    public static string Describe(string? orgCode)
    {
        if (IsBackendBuilt(orgCode)) return "后台标准级";
        if (IsLegacyUnassigned(orgCode)) return "未归属(历史空值)";
        return $"企业级({orgCode})";
    }
}

namespace CertPlatform.Shared.Domain.Scope;

/// <summary>
/// ★★ <b>归属判据唯一入口</b> —— 回答「这一行数据是谁的」这个问题。
///
/// <para><b>★ 归属模型 V2（2026-10-13 用户裁定）—— 三态 + 机构冗余</b>：</para>
/// <list type="bullet">
///   <item><b>机构行</b>：<c>OrgCode</c> 有值（= 机构 Code），<c>EnterpriseCode</c> 空。</item>
///   <item><b>企业行</b>：<c>EnterpriseCode</c> 有值；<b>可同时带 <c>OrgCode</c> = 机构 Code（冗余，合法）</b>。</item>
///   <item><b>平台标准级</b>：两列都空（全平台共享，如 ISO 条款 141 行）。</item>
/// </list>
///
/// <para><b>★ 唯一分类判据 = <see cref="IsEnterpriseRow"/></b>（<c>EnterpriseCode</c> 非空 ⇒ 企业行）。
/// 用户原话：「<i>只要 EnterpriseCode 有值，就是企业端的数据</i>」。</para>
///
/// <para><b>★ 为什么企业行可以带 OrgCode</b>（用户原话）：「<i>很多时候，企业的信息里面存储了真实机构 code，
/// 其实并不能算错误，因为我们需要通过机构 code 快速获取后台的规则或标准，算是一种冗余</i>」。</para>
///
/// <para><b>⛔ 历史常量已废除</b>：旧模型用 <c>yzh-std-env</c>（<c>CoreOrgCodes.StdEnv</c>）标记「后台标准级」；
/// 该常量与 <c>CoreOrgCodes</c> 类已于 2026-10-13 <b>删除</b>，平台标准级改用「两列都空」。
/// 守卫 <b>A-R10</b> 全仓禁该字面量（同时禁旧值 <c>YZH-STD-ENT</c>）。</para>
///
/// <para><b>★ 例外</b>：<c>cert_expert_*</c> 表的 <c>OrgCode</c> = <b>专家工作区编码</b>
/// （DB 注释 ★非认证机构），属独立维度，<b>不适用</b>本类判据。</para>
///
/// <para><b>★ 为什么必须收敛到一处</b>（用户 2026-10-12 原话）：
/// 「<i>避免不同逻辑中，查看到的结果不一致，而且这种问题非常隐蔽，
/// 后续真正正式系统跑起来，系统非常不稳定</i>」。</para>
/// </summary>
public static class OrgScope
{
    /// <summary>
    /// ★★ <b>唯一分类判据</b> —— 该行是否属于某个<b>具体企业</b>。
    ///
    /// <para>判据 = <paramref name="enterpriseCode"/> 非空白。<b>空 = 非企业行</b>
    /// （机构行 / 平台标准级）。</para>
    ///
    /// <para><b>⚠️ 数据库表达式场景</b>：SqlSugar 表达式内无法调本方法，
    /// 用等价的 <c>string.IsNullOrEmpty(x.EnterpriseCode)</c>（该列定义为 <c>NOT NULL</c>）。</para>
    /// </summary>
    public static bool IsEnterpriseRow(string? enterpriseCode)
        => !string.IsNullOrWhiteSpace(enterpriseCode);

    /// <summary>
    /// <b>机构行</b>判据 —— <paramref name="orgCode"/> 有值（= 机构 Code）。
    ///
    /// <para>⚠️ 单独用本方法<b>不足以</b>判定「这是机构行」—— 企业行也可能带 <c>OrgCode</c>（冗余）。
    /// 「是不是机构行」应写 <c>!IsEnterpriseRow(ent) &amp;&amp; IsOrgRow(org)</c>，
    /// 或等价地 <c>!IsPlatformLevel(org, ent)</c>。</para>
    /// </summary>
    public static bool IsOrgRow(string? orgCode)
        => !string.IsNullOrWhiteSpace(orgCode);

    /// <summary>
    /// <b>平台标准级</b>判据 —— 既不属于企业，也不特指机构（两列都空）。
    /// <para>例：<c>cert_iso_clause</c>（ISO 条款，全平台共享）。</para>
    /// </summary>
    public static bool IsPlatformLevel(string? orgCode, string? enterpriseCode)
        => !IsEnterpriseRow(enterpriseCode) && !IsOrgRow(orgCode);

    /// <summary>
    /// 人类可读的归属描述 —— 用于错误消息 / 日志 / 待办说明。
    /// <para>例：<c>平台标准级</c> · <c>机构级(906e8b2a…)</c> · <c>企业级(906e8b2a…)</c></para>
    /// </summary>
    public static string Describe(string? orgCode, string? enterpriseCode)
    {
        if (IsEnterpriseRow(enterpriseCode)) return $"企业级({enterpriseCode})";
        if (IsOrgRow(orgCode)) return $"机构级({orgCode})";
        return "平台标准级";
    }
}

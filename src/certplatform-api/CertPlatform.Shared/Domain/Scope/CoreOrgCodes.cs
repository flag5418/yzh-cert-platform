namespace CertPlatform.Shared.Domain.Scope;

/// <summary>
/// ★ 核心机构常量 —— 「后台标准级数据」的机构归属<b>唯一常量出处</b>。
///
/// <para><b>规则（用户 2026-10-12 裁定，见 `docs/30-项目规则/跨端共享与核心资源检索规则-V1.md` §二）</b>：</para>
/// <list type="bullet">
///   <item><b>后台建立的标准级数据</b>（标准 / 章节 / 后台参数 / 后台提示词 / NC 规则 / 报告规则 /
///         提取规则 / 目录模板）⇒ <c>OrgCode == <see cref="StdEnv"/></c>。</item>
///   <item><b>企业级配置</b>（企业×标准、企业×阶段、企业目录配置、企业文档模板、企业已填参数值）
///         ⇒ <c>OrgCode == &lt;企业 Code&gt;</c>。</item>
///   <item>⛔ <b>禁止</b>用 <c>NULL</c> / <c>''</c> / <c>'   '</c> 作为「这是后台数据」的判据。</item>
/// </list>
///
/// <para><b>★ 为什么必须有这个类</b>：本项目历史上「后台数据」有 <b>3 种物理形态</b>
/// （<c>NULL</c> / <c>''</c> / 无该列）与 <b>4 种判据写法</b>（<c>== ""</c> /
/// <c>IsNullOrEmpty</c> / <c>IsNullOrWhiteSpace</c> / <c>== string.Empty || == org</c>），
/// 直接后果是<b>同一份数据、两种判据、结果不同</b>：</para>
/// <code>
/// cert_iso_clause  WHERE OrgCode = ''            ⇒   0 行
/// cert_iso_clause  WHERE OrgCode IS NULL OR = '' ⇒ 163 行   ← 差 163 行，且不报任何错
/// </code>
/// <para>⇒ 全仓字符串字面量 <c>"yzh-std-env"</c> <b>只允许</b>出现在本文件（守卫 <b>A-R9</b>）。</para>
///
/// <para><b>★ 关键事实（易错）</b>：<c>cert_*.OrgCode</c> 存的是 <c>sys_organization.Code</c>，
/// ⛔ <b>不是</b> <c>sys_organization.OrgCode</c> —— 所以 <c>yzh-std-env</c> 是
/// <b>虚拟机构的 <c>Code</c></b>。</para>
/// </summary>
public static class CoreOrgCodes
{
    /// <summary>
    /// 后台标准级数据的虚拟机构 <c>Code</c>。
    ///
    /// <para>语义：代表「后台（平台方）进行标准建立」这个特殊身份。
    /// 对应 <c>sys_organization</c> 中的虚拟机构行（<c>OrgName = '虚拟体系机构'</c>）。</para>
    ///
    /// <para>⚠️ 与它相对的是<b>真实企业</b> —— 真实企业的所有配置一律存企业自己的 <c>Code</c>，
    /// ⛔ 不用本常量。</para>
    /// </summary>
    public const string StdEnv = "yzh-std-env";
}

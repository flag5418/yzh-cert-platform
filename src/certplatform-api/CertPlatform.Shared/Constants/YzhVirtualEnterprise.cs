namespace CertPlatform.Shared.Constants;

/// <summary>
/// 虚拟企业 Code —— config/file 表「模板行」的 EnterpriseCode 标记常量。
/// <para>★ Q1 裁决（2026-09-28，06 册 03 号 §四）：<b>不在 <c>cert_enterprise</c> 建虚拟企业行</b>；
/// 机构维度从企业行 <c>OrgCode</c>（建档时由专家登录态写入）直取。本常量仅作模板行标记值。</para>
/// <para>⛔ 全仓禁止再出现 <c>"YZH-STD-ENT"</c> 字面量（种子 SQL 除外），一律引用 <see cref="Code"/>。</para>
/// </summary>
public static class YzhVirtualEnterprise
{
    public const string Code = "YZH-STD-ENT";
}

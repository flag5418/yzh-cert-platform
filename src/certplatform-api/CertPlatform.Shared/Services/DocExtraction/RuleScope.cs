namespace CertPlatform.Shared.Services.DocExtraction;

/// <summary>
/// 提取规则四元组作用域（S0，10 号 §三）：规则键 + 机构 + 标准 + 阶段。
/// <para><c>RuleKey</c> = 规则键（模板文件行 Code / FR 模板 Code）；<c>OrgCode</c> = 模板所属机构。</para>
/// <para>★ 2026-10-12 从 <c>Admin.Services.DocExtraction.DocExtractionRuleService.cs</c> 下沉到 Shared
/// —— <c>ExtractionScopeResolver</c>（Shared）要用它，而 Shared ⛔ 不能引 Admin。</para>
/// </summary>
public sealed record RuleScope(string RuleKey, string OrgCode, string StandardCode, string StageCode);

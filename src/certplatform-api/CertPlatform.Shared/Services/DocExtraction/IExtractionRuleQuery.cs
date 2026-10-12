using System.Threading.Tasks;
using CertPlatform.Shared.DocExtraction;
using CertPlatform.Shared.Entities.Dir;
using CertPlatform.Shared.Entities.Doc;

namespace CertPlatform.Shared.Services.DocExtraction;

/// <summary>
/// ★ 提取规则查询（Shared 契约）。
/// <para>实现在 <c>CertPlatform.Admin.Services.DocExtraction.DocExtractionRuleService</c>。
/// <c>ExtractionScopeResolver</c>（Shared）要按四元组解析规则，但实现类在 Admin
/// ⇒ 只能靠接口反转（三方结构铁律：<c>Shared ⛔ 引 Admin</c>）。</para>
/// </summary>
public interface IExtractionRuleQuery
{
    /// <summary>由标准目录文件行推导四元组作用域（S0）</summary>
    Task<RuleScope> ResolveRuleScopeAsync(StandardDirectoryFile file);

    /// <summary>★ 按四元组取规则的<b>唯一入口</b>（S0）：机构 + 标准 + 阶段 + 规则键</summary>
    Task<DocExtractionRule?> GetRuleByScopeAsync(
        string orgCode, string standardCode, string stageCode, string standardFileCode);

    /// <summary>★ 企业资料提取任务用：按规则 + Markdown 跑一次提取（不落库，返回结果）</summary>
    Task<(bool Ok, string? Error, ExtractionData? Data)> TestFieldWithMarkdownAsync(
        DocExtractionRule? rule, string markdown, string enterpriseCode);

    /// <summary>★ 企业资料提取任务用：把提取结果落到企业域 B-08/B-09（真实 VersionNumber，旧行归档）</summary>
    Task<(int FieldCount, int TableCount)> SaveEnterpriseExtractionResultsAsync(
        DocExtractionRule rule, ExtractionData extractionData,
        string enterpriseCode, string fileCode, int versionNumber,
        string standardCode = "", string stageCode = "");
}

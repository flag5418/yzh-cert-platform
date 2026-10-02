using CertPlatform.Shared.DocExtraction;
using CertPlatform.Shared.Constants;
using CertPlatform.Shared.Entities.Dir;
using CertPlatform.Shared.Entities.Doc;
using YZH.Core.DataBase.Interfaces;

namespace CertPlatform.Admin.Services.DocExtraction;

/// <summary>
/// 槽位提取判定结论（10 号 §六 S1：图 2 的 ①②③④ 收口为一份结构化结论）。
/// <para>端点 / 执行器 / 页面一律消费本对象，禁止各自另写一套判定。</para>
/// </summary>
public sealed class ExtractionSlot
{
    /// <summary>企业槽位行 Code（= 上传文件行自身 Code）</summary>
    public string FileCode { get; set; } = "";

    public string FileName { get; set; } = "";

    /// <summary>对齐点：该槽位对应的模板文档 Code（规则键来源）</summary>
    public string? StandardFileCode { get; set; }

    /// <summary>规则键 = StandardFileCode ?? 自身 Code</summary>
    public string RuleKey { get; set; } = "";

    /// <summary>①机构（模板/槽位所属配置的 OrgCode，已归一为认证机构 Code）</summary>
    public string OrgCode { get; set; } = "";

    /// <summary>②标准（以模板行为准，缺省退回槽位行）</summary>
    public string StandardCode { get; set; } = "";

    /// <summary>②阶段（以模板行为准，缺省退回槽位行）</summary>
    public string StageCode { get; set; } = "";

    public int VersionNumber { get; set; } = 1;

    /// <summary>槽位行当前提取状态（4 态：none / completed / failed / skipped）</summary>
    public string ExtractStatus { get; set; } = "none";

    /// <summary>Markdown 产物是否就位（<c>MarkdownPath</c> 非空）</summary>
    public bool MarkdownReady { get; set; }

    /// <summary>四元组命中的规则 Code；空 = 无可用规则</summary>
    public string RuleCode { get; set; } = "";

    /// <summary>
    /// 是否可提取 = 槽位有效 ∧ Markdown 就位 ∧ 命中<b>可用规则</b>
    /// （<c>IsValid=1 ∧ Status ∈ {configured, passed}</c>，见 <see cref="IsUsableRule"/>）。
    /// </summary>
    public bool Extractable { get; set; }

    /// <summary>不可提取原因（空 = 可提取）：<c>no_markdown</c> / <c>no_rule</c></summary>
    public string SkipReason { get; set; } = "";
}

/// <summary>
/// 提取定位链唯一判定器（10 号 §六 S1）。
///
/// <para><b>判定口径（2026-09-29 用户裁决「有规则即可提取」）</b>：四元组（机构 + 标准 + 阶段 +
/// 模板文档 Code）命中 <c>IsValid=1 ∧ Status ∈ {configured, passed}</c> 的规则，且槽位
/// <c>MarkdownPath</c> 非空。</para>
///
/// <para>⚠️ <c>passed</c> 仅为兼容历史验收脚本而保留；管理端保存只写 <c>configured</c>
/// （<c>failed</c> = 管理员停用）。原实现只认 <c>passed</c> 而全项目无人写入 ⇒ 断链：
/// 所有文档恒判「未配置」、自动提取永不真正执行。</para>
///
/// <para><b>机构来源（③，替代图 2 的 ①+③）</b>：取槽位/模板行所属 <c>ConfigCode → config.OrgCode</c>。
/// 该路径已经过 <c>EnsureDirectoryAsync</c> 的归一（<c>NormalizeOrgCodeAsync</c>）与机构漂移修正，
/// 比图 2 ① 的 <c>cert_enterprise.OrgCode</c> 档案值更准（档案值可能未归一）。</para>
///
/// <para>本类是<b>只读判定</b>，不写任何状态；执行器/批量任务拿到结论后自行落库。</para>
/// </summary>
public class ExtractionScopeResolver
{
    private readonly IDbOrm _db;
    private readonly DocExtractionRuleService _ruleService;

    public ExtractionScopeResolver(IDbOrm db, DocExtractionRuleService ruleService)
    {
        _db = db;
        _ruleService = ruleService;
    }

    /// <summary>
    /// 单槽位判定（执行器口径）：给定企业文件行，给出四元组作用域 + 命中规则 + 是否可提取。
    /// </summary>
    public async Task<ExtractionSlot> ResolveAsync(StandardDirectoryFile file)
    {
        if (file == null) return new ExtractionSlot();

        var scope = await _ruleService.ResolveRuleScopeAsync(file);
        var rule = string.IsNullOrWhiteSpace(scope.OrgCode)
            ? null
            : await _ruleService.GetRuleByScopeAsync(
                scope.OrgCode, scope.StandardCode, scope.StageCode, scope.RuleKey);

        return BuildSlot(file, scope.OrgCode, scope.StandardCode, scope.StageCode,
            scope.RuleKey, rule);
    }

    /// <summary>
    /// 批量槽位判定（plan / batch / 页面口径）：一次给定企业 + 阶段，返回该目录下每个槽位的结论。
    /// <para>批量预取模板行 / 配置 / 规则后内存关联，避免逐槽位多次往返。</para>
    /// </summary>
    /// <param name="enterpriseCode">企业 Code</param>
    /// <param name="stageCode">阶段 Code（必填：目录按 标准+阶段 建）</param>
    /// <param name="standardCode">标准 Code（可选；给了就按它过滤）</param>
    public async Task<List<ExtractionSlot>> ResolveAsync(
        string enterpriseCode, string stageCode, string? standardCode = null)
    {
        var result = new List<ExtractionSlot>();
        if (string.IsNullOrWhiteSpace(enterpriseCode) || string.IsNullOrWhiteSpace(stageCode))
            return result;

        // ② 企业槽位行（默认口径已滤 IsValid=1；转换链临时置 0 的行不参与）
        var slots = (await _db.GetListAsync<StandardDirectoryFile>(
            x => x.EnterpriseCode == enterpriseCode && x.StageCode == stageCode)).Data
            ?? new List<StandardDirectoryFile>();

        if (slots.Count == 0) return result;
        if (!string.IsNullOrWhiteSpace(standardCode))
            slots = slots.Where(x => x.StandardCode == standardCode).ToList();
        if (slots.Count == 0) return result;

        var ruleKeys = slots
            .Select(x => string.IsNullOrEmpty(x.StandardFileCode) ? x.Code ?? "" : x.StandardFileCode!)
            .Where(x => !string.IsNullOrEmpty(x))
            .Distinct()
            .ToList();

        // ③ 反查模板行 → 模板配置 → 机构（模板行属虚拟企业域）
        var tplRows = (await _db.GetListAsync<StandardDirectoryFile>(
            x => x.EnterpriseCode == YzhVirtualEnterprise.Code)).Data
            ?? new List<StandardDirectoryFile>();
        var tplMap = tplRows
            .Where(x => !string.IsNullOrEmpty(x.Code) && ruleKeys.Contains(x.Code!))
            .GroupBy(x => x.Code!)
            .ToDictionary(g => g.Key, g => g.First());

        var cfgCodes = tplMap.Values
            .Select(x => x.ConfigCode)
            .Concat(slots.Select(x => x.ConfigCode))
            .Where(x => !string.IsNullOrEmpty(x))
            .Distinct()
            .ToList();
        var cfgMap = (await _db.GetListAsync<StandardDirectoryConfig>()).Data?
                .Where(x => cfgCodes.Contains(x.Code))
                .GroupBy(x => x.Code)
                .ToDictionary(g => g.Key, g => g.First())
            ?? new Dictionary<string, StandardDirectoryConfig>();

        // ④ 规则全量（表很小；GetListAsync 默认已滤 IsValid=1）
        var rules = (await _db.GetListAsync<DocExtractionRule>()).Data ?? new List<DocExtractionRule>();

        foreach (var file in slots)
        {
            var ruleKey = string.IsNullOrEmpty(file.StandardFileCode) ? file.Code ?? "" : file.StandardFileCode!;
            tplMap.TryGetValue(ruleKey, out var tpl);

            // 标准 / 阶段以模板行为准，缺省退回槽位行
            var std = !string.IsNullOrEmpty(tpl?.StandardCode) ? tpl!.StandardCode
                : (file.StandardCode ?? "");
            var stg = !string.IsNullOrEmpty(tpl?.StageCode) ? tpl!.StageCode
                : (file.StageCode ?? "");

            // 机构 = 模板行所属配置（缺省退回槽位行所属配置）
            var cfgCode = !string.IsNullOrEmpty(tpl?.ConfigCode) ? tpl!.ConfigCode : file.ConfigCode;
            cfgMap.TryGetValue(cfgCode, out var cfg);
            var org = cfg?.OrgCode ?? "";

            var rule = string.IsNullOrEmpty(org) ? null
                : rules.FirstOrDefault(r =>
                    r.OrgCode == org && r.StandardCode == std &&
                    r.StageCode == stg && r.StandardFileCode == ruleKey);

            result.Add(BuildSlot(file, org, std, stg, ruleKey, rule));
        }

        return result;
    }

    private static ExtractionSlot BuildSlot(
        StandardDirectoryFile file, string orgCode, string standardCode,
        string stageCode, string ruleKey, DocExtractionRule? rule)
    {
        var markdownReady = !string.IsNullOrWhiteSpace(file.MarkdownPath);
        // 可用规则 = IsValid=1（GetListAsync 已滤）∧ Status ∈ {configured, passed}
        var ruleCode = rule != null && IsUsableRule(rule) ? rule.Code ?? "" : "";
        var extractable = markdownReady && !string.IsNullOrEmpty(ruleCode);

        return new ExtractionSlot
        {
            FileCode = file.Code ?? "",
            FileName = file.FileName ?? "",
            StandardFileCode = file.StandardFileCode,
            RuleKey = ruleKey,
            OrgCode = orgCode,
            StandardCode = standardCode,
            StageCode = stageCode,
            VersionNumber = file.VersionNumber,
            ExtractStatus = EnterpriseExtractStatus.ForDisplay(file.ExtractStatus),
            MarkdownReady = markdownReady,
            RuleCode = ruleCode,
            Extractable = extractable,
            SkipReason = extractable ? ""
                : !markdownReady ? "no_markdown"
                : "no_rule"
        };
    }

    /// <summary>
    /// 规则是否可用（企业侧提取的唯一口径，端点/执行器/页面共用）。
    /// <para><c>configured</c> = 管理端保存且启用；<c>passed</c> = 历史/脚本口径（兼容保留）；
    /// <c>failed</c> = 已停用、<c>none</c> = 未配置 —— 均不可用。</para>
    /// </summary>
    public static bool IsUsableRule(DocExtractionRule rule)
        => rule.Status == "configured" || rule.Status == "passed";
}

using System;
using System.Collections.Generic;

namespace CertPlatform.Auditor.Services.Expert;

/// <summary>
/// <b>状态词表映射（全局唯一出口）</b> —— 设计文档 V3 §5.6。
///
/// <para><b>为什么必须集中一处</b>：本系统同时存在四套词表，各自值域不同、语义也不同：</para>
/// <list type="table">
///   <item><term><c>PrepState</c></term><description>found / partial / missing / unknown / na（资料够不够）</description></item>
///   <item><term><c>Conclusion</c></term><description>pass / ofi / nc / na（符不符合，落库口径）</description></item>
///   <item><term>LLM 原始输出</term><description>conformant / minor_nc / major_nc / observation（<c>audit_check</c> 的值域）</description></item>
///   <item><term><c>DimensionConformity</c></term><description>conform / nonconform / na / unverifiable（维度级，喂 NC 计算器）</description></item>
/// </list>
///
/// <para>⛔ <b>禁止各处自己 switch</b>：一旦两处映射不一致，同一个 LLM 输出会在
/// 「结果行」和「汇总」里变成两个结论，而且没人能一眼看出哪边错了。</para>
///
/// <para><b>★ 落库前必须过这里</b>：LLM 的 <c>major_nc</c> 不是合法的 <c>Conclusion</c> 值，
/// 直接写进 <c>cert_check_result.Conclusion</c> 会让「按结论筛选」全部失效。</para>
/// </summary>
public static class CertVerdictMapper
{
    // ── LLM 原始结论（audit_check 值域）──

    public const string RawConformant = "conformant";
    public const string RawObservation = "observation";
    public const string RawMinorNc = "minor_nc";
    public const string RawMajorNc = "major_nc";

    // ── 落库结论（cert_check_result.Conclusion 值域）──

    public const string ConclusionPass = "pass";
    public const string ConclusionOfi = "ofi";
    public const string ConclusionNc = "nc";
    public const string ConclusionNa = "na";

    /// <summary>
    /// LLM 原始结论 → 落库四态。
    /// <para>无法识别 ⇒ <c>null</c>（⛔ 不要硬塞一个值：宁可留空让人看见，也不要假装判过了）。</para>
    /// </summary>
    public static string? ToConclusion(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return raw.Trim().ToLowerInvariant() switch
        {
            RawConformant or "conform" or "pass" or "符合" => ConclusionPass,
            RawObservation or "ofi" or "观察项" => ConclusionOfi,
            RawMinorNc or RawMajorNc or "nc" or "nonconform" or "不符合" => ConclusionNc,
            "na" or "n/a" or "不适用" => ConclusionNa,
            _ => null
        };
    }

    /// <summary>
    /// 严重度：LLM 给了就用 LLM 的（归一化），没给就<b>从结论反推</b>。
    /// <para>反推的理由：<c>major_nc</c> 这个词本身已经说明了严重度，
    /// 再让模型重复输出一次 <c>severity_suggestion</c> 只是多一个不一致的机会。</para>
    /// </summary>
    public static string? ToSeverity(string? rawConclusion, string? severitySuggestion)
    {
        var s = NormalizeSeverity(severitySuggestion);
        if (s != null) return s;

        return (rawConclusion ?? "").Trim().ToLowerInvariant() switch
        {
            RawMajorNc => "major",
            RawMinorNc => "minor",
            RawObservation => "observation",
            _ => null
        };
    }

    /// <summary>
    /// 归一化严重度到五级（<c>critical / major / minor / observation / none</c>）。
    /// <para><c>critical</c> 保留原值 —— 合并进 major 计数的逻辑在结论计算器里，
    /// 映射层不替它做决定（否则「按最严处理」的痕迹就丢了）。</para>
    /// </summary>
    public static string? NormalizeSeverity(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return raw.Trim().ToLowerInvariant() switch
        {
            "critical" or "严重" => "critical",
            "major" or "严重不符合" => "major",
            "minor" or "一般" or "一般不符合" => "minor",
            "observation" or "obs" or "观察" or "观察项" => "observation",
            "none" or "无" => "none",
            _ => null
        };
    }

    /// <summary>
    /// 是否「建议开 NC」。
    /// <para>★ <b>不是「已开 NC」</b>：这只是系统建议，<c>cert_nc</c> 由审核员逐条签发。</para>
    /// <para>判据以<b>结论</b>为准，而不是「模型有没有填 <c>nc_suggestion</c>」——
    /// 实测旧 <c>audit_check</c> 模板的 JSON 契约里根本没有这个字段，
    /// 靠字段判会让「判了 major_nc 却 NcSuggested=false」的脏数据流出去。</para>
    /// </summary>
    public static bool SuggestsNc(string? rawConclusion, string? ncSuggestion)
    {
        var conclusion = (rawConclusion ?? "").Trim().ToLowerInvariant();
        if (conclusion is RawMinorNc or RawMajorNc) return true;

        // 结论没给或给了别的值，但模型明确写了不符合描述 ⇒ 也算建议
        return !string.IsNullOrWhiteSpace(ncSuggestion);
    }

    /// <summary>结论 → 中文（界面展示用；⛔ 不要拿它做判断）</summary>
    public static string ConclusionLabel(string? conclusion) => conclusion switch
    {
        ConclusionPass => "符合",
        ConclusionOfi => "观察项",
        ConclusionNc => "不符合",
        ConclusionNa => "不适用",
        _ => "未判定"
    };

    /// <summary>准备态归一（<c>found/partial/missing/unknown/na</c>）—— 模型违规输出检查分支值域时降级为 <c>unknown</c></summary>
    public static string? NormalizePrepState(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return raw.Trim().ToLowerInvariant() switch
        {
            "found" or "complete" or "ok" => "found",
            "partial" => "partial",
            "missing" => "missing",
            "na" or "n/a" => "na",
            _ => "unknown"
        };
    }

    /// <summary>本类认可的全部落库结论值（供校验 / 前端下拉复用）</summary>
    public static IReadOnlyList<string> AllConclusions { get; } =
        new[] { ConclusionPass, ConclusionOfi, ConclusionNc, ConclusionNa };
}

namespace CertPlatform.Shared.Entities.Doc;

/// <summary>
/// 提取值的来源（<c>cert_extraction_result.ValueSource</c> / <c>cert_table_extraction_result.ValueSource</c>）
/// </summary>
/// <para>★ 2026-09-30 用户裁决 J2 落库。补录直接写提取结果表，用本值区分
/// 「系统自动提取」与「人工录入」，缺失记录走 INSERT。</para>
/// <para>⛔ <b>不建独立的补录表</b>（05 号 D06）：值改了所有任务都受益。</para>
/// <para>⚠️ 本常量同时供 <c>ExtractionDataResolver</c>（取数时人工优先）
/// 与 <c>GapDetector</c>（判空）使用，⛔ 禁止各处写字面量。</para>
/// </remarks>
public static class ExtractionValueSource
{
    /// <summary>系统自动提取（内容提取规则 / 文档解析产出）</summary>
    public const string Auto = "auto";

    /// <summary>人工录入（专家在补录清单里填写）</summary>
    public const string Manual = "manual";

    /// <summary>
    /// 下期预留：Skill 自动生成（用户已认可方向，但"AI 生成值能否直接采信驱动 NC 结论"
    /// 是业务裁决，未出前不写代码 —— 见 25 号 §7.2）
    /// </summary>
    public const string AiGenerated = "ai_generated";

    /// <summary>是否人工来源（人工录入 or AI 生成）</summary>
    public static bool IsManual(string? valueSource)
        => string.Equals(valueSource, Manual, System.StringComparison.OrdinalIgnoreCase)
        || string.Equals(valueSource, AiGenerated, System.StringComparison.OrdinalIgnoreCase);

    /// <summary>排序用：人工来源排在前（取数时优先）</summary>
    public static int Priority(string? valueSource) => IsManual(valueSource) ? 1 : 0;
}

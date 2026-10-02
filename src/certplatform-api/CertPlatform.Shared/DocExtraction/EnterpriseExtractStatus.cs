namespace CertPlatform.Shared.DocExtraction;

/// <summary>
/// 提取状态收敛器。
///
/// <para>★ 2026-09-30 用户裁决：<c>failed</c> 独立成态（原 3 态 → 4 态）。
/// 用户原话：「提取失败，是有提取规则，但执行的时候，失败了，可能是文档内容和标准目录结构完全不同，
/// 或者该文档不能解析等」⇒ <b>有规则 + 执行失败 = failed</b>，与「无规则（skipped）」是两个正交概念，
/// 不能像旧实现那样把 failed 抹成 none（页面会把"跑了但失败"显示成"未提取"，用户以为没跑）。</para>
///
/// <para>合法取值 4 个：<c>none</c>（未提取/排队中）/ <c>completed</c>（已提取）/
/// <c>skipped</c>（无可用规则，不算失败）/ <c>failed</c>（有规则但执行失败）。
/// 历史行里的 <c>pending</c> / <c>processing</c> / <c>convertFailed</c> 以及空值/未知值一律读作 <c>none</c>
/// —— 「为什么没提取」由 <c>ExtractMessage</c> 承载（tooltip）。</para>
///
/// <para>⛔ 不新增 <c>ExtractSkipReason</c> 列；4 态够用。</para>
/// </summary>
public static class EnterpriseExtractStatus
{
    public const string None = "none";
    public const string Completed = "completed";
    public const string Skipped = "skipped";
    /// <summary>★ 有规则但执行失败（文档与规则不匹配 / 文档无法解析 / LLM 返回空 / AI 调用异常）</summary>
    public const string Failed = "failed";

    /// <summary>写入侧一律经此收口（执行器 / 触发 / 替换 / 恢复 / 未自动提取）</summary>
    public static string Normalize(string? status)
    {
        var s = status?.Trim();
        if (string.IsNullOrEmpty(s)) return None;
        return s.ToLowerInvariant() switch
        {
            None or Completed or Skipped or Failed => s.ToLowerInvariant(),
            _ => None
        };
    }

    /// <summary>读取侧（列表 DTO / resolver / 结果接口）把历史脏值映射回 4 态</summary>
    public static string ForDisplay(string? status)
    {
        var s = status?.Trim().ToLowerInvariant();
        return s is Completed or Skipped or Failed ? s : None;
    }
}

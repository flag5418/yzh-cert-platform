namespace CertPlatform.Shared.Fill;

/// <summary>
/// <b>锚点形态判定</b>（37 号 §6.1 的 D3 承载形态）—— Word / Excel 扫描器<b>共用同一套判定</b>。
///
/// <para><b>为什么必须独立成一个类</b>：本项目反复出现的缺陷是「同一件事两套口径」。
/// Word 扫描器与 Excel 扫描器都要判定 <c>{{ai:xxx}}</c> 是 block、<c>{{@PAGE}}</c> 是 domain(auto)，
/// 若各写一份，模板作者在 Word 里标 <c>{{ai:x}}</c> 与在 Excel 里标，可能被归到不同形态。</para>
///
/// <para><b>零 LLM</b>（37 号 H-3）：模板自己声明了要填什么，判定是「读声明」不是「猜意图」。</para>
/// </summary>
public static class AnchorClassifier
{
    /// <summary>
    /// 由 token <b>内层内容</b>判定锚点形态（⛔ 传不含 <c>{{ }}</c> 的部分）。
    ///
    /// <list type="table">
    ///   <listheader><term>输入</term><description>结果</description></listheader>
    ///   <item><term><c>ai:quality_policy</c></term><description>block —— 段落级 AI 生成，⛔ 不写值</description></item>
    ///   <item><term><c>table:items</c></term><description>table —— 表格区域</description></item>
    ///   <item><term><c>@PAGE</c> / <c>@NUMPAGES</c></term><description>domain(auto) —— 交给 Word 自己算</description></item>
    ///   <item><term><c>@doc_no</c></term><description>domain(text) —— 页眉页脚里要写值的</description></item>
    ///   <item><term><c>enterprise.Name</c></term><description>scalar —— 表达式取值</description></item>
    ///   <item><term><c>ENT_NAME</c></term><description>scalar —— 普通字段</description></item>
    /// </list>
    ///
    /// <para><b>⛔ 刻意不猜 <c>table_total</c></b>：合计锚点没有可靠的命名约定，
    /// 靠 <c>SUM_</c> 前缀猜会误判 ⇒ 一律落 <c>scalar</c>，由实施人员在页面上改。</para>
    /// </summary>
    public static (string AnchorType, string? DomainKind) Classify(string inner)
    {
        var text = (inner ?? string.Empty).Trim();

        if (text.StartsWith(FillSyntax.AiPrefix, StringComparison.OrdinalIgnoreCase))
            return ("block", null);

        if (text.StartsWith(FillSyntax.TablePrefix, StringComparison.OrdinalIgnoreCase))
            return ("table", null);

        if (text.StartsWith(FillSyntax.SysPrefix, StringComparison.Ordinal))
        {
            // @PAGE / @NUMPAGES / @SECTIONPAGES 交给 Word 自己算 ⇒ domain(auto)，⛔ 不写值
            var key = text[1..].ToUpperInvariant();
            var isAuto = key is "PAGE" or "NUMPAGES" or "SECTIONPAGES";
            return ("domain", isAuto ? "auto" : "text");
        }

        return ("scalar", null);
    }

    /// <summary>
    /// 去命名空间前缀，得到与 <c>cert_doc_field_def.FieldCode</c> 对齐的键。
    /// <para>⚠️ 表达式（<c>enterprise.Name</c>）<b>保留原样</b> —— 它的命名空间是语义的一部分。</para>
    /// </summary>
    public static string FieldCodeOf(string inner)
    {
        var text = (inner ?? string.Empty).Trim();

        if (text.StartsWith(FillSyntax.AiPrefix, StringComparison.OrdinalIgnoreCase))
            return text[FillSyntax.AiPrefix.Length..];
        if (text.StartsWith(FillSyntax.TablePrefix, StringComparison.OrdinalIgnoreCase))
            return text[FillSyntax.TablePrefix.Length..];
        if (text.StartsWith(FillSyntax.SysPrefix, StringComparison.Ordinal))
            return text[1..];
        return text;
    }
}

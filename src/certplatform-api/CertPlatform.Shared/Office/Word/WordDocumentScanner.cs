using System.Text;
using System.Text.RegularExpressions;
using CertPlatform.Shared.Fill;
using NPOI.XWPF.UserModel;

namespace CertPlatform.Shared.Office.Word;

/// <summary>
/// Word 全文扫描 —— 两个用途：
/// <list type="number">
/// <item><b>自验收</b>：找残留锚点 / 残留标记（<see cref="Scan"/>，层 1 填充后校验用）。</item>
/// <item><b>模板锚点清单</b>：<see cref="ScanAnchors"/> —— 把模板里<b>所有</b>锚点扫出来，
/// 供「标准文档填写规则」页（37 号 §7.2）落 <c>cert_doc_template_anchor</c>。</item>
/// </list>
///
/// <para><b>为什么两者共用一个类</b>：37 号 Q-6 裁定「<b>扩展</b>已有扫描器，⛔ 不新写第二套解析」。
/// 两者的遍历骨架（段 / 表 / 嵌套表 / 页眉页脚）完全一致，只是<b>产出不同</b>
/// （残留 = 自验收；全量 = 规则定义）。拆成两个类就会出现两套遍历，必然漂移。</para>
///
/// <para><b>★ 零 LLM</b>（37 号 H-3）：模板自己声明了要填什么，扫描是「<b>读声明</b>」不是「猜意图」。</para>
///
/// <para>⚠️ <b>已知未覆盖</b>：文本框（<c>w:txbxContent</c>）、批注、脚注/尾注、艺术字。
/// 若模板用到，<see cref="Scan"/> 会照实报出残留锚点（扫描器只覆盖同样范围，所以这类残留
/// <b>不会</b>被报出）—— 这是当前的已知边界，见 26 号 §3.4.6 的落点说明。</para>
/// </summary>
public static class WordDocumentScanner
{
    /// <summary>扫描结果（自验收用）</summary>
    public readonly record struct ScanResult(List<string> LeftoverTokens, int LeftoverMarkRuns);

    /// <summary>
    /// <b>一条锚点</b>（模板锚点清单用）。字段与 <c>cert_doc_template_anchor</c> 一一对应，
    /// 便于上层直接落库（⛔ 不在上层再写一遍判定逻辑）。
    /// </summary>
    /// <param name="AnchorRef">锚点原文：<c>{{ENT_NAME}}</c></param>
    /// <param name="AnchorType">scalar / block / table / domain（由 token 内容判定）</param>
    /// <param name="AnchorKind">token（文本锚点）</param>
    /// <param name="FieldCode">去前缀的键，与 <c>cert_doc_field_def.FieldCode</c> 对齐</param>
    /// <param name="DomainKind">仅 domain 类型：text（写值）/ auto（交给 Word 算）</param>
    /// <param name="Location">body / table / header / footer —— ⚠️ <b>仅供前端展示</b>，
    /// <c>cert_doc_template_anchor</c> 无此列，<b>不落库、不参与去重</b>。</param>
    /// <param name="Context">所在段落文本片段（人工辨认用，⛔ 不落库）</param>
    public sealed record WordAnchor(
        string AnchorRef,
        string AnchorType,
        string AnchorKind,
        string FieldCode,
        string? DomainKind,
        string Location,
        string? Context,
        int Sort);

    /// <summary>
    /// 扫描<b>全部锚点</b>（37 号 §7.2 的 Word 侧，<b>零 LLM</b>）。
    ///
    /// <para><b>扫什么</b>：正文 / 表格（含嵌套）/ 页眉 / 页脚里的 <c>{{Token}}</c>。</para>
    ///
    /// <para><b>⛔ 首版不扫的两类（有意为之，非遗漏）</b>：</para>
    /// <list type="bullet">
    /// <item><b>书签 <c>ROW_xxx</c></b>（表格区域锚点）：NPOI 2.7.2 的 <c>CT_P</c> 未暴露
    /// <c>bookmarkStart</c> 集合，且<b>当前没有任何真实空白模板可验证</b>（用户裁定
    /// 「现在的文档不是我们真正设置好的空白模板」）⇒ 先不写一段无法验证的解析代码。
    /// 等有真模板再补 —— 这正是 37 号 §9「先做 spike」的建议。</item>
    /// <item><b><c>YZH_Mark</c> 人工填写区</b>：填充引擎侧该分支尚未落地（<c>MarkStyleName</c> 列已预留）。
    /// 现在扫出来只会给实施人员一堆「不知道要填什么」的空锚点，反而增加噪音。</item>
    /// </list>
    ///
    /// <para><b>★ 去重口径 = <c>uk_tpl_anchor</c> 同口径</b>：<c>AnchorKind + AnchorRef</c>。
    /// ⚠️ <b>不含 <c>Location</c></b> —— 因为 <c>cert_doc_template_anchor</c> 根本没有位置列，
    /// 正文与页眉里的同名 token <b>本来就只能存一条</b>（填充时两处都会填）。
    /// 若按位置去重，落库时会撞唯一键。</para>
    /// </summary>
    public static List<WordAnchor> ScanAnchors(
        XWPFDocument doc, IEnumerable<XWPFHeaderFooter>? extraHeaderFooters = null)
    {
        var result = new List<WordAnchor>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var sort = 0;

        void AddToken(string tokenText, string location, string? context)
        {
            // ★ 与 uk_tpl_anchor 同口径（AnchorKind + AnchorRef），⛔ 不含位置
            var key = $"token|{tokenText}";
            if (!seen.Add(key)) return;

            var inner = tokenText[2..^2].Trim();
            var (anchorType, domainKind) = AnchorClassifier.Classify(inner);
            result.Add(new WordAnchor(
                AnchorRef: tokenText,
                AnchorType: anchorType,
                AnchorKind: "token",
                FieldCode: AnchorClassifier.FieldCodeOf(inner),
                DomainKind: domainKind,
                Location: location,
                Context: context,
                Sort: sort++));
        }

        void ScanParagraph(XWPFParagraph p, string location)
        {
            var runs = p.Runs;
            if (runs == null || runs.Count == 0) return;

            // ★ 跨 run 归一：token 可能被 Word 拆到多个 run（W9）。此处先拼全文再匹配，
            //   与填充侧 WordRunText 的归一化口径一致 —— ⛔ 不合并 run（会破坏样式）。
            var sb = new StringBuilder();
            foreach (var run in runs) sb.Append(WordRunText.Get(run));

            var text = sb.ToString();
            if (text.IndexOf("{{", StringComparison.Ordinal) < 0) return;

            var ctx = Truncate(text);
            foreach (Match m in Regex.Matches(text, FillSyntax.TokenPattern, RegexOptions.CultureInvariant))
                AddToken(m.Value, location, ctx);
        }

        void ScanTable(XWPFTable t, string location)
        {
            foreach (var row in t.Rows)
                foreach (var cell in row.GetTableCells())
                {
                    foreach (var p in cell.Paragraphs) ScanParagraph(p, location);
                    foreach (var nested in cell.Tables) ScanTable(nested, location);
                }
        }

        void ScanHeaderFooter(XWPFHeaderFooter hf)
        {
            var loc = hf is XWPFHeader ? "header" : "footer";
            foreach (var p in hf.Paragraphs) ScanParagraph(p, loc);
            foreach (var t in hf.Tables) ScanTable(t, loc);
        }

        foreach (var p in doc.Paragraphs) ScanParagraph(p, "body");
        foreach (var t in doc.Tables) ScanTable(t, "body");

        foreach (var h in doc.HeaderList) ScanHeaderFooter(h);
        foreach (var f in doc.FooterList) ScanHeaderFooter(f);

        // ★ 本次新建的页眉/页脚不在 HeaderList/FooterList 里（见 WordFillWriter 的说明），
        //   必须显式补扫，否则「新建页眉里的锚点」会被漏掉。
        if (extraHeaderFooters != null)
        {
            // ⚠️ 不能写 doc.HeaderList.Concat(doc.FooterList) —— HeaderList 是 IList<XWPFHeader>、
            //    FooterList 是 IList<XWPFFooter>，元素类型不同 ⇒ Concat 泛型推断失败（CS1929）。
            var known = new HashSet<XWPFHeaderFooter>();
            foreach (var h in doc.HeaderList) known.Add(h);
            foreach (var f in doc.FooterList) known.Add(f);
            foreach (var hf in extraHeaderFooters)
                if (known.Add(hf)) ScanHeaderFooter(hf);
        }

        return result;
    }

    /// <summary>
    /// 全文扫描：<b>找残留锚点</b>、<b>找残留标记</b>（层 1 填充后的自验收）。
    ///
    /// <para><b>为什么必须有独立扫描器（而不是复用填充器的遍历）</b>：
    /// 填充器的遍历是「按我知道的位置去找」；扫描器是「把整份文档<b>独立地</b>再读一遍」。
    /// 两者结果不一致，就说明填充器<b>漏了某处</b>（漏页眉、漏嵌套表、漏文本框）。
    /// 这正是自验收的价值 —— ⛔ 不要为了省一次遍历而让扫描器调用填充器的遍历。</para>
    /// </summary>
    public static ScanResult Scan(
        XWPFDocument doc, string markStyleId, IEnumerable<XWPFHeaderFooter>? extraHeaderFooters = null)
    {
        var tokens = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var markRuns = 0;

        void ScanParagraph(XWPFParagraph p)
        {
            var runs = p.Runs;
            if (runs == null || runs.Count == 0) return;

            var sb = new StringBuilder();
            foreach (var run in runs)
            {
                sb.Append(WordRunText.Get(run));

                if (!string.IsNullOrEmpty(markStyleId))
                {
                    var style = run.GetStyle();
                    if (string.Equals(style, markStyleId, StringComparison.Ordinal)) markRuns++;
                }
            }

            var text = sb.ToString();
            if (text.IndexOf("{{", StringComparison.Ordinal) < 0) return;

            foreach (Match m in Regex.Matches(text, FillSyntax.TokenPattern, RegexOptions.CultureInvariant))
                if (seen.Add(m.Value)) tokens.Add(m.Value);
        }

        void ScanTable(XWPFTable t)
        {
            foreach (var row in t.Rows)
                foreach (var cell in row.GetTableCells())
                {
                    foreach (var p in cell.Paragraphs) ScanParagraph(p);
                    foreach (var nested in cell.Tables) ScanTable(nested);
                }
        }

        void ScanHeaderFooter(XWPFHeaderFooter hf)
        {
            foreach (var p in hf.Paragraphs) ScanParagraph(p);
            foreach (var t in hf.Tables) ScanTable(t);
        }

        foreach (var p in doc.Paragraphs) ScanParagraph(p);
        foreach (var t in doc.Tables) ScanTable(t);
        foreach (var h in doc.HeaderList) ScanHeaderFooter(h);
        foreach (var f in doc.FooterList) ScanHeaderFooter(f);

        // ★ 本次新建的页眉/页脚不在 HeaderList/FooterList 里（见 WordFillWriter 的说明），
        //   必须显式补扫，否则「新建页眉里的残留锚点」会被漏报。
        if (extraHeaderFooters != null)
        {
            var known = new HashSet<XWPFHeaderFooter>();
            foreach (var h in doc.HeaderList) known.Add(h);
            foreach (var f in doc.FooterList) known.Add(f);
            foreach (var hf in extraHeaderFooters)
                if (known.Add(hf)) ScanHeaderFooter(hf);
        }

        return new ScanResult(tokens, markRuns);
    }

    // ════════════════════════════════════════════════════════════════════
    // 私有：辅助
    // ════════════════════════════════════════════════════════════════════

    /// <summary>段落文本截断（人工辨认用，⛔ 不落库）</summary>
    private static string? Truncate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var t = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return t.Length <= 60 ? t : t[..60] + "…";
    }
}

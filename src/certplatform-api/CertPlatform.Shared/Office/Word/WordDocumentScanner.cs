using System.Text;
using System.Text.RegularExpressions;
using CertPlatform.Shared.Fill;
using NPOI.XWPF.UserModel;

namespace CertPlatform.Shared.Office.Word;

/// <summary>
/// Word 全文扫描 —— 只做两件事：<b>找残留锚点</b>、<b>找残留标记</b>。
///
/// <para><b>为什么必须有独立扫描器（而不是复用填充器的遍历）</b>：
/// 填充器的遍历是「按我知道的位置去找」；扫描器是「把整份文档**独立地**再读一遍」。
/// 两者结果不一致，就说明填充器**漏了某处**（漏页眉、漏嵌套表、漏文本框）。
/// 这正是自验收的价值 —— ⛔ 不要为了省一次遍历而让扫描器调用填充器的遍历。</para>
///
/// <para>⚠️ <b>已知未覆盖</b>：文本框（<c>w:txbxContent</c>）、批注、脚注/尾注、艺术字。
/// 若模板用到，<see cref="OfficeFillReport.LeftoverTokens"/> 会**照实报出**残留锚点
/// （扫描器只覆盖同样范围，所以这类残留<b>不会</b>被报出）—— 这是当前的已知边界，
/// 见 26 号 §3.4.6 的落点说明。</para>
/// </summary>
internal static class WordDocumentScanner
{
    /// <summary>扫描结果</summary>
    internal readonly record struct ScanResult(List<string> LeftoverTokens, int LeftoverMarkRuns);

    internal static ScanResult Scan(
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
            // ⚠️ 不能写 doc.HeaderList.Concat(doc.FooterList) —— HeaderList 是 IList<XWPFHeader>、
            //    FooterList 是 IList<XWPFFooter>，元素类型不同 ⇒ Concat 泛型推断失败（CS1929）。
            var known = new HashSet<XWPFHeaderFooter>();
            foreach (var h in doc.HeaderList) known.Add(h);
            foreach (var f in doc.FooterList) known.Add(f);
            foreach (var hf in extraHeaderFooters)
                if (known.Add(hf)) ScanHeaderFooter(hf);
        }

        return new ScanResult(tokens, markRuns);
    }
}

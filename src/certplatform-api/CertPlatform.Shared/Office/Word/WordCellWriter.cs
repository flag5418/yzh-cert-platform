using NPOI.XWPF.UserModel;

namespace CertPlatform.Shared.Office.Word;

/// <summary>
/// Word 单元格写入原语 —— <b>「把值写进一个单元格，且不动它的样式」</b>。
///
/// <para><b>★ 为什么单独抽一个原语</b>：参考实现（房产测绘 <c>YZH.BaseReport</c> 系列）用
/// <c>table.CreateCellParagraph(row, col, value, isBold, fontSize, ...)</c> ——
/// <b>每次写入都重新造一个段落并显式设字体/字号/对齐</b>。
/// 那样写等于<b>把模板的排版推倒重来</b>，且换一张模板就要重调一遍字号。</para>
///
/// <para>2026-10-02 用户规格把这条边界定死了：<b>「单元格样式不是我们在程序中设定的，
/// 是在 word 和 excel 模板控制的」</b>。⇒ 本原语只做一件事：
/// <b>把文本塞进已有的 run，样式（字体/字号/加粗/颜色/底纹/边框/行高/合并）一律沿用模板</b>。</para>
///
/// <para><b>★ 为什么不能直接用 <c>cell.SetText()</c></b>（实测坑，见 REFERENCE §二十 ㊱）：
/// <c>XWPFTableCell.SetText()</c> 只改 <c>CT_Tc</c>、<b>不刷新段落缓存</b> ⇒
/// 同会话内 <c>cell.GetText()</c> 读回空串，自验收会误判为「没写进去」。
/// 故这里一律走<b>段落 / run 级 API</b>。</para>
/// </summary>
internal static class WordCellWriter
{
    /// <summary>
    /// 把 <paramref name="text"/> 写进单元格，<b>保留该格原有字符样式</b>。
    ///
    /// <para>规则：写进<b>第一个段落</b>；该段落已有 run ⇒ 复用首个 run（保留其 <c>rPr</c>）
    /// 并删除其余 run；无 run ⇒ 新建一个（此时继承段落/表格默认样式）。</para>
    ///
    /// <para>⚠️ 单元格里的**其余段落原样保留** —— 它们可能是模板作者刻意留的
    /// （如「签名：______」），⛔ 不要为了「干净」把它们删掉。</para>
    /// </summary>
    public static void SetText(XWPFTableCell cell, string? text)
    {
        var value = text ?? string.Empty;

        var paragraphs = cell.Paragraphs;
        var p = paragraphs.Count > 0 ? paragraphs[0] : cell.AddParagraph();

        var runs = p.Runs;
        if (runs.Count == 0)
        {
            p.CreateRun().SetText(value);
            return;
        }

        // 从后往前删，避免索引位移（★ 走 run API，⛔ 不直接改 CT_P —— 见 REFERENCE §二十 ㊳）
        for (var i = runs.Count - 1; i >= 1; i--)
            p.RemoveRun(i);

        WordRunText.Set(p.Runs[0], value);
    }

    /// <summary>
    /// 读取单元格当前文本（拼接全部段落）。
    /// <para>★ 用于自验收与测试；⛔ 不要用 <c>cell.GetText()</c>（见类注释的缓存坑）。</para>
    /// </summary>
    public static string GetText(XWPFTableCell cell)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var p in cell.Paragraphs)
            foreach (var r in p.Runs)
                sb.Append(WordRunText.Get(r));

        return sb.ToString();
    }
}

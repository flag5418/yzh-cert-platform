using NPOI.OpenXmlFormats.Wordprocessing;
using NPOI.XWPF.UserModel;

namespace CertPlatform.Shared.Office.Word;

/// <summary>
/// Word 域（field）写入 —— 对应 <c>doc_domain</c>（26 号 §3.4.6）。
///
/// <para><b>★ 为什么不能「把 <c>{ PAGE }</c> 当普通文本写」</b>：
/// 域是一段**有结构的 XML**（begin / instrText / separate / 结果文本 / end 五个 run），
/// 若整段 <c>setText("{ PAGE }")</c>，Word 只看到一段**死文本**——
/// 页码不再随页变化、目录不再更新、域底纹消失，而且**打开文件完全看不出异常**（典型静默失败）。
/// ⇒ 必须按结构写。</para>
///
/// <para><b>本期范围</b>（23 号已定）：只写 <c>domain(text)</c>（结果文本由调用方给定）；
/// <c>domain(auto)</c>（页码/总页数等需要 Word 重算的域）<b>本期只登记不写值</b> ——
/// 因为「写入时的结果文本」是错的（第 1 页写 1，打印到第 3 页仍是 1），
/// 交给 Word 首次打开时重算才是对的。⛔ 不要为了「看起来填上了」而写 auto 域。</para>
/// </summary>
internal static class WordFieldWriter
{
    /// <summary>
    /// 用域**整体替换**一个段落的内容。
    ///
    /// <para>⛔ 保留 <c>&lt;w:pPr&gt;</c>（段落样式 / 对齐 / 缩进 / 编号），只清空 run。
    /// 这是「锚点独占一段」的约定：域是块级视觉元素，混在文字中间无法保证排版。</para>
    ///
    /// <para><b>★ 必须用 <see cref="XWPFParagraph"/> 的 run API（<c>RemoveRun</c> / <c>CreateRun</c>），
    /// ⛔ 不能直接改 <c>CT_P</c></b>：<c>XWPFParagraph</c> 在构造时把 run **缓存**在内部列表中，
    /// 直接操作 <c>CT_P</c> 只改 XML、不改缓存 ⇒ 同一会话内 <c>paragraph.Runs</c> 仍是旧内容
    /// （实测：段落实际已是域结构，扫描器却还能读到 <c>{{@page_no}}</c> ⇒ 自验收误报残留）。
    /// 用 run API 则两边同步。</para>
    /// </summary>
    internal static void ReplaceParagraphWithField(XWPFParagraph paragraph, string instruction, string displayText)
    {
        // 1. 清空全部 run（<w:pPr> 不受影响）
        while (paragraph.Runs.Count > 0) paragraph.RemoveRun(0);

        // 2. 五段式域结构
        //    ⛔ 顺序不可调换：begin → instrText → separate → 结果 → end
        paragraph.CreateRun().GetCTR().AddNewFldChar().fldCharType = ST_FldCharType.begin;

        var instrText = paragraph.CreateRun().GetCTR().AddNewInstrText();
        // 指令两侧必须留空格（OOXML 要求），且必须 preserve 否则空格被吞
        instrText.Value = " " + (instruction ?? string.Empty).Trim() + " ";
        instrText.space = "preserve";

        paragraph.CreateRun().GetCTR().AddNewFldChar().fldCharType = ST_FldCharType.separate;

        if (!string.IsNullOrEmpty(displayText))
            paragraph.CreateRun().SetText(displayText);

        paragraph.CreateRun().GetCTR().AddNewFldChar().fldCharType = ST_FldCharType.end;
    }
}

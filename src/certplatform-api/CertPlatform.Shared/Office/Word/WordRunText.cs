using NPOI.OpenXmlFormats.Wordprocessing;
using NPOI.XWPF.UserModel;

namespace CertPlatform.Shared.Office.Word;

/// <summary>
/// Word run 级文本读写 —— <b>W9（token 跨 run）的唯一解法落点</b>。
///
/// <para><b>为什么不能直接用 <c>run.Text</c> / <c>run.SetText()</c></b>：</para>
/// <list type="number">
///   <item>一个 <c>&lt;w:r&gt;</c> 里可以有**多个** <c>&lt;w:t&gt;</c>（Word 拆分/合并编辑历史造成），
///         <c>run.Text</c> 只反映其中一个 ⇒ 拼接式读取会**丢字**。</item>
///   <item><c>run.SetText(v)</c> 只写第 0 个 <c>&lt;w:t&gt;</c>，**残留**其余 <c>&lt;w:t&gt;</c> 的旧内容
///         ⇒ 症状是「替换后原文还在」（且不报错）。</item>
/// </list>
///
/// <para><b>本类的两条不变量</b>：① 读写覆盖 run 内**全部** <c>&lt;w:t&gt;</c>；
/// ② <b>永不触碰 <c>&lt;w:rPr&gt;</c></b> ⇒ 字符样式天然保留（这是「写值不丢样式」的实现方式，
/// 而不是「写完再把样式刷回去」——后者在样式继承/主题字体场景会刷错）。</para>
/// </summary>
internal static class WordRunText
{
    /// <summary>读取 run 的全部文本（拼接该 run 内所有 <c>&lt;w:t&gt;</c>）</summary>
    internal static string Get(XWPFRun run)
    {
        var ctr = run.GetCTR();
        var count = ctr.SizeOfTArray();
        if (count == 0) return string.Empty;
        if (count == 1) return ctr.GetTArray(0).Value ?? string.Empty;

        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < count; i++) sb.Append(ctr.GetTArray(i).Value);
        return sb.ToString();
    }

    /// <summary>
    /// 覆写 run 的文本：保留首个 <c>&lt;w:t&gt;</c>，删除其余，避免旧文本残留。
    /// <para>仅当文本首/尾为空白时写 <c>xml:space="preserve"</c>（与 NPOI 自身 <c>preserveSpaces</c> 同口径，
    /// ⛔ 不做「无条件 preserve」——会给整份文档的 XML 增加无意义属性，干扰人工比对）。</para>
    /// </summary>
    internal static void Set(XWPFRun run, string text)
    {
        var ctr = run.GetCTR();

        while (ctr.SizeOfTArray() > 1) ctr.RemoveT(ctr.SizeOfTArray() - 1);

        CT_Text t;
        if (ctr.SizeOfTArray() == 0)
        {
            // ⛔ 不为空串新建 <w:t/>：会产生无意义的空元素，且 Word 打开时可能被规范化掉
            if (text.Length == 0) return;
            t = ctr.AddNewT();
        }
        else
        {
            t = ctr.GetTArray(0);
        }

        t.Value = text;
        if (text.Length > 0 && (char.IsWhiteSpace(text[0]) || char.IsWhiteSpace(text[^1])))
            t.space = "preserve";
    }

    /// <summary>
    /// run 是否**只承载文本**。
    /// <para>返回 <c>false</c> = 该 run 有子元素但没有 <c>&lt;w:t&gt;</c> ⇒ 它是域 / 图片 / 换行 / 制表符等
    /// <b>原子内容</b>。锚点若跨越这类 run，替换会「删掉锚点却留下原子内容」，属不可预期结果
    /// ⇒ 调用方应**跳过并记为待办**（见 <see cref="WordParagraphFiller"/>）。</para>
    /// </summary>
    internal static bool IsTextOnly(XWPFRun run)
    {
        var ctr = run.GetCTR();
        if (ctr.SizeOfTArray() > 0) return true;
        // 无 <w:t> 且无任何子元素 ⇒ 空 run（如仅带 rPr 的占位），不影响索引
        return ctr.Items == null || ctr.Items.Count == 0;
    }
}

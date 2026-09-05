namespace YZH.Core.Extractor.Models
{
    /// <summary>
    /// 结构化文本段落。
    /// <para>每个文件类型统一输出 Sections 列表，LLM 可精准定位每段内容的位置（页码/行号）。</para>
    /// <para>PositionInfo 为 JSON 字符串，格式依 SourceType 而定：</para>
    /// <list type="bullet">
    ///   <item>Word: {"page":n,"line_start":n,"line_end":n}</item>
    ///   <item>Excel: {"sheet":"名","row":n}</item>
    ///   <item>PDF: {"page":n}</item>
    ///   <item>Text: {"line":n}</item>
    /// </list>
    /// </summary>
    public class TextSection
    {
        /// <summary>段落内容文本</summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>页码（Word/PDF 有效；Excel/Text 为 0）</summary>
        public int PageNumber { get; set; }

        /// <summary>段落序号（同页面/工作表内递增）</summary>
        public int SectionIndex { get; set; }

        /// <summary>位置信息 JSON（见类注释）</summary>
        public string? PositionInfo { get; set; }

        /// <summary>段落类型：paragraph / table / sheet / page / line</summary>
        public string SectionType { get; set; } = "paragraph";

        /// <summary>所属工作表名（仅 Excel sheet 类型时有值）</summary>
        public string? SheetName { get; set; }

        /// <summary>是否为表格段落（Word 表格 / Excel 全表）</summary>
        public bool IsTable => SectionType == "table";
    }
}

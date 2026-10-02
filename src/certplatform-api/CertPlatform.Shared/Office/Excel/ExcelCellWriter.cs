using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace CertPlatform.Shared.Office.Excel;

/// <summary>
/// Excel 单元格写入原语 —— <b>「把一个值写进一个格子，且不动它的样式」</b>。
///
/// <para><b>★ 2026-10-02 用户规格</b>：「单元格样式不是我们在程序中设定的，是在 word 和 excel
/// <b>模板控制的</b>」⇒ 本原语<b>只改值</b>，样式一律沿用；唯一例外是<b>数字/日期的显示格式</b>
/// —— 那不套格式就<b>不是「不好看」而是语义丢失</b>（日期显示成 <c>46235</c>）。</para>
///
/// <para><b>三条 Excel 专属陷阱（全部在此消化）</b>：</para>
/// <list type="number">
///   <item><b>类型必须显式</b>：<c>SetCellValue(string)</c> 会把数字写成文本 ⇒ 不能求和/排序，
///         且打开文件看不出来。⇒ 由 <see cref="FillValue.Kind"/> 决定重载，⛔ 不按值猜类型。</item>
///   <item><b>数字/日期要显式 <c>DataFormat</c></b>。</item>
///   <item><b><c>cell.CellStyle</c> 必须保留</b>：直接 <c>cell.CellStyle = wb.CreateCellStyle()</c>
///         会丢掉边框/底纹/对齐，症状是「填完值表格框线没了」⇒ 用 <c>CloneStyleFrom</c> 复制原样式再改格式。</item>
/// </list>
/// </summary>
internal static class ExcelCellWriter
{
    /// <summary>
    /// 按值类型写单元格。<paramref name="value"/> 为 <c>null</c> ⇒ <b>写空串</b>
    /// （用户规格：「如果没有值则自动将填写内容赋值为空」）。
    /// </summary>
    public static void Write(ICell cell, FillValue? value, StyleCache styleCache)
    {
        if (value == null)
        {
            cell.SetCellValue(string.Empty);
            return;
        }

        switch (value.Kind)
        {
            case FillValueKind.Number:
                cell.SetCellValue(value.Number ?? 0d);
                break;
            case FillValueKind.Date:
                cell.SetCellValue(value.Date ?? default);
                break;
            case FillValueKind.Bool:
                cell.SetCellValue(value.Bool ?? false);
                break;
            default:
                cell.SetCellValue(value.ToDisplayText());
                break;
        }

        ApplyNumberFormat(cell, value, styleCache);
    }

    /// <summary>
    /// 按需给单元格套 <c>DataFormat</c>。优先级：<b>值字典给的 &gt; 模板锚点里写的 &gt; 原格格式</b>。
    ///
    /// <para>★ <b>Date 的「类型驱动兜底」</b>：日期值不套日期格式时 Excel 显示成 <c>46235</c>
    /// —— 这是<b>语义丢失</b>而非「不好看」。故：给了格式用给的；没给但原格已是日期格式则不动；
    /// 没给且原格是 <c>General</c> ⇒ 兜底 <c>yyyy-mm-dd</c>。</para>
    ///
    /// <para>⛔ 这不违反「不做类型推断」：兜底依据是<b>值的类型</b>而非字面内容。
    /// <see cref="FillValueKind.Number"/> <b>不做兜底</b> ——「1000 显示成 1,000 还是 1000.00」
    /// 是业务判断，属层 2。</para>
    /// </summary>
    public static void ApplyNumberFormat(ICell cell, FillValue value, StyleCache styleCache)
    {
        var format = value.NumberFormat;

        if (string.IsNullOrWhiteSpace(format))
        {
            if (value.Kind != FillValueKind.Date) return;

            var current = cell.CellStyle?.GetDataFormatString();
            if (!string.IsNullOrWhiteSpace(current)
                && !string.Equals(current, "General", StringComparison.OrdinalIgnoreCase))
            {
                return;   // 原格已是日期格式 ⇒ 不动
            }

            format = "yyyy-mm-dd";
        }

        cell.CellStyle = styleCache.GetOrClone(cell.CellStyle, format!);
    }
}

/// <summary>
/// 单元格样式缓存：<c>(原样式索引, 目标格式串) → 新样式</c>。
/// <para>⛔ 必须缓存：Excel 单个工作簿样式数上限 64,000，逐格 <c>CreateCellStyle</c>
/// 会在大表上撞上限并抛异常（且报错位置与真实原因无关）。</para>
/// </summary>
internal sealed class StyleCache
{
    private readonly XSSFWorkbook _workbook;
    private readonly Dictionary<(short, string), ICellStyle> _cache = new();

    internal StyleCache(XSSFWorkbook workbook) => _workbook = workbook;

    internal ICellStyle GetOrClone(ICellStyle? source, string format)
    {
        var sourceIndex = source?.Index ?? -1;
        var cacheKey = (sourceIndex, format);

        if (_cache.TryGetValue(cacheKey, out var cached)) return cached;

        var style = _workbook.CreateCellStyle();
        if (source != null) style.CloneStyleFrom(source);
        style.DataFormat = _workbook.CreateDataFormat().GetFormat(format);

        _cache[cacheKey] = style;
        return style;
    }
}

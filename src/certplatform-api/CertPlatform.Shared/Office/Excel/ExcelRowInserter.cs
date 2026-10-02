using NPOI.SS.UserModel;

namespace CertPlatform.Shared.Office.Excel;

/// <summary>
/// Excel 行插入原语 —— 对应 <c>doc_table_write</c> 的 Excel 侧（26 号 §3.4.6）。
///
/// <para><b>★★ 必现副作用：<c>ShiftRows</c> 会把「区外内容」一起下移。</b>
/// 这是 NPOI/POI 的既定行为（整行下移），<b>不报错</b>。所以：</para>
/// <list type="bullet">
///   <item>模板的数据区**下方不能有别的内容**（签名栏、说明、合计行）—— 否则会被推走；</item>
///   <item>若确有下方内容，调用方必须先记录其原行号，插入后**按新行号重新定位**（或先整体上移再统一回写）；</item>
///   <item>⛔ <b>禁止「先删区外内容再插行」</b> —— 那会永久丢失模板资产（19 号 E8 已定）。</item>
/// </list>
///
/// <para><b>★ 合计行锚点按数据区下沿相对重定位</b>：不要在模板里写死 <c>=SUM(D5:D9)</c> 的行号，
/// 而应在插行后按「数据区起始 + 实际行数」重算 —— 否则插行后合计区间永远是错的（且 Excel 不报错）。</para>
/// </summary>
public static class ExcelRowInserter
{
    /// <summary>
    /// 在 <paramref name="startRow"/> 处下移 <paramref name="count"/> 行，腾出插入空间。
    /// <para>下移区间 = <c>[startRow, lastRowToShift]</c>；<c>lastRowToShift</c> 建议取
    /// <c>sheet.LastRowNum</c>（含）以确保下方全部内容同步下移、不互相覆盖。</para>
    /// </summary>
    public static void ShiftDown(
        ISheet sheet,
        int startRow,
        int count,
        int? lastRowToShift = null,
        bool copyRowHeight = true,
        bool resetOriginalRowHeight = false)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        if (count <= 0) return;

        var last = lastRowToShift ?? sheet.LastRowNum;
        if (last < startRow) last = startRow;

        sheet.ShiftRows(startRow, last, count, copyRowHeight, resetOriginalRowHeight);
    }

    /// <summary>
    /// 把 <paramref name="sourceRowIndex"/> 整行**深拷贝**到 <paramref name="targetRowIndex"/>。
    /// <para>⚠️ 仅当目标行**已存在**时有效（<c>CopyRow</c> 不创建行）。要插入新行请先
    /// <see cref="ShiftDown"/> 腾位，再调用本方法 —— 顺序颠倒会覆盖原数据。</para>
    /// </summary>
    public static void CopyRow(ISheet sheet, int sourceRowIndex, int targetRowIndex)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        sheet.CopyRow(sourceRowIndex, targetRowIndex);
    }
}

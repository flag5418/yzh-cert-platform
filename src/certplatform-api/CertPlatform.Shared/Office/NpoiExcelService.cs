using System.Globalization;
using System.Reflection;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using YZH.Core.Stand.Interfaces;

namespace CertPlatform.Shared.Office;

/// <summary>
/// <see cref="IExcelService"/> 的 NPOI 实现 —— 补上 YZH.Core 里 <c>NullExcelService</c>
/// 三个方法全 <c>throw NotImplementedException</c> 的空洞（26 号 §3.4.6 现状实测第 3 行）。
///
/// <para><b>注册方式</b>（业务项目层，即 <c>AddCertPlatformAdminServices()</c>）：
/// <c>services.AddSingleton&lt;IExcelService, NpoiExcelService&gt;();</c></para>
///
/// <para><b>★ 表头契约（导出/模板/导入三者必须一致）</b>：</para>
/// <list type="bullet">
///   <item>导出：给了 <c>columnMapping</c> ⇒ 表头用 <c>DesName</c> 且**只导出映射内的字段**；未给 ⇒ 表头用**属性名**。</item>
///   <item>模板：表头 = <c>fieldNames</c>（未给则 = 全部可读写属性名）。</item>
///   <item>导入：表头**按属性名匹配**（大小写不敏感）。
///         ⇒ ⛔ <b>导入用的表头必须是属性名</b> —— 即「导出时传了自定义 <c>DesName</c>」的文件**不能直接导回**。
///         这不是缺陷，是刻意的：<c>DesName</c> 是给人看的，属性名是给程序看的，
///         两者混用才会产生「看着对、导进来是空的」这类静默失败。
///         ⇒ 导入请用 <see cref="GenerateImportTemplate{T}"/> 生成的模板。</item>
/// </list>
/// </summary>
public sealed class NpoiExcelService : IExcelService
{
    /// <inheritdoc />
    public byte[] ExportToExcel<T>(
        IList<T> entities, IDictionary<string, string>? columnMapping = null, string sheetName = "Sheet1")
        where T : class
    {
        ArgumentNullException.ThrowIfNull(entities);

        var props = ResolveExportProperties<T>(columnMapping);

        using var workbook = new XSSFWorkbook();
        var sheet = workbook.CreateSheet(string.IsNullOrWhiteSpace(sheetName) ? "Sheet1" : sheetName);
        var dateStyle = CreateDateFormatStyle(workbook, "yyyy-mm-dd");

        // 表头
        var header = sheet.CreateRow(0);
        for (var c = 0; c < props.Count; c++)
            header.CreateCell(c).SetCellValue(props[c].Header);

        // 数据
        for (var r = 0; r < entities.Count; r++)
        {
            var row = sheet.CreateRow(r + 1);
            for (var c = 0; c < props.Count; c++)
            {
                var cell = row.CreateCell(c);
                WriteCellValue(cell, props[c].Property.GetValue(entities[r]), dateStyle);
            }
        }

        using var output = new MemoryStream();
        workbook.Write(output, leaveOpen: false);
        return output.ToArray();
    }

    /// <inheritdoc />
    public List<T> ImportFromExcel<T>(byte[] fileBytes, int headerRow = 1) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(fileBytes);
        if (fileBytes.Length == 0) return new List<T>();

        var result = new List<T>();

        using var input = new MemoryStream(fileBytes, writable: false);
        var workbook = new XSSFWorkbook(input);
        var sheet = workbook.GetSheetAt(0);
        if (sheet == null) return result;

        var headerIndex = Math.Max(0, headerRow - 1);
        var headerRowObj = sheet.GetRow(headerIndex);
        if (headerRowObj == null) return result;

        // 表头 → 属性
        var propertyMap = new Dictionary<int, PropertyInfo>();
        for (var ci = headerRowObj.FirstCellNum; ci < headerRowObj.LastCellNum; ci++)
        {
            var cell = headerRowObj.GetCell(ci);
            if (cell == null) continue;
            var title = ReadCellAsString(cell);
            if (string.IsNullOrWhiteSpace(title)) continue;

            var prop = typeof(T).GetProperty(
                title.Trim(), BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop is { CanWrite: true }) propertyMap[ci] = prop;
        }

        if (propertyMap.Count == 0) return result;

        for (var ri = headerIndex + 1; ri <= sheet.LastRowNum; ri++)
        {
            var row = sheet.GetRow(ri);
            if (row == null) continue;

            var item = new T();
            var anyValue = false;

            foreach (var (ci, prop) in propertyMap)
            {
                var cell = row.GetCell(ci);
                if (cell == null) continue;

                var raw = ReadCellAsString(cell);
                if (string.IsNullOrWhiteSpace(raw)) continue;

                if (TryConvert(raw, prop.PropertyType, out var converted))
                {
                    prop.SetValue(item, converted);
                    anyValue = true;
                }
            }

            // ⛔ 整行为空的「幽灵行」不入结果（Excel 常见：格式被刷过但没内容）
            if (anyValue) result.Add(item);
        }

        return result;
    }

    /// <inheritdoc />
    public byte[] GenerateImportTemplate<T>(IList<string>? fieldNames = null) where T : class
    {
        var names = fieldNames is { Count: > 0 }
            ? fieldNames.ToList()
            : typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0)
                .Select(p => p.Name)
                .ToList();

        using var workbook = new XSSFWorkbook();
        var sheet = workbook.CreateSheet("Sheet1");

        var header = sheet.CreateRow(0);
        for (var c = 0; c < names.Count; c++)
            header.CreateCell(c).SetCellValue(names[c]);

        // 给表头加粗 + 冻结首行：降低「填错列」的概率
        var boldFont = workbook.CreateFont();
        boldFont.IsBold = true;
        var headerStyle = workbook.CreateCellStyle();
        headerStyle.SetFont(boldFont);
        for (var c = 0; c < names.Count; c++) header.GetCell(c).CellStyle = headerStyle;

        sheet.CreateFreezePane(0, 1);

        using var output = new MemoryStream();
        workbook.Write(output, leaveOpen: false);
        return output.ToArray();
    }

    // ───────────────────────────── 内部 ─────────────────────────────

    private sealed record ExportColumn(string Header, PropertyInfo Property);

    private static List<ExportColumn> ResolveExportProperties<T>(IDictionary<string, string>? columnMapping)
        where T : class
    {
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .ToList();

        if (columnMapping is not { Count: > 0 })
            return properties.Select(p => new ExportColumn(p.Name, p)).ToList();

        var columns = new List<ExportColumn>();
        foreach (var (field, desName) in columnMapping)
        {
            var prop = properties.FirstOrDefault(
                p => string.Equals(p.Name, field, StringComparison.OrdinalIgnoreCase));
            // ⛔ 映射里写了不存在的字段 ⇒ 直接抛，不静默跳过：
            //    「导出少了一列」比「导出报错」难查得多（本仓 §二十 静默失败模式）。
            if (prop == null)
                throw new ArgumentException($"列映射中的字段 '{field}' 在 {typeof(T).Name} 上不存在", nameof(columnMapping));

            columns.Add(new ExportColumn(string.IsNullOrWhiteSpace(desName) ? prop.Name : desName, prop));
        }

        return columns;
    }

    private static ICellStyle CreateDateFormatStyle(IWorkbook workbook, string format)
    {
        var style = workbook.CreateCellStyle();
        style.DataFormat = workbook.CreateDataFormat().GetFormat(format);
        return style;
    }

    private static void WriteCellValue(ICell cell, object? value, ICellStyle dateStyle)
    {
        switch (value)
        {
            case null:
                break;
            case DateTime dt:
                cell.SetCellValue(dt);
                cell.CellStyle = dateStyle;   // ⛔ 不设 DataFormat 会显示成 46235
                break;
            case bool b:
                cell.SetCellValue(b);
                break;
            case byte or sbyte or short or ushort or int or uint or long or ulong:
                cell.SetCellValue(Convert.ToDouble(value, CultureInfo.InvariantCulture));
                break;
            case float or double or decimal:
                cell.SetCellValue(Convert.ToDouble(value, CultureInfo.InvariantCulture));
                break;
            case Enum e:
                cell.SetCellValue(e.ToString());
                break;
            default:
                cell.SetCellValue(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
                break;
        }
    }

    private static string ReadCellAsString(ICell cell) => cell.CellType switch
    {
        CellType.String => cell.StringCellValue ?? string.Empty,
        CellType.Numeric => DateUtil.IsCellDateFormatted(cell)
            ? (cell.DateCellValue?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? string.Empty)
            : cell.NumericCellValue.ToString("R", CultureInfo.InvariantCulture),
        CellType.Boolean => cell.BooleanCellValue ? "true" : "false",
        CellType.Formula => cell.CachedFormulaResultType == CellType.Numeric
            ? cell.NumericCellValue.ToString("R", CultureInfo.InvariantCulture)
            : cell.ToString() ?? string.Empty,
        _ => cell.ToString() ?? string.Empty,
    };

    private static bool TryConvert(string raw, Type targetType, out object? value)
    {
        value = null;

        var type = Nullable.GetUnderlyingType(targetType) ?? targetType;
        var text = raw.Trim();

        try
        {
            if (type == typeof(string)) { value = raw; return true; }
            if (type == typeof(DateTime))
            {
                if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                { value = dt; return true; }
                if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var oa))
                { value = DateTime.FromOADate(oa); return true; }
                return false;
            }
            if (type == typeof(bool))
            {
                if (bool.TryParse(text, out var b)) { value = b; return true; }
                if (text is "1" or "是" or "Y" or "y") { value = true; return true; }
                if (text is "0" or "否" or "N" or "n") { value = false; return true; }
                return false;
            }
            if (type.IsEnum)
            {
                if (Enum.TryParse(type, text, ignoreCase: true, out var e)) { value = e; return true; }
                return false;
            }

            value = Convert.ChangeType(text, type, CultureInfo.InvariantCulture);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException or ArgumentException)
        {
            return false;
        }
    }
}

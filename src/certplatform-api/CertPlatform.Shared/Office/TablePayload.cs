using System.Collections.Generic;

namespace CertPlatform.Shared.Office;

/// <summary>
/// 表格数据载荷 —— <c>fill_table</c> 的**唯一输入形态**（39 号 §11.2）。
///
/// <para><b>★ 为什么单独一个类型，而不是直接给 <see cref="OfficeFillRegion"/></b>：
/// <see cref="OfficeFillRegion"/> 是<b>层 1 的写入指令</b>（含 <c>StartRow</c>/<c>SheetName</c> 等
/// 「写到哪」的定位参数）。而 <c>fill_table</c> 的输入应该是**纯业务数据**（「有哪些列、每行什么值」），
/// 定位参数由 Skill 按 <c>FileKind</c> 自己分派（39 号 §11.3）。
/// 若让上游直接给 <see cref="OfficeFillRegion"/>，Word / Excel 两套定位参数就会**泄漏到规则配置里**
/// —— 这正是 38 号 §4.2 说的「泄漏点②」。</para>
///
/// <para><b>★ 列的顺序 = 写入顺序</b>：<see cref="Columns"/> 的次序决定每个单元格落在第几列。
/// ⛔ 不要靠 <see cref="Rows"/> 里字典的键序（字典无序 ⇒ 结果不可复现）。</para>
/// </summary>
public sealed class TablePayload
{
    /// <summary>
    /// Word 表格标签 —— 对应模板里的 <c>{{table:Tag}}</c>。
    /// <para>⚠️ 仅 <c>FileKind=word</c> 使用；Excel 侧忽略（39 号 §11.3）。</para>
    /// </summary>
    public string? TableTag { get; set; }

    /// <summary>列定义（★ 顺序即写入列序）</summary>
    public List<TableColumn> Columns { get; set; } = new();

    /// <summary>
    /// 数据行 —— 每行一个 <c>{列 FieldCode: 原始值}</c>。
    /// <para>⚠️ 某列缺键 ⇒ 该格<b>写空</b>（⛔ 不报错、⛔ 不左移 —— 左移会让整行错位且不报错）。</para>
    /// </summary>
    public List<Dictionary<string, string?>> Rows { get; set; } = new();

    /// <summary>
    /// 合计行（列 FieldCode → 原始值）。
    /// <para>⚠️ 本期<b>只承载数据</b>，是否输出合计行由模板决定（模板里有合计样板行才写）。</para>
    /// </summary>
    public Dictionary<string, string?> Totals { get; set; } = new();
}

/// <summary>
/// 表格的一列 —— 决定「这一列的值按什么类型落笔」。
///
/// <para><b>★ 为什么类型在列上而不是在值上</b>：同一列的所有单元格必须用同一种落笔方式
/// （Excel 的 <c>SetCellValue</c> 重载传错<b>不报错但结果错</b> —— 见 <see cref="FillValueKind"/>）。
/// 放在列上可保证「一列要么全是数字、要么全是文本」，不会出现同列混排。</para>
/// </summary>
public sealed class TableColumn
{
    /// <summary>列编码（= 该列对应的字段编码，如 <c>training_date</c>）</summary>
    public string FieldCode { get; set; } = string.Empty;

    /// <summary>列标题（仅用于报告可读性，⛔ 不写入文件 —— 表头由模板控制）</summary>
    public string? Title { get; set; }

    /// <summary>值类型：<c>text</c>/<c>number</c>/<c>date</c>/<c>bool</c>/<c>enum</c>（空 ⇒ <c>text</c>）</summary>
    public string? ValueKind { get; set; }

    /// <summary>格式串（.NET 方言，见 39 号 §十六）；空 ⇒ 保留单元格原有格式</summary>
    public string? NumberFormat { get; set; }
}

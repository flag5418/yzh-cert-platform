using CertPlatform.Shared.Entities.Doc;
using YZH.Core.DataBase.Interfaces;

namespace CertPlatform.Auditor.Services.Expert;

/// <summary>
/// ★ 缺口中文名解析器（2026-10-07 用户裁决 · 「关键信息补录」）
/// </summary>
///
/// <para><b>存在意义</b>：缺口界面此前直接暴露英文码（实测
/// <c>GapLabel = appendix_three_program_file_list</c> + <c>RuleCode</c> 是 GUID），
/// 用户逐字反馈「填写的信息不是中文，全是字段的英文和看不懂的编号……我作为开发者都很难理解，
/// 更不用说让审核员他们来理解如何执行」。</para>
///
/// <para><b>★ 中文名的唯一权威来源 = 文档提取规则页
/// （<c>/business/doc-extraction-rule</c>）定义的字段/表格</b>：</para>
/// <list type="bullet">
///   <item>字段中文名 → <see cref="DocFieldDef.FieldName"/>（<c>cert_doc_field_def</c>）</item>
///   <item>表格中文名 → <see cref="DocTableDef.TableName"/>（<c>cert_doc_table_def</c>）</item>
///   <item>表格列中文名 → <see cref="DocTableFieldDef.ColumnName"/>（<c>cert_doc_table_field_def</c>），
///         供「关键表格信息补录」表单渲染中文列头（⛔ 不再让用户手写 JSON）</item>
/// </list>
///
/// <para><b>⛔ 不要用 <c>cert_extraction_result.FieldName</c> 当主来源</b>：
/// 那一列只在「已存在提取结果行」时才有值，而<b>缺口场景恰恰是结果行不存在</b>
/// ⇒ 必然拿不到中文名（这正是 F3 的第一层根因）。</para>
///
/// <para><b>★ 兜底语义</b>：规则里确实没登记中文名时，返回
/// <see cref="UnnamedField"/> / <see cref="UnnamedTable"/>（人话），
/// ⛔ 不回退成英文码 —— 宁可说「未命名字段」，也不要甩一个 <c>appendix_three_...</c> 给审核员。</para>
///
/// <para><b>性能</b>：全部按 RuleCode 批量查库后建索引，⛔ 不逐条查（缺口可达数百条）。</para>
/// </remarks>
public class GapLabelResolver
{
    private readonly IDbOrm _db;

    public GapLabelResolver(IDbOrm db) => _db = db;

    /// <summary>规则里未登记中文名时的兜底文案（字段）</summary>
    public const string UnnamedField = "未命名字段";

    /// <summary>规则里未登记中文名时的兜底文案（表格）</summary>
    public const string UnnamedTable = "未命名表格";

    /// <summary>缺口类型常量（与 <c>CertExpertTaskDataGap.GapType</c> 同口径）</summary>
    public const string TypeField = "field";

    /// <summary>缺口类型常量（与 <c>CertExpertTaskDataGap.GapType</c> 同口径）</summary>
    public const string TypeTable = "table";

    /// <summary>解析结果的字典键（<c>gapType|ruleCode|code</c>）</summary>
    public static string Key(string gapType, string? ruleCode, string? code)
        => $"{gapType}|{ruleCode ?? "-"}|{code ?? "-"}";

    /// <summary>表格列（供前端渲染可编辑表格的中文列头）</summary>
    public sealed class TableColumn
    {
        /// <summary>列编码（英文驼峰，回写用）</summary>
        public string Code { get; init; } = "";

        /// <summary>列中文名（界面列头）</summary>
        public string Name { get; init; } = "";

        /// <summary><c>string</c> | <c>number</c> | <c>date</c></summary>
        public string DataType { get; init; } = "string";
    }

    /// <summary>
    /// 批量解析字段 / 表格的<b>中文展示名</b>。
    /// </summary>
    /// <param name="keys">三元组（<c>GapType</c>, <c>RuleCode</c>, <c>FieldCode|TableCode</c>）</param>
    /// <returns>字典：<see cref="Key"/> → 中文名（<b>恒非空</b>，缺失时为兜底文案）</returns>
    public async Task<Dictionary<string, string>> ResolveLabelsAsync(
        IEnumerable<(string GapType, string? RuleCode, string? Code)> keys)
    {
        var list = keys
            .Where(k => !string.IsNullOrWhiteSpace(k.GapType) && !string.IsNullOrWhiteSpace(k.Code))
            .Distinct()
            .ToList();

        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (list.Count == 0) return map;

        var fieldRuleCodes = list
            .Where(k => k.GapType == TypeField)
            .Select(k => k.RuleCode)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!)
            .Distinct()
            .ToList();

        var tableRuleCodes = list
            .Where(k => k.GapType == TypeTable)
            .Select(k => k.RuleCode)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!)
            .Distinct()
            .ToList();

        // ── 一次查库（按 RuleCode 收窄），⛔ 不逐条查 ──
        var fields = fieldRuleCodes.Count == 0
            ? new List<DocFieldDef>()
            : (await _db.GetListAsync<DocFieldDef>(x =>
                fieldRuleCodes.Contains(x.RuleCode) && !x.IsDeleted)).Data ?? new List<DocFieldDef>();

        var tables = tableRuleCodes.Count == 0
            ? new List<DocTableDef>()
            : (await _db.GetListAsync<DocTableDef>(x =>
                tableRuleCodes.Contains(x.RuleCode) && !x.IsDeleted)).Data ?? new List<DocTableDef>();

        var fieldIdx = fields
            .Where(x => !string.IsNullOrWhiteSpace(x.FieldCode))
            .GroupBy(x => $"{x.RuleCode}|{x.FieldCode}", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().FieldName, StringComparer.OrdinalIgnoreCase);

        var tableIdx = tables
            .Where(x => !string.IsNullOrWhiteSpace(x.TableCode))
            .GroupBy(x => $"{x.RuleCode}|{x.TableCode}", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().TableName, StringComparer.OrdinalIgnoreCase);

        foreach (var k in list)
        {
            var idxKey = $"{k.RuleCode}|{k.Code}";
            if (k.GapType == TypeField)
            {
                map[Key(k.GapType, k.RuleCode, k.Code)] =
                    fieldIdx.TryGetValue(idxKey, out var n) && !string.IsNullOrWhiteSpace(n)
                        ? n
                        : UnnamedField;
            }
            else if (k.GapType == TypeTable)
            {
                map[Key(k.GapType, k.RuleCode, k.Code)] =
                    tableIdx.TryGetValue(idxKey, out var n) && !string.IsNullOrWhiteSpace(n)
                        ? n
                        : UnnamedTable;
            }
        }

        return map;
    }

    /// <summary>
    /// 批量解析表格的<b>列定义</b>（中文列头 + 类型）。
    /// </summary>
    /// <remarks>
    /// 供「关键表格信息补录」Tab 渲染可编辑表格。
    /// ⛔ 不要让用户手写 JSON 数组（旧实现要求 <c>[{"列名":"值"}]</c>，实测极难用）。
    /// 列定义来源 = <c>cert_doc_table_field_def</c>（由文档提取规则页维护）。
    /// </remarks>
    /// <returns>字典：<c>TableCode</c> → 列定义（按 <c>Sort</c> 升序）</returns>
    public async Task<Dictionary<string, List<TableColumn>>> ResolveTableColumnsAsync(
        IEnumerable<string?> tableCodes)
    {
        var codes = tableCodes
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!)
            .Distinct()
            .ToList();

        var map = new Dictionary<string, List<TableColumn>>(StringComparer.OrdinalIgnoreCase);
        if (codes.Count == 0) return map;

        var cols = (await _db.GetListAsync<DocTableFieldDef>(x =>
            codes.Contains(x.TableCode) && !x.IsDeleted)).Data ?? new List<DocTableFieldDef>();

        foreach (var g in cols.GroupBy(x => x.TableCode, StringComparer.OrdinalIgnoreCase))
        {
            map[g.Key] = g
                .OrderBy(x => x.Sort)
                .Select(x => new TableColumn
                {
                    Code = x.ColumnCode,
                    // ⛔ 列名缺失时回退列编码，但这是「规则没配好」的信号，不静默隐藏
                    Name = string.IsNullOrWhiteSpace(x.ColumnName) ? x.ColumnCode : x.ColumnName,
                    DataType = string.IsNullOrWhiteSpace(x.DataType) ? "string" : x.DataType
                })
                .ToList();
        }

        return map;
    }
}

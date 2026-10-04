using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace CertPlatform.Shared.Office;

/// <summary>
/// <see cref="TablePayload"/> 的构造入口 —— 把「AI 返回的行数组」+「模板决定的列定义」
/// 组装成 <c>fill_table</c> 能吃的形态。
///
/// <para><b>★★ 为什么列<b>只能</b>来自输入，⛔ 不从 AI 返回里取</b>（39 号 §8.3）：
/// 列由<b>模板</b>决定（模板样板行写 <c>{{col:FieldCode}}</c>，由扫描器扫出来）。
/// 若让 AI 自己定列，它返回的列名与模板列<b>对不上</b>时，
/// <c>fill_table</c> 的列映射层会按「缺键 ⇒ 写空」处理 ——
/// <b>整列数据静默消失，而报告里只看得出「这列是空的」</b>。</para>
///
/// <para><b>★ 为什么容忍两种 AI 返回形态</b>：39 号 §12.4 的 <c>OutputSchema</c> 规定
/// <c>tables.{tag}</c> 是<b>行数组</b>，但 §8.2 的代码注释写的是
/// <c>{ columns: [...], rows: [...] }</c> —— 文档自相矛盾。本类<b>两种都收</b>：
/// 数组直接用；对象取 <c>rows</c> 并<b>丢弃其中可能存在的 <c>columns</c></b>（见上）。
/// 模型偶发不严格遵循 Schema 是常态，收窄输入只会让「什么都没填上」变成静默结果。</para>
///
/// <para>⚠️ <b>输入的行必须是已归一化的 CLR 对象</b>（<c>Dictionary</c> / 标量），
/// 不是 <see cref="JsonElement"/> —— 归一化由 <c>AiFillJsonReader.NormalizeJson</c> 完成。
/// 本类对 <see cref="JsonElement"/> 仍做了兜底（见 <see cref="CellText"/>），但那只是保险。</para>
/// </summary>
public static class TablePayloadFactory
{
    /// <summary>
    /// 解析列定义 JSON（= 锚点表 <c>ColumnsJson</c> 列 / <c>src_ai_table.columns_json</c> 入参）。
    ///
    /// <para>形态（39 号 §8.2）：<c>[{"field_code":"name","title":"姓名","kind":"text"}]</c>。
    /// ⚠️ 键名<b>三套写法都收</b>（<c>field_code</c> / <c>fieldCode</c> / <c>FieldCode</c>）——
    /// 该 JSON 可能由前端、锚点表、或手工填写三处产出，写死一种必然在某一处静默取不到列。</para>
    ///
    /// <para>⛔ <b>不抛异常</b>：解析失败 ⇒ 返回空列表，由调用方按「columns 不能为空」<c>Fail</c>
    /// —— 比抛一个 <see cref="JsonException"/> 到 SkillExecutor 上更容易定位。</para>
    /// </summary>
    public static List<TableColumn> ParseColumns(string? columnsJson)
    {
        var cols = new List<TableColumn>();
        if (string.IsNullOrWhiteSpace(columnsJson)) return cols;

        try
        {
            using var doc = JsonDocument.Parse(columnsJson!);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return cols;

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;

                var col = new TableColumn
                {
                    FieldCode = ReadString(item, "field_code", "fieldCode", "FieldCode") ?? string.Empty,
                    Title = ReadString(item, "title", "Title", "name", "Name"),
                    ValueKind = ReadString(item, "kind", "Kind", "value_kind", "valueKind", "ValueKind"),
                    NumberFormat = ReadString(item, "format", "Format", "number_format", "numberFormat", "NumberFormat"),
                };

                // 无 FieldCode 的列无法映射 ⇒ 丢弃（⛔ 不留一个永远写不进去的列）
                if (!string.IsNullOrWhiteSpace(col.FieldCode)) cols.Add(col);
            }
        }
        catch (JsonException)
        {
            // 见方法注释：返回空列表，由调用方 Fail
        }

        return cols;
    }

    /// <summary>
    /// 从「列定义 + 行集合」组装。
    /// </summary>
    /// <param name="tableTag">表格标签（Word 用；Excel 忽略）</param>
    /// <param name="columns">列定义（<b>顺序即写入列序</b>）。⛔ 空 ⇒ 返回空 payload（见类注释）</param>
    /// <param name="rows">行集合（每行一个字典；<c>null</c> 行被跳过）</param>
    /// <param name="maxRows">行数上限（<c>&lt;=0</c> = 不限制）。★ 保护：模型偶尔会返回几百行</param>
    public static TablePayload FromRows(
        string? tableTag,
        IReadOnlyList<TableColumn>? columns,
        IEnumerable<object?>? rows,
        int maxRows = 0)
    {
        var payload = new TablePayload
        {
            TableTag = string.IsNullOrWhiteSpace(tableTag) ? null : tableTag,
            Columns = columns == null ? new List<TableColumn>() : columns.ToList(),
        };

        // ⛔ 没有列定义 ⇒ 不做任何事（见类注释：不允许从行键反推列）
        if (payload.Columns.Count == 0 || rows == null) return payload;

        foreach (var row in rows)
        {
            var dict = AsDictionary(row);
            if (dict == null) continue;

            var line = new Dictionary<string, string?>(payload.Columns.Count);
            foreach (var col in payload.Columns)
            {
                // ★ 只取本列定义的键 ⇒ 行里多出来的键被忽略（⛔ 不写入文件）
                var raw = TryGet(dict, col.FieldCode);
                line[col.FieldCode] = CellText(raw);
            }

            payload.Rows.Add(line);
            if (maxRows > 0 && payload.Rows.Count >= maxRows) break;
        }

        return payload;
    }

    /// <summary>
    /// 从 AI 返回的<b>节点</b>组装（兼容「行数组」与 <c>{ rows: [...] }</c> 两种形态）。
    /// </summary>
    /// <param name="tableTag">表格标签</param>
    /// <param name="columns">列定义（来自模板扫描，⛔ 不是 AI 给的）</param>
    /// <param name="node"><c>tables.{table_tag}</c> 的值（<c>List&lt;object?&gt;</c> 或 <c>Dictionary</c>）</param>
    /// <param name="maxRows">行数上限（<c>&lt;=0</c> = 不限制）</param>
    public static TablePayload FromAiNode(
        string? tableTag,
        IReadOnlyList<TableColumn>? columns,
        object? node,
        int maxRows = 0)
    {
        var rows = ExtractRows(node);
        return FromRows(tableTag, columns, rows, maxRows);
    }

    /// <summary>从 AI 节点里取出「行集合」（数组直接用；对象取 <c>rows</c>）</summary>
    private static IEnumerable<object?>? ExtractRows(object? node)
    {
        switch (node)
        {
            case null:
                return null;

            // 形态①：行数组（39 号 §12.4 的 Schema）
            case List<object?> list:
                return list;

            // 形态②：{ columns: [...], rows: [...] }（39 号 §8.2 的注释写法）
            //   ⚠️ 这一条同时覆盖 Dictionary<string, object> —— 可空引用类型只是编译期注解，
            //      CLR 层面两者同类型 ⇒ 不必（也不能）再写一个 case。
            case IReadOnlyDictionary<string, object?> map:
                return TryGet(map, "rows") as List<object?>;

            default:
                return null;
        }
    }

    /// <summary>
    /// 单元格值 → 文本。
    ///
    /// <para>⚠️ 为什么统一转文本而不是保留原始类型：<see cref="TablePayload.Rows"/> 的契约是
    /// <c>string?</c>，<b>类型由 <see cref="TableColumn.ValueKind"/> 给</b> ——
    /// 由 <c>fill_table</c> 按列类型解析（39 号 §8.4 / <see cref="TableColumn"/> 注释）。
    /// 若在这里就转成 <c>double</c>，列类型与值类型会变成两个真相源。</para>
    /// </summary>
    private static string? CellText(object? raw) => raw switch
    {
        null => null,
        JsonElement je => je.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.String => je.GetString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            // 数字原样保留（⛔ 不走 double 中转，避免 1E+15 这类形态变化）
            JsonValueKind.Number => je.ToString(),
            _ => je.ToString(),
        },
        _ => FillValueFactory.ToText(raw),
    };

    /// <summary>
    /// 行 → 只读字典。
    /// <para>⚠️ <c>Dictionary&lt;string, object&gt;</c> 与 <c>Dictionary&lt;string, object?&gt;</c>
    /// 在 CLR 里是<b>同一个类型</b>（可空引用类型只是编译期注解），但泛型参数不变 ⇒
    /// 直接转换会产生 CS8619 警告。这里<b>统一成后者</b>（前者做一次浅拷贝，行数最多几百，成本可忽略）。</para>
    /// </summary>
    private static IReadOnlyDictionary<string, object?>? AsDictionary(object? row)
    {
        if (row is Dictionary<string, object?> d2) return d2;

        if (row is Dictionary<string, object> d1)
            return d1.ToDictionary(kv => kv.Key, kv => (object?)kv.Value);

        return null;
    }

    /// <summary>取键（精确 → 大小写不敏感回退，只回退一次；与 <c>AiFillJsonReader</c> 同口径）</summary>
    private static object? TryGet(IReadOnlyDictionary<string, object?> dict, string key)
    {
        if (dict.TryGetValue(key, out var v)) return v;

        foreach (var kv in dict)
            if (string.Equals(kv.Key, key, System.StringComparison.OrdinalIgnoreCase))
                return kv.Value;

        return null;
    }

    /// <summary>按候选键名依次读字符串（⛔ 非字符串类型不取 —— 列定义里不该出现数字/对象）</summary>
    private static string? ReadString(JsonElement obj, params string[] keys)
    {
        foreach (var k in keys)
            if (obj.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String)
                return v.GetString();

        return null;
    }
}

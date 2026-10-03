using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace CertPlatform.Shared.Office;

/// <summary>
/// <see cref="FillValue"/> 的**唯一**构造入口 —— 把「原始字符串 + 值类型 + 格式串」变成强类型值。
///
/// <para><b>★ 为什么必须有这个工厂</b>：<see cref="FillValueKind"/> 是**必须显式给**的
/// （见该枚举的注释：NPOI 的 <c>SetCellValue</c> 重载传错<b>不报错但结果错</b>）。
/// 8 个 Skill 各写一遍「字符串转 <see cref="FillValue"/>」必然漂移 ——
/// 典型症状是「A Skill 把 <c>"1,234"</c> 转成了数字、B Skill 转成了文本」，
/// 于是同一份模板里两个格子一个是可求和的数、一个是左对齐的串，<b>打开文件看不出异常</b>。</para>
///
/// <para><b>★ 失败语义</b>：⛔ <b>不抛异常</b>、⛔ <b>不静默降级</b>。
/// 声明 <c>number</c> 但值不是数字 ⇒ 返回失败 + 原因，由调用方
/// <c>SkillResult.Fail</c>（38 号 §4.2 已警告：静默降级成文本会让 Excel 数字变文本）。</para>
/// </summary>
public static class FillValueFactory
{
    /// <summary>支持的 <c>value_kind</c> 字面量（与 <c>cert_fill_param_def.ValueType</c> 值域一致）</summary>
    public static readonly string[] SupportedKinds = { "text", "number", "date", "bool", "enum" };

    /// <summary>
    /// 从原始字符串构造 <see cref="FillValue"/>。
    /// </summary>
    /// <param name="anchorCode">锚点键（与文档 <c>{{ }}</c> 内文本逐字一致）</param>
    /// <param name="raw">原始值（可为 null / 空 ⇒ 产出空值，不算失败）</param>
    /// <param name="valueKind">
    /// 值类型：<c>text</c> / <c>number</c> / <c>date</c> / <c>bool</c> / <c>enum</c>。
    /// <para>⚠️ <c>enum</c> 按 <b>文本</b>处理（<see cref="FillValueKind"/> 无 Enum 成员，
    /// 枚举在模板里就是选中的那个标签）。未知类型按 <c>text</c> 处理。</para>
    /// </param>
    /// <param name="numberFormat">
    /// 格式串（<b>.NET 方言</b>，见 39 号 §十六）。
    /// <para>⚠️ 统一存 .NET 方言、Excel 侧做一次转换 —— ⛔ 不要在这里存 Excel 方言，
    /// 否则 Word 侧用不了（Word 无「数字格式」概念，必须预格式化）。</para>
    /// </param>
    /// <returns><c>(Ok, Value, Error)</c>。<c>Ok=false</c> 时 <c>Error</c> 为原因，<c>Value</c> 为 null。</returns>
    public static (bool Ok, FillValue? Value, string? Error) TryCreate(
        string anchorCode,
        string? raw,
        string? valueKind,
        string? numberFormat = null)
    {
        if (string.IsNullOrWhiteSpace(anchorCode))
            return (false, null, "anchor_code 不能为空");

        var kind = (valueKind ?? "text").Trim().ToLowerInvariant();
        var text = raw ?? string.Empty;

        var value = new FillValue
        {
            AnchorCode = anchorCode,
            NumberFormat = string.IsNullOrWhiteSpace(numberFormat) ? null : numberFormat!.Trim(),
        };

        // ★ 空值不是失败：空 = 「取到了值，但值是空的」，与「没取到值」是两回事
        //   （后者由调用方 Fail 表达）。类型仍按声明给，便于下游按类型落笔（写空）。
        if (string.IsNullOrWhiteSpace(text))
        {
            ApplyKind(value, kind);
            return (true, value, null);
        }

        switch (kind)
        {
            case "number":
            {
                if (!TryParseNumber(text, out var n))
                    return (false, null, $"值「{Truncate(text)}」不是合法数字（声明为 number）");

                value.Kind = FillValueKind.Number;
                value.Number = n;
                return (true, value, null);
            }

            case "date":
            {
                if (!TryParseDate(text, out var d))
                    return (false, null, $"值「{Truncate(text)}」不是合法日期（声明为 date）");

                value.Kind = FillValueKind.Date;
                value.Date = d;
                return (true, value, null);
            }

            case "bool":
            {
                if (!TryParseBool(text, out var b))
                    return (false, null, $"值「{Truncate(text)}」不是合法布尔（声明为 bool，接受 是/否/true/false/1/0）");

                value.Kind = FillValueKind.Bool;
                value.Bool = b;
                return (true, value, null);
            }

            default: // text / enum / 未知
            {
                value.Kind = FillValueKind.Text;
                value.Text = text;
                return (true, value, null);
            }
        }
    }

    /// <summary>空值时的类型落笔（值仍为空，但类型按声明给）</summary>
    private static void ApplyKind(FillValue value, string kind) => value.Kind = kind switch
    {
        "number" => FillValueKind.Number,
        "date" => FillValueKind.Date,
        "bool" => FillValueKind.Bool,
        _ => FillValueKind.Text,
    };

    /// <summary>
    /// 数字解析 —— <b>InvariantCulture 优先，回退当前区域性</b>。
    ///
    /// <para>为什么两步：模板/参数值多由系统写入（<c>1234.5</c>），但企业人工填的可能是
    /// <c>1,234.5</c>（千分位）或本地写法。先按不变区域性严格解析（避免
    /// 「<c>1.234</c> 在德语区被读成 1234」这类静默错值），失败再按当前区域性兜底。</para>
    /// </summary>
    private static bool TryParseNumber(string text, out double number)
    {
        const NumberStyles styles = NumberStyles.Number | NumberStyles.AllowExponent;

        if (double.TryParse(text, styles, CultureInfo.InvariantCulture, out number))
            return true;

        return double.TryParse(text, styles, CultureInfo.CurrentCulture, out number);
    }

    /// <summary>
    /// 日期解析 —— 常见格式优先（<b>显式顺序</b>，避免区域性歧义）。
    ///
    /// <para>⚠️ 刻意<b>不</b>先试 <c>DateTime.TryParse</c> 的宽松模式：<c>03/04/2026</c>
    /// 在美式/英式下是<b>不同日期</b>，宽松解析会让结果随服务器区域性变化（不可复现）。
    /// 故先按明确的 ISO / 中文 / 斜杠顺序匹配。</para>
    /// </summary>
    private static bool TryParseDate(string text, out DateTime date)
    {
        string[] formats =
        {
            "yyyy-MM-dd", "yyyy/M/d", "yyyy.M.d",
            "yyyy年M月d日", "yyyy年MM月dd日",
            "yyyy-MM-dd HH:mm:ss", "yyyy/M/d HH:mm:ss",
            "yyyyMMdd",
        };

        if (DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out date))
            return true;

        // 兜底：不变区域性的通用解析（仍不依赖服务器区域性）
        return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    /// <summary>布尔解析（中文与英文写法都接受）</summary>
    private static bool TryParseBool(string text, out bool value)
    {
        switch (text.Trim().ToLowerInvariant())
        {
            case "是": case "true": case "1": case "y": case "yes": case "t":
                value = true; return true;
            case "否": case "false": case "0": case "n": case "no": case "f":
                value = false; return true;
            default:
                value = false; return false;
        }
    }

    private static string Truncate(string s) => s.Length <= 40 ? s : s[..40] + "…";

    // ────────────────────────────────────────────────────────────────────────
    // Coerce —— fill_cell 的入参解包口（39 号 §10.2）
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 把「任意形态的值」归一成 <see cref="FillValue"/>。
    ///
    /// <para><b>★ 为什么需要它</b>：<c>fill_cell</c> 的 <c>values</c> 是
    /// <c>IDictionary&lt;string, object&gt;</c>，同一个字典可能有<b>两种来源</b>：
    /// ① <b>编排器直连</b>（放的是真的 <see cref="FillValue"/> 实例）；
    /// ② <b>声明式配置 / JSON 链</b>（放的是「值描述」字典或 <see cref="JsonElement"/>）。
    /// 两种都要能收 —— 否则「单项试跑」和「规则驱动」会各写一套解包逻辑，必然漂移。</para>
    ///
    /// <para><b>★ 支持的形态</b>：<see cref="FillValue"/>（透传）｜<c>string</c>｜
    /// <c>bool</c> / 整数 / 浮点 / <c>decimal</c>｜<see cref="System.DateTime"/> /
    /// <see cref="System.DateOnly"/>｜值描述字典
    /// <c>{ "value": ..., "kind": ..., "format": ... }</c>｜<see cref="JsonElement"/>。</para>
    ///
    /// <para><b>⛔ 不认识的形态返回 <c>null</c></b>（<b>不猜、不 ToString 兜底</b>）——
    /// 调用方必须 <c>SkillResult.Fail</c>。理由：把任意对象 <c>ToString()</c> 成文本
    /// 会让 Excel 数字静默变文本（见 <see cref="FillValueKind"/> 注释），
    /// <b>不报错但结果错</b>，是最难查的一类缺陷。</para>
    /// </summary>
    /// <param name="anchorCode">锚点键（用于补全 <see cref="FillValue.AnchorCode"/>）</param>
    /// <param name="raw">原始值</param>
    /// <returns>归一后的值；无法识别返回 <c>null</c></returns>
    public static FillValue? Coerce(string anchorCode, object? raw)
    {
        if (string.IsNullOrWhiteSpace(anchorCode) || raw == null)
            return null;

        // ① 已是 FillValue（编排器直连路径）⇒ 透传，只补锚点
        if (raw is FillValue direct)
        {
            if (string.IsNullOrWhiteSpace(direct.AnchorCode))
                direct.AnchorCode = anchorCode;
            return direct;
        }

        // ② JsonElement（JSON 链）⇒ 先降到 CLR 再递归，避免每种类型写两遍
        if (raw is JsonElement je)
            return Coerce(anchorCode, FromJsonElement(je));

        // ③ 值描述字典：{ "value": ..., "kind": "number", "format": "#,##0.00" }
        if (raw is IDictionary<string, object> map)
            return FromMap(anchorCode, map);

        // ④ 基础类型 —— ★ 类型由 CLR **显式**给出，不是字符串猜测
        switch (raw)
        {
            case string s:
                return Single(anchorCode, s, "text");
            case bool b:
                return Single(anchorCode, b ? "true" : "false", "bool");
            case System.DateTime dt:
                return Single(anchorCode, dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), "date");
            case System.DateOnly d:
                return Single(anchorCode, d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), "date");
            case int or long or short or byte or sbyte or uint or ulong or ushort
                 or double or float or decimal:
                return Single(anchorCode,
                    System.Convert.ToString(raw, CultureInfo.InvariantCulture), "number");
        }

        // ⑤ ⛔ 不认识 ⇒ 不猜
        return null;
    }

    /// <summary>值描述字典 → <see cref="FillValue"/>（键名兼容 camelCase / snake_case / PascalCase）</summary>
    private static FillValue? FromMap(string anchorCode, IDictionary<string, object> map)
    {
        var value = Pick(map, "value", "Value", "raw", "Raw");
        var kind = Pick(map, "kind", "Kind", "value_kind", "valueKind", "ValueKind");
        var format = Pick(map, "format", "Format", "number_format", "numberFormat", "NumberFormat");

        var (ok, fv, _) = TryCreate(anchorCode, value?.ToString(), kind?.ToString(), format?.ToString());
        return ok ? fv : null;
    }

    private static object? Pick(IDictionary<string, object> map, params string[] keys)
    {
        foreach (var k in keys)
            if (map.TryGetValue(k, out var v))
                return v;
        return null;
    }

    private static FillValue? Single(string anchorCode, string? raw, string kind)
    {
        var (ok, value, _) = TryCreate(anchorCode, raw, kind);
        return ok ? value : null;
    }

    /// <summary><see cref="JsonElement"/> → CLR（对象 ⇒ 字典，数组 ⇒ 列表）</summary>
    private static object? FromJsonElement(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        JsonValueKind.String => e.GetString(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number => e.TryGetInt64(out var l) ? l : e.GetDouble(),
        JsonValueKind.Object => e.EnumerateObject()
            .ToDictionary(p => p.Name, p => FromJsonElement(p.Value)),
        JsonValueKind.Array => e.EnumerateArray().Select(FromJsonElement).ToList(),
        _ => e.ToString(),
    };
}

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace YZH.Core.Stand.Converters;

/// <summary>
///     全局日期时间 JSON 序列化器 —— 架构统一（2026-10-19）。
///
///     <para>
///         <b>写出（Write）</b>：所有 <see cref="DateTime" />（含 <c>DateTime?</c>，
///         STJ 对可空类型复用本转换器）**一律** <c>yyyy-MM-dd HH:mm:ss</c>
///         （本地时间，无 <c>T</c>、无毫秒、无时区偏移）。
///     </para>
///     <para>
///         <b>为什么</b>：
///         ① 前端 <c>el-date-picker</c> 的 <c>value-format="YYYY-MM-DD HH:mm:ss"</c>
///            ⇒ 前端**传参**本就是该格式；后端过去**返回** ISO（带 <c>T</c>），
///            同一字段进出两种字符串，逼出 <c>replace('T',' ').slice(0,19)</c> 之类 workaround。
///         ② 不带偏移 ⇒ 前端 <c>new Date(s)</c> 按**本地**解析，与**铁律十**
///            （业务时间一律 <c>DateTime.Now</c>，Asia/Shanghai）同源，
///            杜绝「UTC 值整体早 8 小时」的静默错位。
///     </para>
///     <para>
///         <b>读入（Read）</b>：兼容 <c>yyyy-MM-dd HH:mm:ss</c>、ISO-8601
///         （<c>T</c> / 毫秒 / <c>Z</c> / <c>±hh:mm</c>）、纯日期 <c>yyyy-MM-dd</c>
///         —— 保证既有请求绑定（前端表单 / 历史调用）不回归。
///     </para>
///     <para>
///         ⛔ 内部 JSON（<c>SerializationExtensions.ToJson</c>，如落库 JSON 列）
///         **不走本转换器**，仍为 ISO —— 那是给存储的，不是给用户看的。
///     </para>
///     <para>安装入口见 <c>Extensions/JsonDateTimeExtensions.ApplyYzhDateTimeFormat</c>。</para>
/// </summary>
public sealed class YzhDateTimeJsonConverter : JsonConverter<DateTime>
{
    /// <summary>统一输出格式（架构唯一值，改这里即全局生效）。</summary>
    public const string Format = "yyyy-MM-dd HH:mm:ss";

    /// <summary>读入时优先尝试的精确格式（含历史 ISO 变体）。</summary>
    private static readonly string[] ReadFormats =
    {
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-dd HH:mm:ss.FFFFFFF",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss.FFFFFFF",
        "yyyy-MM-ddTHH:mm:ssZ",
        "yyyy-MM-ddTHH:mm:ss.FFFFFFFZ",
        "yyyy-MM-ddTHH:mm:sszzz",
        "yyyy-MM-ddTHH:mm:ss.FFFFFFFzzz",
        "yyyy-MM-dd",
        "yyyy/MM/dd",
    };

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException(
                $"日期时间字段期望字符串（{Format} 或 ISO-8601），实际为 {reader.TokenType}。");
        }

        var s = reader.GetString();
        if (string.IsNullOrWhiteSpace(s)) return default;

        // 精确匹配优先（RoundtripKind 保留 Z / 偏移的语义，与 STJ 原生一致）
        if (DateTime.TryParseExact(
                s,
                ReadFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.RoundtripKind,
                out var exact))
        {
            return exact;
        }

        // 兜底：宽松解析（InvariantCulture，避免跟随服务器区域设置漂移）
        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var loose))
        {
            return loose;
        }

        throw new JsonException($"无法解析日期时间：\"{s}\"（期望 {Format} 或 ISO-8601）。");
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString(Format, CultureInfo.InvariantCulture));
}

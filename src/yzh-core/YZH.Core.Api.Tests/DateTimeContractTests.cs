using System.Text.Json;
using Xunit;
using YZH.Core.Stand.Converters;
using YZH.Core.Stand.Extensions;

namespace YZH.Core.Api.Tests;

/// <summary>
///     日期时间序列化契约测试（契约表 §3.7 · 架构统一 2026-10-19）。
///
///     <list type="bullet">
///         <item>写出：<c>DateTime</c> / <c>DateTime?</c> 一律 <c>yyyy-MM-dd HH:mm:ss</c>（无 <c>T</c> / 毫秒 / 偏移）</item>
///         <item>读入：兼容空格分隔、ISO-8601（<c>T</c> / 毫秒 / <c>Z</c> / <c>±hh:mm</c>）、纯日期</item>
///         <item>安装：<c>ApplyYzhDateTimeFormat</c> 幂等（两处落点都调不重复注册）</item>
///         <item>不越界：<c>DateTimeOffset</c> / 内部 <c>ToJson</c> 不走本转换器</item>
///     </list>
/// </summary>
public class DateTimeContractTests
{
    /// <summary>与 MVC 管线同构的最小选项集（PascalCase + 全局日期转换器）。</summary>
    private static JsonSerializerOptions Options()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = null };
        return options.ApplyYzhDateTimeFormat();
    }

    private sealed record Row(string Name, DateTime CreateTime, DateTime? UpdateTime);

    // ── 写出 ────────────────────────────────────────────────

    [Fact]
    public void Write_DateTime_UsesUnifiedFormat()
    {
        var json = JsonSerializer.Serialize(new Row("a", new DateTime(2026, 10, 19, 9, 35, 14), null), Options());

        Assert.Contains("\"CreateTime\":\"2026-10-19 09:35:14\"", json);
    }

    [Fact]
    public void Write_NullableDateTime_NullIsWrittenAsNull_NotNullUnified()
    {
        var json = JsonSerializer.Serialize(new Row("a", new DateTime(2026, 1, 2, 3, 4, 5), null), Options());
        Assert.Contains("\"UpdateTime\":null", json);

        var json2 = JsonSerializer.Serialize(
            new Row("a", new DateTime(2026, 1, 2, 3, 4, 5), new DateTime(2026, 12, 31, 23, 59, 59)), Options());
        Assert.Contains("\"UpdateTime\":\"2026-12-31 23:59:59\"", json2);
    }

    [Fact]
    public void Write_HasNoT_NoMilliseconds_NoOffset()
    {
        var json = JsonSerializer.Serialize(
            new Row("a", new DateTime(2026, 10, 19, 9, 35, 14, 979), null), Options());

        Assert.Contains("\"CreateTime\":\"2026-10-19 09:35:14\"", json);
        Assert.DoesNotContain("T09:35", json);
        Assert.DoesNotContain(".979", json);
        Assert.DoesNotContain("+08:00", json);
    }

    // ── 读入（不回归既有调用方） ───────────────────────────

    [Theory]
    [InlineData("2026-10-19 09:35:14")]      // 新格式（前端传参）
    [InlineData("2026-10-19T09:35:14")]      // 历史 ISO（本地）
    [InlineData("2026-10-19T09:35:14.979")]  // 历史 ISO + 毫秒
    public void Read_DateTimeFormats_RoundTripsClockValue(string input)
    {
        var json = $"{{\"Name\":\"a\",\"CreateTime\":\"{input}\",\"UpdateTime\":null}}";
        var row = JsonSerializer.Deserialize<Row>(json, Options());

        Assert.NotNull(row);
        Assert.Equal(new DateTime(2026, 10, 19), row!.CreateTime.Date);
        Assert.Equal(9, row.CreateTime.Hour);
        Assert.Equal(35, row.CreateTime.Minute);
        Assert.Equal(14, row.CreateTime.Second);
    }

    [Theory]
    [InlineData("2026-10-19")]   // 纯日期（date picker 的 value-format）
    [InlineData("2026/10/19")]   // 宽松兜底
    public void Read_DateOnlyFormats_ResolveToMidnight(string input)
    {
        var json = $"{{\"Name\":\"a\",\"CreateTime\":\"{input}\",\"UpdateTime\":null}}";
        var row = JsonSerializer.Deserialize<Row>(json, Options());

        Assert.NotNull(row);
        Assert.Equal(new DateTime(2026, 10, 19), row!.CreateTime);
    }

    [Fact]
    public void Read_UtcSuffix_PreservesUtcKind()
    {
        var json = "{\"Name\":\"a\",\"CreateTime\":\"2026-10-19T01:25:33Z\",\"UpdateTime\":null}";
        var row = JsonSerializer.Deserialize<Row>(json, Options());

        Assert.Equal(DateTimeKind.Utc, row!.CreateTime.Kind);
        Assert.Equal(1, row.CreateTime.Hour);
    }

    [Fact]
    public void Read_UnparseableString_ThrowsJsonException()
    {
        var json = "{\"Name\":\"a\",\"CreateTime\":\"不是日期\",\"UpdateTime\":null}";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Row>(json, Options()));
    }

    // ── 安装幂等 ───────────────────────────────────────────

    [Fact]
    public void ApplyYzhDateTimeFormat_IsIdempotent()
    {
        var options = new JsonSerializerOptions();

        options.ApplyYzhDateTimeFormat();
        options.ApplyYzhDateTimeFormat();

        Assert.Single(options.Converters, c => c is YzhDateTimeJsonConverter);
    }

    // ── 边界：不越界 ───────────────────────────────────────

    [Fact]
    public void Converter_HandlesDateTime()
    {
        Assert.True(new YzhDateTimeJsonConverter().CanConvert(typeof(DateTime)));
    }

    [Fact]
    public void Write_NullableProperty_UsesConverter()
    {
        // STJ 用 NullableConverter<T> 包住本转换器 ⇒ `DateTime?` 属性同样走统一格式
        var json = JsonSerializer.Serialize(
            new Row("a", new DateTime(2026, 1, 2, 3, 4, 5), new DateTime(2026, 12, 31, 23, 59, 59)), Options());

        Assert.Contains("\"UpdateTime\":\"2026-12-31 23:59:59\"", json);
    }

    [Fact]
    public void DateTimeOffset_IsNotAffected()
    {
        // 当前全仓无 DateTimeOffset 业务列 ⇒ 有意不实现；确认不会误改其 ISO 输出
        var options = Options();
        var json = JsonSerializer.Serialize(
            new { At = new DateTimeOffset(2026, 10, 19, 9, 35, 14, TimeSpan.FromHours(8)) }, options);

        Assert.Contains("2026-10-19T09:35:14+08:00", json);
    }
}

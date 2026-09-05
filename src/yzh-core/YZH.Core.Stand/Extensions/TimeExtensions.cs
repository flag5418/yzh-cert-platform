using System.Globalization;

namespace YZH.Core.Stand.Extensions;

/// <summary>时间扩展工具</summary>
public static class TimeExtensions
{
    private static readonly DateTime UnixEpoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>ToUnixTimestamp - DateTime 转 Unix 时间戳(秒)</summary>
    public static long ToUnixTimestamp(this DateTime dt) =>
        new DateTimeOffset(dt).ToUnixTimeSeconds();

    /// <summary>FromUnixTimestamp - Unix 时间戳转 DateTime（本地时间）</summary>
    public static DateTime FromUnixTimestamp(long timestamp) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime, TimeZoneInfo.Local);



    /// <summary>ToLocalTime - UTC 转本地时间</summary>
    public static DateTime? ToLocalTime(this DateTime? dt) =>
        dt.HasValue ? TimeZoneInfo.ConvertTimeFromUtc(dt.Value, TimeZoneInfo.Local) : null;

    /// <summary>ToLocalTime - DateTime 转本地时间</summary>
    public static DateTime ToLocalTime(this DateTime dt) =>
        TimeZoneInfo.ConvertTimeFromUtc(dt, TimeZoneInfo.Local);

    /// <summary>StartOfDay - 当天开始</summary>
    public static DateTime StartOfDay(this DateTime dt) => dt.Date;

    /// <summary>EndOfDay - 当天结束</summary>
    public static DateTime EndOfDay(this DateTime dt) => dt.Date.AddDays(1).AddTicks(-1);

    /// <summary>StartOfMonth - 月初</summary>
    public static DateTime StartOfMonth(this DateTime dt) => new(dt.Year, dt.Month, 1);

    /// <summary>EndOfMonth - 月末</summary>
    public static DateTime EndOfMonth(this DateTime dt) =>
        new(dt.Year, dt.Month, DateTime.DaysInMonth(dt.Year, dt.Month), 23, 59, 59);

    /// <summary>Humanize - 人性化时间差显示</summary>
    public static string ToRelativeTime(this DateTime dt)
    {
        var diff = DateTime.UtcNow - dt;
        return diff.TotalMinutes < 1 ? "刚刚"
            : diff.TotalMinutes < 60 ? $"{(int)diff.TotalMinutes}分钟前"
            : diff.TotalHours < 24 ? $"{(int)diff.TotalHours}小时前"
            : diff.TotalDays < 30 ? $"{(int)diff.TotalDays}天前"
            : diff.TotalDays < 365 ? $"{(int)(diff.TotalDays / 30)}个月前"
            : $"{(int)(diff.TotalDays / 365)}年前";
    }

    /// <summary>FormatDate - 格式化日期</summary>
    public static string ToDateString(this DateTime? dt, string format = "yyyy-MM-dd") =>
        dt?.ToString(format) ?? string.Empty;

    /// <summary>FormatDateTime - 格式化日期时间</summary>
    public static string ToDateTimeString(this DateTime? dt, string format = "yyyy-MM-dd HH:mm:ss") =>
        dt?.ToString(format) ?? string.Empty;

    /// <summary>AgeByBirthday - 根据生日计算年龄</summary>
    public static int AgeByBirthday(this DateTime birthday)
    {
        var today = DateTime.Today;
        var age = today.Year - birthday.Year;
        if (birthday.Date.AddYears(age) > today) age--;
        return age;
    }
}

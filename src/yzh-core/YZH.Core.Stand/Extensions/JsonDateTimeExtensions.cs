using System.Text.Json;
using YZH.Core.Stand.Converters;

namespace YZH.Core.Stand.Extensions;

/// <summary>
///     全局日期时间格式「架构统一」安装器（2026-10-19）：
///     把 <see cref="YzhDateTimeJsonConverter" /> 装进
///     <see cref="JsonSerializerOptions.Converters" />，使所有 <c>DateTime</c> 输出
///     统一为 <c>yyyy-MM-dd HH:mm:ss</c>。
///
///     必须在 <c>AddJsonOptions</c> / <c>Configure&lt;JsonOptions&gt;</c> 回调内、
///     序列化选项首次被使用之前调用。
/// </summary>
public static class JsonDateTimeExtensions
{
    /// <summary>
    ///     安装日期时间格式转换器。★ 幂等：框架层 JSON 配置有 **2 个落点**
    ///     （<c>Program.cs</c> / <c>YzhWebBuilder.cs</c>），两处都调也**不会重复注册**。
    /// </summary>
    public static JsonSerializerOptions ApplyYzhDateTimeFormat(this JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        foreach (var converter in options.Converters)
        {
            if (converter is YzhDateTimeJsonConverter) return options;
        }

        options.Converters.Add(new YzhDateTimeJsonConverter());
        return options;
    }
}

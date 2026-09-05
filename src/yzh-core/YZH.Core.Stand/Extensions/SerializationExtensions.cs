using System.Text.Encodings.Web;
using System.Text.Json;

namespace YZH.Core.Stand.Extensions;

/// <summary>序列化扩展</summary>
public static class SerializationExtensions
{
    private static readonly JsonSerializerOptions DefaultOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };

    private static readonly JsonSerializerOptions IndentedOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true
    };

    /// <summary>ToJson - 序列化为 JSON 字符串</summary>
    public static string ToJson(this object? obj, bool indented = false) =>
        JsonSerializer.Serialize(obj, indented ? IndentedOptions : DefaultOptions);

    /// <summary>FromJson - 反序列化</summary>
    public static T? FromJson<T>(this string json) =>
        JsonSerializer.Deserialize<T>(json, DefaultOptions);

    /// <summary>FromJson - 反序列化（泛型）</summary>
    public static object? FromJson(this string json, Type returnType) =>
        JsonSerializer.Deserialize(json, returnType, DefaultOptions);

    /// <summary>DeepClone - 深拷贝（通过 JSON 序列化）</summary>
    public static T? DeepClone<T>(this T? obj)
    {
        if (obj is null) return default;
        var json = obj.ToJson();
        return json.FromJson<T>();
    }

    /// <summary>TryFromJson - 安全反序列化</summary>
    public static bool TryFromJson<T>(this string json, out T? result)
    {
        try
        {
            result = json.FromJson<T>();
            return true;
        }
        catch
        {
            result = default;
            return false;
        }
    }
}

using System.Text.Json.Serialization;

namespace YZH.Core.Stand.Models;

/// <summary>
///     原子操作结果包装（泛型）
///     替代元组 (T?, string?)，提供类型安全和链式调用能力
/// </summary>
public class Result<T>
{
    /// <summary>操作数据</summary>
    [JsonPropertyName("data")]
    public T? Data { get; init; }

    /// <summary>错误信息（失败时）</summary>
    [JsonPropertyName("error")]
    public string? Error { get; init; }

    /// <summary>错误码（可选）</summary>
    [JsonPropertyName("code")]
    public int? Code { get; init; }

    /// <summary>是否成功</summary>
    [JsonPropertyName("success")]
    public bool Success => Error == null;

    /// <summary>操作时间</summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    // ==================== 工厂方法 ====================

    public static Result<T> Ok(T data) => new() { Data = data };

    public static Result<T> Fail(string error, int? code = null) =>
        new() { Error = error, Code = code };

    public static Result<T> Fail(Exception ex, int? code = null) =>
        new() { Error = ex.Message, Code = code ?? 500 };

    // ==================== 链式调用 ====================

    /// <summary>映射数据（成功时转换，失败时透传错误）</summary>
    public Result<TResult> Map<TResult>(Func<T, TResult> selector)
    {
        if (!Success) return Result<TResult>.Fail(Error, Code);
        try
        {
            return Result<TResult>.Ok(selector(Data!));
        }
        catch (Exception ex)
        {
            return Result<TResult>.Fail(ex);
        }
    }

    /// <summary>绑定数据（成功时回调，失败时透传错误）</summary>
    public Result<TResult> Bind<TResult>(Func<T, Result<TResult>> selector)
    {
        if (!Success) return Result<TResult>.Fail(Error, Code);
        return selector(Data!);
    }

    /// <summary>失败时回退</summary>
    public Result<T> OrElse(T fallback) => Success ? this : Result<T>.Ok(fallback);

    /// <summary>失败时回退（工厂方法）</summary>
    public Result<T> OrElse(Func<Result<T>> fallbackFactory) =>
        Success ? this : fallbackFactory();

    /// <summary>执行副作用（无论成功失败）</summary>
    public Result<T> Do(Action<T?> action)
    {
        action(Data);
        return this;
    }

    /// <summary>转换为 ApiResponse</summary>
    public ApiResponse<T> ToApiResponse(string successMessage = "操作成功")
    {
        if (Success)
            return ApiResponse<T>.Ok(Data!, successMessage);
        return ApiResponse<T>.Fail(Error!, Code ?? 400);
    }
}

/// <summary>
///     无数据结果包装（用于 void 操作）
/// </summary>
public class Result
{
    /// <summary>错误信息（失败时）</summary>
    [JsonPropertyName("error")]
    public string? Error { get; init; }

    /// <summary>错误码（可选）</summary>
    [JsonPropertyName("code")]
    public int? Code { get; init; }

    /// <summary>是否成功</summary>
    [JsonPropertyName("success")]
    public bool Success => Error == null;

    /// <summary>操作时间</summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    // ==================== 工厂方法 ====================

    public static Result Ok() => new();

    public static Result Fail(string error, int? code = null) =>
        new() { Error = error, Code = code };

    public static Result Fail(Exception ex, int? code = null) =>
        new() { Error = ex.Message, Code = code ?? 500 };

    /// <summary>转换为 ApiResponse</summary>
    public ApiResponse ToApiResponse(string successMessage = "操作成功")
    {
        if (Success)
            return ApiResponse.Ok(successMessage);
        return ApiResponse.Fail(Error!, Code ?? 400);
    }
}

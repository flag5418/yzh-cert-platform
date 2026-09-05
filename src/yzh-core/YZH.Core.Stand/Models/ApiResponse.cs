using System.Text.Json.Serialization;

namespace YZH.Core.Stand.Models;

/// <summary>API 统一响应</summary>
public class ApiResponse<T>
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("data")]
    public T? Data { get; init; }

    [JsonPropertyName("code")]
    public int Code { get; init; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; } = DateTime.Now;

    public static ApiResponse<T> Ok(string message = "操作成功") =>
        new() { Success = true, Code = 200, Message = message };

    public static ApiResponse<T> Ok(T data, string message = "操作成功") =>
        new() { Success = true, Code = 200, Data = data, Message = message };

    public static ApiResponse<T> Fail(string message, int code = 400) =>
        new() { Success = false, Code = code, Message = message };

    public static ApiResponse<T> Error(string message, int code = 500) =>
        new() { Success = false, Code = code, Message = message };
}

/// <summary>无数据 API 响应</summary>
public class ApiResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; } = DateTime.Now;

    public static ApiResponse Ok(string message = "操作成功") =>
        new() { Success = true, Message = message };

    public static ApiResponse Fail(string message, int code = 400) =>
        new() { Success = false, Message = message };

    public static ApiResponse Error(string message) =>
        new() { Success = false, Message = message };
}

using System.Text.Json.Serialization;

namespace YZH.Core.Stand.Models.Result;

/// <summary>
/// API 统一响应（信封唯一权威：docs/10-YZH架构/22-接口返回规范-V1.md）
///
/// ★ 2026-09-25 起构造封闭：只有本类的静态工厂能创建实例。
///   好处是三条不变量由工厂统一保证，调用点不可能拼错：
///     ① success:false ⇒ Err 非空
///     ② success:true  ⇒ Err 为空
///     ③ success:false ⇒ Message 为空（错误文本只从 err 出，前端单一读取点）
///   已知唯一 `new ApiResponse` 站点（GlobalExceptionFilter）已改用工厂（P0-b）。
///
/// 语义分工：
///   Message = 成功提示（给人看的"好消息"）
///   Err     = 失败原因（给人看的"坏消息"，失败时唯一错误出口）
/// </summary>
public class ApiResponse<T>
{
    /// <summary>业务结果的唯一判据（前端只读它）</summary>
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    /// <summary>成功提示；失败时恒为 ""</summary>
    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    /// <summary>失败原因；成功时恒为 ""，失败时必非空</summary>
    [JsonPropertyName("err")]
    public string Err { get; init; } = string.Empty;

    [JsonPropertyName("data")]
    public T? Data { get; init; }

    /// <summary>与 HTTP 解耦的业务码（200/400/500）；前端禁止读</summary>
    [JsonPropertyName("code")]
    public int Code { get; init; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; } = DateTime.Now;

    /// <summary>封闭构造：请用下方静态工厂创建</summary>
    private ApiResponse() { }

    /// <summary>成功（无载荷）</summary>
    public static ApiResponse<T> Ok(string message = "操作成功") =>
        new() { Success = true, Code = 200, Message = message, Err = string.Empty };

    /// <summary>成功（有载荷）</summary>
    public static ApiResponse<T> Ok(T data, string message = "操作成功") =>
        new() { Success = true, Code = 200, Data = data, Message = message, Err = string.Empty };

    /// <summary>业务失败（HTTP 恒 200，code=400；P1 起业务拒绝统一入口）</summary>
    public static ApiResponse<T> BizFail(string err, int code = 400) =>
        Fail(err, code);

    /// <summary>业务失败（HTTP 由调用方决定；P1 起一律 HTTP 200）</summary>
    public static ApiResponse<T> Fail(string err, int code = 400) =>
        new() { Success = false, Code = code, Message = string.Empty, Err = string.IsNullOrEmpty(err) ? "操作失败" : err };

    /// <summary>系统异常（code=500）</summary>
    public static ApiResponse<T> Error(string err, int code = 500) =>
        new() { Success = false, Code = code, Message = string.Empty, Err = string.IsNullOrEmpty(err) ? "操作失败，请联系管理员" : err };

    /// <summary>
    /// 异常出口：仅供 <c>GlobalExceptionFilter</c> 使用。
    /// 业务异常原文入 err（不再脱敏）；系统异常由过滤器先脱敏再传入。
    /// </summary>
    public static ApiResponse<T> FromError(string err, int code, T? data = default) =>
        new()
        {
            Success = false,
            Code = code,
            Message = string.Empty,
            Err = string.IsNullOrEmpty(err) ? "操作失败，请联系管理员" : err,
            Data = data,
        };

    /// <summary>
    /// 从 IOperationResult&lt;T&gt; 自动转换（基类便捷方法）
    /// </summary>
    public static ApiResponse<T> FromOperationResult(IOperationResult<T> result)
    {
        return result.Success
            ? Ok(result.Data!, result.Message)
            : Fail(result.Message);
    }
}

/// <summary>无数据 API 响应（与泛型版同构：同一套工厂 + 同一组不变量）</summary>
public class ApiResponse
{
    /// <summary>业务结果的唯一判据（前端只读它）</summary>
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    /// <summary>成功提示；失败时恒为 ""</summary>
    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    /// <summary>失败原因；成功时恒为 ""，失败时必非空</summary>
    [JsonPropertyName("err")]
    public string Err { get; init; } = string.Empty;

    /// <summary>与 HTTP 解耦的业务码（200/400/500）；前端禁止读。P0-a 起补齐，与泛型版一致</summary>
    [JsonPropertyName("code")]
    public int Code { get; init; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; } = DateTime.Now;

    /// <summary>封闭构造：请用下方静态工厂创建</summary>
    private ApiResponse() { }

    /// <summary>成功（无载荷）</summary>
    public static ApiResponse Ok(string message = "操作成功") =>
        new() { Success = true, Code = 200, Message = message, Err = string.Empty };

    /// <summary>业务失败（HTTP 恒 200，code=400；P1 起业务拒绝统一入口）</summary>
    public static ApiResponse BizFail(string err, int code = 400) =>
        Fail(err, code);

    /// <summary>业务失败（HTTP 由调用方决定；P1 起一律 HTTP 200）</summary>
    public static ApiResponse Fail(string err, int code = 400) =>
        new() { Success = false, Code = code, Message = string.Empty, Err = string.IsNullOrEmpty(err) ? "操作失败" : err };

    /// <summary>系统异常（code=500）</summary>
    public static ApiResponse Error(string err, int code = 500) =>
        new() { Success = false, Code = code, Message = string.Empty, Err = string.IsNullOrEmpty(err) ? "操作失败，请联系管理员" : err };

    /// <summary>异常出口：仅供 <c>GlobalExceptionFilter</c> 使用</summary>
    public static ApiResponse FromError(string err, int code) =>
        new()
        {
            Success = false,
            Code = code,
            Message = string.Empty,
            Err = string.IsNullOrEmpty(err) ? "操作失败，请联系管理员" : err,
        };

    /// <summary>
    /// 从 IOperationResult 自动转换（基类便捷方法）
    /// </summary>
    public static ApiResponse FromOperationResult(IOperationResult result)
    {
        return result.Success
            ? Ok(result.Message)
            : Fail(result.Message);
    }
}

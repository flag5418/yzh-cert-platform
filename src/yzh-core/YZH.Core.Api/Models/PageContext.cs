using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using YZH.Core.Api.Extensions;
using YZH.Core.Stand.Models;

namespace YZH.Core.Api.Models;

/// <summary>
///     查询上下文 - 用于分页查询、列表查询等读操作
///     包含当前请求的用户信息、IP、查询条件
/// </summary>
public class PageContext<T> where T : BaseEntity
{
    /// <summary>页面配置</summary>
    public EntityConfig Config { get; set; } = null!;

    /// <summary>查询条件</summary>
    public RequestCondition[] Conditions { get; set; } = Array.Empty<RequestCondition>();

    /// <summary>HttpContext</summary>
    public HttpContext HttpContext { get; set; } = null!;

    /// <summary>当前用户 ID（GUID，旧版兼容）</summary>
    public string UserId => HttpContext.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0";

    /// <summary>当前用户 Code（新版核心业务编码）</summary>
    public string UserCode => HttpContext.User?.FindFirst("code")?.Value ?? "";

    /// <summary>当前用户名称</summary>
    public string UserName => HttpContext.User?.FindFirst(ClaimTypes.Name)?.Value ?? "anonymous";

    /// <summary>客户端 IP</summary>
    public string ClientIp => HttpContext.GetClientIp();

    /// <summary>是否取消操作（业务校验失败时设置）</summary>
    public bool Cancel { get; set; }

    /// <summary>取消原因</summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
///     保存上下文 - 用于新增、修改、删除等写操作
///     包含实体数据、操作类型、取消标记、原始数据
/// </summary>
public class SaveContext<T> : PageContext<T> where T : BaseEntity
{
    /// <summary>当前操作的实体</summary>
    public T Entity { get; set; } = null!;

    /// <summary>批量操作的实体列表</summary>
    public List<T>? Entities { get; set; }

    /// <summary>操作类型</summary>
    public OperType OperType { get; set; }

    /// <summary>待操作的 ID 列表（批量删除/批量操作场景）</summary>
    public List<string>? Ids { get; set; }
}

/// <summary>
///     操作类型枚举
/// </summary>
public enum OperType
{
    Add = 0,
    Update = 1,
    Delete = 2
}

/// <summary>
///     请求查询条件（前端传入的标准格式）
/// </summary>
public class RequestCondition
{
    public string Field { get; set; } = string.Empty;
    public string? Value { get; set; }
    public string Operator { get; set; } = "eq";
}

/// <summary>
///     校验结果
/// </summary>
public class ValidationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;

    public static ValidationResult Ok() => new() { Success = true };
    public static ValidationResult Fail(string msg) => new() { Success = false, Message = msg };
}

/// <summary>
///     导入结果
/// </summary>
public class ImportResult
{
    public int TotalRows { get; set; }
    public int Inserted { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public List<string> Errors { get; set; } = new();
}

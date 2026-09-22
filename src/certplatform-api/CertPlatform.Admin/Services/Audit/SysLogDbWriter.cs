using CertPlatform.Shared.Entities.Sys;
using YZH.Core.Api.Services;
using YZH.Core.DataBase.Interfaces;

namespace CertPlatform.Admin.Services.Audit;

/// <summary>
/// 操作日志数据库写入服务
/// 由 IYzhAuditLogger.DbWriter 回调调用，将审计条目持久化到 sys_log 表
/// </summary>
public class SysLogDbWriter
{
    private readonly IDbOrm _db;

    public SysLogDbWriter(IDbOrm db)
    {
        _db = db;
    }

    public async Task WriteAsync(AuditLogEntry entry)
    {
        var actionName = entry.Action ?? "";
        var moduleName = ExtractModuleName(actionName);
        var operationType = ExtractOperationType(entry.HttpMethod, entry.Level);
        // TargetType 从 Action 的 Controller 命名空间前缀提取（如 ...Controllers.Foundation.SysLogController → "Foundation"）
        var targetType = ExtractTargetType(actionName);

        var logEntity = new SysLog
        {
            Code = Guid.NewGuid().ToString("N"),
            Module = moduleName,
            Action = operationType,
            TargetType = targetType,
            TargetId = null,
            UserId = null,
            Detail = BuildDetail(entry),
            IpAddress = entry.IpAddress,
            UserAgent = entry.UserAgent,
            CreateTime = DateTime.UtcNow,
        };

        await _db.InsertAsync(logEntity);
    }

    private static string ExtractModuleName(string actionName)
    {
        var parts = actionName.Split('.');
        for (int i = parts.Length - 1; i >= 0; i--)
        {
            if (parts[i].EndsWith("Controller"))
            {
                return parts[Math.Max(0, i - 2)] switch
                {
                    "Foundation" => "基础配置",
                    "System" => "系统管理",
                    "Workflow" => "工作流",
                    _ => parts[Math.Max(0, i - 1)]
                };
            }
        }
        return actionName;
    }

    private static string ExtractTargetType(string actionName)
    {
        // 从完整 Action 名提取 Controller 所在命名空间作为 TargetType
        // 例：CertPlatform.Admin.Controllers.System.SysLogController.Filter → "System"
        var parts = actionName.Split('.');
        for (int i = parts.Length - 1; i >= 0; i--)
        {
            if (parts[i].EndsWith("Controller"))
            {
                return parts[Math.Max(0, i - 1)]; // 取 Controller 所在命名空间
            }
        }
        return null;
    }

    private static string ExtractOperationType(string? httpMethod, YZH.Core.Api.Services.LogLevel level)
    {
        if (level == YZH.Core.Api.Services.LogLevel.Error || level == YZH.Core.Api.Services.LogLevel.Critical)
            return "异常";
        return httpMethod?.ToUpper() switch
        {
            "POST" => "新增",
            "PUT" or "PATCH" => "编辑",
            "DELETE" => "删除",
            "GET" => "查询",
            _ => "操作"
        };
    }

    private static string BuildDetail(AuditLogEntry entry)
    {
        var parts = new List<string>();
        if (entry.UserName != null) parts.Add($"用户={entry.UserName}({entry.UserCode})");
        if (entry.Path != null) parts.Add($"路径={entry.Path}");
        if (entry.RequestParams != null) parts.Add($"参数={entry.RequestParams}");
        if (entry.ErrorMessage != null) parts.Add($"错误={entry.ErrorMessage}");
        if (entry.DurationMs.HasValue) parts.Add($"耗时={entry.DurationMs}ms");
        return string.Join(" | ", parts);
    }
}

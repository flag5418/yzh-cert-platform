using Microsoft.AspNetCore.Hosting;
using YZH.Core.Stand.Models;

namespace YZH.Core.Api.Services;

/// <summary>
///     审计日志服务接口
///     记录所有数据库写操作（操作人、时间、操作类型、客户端 IP）
///     对标参考架构的 YZH.Stand.Helper.log.LogHelper
/// </summary>
public interface IAuditLogger
{
    /// <summary>记录插入操作</summary>
    void LogInsert<T>(T entity, RequestContext context) where T : BaseEntity;

    /// <summary>记录更新操作</summary>
    void LogUpdate<T>(T entity, RequestContext context) where T : BaseEntity;

    /// <summary>记录删除操作</summary>
    void LogDelete<T>(T entity, RequestContext context) where T : BaseEntity;

    /// <summary>记录批量操作</summary>
    void LogBatchInsert<T>(List<T> entities, RequestContext context) where T : BaseEntity;
    void LogBatchUpdate<T>(List<T> entities, RequestContext context) where T : BaseEntity;
    void LogBatchDelete<T>(List<string> codes, RequestContext context) where T : BaseEntity;

    /// <summary>记录异常</summary>
    void LogException(Exception ex, string operationType, object? entity, RequestContext context);

    /// <summary>记录自定义操作</summary>
    void LogOperation(string operationType, string description, RequestContext context);
}

/// <summary>
///     审计日志实现 - 文件日志 + 控制台
/// </summary>
public class AuditLogger : IAuditLogger
{
    private readonly IWebHostEnvironment _environment;

    public AuditLogger(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public void LogInsert<T>(T entity, RequestContext context) where T : BaseEntity
    {
        Log("INSERT", $"{context.UserName}({context.ClientIp}) 新增记录 Id={entity.Id}, Code={entity.Code}, Type={typeof(T).Name}", context);
    }

    public void LogUpdate<T>(T entity, RequestContext context) where T : BaseEntity
    {
        Log("UPDATE", $"{context.UserName}({context.ClientIp}) 更新记录 Id={entity.Id}, Code={entity.Code}, Type={typeof(T).Name}", context);
    }

    public void LogDelete<T>(T entity, RequestContext context) where T : BaseEntity
    {
        Log("DELETE", $"{context.UserName}({context.ClientIp}) 删除记录 Id={entity.Id}, Code={entity.Code}, Type={typeof(T).Name}", context);
    }

    public void LogBatchInsert<T>(List<T> entities, RequestContext context) where T : BaseEntity
    {
        Log("BATCH_INSERT", $"{context.UserName}({context.ClientIp}) 批量新增 {entities.Count} 条记录, Type={typeof(T).Name}", context);
    }

    public void LogBatchUpdate<T>(List<T> entities, RequestContext context) where T : BaseEntity
    {
        Log("BATCH_UPDATE", $"{context.UserName}({context.ClientIp}) 批量更新 {entities.Count} 条记录, Type={typeof(T).Name}", context);
    }

    public void LogBatchDelete<T>(List<string> codes, RequestContext context) where T : BaseEntity
    {
        Log("BATCH_DELETE", $"{context.UserName}({context.ClientIp}) 批量删除 {codes.Count} 条记录, Type={typeof(T).Name}", context);
    }

    public void LogException(Exception ex, string operationType, object? entity, RequestContext context)
    {
        var entityInfo = entity != null ? $"Entity={entity.GetType().Name}" : "Entity=null";
        var msg = $"{operationType} 异常: {ex.Message}, {entityInfo}, User={context.UserName}({context.ClientIp})";
        Log("ERROR", msg, context);
    }

    public void LogOperation(string operationType, string description, RequestContext context)
    {
        Log(operationType, $"{context.UserName}({context.ClientIp}) {description}", context);
    }

    private void Log(string level, string message, RequestContext context)
    {
        var logEntry = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [{level}] [User={context.UserName}({context.ClientIp})] {message}";

        // 控制台日志
        try
        {
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = level switch
            {
                "ERROR" => ConsoleColor.Red,
                "DELETE" => ConsoleColor.Yellow,
                "BATCH_DELETE" => ConsoleColor.Yellow,
                "INSERT" => ConsoleColor.Green,
                "UPDATE" => ConsoleColor.Cyan,
                _ => ConsoleColor.Gray
            };
            Console.WriteLine(logEntry);
            Console.ForegroundColor = originalColor;
        }
        catch { /* 忽略日志写入失败 */ }

        // 文件日志
        try
        {
            var logDir = Path.Combine(_environment.ContentRootPath, "Logs", "Audit");
            Directory.CreateDirectory(logDir);
            var logFile = Path.Combine(logDir, $"{DateTime.UtcNow:yyyy-MM-dd}.log");
            File.AppendAllText(logFile, logEntry + Environment.NewLine);
        }
        catch { /* 日志写入失败不影响主流程 */ }
    }
}

using YZH.Core.Api.Models.System;
using YZH.Core.Api.Services;
using YZH.Core.DataBase.Interfaces;

namespace CertPlatform.Admin.Services.Audit;

/// <summary>
/// 操作日志数据库写入服务
/// 由 IYzhAuditLogger.DbWriter 回调调用，将审计条目持久化到 sys_log 表
///
/// 落库语义（2026-10-16 修正，用户报障「看不出是哪个接口执行了什么操作」）：
///   · Module    ← 控制器所在业务域（中文，如 基础资料 / 系统管理 / 业务管理）
///   · Action    ← {中文操作} {动作方法名}（如 查询 Filter、新增 Save）
///   · ApiPath   ← HTTP 请求路径（真实接口地址，新增列）
///   · TargetType← 控制器名（去 Controller 后缀）
///   旧实现仅按 HTTP 动词落库、Module/TargetType 写命名空间，真实接口信息全丢。
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
        var (module, controller, actionName) = ResolveEndpoint(entry);
        var operationType = ExtractOperationType(entry.HttpMethod, actionName, entry.Level);
        var actionText = string.IsNullOrEmpty(actionName)
            ? operationType
            : $"{operationType} {actionName}";

        var logEntity = new SysLog
        {
            Code = Guid.NewGuid().ToString("N"),
            Module = module,
            Action = actionText,
            ApiPath = entry.Path,
            TargetType = controller,
            TargetId = null,
            UserId = null,
            Detail = BuildDetail(entry),
            IpAddress = entry.IpAddress,
            UserAgent = entry.UserAgent,
            CreateTime = DateTime.Now,
        };

        await _db.InsertAsync(logEntity);
    }

    /// <summary>
    /// 解析「模块 / 控制器名 / 动作名」。
    /// 优先取过滤器下发的结构化字段；缺失时回退解析 DisplayName
    /// （格式：命名空间.控制器.动作 (程序集)）。
    /// </summary>
    private static (string module, string? controller, string? actionName) ResolveEndpoint(AuditLogEntry entry)
    {
        var controller = entry.Controller;
        var actionName = entry.ActionName;
        var ns = entry.ControllerNamespace;

        if (controller == null || actionName == null || ns == null)
        {
            var (parsedNs, parsedController, parsedAction) = ParseDisplayName(entry.Action);
            controller ??= parsedController;
            actionName ??= parsedAction;
            ns ??= parsedNs;
        }

        return (ResolveModule(ns), controller, actionName);
    }

    private static (string? ns, string? controller, string? action) ParseDisplayName(string? displayName)
    {
        if (string.IsNullOrEmpty(displayName)) return (null, null, null);

        var text = displayName;
        var paren = text.IndexOf(" (", StringComparison.Ordinal);
        if (paren >= 0) text = text[..paren];

        var parts = text.Split('.', StringSplitOptions.RemoveEmptyEntries);
        for (var i = parts.Length - 1; i >= 0; i--)
        {
            if (!parts[i].EndsWith("Controller", StringComparison.Ordinal)) continue;

            var ns = string.Join('.', parts[..i]);
            var controllerName = parts[i][..^"Controller".Length];
            var action = i + 1 < parts.Length ? parts[i + 1] : null;
            return (ns, controllerName, action);
        }

        return (null, null, null);
    }

    private static string ResolveModule(string? ns)
    {
        if (string.IsNullOrWhiteSpace(ns)) return "其他";

        if (ns.Contains(".Auditor.") || ns.EndsWith(".Auditor", StringComparison.Ordinal)) return "专家端";
        if (ns.Contains(".Controllers.Foundation")) return "基础资料";
        if (ns.Contains(".Controllers.Workflow")) return "业务管理";
        if (ns.Contains(".Controllers.Cert")) return "认证业务";
        if (ns.Contains(".Controllers.System")) return "系统管理";
        if (ns.Contains(".Admin.Controllers")) return "后台管理";
        return "框架";
    }

    private static string ExtractOperationType(string? httpMethod, string? actionName, LogLevel level)
    {
        if (level == LogLevel.Error || level == LogLevel.Critical)
            return "异常";

        // 优先按动作名语义判定（查询类接口常以 POST 承载，仅看 HTTP 方法会误判为「新增」）
        if (!string.IsNullOrEmpty(actionName))
        {
            var a = actionName.ToLowerInvariant();
            if (StartsWithAny(a, "get", "query", "list", "filter", "tree", "config", "page",
                    "search", "load", "detail", "read", "active", "unread"))
                return "查询";
            if (StartsWithAny(a, "export", "download")) return "导出";
            if (StartsWithAny(a, "import", "upload")) return "导入";
            if (StartsWithAny(a, "save", "add", "create", "insert", "new")) return "新增";
            if (StartsWithAny(a, "update", "edit", "modify", "set", "change", "enable", "disable", "valid"))
                return "编辑";
            if (StartsWithAny(a, "delete", "remove", "clear", "drop")) return "删除";
            if (StartsWithAny(a, "login", "signin")) return "登录";
            if (StartsWithAny(a, "logout", "signout")) return "登出";
            if (a.StartsWith("sync", StringComparison.Ordinal)) return "同步";
        }

        return httpMethod?.ToUpper() switch
        {
            "POST" => "新增",
            "PUT" or "PATCH" => "编辑",
            "DELETE" => "删除",
            "GET" => "查询",
            _ => "操作"
        };
    }

    private static bool StartsWithAny(string value, params string[] prefixes)
        => prefixes.Any(p => value.StartsWith(p, StringComparison.Ordinal));

    private static string BuildDetail(AuditLogEntry entry)
    {
        var parts = new List<string>();
        if (entry.UserCode != null) parts.Add($"用户={entry.UserName}({entry.UserCode})");
        else if (entry.UserName != null) parts.Add($"用户={entry.UserName}");
        if (entry.RequestParams != null) parts.Add($"参数={entry.RequestParams}");
        if (entry.ErrorMessage != null) parts.Add($"错误={entry.ErrorMessage}");
        if (entry.DurationMs.HasValue) parts.Add($"耗时={entry.DurationMs}ms");
        return string.Join(" | ", parts);
    }
}

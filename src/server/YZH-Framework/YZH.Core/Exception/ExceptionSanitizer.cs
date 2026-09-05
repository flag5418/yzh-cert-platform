using YZH.Core.Services;
using YZH.Core.Utilities;

namespace YZH.Core.Exceptions;

/// <summary>
/// 异常消息脱敏器。
/// 核心职责：
/// 1. 识别数据库异常类型，匹配友好提示
/// 2. 过滤敏感信息（SQL/连接字符串/表名/堆栈）
/// 3. 生成错误码用于日志关联
///
/// 绝不向前端暴露：
/// - SQL 语句（SELECT/INSERT/UPDATE/DELETE ...）
/// - 连接字符串（Server=/Password=/Database=）
/// - 表结构信息（列名、约束名、索引名）
/// - 堆栈信息（at YZH.Core...）
/// </summary>
public static class ExceptionSanitizer
{
    /// <summary>
    /// 将原始异常转换为安全的 WebResponseContent。
    /// 处理顺序：
    /// 1. YZHException → 直接使用 Message（已经是友好提示）
    /// 2. DbException → 脱敏匹配
    /// 3. 其他异常 → 统一兜底
    /// </summary>
    public static WebResponseContent Sanitize(
        System.Exception? ex,
        string operation = "操作")
    {
        if (ex == null)
            return new WebResponseContent().Error($"{operation}失败：未知错误");

        // 1. 业务异常：直接使用消息
        if (ex is YZHException bizEx)
        {
            Logger.Info($"业务异常 [{bizEx.ErrorCode}]: {bizEx.Message}");
            return new WebResponseContent().Error(bizEx.Message);
        }

        // 2. 数据库异常：脱敏匹配
        var dbFriendly = TryGetDbFriendlyMessage(ex);
        if (dbFriendly != null)
        {
            var errorCode = GenerateErrorCode();
            Logger.Error(
                $"数据库异常 [{errorCode}] 类型:{ex.GetType().Name} " +
                $"原始:{ex.Message}\n{ex.StackTrace}");
            return new WebResponseContent().Error(
                $"{dbFriendly}（错误码：{errorCode}）");
        }

        // 3. 系统异常：完全脱敏
        var sysErrorCode = GenerateErrorCode();
        Logger.Error(
            $"系统异常 [{sysErrorCode}] 类型:{ex.GetType().Name} " +
            $"消息:{ex.Message}\n{ex.StackTrace}");
        return new WebResponseContent().Error(
            $"{operation}失败，请联系管理员（错误码：{sysErrorCode}）");
    }

    /// <summary>
    /// 尝试从数据库异常中提取友好提示。
    /// MySQL 错误码匹配表：
    /// - 1062 Duplicate entry → "数据重复，请检查唯一性字段"
    /// - 1451 Foreign key constraint → "存在关联数据，无法删除"
    /// - 1213 Deadlock → "系统繁忙，请重试"
    /// - 1205 Lock wait timeout → "操作超时，请重试"
    /// - 1040 Too many connections → "系统繁忙，请稍后重试"
    /// - 1048 Cannot be null → "必填字段不能为空"
    /// - 1406 Data too long → "数据长度超出限制"
    /// </summary>
    private static string? TryGetDbFriendlyMessage(System.Exception? ex)
    {
        if (ex == null) return null;

        var message = (ex.Message ?? "").ToLower();

        // 唯一键冲突
        if (message.Contains("duplicate entry") || message.Contains("1062"))
            return "数据重复，请检查唯一性字段";

        // 外键约束
        if (message.Contains("foreign key constraint") || message.Contains("1451"))
            return "存在关联数据，无法删除，请先处理关联项";

        // 死锁
        if (message.Contains("deadlock") || message.Contains("1213"))
            return "系统繁忙，请重试";

        // 锁超时
        if (message.Contains("lock wait timeout") || message.Contains("1205"))
            return "操作超时，请重试";

        // 连接过多
        if (message.Contains("too many connections") || message.Contains("1040"))
            return "系统繁忙，请稍后重试";

        // 字段不能为空
        if (message.Contains("cannot be null") || message.Contains("1048"))
            return "必填字段不能为空";

        // 数据过长
        if (message.Contains("data too long") || message.Contains("1406"))
            return "数据长度超出限制，请检查输入";

        // EF Core 包装的 DbUpdateException → 递归检查内部异常
        if (ex is Microsoft.EntityFrameworkCore.DbUpdateException)
            return TryGetDbFriendlyMessage(ex.InnerException) ?? "数据保存失败";

        // 其他数据库异常类型
        var typeName = ex.GetType().Name;
        if (typeName.Contains("MySql") || typeName.Contains("SqlException"))
            return "数据库操作失败，请检查数据或联系管理员";

        return null;
    }

    private static string GenerateErrorCode()
        => $"ERR-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";

    /// <summary>
    /// 对 Vol 框架返回的 WebResponseContent 做脱敏检查。
    /// Vol 的 ServiceBase 内部会 catch 异常并把 ex.Message 直接放入 response.Message，
    /// 导致 SQL/堆栈/表名等信息暴露给前端。
    /// 此方法检测 response.Message 是否包含敏感信息，如果包含则脱敏后重新包装。
    /// </summary>
    public static WebResponseContent SanitizeResponse(WebResponseContent response, string operation = "操作")
    {
        if (response == null || response.Status) return response ?? new WebResponseContent().OK();

        var msg = response.Message ?? "";
        if (string.IsNullOrEmpty(msg)) return response;

        // 检查是否包含敏感信息特征
        bool containsSensitive = IsSensitiveMessage(msg);

        if (!containsSensitive) return response;

        // 包含敏感信息，需要脱敏
        var friendly = TryGetDbFriendlyMessageFromString(msg);
        var errorCode = GenerateErrorCode();
        Logger.Error(
            $"{operation}异常 [{errorCode}] 原始消息:{msg}");

        if (friendly != null)
            return new WebResponseContent().Error($"{friendly}（错误码：{errorCode}）");

        return new WebResponseContent().Error($"{operation}失败，请联系管理员（错误码：{errorCode}）");
    }

    /// <summary>检测消息是否包含敏感信息</summary>
    private static bool IsSensitiveMessage(string msg)
    {
        if (string.IsNullOrEmpty(msg)) return false;
        var lower = msg.ToLower();

        return lower.Contains("duplicate entry")
            || lower.Contains("foreign key constraint")
            || lower.Contains("deadlock")
            || lower.Contains("lock wait timeout")
            || lower.Contains("cannot be null")
            || lower.Contains("data too long")
            || lower.Contains(" at vol.core")
            || lower.Contains(" at system.")
            || lower.Contains("savedata")
            || lower.Contains("dbcontext")
            || lower.Contains(".cs:line")
            || lower.Contains("mysql")
            || lower.Contains("sql")
            && lower.Contains("exception");
    }

    /// <summary>从消息字符串中提取数据库友好提示</summary>
    private static string? TryGetDbFriendlyMessageFromString(string msg)
    {
        if (string.IsNullOrEmpty(msg)) return null;
        var lower = msg.ToLower();

        if (lower.Contains("duplicate entry") || lower.Contains("1062"))
            return "数据重复，请检查唯一性字段";
        if (lower.Contains("foreign key constraint") || lower.Contains("1451"))
            return "存在关联数据，无法删除，请先处理关联项";
        if (lower.Contains("deadlock") || lower.Contains("1213"))
            return "系统繁忙，请重试";
        if (lower.Contains("lock wait timeout") || lower.Contains("1205"))
            return "操作超时，请重试";
        if (lower.Contains("cannot be null") || lower.Contains("1048"))
            return "必填字段不能为空";
        if (lower.Contains("data too long") || lower.Contains("1406"))
            return "数据长度超出限制，请检查输入";

        return null;
    }
}

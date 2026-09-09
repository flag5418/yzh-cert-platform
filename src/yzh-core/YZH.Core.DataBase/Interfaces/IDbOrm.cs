using System.Linq.Expressions;
using YZH.Core.Stand.Models;
using YZH.Core.Stand.Models.Result;
using YZH.Core.DataBase.Models;

namespace YZH.Core.DataBase.Interfaces;

/// <summary>
///     数据库操作接口（IDbOrm）
///     定义标准的 CRUD 操作，支持未来替换 ORM 实现
///     对标参考架构 YZH.DataBase/Interface/IDbOrm.cs
/// </summary>
public interface IDbOrm
{
    // ==================== 查询 ====================

    /// <summary>根据条件获取单条</summary>
    Task<Result<T?>> GetOneAsync<T>(Expression<Func<T, bool>> predicate) where T : class, new();

    /// <summary>获取列表</summary>
    Task<Result<List<T>>> GetListAsync<T>(Expression<Func<T, bool>>? predicate = null) where T : class, new();

    /// <summary>分页查询</summary>
    Task<Result<(List<T> items, int total)>> GetPageAsync<T>(SqlPageOptions options) where T : class, new();

    /// <summary>统计数量</summary>
    Task<Result<int>> CountAsync<T>(Expression<Func<T, bool>>? predicate = null) where T : class, new();

    /// <summary>判断是否存在</summary>
    Task<Result<bool>> ExistsAsync<T>(Expression<Func<T, bool>> predicate) where T : class, new();

    // ==================== 写入 ====================

    /// <summary>新增</summary>
    Task<Result<T>> InsertAsync<T>(T entity) where T : class, new();

    /// <summary>批量新增</summary>
    Task<Result<int>> InsertBatchAsync<T>(IEnumerable<T> entities) where T : class, new();

    /// <summary>更新（全字段）</summary>
    Task<Result<T>> UpdateAsync<T>(T entity) where T : class, new();

    /// <summary>更新（指定字段）</summary>
    Task<Result<T>> UpdateAsync<T>(T entity, params string[] fields) where T : class, new();

    /// <summary>批量更新</summary>
    Task<Result<int>> UpdateBatchAsync<T>(IEnumerable<T> entities) where T : class, new();

    /// <summary>按 Code 删除</summary>
    Task<Result<bool>> DeleteByCodeAsync<T>(string code) where T : class, new();

    /// <summary>按 Code 批量删除</summary>
    Task<Result<int>> DeleteByCodeBatchAsync<T>(IEnumerable<string> codes) where T : class, new();

    // ==================== 原生 SQL ====================

    /// <summary>执行 SQL 查询（返回动态对象）</summary>
    Task<Result<List<dynamic>>> SqlQueryAsync(string sql, object? param = null);

    /// <summary>执行 SQL 查询（返回强类型）</summary>
    Task<Result<List<T>>> SqlQueryAsync<T>(string sql, object? param = null) where T : class, new();

    /// <summary>
    ///     执行 SQL 查询并返回单条记录（强类型 + 参数化）
    ///     内部调用 Dapper 的 QueryFirstOrDefaultAsync，天然支持参数化防注入
    /// </summary>
    Task<Result<T?>> QueryFirstOrDefaultAsync<T>(string sql, object? param = null) where T : class, new();

    /// <summary>
    ///     执行 SQL 命令（INSERT/UPDATE/DELETE）
    ///     内部调用 Dapper 的 ExecuteAsync，天然支持参数化防注入
    /// </summary>
    Task<Result<int>> SqlExecuteAsync(string sql, object? param = null);

    /// <summary>执行标量查询</summary>
    Task<Result<T?>> SqlScalarAsync<T>(string sql, object? param = null);

    // ==================== 事务 ====================

    /// <summary>开始事务</summary>
    IDbTransaction BeginTransaction();
}

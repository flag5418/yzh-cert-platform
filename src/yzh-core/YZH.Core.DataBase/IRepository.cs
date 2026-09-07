using System.Linq.Expressions;
using YZH.Core.Stand.Models;

namespace YZH.Core.DataBase;

/// <summary>
///     通用仓储接口
///     对标老YZH架构的 IDbOrm 接口
///     定义标准的 CRUD 操作 + 分页 + 过滤 + 排序
///     
///     软删除过滤约定：
///     - 所有查询方法默认 includeDeleted: false，自动过滤 IsDeleted=true 的记录
///     - 传入 includeDeleted: true 时返回全部记录（含已软删）
/// </summary>
public interface IRepository<T> where T : class
{
    // === 查询（默认过滤软删除） ===
    
    /// <summary>根据主键获取</summary>
    T? GetById(string id, bool includeDeleted = false);

    /// <summary>根据条件获取单条</summary>
    T? GetOne(Expression<Func<T, bool>> predicate, bool includeDeleted = false);

    /// <summary>根据业务 Code 获取实体</summary>
    T? GetByCode(string code, bool includeDeleted = false);

    /// <summary>根据条件获取列表</summary>
    List<T> GetList(Expression<Func<T, bool>>? predicate = null, bool includeDeleted = false);

    /// <summary>分页查询</summary>
    (List<T> items, int total) GetPage(PagerOptions options, bool includeDeleted = false);

    /// <summary>统计数量</summary>
    int Count(Expression<Func<T, bool>>? predicate = null, bool includeDeleted = false);

    /// <summary>判断是否存在</summary>
    bool Exists(Expression<Func<T, bool>> predicate, bool includeDeleted = false);

    // === 写入 ===
    
    /// <summary>新增单个（默认提交）</summary>
    T Insert(T entity);

    /// <summary>新增单个（支持 saveChanges 参数）</summary>
    T Insert(T entity, bool saveChanges);

    /// <summary>批量新增</summary>
    void InsertBatch(IEnumerable<T> entities);

    /// <summary>批量新增（支持 saveChanges 参数）</summary>
    void InsertBatch(IEnumerable<T> entities, bool saveChanges);

    // === 更新 ===

    /// <summary>更新实体（全字段，默认提交）</summary>
    T Update(T entity);

    /// <summary>更新实体（全字段，支持 saveChanges 参数）</summary>
    T Update(T entity, bool saveChanges);

    /// <summary>更新实体（指定字段）</summary>
    T Update(T entity, params string[] fields);

    /// <summary>更新实体（指定字段，支持 saveChanges 参数）</summary>
    T Update(T entity, bool saveChanges, string[]? fields);

    // === 删除 ===

    /// <summary>删除单个</summary>
    void Delete(string id);

    /// <summary>删除单个（支持 saveChanges 参数）</summary>
    void Delete(string id, bool saveChanges);

    /// <summary>批量删除</summary>
    void DeleteBatch(IEnumerable<string> ids);

    /// <summary>批量删除（支持 saveChanges 参数）</summary>
    void DeleteBatch(IEnumerable<string> ids, bool saveChanges);

    /// <summary>根据条件删除</summary>
    void DeleteWhere(Expression<Func<T, bool>> predicate, bool saveChanges);

    /// <summary>保存变更（显式调用）</summary>
    int SaveChanges();

    // === 事务 ===
    
    /// <summary>执行事务（返回 (success, error)）</summary>
    (bool success, string? err) ExecuteInTransaction(Action action);
    
    /// <summary>执行事务（带返回值），返回 (result, error)）</summary>
    (TResult? result, string? err) ExecuteInTransaction<TResult>(Func<TResult> func);

    /// <summary>开启事务（返回事务句柄，用于更复杂的控制）</summary>
    IRepositoryTransaction BeginTransaction();
}

/// <summary>
///     仓储事务句柄（支持 Commit/Rollback）
/// </summary>
public interface IRepositoryTransaction : IDisposable
{
    void Commit();
    void Rollback();
}

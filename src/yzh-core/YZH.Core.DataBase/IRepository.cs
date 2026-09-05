using System.Linq.Expressions;
using YZH.Core.Stand.Models;

namespace YZH.Core.DataBase;

/// <summary>
///     通用仓储接口
///     对标老YZH架构的 IDbOrm 接口
///     定义标准的 CRUD 操作 + 分页 + 过滤 + 排序
/// </summary>
public interface IRepository<T> where T : BaseEntity
{
    // === 查询 ===
    
    /// <summary>根据主键获取</summary>
    T? GetById(string id);

    /// <summary>根据条件获取单条</summary>
    T? GetOne(Expression<Func<T, bool>> predicate);

    /// <summary>根据条件获取列表</summary>
    List<T> GetList(Expression<Func<T, bool>>? predicate = null);

    /// <summary>分页查询</summary>
    (List<T> items, int total) GetPage(PagerOptions options);

    /// <summary>统计数量</summary>
    int Count(Expression<Func<T, bool>>? predicate = null);

    /// <summary>判断是否存在</summary>
    bool Exists(Expression<Func<T, bool>> predicate);

    // === 写入 ===
    
    /// <summary>新增单个</summary>
    T Insert(T entity);

    /// <summary>批量新增</summary>
    void InsertBatch(IEnumerable<T> entities);

    // === 更新 ===
    
    /// <summary>更新实体（全字段）</summary>
    T Update(T entity);

    /// <summary>更新实体（指定字段）</summary>
    T Update(T entity, params string[] fields);

    // === 删除 ===
    
    /// <summary>删除单个</summary>
    void Delete(string id);

    /// <summary>批量删除</summary>
    void DeleteBatch(IEnumerable<string> ids);

    /// <summary>根据条件删除</summary>
    void DeleteWhere(Expression<Func<T, bool>> predicate);

    // === 事务 ===
    
    /// <summary>执行事务</summary>
    void ExecuteInTransaction(Action action);
    
    /// <summary>执行事务（带返回值）</summary>
    TResult ExecuteInTransaction<TResult>(Func<TResult> func);
}

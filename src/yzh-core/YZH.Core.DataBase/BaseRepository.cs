using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using YZH.Core.Stand.Models;

namespace YZH.Core.DataBase;

/// <summary>
///     通用仓储的 EF Core 实现
///     
///     软删除过滤：所有查询方法默认过滤 IsDeleted=true，传入 includeDeleted: true 时不过滤
/// </summary>
public class BaseRepository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly BaseDbContext Context;
    protected readonly DbSet<T> DbSet;

    public BaseRepository(BaseDbContext context)
    {
        Context = context;
        DbSet = context.Set<T>();
    }

    // ==================== 查询（默认过滤软删除） ====================

    public virtual T? GetById(string id, bool includeDeleted = false)
    {
        var query = DbSet.AsQueryable();
        if (!includeDeleted) query = query.Where(e => !e.IsDeleted);
        return query.FirstOrDefault(e => e.Id == id);
    }

    /// <summary>根据业务 Code 获取实体</summary>
    public virtual T? GetByCode(string code, bool includeDeleted = false)
    {
        var query = DbSet.AsQueryable();
        if (!includeDeleted) query = query.Where(e => !e.IsDeleted);
        return query.FirstOrDefault(e => e.Code == code);
    }

    public virtual T? GetOne(Expression<Func<T, bool>> predicate, bool includeDeleted = false)
    {
        var query = DbSet.AsQueryable();
        if (!includeDeleted) query = query.Where(e => !e.IsDeleted);
        return query.FirstOrDefault(predicate);
    }

    public virtual List<T> GetList(Expression<Func<T, bool>>? predicate = null, bool includeDeleted = false)
    {
        var query = DbSet.AsQueryable();
        if (!includeDeleted) query = query.Where(e => !e.IsDeleted);
        if (predicate != null) query = query.Where(predicate);
        return query.ToList();
    }

    public virtual (List<T> items, int total) GetPage(PagerOptions options, bool includeDeleted = false)
    {
        var query = DbSet.AsQueryable();
        if (!includeDeleted) query = query.Where(e => !e.IsDeleted);

        // 过滤
        if (options.Filters != null)
        {
            foreach (var filter in options.Filters)
            {
                query = ApplyFilter(query, filter);
            }
        }

        // 关键字搜索
        if (!string.IsNullOrEmpty(options.SearchKey))
        {
            query = ApplySearch(query, options.SearchKey);
        }

        var total = query.Count();

        // 排序
        if (!string.IsNullOrEmpty(options.SortBy))
        {
            query = ApplySorting(query, options.SortBy, options.SortDirection ?? "asc");
        }

        // 分页
        if (!options.NoPage)
        {
            query = query.Skip((options.Page - 1) * options.PageSize)
                        .Take(options.PageSize);
        }

        return (query.ToList(), total);
    }

    public virtual int Count(Expression<Func<T, bool>>? predicate = null, bool includeDeleted = false)
    {
        var query = DbSet.AsQueryable();
        if (!includeDeleted) query = query.Where(e => !e.IsDeleted);
        if (predicate != null) query = query.Where(predicate);
        return query.Count();
    }

    public virtual bool Exists(Expression<Func<T, bool>> predicate, bool includeDeleted = false)
    {
        var query = DbSet.AsQueryable();
        if (!includeDeleted) query = query.Where(e => !e.IsDeleted);
        return query.Any(predicate);
    }

    // ==================== 写入 ====================

    public virtual T Insert(T entity)
    {
        entity.Id = string.IsNullOrEmpty(entity.Id) ? Guid.NewGuid().ToString("N") : entity.Id;
        DbSet.Add(entity);
        Context.SaveChanges();
        return entity;
    }

    /// <summary>新增（支持 saveChanges 参数）</summary>
    public virtual T Insert(T entity, bool saveChanges)
    {
        entity.Id = string.IsNullOrEmpty(entity.Id) ? Guid.NewGuid().ToString("N") : entity.Id;
        DbSet.Add(entity);
        if (saveChanges) Context.SaveChanges();
        return entity;
    }

    public virtual void InsertBatch(IEnumerable<T> entities)
    {
        DbSet.AddRange(entities);
        Context.SaveChanges();
    }

    public virtual void InsertBatch(IEnumerable<T> entities, bool saveChanges)
    {
        DbSet.AddRange(entities);
        if (saveChanges) Context.SaveChanges();
    }

    public virtual T Update(T entity)
    {
        DbSet.Update(entity);
        Context.SaveChanges();
        return entity;
    }

    public virtual T Update(T entity, bool saveChanges)
    {
        DbSet.Update(entity);
        if (saveChanges) Context.SaveChanges();
        return entity;
    }

    public virtual T Update(T entity, params string[] fields)
    {
        var entry = Context.Entry(entity);
        foreach (var field in fields)
        {
            entry.Property(field).IsModified = true;
        }
        Context.SaveChanges();
        return entity;
    }

    public virtual T Update(T entity, bool saveChanges, string[]? fields)
    {
        var entry = Context.Entry(entity);
        if (fields != null)
        {
            foreach (var field in fields)
                entry.Property(field).IsModified = true;
        }
        else
        {
            DbSet.Update(entity);
        }
        if (saveChanges) Context.SaveChanges();
        return entity;
    }

    // ==================== 删除 ====================

    public virtual void Delete(string id)
    {
        var entity = DbSet.Find(id);
        if (entity != null)
        {
            DbSet.Remove(entity);
            Context.SaveChanges();
        }
    }

    public virtual void Delete(string id, bool saveChanges)
    {
        var entity = DbSet.Find(id);
        if (entity != null)
        {
            DbSet.Remove(entity);
            if (saveChanges) Context.SaveChanges();
        }
    }

    public virtual void DeleteBatch(IEnumerable<string> ids)
    {
        var idList = ids.ToList();
        var entities = DbSet.Where(x => idList.Contains(x.Id)).ToList();
        if (entities.Any())
        {
            DbSet.RemoveRange(entities);
            Context.SaveChanges();
        }
    }

    public virtual void DeleteBatch(IEnumerable<string> ids, bool saveChanges)
    {
        var idList = ids.ToList();
        var entities = DbSet.Where(x => idList.Contains(x.Id)).ToList();
        if (entities.Any())
        {
            DbSet.RemoveRange(entities);
            if (saveChanges) Context.SaveChanges();
        }
    }

    public virtual void DeleteWhere(Expression<Func<T, bool>> predicate, bool saveChanges)
    {
        var entities = DbSet.Where(predicate).ToList();
        if (entities.Any())
        {
            DbSet.RemoveRange(entities);
            if (saveChanges) Context.SaveChanges();
        }
    }

    public virtual int SaveChanges() => Context.SaveChanges();

    // ==================== 事务 ====================

    public virtual (bool success, string? err) ExecuteInTransaction(Action action)
    {
        using var tx = Context.Database.BeginTransaction();
        try
        {
            action();
            tx.Commit();
            return (true, null);
        }
        catch (Exception ex)
        {
            try { tx.Rollback(); }
            catch (Exception rollbackEx)
            {
                return (false, $"事务回滚失败：{rollbackEx.Message}");
            }
            return (false, ex.Message);
        }
    }

    public virtual (TResult? result, string? err) ExecuteInTransaction<TResult>(Func<TResult> func)
    {
        using var tx = Context.Database.BeginTransaction();
        try
        {
            var result = func();
            tx.Commit();
            return (result, null);
        }
        catch (Exception ex)
        {
            try { tx.Rollback(); }
            catch (Exception rollbackEx)
            {
                return (default, $"事务回滚失败：{rollbackEx.Message}");
            }
            return (default, ex.Message);
        }
    }

    public virtual IRepositoryTransaction BeginTransaction()
    {
        var dbTx = Context.Database.BeginTransaction();
        return new RepositoryTransaction(dbTx);
    }

    // ==================== 过滤/排序 ====================

    /// <summary>应用过滤条件</summary>
    protected virtual IQueryable<T> ApplyFilter(IQueryable<T> query, FilterItem filter)
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        var property = Expression.Property(parameter, filter.Field);
        var targetType = property.Type;
        var value = Convert.ChangeType(filter.Value ?? "", targetType);
        var constant = Expression.Constant(value, targetType);

        var op = (filter.Operator ?? "eq").ToLower();
        Expression comparison = op switch
        {
            "eq" => Expression.Equal(property, constant),
            "neq" => Expression.NotEqual(property, constant),
            "gt" => Expression.GreaterThan(property, constant),
            "lt" => Expression.LessThan(property, constant),
            "gte" => Expression.GreaterThanOrEqual(property, constant),
            "lte" => Expression.LessThanOrEqual(property, constant),
            "like" => BuildLikeExpression(property, constant),
            _ => Expression.Equal(property, constant)
        };

        var lambda = Expression.Lambda<Func<T, bool>>(comparison, parameter);
        return query.Where(lambda);
    }

    /// <summary>应用关键字搜索</summary>
    protected virtual IQueryable<T> ApplySearch(IQueryable<T> query, string searchKey)
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        var stringProperties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(string) && p.CanRead)
            .ToList();

        if (!stringProperties.Any()) return query;

        Expression? combined = null;
        var containsMethod = typeof(string).GetMethod("Contains", new[] { typeof(string) })!;
        var searchConstant = Expression.Constant(searchKey);

        foreach (var prop in stringProperties)
        {
            var propertyAccess = Expression.Property(parameter, prop);
            var containsCall = Expression.Call(propertyAccess, containsMethod, searchConstant);
            combined = combined == null ? containsCall : Expression.OrElse(combined, containsCall);
        }

        if (combined == null) return query;

        var lambda = Expression.Lambda<Func<T, bool>>(combined, parameter);
        return query.Where(lambda);
    }

    /// <summary>构建 Like 表达式（字符串包含）</summary>
    private static Expression BuildLikeExpression(MemberExpression property, ConstantExpression constant)
    {
        var containsMethod = typeof(string).GetMethod("Contains", new[] { typeof(string) })!;
        return Expression.Call(property, containsMethod, constant);
    }

    /// <summary>应用排序</summary>
    protected virtual IQueryable<T> ApplySorting(IQueryable<T> query, string sortBy, string direction)
    {
        try
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var property = Expression.Property(parameter, sortBy);
            var lambda = Expression.Lambda(property, parameter);

            var methodName = (direction ?? "desc").ToLower() == "desc" ? "OrderByDescending" : "OrderBy";
            var orderByMethod = typeof(Queryable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(m => m.Name == methodName && m.GetParameters().Length == 2);

            var genericMethod = orderByMethod.MakeGenericMethod(typeof(T), property.Type);
            return (IQueryable<T>)(genericMethod.Invoke(null, new object[] { query, lambda }) ?? query);
        }
        catch
        {
            // 排序字段不存在时回退
            return query;
        }
    }
}

/// <summary>
///     仓储事务实现
/// </summary>
internal class RepositoryTransaction : IRepositoryTransaction
{
    private readonly Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction _transaction;
    private bool _disposed;

    public RepositoryTransaction(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction)
    {
        _transaction = transaction;
    }

    public void Commit()
    {
        if (!_disposed)
            _transaction.Commit();
    }

    public void Rollback()
    {
        if (!_disposed)
            _transaction.Rollback();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _transaction.Dispose();
            _disposed = true;
        }
    }
}

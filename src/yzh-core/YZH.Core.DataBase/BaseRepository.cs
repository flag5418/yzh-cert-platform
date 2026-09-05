using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using YZH.Core.Stand.Models;

namespace YZH.Core.DataBase;

/// <summary>
///     通用仓储的 EF Core 实现
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

    public virtual T? GetById(string id) => DbSet.Find(id);

    public virtual T? GetOne(Expression<Func<T, bool>> predicate) 
        => DbSet.FirstOrDefault(predicate);

    public virtual List<T> GetList(Expression<Func<T, bool>>? predicate = null)
        => predicate == null ? DbSet.ToList() : DbSet.Where(predicate).ToList();

    public virtual (List<T> items, int total) GetPage(PagerOptions options)
    {
        var query = DbSet.AsQueryable();

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

    public virtual int Count(Expression<Func<T, bool>>? predicate = null)
        => predicate == null ? DbSet.Count() : DbSet.Count(predicate);

    public virtual bool Exists(Expression<Func<T, bool>> predicate)
        => DbSet.Any(predicate);

    public virtual T Insert(T entity)
    {
        entity.CreateTime = DateTime.UtcNow;
        entity.UpdateTime = DateTime.UtcNow;
        entity.Id = string.IsNullOrEmpty(entity.Id) ? Guid.NewGuid().ToString("N") : entity.Id;
        DbSet.Add(entity);
        Context.SaveChanges();
        return entity;
    }

    public virtual void InsertBatch(IEnumerable<T> entities)
    {
        foreach (var entity in entities)
        {
            entity.CreateTime = DateTime.UtcNow;
            entity.UpdateTime = DateTime.UtcNow;
        }
        DbSet.AddRange(entities);
        Context.SaveChanges();
    }

    public virtual T Update(T entity)
    {
        entity.UpdateTime = DateTime.UtcNow;
        DbSet.Update(entity);
        Context.SaveChanges();
        return entity;
    }

    public virtual T Update(T entity, params string[] fields)
    {
        entity.UpdateTime = DateTime.UtcNow;
        var entry = Context.Entry(entity);
        foreach (var field in fields)
        {
            entry.Property(field).IsModified = true;
        }
        Context.SaveChanges();
        return entity;
    }

    public virtual void Delete(string id)
    {
        var entity = DbSet.Find(id);
        if (entity != null)
        {
            DbSet.Remove(entity);
            Context.SaveChanges();
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

    public virtual void DeleteWhere(Expression<Func<T, bool>> predicate)
    {
        var entities = DbSet.Where(predicate).ToList();
        if (entities.Any())
        {
            DbSet.RemoveRange(entities);
            Context.SaveChanges();
        }
    }

    public virtual void ExecuteInTransaction(Action action)
    {
        using var tx = Context.Database.BeginTransaction();
        try
        {
            action();
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public virtual TResult ExecuteInTransaction<TResult>(Func<TResult> func)
    {
        using var tx = Context.Database.BeginTransaction();
        try
        {
            var result = func();
            tx.Commit();
            return result;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

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

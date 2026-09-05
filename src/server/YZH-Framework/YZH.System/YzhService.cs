using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using YZH.System.Entities;

namespace YZH.System
{
    /// <summary>
    /// YZH 通用数据服务：干净、可读、无反射魔法。
    /// 替代 VOL 的 ServiceBase / ApplicationServiceBase 反射式通用逻辑。
    /// </summary>
    public class YzhService<T> where T : class
    {
        protected readonly YzhDbContext _db;

        public YzhService(YzhDbContext db)
        {
            _db = db;
        }

        protected DbSet<T> Set => _db.Set<T>();

        // ===================== 查询 =====================

        public (List<T> rows, int total) GetPage(PageQuery q)
        {
            IQueryable<T> query = Set.AsQueryable();

            if (q?.filter != null && q.filter.Count > 0)
            {
                var expr = BuildFilterExpression(q.filter);
                if (expr != null) query = query.Where(expr);
            }

            int total = query.Count();

            if (!string.IsNullOrEmpty(q?.sort))
            {
                query = ApplySort(query, q.sort, q.order);
            }

            int page = q?.page ?? 1;
            int size = q?.rows ?? 20;
            if (page < 1) page = 1;
            if (size < 1) size = 20;

            var rows = query.Skip((page - 1) * size).Take(size).ToList();
            return (rows, total);
        }

        public List<T> GetAll()
        {
            return Set.AsNoTracking().ToList();
        }

        public T Get(object key)
        {
            return Set.Find(key);
        }

        // ===================== 写操作 =====================

        public void Add(T entity)
        {
            Set.Add(entity);
            _db.SaveChanges();
        }

        public void Update(T entity)
        {
            Set.Update(entity);
            _db.SaveChanges();
        }

        public void Delete(Expression<Func<T, bool>> predicate)
        {
            var list = Set.Where(predicate).ToList();
            if (list.Count > 0)
            {
                Set.RemoveRange(list);
                _db.SaveChanges();
            }
        }

        // ===================== 过滤表达式构建 =====================

        private Expression<Func<T, bool>> BuildFilterExpression(List<FilterItem> filters)
        {
            Expression combined = null;
            var param = Expression.Parameter(typeof(T), "x");

            foreach (var f in filters)
            {
                if (string.IsNullOrWhiteSpace(f?.Name) || string.IsNullOrWhiteSpace(f.Value))
                    continue;

                var prop = typeof(T).GetProperty(f.Name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (prop == null) continue;

                var member = Expression.Property(param, prop);
                Expression cond = null;

                if (prop.PropertyType == typeof(string))
                {
                    var constant = Expression.Constant(f.Value, typeof(string));
                    if (f.DisplayType == "==" || f.DisplayType == "=")
                    {
                        cond = Expression.Equal(member, constant);
                    }
                    else
                    {
                        // contains / like / 默认
                        var containsMethod = typeof(string).GetMethod("Contains", new[] { typeof(string) });
                        cond = Expression.Call(member, containsMethod, constant);
                    }
                }
                else
                {
                    // 值类型：相等比较
                    var underlying = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                    object converted;
                    try { converted = Convert.ChangeType(f.Value, underlying); }
                    catch { continue; }
                    var constant = Expression.Constant(converted, underlying);
                    var body = prop.PropertyType == underlying
                        ? (Expression)constant
                        : Expression.Convert(constant, prop.PropertyType);
                    cond = Expression.Equal(member, body);
                }

                combined = combined == null ? cond : Expression.AndAlso(combined, cond);
            }

            if (combined == null) return null;
            return Expression.Lambda<Func<T, bool>>(combined, param);
        }

        // ===================== 排序 =====================

        private IQueryable<T> ApplySort(IQueryable<T> query, string field, string order)
        {
            var prop = typeof(T).GetProperty(field, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop == null) return query;

            var param = Expression.Parameter(typeof(T), "x");
            var member = Expression.Property(param, prop);
            var lambda = Expression.Lambda(member, param);

            string method = (order ?? "desc").ToLower() == "asc" ? "OrderBy" : "OrderByDescending";
            var methodInfo = typeof(Queryable).GetMethods()
                .First(m => m.Name == method && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(T), prop.PropertyType);

            return (IQueryable<T>)methodInfo.Invoke(null, new object[] { query, lambda });
        }
    }
}

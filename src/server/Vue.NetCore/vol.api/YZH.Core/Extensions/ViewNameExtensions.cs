using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using YZH.Entity;

namespace YZH.Core.Extensions
{
    /// <summary>
    /// YZH Framework - 视图名扩展方法
    /// 
    /// 架构说明：
    /// - 实体类可同时标记 [TableName]（用于 CRUD）和 [ViewName]（用于查询）
    /// - 查询时：自动使用 FromSqlRaw 路由到视图查询
    /// - 所有 [NotMapped] 字段会从视图结果中填充
    /// - SaveChanges 时：[NotMapped] 字段自动忽略，数据写入 TABLE
    /// 
    /// MySQL 注意事项：
    /// - 使用 FromSqlRaw + 反引号包裹视图名
    /// - Pomelo EF Core 8 需要将视图查询作为子查询源
    /// </summary>
    public static class ViewNameExtensions
    {
        /// <summary>
        /// 获取实体类的 ViewName 标记值
        /// </summary>
        public static string GetViewName<T>() where T : class
        {
            return GetViewName(typeof(T));
        }

        /// <summary>
        /// 获取实体类的 ViewName 标记值
        /// </summary>
        public static string GetViewName(Type entityType)
        {
            var entityAttr = entityType.GetCustomAttribute<EntityAttribute>();
            if (entityAttr != null && !string.IsNullOrEmpty(entityAttr.ViewName))
            {
                return entityAttr.ViewName;
            }

            var viewNameAttr = entityType.GetCustomAttribute<ViewNameAttribute>();
            return viewNameAttr?.Name;
        }

        /// <summary>
        /// 判断实体是否配置了 ViewName
        /// </summary>
        public static bool HasViewName<T>() where T : class
        {
            return !string.IsNullOrEmpty(GetViewName<T>());
        }

        /// <summary>
        /// 如果实体配置了 ViewName，则自动路由到视图查询。
        /// 否则返回原 DbSet。
        /// 
        /// MySQL 实现：使用 FromSqlRaw + 反引号，避免 EF 参数化问题
        /// 
        /// 生成 SQL（MySQL）：
        /// SELECT * FROM (SELECT * FROM `v_iso_standard`) AS `x` WHERE ...
        /// </summary>
        public static IQueryable<T> UseViewIfExists<T>(this Microsoft.EntityFrameworkCore.DbSet<T> dbSet) where T : class
        {
            var viewName = GetViewName<T>();
            if (!string.IsNullOrEmpty(viewName))
            {
                // 使用 FromSqlRaw 直接嵌入视图名（MySQL 需要反引号）
                return dbSet.FromSqlRaw($"SELECT * FROM `{viewName}`");
            }
            return dbSet;
        }

        /// <summary>
        /// 获取分页查询的 IQueryable（自动路由到视图）
        /// </summary>
        public static IQueryable<T> GetViewQueryable<T>(this Microsoft.EntityFrameworkCore.DbContext context) where T : class
        {
            return context.Set<T>().UseViewIfExists();
        }
    }

    /// <summary>
    /// 独立视图名称 Attribute（当 EntityAttribute 无法满足时使用）
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class ViewNameAttribute : Attribute
    {
        public string Name { get; }
        public ViewNameAttribute(string name) => Name = name;
    }
}

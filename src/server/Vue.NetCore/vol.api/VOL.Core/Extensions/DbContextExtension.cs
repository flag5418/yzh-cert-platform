﻿using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using VOL.Core.BaseProvider;
using VOL.Core.EFDbContext;

namespace VOL.Core.Extensions
{
    public static class DbContextExtension
    {
        public static TFind FindById<TFind>(this BaseDbContext dbContext, object id) where TFind : class
        {
            return dbContext.QueryOriginListByKeys<TFind>([id],false)?.FirstOrDefault();
        }

        public static async Task<TFind> FindByIdAsync<TFind>(this BaseDbContext dbContext, object id) where TFind : class
        {
            return (await dbContext.QueryOriginListByKeysAsync<TFind>([id],false))?.FirstOrDefault();
        }
        public static int Update<TSource>(this BaseDbContext dbContext, TSource entity, string[] properties, bool saveChanges = false) where TSource : class
        {
            return dbContext.UpdateRange<TSource>(new List<TSource>() { entity }, properties, saveChanges);
        }
        public static int Update<TSource>(this BaseDbContext dbContext, TSource entity, bool saveChanges = false) where TSource : class
        {
            return dbContext.UpdateRange<TSource>(new List<TSource>() { entity }, new string[0], saveChanges);
        }
        public static int UpdateRange<TSource>(this BaseDbContext dbContext, IEnumerable<TSource> entities, Expression<Func<TSource, object>> properties, bool saveChanges = false) where TSource : class
        {
            return dbContext.UpdateRange<TSource>(entities, properties?.GetExpressionProperty(), saveChanges);
        }
        public static int UpdateRange<TSource>(this BaseDbContext dbContext, IEnumerable<TSource> entities, bool saveChanges = false) where TSource : class
        {
            return dbContext.UpdateRange<TSource>(entities, new string[0], saveChanges);
        }


        public static int UpdateRange<TSource>(this BaseDbContext dbContext, IEnumerable<TSource> entities, string[] properties, bool saveChanges = false) where TSource : class
        {
            if (properties != null && properties.Length > 0)
            {
                PropertyInfo[] entityProperty = typeof(TSource).GetProperties()
                        .Where(x => x.GetCustomAttribute<NotMappedAttribute>() == null).ToArray();
                string keyName = entityProperty.GetKeyName();
                if (properties.Contains(keyName))
                {
                    properties = properties.Where(x => x != keyName).ToArray();
                }
                properties = properties.Where(x => entityProperty.Select(s => s.Name).Contains(x)).ToArray();
            }
            foreach (TSource item in entities)
            {
                if (properties == null || properties.Length == 0)
                {
                    dbContext.Entry<TSource>(item).State = EntityState.Modified;
                    continue;
                }
                var entry = dbContext.Entry(item);
                properties.ToList().ForEach(x =>
                {
                    entry.Property(x).IsModified = true;
                });
            }
            if (!saveChanges)
            {
                return 0;
            }
            else
            {
                dbContext.SaveChanges();
            }
            return entities.Count();
        }


        /// <summary>
        /// 通过主键批量删除
        /// </summary>
        /// <param name="keys">主键key</param>
        /// <param name="delList">是否连明细一起删除</param>
        /// <returns></returns>
        public static int DeleteWithKeys<T>(this BaseDbContext dbContext, object[] keys, bool saveChange = false) where T : class
        {
            var keyPro = typeof(T).GetKeyProperty();
            foreach (var key in keys.Distinct())
            {
                T entity = Activator.CreateInstance<T>();
                keyPro.SetValue(entity, key.ChangeType(keyPro.PropertyType));
                dbContext.Entry<T>(entity).State = EntityState.Deleted;
            }
            if (saveChange)
            {
                dbContext.SaveChanges();
            }
            return keys.Length;
        }

        public static int Delete<T>(this BaseDbContext dbContext, [NotNull] Expression<Func<T, bool>> wheres, bool saveChange = false) where T : class
        {
            var keyProperty = typeof(T).GetKeyProperty();
            string keyName = typeof(T).GetKeyProperty().Name;
            var expression = keyName.GetExpression<T, object>();
            var ids = dbContext.Set<T>().Where(wheres).Select(expression).ToList();
            List<T> list = new List<T>();
            foreach (var id in ids)
            {
                T entity = Activator.CreateInstance<T>();
                keyProperty.SetValue(entity, id);
                list.Add(entity);
            }
            dbContext.RemoveRange(list);
            if (saveChange)
            {
                return dbContext.SaveChanges();
            }
            return 0;
        }
        /// <summary>
        /// 过滤逻辑删除 
        /// 
        /// YZH 扩展（2026-08-07）：
        /// - 原始实现直接 return query，不做任何过滤
        /// - 现在自动检测实体是否有 Enable 属性（bool 类型）
        /// - 有 Enable 属性 → 追加 WHERE Enable = true 过滤条件
        /// - 无 Enable 属性 → 保持原行为（不过滤）
        /// 
        /// 这样 YZHBaseEntity 子类的软删除记录（Enable=false）不会出现在查询结果中
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="query"></param>
        /// <returns></returns>
        public static IQueryable<T> FilterLogicDel<T>(this IQueryable<T> query) where T : class
        {
            var logicDelProperty = typeof(T).GetLogicDelPropertyWithType();
            if (logicDelProperty == null)
            {
                return query;
            }

            // 构建 WHERE Enable == true 表达式
            var parameter = Expression.Parameter(typeof(T), "x");
            var propertyAccess = Expression.Property(parameter, logicDelProperty);
            var trueValue = Expression.Constant(true, typeof(bool));
            var equalExpr = Expression.Equal(propertyAccess, trueValue);
            var lambda = Expression.Lambda<Func<T, bool>>(equalExpr, parameter);

            return query.Where(lambda);
        }
    }
}

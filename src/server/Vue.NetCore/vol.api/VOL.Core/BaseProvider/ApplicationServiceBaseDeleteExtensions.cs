using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VOL.Core.EFDbContext;
using VOL.Core.Enums;
using VOL.Core.Extensions;
using VOL.Entity;

namespace VOL.Core.BaseProvider
{
    public static class ApplicationServiceBaseDeleteExtensions
    {
        private static MethodInfo[] Methods
        {
            get
            {
                return typeof(ApplicationServiceBaseDeleteExtensions)
                       .GetMethods(BindingFlags.Public | BindingFlags.Static);
            }
        }
        public static int DeleteWithType(this BaseDbContext dbContext, Type entityType, List<object> keys, PropertyInfo keyPro = null, bool logicDel = true)
        {
            if (keys == null || keys.Count == 0) return 0;
            int delCount = 0;
            keyPro ??= entityType.GetKeyProperty();

            var logicDelProperty = entityType.GetLogicDelPropertyWithType();

            if (logicDel && logicDelProperty != null)
            {
                delCount = Methods.First(m => m.Name == nameof(LogicDelete))
                          .MakeGenericMethod(entityType, keyPro.PropertyType, logicDelProperty.PropertyType)
                          .Invoke(null, [dbContext, keys, keyPro.Name, logicDelProperty]).GetInt();
            }
            else
            {
                delCount = Methods.First(m => m.Name == nameof(Delete))
                          .MakeGenericMethod(entityType, keyPro.PropertyType)
                          .Invoke(null, [dbContext, keys, keyPro.Name]).GetInt();
            }
            return delCount;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dbContext"></param>
        /// <param name="entityType"></param>
        /// <param name="keys"></param>
        /// <param name="keyPro"></param>
        /// <param name="logicDel"></param>
        /// <returns></returns>
        public static async Task<int> DeleteWithTypeAsync(this BaseDbContext dbContext,
            Type entityType,
            List<object> keys,
            PropertyInfo keyPro = null,
            bool logicDel = true)
        {
            if (keys == null || keys.Count == 0) return 0;
            int delCount = 0;
            keyPro ??= entityType.GetKeyProperty();

            var logicDelProperty = entityType.GetLogicDelPropertyWithType();
            if (logicDel && logicDelProperty != null)
            {
                delCount = await (Methods
                      .First(m => m.Name == nameof(LogicDeleteAsync))
                       .MakeGenericMethod(entityType, keyPro.PropertyType, logicDelProperty.PropertyType)
                      .Invoke(null, [dbContext, keys, keyPro.Name, logicDelProperty]) as Task<int>);
            }
            else
            {
                delCount = await (Methods
                    .First(m => m.Name == nameof(DeleteAsync))
                    .MakeGenericMethod(entityType, keyPro.PropertyType)
                    .Invoke(null, [dbContext, keys, keyPro.Name]) as Task<int>);
            }
            return delCount;
        }
        /// <summary>
        /// 删除二、三级明细表
        /// </summary>
        /// <param name="dbContext"></param>
        /// <param name="entityType"></param>
        /// <param name="keyList"></param>
        /// <returns></returns>
        public static async Task<int> DeleteAllDetilWithTypeAsync(this BaseDbContext dbContext, Type entityType, List<object> keyList)
        {
            int delCount = 0;
            if (keyList.Count == 0)
            {
                return delCount;
            }
            var detailTypes = entityType.GetCustomAttribute<EntityAttribute>()?.DetailTable ?? [];
            if (detailTypes.Length == 0) return delCount;

            foreach (var detailType in detailTypes)
            {
                //删除三级明细表
                var detailFkToMain = entityType.GetForeignKeyType(detailType, detailType.Name);
                var subTypes = detailType.GetCustomAttribute<EntityAttribute>()?.DetailTable ?? [];
                if (subTypes.Length > 0)
                {
                    var detailKeyPro = detailType.GetKeyProperty();
                    var detailKeys = await dbContext.QueryKeysByForeignKeysAsync(detailType, detailKeyPro, detailFkToMain, keyList);
                    if (detailKeys.Count > 0)
                    {
                        foreach (var subType in subTypes)
                        {
                            var subFkToDetail = detailType.GetForeignKeyType(subType, subType.Name);
                            await dbContext.DeleteWithTypeAsync(subType, detailKeys, subFkToDetail, true);
                        }
                    }
                }
                //删除二级明细表
                delCount += await dbContext.DeleteWithTypeAsync(detailType, keyList, detailFkToMain, true);
            }
            return delCount;
        }

        public static int DeleteAllDetilWithType(this BaseDbContext dbContext, Type entityType, List<object> keyList)
        {
            int delCount = 0;
            if (keyList.Count == 0)
            {
                return delCount;
            }
            var detailTypes = entityType.GetCustomAttribute<EntityAttribute>()?.DetailTable ?? [];
            if (detailTypes.Length == 0) return delCount;

            foreach (var detailType in detailTypes)
            {
                //删除三级明细表
                var detailFkToMain = entityType.GetForeignKeyType(detailType, detailType.Name);
                var subTypes = detailType.GetCustomAttribute<EntityAttribute>()?.DetailTable ?? [];
                if (subTypes.Length > 0)
                {
                    var detailKeyPro = detailType.GetKeyProperty();
                    var detailKeys = dbContext.QueryKeysByForeignKeys(detailType, detailKeyPro, detailFkToMain, keyList);
                    if (detailKeys.Count > 0)
                    {
                        foreach (var subType in subTypes)
                        {
                            var subFkToDetail = detailType.GetForeignKeyType(subType, subType.Name);
                            dbContext.DeleteWithType(subType, detailKeys, subFkToDetail, true);
                        }
                    }
                }
                //删除二级明细表
                delCount += dbContext.DeleteWithType(detailType, keyList, detailFkToMain, true);
            }
            return delCount;
        }

        public static int Delete<TEntity, TKey>(this BaseDbContext dbContext, List<object> keys, string keyName)
         where TEntity : class
        {
            var expression = CreateExpression<TEntity, TKey>(keyName, keys);
            if (expression == null) return 0;
            return dbContext.Set<TEntity>().Where(expression).ExecuteDelete();
        }

        public static async Task<int> DeleteAsync<TEntity, TKey>(this BaseDbContext dbContext, List<object> keys, string keyName)
          where TEntity : class
        {
            var expression = CreateExpression<TEntity, TKey>(keyName, keys);
            if (expression == null) return 0;
            return await dbContext.Set<TEntity>().Where(expression).ExecuteDeleteAsync();
        }
        private static Expression<Func<TEntity, bool>> CreateExpression<TEntity, TKey>(string keyName, List<object> keys)
          where TEntity : class
        {
            if (keys == null) return null;
            var values = keys.Where(k => k != null).Select(k => (TKey)k.ChangeType(typeof(TKey))).ToList();
            if (values.Count == 0) return null;
            return keyName.CreateExpression<TEntity>(values, LinqExpressionType.In);
        }

        /// <summary>
        /// 逻辑删除
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <typeparam name="TKey"></typeparam>
        /// <param name="dbContext"></param>
        /// <param name="keys"></param>
        /// <param name="keyName"></param>
        /// <returns></returns>
        public static int LogicDelete<TEntity, TKey, LogicFieldType>(this BaseDbContext dbContext,
            List<object> keys,
            string keyName,
            PropertyInfo logicDelProperty)
           where TEntity : class
        {
            var expression = CreateExpression<TEntity, TKey>(keyName, keys);
            if (expression == null) return 0;
            var value = (LogicFieldType)(((int)DelStatus.已删除).ChangeType(typeof(LogicFieldType)));
            dbContext
                .Set<TEntity>()
                .Where(expression)
               .ExecuteUpdate(c => c.SetProperty(e => EF.Property<LogicFieldType>(e, logicDelProperty.Name),
                value));
            return 0;
        }

        /// <summary>
        /// ??潩
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <typeparam name="TKey"></typeparam>
        /// <param name="dbContext"></param>
        /// <param name="keys"></param>
        /// <param name="keyName"></param>
        /// <returns></returns>
        public static async Task<int> LogicDeleteAsync<TEntity, TKey, LogicFieldType>(this BaseDbContext dbContext,
            List<object> keys,
            string keyName,
            PropertyInfo logicDelProperty)
            where TEntity : class
        {
            var expression = CreateExpression<TEntity, TKey>(keyName, keys);
            if (expression == null) return 0;
            var value = (LogicFieldType)(((int)DelStatus.已删除).ChangeType(typeof(LogicFieldType)));
            await dbContext
                   .Set<TEntity>()
                   .Where(expression)
                  .ExecuteUpdateAsync(c => c.SetProperty(e => EF.Property<LogicFieldType>(e, logicDelProperty.Name),
                   value));
            return 0;
        }


        public static async Task<List<object>> QueryKeysByForeignKeysAsync(
              this BaseDbContext dbContext,
              Type entityType,
              PropertyInfo selectKeyProperty,
              PropertyInfo whereForeignKeyProperty,
              List<object> foreignKeyValues)
        {
            var method = typeof(ApplicationServiceBaseDeleteExtensions)
                .GetMethod(nameof(QueryKeysByForeignKeysCore), BindingFlags.NonPublic | BindingFlags.Static);

            var generic = method.MakeGenericMethod(entityType, whereForeignKeyProperty.PropertyType, selectKeyProperty.PropertyType);
            var query = generic.Invoke(null,
            [
                dbContext,
                whereForeignKeyProperty.Name,
                foreignKeyValues,
                selectKeyProperty.Name
            ]) as IQueryable<object>;
            return await query.ToListAsync();
        }

        public static List<object> QueryKeysByForeignKeys(
        this BaseDbContext dbContext,
        Type entityType,
        PropertyInfo selectKeyProperty,
        PropertyInfo whereForeignKeyProperty,
        List<object> foreignKeyValues)
        {
            var method = typeof(ApplicationServiceBaseDeleteExtensions)
                .GetMethod(nameof(QueryKeysByForeignKeysCore), BindingFlags.NonPublic | BindingFlags.Static);

            var generic = method.MakeGenericMethod(entityType, whereForeignKeyProperty.PropertyType, selectKeyProperty.PropertyType);
            var query = generic.Invoke(null,
            [
                dbContext,
                whereForeignKeyProperty.Name,
                foreignKeyValues,
                selectKeyProperty.Name
            ]) as IQueryable<object>;
            return query.ToList();
        }

        private static IQueryable<object> QueryKeysByForeignKeysCore<TQueryEntity, TWhereKey, TSelectKey>(
            BaseDbContext dbContext,
            string whereForeignKeyName,
            List<object> foreignKeyValues,
            string selectKeyName)
            where TQueryEntity : class
        {
            var values = foreignKeyValues
                .Where(k => k != null)
                .Select(k => (TWhereKey)k.ChangeType(typeof(TWhereKey)))
                .ToList();
            var predicate = whereForeignKeyName.CreateExpression<TQueryEntity>(values, LinqExpressionType.In);
            return dbContext.Set<TQueryEntity>()
                .Where(predicate)
                .Select(e => EF.Property<TSelectKey>(e, selectKeyName))
                .Select(v => (object)v);
        }
        /// <summary>
        /// 获取删除前的原始数据
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="dbContext"></param>
        /// <param name="keyList"></param>
        /// <returns></returns>
        public static List<TEntity> QueryOriginListByKeys<TEntity>(
         this BaseDbContext dbContext,
         List<object> keyList,
         bool checkActionLog = true)
         where TEntity : class
        {
            if (keyList == null || keyList.Count == 0) return [];
            if (checkActionLog)
            {
                //没有审计日志的不记录
                bool isActionLog = typeof(TEntity).IsTableActionLog();
                if (!isActionLog) return null;
            }
            var keyPro = typeof(TEntity).GetKeyProperty();
            var method = typeof(ApplicationServiceBaseDeleteExtensions)
                .GetMethod(nameof(QueryOriginListByKeysCoreAsync), BindingFlags.NonPublic | BindingFlags.Static);
            var generic = method.MakeGenericMethod(typeof(TEntity), keyPro.PropertyType);
            var query = generic.Invoke(null,
            [
                dbContext,
                keyPro.Name,
                keyList
            ]) as IQueryable<TEntity>;
            return query.ToList();
        }

        public static async Task<List<TEntity>> QueryOriginListByKeysAsync<TEntity>(
       this BaseDbContext dbContext,
       List<object> keyList,
        bool checkActionLog = true)
       where TEntity : class
        {
            if (keyList == null || keyList.Count == 0) return [];
            if (checkActionLog)
            {
                //没有审计日志的不记录
                bool isActionLog = typeof(TEntity).IsTableActionLog();
                if (!isActionLog) return null;
            }
            var keyPro = typeof(TEntity).GetKeyProperty();
            var method = typeof(ApplicationServiceBaseDeleteExtensions)
                .GetMethod(nameof(QueryOriginListByKeysCoreAsync), BindingFlags.NonPublic | BindingFlags.Static);
            var generic = method.MakeGenericMethod(typeof(TEntity), keyPro.PropertyType);
            var query = generic.Invoke(null,
            [
                dbContext,
                keyPro.Name,
                keyList
            ]) as IQueryable<TEntity>;
            return await query.ToListAsync();
        }

        private static IQueryable<TEntity> QueryOriginListByKeysCoreAsync<TEntity, TKey>(
            BaseDbContext dbContext,
            string keyName,
            List<object> keyList)
            where TEntity : class
        {
            var values = keyList
                .Where(k => k != null)
                .Select(k => (TKey)k.ChangeType(typeof(TKey)))
                .ToList();
            var predicate = keyName.CreateExpression<TEntity>(values, LinqExpressionType.In);
            return dbContext.Set<TEntity>().Where(predicate);
        }

    }
}
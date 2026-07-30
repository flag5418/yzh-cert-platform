using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using VOL.Core.Configuration;
using VOL.Core.Dapper;
using VOL.Core.DBManager;
using VOL.Core.EFDbContext;
using VOL.Core.Enums;
using VOL.Core.Extensions;
using VOL.Core.Services;
using VOL.Core.Utilities;
using VOL.Entity;
using VOL.Entity.SystemModels;

namespace VOL.Core.BaseProvider
{
    public abstract class RepositoryBase<TEntity> where TEntity : BaseEntity
    {
        public RepositoryBase()
        {
        }
        public RepositoryBase(BaseDbContext dbContext)
        {
            DefaultDbContext = dbContext ?? throw new Exception("dbContext未实例化。");
        }

        private BaseDbContext DefaultDbContext { get; set; }
        private BaseDbContext EFContext
        {
            get
            {
                return DefaultDbContext;
            }
        }

        public virtual BaseDbContext DbContext
        {
            get { return DefaultDbContext; }
        }
        private DbSet<TEntity> DBSet
        {
            get { return EFContext.Set<TEntity>(); }
        }
        private ISqlDapper _dapper = null;
        public ISqlDapper DapperContext
        {
            get
            {
                _dapper ??= DBServerProvider.GetSqlDapper<TEntity>();
                return _dapper;
            }
        }
        /// <summary>
        /// 执行事务
        /// </summary>
        /// <param name="action">如果返回false则回滚事务(可自行定义规则)</param>
        /// <returns></returns>
        public virtual WebResponseContent DbContextBeginTransaction(Func<WebResponseContent> action)
        {
            if (DefaultDbContext.Database.CurrentTransaction != null) return action();
            WebResponseContent webResponse = new WebResponseContent();
            using IDbContextTransaction transaction = DefaultDbContext.Database.BeginTransaction();
            try
            {
                webResponse = action();
                if (webResponse.Status)
                {
                    transaction.Commit();
                }
                else
                {
                    transaction.Rollback();
                }

                return webResponse;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                string message = ex.Message + ex?.InnerException + ex?.StackTrace;
                if (HttpContext.Current.GetService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>().IsDevelopment())
                {
                    return webResponse.Error(message);
                }
                Logger.Error(message);
                return webResponse.Error("处理异常");
            }
        }
        public virtual async Task<WebResponseContent> DbContextBeginTransactionAsync(Func<Task<WebResponseContent>> action)
        {
            if (DefaultDbContext.Database.CurrentTransaction != null) return await action();
            WebResponseContent webResponse = new WebResponseContent();
            using IDbContextTransaction transaction = await DefaultDbContext.Database.BeginTransactionAsync();
            try
            {
                webResponse = await action();
                if (webResponse.Status)
                {
                    await transaction.CommitAsync();
                }
                else
                {
                    await transaction.RollbackAsync();
                }

                return webResponse;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                string message = ex.Message + ex?.InnerException + ex?.StackTrace;
                if (HttpContext.Current.GetService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>().IsDevelopment())
                {
                    return webResponse.Error(message);
                }
                Logger.Error(message);
                return webResponse.Error("处理异常");
            }
        }
        public virtual bool Exists<TExists>(Expression<Func<TExists, bool>> predicate, bool filterDeleted = true) where TExists : class
        {
            return EFContext.Set<TExists>().Any(predicate);
        }

        public virtual Task<bool> ExistsAsync<TExists>(Expression<Func<TExists, bool>> predicate, bool filterDeleted = true) where TExists : class
        {
            return EFContext.Set<TExists>(filterDeleted).AnyAsync(predicate);
        }

        public virtual bool Exists(Expression<Func<TEntity, bool>> predicate, bool filterDeleted = true)
        {
            return EFContext.Set<TEntity>(filterDeleted).Any(predicate);
        }

        public virtual Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate, bool filterDeleted = true)
        {
            return EFContext.Set<TEntity>(filterDeleted).AnyAsync(predicate);
        }
        /// <summary>
        /// 查询字段不为null或者为空
        /// </summary>
        /// <param name="field">x=>new {x.字段}</param>
        /// <param name="value">查询的类</param>
        /// <param name="linqExpression">查询类型</param>
        /// <returns></returns>
        public virtual IQueryable<TEntity> WhereIF([NotNull] Expression<Func<TEntity, object>> field, string value, LinqExpressionType linqExpression = LinqExpressionType.Equal)
        {
            return EFContext.Set<TEntity>().WhereNotEmpty(field, value, linqExpression);
        }

        public virtual IQueryable<TEntity> WhereIF(bool checkCondition, Expression<Func<TEntity, bool>> predicate)
        {
            if (checkCondition)
            {
                return EFContext.Set<TEntity>().Where(predicate);
            }
            return EFContext.Set<TEntity>();
        }

        public virtual IQueryable<T> WhereIF<T>(bool checkCondition, Expression<Func<T, bool>> predicate) where T : class
        {
            if (checkCondition)
            {
                return EFContext.Set<T>().Where(predicate);
            }
            return EFContext.Set<T>();
        }

        public virtual TFind FindById<TFind>(object id) where TFind : class
        {
            return EFContext.FindById<TFind>(id);
        }

        public virtual async Task<TFind> FindByIdAsync<TFind>(List<object> id) where TFind : class
        {
            return await EFContext.FindByIdAsync<TFind>(id);
        }
        public virtual List<TFind> Find<TFind>(Expression<Func<TFind, bool>> predicate, bool filterDeleted = true) where TFind : class
        {
            return EFContext.Set<TFind>(filterDeleted).Where(predicate).ToList();
        }

        public virtual Task<TFind> FindAsyncFirst<TFind>(Expression<Func<TFind, bool>> predicate, bool filterDeleted = true) where TFind : class
        {
            return FindAsIQueryable(predicate, filterDeleted).FirstOrDefaultAsync();
        }

        public virtual Task<TEntity> FindAsyncFirst(Expression<Func<TEntity, bool>> predicate, bool filterDeleted = true)
        {
            return FindAsIQueryable<TEntity>(predicate, filterDeleted).FirstOrDefaultAsync();
        }

        public virtual Task<List<TFind>> FindAsync<TFind>(Expression<Func<TFind, bool>> predicate, bool filterDeleted = true) where TFind : class
        {
            return FindAsIQueryable<TFind>(predicate, filterDeleted).ToListAsync();
        }

        public virtual Task<List<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate, bool filterDeleted = true)
        {
            return FindAsIQueryable(predicate, filterDeleted).ToListAsync();
        }

        public virtual Task<TEntity> FindFirstAsync(Expression<Func<TEntity, bool>> predicate, bool filterDeleted = true)
        {
            return FindAsIQueryable(predicate, filterDeleted).FirstOrDefaultAsync();
        }

        public virtual Task<List<T>> FindAsync<T>(Expression<Func<TEntity, bool>> predicate, Expression<Func<TEntity, T>> selector, bool filterDeleted = true)
        {
            return FindAsIQueryable(predicate, filterDeleted).Select(selector).ToListAsync();
        }

        public virtual Task<T> FindFirstAsync<T>(Expression<Func<TEntity, bool>> predicate, Expression<Func<TEntity, T>> selector, bool filterDeleted = true)
        {
            return FindAsIQueryable(predicate, filterDeleted).Select(selector).FirstOrDefaultAsync();
        }

        private IQueryable<TFind> FindAsIQueryable<TFind>(Expression<Func<TFind, bool>> predicate, bool filterDeleted = true) where TFind : class
        {
            return EFContext.Set<TFind>(filterDeleted).Where(predicate);
        }


        public virtual List<T> Find<T>(Expression<Func<TEntity, bool>> predicate, Expression<Func<TEntity, T>> selector, bool filterDeleted = true)
        {
            return EFContext.Set<TEntity>(filterDeleted).Where(predicate).Select(selector).ToList();
        }
        /// <summary>
        /// 单表查询
        /// </summary>
        /// <param name="predicate"></param>
        /// <returns></returns>
        public virtual List<TEntity> Find(Expression<Func<TEntity, bool>> predicate, bool filterDeleted = true)
        {
            return FindAsIQueryable(predicate, filterDeleted).ToList();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="predicate"></param>
        /// <param name=""></param>
        /// <param name="orderBy">排序字段</param>
        /// <returns></returns>
        public virtual TEntity FindFirst(Expression<Func<TEntity, bool>> predicate, bool filterDeleted = true)
        {
            return EFContext.Set<TEntity>(filterDeleted).Where(predicate).FirstOrDefault();
        }


        public IQueryable<TEntity> FindAsIQueryable(Expression<Func<TEntity, bool>> predicate, Expression<Func<TEntity, Dictionary<object, QueryOrderBy>>> orderBy = null, bool filterDeleted = true)
        {
            if (orderBy != null)
                return DbContext.Set<TEntity>(filterDeleted).Where(predicate).GetIQueryableOrderBy(orderBy.GetExpressionToDic());
            return DbContext.Set<TEntity>(filterDeleted).Where(predicate);
        }

        public IIncludableQueryable<TEntity, TProperty> Include<TProperty>(Expression<Func<TEntity, TProperty>> incluedProperty)
        {
            return DbContext.Set<TEntity>().Include(incluedProperty);
        }

        /// <summary>
        /// 通过条件查询返回指定列的数据(将TEntity映射到匿名或实体T)
        ///var result = Sys_UserRepository.GetInstance.Find(x => x.UserName == loginInfo.userName, p => new { uname = p.UserName });
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <param name="pageIndex"></param>
        /// <param name="pagesize"></param>
        /// <param name="rowcount"></param>
        /// <param name="predicate">查询条件</param>
        /// <param name="orderBySelector">多个排序字段key为字段，value为升序/降序</param>
        /// <returns></returns>
        public virtual IQueryable<TFind> IQueryablePage<TFind>(int pageIndex, int pagesize, out int rowcount, Expression<Func<TFind, bool>> predicate, Expression<Func<TEntity, Dictionary<object, QueryOrderBy>>> orderBy, bool returnRowCount = true) where TFind : class
        {
            pageIndex = pageIndex <= 0 ? 1 : pageIndex;
            pagesize = pagesize <= 0 ? 10 : pagesize;
            if (predicate == null)
            {
                predicate = x => 1 == 1;
            }
            var _db = DbContext.Set<TFind>();
            rowcount = returnRowCount ? _db.Count(predicate) : 0;
            return DbContext.Set<TFind>().Where(predicate)
                .GetIQueryableOrderBy(orderBy.GetExpressionToDic())
                .Skip((pageIndex - 1) * pagesize)
                .Take(pagesize);
        }

        /// <summary>
        /// 分页排序
        /// </summary>
        /// <param name="queryable"></param>
        /// <param name="pageIndex"></param>
        /// <param name="pagesize"></param>
        /// <param name="rowcount"></param>
        /// <param name="orderBy"></param>
        /// <returns></returns>
        public virtual IQueryable<TEntity> IQueryablePage(IQueryable<TEntity> queryable, int pageIndex, int pagesize, out int rowcount, Dictionary<string, QueryOrderBy> orderBy, bool returnRowCount = true)
        {
            pageIndex = pageIndex <= 0 ? 1 : pageIndex;
            pagesize = pagesize <= 0 ? 10 : pagesize;
            rowcount = returnRowCount ? queryable.Count() : 0;
            return queryable.GetIQueryableOrderBy<TEntity>(orderBy)
                .Skip((pageIndex - 1) * pagesize)
                .Take(pagesize);
        }



        /// <summary>
        /// 更新表数据
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="saveChanges">是否保存</param>
        /// <param name="properties">格式 Expression<Func<entityt, object>> expTree = x => new { x.字段1, x.字段2 };</param>
        public virtual int Update(TEntity entity, Expression<Func<TEntity, object>> properties, bool saveChanges = false)
        {
            return Update<TEntity>(entity, properties, saveChanges);
        }

        public virtual int Update<TSource>(TSource entity, Expression<Func<TSource, object>> properties, bool saveChanges = false) where TSource : class
        {
            return UpdateRange(new List<TSource>
            {
                entity
            }, properties, saveChanges);
        }


        public virtual int Update<TSource>(TSource entity, string[] properties, bool saveChanges = false) where TSource : class
        {
            return UpdateRange<TSource>(new List<TSource>() { entity }, properties, saveChanges);
        }
        public virtual int Update<TSource>(TSource entity, bool saveChanges = false) where TSource : class
        {
            return UpdateRange<TSource>(new List<TSource>() { entity }, new string[0], saveChanges);
        }
        public virtual int UpdateRange<TSource>(IEnumerable<TSource> entities, Expression<Func<TSource, object>> properties, bool saveChanges = false) where TSource : class
        {
            return UpdateRange<TSource>(entities, properties?.GetExpressionProperty(), saveChanges);
        }
        public virtual int UpdateRange<TSource>(IEnumerable<TSource> entities, bool saveChanges = false) where TSource : class
        {
            return UpdateRange<TSource>(entities, new string[0], saveChanges);
        }

        /// <summary>
        /// 更新表数据
        /// </summary>
        /// <param name="models"></param>
        /// <param name="properties">格式 Expression<Func<entityt, object>> expTree = x => new { x.字段1, x.字段2 };</param>
        public int UpdateRange<TSource>(IEnumerable<TSource> entities, string[] properties, bool saveChanges = false) where TSource : class
        {
            if (properties != null && properties.Length > 0)
            {
                PropertyInfo[] entityProperty = typeof(TSource).GetProperties()
                        .Where(x => x.GetCustomAttribute<NotMappedAttribute>() == null && !(x.PropertyType.IsGenericType && x.PropertyType.GetGenericTypeDefinition() == typeof(List<>))).ToArray();
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
                    DbContext.Entry<TSource>(item).State = EntityState.Modified;
                    continue;
                }
                var entry = DbContext.Entry(item);
                properties.ToList().ForEach(x =>
                {
                    entry.Property(x).IsModified = true;
                });
            }
            if (!saveChanges) return 0;

            //2020.04.24增加更新时并行重试处理
            try
            {
                // Attempt to save changes to the database
                return DbContext.SaveChanges();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                int affectedRows = 0;
                foreach (var entry in ex.Entries)
                {
                    var proposedValues = entry.CurrentValues;

                    var databaseValues = entry.GetDatabaseValues();
                    //databaseValues == null说明数据已被删除
                    if (databaseValues != null)
                    {
                        foreach (var property in properties == null
                            || properties.Length == 0 ? proposedValues.Properties
                            : proposedValues.Properties.Where(x => properties.Contains(x.Name)))
                        {
                            var proposedValue = proposedValues[property];
                            var databaseValue = databaseValues[property];
                        }
                        affectedRows++;
                        entry.OriginalValues.SetValues(databaseValues);
                    }
                }
                if (affectedRows == 0) return 0;

                return DbContext.SaveChanges();
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="updateDetail">是否修改明细</param>
        /// <param name="delNotExist">是否删除明细不存在的数据</param>
        /// <param name="updateMainFields">主表指定修改字段</param>
        /// <param name="updateDetailFields">明细指定修改字段</param>
        /// <param name="saveChange">是否保存</param>
        /// <returns></returns>
        public virtual WebResponseContent UpdateRange<Detail>(TEntity entity,
            bool updateDetail = false,
            bool delNotExist = false,
            Expression<Func<TEntity, object>> updateMainFields = null,
            Expression<Func<Detail, object>> updateDetailFields = null,
            bool saveChange = false) where Detail : class
        {
            WebResponseContent webResponse = new WebResponseContent();
            Update(entity, updateMainFields);
            string message = "";
            if (updateDetail)
            {
                PropertyInfo[] properties = typeof(TEntity).GetProperties();
                PropertyInfo detail = properties.Where(x => x.PropertyType.Name == "List`1").ToList().FirstOrDefault();
                if (detail != null)
                {
                    PropertyInfo key = properties.GetKeyProperty();
                    object obj = detail.GetValue(entity);
                    Type detailType = typeof(TEntity).GetCustomAttribute<EntityAttribute>().DetailTable[0];
                    message = UpdateDetail<Detail>(obj as List<Detail>, key.Name, key.GetValue(entity), updateDetailFields, delNotExist);
                }
            }
            if (!saveChange) return webResponse.OK();

            DbContext.SaveChanges();
            return webResponse.OK("修改成功,明细" + message, entity);
        }
        private string UpdateDetail<TDetail>(List<TDetail> list,
            string keyName,
            object keyValue,
            Expression<Func<TDetail, object>> updateDetailFields = null,
            bool delNotExist = false) where TDetail : class
        {
            if (list == null) return "";
            PropertyInfo property = typeof(TDetail).GetKeyProperty();
            string detailKeyName = property.Name;
            DbSet<TDetail> details = DbContext.Set<TDetail>();
            Expression<Func<TDetail, object>> selectExpression = detailKeyName.GetExpression<TDetail, object>();
            Expression<Func<TDetail, bool>> whereExpression = keyName.CreateExpression<TDetail>(keyValue, LinqExpressionType.Equal);

            List<object> detailKeys = details.Where(whereExpression).Select(selectExpression).ToList();

            //获取主键默认值
            string keyDefaultVal = property.PropertyType
                .Assembly
                .CreateInstance(property.PropertyType.FullName).ToString();
            int addCount = 0;
            int editCount = 0;
            int delCount = 0;
            PropertyInfo mainKeyProperty = typeof(TDetail).GetProperty(keyName);
            List<object> keys = new List<object>();
            list.ForEach(x =>
            {
                var set = DbContext.Set<TDetail>();
                object val = property.GetValue(x);
                //主键是默认值的为新增的数据
                if (val.ToString() == keyDefaultVal)
                {
                    x.SetCreateDefaultVal();
                    //设置主表的值，也可以不设置
                    mainKeyProperty.SetValue(x, keyValue);
                    details.Add(x);
                    addCount++;
                }
                else//修改的数据
                {
                    //获取所有修改的key,如果从数据库查来的key,不在修改中的key，则为删除的数据
                    keys.Add(val);
                    x.SetModifyDefaultVal();
                    Update<TDetail>(x, updateDetailFields);
                    //  repository.DbContext.Entry<TDetail>(x).State = EntityState.Modified;
                    editCount++;
                }
            });
            //删除
            if (delNotExist)
            {
                detailKeys.Where(x => !keys.Contains(x)).ToList().ForEach(d =>
                {
                    delCount++;
                    TDetail detail = Activator.CreateInstance<TDetail>();
                    property.SetValue(detail, d);
                    DbContext.Entry<TDetail>(detail).State = EntityState.Deleted;
                    for (int i = 0; i < list.Count(); i++)
                    {
                        if (property.GetValue(list[i]) == d)
                        {
                            list.RemoveAt(i);
                        }
                    }
                });
            }
            return $"修改[{editCount}]条,新增[{addCount}]条,删除[{delCount}]条";
        }

        public virtual void Delete(TEntity model, bool saveChanges)
        {
            DBSet.Remove(model);
            if (saveChanges)
            {
                DbContext.SaveChanges();
            }
        }
        /// <summary>
        /// 通过主键批量删除
        /// </summary>
        /// <param name="keys">主键key</param>
        /// <param name="delList">是否连明细一起删除</param>
        /// <returns></returns>
        public virtual int DeleteWithKeys(object[] keys, bool saveChange = false)
        {
            var keyPro = typeof(TEntity).GetKeyProperty();
            foreach (var key in keys.Distinct())
            {
                TEntity entity = Activator.CreateInstance<TEntity>();
                keyPro.SetValue(entity, key.ChangeType(keyPro.PropertyType));
                DbContext.Entry(entity).State = EntityState.Deleted;
            }
            if (saveChange)
            {
                DbContext.SaveChanges();
            }
            return keys.Length;
        }


        public virtual int Delete([NotNull] Expression<Func<TEntity, bool>> wheres, bool saveChange = true)
        {
            return Delete<TEntity>(wheres, saveChange);
        }
        public virtual int Delete<T>([NotNull] Expression<Func<T, bool>> wheres, bool saveChange = true) where T : class
        {
            return DbContext.Set<T>().Where(wheres).ExecuteDelete();
        }

        public virtual Task AddAsync(TEntity entities)
        {
            return DBSet.AddRangeAsync(entities);
        }

        public virtual Task AddRangeAsync(IEnumerable<TEntity> entities)
        {
            return DBSet.AddRangeAsync(entities);
        }

        public virtual void Add(TEntity entities, bool saveChanges = false)
        {
            AddRange([entities], saveChanges);
        }
        public virtual void Add<T>(T entity, bool saveChanges = false) where T : class
        {
            AddRange([entity], saveChanges);
        }
        public virtual void AddRange(IEnumerable<TEntity> entities, bool saveChanges = false)
        {
            AddRange<TEntity>(entities, saveChanges);
        }

        public virtual void AddRange<T>(IEnumerable<T> entities, bool saveChanges = false)
            where T : class
        {
            if (entities.Count() == 0)
            {
                return;
            }
            if (AppSetting.EnableSnowFlakeID)
            {
                PropertyInfo keyPro = typeof(T).GetKeyProperty();
                if (keyPro.PropertyType == typeof(long))
                {
                    //生成雪花id
                    var idWorker = new IdWorker();
                    foreach (var item in entities)
                    {
                        if (keyPro.GetValue(item).ToString().Length < 10)
                        {
                            keyPro.SetValue(item, idWorker.NextId());
                        }
                    }
                }
            }
            DbContext.Set<T>().AddRange(entities);
            if (saveChanges) DbContext.SaveChanges();
        }


        public virtual int SaveChanges()
        {
            return EFContext.SaveChanges();
        }

        public virtual Task<int> SaveChangesAsync()
        {
            return EFContext.SaveChangesAsync();
        }

        public virtual int ExecuteSqlCommand(string sql, params SqlParameter[] sqlParameters)
        {
            return DbContext.Database.ExecuteSqlRaw(sql, sqlParameters);
        }

        public virtual List<TEntity> FromSql(string sql, params SqlParameter[] sqlParameters)
        {
            return DBSet.FromSqlRaw(sql, sqlParameters).ToList();
        }

        /// <summary>
        /// 执行sql
        /// 使用方式 FormattableString sql=$"select * from xx where name ={xx} and pwd={xx1} "，
        /// FromSqlInterpolated内部处理sql注入的问题，直接在{xx}写对应的值即可
        /// 注意：sql必须 select * 返回所有TEntity字段，
        /// </summary>
        /// <param name="formattableString"></param>
        /// <returns></returns>
        public virtual IQueryable<TEntity> FromSqlInterpolated([NotNull] FormattableString sql)
        {
            return DBSet.FromSqlInterpolated(sql);
        }

        /// <summary>
        /// 取消上下文跟踪
        /// </summary>
        /// <param name="entity"></param>
        public virtual void Detached(TEntity entity)
        {
            DbContext.Entry(entity).State = EntityState.Detached;
        }
        public virtual void DetachedRange(IEnumerable<TEntity> entities)
        {
            foreach (var entity in entities)
            {
                DbContext.Entry(entity).State = EntityState.Detached;
            }
        }
    }
}

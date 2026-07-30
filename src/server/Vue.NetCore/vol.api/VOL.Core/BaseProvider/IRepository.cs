using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using VOL.Core.Dapper;
using VOL.Core.Enums;
using VOL.Core.Utilities;
using VOL.Entity.SystemModels;

namespace VOL.Core.BaseProvider
{
    public interface IRepository<TEntity> : IRepositoryDbContext where TEntity : BaseEntity
    {

        ISqlDapper DapperContext { get; }
        /// <summary>
        /// 执行事务。将在执行的方法带入Action
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        WebResponseContent DbContextBeginTransaction(Func<WebResponseContent> action);

        Task<WebResponseContent> DbContextBeginTransactionAsync(Func<Task<WebResponseContent>> action);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="where">查询条件</param>
        /// <param name="filterDeleted">是否过滤逻辑删除的数据，默认过</param>
        /// <returns></returns>
        List<TEntity> Find(Expression<Func<TEntity, bool>> where, bool filterDeleted = true);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="predicate"></param>
        /// <param name="orderBySelector">排序字段,数据格式如:
        ///  orderBy = x => new Dictionary<object, bool>() {
        ///          { x.BalconyName,QueryOrderBy.Asc},
        ///          { x.TranCorpCode1,QueryOrderBy.Desc}
        ///         };
        /// <param name="filterDeleted">是否过滤逻辑删除的数据，默认过</param>
        /// </param>
        /// <returns></returns>
        TEntity FindFirst(Expression<Func<TEntity, bool>> predicate, bool filterDeleted = true);

        IQueryable<TEntity> WhereIF([NotNull] Expression<Func<TEntity, object>> field, string value, LinqExpressionType linqExpression = LinqExpressionType.Equal);

        /// <summary>
        ///  if判断查询
        /// </summary>
        /// 查询示例，value不为null时参与条件查询
        ///    string value = null;
        ///    repository.WhereIF(value!=null,x=>x.Creator==value);
        /// <param name="checkCondition"></param>
        /// <param name="predicate"></param>
        /// <returns></returns>
        IQueryable<TEntity> WhereIF(bool checkCondition, Expression<Func<TEntity, bool>> predicate);

        /// <summary>
        ///  if判断查询
        /// </summary>
        /// 查询示例，value不为null时参与条件查询
        ///    string value = null;
        ///    repository.WhereIF<Sys_User>(value!=null,x=>x.Creator==value);
        /// <param name="checkCondition"></param>
        /// <param name="predicate"></param>
        /// <returns></returns>
        IQueryable<T> WhereIF<T>(bool checkCondition, Expression<Func<T, bool>> predicate) where T : class;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="predicate">where条件</param>
        /// <param name="orderBy">排序字段,数据格式如:
        ///  orderBy = x => new Dictionary<object, bool>() {
        ///          { x.BalconyName,QueryOrderBy.Asc},
        ///          { x.TranCorpCode1,QueryOrderBy.Desc}
        ///         };
        /// </param>
        /// <returns></returns>
        IQueryable<TEntity> FindAsIQueryable(Expression<Func<TEntity, bool>> predicate, Expression<Func<TEntity, Dictionary<object, QueryOrderBy>>> orderBy = null, bool filterDeleted = true);
        /// <summary>
        /// 通过条件查询数据
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="predicate">查询条件</param>
        /// <param name="selector">返回类型如:Find(x => x.UserName == loginInfo.userName, p => new { uname = p.UserName });</param>
        /// <returns></returns>
        List<T> Find<T>(Expression<Func<TEntity, bool>> predicate, Expression<Func<TEntity, T>> selector, bool filterDeleted = true);

        /// <summary>
        /// 根据id查询
        /// </summary>
        /// <typeparam name="TFind"></typeparam>
        /// <param name="id"></param>
        /// <returns></returns>
        TFind FindById<TFind>(object id) where TFind : class;


        Task<TFind> FindByIdAsync<TFind>(List<object> id) where TFind : class;
       
        /// <summary>
        /// 根据条件，返回查询的类
        /// </summary>
        /// <typeparam name="TFind"></typeparam>
        /// <param name="predicate"></param>
        /// <param name="filterDeleted">是否过滤逻辑删除的数据，默认过</param>
        /// <returns></returns>
        List<TFind> Find<TFind>(Expression<Func<TFind, bool>> predicate, bool filterDeleted = true) where TFind : class;
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="TFind"></typeparam>
        /// <param name="predicate"></param>
        /// <param name="filterDeleted">是否过滤逻辑删除的数据，默认过</param>
        /// <returns></returns>
        Task<TFind> FindAsyncFirst<TFind>(Expression<Func<TFind, bool>> predicate, bool filterDeleted = true) where TFind : class;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="predicate"></param>
        ///<param name="filterDeleted">是否过滤逻辑删除的数据，默认过</param>
        /// <returns></returns>
        Task<TEntity> FindAsyncFirst(Expression<Func<TEntity, bool>> predicate, bool filterDeleted = true);
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="TFind"></typeparam>
        /// <param name="predicate"></param>
        /// <param name="filterDeleted">是否过滤逻辑删除的数据，默认过</param>
        /// <returns></returns>
        Task<List<TFind>> FindAsync<TFind>(Expression<Func<TFind, bool>> predicate, bool filterDeleted = true) where TFind : class;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="predicate"></param>
        ///<param name="filterDeleted">是否过滤逻辑删除的数据，默认过</param>
        /// <returns></returns>
        Task<TEntity> FindFirstAsync(Expression<Func<TEntity, bool>> predicate, bool filterDeleted = true);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="predicate"></param>
        /// <param name="filterDeleted">是否过滤逻辑删除的数据，默认过</param>
        /// <returns></returns>
        Task<List<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate, bool filterDeleted = true);
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="predicate"></param>
        /// <param name="selector"></param>
        ///<param name="filterDeleted">是否过滤逻辑删除的数据，默认过</param>
        /// <returns></returns>
        Task<List<T>> FindAsync<T>(Expression<Func<TEntity, bool>> predicate, Expression<Func<TEntity, T>> selector, bool filterDeleted = true);
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="predicate"></param>
        /// <param name="selector"></param>
        /// <param name="filterDeleted">是否过滤逻辑删除的数据，默认过</param>
        /// <returns></returns>
        Task<T> FindFirstAsync<T>(Expression<Func<TEntity, bool>> predicate, Expression<Func<TEntity, T>> selector, bool filterDeleted = true);


        /// <summary>
        /// 
        /// </summary>
        /// <param name="predicate"></param>
        /// <param name="filterDeleted">是否过滤逻辑删除的数据，默认过</param>
        /// <returns></returns>
        Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate, bool filterDeleted = true);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="predicate"></param>
        /// <param name="filterDeleted">是否过滤逻辑删除的数据，默认过</param>
        /// <returns></returns>
        bool Exists(Expression<Func<TEntity, bool>> predicate, bool filterDeleted = true);
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="TExists"></typeparam>
        /// <param name="predicate"></param>
        /// <param name="filterDeleted">是否过滤逻辑删除的数据，默认过</param>
        /// <returns></returns>
        bool Exists<TExists>(Expression<Func<TExists, bool>> predicate, bool filterDeleted = true) where TExists : class;
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="TExists"></typeparam>
        /// <param name="predicate"></param>
        /// <param name="filterDeleted">是否过滤逻辑删除的数据，默认过</param>
        /// <returns></returns>
        Task<bool> ExistsAsync<TExists>(Expression<Func<TExists, bool>> predicate, bool filterDeleted = true) where TExists : class;

        IIncludableQueryable<TEntity, TProperty> Include<TProperty>(Expression<Func<TEntity, TProperty>> incluedProperty);



        IQueryable<TFind> IQueryablePage<TFind>(int pageIndex, int pagesize, out int rowcount, Expression<Func<TFind, bool>> predicate, Expression<Func<TEntity, Dictionary<object, QueryOrderBy>>> orderBy, bool returnRowCount = true) where TFind : class;


        IQueryable<TEntity> IQueryablePage(IQueryable<TEntity> queryable, int pageIndex, int pagesize, out int rowcount, Dictionary<string, QueryOrderBy> orderBy, bool returnRowCount = true);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="properties">指定更新字段:x=>new {x.Name,x.Enable}</param>
        /// <param name="saveChanges">是否保存</param>
        /// <returns></returns>

        int Update(TEntity entity, Expression<Func<TEntity, object>> properties, bool saveChanges = false);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="properties">指定更新字段:x=>new {x.Name,x.Enable}</param>
        /// <param name="saveChanges">是否保存</param>
        /// <returns></returns>
        int Update<TSource>(TSource entity, Expression<Func<TSource, object>> properties, bool saveChanges = false) where TSource : class;

        int Update<TSource>(TSource entity, bool saveChanges = false) where TSource : class;

        int Update<TSource>(TSource entity, string[] properties, bool saveChanges = false) where TSource : class;

        int UpdateRange<TSource>(IEnumerable<TSource> entities, bool saveChanges = false) where TSource : class;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="properties">指定更新字段:x=>new {x.Name,x.Enable}</param>
        /// <param name="saveChanges">是否保存</param>
        /// <returns></returns>
        int UpdateRange<TSource>(IEnumerable<TSource> models, Expression<Func<TSource, object>> properties, bool saveChanges = false) where TSource : class;

        int UpdateRange<TSource>(IEnumerable<TSource> entities, string[] properties, bool saveChanges = false) where TSource : class;


        /// <summary>
        ///修改时同时对明细的添加、删除、修改
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="updateDetail">是否修改明细</param>
        /// <param name="delNotExist">是否删除明细不存在的数据</param>
        /// <param name="updateMainFields">主表指定修改字段</param>
        /// <param name="updateDetailFields">明细指定修改字段</param>
        /// <param name="saveChange">是否保存</param>
        /// <returns></returns>
        WebResponseContent UpdateRange<Detail>(TEntity entity,
            bool updateDetail = false,
            bool delNotExist = false,
            Expression<Func<TEntity, object>> updateMainFields = null,
            Expression<Func<Detail, object>> updateDetailFields = null,
            bool saveChange = false) where Detail : class;

        void Delete(TEntity model, bool saveChanges = false);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="keys"></param>
        /// <param name="delList">是否将子表的数据也删除</param>
        /// <param name="saveChange">是否执行保存数据库</param>
        /// <returns></returns>
        int DeleteWithKeys(object[] keys, bool saveChange = true);


        /// <summary>
        /// 按条件删除
        /// </summary>
        /// <param name="wheres"></param>
        /// <returns></returns>
        int Delete(Expression<Func<TEntity, bool>> wheres, bool saveChange = true);
        /// <summary>
        /// 按条件删除
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="wheres"></param>
        /// <returns></returns>
        int Delete<T>(Expression<Func<T, bool>> wheres, bool saveChange = true) where T : class;

        void Add(TEntity entities, bool SaveChanges = false);
        void Add<T>(T entity, bool saveChanges = false) where T : class;
        void AddRange(IEnumerable<TEntity> entities, bool SaveChanges = false);

        Task AddAsync(TEntity entities);
        Task AddRangeAsync(IEnumerable<TEntity> entities);

        void AddRange<T>(IEnumerable<T> entities, bool saveChanges = false)
           where T : class;


        int SaveChanges();

        Task<int> SaveChangesAsync();



        int ExecuteSqlCommand(string sql, params SqlParameter[] sqlParameters);

        List<TEntity> FromSql(string sql, params SqlParameter[] sqlParameters);

        /// <summary>
        /// 执行sql
        /// 使用方式 FormattableString sql=$"select * from xx where name ={xx} and pwd={xx1} "，
        /// FromSqlInterpolated内部处理sql注入的问题，直接在{xx}写对应的值即可
        /// 注意：sql必须 select * 返回所有TEntity字段，
        /// </summary>
        /// <param name="formattableString"></param>
        /// <returns></returns>
        IQueryable<TEntity> FromSqlInterpolated([System.Diagnostics.CodeAnalysis.NotNull] FormattableString sql);


        /// <summary>
        /// 取消上下文跟踪
        /// 更新报错时，请调用此方法：The instance of entity type 'XXX' cannot be tracked because another instance with the same key value for {'XX'} is already being tracked.
        /// </summary>
        /// <param name="entity"></param>
        void Detached(TEntity entity);
        void DetachedRange(IEnumerable<TEntity> entities);
    }
}

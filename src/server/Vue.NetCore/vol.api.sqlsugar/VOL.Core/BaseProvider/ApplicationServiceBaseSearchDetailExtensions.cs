using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using SqlSugar;
using VOL.Core.DBManager;
using VOL.Core.Enums;
using VOL.Core.Extensions;
using VOL.Entity;
using VOL.Entity.DomainModels;
using VOL.Entity.SystemModels;

namespace VOL.Core.BaseProvider
{
    public static class ApplicationServiceBaseSearchDetailExtensions
    {

        public static PageGridData<Detail> GetDetailPageData<TEntity, TRepository, MainEntity, Detail>(
            this PageDataOptions options, ServiceBase<TEntity, TRepository> service)
            where TEntity : BaseEntity, new()
            where TRepository : IRepository<TEntity>
            where MainEntity : class, new()
            where Detail : class, new()
        {
            var queryable = GetDetailQueryable<TEntity, TRepository, MainEntity, Detail>(service, options);
            PageGridData<Detail> gridData = new();
            gridData.summary = service.GetDetailSummaryData(queryable.Clone());
            gridData.total = queryable.Count();
            options.Sort = options.Sort ?? typeof(Detail).GetKeyName();
            Dictionary<string, QueryOrderBy> orderBy = service.GetPageDataSort(options, typeof(Detail).GetProperties());
            gridData.rows = queryable.GetIQueryableOrderBy(orderBy)
                 .TakePage(options.Page, options.Rows)
                 //明细字段权限
                 .FilterQueryableAuthFields();
   
            return gridData;
        }

        public static async Task<object> GetDetailPageDataAsync<TEntity, TRepository, MainEntity, Detail>(
           this PageDataOptions options, ServiceBase<TEntity, TRepository> service)
           where TEntity : BaseEntity, new()
           where TRepository : IRepository<TEntity>
           where MainEntity : class, new()
           where Detail : class,new()
        {
            var queryable = GetDetailQueryable<TEntity, TRepository, MainEntity, Detail>(service, options);
            PageGridData<Detail> gridData = new();
            gridData.summary = service.GetDetailSummaryData(queryable.Clone());
            gridData.summary ??= await service.GetDetailSummaryDataAsync(queryable.Clone());
            gridData.total = await queryable.CountAsync();
            options.Sort = options.Sort ?? typeof(Detail).GetKeyName();
            Dictionary<string, QueryOrderBy> orderBy = service.GetPageDataSort(options, typeof(Detail).GetProperties());
            gridData.rows = await queryable.GetISugarQueryableOrderBy(orderBy)
                 .TakePage(options.Page, options.Rows)
                //明细字段权限
                .FilterQueryableAuthFieldsAsync();
        
            return gridData;
        }

        private static ISugarQueryable<Detail> GetDetailQueryable<TEntity, TRepository, MainEntity, Detail>(
             ServiceBase<TEntity, TRepository> service,
            PageDataOptions options)
            where TEntity : BaseEntity, new()
            where TRepository : IRepository<TEntity>
            where MainEntity : class, new()
            where Detail : class, new()
        {
            if (options.Rows <= 0)
            {
                options.Rows = 30;
            }
            options.GetSearchParameters();
            options.Filter = options.Filter ?? new List<SearchParameters>();
            string mainKeyName = typeof(MainEntity).GetForeignKey(typeof(Detail).GetEntityTableName(false));
            //主从自定义关联字段
            if (options.Filter.Count <= 1 && !options.Filter.Any(x => x.Name == mainKeyName))
            {
                options.Filter = new List<SearchParameters>();
            }
            if (options.Filter.Count == 0 && !string.IsNullOrEmpty(options.Value?.ToString()))
            {
                //自定义主从表关联字段
                options.Filter.Add(new SearchParameters()
                {
                    Name = mainKeyName,
                    Value = options.Value.ToString()
                });
            }
            var queryable = service.repository.DbContext.Set<Detail>(false);
            queryable = options.ConvertQueryFilter(queryable).FilterLogicDel();
            queryable = service.DetailQuery(queryable, options.Filter);
            return queryable;
        }
        /// <summary>
        /// 获取明细表类型
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="type"></param>
        /// <param name="pageData"></param>
        /// <returns></returns>
        public static (Type mainType, Type detailType) GetDetailType<TEntity>(this Type type, PageDataOptions pageData)
        {
            var tables = typeof(TEntity).GetCustomAttribute<EntityAttribute>();
            if (tables == null) return (null, null);
            Type detailType = null;

            if (string.IsNullOrEmpty(pageData.TableName) && string.IsNullOrEmpty(pageData.DetailTable))
            {
                detailType = tables.DetailTable.FirstOrDefault();
                return (typeof(TEntity), detailType);
            }
            detailType = tables.DetailTable.Where(c => c.Name == pageData.TableName).FirstOrDefault();
            return (typeof(TEntity), detailType);
        }
        /// <summary>
        /// 获取明细表与主表主键关联的字段
        /// </summary>
        /// <param name="type">主表</param>
        /// <param name="detailTable">明细表表名</param>
        /// <returns></returns>
        public static string GetForeignKey(this Type type, string detailTable)
        {
            PropertyInfo property = type.GetProperty(detailTable);
            if (property == null) return type.GetKeyProperty().Name;

            return property.GetTypeCustomValue<ForeignKeyAttribute>(x => x.Name);
        }
        /// <summary>
        /// 获取明细表与主表主键关联的字段
        /// </summary>
        /// <param name="type">主表</param>
        /// <param name="detailTable">明细表</param>
        /// <param name="detailTable">明细表表名</param>
        /// <returns></returns>
        public static PropertyInfo GetForeignKeyType(this Type type, Type detailType, string detailTable)
        {
            string foreignName = type.GetForeignKey(detailTable);
            return detailType.GetProperty(foreignName);
        }
    }
}

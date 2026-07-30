using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using OfficeOpenXml;
using SqlSugar;
using VOL.Core.Enums;
using VOL.Core.Utilities;
using VOL.Core.WorkFlow;
using VOL.Entity.DomainModels;
using VOL.Entity.SystemModels;

namespace VOL.Core.BaseProvider
{
    public abstract class ApplicationServiceBase<TEntity, TRepository> where TEntity : BaseEntity where TRepository : IRepository<TEntity>
    {
        private PropertyInfo[] properties { get; set; } = null;
        public PropertyInfo[] TProperties
        {
            get
            {
                properties ??= typeof(TEntity).GetProperties();
                return properties;
            }
        }
        public IRepository<TEntity> repository;
        public ApplicationServiceBase()
        {

        }
        public WebResponseContent baseWebResponse { get; set; }
        public ApplicationServiceBase(TRepository repository)
        {
            baseWebResponse = new WebResponseContent(true);
            this.repository = repository;
        }
        /// <summary>
        /// 主从明细表，一对多明细表编辑信息
        /// </summary>
        public ApplicationServiceBaseMultipleTableEntity MultipleTableEntity { get; set; }
        /// <summary>
        /// 是否开启多租户功能
        /// </summary>
        public bool IsMultiTenancy = true;

        /// <summary>
        /// 自定义原生查询sql,这个对于不想写表达式关联或者复杂查询非常有用
        /// 例：QuerySql=$"select * from tb1 as a where  a.name='xxxx' x.id in (select b.id from tb2 b)";
        ///  select * 这里可以自定义，但select 必须返回表所有的列，不能少
        /// </summary>
        public string QuerySql = null;

        /// <summary>
        /// 查询前,对现在有的查询字符串条件增加或删除
        /// </summary>
        public Action<List<SearchParameters>> QueryRelativeList { get; set; }
        /// <summary>
        /// 设置查询排序参数及方式,参数格式为：
        ///  Expression<Func<T, Dictionary<object, bool>>> orderBy = x => new Dictionary<object, QueryOrderBy>() 
        ///            {{ x.ID, QueryOrderBy.Asc },{ x.DestWarehouseName, QueryOrderBy.Desc } };
        /// 返回的是new Dictionary<object, bool>(){{}}key为排序字段，QueryOrderBy为排序方式
        /// </summary>
        public Expression<Func<TEntity, Dictionary<object, QueryOrderBy>>> OrderByExpression;

        //查询前,在现有的查询条件上通过表达式修改查询条件
        public Func<ISugarQueryable<TEntity>, ISugarQueryable<TEntity>> QueryRelativeExpression { get; set; }

        /// <summary>
        /// 查询界面table 统计、求和、平均值等
        /// 实现方式
        ///SummaryExpress = (ISugarQueryable<App_TransactionAvgPrice> queryable) =>
        //                {
        //                return queryable.GroupBy(x => 1).Select(x => new
        //                {
        //                    AvgPrice = x.Average(o => o.AvgPrice),
        //                    Enable = x.Sum(o => o.Enable)
        //            }).FirstOrDefault();
        //};
        /// </summary>
        //   protected Func<IGrouping<T, T>, object> SummaryExpress = null;
        public Func<ISugarQueryable<TEntity>, object> SummaryExpress = null;
        public Func<ISugarQueryable<TEntity>, Task<object>> SummaryExpressAsync = null;
        /// <summary>
        /// 页面查询或导出，从数据库查询出来的结果
        /// </summary>
        public Action<PageGridData<TEntity>> GetPageDataOnExecuted;
        public Func<PageGridData<TEntity>, Task> GetPageDataOnExecutedAsync;
        /// <summary>
        /// 调用新建处理前(SaveModel为传入的原生数据)
        /// </summary>
        public Func<SaveModel, WebResponseContent> AddOnExecute;

        /// <summary>
        /// 调用新建保存数据库前处理(已将提交的原生数据转换成了对象)
        ///  Func<T, object,ResponseData>  T为主表数据，object为明细数据(如果需要使用明细对象,请用 object as List<T>转换) 
        /// </summary>
        public Func<TEntity, object, WebResponseContent> AddOnExecuting;
        public Func<TEntity, object, Task<WebResponseContent>> AddOnExecutingAsync;

        /// <summary>
        /// 调用更新方法前处理(SaveModel为传入的原生数据)
        /// </summary>
        public Func<SaveModel, WebResponseContent> UpdateOnExecute;

        /// <summary>
        ///  调用更新方法保存数据库前处理
        ///  (已将提交的原生数据转换成了对象,将明细新增、修改、删除的数据分别用object1/2/3标识出来 )
        ///  T=更新的主表对象
        ///  object1=为新增明细的对象，使用时将object as List<T>转换一下
        ///  object2=为更新明细的对象
        ///  List<object>=为删除的细的对象Key
        /// </summary>
        protected Func<TEntity, object, object, List<object>, WebResponseContent> UpdateOnExecuting;
        protected Func<TEntity, object, object, List<object>, Task<WebResponseContent>> UpdateOnExecutingAsync;
        /// <summary>
        ///  调用更新方法保存数据库后处理
        ///   **实现当前方法时，内部默认已经开启事务，如果实现的方法操作的是同一数据库,则不需要在UpdateOnExecuted中事务
        ///  (已将提交的原生数据转换成了对象,将明细新增、修改、删除的数据分别用object1/2/3标识出来 )
        ///  T=更新的主表对象
        ///  object1=为新增明细的对象，使用时将object as List<T>转换一下
        ///  object2=为更新明细的对象
        ///  List<object>=为删除的细的对象Key
        /// 此处已开启了DbContext事务(重点),如果还有其他业务处事，直接在这里写EF代码,不需要再开启事务
        /// 如果执行的是手写sql请用repository.DbContext.Database.ExecuteSqlCommand()或 repository.DbContext.Set<T>().FromSql执行具体sql语句
        /// </summary>
        protected Func<TEntity, object, object, List<object>, WebResponseContent> UpdateOnExecuted;

        protected Func<TEntity, object, object, List<object>, Task<WebResponseContent>> UpdateOnExecutedAsync;

        /// <summary>
        /// 删除前处理,object[]准备删除的主键
        /// </summary>
        protected Func<object[], WebResponseContent> DelOnExecuting;
        protected Func<object[], Task<WebResponseContent>> DelOnExecutingAsync;
        /// <summary>
        /// 删除后处理,object[]已删除的主键,此处已开启了DbContext事务(重点),如果还有其他业务处事，直接在这里写EF代码,不需要再开启事务
        /// 如果执行的是手写sql请用repository.DbContext.Database.ExecuteSqlCommand()或 repository.DbContext.Set<T>().FromSql执行具体sql语句
        /// </summary>
        protected Func<object[], WebResponseContent> DelOnExecuted;

        protected Func<object[], Task<WebResponseContent>> DelOnExecutedAsync;
        /// <summary>
        /// 调用新建保存数据库后处理。
        /// **实现当前方法时，内部默认已经开启事务，如果实现的方法操作的是同一数据库,则不需要在AddOnExecuted中事务
        ///  Func<T, object,ResponseData>  T为主表数据，object为明细数据(如果需要使用明细对象,请用 object as List<T>转换) 
        ///  此处已开启了DbContext事务(重点),如果还有其他业务处事，直接在这里写EF代码,不需要再开启事务
        /// 如果执行的是手写sql请用repository.DbContext.Database.ExecuteSqlCommand()或 repository.DbContext.Set<T>().FromSql执行具体sql语句
        /// </summary>
        protected Func<TEntity, object, WebResponseContent> AddOnExecuted;

        protected Func<TEntity, object, Task<WebResponseContent>> AddOnExecutedAsync;

        /// <summary>
        /// 进入审批流程方法之前
        /// </summary>
        public Func<TEntity, bool> AddWorkFlowExecuting;

        public Func<TEntity, Task<bool>> AddWorkFlowExecutingAsync;

        /// <summary>
        /// 写入审批流程数据之后
        /// list:审批的人id
        /// </summary>
        public Action<TEntity, List<int>> AddWorkFlowExecuted;
        public Func<TEntity, List<int>, Task> AddWorkFlowExecutedAsync;

        ///默认导出最大表数量：0不限制 
        protected int Limit { get; set; } = 0;
        /// <summary>
        /// 自定义上传的文件夹
        /// </summary>
        protected string UploadFolder = null;
        protected bool IsRoot = true;


        /// <summary>
        /// 导出下载模板，指定要导出的模板列，格式:Expression<Func<TEntity, object>> exp = x => new { x.字段1, x.字段2 }
        /// </summary>
        protected Expression<Func<TEntity, object>> DownLoadTemplateColumns { get; set; }
        /// <summary>
        /// 指定导入字段的名称
        /// </summary>
        protected Expression<Func<TEntity, Dictionary<object, string>>> ExcelHeaderMap { get; set; }


        /// <summary>
        /// 导入excel从第几行开始读取
        /// </summary>
        protected int ImportStartRowIndex { get; set; } = 1;
        /// <summary>
        /// 导入时不验证下拉框数据源的字段值
        /// </summary>
        protected Expression<Func<TEntity, object>> ImportIgnoreSelectValidationColumns;

        /// <summary>
        /// 导入保存前
        /// </summary>
        protected Func<List<TEntity>, WebResponseContent> ImportOnExecuting;

        /// <summary>
        /// 导入保存前
        /// </summary>
        protected Func<List<TEntity>, Task<WebResponseContent>> ImportOnExecutingAsync;

        /// <summary>
        /// 导入保存后
        /// </summary>
        protected Func<List<TEntity>, WebResponseContent> ImportOnExecuted;

        /// <summary>
        /// 导入保存后
        /// </summary>
        protected Func<List<TEntity>, Task<WebResponseContent>> ImportOnExecutedAsync;

        /// <summary>
        ///导出前处理,DataTable导出的表数据
        ///List<T>导出的数据, List<string>忽略不需要导出的字段
        ///此方法不建议使用,由下面ExportColumns委托替代2020.05.07
        /// </summary>
        protected Func<List<TEntity>, List<string>, WebResponseContent> ExportOnExecuting;
        protected Func<List<TEntity>, List<string>, Task<WebResponseContent>> ExportOnExecutingAsync;

        /// <summary>
        /// 导出表数据(界面上导出操作),指定要导出的列，格式:Expression<Func<T, object>> exp = x => new { x.字段1, x.字段2 }
        /// </summary>
        protected Expression<Func<TEntity, object>> ExportColumns { get; set; }

        /// <summary>
        /// 2022.06.20增加原生excel读取方法(导入时可以自定义读取excel内容)
        /// string=当前读取的excel单元格的值
        /// ExcelWorksheet=excel对象
        /// ExcelRange当前excel单元格对象
        /// int=当前读取的第几数
        /// int=当前读取的第几列
        /// string=返回的值
        /// </summary>
        protected Func<string, ExcelWorksheet, ExcelRange, int, int, string> ImportOnReadCellValue;


        /// <summary>
        /// 审核前处理
        /// </summary>
        public Func<List<TEntity>, WebResponseContent> AuditOnExecuting;
        public Func<List<TEntity>, Task<WebResponseContent>> AuditOnExecutingAsync;
        /// <summary>
        /// 审核后处理
        /// </summary>
        public Func<List<TEntity>, WebResponseContent> AuditOnExecuted;

        public Func<List<TEntity>, Task<WebResponseContent>> AuditOnExecutedAsync;
        /// <summary>
        /// 反核前处理
        /// </summary>
        public Func<TEntity, WebResponseContent> AntiAuditOnExecuting;

        public Func<TEntity, Task<WebResponseContent>> AntiAuditOnExecutingAsync;

        /// <summary>
        /// 反核后处理
        /// </summary>
        public Func<TEntity, WebResponseContent> AntiAuditOnExecuted;
        public Func<TEntity, Task<WebResponseContent>> AntiAuditOnExecutedAsync;

        /// <summary>
        /// 审批流程审核前
        /// TEntity:当前审核的数据
        /// AuditStatus:审核状态
        /// bool:当前数据是否为最后一个人审核
        /// </summary>
        public Func<TEntity, AuditStatus, bool, WebResponseContent> AuditWorkFlowExecuting;
        public Func<TEntity, AuditStatus, bool, Task<WebResponseContent>> AuditWorkFlowExecutingAsync;

        /// <summary>
        /// 审批流程审核后
        /// T:当前审核的数据
        /// AuditStatus:审核状态
        /// list:下一个节点的审批人id
        /// bool:当前数据是否为最后一个人审核
        /// </summary>
        public Func<TEntity, AuditStatus, List<int>, bool, WebResponseContent> AuditWorkFlowExecuted;
        public Func<TEntity, AuditStatus, List<int>, bool, Task<WebResponseContent>> AuditWorkFlowExecutedAsync;

        public bool ResponseIsError
        {
            get { return !baseWebResponse.Status || baseWebResponse.Code == "-1"; }
        }

        /// <summary>
        /// 明细合计
        /// </summary>
        /// <typeparam name="Detail"></typeparam>
        /// <param name="queryeable"></param>
        /// <returns></returns>
        protected virtual object GetDetailSummary<Detail>(ISugarQueryable<Detail> queryeable) where Detail : class
        {
            return null;
        }
        public virtual object GetDetailSummaryData<Detail>(ISugarQueryable<Detail> queryeable) where Detail : class
        {
            return GetDetailSummary(queryeable);
        }
        /// <summary>
        /// 明细合计
        /// </summary>
        /// <typeparam name="Detail"></typeparam>
        /// <param name="queryeable"></param>
        /// <returns></returns>
        protected virtual Task<object> GetDetailSummaryAsync<Detail>(ISugarQueryable<Detail> queryeable) where Detail : class
        {
            return Task.FromResult<object>(null);
        }
        public virtual async Task<object> GetDetailSummaryDataAsync<Detail>(ISugarQueryable<Detail> queryeable) where Detail : class
        {
            return await GetDetailSummaryAsync(queryeable);
        }
        /// <summary>
        /// 明细表自定义查询
        /// </summary>
        /// <typeparam name="Detail"></typeparam>
        /// <param name="queryable"></param>
        /// <param name="searchParametersList"></param>
        /// <returns></returns>
        public virtual ISugarQueryable<Detail> DetailQuery<Detail>(ISugarQueryable<Detail> queryable, List<SearchParameters> searchParametersList)
        {
            return queryable;
        }

        public bool IsTableActionLog {
            get {
                return typeof(TEntity).IsTableActionLog();
            }
        }
    }
}

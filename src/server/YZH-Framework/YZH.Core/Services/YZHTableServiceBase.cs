using System.Reflection;
using VOL.Core.BaseProvider;
using VOL.Core.DBManager;
using VOL.Core.Extensions;
using VOL.Core.Services;
using VOL.Core.Utilities;
using VOL.Entity.DomainModels;
using VOL.Entity.SystemModels;
using YZH.Core.Attributes;
using YZH.Core.Validation;

namespace YZH.Core;

/// <summary>
/// 单表 CRUD 专用基类（90% 场景）。
/// 继承 YZHServiceBase，新增 QueryView 视图查询支持。
///
/// 使用：
///   // 零配置
///   public class CertStageService
///       : YZHTableServiceBase&lt;CertStage, ICertStageRepository&gt;
///       , ICertStageService, IDependency
///   { }
///
///   // 声明查询视图
///   [QueryView(typeof(ISOStandardView))]
///   public class ISOStandard : YZHBaseEntity { ... }
///   // Service 自动使用 ISOStandardView 查询，Add/Update/Del 用 ISOStandard
/// </summary>
public abstract class YZHTableServiceBase<TEntity, TRepository>
    : YZHServiceBase<TEntity, TRepository>
    where TEntity : BaseEntity
    where TRepository : IRepository<TEntity>
{
    public YZHTableServiceBase() { }
    public YZHTableServiceBase(TRepository repository) : base(repository) { }

    /// <summary>
    /// 重写 GetPageData：如果实体声明了 [QueryView]，自动查询视图。
    /// </summary>
    public override PageGridData<TEntity> GetPageData(PageDataOptions options)
    {
        // 查询前钩子（可改查询参数）
        try
        {
            OnGetPageDataBefore(options);
        }
        catch (Exception ex)
        {
            Logger.Error($"OnGetPageDataBefore 异常: {ex.Message}");
        }

        var viewType = GetQueryViewType();
        if (viewType != null && viewType != typeof(TEntity))
        {
            var result = GetPageDataFromView(options, viewType);
            // 查询后钩子（可加工数据）
            try
            {
                OnGetPageDataAfter(result.rows);
            }
            catch (Exception ex)
            {
                Logger.Error($"OnGetPageDataAfter 异常: {ex.Message}");
            }
            return result;
        }

        var baseResult = base.GetPageData(options);
        try
        {
            OnGetPageDataAfter(baseResult.rows);
        }
        catch (Exception ex)
        {
            Logger.Error($"OnGetPageDataAfter 异常: {ex.Message}");
        }
        return baseResult;
    }

    /// <summary>查询前钩子（可改 wheres / sort）</summary>
    protected virtual void OnGetPageDataBefore(PageDataOptions options) { }

    /// <summary>查询后钩子（可加工 rows：翻译字典、合并字段）</summary>
    protected virtual void OnGetPageDataAfter(List<TEntity> rows) { }

    /// <summary>获取实体声明的查询视图类型</summary>
    protected Type? GetQueryViewType()
        => typeof(TEntity).GetCustomAttribute<QueryViewAttribute>()?.ViewType;

    /// <summary>从视图查询分页数据（子类可覆写自定义逻辑）</summary>
    protected virtual PageGridData<TEntity> GetPageDataFromView(
        PageDataOptions options, Type viewType)
    {
        var tableName = viewType.GetCustomAttribute<VOL.Entity.EntityAttribute>()?.TableName
            ?? viewType.Name;
        var parameters = new Dictionary<string, object>();
        var whereClause = "1=1";

        var countSql = $"SELECT COUNT(1) FROM {tableName} WHERE {whereClause} " +
                       $"AND (IsDeleted = 0 OR IsDeleted IS NULL)";
        var total = Convert.ToInt32(
            DBServerProvider.SqlDapper.ExecuteScalar(countSql, parameters));

        var dataSql = $"SELECT * FROM {tableName} WHERE {whereClause} " +
                      $"AND (IsDeleted = 0 OR IsDeleted IS NULL) " +
                      $"ORDER BY Id DESC LIMIT @offset, @rows";
        parameters["offset"] = (options.Page - 1) * options.Rows;
        parameters["rows"] = options.Rows;

        var viewData = DBServerProvider.SqlDapper.QueryList<dynamic>(dataSql, parameters);
        var rows = viewData.Select(d => (TEntity)d).ToList();

        return new PageGridData<TEntity> { rows = rows, total = total };
    }
}

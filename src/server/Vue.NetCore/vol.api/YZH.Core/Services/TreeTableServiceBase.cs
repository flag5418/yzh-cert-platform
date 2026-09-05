using System.Reflection;
using YZH.Core.BaseProvider;
using YZH.Core.DBManager;
using YZH.Core.Extensions;
using YZH.Core.Services;
using YZH.Core.Utilities;
using YZH.Entity.DomainModels;
using YZH.Entity.SystemModels;
using YZH.Core.Attributes;

namespace YZH.Core;

/// <summary>
/// 左树右表专用基类。
/// 继承 TableServiceBase，新增左树数据加载 + 联动过滤。
///
/// 使用：
///   [TreeSource("CertCertificationBody", "CbCode", "Code", "Name")]
///   [QueryView(typeof(ISOStandardView))]
///   public class ISOStandard : EntityBase { ... }
///
///   public class ISOStandardService
///       : TreeTableServiceBase&lt;ISOStandard, IISOStandardRepository&gt;
///       , IISOStandardService, IDependency
///   { }
///   // 自动：GetTreeData() 返回认证机构列表，GetPageData 按 CbCode 过滤
/// </summary>
public abstract class TreeTableServiceBase<TEntity, TRepository>
    : TableServiceBase<TEntity, TRepository>
    where TEntity : BaseEntity
    where TRepository : IRepository<TEntity>
{
    public TreeTableServiceBase() { }
    public TreeTableServiceBase(TRepository repository) : base(repository) { }
    /// <summary>
    /// 获取左侧树数据。
    /// 根据 [TreeSource] 特性自动查询关联表。
    /// </summary>
    public virtual List<object> GetTreeData()
    {
        var attr = typeof(TEntity).GetCustomAttribute<TreeSourceAttribute>();
        if (attr == null) return new List<object>();

        // 用 Dapper 查询树数据来源表
        var treeTable = GetTableNameByController(attr.TreeController);
        var sql = $"SELECT * FROM {treeTable} " +
                  $"WHERE (IsDeleted = 0 OR IsDeleted IS NULL) ORDER BY Id";
        var data = DBServerProvider.SqlDapper.QueryList<dynamic>(sql, null);
        return data.Cast<object>().ToList();
    }

    /// <summary>
    /// 重写 GetPageData：自动注入 treeKey 过滤条件。
    /// </summary>
    public override PageGridData<TEntity> GetPageData(PageDataOptions options)
    {
        var attr = typeof(TEntity).GetCustomAttribute<TreeSourceAttribute>();
        if (attr != null)
        {
            // 从查询参数中提取 treeKey
            var treeKey = ExtractTreeKey(options);
            if (!string.IsNullOrEmpty(treeKey))
            {
                InjectTreeFilter(options, attr.FilterField, treeKey);
                // 钩子：树节点选中后
                try
                {
                    OnTreeSelect(treeKey, options);
                }
                catch (Exception ex)
                {
                    Logger.Error($"OnTreeSelect 异常: {ex.Message}");
                }
            }
        }
        return base.GetPageData(options);
    }

    /// <summary>树节点选中后钩子（可改右侧查询参数）</summary>
    protected virtual void OnTreeSelect(string treeKey, PageDataOptions options) { }

    private static string? ExtractTreeKey(PageDataOptions options)
    {
        // 从 Vol 的 Filter 查询参数中提取树节点 key
        if (options.Filter != null)
        {
            foreach (var filter in options.Filter)
            {
                if (filter.Name?.Equals("treeKey", StringComparison.OrdinalIgnoreCase) == true)
                    return filter.Value?.ToString();
            }
        }
        return null;
    }

    private static void InjectTreeFilter(PageDataOptions options, string filterField, string treeKey)
    {
        // 注入过滤条件到 Filter
        if (options.Filter == null) options.Filter = new List<SearchParameters>();
        options.Filter.Add(new SearchParameters
        {
            Name = filterField,
            Value = treeKey,
            DisplayType = "text"
        });
    }

    private static string GetTableNameByController(string controllerName)
    {
        // 约定：Controller 名去掉前缀 Cert → 表名 cert_ + snake_case
        if (controllerName.StartsWith("Cert"))
            controllerName = controllerName.Substring(4);
        return "cert_" + ToSnakeCase(controllerName);
    }

    private static string ToSnakeCase(string str)
    {
        return string.Concat(str.Select((c, i) =>
            i > 0 && char.IsUpper(c) ? "_" + char.ToLower(c).ToString() : char.ToLower(c).ToString()));
    }
}

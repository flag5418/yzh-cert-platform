using System.Reflection;
using VOL.Core.BaseProvider;
using VOL.Core.DBManager;
using VOL.Core.Extensions;
using VOL.Core.Utilities;
using VOL.Entity.DomainModels;
using VOL.Entity.SystemModels;
using YZH.Core.Validation;

namespace YZH.Core;

/// <summary>
/// 纯树形管理专用基类。
/// 重写 GetPageData → 返回树形结构（不分页）。
/// 内置级联删除校验 + 移动节点校验。
///
/// 使用：
///   public class DirectoryTemplateService
///       : YZHTreeServiceBase&lt;DirectoryTemplate, IDirectoryTemplateRepository&gt;
///       , IDirectoryTemplateService, IDependency
///   { }
/// </summary>
public abstract class YZHTreeServiceBase<TEntity, TRepository>
    : YZHTableServiceBase<TEntity, TRepository>
    where TEntity : BaseEntity
    where TRepository : IRepository<TEntity>
{
    public YZHTreeServiceBase() { }
    public YZHTreeServiceBase(TRepository repository) : base(repository) { }
    /// <summary>重写 GetPageData → 返回树形结构</summary>
    public override PageGridData<TEntity> GetPageData(PageDataOptions options)
    {
        var (queryable, _) = options.BuildPageDataQuery(this, IsMultiTenancy, true);
        var allData = queryable.ToList();
        var treeData = BuildTree(allData);
        return new PageGridData<TEntity> { rows = treeData, total = allData.Count };
    }

    /// <summary>构建树形结构（基于 ParentCode 字段）</summary>
    protected virtual List<TEntity> BuildTree(List<TEntity> flatList)
    {
        var codeProp = typeof(TEntity).GetProperty("Code");
        var parentProp = typeof(TEntity).GetProperty("ParentCode");
        if (codeProp == null || parentProp == null) return flatList;

        var lookup = flatList.ToLookup(
            e => parentProp.GetValue(e)?.ToString() ?? "", e => e);
        var rootItems = flatList.Where(e =>
            string.IsNullOrEmpty(parentProp.GetValue(e)?.ToString())).ToList();
        foreach (var item in rootItems)
            SetChildren(item, lookup, codeProp, parentProp);
        return rootItems;
    }

    private static void SetChildren(TEntity parent,
        ILookup<string, TEntity> lookup,
        PropertyInfo codeProp, PropertyInfo parentProp)
    {
        var code = codeProp.GetValue(parent)?.ToString();
        var children = lookup[code ?? ""].ToList();
        // 通过反射设置 Children 属性（如有）
        var childrenProp = typeof(TEntity).GetProperty("Children");
        if (childrenProp != null && childrenProp.PropertyType == typeof(List<TEntity>))
        {
            childrenProp.SetValue(parent, children);
            foreach (var child in children)
                SetChildren(child, lookup, codeProp, parentProp);
        }
    }

    /// <summary>
    /// 删除前校验：检查子节点
    /// </summary>
    protected override EntityValidationResult OnDeleting(object[] keys, List<TEntity> entities)
    {
        foreach (var entity in entities)
        {
            var codeProp = typeof(TEntity).GetProperty("Code");
            var code = codeProp?.GetValue(entity)?.ToString();
            if (string.IsNullOrEmpty(code)) continue;

            var childCount = GetRelatedCount(GetTableName(), "parent_code", code);
            if (childCount > 0)
                return new EntityValidationResult()
                    .Error($"该项下有 {childCount} 个子节点，请先删除子节点");
        }
        return new EntityValidationResult().OK();
    }

    /// <summary>移动节点校验（防止循环引用）</summary>
    protected virtual EntityValidationResult ValidateMove(
        string code, string newParentCode)
    {
        if (code == newParentCode)
            return new EntityValidationResult().Error("不能将节点移动到自身下");
        if (IsDescendant(newParentCode, code))
            return new EntityValidationResult().Error("不能将节点移动到其子节点下");
        return new EntityValidationResult().OK();
    }

    private bool IsDescendant(string code, string ancestorCode)
    {
        var sql = $"WITH RECURSIVE TreePath AS (" +
                  $"  SELECT code, parent_code FROM {GetTableName()} WHERE code = @code " +
                  $"  UNION ALL " +
                  $"  SELECT t.code, t.parent_code FROM {GetTableName()} t " +
                  $"  INNER JOIN TreePath tp ON t.code = tp.parent_code" +
                  $") SELECT COUNT(1) FROM TreePath WHERE code = @ancestorCode";
        var count = Convert.ToInt32(DBServerProvider.SqlDapper.ExecuteScalar(
            sql, new { code, ancestorCode }));
        return count > 0;
    }
}

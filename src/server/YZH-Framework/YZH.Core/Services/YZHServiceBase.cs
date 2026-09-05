using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using YZH.Core.BaseProvider;
using YZH.Core.DBManager;
using YZH.Core.Enums;
using YZH.Core.Extensions;
using YZH.Core.ManageUser;
using YZH.Core.Services;
using YZH.Core.Utilities;
using YZH.Entity.Admin.Platform;
using YZH.Entity.DomainModels;
using YZH.Entity.SystemModels;
using YZH.Core.Attributes;
using YZH.Core.Exceptions;
using YZH.Core.Validation;

namespace YZH.Core;

/// <summary>
/// 删除模式枚举（统一术语：Logical / Physical / Cascade）
/// </summary>
public enum DeleteMode
{
    /// <summary>逻辑删除（默认）：Enable=false + 填充删除信息</summary>
    Logical = 0,
    /// <summary>物理删除：直接从数据库移除</summary>
    Physical = 1,
    /// <summary>级联删除：主表 + 关联从表</summary>
    Cascade = 2
}

/// <summary>
/// 删除策略特性（声明式配置，标记在实体类上）
///
/// 使用示例：
///   // 默认逻辑删除（无需声明）
///   public class CertificationBody : YZHBaseEntity { }
///
///   // 显式物理删除
///   [YZHDeleteStrategy(Mode = DeleteMode.Physical)]
///   public class TempFile : YZHBaseEntity { }
///
///   // 级联删除
///   [YZHDeleteStrategy(Mode = DeleteMode.Cascade, CascadeEntities = new[] { typeof(OrderDetail) })]
///   public class Order : YZHBaseEntity { }
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class YZHDeleteStrategyAttribute : Attribute
{
    /// <summary>删除模式（默认 Logical）</summary>
    public DeleteMode Mode { get; set; } = DeleteMode.Logical;

    /// <summary>级联删除的实体类型列表（仅 Mode=Cascade 时有效）</summary>
    public Type[]? CascadeEntities { get; set; }

    /// <summary>是否允许强制删除有关联数据（默认 false）</summary>
    public bool ForceDelete { get; set; }
}

/// <summary>
/// YZH 业务 Service 抽象根。继承 Vol 的 ServiceBase，但**接管全部写入管道**。
///
/// 设计要点（2026-09-04 架构重写）：
/// 1. 同步与异步走**同一条管道** —— AddAsync/UpdateAsync/DelAsync 与同步版本行为完全一致，
///    不再出现「异步调用绕过 YZH 校验与生命周期」的历史缺陷。
/// 2. 架构契约可覆写 —— BusinessKeyField / SoftDeleteFilter / ExecuteLogicalDelete，
///    使 YZH 基类能承载主键、Enable 类型、字段名各异的 Sys_* 实体。
/// 3. 生命周期钩子的修改**会写回 SaveModel**，保证 OnAdding/OnUpdating 中的赋值真正落库。
/// 4. 统一异常脱敏 —— 所有管道出口都经 ExceptionSanitizer。
///
/// 业务 Service 不直接继承此类，应继承场景专用基类：
/// - 单表 CRUD → YZHTableServiceBase
/// - 左树右表 → YZHTreeTableServiceBase
/// - 纯树形   → YZHTreeServiceBase
/// - 关联表   → YZHLinkTableServiceBase
/// </summary>
public abstract class YZHServiceBase<TEntity, TRepository>
    : ServiceBase<TEntity, TRepository>
    where TEntity : BaseEntity
    where TRepository : IRepository<TEntity>
{
    public YZHServiceBase() { }
    public YZHServiceBase(TRepository repository) : base(repository) { }

    #region 0. 架构契约（子类覆写以适配异形实体）

    /// <summary>
    /// 业务唯一键字段名。Update 做唯一性校验时用于排除自身。
    /// 默认 "Code"（YZH 实体约定）；Sys_* 等无 Code 的实体应覆写为实际字段名，或返回 null 表示不做排除。
    /// </summary>
    protected virtual string? BusinessKeyField => "Code";

    /// <summary>
    /// 软删除过滤 SQL 片段，用于手写 SQL（关联计数、视图查询等）。
    /// 默认 "IsDeleted = 0 OR IsDeleted IS NULL"（YZH 实体约定）；
    /// 表内无 IsDeleted 列的实体（如全部 Sys_* 表）必须覆写为 null 或其它有效条件。
    /// </summary>
    protected virtual string? SoftDeleteFilter => "IsDeleted = 0 OR IsDeleted IS NULL";

    /// <summary>
    /// 逻辑删除实现。
    /// 默认：YZHBaseEntity 派生实体走 YZH 统一软删语义（Enable=false + 删除审计字段），
    ///       其余实体（Vol 的 Sys_* 等）沿用 Vol 的 DeleteWithType。
    /// </summary>
    protected virtual WebResponseContent ExecuteLogicalDelete(object[] keys, List<TEntity> entities)
    {
        if (entities.Count == 0)
            return new WebResponseContent().Error("未找到要删除的数据");

        if (entities[0] is not YZHBaseEntity)
            return base.Del(keys, true);

        var userId = SafeGetUserId();
        var userName = SafeGetUserName();
        var logicalFields = new[] { "Enable", "DeleteID", "Deleter", "DeleteTime" };

        foreach (var entity in entities)
        {
            if (entity is YZHBaseEntity yzhEntity)
                yzhEntity.MarkAsDeleted(userId, userName);
            repository.Update(entity, logicalFields, saveChanges: false);
        }
        repository.SaveChanges();

        return new WebResponseContent().OK(ResponseType.DelSuccess);
    }

    #endregion

    #region 1. 校验器创建（子类可覆写）

    protected virtual EntityValidationHandler<TEntity> CreateValidator()
        => new EntityValidationHandler<TEntity>();

    #endregion

    #region 2. 生命周期钩子

    // 异步版本默认委托给同步版本：
    // - 子类只覆写同步版 → 异步管道同样生效（历史缺陷已修复）
    // - 子类需要异步 IO → 覆写异步版即可覆盖

    protected virtual EntityValidationResult OnAdding(TEntity entity) => OK;
    protected virtual Task<EntityValidationResult> OnAddingAsync(TEntity entity) => Task.FromResult(OnAdding(entity));
    protected virtual void OnAdded(TEntity entity) { }
    protected virtual Task OnAddedAsync(TEntity entity) { OnAdded(entity); return Task.CompletedTask; }

    protected virtual EntityValidationResult OnUpdating(TEntity entity, string? excludeBusinessKey) => OK;
    protected virtual Task<EntityValidationResult> OnUpdatingAsync(TEntity entity, string? excludeBusinessKey)
        => Task.FromResult(OnUpdating(entity, excludeBusinessKey));
    protected virtual void OnUpdated(TEntity entity) { }
    protected virtual Task OnUpdatedAsync(TEntity entity) { OnUpdated(entity); return Task.CompletedTask; }

    protected virtual EntityValidationResult OnDeleting(object[] keys, List<TEntity> entities) => OK;
    protected virtual Task<EntityValidationResult> OnDeletingAsync(object[] keys, List<TEntity> entities)
        => Task.FromResult(OnDeleting(keys, entities));
    protected virtual void OnDeleted(object[] keys) { }
    protected virtual Task OnDeletedAsync(object[] keys) { OnDeleted(keys); return Task.CompletedTask; }

    private static readonly EntityValidationResult OK = new EntityValidationResult().OK();

    #endregion

    #region 3. 同步写入管道

    public override WebResponseContent Add(SaveModel saveDataModel)
    {
        var pre = Preprocess(saveDataModel);
        if (pre.Error != null) return pre.Error;

        var entity = pre.Entity!;

        try
        {
            ApplyAuditInfo(entity, isAdd: true);

            var validateResult = CreateValidator().Validate(entity, ValidationAction.Add);
            if (!validateResult.IsValid) return validateResult.ToWebResponse();
        }
        catch (Exception ex)
        {
            return ExceptionSanitizer.Sanitize(ex, "数据校验");
        }

        try
        {
            var customResult = OnAdding(entity);
            if (!customResult.IsValid) return customResult.ToWebResponse();

            // 钩子中的赋值必须写回 SaveModel，否则 Vol 内部二次转换会丢弃
            WriteBackToSaveModel(saveDataModel, entity, AuditFieldNames);
        }
        catch (YZHException bizEx)
        {
            return ExceptionSanitizer.Sanitize(bizEx, "新增");
        }
        catch (Exception ex)
        {
            return ExceptionSanitizer.Sanitize(ex, "新增");
        }

        try
        {
            var result = base.Add(saveDataModel);
            if (!result.Status)
                return ExceptionSanitizer.SanitizeResponse(result, "新增");

            SafeRun(() => OnAdded(entity), "OnAdded");
            return result;
        }
        catch (YZHException bizEx)
        {
            return ExceptionSanitizer.Sanitize(bizEx, "新增");
        }
        catch (Exception ex)
        {
            return ExceptionSanitizer.Sanitize(ex, "新增");
        }
    }

    public override WebResponseContent Update(SaveModel saveDataModel)
    {
        var pre = Preprocess(saveDataModel);
        if (pre.Error != null) return pre.Error;

        var entity = pre.Entity!;

        try
        {
            ApplyAuditInfo(entity, isAdd: false);

            var validateResult = CreateValidator()
                .Validate(entity, ValidationAction.Update, pre.BusinessKey);
            if (!validateResult.IsValid) return validateResult.ToWebResponse();
        }
        catch (Exception ex)
        {
            return ExceptionSanitizer.Sanitize(ex, "数据校验");
        }

        try
        {
            var customResult = OnUpdating(entity, pre.BusinessKey);
            if (!customResult.IsValid) return customResult.ToWebResponse();

            WriteBackToSaveModel(saveDataModel, entity, AuditFieldNames);
        }
        catch (YZHException bizEx)
        {
            return ExceptionSanitizer.Sanitize(bizEx, "修改");
        }
        catch (Exception ex)
        {
            return ExceptionSanitizer.Sanitize(ex, "修改");
        }

        try
        {
            var result = base.Update(saveDataModel);
            if (!result.Status)
                return ExceptionSanitizer.SanitizeResponse(result, "修改");

            SafeRun(() => OnUpdated(entity), "OnUpdated");
            return result;
        }
        catch (YZHException bizEx)
        {
            return ExceptionSanitizer.Sanitize(bizEx, "修改");
        }
        catch (Exception ex)
        {
            return ExceptionSanitizer.Sanitize(ex, "修改");
        }
    }

    public override WebResponseContent Del(object[] keys, bool delList = true)
    {
        List<TEntity> entityList;
        try
        {
            entityList = LoadEntitiesByKeys(keys);
            if (entityList.Count == 0)
                return new WebResponseContent().Error("未找到要删除的数据");
        }
        catch (Exception ex)
        {
            return ExceptionSanitizer.Sanitize(ex, "删除");
        }

        var deleteAttr = typeof(TEntity).GetCustomAttribute<YZHDeleteStrategyAttribute>();
        var deleteMode = deleteAttr?.Mode ?? DeleteMode.Logical;

        try
        {
            var customResult = OnDeleting(keys, entityList);
            if (!customResult.IsValid) return customResult.ToWebResponse();
        }
        catch (YZHException bizEx)
        {
            return ExceptionSanitizer.Sanitize(bizEx, "删除");
        }
        catch (Exception ex)
        {
            return ExceptionSanitizer.Sanitize(ex, "删除");
        }

        try
        {
            var result = deleteMode switch
            {
                DeleteMode.Physical => ExecutePhysicalDelete(keys),
                DeleteMode.Cascade => ExecuteCascadeDelete(keys, entityList, deleteAttr),
                _ => ExecuteLogicalDelete(keys, entityList)
            };

            if (!result.Status)
                return ExceptionSanitizer.SanitizeResponse(result, "删除");

            SafeRun(() => OnDeleted(keys), "OnDeleted");
            return result;
        }
        catch (YZHException bizEx)
        {
            return ExceptionSanitizer.Sanitize(bizEx, "删除");
        }
        catch (Exception ex)
        {
            return ExceptionSanitizer.Sanitize(ex, "删除");
        }
    }

    #endregion

    #region 4. 异步写入管道（与同步同一条管道）

    public override async Task<WebResponseContent> AddAsync(SaveModel saveDataModel)
    {
        var pre = Preprocess(saveDataModel);
        if (pre.Error != null) return pre.Error;

        var entity = pre.Entity!;

        try
        {
            ApplyAuditInfo(entity, isAdd: true);

            var validateResult = CreateValidator().Validate(entity, ValidationAction.Add);
            if (!validateResult.IsValid) return validateResult.ToWebResponse();
        }
        catch (Exception ex)
        {
            return ExceptionSanitizer.Sanitize(ex, "数据校验");
        }

        try
        {
            var customResult = await OnAddingAsync(entity);
            if (!customResult.IsValid) return customResult.ToWebResponse();

            WriteBackToSaveModel(saveDataModel, entity, AuditFieldNames);
        }
        catch (YZHException bizEx)
        {
            return ExceptionSanitizer.Sanitize(bizEx, "新增");
        }
        catch (Exception ex)
        {
            return ExceptionSanitizer.Sanitize(ex, "新增");
        }

        try
        {
            var result = await base.AddAsync(saveDataModel);
            if (!result.Status)
                return ExceptionSanitizer.SanitizeResponse(result, "新增");

            await SafeRunAsync(() => OnAddedAsync(entity), "OnAddedAsync");
            return result;
        }
        catch (YZHException bizEx)
        {
            return ExceptionSanitizer.Sanitize(bizEx, "新增");
        }
        catch (Exception ex)
        {
            return ExceptionSanitizer.Sanitize(ex, "新增");
        }
    }

    public override async Task<WebResponseContent> UpdateAsync(SaveModel saveDataModel)
    {
        var pre = Preprocess(saveDataModel);
        if (pre.Error != null) return pre.Error;

        var entity = pre.Entity!;

        try
        {
            ApplyAuditInfo(entity, isAdd: false);

            var validateResult = CreateValidator()
                .Validate(entity, ValidationAction.Update, pre.BusinessKey);
            if (!validateResult.IsValid) return validateResult.ToWebResponse();
        }
        catch (Exception ex)
        {
            return ExceptionSanitizer.Sanitize(ex, "数据校验");
        }

        try
        {
            var customResult = await OnUpdatingAsync(entity, pre.BusinessKey);
            if (!customResult.IsValid) return customResult.ToWebResponse();

            WriteBackToSaveModel(saveDataModel, entity, AuditFieldNames);
        }
        catch (YZHException bizEx)
        {
            return ExceptionSanitizer.Sanitize(bizEx, "修改");
        }
        catch (Exception ex)
        {
            return ExceptionSanitizer.Sanitize(ex, "修改");
        }

        try
        {
            var result = await base.UpdateAsync(saveDataModel);
            if (!result.Status)
                return ExceptionSanitizer.SanitizeResponse(result, "修改");

            await SafeRunAsync(() => OnUpdatedAsync(entity), "OnUpdatedAsync");
            return result;
        }
        catch (YZHException bizEx)
        {
            return ExceptionSanitizer.Sanitize(bizEx, "修改");
        }
        catch (Exception ex)
        {
            return ExceptionSanitizer.Sanitize(ex, "修改");
        }
    }

    public override async Task<WebResponseContent> DelAsync(object[] keys, bool delList = true)
    {
        List<TEntity> entityList;
        try
        {
            entityList = await LoadEntitiesByKeysAsync(keys);
            if (entityList.Count == 0)
                return new WebResponseContent().Error("未找到要删除的数据");
        }
        catch (Exception ex)
        {
            return ExceptionSanitizer.Sanitize(ex, "删除");
        }

        var deleteAttr = typeof(TEntity).GetCustomAttribute<YZHDeleteStrategyAttribute>();
        var deleteMode = deleteAttr?.Mode ?? DeleteMode.Logical;

        try
        {
            var customResult = await OnDeletingAsync(keys, entityList);
            if (!customResult.IsValid) return customResult.ToWebResponse();
        }
        catch (YZHException bizEx)
        {
            return ExceptionSanitizer.Sanitize(bizEx, "删除");
        }
        catch (Exception ex)
        {
            return ExceptionSanitizer.Sanitize(ex, "删除");
        }

        try
        {
            var result = deleteMode switch
            {
                DeleteMode.Physical => await ExecutePhysicalDeleteAsync(keys),
                DeleteMode.Cascade => ExecuteCascadeDelete(keys, entityList, deleteAttr),
                _ => ExecuteLogicalDelete(keys, entityList)
            };

            if (!result.Status)
                return ExceptionSanitizer.SanitizeResponse(result, "删除");

            await SafeRunAsync(() => OnDeletedAsync(keys), "OnDeletedAsync");
            return result;
        }
        catch (YZHException bizEx)
        {
            return ExceptionSanitizer.Sanitize(bizEx, "删除");
        }
        catch (Exception ex)
        {
            return ExceptionSanitizer.Sanitize(ex, "删除");
        }
    }

    #endregion

    #region 5. 导入导出管道（异常脱敏）

    public override WebResponseContent Export(PageDataOptions pageData)
    {
        try
        {
            var result = base.Export(pageData);
            return result.Status ? result : ExceptionSanitizer.SanitizeResponse(result, "导出");
        }
        catch (Exception ex)
        {
            return ExceptionSanitizer.Sanitize(ex, "导出");
        }
    }

    public override async Task<WebResponseContent> ExportAsync(PageDataOptions pageData)
    {
        try
        {
            var result = await base.ExportAsync(pageData);
            return result.Status ? result : ExceptionSanitizer.SanitizeResponse(result, "导出");
        }
        catch (Exception ex)
        {
            return ExceptionSanitizer.Sanitize(ex, "导出");
        }
    }

    public override WebResponseContent Import(List<IFormFile> files)
    {
        try
        {
            var result = base.Import(files);
            return result.Status ? result : ExceptionSanitizer.SanitizeResponse(result, "导入");
        }
        catch (Exception ex)
        {
            return ExceptionSanitizer.Sanitize(ex, "导入");
        }
    }

    public override async Task<WebResponseContent> ImportAsync(List<IFormFile> files)
    {
        try
        {
            var result = await base.ImportAsync(files);
            return result.Status ? result : ExceptionSanitizer.SanitizeResponse(result, "导入");
        }
        catch (Exception ex)
        {
            return ExceptionSanitizer.Sanitize(ex, "导入");
        }
    }

    #endregion

    #region 6. 删除策略执行器

    /// <summary>物理删除：直接从数据库移除记录</summary>
    protected virtual WebResponseContent ExecutePhysicalDelete(object[] keys)
    {
        try
        {
            var deleted = DBServerProvider.SqlDapper.ExcuteNonQuery(
                BuildPhysicalDeleteSql(), new { keys });
            return deleted > 0
                ? new WebResponseContent().OK(ResponseType.DelSuccess)
                : new WebResponseContent().Error("删除失败，记录可能已不存在");
        }
        catch (Exception ex)
        {
            return ExceptionSanitizer.Sanitize(ex, "物理删除");
        }
    }

    /// <summary>物理删除（异步）</summary>
    protected virtual async Task<WebResponseContent> ExecutePhysicalDeleteAsync(object[] keys)
    {
        try
        {
            var deleted = await DBServerProvider.SqlDapper.ExcuteNonQueryAsync(
                BuildPhysicalDeleteSql(), new { keys });
            return deleted > 0
                ? new WebResponseContent().OK(ResponseType.DelSuccess)
                : new WebResponseContent().Error("删除失败，记录可能已不存在");
        }
        catch (Exception ex)
        {
            return ExceptionSanitizer.Sanitize(ex, "物理删除");
        }
    }

    private string BuildPhysicalDeleteSql()
    {
        var tableName = GetTableName();
        var keyName = typeof(TEntity).GetKeyName();
        return $"DELETE FROM {tableName} WHERE {keyName} IN @keys";
    }

    /// <summary>
    /// 级联删除：删除主表 + 关联从表。
    /// 默认实现退化为逻辑删除，子类可覆写实现真正的级联。
    /// </summary>
    protected virtual WebResponseContent ExecuteCascadeDelete(
        object[] keys, List<TEntity> entities, YZHDeleteStrategyAttribute? attr)
        => ExecuteLogicalDelete(keys, entities);

    #endregion

    #region 7. 管道内建工具

    private readonly struct PreprocessResult
    {
        public TEntity? Entity { get; init; }
        public string? BusinessKey { get; init; }
        public WebResponseContent? Error { get; init; }
    }

    /// <summary>
    /// 预转换：SaveModel → 实体，并提取业务唯一键。
    /// 转换失败时返回错误响应（不再静默跳过校验，避免脏数据入库）。
    /// </summary>
    private PreprocessResult Preprocess(SaveModel saveDataModel)
    {
        if (saveDataModel?.MainData == null || saveDataModel.MainData.Count == 0)
        {
            return new PreprocessResult
            {
                Error = new WebResponseContent().Error("提交数据为空")
            };
        }

        TEntity? entity;
        try
        {
            entity = saveDataModel.MainData.DicToEntity<TEntity>();
        }
        catch (Exception ex)
        {
            Logger.Error($"SaveModel 转换实体失败 [{typeof(TEntity).Name}]: {ex.Message}");
            return new PreprocessResult
            {
                Error = new WebResponseContent().Error("提交数据格式有误，无法解析")
            };
        }

        if (entity == null)
        {
            return new PreprocessResult
            {
                Error = new WebResponseContent().Error("提交数据为空")
            };
        }

        return new PreprocessResult
        {
            Entity = entity,
            BusinessKey = ExtractBusinessKeyValue(entity, saveDataModel)
        };
    }

    /// <summary>读取业务唯一键值（覆写 BusinessKeyField 即可切换字段）</summary>
    private string? ExtractBusinessKeyValue(TEntity entity, SaveModel saveDataModel)
    {
        var fieldName = BusinessKeyField;
        if (string.IsNullOrWhiteSpace(fieldName))
            return null;

        try
        {
            return typeof(TEntity).GetProperty(fieldName!)?.GetValue(entity)?.ToString();
        }
        catch
        {
            return saveDataModel.MainData != null
                && saveDataModel.MainData.TryGetValue(fieldName!, out var raw)
                ? raw?.ToString()
                : null;
        }
    }

    /// <summary>
    /// 把实体上的改动写回 SaveModel。
    /// Vol 内部会以 SaveModel 为准二次转换，钩子里的赋值不回写就会被丢弃。
    /// </summary>
    protected void WriteBackToSaveModel(SaveModel saveDataModel, TEntity entity, IEnumerable<string>? extraKeys = null)
    {
        if (saveDataModel?.MainData == null || entity == null) return;

        foreach (var key in saveDataModel.MainData.Keys.ToList())
        {
            var prop = typeof(TEntity).GetProperty(
                key, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop == null || !prop.CanRead) continue;

            try
            {
                saveDataModel.MainData[key] = prop.GetValue(entity);
            }
            catch (Exception ex)
            {
                Logger.Error($"写回 SaveModel 失败 [{typeof(TEntity).Name}.{key}]: {ex.Message}");
            }
        }

        if (extraKeys == null) return;

        foreach (var key in extraKeys)
        {
            if (saveDataModel.MainData.ContainsKey(key)) continue;

            var prop = typeof(TEntity).GetProperty(
                key, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop == null || !prop.CanRead) continue;

            try
            {
                saveDataModel.MainData[key] = prop.GetValue(entity);
            }
            catch (Exception ex)
            {
                Logger.Error($"写回 SaveModel 扩展字段失败 [{typeof(TEntity).Name}.{key}]: {ex.Message}");
            }
        }
    }

    /// <summary>YZH 实体需要回写的审计字段</summary>
    private static readonly string[] AuditFieldNames =
        { "CreateID", "Creator", "CreateDate", "ModifyID", "Modifier", "ModifyDate" };

    /// <summary>
    /// 填充审计字段。仅对 YZHBaseEntity 派生实体生效；
    /// Vol 的 Sys_* 实体沿用 Vol 自身的审计机制，避免破坏现有行为。
    /// </summary>
    protected virtual void ApplyAuditInfo(TEntity entity, bool isAdd)
    {
        if (entity is not YZHBaseEntity yzhEntity) return;

        var userId = SafeGetUserId();
        var userName = SafeGetUserName();

        if (isAdd && yzhEntity.CreateID == null)
            yzhEntity.FillCreateInfo(userId, userName);
        else
            yzhEntity.FillModifyInfo(userId, userName);
    }

    /// <summary>按主键批量加载实体（生成 IN 表达式，单条 SQL）</summary>
    protected List<TEntity> LoadEntitiesByKeys(object[] keys)
        => repository.FindAsIQueryable(BuildKeyInExpression(keys)).ToList();

    /// <summary>按主键批量加载实体（异步）</summary>
    protected async Task<List<TEntity>> LoadEntitiesByKeysAsync(object[] keys)
        => await repository.FindAsIQueryable(BuildKeyInExpression(keys)).ToListAsync();

    /// <summary>
    /// 构造主键 IN 表达式。
    /// 修复历史缺陷：旧实现只用 keys[0] 构造 Equal 条件，批量删除时后续记录不参与校验。
    /// </summary>
    private static Expression<Func<TEntity, bool>> BuildKeyInExpression(object[] keys)
    {
        var keyName = typeof(TEntity).GetKeyName();
        var parameter = Expression.Parameter(typeof(TEntity), "x");
        var property = Expression.Property(parameter, keyName);
        var propertyType = ((PropertyInfo)property.Member).PropertyType;
        var valueType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

        var listType = typeof(List<>).MakeGenericType(valueType);
        var typedValues = (System.Collections.IList)Activator.CreateInstance(listType)!;
        foreach (var key in keys.Distinct())
        {
            if (key == null) continue;
            typedValues.Add(Convert.ChangeType(key, valueType));
        }

        var containsMethod = typeof(Enumerable).GetMethods()
            .First(m => m.Name == nameof(Enumerable.Contains) && m.GetParameters().Length == 2)
            .MakeGenericMethod(valueType);

        Expression propertyAccess = property;
        if (propertyType != valueType)
            propertyAccess = Expression.Convert(property, valueType);

        var body = Expression.Call(
            containsMethod,
            Expression.Constant(typedValues, listType),
            propertyAccess);

        return Expression.Lambda<Func<TEntity, bool>>(body, parameter);
    }

    private static int SafeGetUserId()
    {
        try { return UserContext.Current.UserId; }
        catch { return 0; }
    }

    private static string SafeGetUserName()
    {
        try { return UserContext.Current.UserTrueName ?? UserContext.Current.UserName ?? "system"; }
        catch { return "system"; }
    }

    private static void SafeRun(Action action, string hookName)
    {
        try { action(); }
        catch (Exception ex) { Logger.Error($"{hookName} 后置处理异常: {ex.Message}"); }
    }

    private static async Task SafeRunAsync(Func<Task> action, string hookName)
    {
        try { await action(); }
        catch (Exception ex) { Logger.Error($"{hookName} 后置处理异常: {ex.Message}"); }
    }

    #endregion

    #region 8. 辅助方法

    /// <summary>获取关联表记录数（用于删除校验）。软删条件由 SoftDeleteFilter 决定。</summary>
    protected int GetRelatedCount(string relatedTable, string foreignKeyColumn, object foreignKeyValue)
    {
        var where = $"{foreignKeyColumn} = @val";
        if (!string.IsNullOrWhiteSpace(SoftDeleteFilter))
            where += $" AND ({SoftDeleteFilter})";

        var sql = $"SELECT COUNT(1) FROM {relatedTable} WHERE {where}";
        return Convert.ToInt32(DBServerProvider.SqlDapper.ExecuteScalar(sql, new { val = foreignKeyValue }));
    }

    /// <summary>获取关联表记录数（多条件版本）</summary>
    protected int GetRelatedCount(string relatedTable, Dictionary<string, object> conditions)
    {
        var clauses = conditions.Select(kv => $"{kv.Key} = @{kv.Key}").ToList();
        if (!string.IsNullOrWhiteSpace(SoftDeleteFilter))
            clauses.Add($"({SoftDeleteFilter})");

        var sql = $"SELECT COUNT(1) FROM {relatedTable} WHERE {string.Join(" AND ", clauses)}";
        return Convert.ToInt32(DBServerProvider.SqlDapper.ExecuteScalar(sql, conditions));
    }

    /// <summary>获取实体表名</summary>
    protected string GetTableName()
    {
        var entityAttr = typeof(TEntity).GetCustomAttribute<YZH.Entity.EntityAttribute>();
        return entityAttr?.TableName ?? typeof(TEntity).Name;
    }

    #endregion
}

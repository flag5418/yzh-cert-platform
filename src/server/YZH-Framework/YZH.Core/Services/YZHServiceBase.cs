using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using VOL.Core.BaseProvider;
using VOL.Core.DBManager;
using VOL.Core.Enums;
using VOL.Core.Extensions;
using VOL.Core.Services;
using VOL.Core.Utilities;
using VOL.Entity.DomainModels;
using VOL.Entity.SystemModels;
using YZH.Core.Attributes;
using YZH.Core.Validation;
using YZH.Core.Exceptions;

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
    /// <summary>级联删除：主表 + 关联从表（TODO: Phase 3）</summary>
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
///   [YZHDeleteStrategy(Mode = DeleteMode.Cascade, CascadeEntities = typeof(Detail[]))]
///   public class Order : YZHBaseEntity { }
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class YZHDeleteStrategyAttribute : Attribute
{
    /// <summary>删除模式（默认 Logical）</summary>
    public DeleteMode Mode { get; set; } = DeleteMode.Logical;

    /// <summary>级联删除的实体类型列表（仅 Mode=Cascade 时有效）</summary>
    public Type[] CascadeEntities { get; set; } = null;

    /// <summary>是否允许强制删除有关联数据（默认 false）</summary>
    public bool ForceDelete { get; set; } = false;
}

/// <summary>
/// YZH 业务 Service 通用抽象根，继承 Vol 的 ServiceBase。
///
/// 职责（只管通用能力，不含场景特化逻辑）：
/// 1. 统一异常兜底 — 所有 Add/Update/Del 的异常在此层捕获、脱敏
/// 2. 自动校验管道 — EntityValidationHandler 自动执行 [Required]/[UniqueField]
/// 3. 声明式删除策略 — 读取 [YZHDeleteStrategy] 特性决定 Logical/Physical/Cascade
/// 4. 完整生命周期 — 6 个 virtual 方法
/// 5. QueryView 支持
///
/// 删除策略（通过 [YZHDeleteStrategy] 特性声明）：
///   - Logical（默认）：设置 Enable=false + 填充删除信息
///   - Physical：直接物理删除
///   - Cascade：级联删除关联从表（TODO: Phase 3 完善）
///
/// 业务 Service 不直接继承此类，应继承场景专用基类：
/// - 单表 CRUD → YZHTableServiceBase
/// - 左树右表 → YZHTreeTableServiceBase
/// - 关联表   → YZHLinkTableServiceBase
/// - 纯树形   → YZHTreeServiceBase
/// </summary>
public abstract class YZHServiceBase<TEntity, TRepository>
    : ServiceBase<TEntity, TRepository>
    where TEntity : BaseEntity
    where TRepository : IRepository<TEntity>
{
    public YZHServiceBase() { }
    public YZHServiceBase(TRepository repository) : base(repository) { }

    #region 1. 校验器创建（子类可覆写）

    protected virtual EntityValidationHandler<TEntity> CreateValidator()
        => new EntityValidationHandler<TEntity>();

    #endregion

    #region 2. 生命周期虚方法（子类按需覆写）

    // ====== 新增 ======
    protected virtual EntityValidationResult OnAdding(TEntity entity) => OK;
    protected virtual void OnAdded(TEntity entity) { }

    // ====== 修改 ======
    protected virtual EntityValidationResult OnUpdating(TEntity entity, string? excludeCode) => OK;
    protected virtual void OnUpdated(TEntity entity) { }

    // ====== 删除 ======
    protected virtual EntityValidationResult OnDeleting(object[] keys, List<TEntity> entities) => OK;
    protected virtual void OnDeleted(object[] keys) { }

    private static readonly EntityValidationResult OK = new EntityValidationResult().OK();

    #endregion

    #region 3. 重写 Add：生命周期 + 校验 + 异常兜底

    public override WebResponseContent Add(SaveModel saveDataModel)
    {
        // 1. 空数据检查
        if (saveDataModel?.MainData == null || saveDataModel.MainData.Count == 0)
            return new WebResponseContent().Error("提交数据为空");

        // 2. 预转换实体用于校验（失败则跳过预校验，交给 Vol 框架处理）
        TEntity? entity = default;
        bool entityConverted = false;
        try
        {
            entity = saveDataModel.MainData.DicToEntity<TEntity>();
            entityConverted = true;
        }
        catch
        {
            // DicToEntity 可能因 JSON 反序列化产生的 JsonElement 类型不匹配而失败
            // 不阻断流程，Vol 框架的 base.Add 内部有自己的转换逻辑
        }

        // 3. 自动校验（唯一性 + 必填）— 仅在预转换成功时执行
        if (entityConverted && entity != null)
        {
            try
            {
                var validateResult = CreateValidator().Validate(entity, ValidationAction.Add);
                if (!validateResult.IsValid)
                    return validateResult.ToWebResponse();
            }
            catch (Exception ex)
            {
                return ExceptionSanitizer.Sanitize(ex, "数据校验");
            }

            // 4. 自定义新增前校验（业务规则）
            try
            {
                var customResult = OnAdding(entity);
                if (!customResult.IsValid)
                    return customResult.ToWebResponse();
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

        // 5. 执行保存（Vol 框架标准流程）— 异常兜底
        try
        {
            var result = base.Add(saveDataModel);

            // Vol 框架内部 catch 异常后把 ex.Message 直接放入 response.Message
            // 需要检测并脱敏
            if (!result.Status)
                result = ExceptionSanitizer.SanitizeResponse(result, "新增");

            // 6. 新增后处理（异常不影响已保存数据，仅记录日志）
            if (result.Status && entityConverted && entity != null)
            {
                try
                {
                    OnAdded(entity);
                }
                catch (Exception afterEx)
                {
                    Logger.Error($"OnAdded 后置处理异常: {afterEx.Message}");
                }
            }
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

    #endregion

    #region 4. 重写 Update：生命周期 + 校验 + 异常兜底

    public override WebResponseContent Update(SaveModel saveDataModel)
    {
        // 1. 空数据检查
        if (saveDataModel?.MainData == null || saveDataModel.MainData.Count == 0)
            return new WebResponseContent().Error("提交数据为空");

        // 2. 预转换实体用于校验（失败则跳过预校验，交给 Vol 框架处理）
        TEntity? entity = default;
        bool entityConverted = false;
        string? excludeCode = null;
        try
        {
            entity = saveDataModel.MainData.DicToEntity<TEntity>();
            entityConverted = true;
            // 3. 获取 Code（业务主键，用于排除自身）
            excludeCode = typeof(TEntity)
                .GetProperty("Code")?.GetValue(entity)?.ToString();
        }
        catch
        {
            // DicToEntity 可能因 JSON 反序列化产生的 JsonElement 类型不匹配而失败
            // 不阻断流程，Vol 框架的 base.Update 内部有自己的转换逻辑
            // 但可以尝试从字典直接取 Code
            if (saveDataModel.MainData.TryGetValue("Code", out var codeVal))
                excludeCode = codeVal?.ToString();
        }

        // 4. 自动校验 — 仅在预转换成功时执行
        if (entityConverted && entity != null)
        {
            try
            {
                var validateResult = CreateValidator()
                    .Validate(entity, ValidationAction.Update, excludeCode);
                if (!validateResult.IsValid)
                    return validateResult.ToWebResponse();
            }
            catch (Exception ex)
            {
                return ExceptionSanitizer.Sanitize(ex, "数据校验");
            }

            // 5. 自定义修改前校验
            try
            {
                var customResult = OnUpdating(entity, excludeCode);
                if (!customResult.IsValid)
                    return customResult.ToWebResponse();
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

        // 6. 执行保存 — 异常兜底
        try
        {
            var result = base.Update(saveDataModel);

            // Vol 框架内部 catch 异常后把 ex.Message 直接放入 response.Message
            // 需要检测并脱敏
            if (!result.Status)
                result = ExceptionSanitizer.SanitizeResponse(result, "修改");

            if (result.Status && entityConverted && entity != null)
            {
                try
                {
                    OnUpdated(entity);
                }
                catch (Exception afterEx)
                {
                    Logger.Error($"OnUpdated 后置处理异常: {afterEx.Message}");
                }
            }
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

    #endregion

    #region 5. 重写 Del：生命周期 + 声明式删除策略 + 异常兜底

    public override WebResponseContent Del(object[] keys, bool delList = true)
    {
        // 1. 查询待删除实体
        List<TEntity> entityList;
        try
        {
            var keyName = typeof(TEntity).GetKeyName();
            var keyExpression = keyName.CreateExpression<TEntity>(keys[0].ToString()!, LinqExpressionType.Equal);
            entityList = repository.FindAsIQueryable(keyExpression).ToList();

            if (entityList == null || entityList.Count == 0)
                return new WebResponseContent().Error("未找到要删除的数据");
        }
        catch (Exception ex)
        {
            return ExceptionSanitizer.Sanitize(ex, "删除");
        }

        // 2. 读取声明式删除策略
        var deleteAttr = typeof(TEntity).GetCustomAttribute<YZHDeleteStrategyAttribute>();
        var deleteMode = deleteAttr?.Mode ?? DeleteMode.Logical;

        // 3. 删除前校验（关联关系校验）
        try
        {
            var customResult = OnDeleting(keys, entityList);
            if (!customResult.IsValid)
                return customResult.ToWebResponse();
        }
        catch (YZHException bizEx)
        {
            return ExceptionSanitizer.Sanitize(bizEx, "删除");
        }
        catch (Exception ex)
        {
            return ExceptionSanitizer.Sanitize(ex, "删除");
        }

        // 4. 根据策略执行删除
        WebResponseContent result;
        try
        {
            result = deleteMode switch
            {
                DeleteMode.Physical => ExecutePhysicalDelete(keys),
                DeleteMode.Cascade => ExecuteCascadeDelete(keys, entityList, deleteAttr),
                _ => base.Del(keys, delList) // Logical: 默认走 Vol 框架的逻辑删除
            };

            if (!result.Status)
                result = ExceptionSanitizer.SanitizeResponse(result, "删除");

            if (result.Status)
            {
                try
                {
                    OnDeleted(keys);
                }
                catch (Exception afterEx)
                {
                    Logger.Error($"OnDeleted 后置处理异常: {afterEx.Message}");
                }
            }
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

    /// <summary>物理删除：直接从数据库移除记录</summary>
    private WebResponseContent ExecutePhysicalDelete(object[] keys)
    {
        try
        {
            var tableName = GetTableName();
            var keyName = typeof(TEntity).GetKeyName();
            var sql = $"DELETE FROM {tableName} WHERE {keyName} IN @keys";
            var affected = DBServerProvider.SqlDapper.ExcuteNonQuery(sql, new { keys });
            return affected > 0
                ? new WebResponseContent().OK()
                : new WebResponseContent().Error("删除失败，记录可能已不存在");
        }
        catch (Exception ex)
        {
            return ExceptionSanitizer.Sanitize(ex, "物理删除");
        }
    }

    /// <summary>级联删除：删除主表 + 关联从表</summary>
    private WebResponseContent ExecuteCascadeDelete(object[] keys, List<TEntity> entities, YZHDeleteStrategyAttribute? attr)
    {
        // TODO: Phase 3 完善级联删除逻辑
        // 当前实现：先走标准逻辑删除主表，后续扩展级联删除从表
        // attr?.CascadeEntities 可用于获取需要级联的实体类型列表
        return base.Del(keys, true);
    }

    #endregion

    #region 6. 辅助方法

    /// <summary>获取关联表记录数（用于删除校验）</summary>
    protected int GetRelatedCount(string relatedTable, string foreignKeyColumn, object foreignKeyValue)
    {
        var sql = $"SELECT COUNT(1) FROM {relatedTable} " +
                  $"WHERE {foreignKeyColumn} = @val AND (IsDeleted = 0 OR IsDeleted IS NULL)";
        return Convert.ToInt32(DBServerProvider.SqlDapper.ExecuteScalar(sql, new { val = foreignKeyValue }));
    }

    /// <summary>获取关联表记录数（多条件版本）</summary>
    protected int GetRelatedCount(string relatedTable, Dictionary<string, object> conditions)
    {
        var whereClauses = conditions.Select(kv => $"{kv.Key} = @{kv.Key}").ToList();
        var sql = $"SELECT COUNT(1) FROM {relatedTable} " +
                  $"WHERE {string.Join(" AND ", whereClauses)} AND (IsDeleted = 0 OR IsDeleted IS NULL)";
        return Convert.ToInt32(DBServerProvider.SqlDapper.ExecuteScalar(sql, conditions));
    }

    /// <summary>获取实体表名</summary>
    protected string GetTableName()
    {
        var entityAttr = typeof(TEntity).GetCustomAttribute<VOL.Entity.EntityAttribute>();
        return entityAttr?.TableName ?? typeof(TEntity).Name;
    }

    #endregion
}

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using YZH.Core.BaseProvider;
using YZH.Core.DBManager;
using YZH.Core.Enums;
using YZH.Core.Exceptions;
using YZH.Core.Extensions;
using YZH.Core.Services;
using YZH.Core.Utilities;
using YZH.Core.Validation;
using YZH.Entity.DomainModels;
using YZH.Entity.SystemModels;
using YZH.Entity.Admin.Platform.Base;

namespace YZH.Core.Services
{
    /// <summary>
    /// 体系认证平台统一Service基类
    /// 整合Vol的CRUD能力 + YZH的校验/生命周期/删除策略/异常脱敏
    /// 
    /// 所有业务Service应继承此类，而非直接继承ServiceBase
    /// 
    /// 能力清单：
    /// 1. 自动校验管道 - EntityValidationHandler自动执行[Required]/[UniqueField]
    /// 2. 生命周期钩子 - OnAdding/OnAdded/OnUpdating/OnUpdated/OnDeleting/OnDeleted
    /// 3. 声明式删除策略 - 读取[YZHDeleteStrategy]特性决定Logical/Physical/Cascade
    /// 4. 异常脱敏 - ExceptionSanitizer统一处理异常，暴露友好消息
    /// 5. 配置生成 - GetPageConfig()自动生成前端页面配置
    /// </summary>
    public abstract class CertServiceBase<TEntity, TRepository>
        : ServiceBase<TEntity, TRepository>
        where TEntity : BaseEntity
        where TRepository : IRepository<TEntity>
    {
        public CertServiceBase() { }
        public CertServiceBase(TRepository repository) : base(repository) { }

        #region 1. 校验器创建（子类可覆写）

        /// <summary>
        /// 创建实体校验器
        /// 子类可覆写以自定义校验逻辑
        /// </summary>
        protected virtual EntityValidationHandler<TEntity> CreateValidator()
            => new EntityValidationHandler<TEntity>();

        #endregion

        #region 2. 生命周期钩子（子类按需覆写）

        // ===== 新增 =====
        /// <summary>
        /// 新增前校验钩子
        /// 返回EntityValidationResult.OK()表示通过，.Error()表示拦截
        /// </summary>
        protected virtual EntityValidationResult OnAdding(TEntity entity) => OK;

        /// <summary>
        /// 新增后处理钩子
        /// 异常不影响已保存数据，仅记录日志
        /// </summary>
        protected virtual void OnAdded(TEntity entity) { }

        // ===== 修改 =====
        /// <summary>
        /// 修改前校验钩子
        /// excludeCode用于唯一性校验时排除自身
        /// </summary>
        protected virtual EntityValidationResult OnUpdating(TEntity entity, string? excludeCode) => OK;

        /// <summary>
        /// 修改后处理钩子
        /// </summary>
        protected virtual void OnUpdated(TEntity entity) { }

        // ===== 删除 =====
        /// <summary>
        /// 删除前校验钩子
        /// 可用于关联关系校验
        /// </summary>
        protected virtual EntityValidationResult OnDeleting(object[] keys, List<TEntity> entities) => OK;

        /// <summary>
        /// 删除后处理钩子
        /// </summary>
        protected virtual void OnDeleted(object[] keys) { }

        private static readonly EntityValidationResult OK = new EntityValidationResult().OK();

        #endregion

        #region 3. 配置获取方法

        /// <summary>
        /// 获取当前实体的完整页面配置
        /// 用于前端自动生成表格/表单/搜索配置
        /// </summary>
        public PageUIConfig GetPageConfig()
        {
            return EntityConfigGenerator.Generate<TEntity>();
        }

        /// <summary>
        /// 获取当前实体的表格列配置
        /// </summary>
        public List<FieldConfig> GetTableColumns()
        {
            return EntityConfigGenerator.GenerateTableColumns<TEntity>();
        }

        /// <summary>
        /// 获取当前实体的表单字段配置
        /// </summary>
        public List<FieldConfig> GetFormFields()
        {
            return EntityConfigGenerator.GenerateFormFields<TEntity>();
        }

        /// <summary>
        /// 获取当前实体的搜索条件配置
        /// </summary>
        public List<FieldConfig> GetSearchFields()
        {
            return EntityConfigGenerator.GenerateSearchFields<TEntity>();
        }

        #endregion

        #region 4. 重写Add：生命周期 + 校验 + 异常兜底

        public override WebResponseContent Add(SaveModel saveDataModel)
        {
            // 1. 空数据检查
            if (saveDataModel?.MainData == null || saveDataModel.MainData.Count == 0)
                return new WebResponseContent().Error("提交数据为空");

            // 2. 预转换实体用于校验
            TEntity? entity = default;
            bool entityConverted = false;
            try
            {
                entity = saveDataModel.MainData.DicToEntity<TEntity>();
                entityConverted = true;
            }
            catch
            {
                // DicToEntity可能失败，不阻断流程，交给Vol框架处理
            }

            // 3. 自动校验（唯一性 + 必填）
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

                // 4. 自定义新增前校验
                try
                {
                    var customResult = OnAdding(entity);
                    if (!customResult.IsValid)
                        return customResult.ToWebResponse();
                }
                catch (Exception ex)
                {
                    return ExceptionSanitizer.Sanitize(ex, "新增");
                }
            }

            // 5. 执行保存
            try
            {
                var result = base.Add(saveDataModel);

                // 6. 异常脱敏（Vol框架内部catch后可能暴露SQL/堆栈）
                if (!result.Status)
                    result = ExceptionSanitizer.SanitizeResponse(result, "新增");

                // 7. 新增后处理（异常不影响已保存数据）
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
            catch (Exception ex)
            {
                return ExceptionSanitizer.Sanitize(ex, "新增");
            }
        }

        #endregion

        #region 5. 重写Update：生命周期 + 校验 + 异常兜底

        public override WebResponseContent Update(SaveModel saveDataModel)
        {
            // 1. 空数据检查
            if (saveDataModel?.MainData == null || saveDataModel.MainData.Count == 0)
                return new WebResponseContent().Error("提交数据为空");

            // 2. 预转换实体用于校验
            TEntity? entity = default;
            bool entityConverted = false;
            string? excludeCode = null;
            try
            {
                entity = saveDataModel.MainData.DicToEntity<TEntity>();
                entityConverted = true;
                // 获取Code用于排除自身
                excludeCode = typeof(TEntity)
                    .GetProperty("Code")?.GetValue(entity)?.ToString();
            }
            catch
            {
                // 不阻断流程
            }

            // 3. 自动校验
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

                // 4. 自定义修改前校验
                try
                {
                    var customResult = OnUpdating(entity, excludeCode);
                    if (!customResult.IsValid)
                        return customResult.ToWebResponse();
                }
                catch (Exception ex)
                {
                    return ExceptionSanitizer.Sanitize(ex, "修改");
                }
            }

            // 5. 执行保存
            try
            {
                var result = base.Update(saveDataModel);

                // 6. 异常脱敏
                if (!result.Status)
                    result = ExceptionSanitizer.SanitizeResponse(result, "修改");

                // 7. 修改后处理
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
            catch (Exception ex)
            {
                return ExceptionSanitizer.Sanitize(ex, "修改");
            }
        }

        #endregion

        #region 6. 重写Del：生命周期 + 声明式删除策略 + 异常兜底

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

            // 3. 删除前校验
            try
            {
                var customResult = OnDeleting(keys, entityList);
                if (!customResult.IsValid)
                    return customResult.ToWebResponse();
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
                    _ => base.Del(keys, delList)
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
            catch (Exception ex)
            {
                return ExceptionSanitizer.Sanitize(ex, "删除");
            }
        }

        /// <summary>
        /// 物理删除：直接从数据库移除记录
        /// </summary>
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

        /// <summary>
        /// 级联删除：删除主表 + 关联从表
        /// 当前实现：先走标准逻辑删除，后续扩展
        /// </summary>
        private WebResponseContent ExecuteCascadeDelete(object[] keys, List<TEntity> entities, YZHDeleteStrategyAttribute? attr)
        {
            return base.Del(keys, true);
        }

        #endregion

        #region 7. 辅助方法

        /// <summary>
        /// 获取关联表记录数（用于删除前校验）
        /// </summary>
        protected int GetRelatedCount(string relatedTable, string foreignKeyColumn, object foreignKeyValue)
        {
            var sql = $"SELECT COUNT(1) FROM {relatedTable} " +
                      $"WHERE {foreignKeyColumn} = @val AND (IsDeleted = 0 OR IsDeleted IS NULL)";
            return Convert.ToInt32(DBServerProvider.SqlDapper.ExecuteScalar(sql, new { val = foreignKeyValue }));
        }

        /// <summary>
        /// 获取关联表记录数（多条件版本）
        /// </summary>
        protected int GetRelatedCount(string relatedTable, Dictionary<string, object> conditions)
        {
            var whereClauses = conditions.Select(kv => $"{kv.Key} = @{kv.Key}").ToList();
            var sql = $"SELECT COUNT(1) FROM {relatedTable} " +
                      $"WHERE {string.Join(" AND ", whereClauses)} AND (IsDeleted = 0 OR IsDeleted IS NULL)";
            return Convert.ToInt32(DBServerProvider.SqlDapper.ExecuteScalar(sql, conditions));
        }

        /// <summary>
        /// 获取实体表名
        /// </summary>
        protected string GetTableName()
        {
            var entityAttr = typeof(TEntity).GetCustomAttribute<YZH.Entity.EntityAttribute>();
            return entityAttr?.TableName ?? typeof(TEntity).Name;
        }

        #endregion
    }
}

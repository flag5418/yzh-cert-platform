using System.Linq.Expressions;
using Microsoft.Extensions.Logging;
using YZH.Core.DataBase;
using YZH.Core.Stand.Attributes;
using YZH.Core.Stand.Models;

namespace YZH.Core.Api.Services;

/// <summary>
///     通用实体操作服务 — 原子能力层
///     
///     核心设计：
///     1. 所有方法内部控制异常，返回 (T? data, string? err)
///     2. err=null 表示成功，err!=null 包含详细错误信息
///     3. 审计字段自动填充（CreateTime/CreateBy/UpdateTime/UpdateBy/DeleteTime/DeleteBy）
///     4. 软删除过滤：查询默认排除 IsDeleted=true
///     5. 缓存由调用方（Controller）控制，Service 不负责缓存
///     
///     所有方法可被 Controller 或任何其他代码直接调用
/// </summary>
public class EntityService<T> where T : BaseEntity
{
    private readonly IRepository<T> _repository;
    private readonly IAuditLogger _auditLogger;
    private readonly IUserContext _userContext;
    private readonly ILogger<EntityService<T>> _logger;

    public EntityService(
        IRepository<T> repository,
        IAuditLogger auditLogger,
        IUserContext userContext,
        ILogger<EntityService<T>> logger)
    {
        _repository = repository;
        _auditLogger = auditLogger;
        _userContext = userContext;
        _logger = logger;
    }

    // ==================== 查询操作（不抛异常，返回null表示无数据） ====================

    /// <summary>根据 ID 获取实体</summary>
    public virtual T? GetById(string id, bool includeDeleted = false)
    {
        try { return _repository.GetById(id, includeDeleted); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetById 错误，Type={Type}, Id={Id}", typeof(T).Name, id);
            return null;
        }
    }

    /// <summary>根据 Code 获取实体</summary>
    public virtual T? GetByCode(string code, bool includeDeleted = false)
    {
        try { return _repository.GetByCode(code, includeDeleted); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetByCode 错误，Type={Type}, Code={Code}", typeof(T).Name, code);
            return null;
        }
    }

    /// <summary>根据条件获取单条</summary>
    public virtual T? GetOne(Expression<Func<T, bool>> predicate, bool includeDeleted = false)
    {
        try { return _repository.GetOne(predicate, includeDeleted); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetOne 错误，Type={Type}", typeof(T).Name);
            return null;
        }
    }

    /// <summary>获取列表</summary>
    public virtual (List<T>? data, string? err) GetList(Expression<Func<T, bool>>? predicate = null, bool includeDeleted = false)
    {
        try { return (_repository.GetList(predicate, includeDeleted), null); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetList 错误，Type={Type}", typeof(T).Name);
            return (null, FormatError(ex, "查询列表"));
        }
    }

    /// <summary>分页查询</summary>
    public virtual ((List<T> items, int total)? data, string? err) GetPage(PagerOptions options, bool includeDeleted = false)
    {
        try
        {
            var (items, total) = _repository.GetPage(options, includeDeleted);
            return ((items, total), null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetPage 错误，Type={Type}", typeof(T).Name);
            return (null, FormatError(ex, "分页查询"));
        }
    }

    /// <summary>统计数量</summary>
    public virtual (int count, string? err) Count(Expression<Func<T, bool>>? predicate = null, bool includeDeleted = false)
    {
        try { return (_repository.Count(predicate, includeDeleted), null); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Count 错误，Type={Type}", typeof(T).Name);
            return (0, FormatError(ex, "统计数量"));
        }
    }

    /// <summary>判断是否存在</summary>
    public virtual (bool exists, string? err) Exists(Expression<Func<T, bool>> predicate, bool includeDeleted = false)
    {
        try { return (_repository.Exists(predicate, includeDeleted), null); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exists 错误，Type={Type}", typeof(T).Name);
            return (false, FormatError(ex, "存在性检查"));
        }
    }

    /// <summary>根据 Code 判断是否存在</summary>
    public virtual (bool exists, string? err) ExistsByCode(string code, bool includeDeleted = false)
    {
        try { return (_repository.Exists(e => e.Code == code, includeDeleted), null); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ExistsByCode 错误，Type={Type}, Code={Code}", typeof(T).Name, code);
            return (false, FormatError(ex, "Code存在性检查"));
        }
    }

    // ==================== 写入操作（返回 data + err） ====================

    /// <summary>
    ///     新增实体（自动填充 Code、审计字段）
    ///     返回 (entity, null) 表示成功，(null, errorMsg) 表示失败
    /// </summary>
    public virtual (T? entity, string? err) Insert(T entity, string? clientIp = null, bool saveChanges = true)
    {
        try
        {
            if (string.IsNullOrEmpty(entity.Code))
                entity.Code = Guid.NewGuid().ToString("N");

            FillCreateAudit(entity);
            var result = _repository.Insert(entity, saveChanges);

            var ctx = _userContext.GetRequestContext();
            if (clientIp != null) ctx.ClientIp = clientIp;
            _auditLogger.LogInsert(result, ctx);

            return (result, null);
        }
        catch (Exception ex)
        {
            var ctx = _userContext.GetRequestContext();
            _auditLogger.LogException(ex, "Insert", entity, ctx);
            _logger.LogError(ex, "Insert 错误，Type={Type}", typeof(T).Name);
            return (null, FormatError(ex, "新增"));
        }
    }

    /// <summary>
    ///     修改实体（自动填充 UpdateTime/UpdateBy 审计字段）
    /// </summary>
    public virtual (T? entity, string? err) Update(
        T entity,
        string? clientIp = null,
        bool saveChanges = true,
        string[]? updateFields = null)
    {
        try
        {
            FillUpdateAudit(entity);
            var result = updateFields != null
                ? _repository.Update(entity, saveChanges, updateFields)
                : _repository.Update(entity, saveChanges);

            var ctx = _userContext.GetRequestContext();
            if (clientIp != null) ctx.ClientIp = clientIp;
            _auditLogger.LogUpdate(result, ctx);

            return (result, null);
        }
        catch (Exception ex)
        {
            var ctx = _userContext.GetRequestContext();
            _auditLogger.LogException(ex, "Update", entity, ctx);
            _logger.LogError(ex, "Update 错误，Type={Type}", typeof(T).Name);
            return (null, FormatError(ex, "修改"));
        }
    }

    /// <summary>
    ///     根据 ID 删除（软删除/硬删除由实体特性决定）
    ///     返回 (true, null) 表示成功，(false, errorMsg) 表示失败
    /// </summary>
    public virtual (bool success, string? err) DeleteById(string id, string? clientIp = null, bool saveChanges = true)
    {
        try
        {
            var entity = _repository.GetById(id);
            if (entity == null) return (false, "记录不存在或已被删除");

            if (DeleteStrategyHelper.IsHardDelete<T>())
                _repository.Delete(id, saveChanges);
            else
            {
                FillDeleteAudit(entity);
                SoftDelete(entity, saveChanges);
            }

            var ctx = _userContext.GetRequestContext();
            if (clientIp != null) ctx.ClientIp = clientIp;
            _auditLogger.LogDelete(entity, ctx);

            return (true, null);
        }
        catch (Exception ex)
        {
            var ctx = _userContext.GetRequestContext();
            _auditLogger.LogException(ex, "DeleteById", null, ctx);
            _logger.LogError(ex, "DeleteById 错误，Type={Type}, Id={Id}", typeof(T).Name, id);
            return (false, FormatError(ex, "删除"));
        }
    }

    /// <summary>根据 Code 删除</summary>
    public virtual (bool success, string? err) DeleteByCode(string code, string? clientIp = null, bool saveChanges = true)
    {
        try
        {
            var entity = _repository.GetByCode(code);
            if (entity == null) return (false, "记录不存在或已被删除");

            if (DeleteStrategyHelper.IsHardDelete<T>())
                _repository.Delete(entity.Id, saveChanges);
            else
            {
                FillDeleteAudit(entity);
                SoftDelete(entity, saveChanges);
            }

            var ctx = _userContext.GetRequestContext();
            if (clientIp != null) ctx.ClientIp = clientIp;
            _auditLogger.LogDelete(entity, ctx);

            return (true, null);
        }
        catch (Exception ex)
        {
            var ctx = _userContext.GetRequestContext();
            _auditLogger.LogException(ex, "DeleteByCode", null, ctx);
            _logger.LogError(ex, "DeleteByCode 错误，Type={Type}, Code={Code}", typeof(T).Name, code);
            return (false, FormatError(ex, "删除"));
        }
    }

    // ==================== 批量操作（事务保护） ====================

    /// <summary>批量新增（事务保护）</summary>
    public virtual (List<T>? entities, string? err) InsertBatch(IEnumerable<T> entities, string? clientIp = null)
    {
        try
        {
            var list = entities.ToList();
            foreach (var entity in list)
            {
                if (string.IsNullOrEmpty(entity.Code))
                    entity.Code = Guid.NewGuid().ToString("N");
                FillCreateAudit(entity);
            }

            var txResult = _repository.ExecuteInTransaction(() =>
            {
                foreach (var entity in list)
                    _repository.Insert(entity, false);
            });

            if (!txResult.success)
            {
                var err = txResult.err ?? "批量新增失败";
                return (null, err);
            }

            var ctx = _userContext.GetRequestContext();
            if (clientIp != null) ctx.ClientIp = clientIp;
            _auditLogger.LogBatchInsert(list, ctx);

            return (list, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "InsertBatch 错误，Type={Type}", typeof(T).Name);
            return (null, FormatError(ex, "批量新增"));
        }
    }

    /// <summary>批量修改（事务保护）</summary>
    public virtual (List<T>? entities, string? err) UpdateBatch(IEnumerable<T> entities, string? clientIp = null)
    {
        try
        {
            var list = entities.ToList();
            var txResult = _repository.ExecuteInTransaction(() =>
            {
                foreach (var entity in list)
                {
                    FillUpdateAudit(entity);
                    _repository.Update(entity, false);
                }
            });

            if (!txResult.success)
            {
                var err = txResult.err ?? "批量修改失败";
                return (null, err);
            }

            var ctx = _userContext.GetRequestContext();
            if (clientIp != null) ctx.ClientIp = clientIp;
            _auditLogger.LogBatchUpdate(list, ctx);

            return (list, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateBatch 错误，Type={Type}", typeof(T).Name);
            return (null, FormatError(ex, "批量修改"));
        }
    }

    /// <summary>
    ///     批量按 Code 删除（软删除/硬删除由实体特性决定）
    ///     前端传 Codes 数组，后端循环处理
    /// </summary>
    public virtual (int count, string? err) DeleteBatch(IEnumerable<string> codes, bool hardDelete = false, string? clientIp = null)
    {
        try
        {
            var codeList = codes.ToList();
            using var tx = _repository.BeginTransaction();
            try
            {
                int count;
                if (hardDelete)
                {
                    count = 0;
                    foreach (var code in codeList)
                    {
                        var entity = _repository.GetByCode(code);
                        if (entity != null)
                        {
                            _repository.Delete(entity.Id, false);
                            count++;
                        }
                    }
                }
                else
                {
                    count = 0;
                    foreach (var code in codeList)
                    {
                        var entity = _repository.GetByCode(code);
                        if (entity != null)
                        {
                            FillDeleteAudit(entity);
                            _repository.Update(entity, false, new[] { "IsDeleted", "DeleteTime", "DeleteBy" });
                            count++;
                        }
                    }
                }

                tx.Commit();

                var ctx = _userContext.GetRequestContext();
                if (clientIp != null) ctx.ClientIp = clientIp;
                _auditLogger.LogBatchDelete<T>(codeList, ctx);

                return (count, null);
            }
            catch (Exception ex)
            {
                try { tx.Rollback(); } catch { /* 忽略回滚失败 */ }
                _logger.LogError(ex, "DeleteBatch 事务错误，Type={Type}", typeof(T).Name);
                return (0, FormatError(ex, "批量删除"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeleteBatch 错误，Type={Type}", typeof(T).Name);
            return (0, FormatError(ex, "批量删除"));
        }
    }

    // ==================== 审计字段填充 ====================

    private void FillCreateAudit(T entity)
    {
        entity.CreateTime = DateTime.UtcNow;
        entity.CreateBy = _userContext.UserCode;
    }

    private void FillUpdateAudit(T entity)
    {
        entity.UpdateTime = DateTime.UtcNow;
        entity.UpdateBy = _userContext.UserCode;
    }

    private void FillDeleteAudit(T entity)
    {
        entity.DeleteTime = DateTime.UtcNow;
        entity.DeleteBy = _userContext.UserCode;
    }

    private void SoftDelete(T entity, bool saveChanges)
    {
        entity.IsDeleted = true;
        _repository.Update(entity, saveChanges, new[] { "IsDeleted", "DeleteTime", "DeleteBy" });
    }

    // ==================== 错误格式化 ====================

    /// <summary>
    ///     将异常转换为用户友好的错误信息
    ///     内部保留原始异常详情以便调试
    /// </summary>
    private static string FormatError(Exception? ex, string operationName)
    {
        var msg = ex?.Message ?? "未知数据库错误";

        if (msg.Contains("Duplicate") || msg.Contains("UNIQUE") || msg.Contains("duplicate"))
            return $"{operationName}失败：数据已存在（唯一约束冲突）";

        if (msg.Contains("foreign key") || msg.Contains("FOREIGN KEY"))
            return $"{operationName}失败：数据被其他业务引用，无法操作";

        if (msg.Contains("Cannot insert the value NULL") || msg.Contains("cannot be null"))
            return $"{operationName}失败：必填字段为空";

        return $"{operationName}失败：{msg}";
    }
}

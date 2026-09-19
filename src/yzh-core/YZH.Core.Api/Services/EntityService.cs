using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.Logging;
using YZH.Core.Api.Services;
using YZH.Core.DataBase;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.DataBase.Models;
using YZH.Core.DataBase.Query;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Annotations;
using YZH.Core.Stand.Attributes;
using YZH.Core.Stand.Models;
using YZH.Core.Stand.Models.Result;
using YZH.Core.Stand.Models.Request;
using YZH.Core.Stand.Models.Entity;

namespace YZH.Core.Api.Services;

/// <summary>
///     通用实体操作服务 — 原子能力层（重构版）
///     
///     核心设计：
///     1. 所有方法返回 Result<T>，类型安全 + 链式调用
///     2. 审计字段自动填充（CreateTime/CreateBy/UpdateTime/UpdateBy/DeleteTime/DeleteBy）
///     3. 软删除过滤：查询默认排除 IsDeleted=true
///     4. 缓存由调用方（Controller）控制，Service 不负责缓存
///     
///     所有方法可被 Controller 或任何其他代码直接调用
/// </summary>
public class EntityService<T> where T : class, new()
{
    private readonly IDbOrm _dbOrm;
    private readonly IYzhAuditLogger _auditLogger;
    private readonly IUserContext _userContext;
    private readonly ILogger<EntityService<T>> _logger;

    public EntityService(
        IDbOrm dbOrm,
        IYzhAuditLogger auditLogger,
        IUserContext userContext,
        ILogger<EntityService<T>> logger)
    {
        _dbOrm = dbOrm;
        _auditLogger = auditLogger;
        _userContext = userContext;
        _logger = logger;
    }

    // ==================== 查询操作（返回 Result<T>） ====================

    /// <summary>根据 Code 获取实体</summary>
    public virtual async Task<Result<T?>> GetByCode(string code)
    {
        try
        {
            var predicate = BuildStringEqualsExpression("Code", code);
            if (predicate == null)
                return Result<T?>.Fail("实体不包含 Code 属性");
            var result = await _dbOrm.GetOneAsync<T>(predicate);
            return result.Success 
                ? Result<T?>.Ok(result.Data)
                : Result<T?>.Fail(result.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetByCode 错误，Type={Type}, Code={Code}", typeof(T).Name, code);
            return Result<T?>.Fail($"根据 Code 获取实体失败：{ex.Message}");
        }
    }

    /// <summary>
    ///     根据 Code 获取实体（不过滤 IsValid 和 IsDeleted）
    ///     用途：toggle-valid 等需要操作无效记录的场景
    ///     安全加固：使用参数化查询替代字符串拼接，防止 SQL 注入
    /// </summary>
    public virtual async Task<Result<T?>> GetByCodeAny(string code)
    {
        try
        {
            // 使用安全的参数化查询（通过 ORM 获取真实表名，避免拼接注入）
            var tableName = GetQueryTableName<T>();
            var sql = $"SELECT * FROM `{tableName}` WHERE Code = @code LIMIT 1";
            var result = await _dbOrm.QueryFirstOrDefaultAsync<T>(sql, new { code });
            return result.Success
                ? Result<T?>.Ok(result.Data)
                : Result<T?>.Fail($"记录 {code} 不存在");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetByCodeAny 错误，Type={Type}, Code={Code}", typeof(T).Name, code);
            return Result<T?>.Fail($"根据 Code 获取实体失败：{ex.Message}");
        }
    }

    /// <summary>根据条件获取单条</summary>
    public virtual async Task<Result<T?>> GetOne(Expression<Func<T, bool>> predicate)
    {
        try
        {
            var result = await _dbOrm.GetOneAsync<T>(predicate);
            return result.Success 
                ? Result<T?>.Ok(result.Data)
                : Result<T?>.Fail(result.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetOne 错误，Type={Type}", typeof(T).Name);
            return Result<T?>.Fail($"获取单条记录失败：{ex.Message}");
        }
    }

    /// <summary>获取列表</summary>
    public virtual async Task<Result<List<T>>> GetListAsync(Expression<Func<T, bool>>? predicate = null, bool includeDisabled = false)
    {
        try
        {
            var result = await _dbOrm.GetListAsync<T>(predicate, includeDisabled);
            return result.Success 
                ? Result<List<T>>.Ok(result.Data!)
                : Result<List<T>>.Fail(result.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetList 错误，Type={Type}", typeof(T).Name);
            return Result<List<T>>.Fail($"获取列表失败：{ex.Message}");
        }
    }

    /// <summary>分页查询（使用 FilterOperation 安全解析 FilterItem → SqlCondition）</summary>
    public virtual async Task<Result<PagedResult<T>>> GetPageAsync(PagerOptions options)
    {
        try
        {
            // 使用 FilterOperation 安全解析 FilterItem → SqlCondition
            // 自动处理操作符映射（eq→=, like→LIKE）、值处理（like 添加 % 通配符）、字段名安全校验
            var conditions = FilterOperation.ParseList(options.Filters);

            // 视图路由：若实体标记了 [ViewName]，查询走视图（含关联扩展字段如 OrgName/RoleName）
            // 增删改仍走物理表（SqlSugar 自动忽略 [NotMapped] 字段）
            var tableName = GetQueryTableName<T>();

            var sqlOptions = new SqlPageOptions
            {
                TableName = tableName,
                PageNumber = options.Page,
                PageSize = options.PageSize,
                SortField = options.SortBy,
                SortDirection = options.SortDirection ?? "ASC",
                Conditions = conditions.ToArray()
            };
            
            var result = await _dbOrm.GetPageAsync<T>(sqlOptions);
            if (!result.Success) return Result<PagedResult<T>>.Fail(result.Error);
            
            var data = result.Data!;
            return Result<PagedResult<T>>.Ok(new PagedResult<T>(data.items, data.total, options.Page, options.PageSize));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "GetPage 参数错误，Type={Type}", typeof(T).Name);
            return Result<PagedResult<T>>.Fail($"查询参数错误：{ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetPage 错误，Type={Type}", typeof(T).Name);
            return Result<PagedResult<T>>.Fail($"分页查询失败：{ex.Message}");
        }
    }

    /// <summary>统计数量</summary>
    public virtual async Task<Result<int>> CountAsync(Expression<Func<T, bool>>? predicate = null)
    {
        try
        {
            var result = await _dbOrm.CountAsync<T>(predicate);
            return result.Success 
                ? Result<int>.Ok(result.Data)
                : Result<int>.Fail(result.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Count 错误，Type={Type}", typeof(T).Name);
            return Result<int>.Fail($"统计数量失败：{ex.Message}");
        }
    }

    /// <summary>判断是否存在</summary>
    public virtual async Task<Result<bool>> ExistsAsync(Expression<Func<T, bool>> predicate)
    {
        try
        {
            var result = await _dbOrm.ExistsAsync<T>(predicate);
            return result.Success 
                ? Result<bool>.Ok(result.Data)
                : Result<bool>.Fail(result.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exists 错误，Type={Type}", typeof(T).Name);
            return Result<bool>.Fail($"存在性检查失败：{ex.Message}");
        }
    }

    /// <summary>根据 Code 判断是否存在</summary>
    public virtual async Task<Result<bool>> ExistsByCodeAsync(string code)
    {
        try
        {
            var predicate = BuildStringEqualsExpression("Code", code);
            if (predicate == null)
                return Result<bool>.Fail("实体不包含 Code 属性");
            var result = await _dbOrm.ExistsAsync<T>(predicate);
            return result.Success 
                ? Result<bool>.Ok(result.Data)
                : Result<bool>.Fail(result.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ExistsByCode 错误，Type={Type}, Code={Code}", typeof(T).Name, code);
            return Result<bool>.Fail($"Code 存在性检查失败：{ex.Message}");
        }
    }

    // ==================== 视图查询（树结构专用） ====================

    /// <summary>
    ///     从视图获取树节点列表（扁平，只返回直接子级）
    ///     用于 TreeControllerBase 的 GetTree/GetChildren
    /// </summary>
    /// <param name="parentCode">父节点编码（null 或空返回根节点）</param>
    /// <returns>直接子级列表</returns>
    public virtual async Task<List<T>> GetViewList(string? parentCode)
    {
        try
        {
            List<T> result;
            if (string.IsNullOrEmpty(parentCode))
            {
                // 返回根节点（parent_code IS NULL）
                var nullPredicate = BuildStringNullExpression("ParentCode");
                if (nullPredicate != null)
                {
                    var queryResult = await _dbOrm.GetListAsync<T>(nullPredicate);
                    result = queryResult.Data ?? new List<T>();
                }
                else
                {
                    result = new List<T>();
                }
            }
            else
            {
                // 返回指定父节点的直接子级
                var equalityPredicate = BuildStringEqualsExpression("ParentCode", parentCode);
                if (equalityPredicate != null)
                {
                    var queryResult = await _dbOrm.GetListAsync<T>(equalityPredicate);
                    result = queryResult.Data ?? new List<T>();
                }
                else
                {
                    result = new List<T>();
                }
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetViewList 错误，Type={Type}, ParentCode={ParentCode}", typeof(T).Name, parentCode);
            return new List<T>();
        }
    }

    /// <summary>
    ///     批量获取子节点数量（一次 SQL GROUP BY）
    ///     用于 FillIsLeafBatch，杜绝 N+1 查询
    /// </summary>
    /// <param name="parentCodes">父节点编码列表</param>
    /// <returns>Dictionary&lt;parentCode, childrenCount&gt;</returns>
    public virtual async Task<Dictionary<string, int>> GetChildrenCountBatch(List<string> parentCodes)
    {
        var result = new Dictionary<string, int>();
        
        if (parentCodes == null || parentCodes.Count == 0)
            return result;

        try
        {
            var viewName = GetViewName<T>();
            
            // 获取 ParentCode 属性的实际 DB 列名（从 SugarColumn 特性）
            var parentCodeColumnName = GetParentCodeColumnName();

            // IN 子句需要手动展开（SqlSugar 参数化 IN 与 MySQL 兼容问题）
            var inParams = string.Join("','", parentCodes.Select(c => c.Replace("'", "''")));
            var sql = $"SELECT `{parentCodeColumnName}` AS ParentCode, COUNT(*) AS Cnt " +
                      $"FROM `{viewName}` " +
                      $"WHERE `{parentCodeColumnName}` IN ('{inParams}') " +
                      $"GROUP BY `{parentCodeColumnName}`";

            var queryResult = await _dbOrm.SqlQueryAsync<ChildrenCountRow>(sql);
            
            if (queryResult.Success && queryResult.Data != null)
            {
                foreach (var item in queryResult.Data)
                {
                    result[item.ParentCode] = item.Cnt;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetChildrenCountBatch 错误，Type={Type}", typeof(T).Name);
        }

        return result;
    }

    /// <summary>
    ///     获取 ParentCode 属性的数据库列名
    /// </summary>
    private static string GetParentCodeColumnName()
    {
        var prop = typeof(T).GetProperty("ParentCode",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);
        
        if (prop == null)
            return "ParentCode"; // 默认
        
        // 优先使用 SqlSugar 的 SugarColumn 特性
        var sugarColumn = prop.GetCustomAttributes(typeof(SqlSugar.SugarColumn), false)
            .Cast<SqlSugar.SugarColumn>()
            .FirstOrDefault();
        if (sugarColumn?.ColumnName != null)
            return sugarColumn.ColumnName;
        
        // 其次使用 EF Core 的 Column 特性
        var columnAttr = prop.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.Schema.ColumnAttribute), false)
            .Cast<System.ComponentModel.DataAnnotations.Schema.ColumnAttribute>()
            .FirstOrDefault();
        if (columnAttr?.Name != null)
            return columnAttr.Name;
        
        // 默认使用属性名
        return prop.Name;
    }

    /// <summary>
    ///     获取查询表名/视图名（视图路由）
    ///     优先级：[ViewName] 特性 > [SugarTable] 特性 > [Table] 特性 > 类型名
    ///     用法：GetPageAsync / GetListAsync 等查询操作使用视图；Insert/Update/Delete 走物理表
    /// </summary>
    private static string GetQueryTableName<TEntity>()
    {
        var type = typeof(TEntity);
        // 1. 优先 [ViewName]（视图路由）
        var viewAttr = type.GetCustomAttributes(typeof(ViewNameAttribute), inherit: true)
            .Cast<ViewNameAttribute>()
            .FirstOrDefault();
        if (viewAttr != null) return viewAttr.ViewName;

        // 2. [SugarTable] 特性（YZH.Core 新架构标准映射，SqlSugar）
        //    修复：早期实现遗漏了本步，导致仅标 [SugarTable] 的实体在 GetByCodeAny
        //    等原生 SQL 路径下回退为类型名（如 CertificationBody），报“表不存在”。
        var sugarAttr = type.GetCustomAttributes(typeof(SqlSugar.SugarTable), inherit: true)
            .Cast<SqlSugar.SugarTable>()
            .FirstOrDefault();
        if (sugarAttr != null && !string.IsNullOrEmpty(sugarAttr.TableName))
            return sugarAttr.TableName;

        // 3. 回退 [Table] 特性（EF Core 标准表名映射，历史实体）
        var tableAttr = type.GetCustomAttributes(typeof(TableAttribute), inherit: true)
            .Cast<TableAttribute>()
            .FirstOrDefault();
        return tableAttr?.Name ?? type.Name;
    }

    /// <summary>
    ///     获取视图名称（从 ViewNameAttribute 或类型名）
    ///     ⚠️ 已废弃，统一使用 GetQueryTableName
    /// </summary>
    [Obsolete("使用 GetQueryTableName 替代")]
    private static string GetViewName<TEntity>()
    {
        return GetQueryTableName<TEntity>();
    }

    // ==================== 写入操作（返回 Result<T>） ====================

    /// <summary>
    ///     新增实体（自动填充 Code、审计字段）
    /// </summary>
    public virtual async Task<Result<T>> Insert(T entity, string? clientIp = null)
    {
        try
        {
            // 自动生成 Code（如果实体有 Code 属性且为空）
            var codeProp = typeof(T).GetProperty("Code",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (codeProp != null && codeProp.PropertyType == typeof(string))
            {
                var currentCode = codeProp.GetValue(entity) as string;
                if (string.IsNullOrEmpty(currentCode))
                {
                    codeProp.SetValue(entity, Guid.NewGuid().ToString("N"));
                }
            }

            FillCreateAudit(entity);
            var result = await _dbOrm.InsertAsync(entity);

            var ctx = _userContext.GetRequestContext();
            var (id, code) = GetEntityIdentifiers(result.Data);
            _auditLogger.Info($"新增{typeof(T).Name}记录", _userContext.UserCode, _userContext.UserName,
                clientIp ?? ctx.ClientIp, $"Id={id}, Code={code}");

            return result;
        }
        catch (Exception ex)
        {
            var ctx = _userContext.GetRequestContext();
            _auditLogger.Error($"新增{typeof(T).Name}", ex, _userContext.UserCode, _userContext.UserName,
                clientIp ?? ctx.ClientIp);
            _logger.LogError(ex, "Insert 错误，Type={Type}", typeof(T).Name);
            return Result<T>.Fail(FormatError(ex, "新增"));
        }
    }

    /// <summary>
    ///     修改实体（自动填充 UpdateTime/UpdateBy 审计字段）
    /// </summary>
    public virtual async Task<Result<T>> Update(T entity, string? clientIp = null, string[]? updateFields = null)
    {
        try
        {
            FillUpdateAudit(entity);
            var result = updateFields != null
                ? await _dbOrm.UpdateAsync(entity, updateFields)
                : await _dbOrm.UpdateAsync(entity);

            var ctx = _userContext.GetRequestContext();
            var (id, code) = GetEntityIdentifiers(result.Data);
            _auditLogger.Info($"更新{typeof(T).Name}记录", _userContext.UserCode, _userContext.UserName,
                clientIp ?? ctx.ClientIp, $"Id={id}, Code={code}");

            return result;
        }
        catch (Exception ex)
        {
            var ctx = _userContext.GetRequestContext();
            _auditLogger.Error($"更新{typeof(T).Name}", ex, _userContext.UserCode, _userContext.UserName,
                clientIp ?? ctx.ClientIp);
            _logger.LogError(ex, "Update 错误，Type={Type}", typeof(T).Name);
            return Result<T>.Fail(FormatError(ex, "修改"));
        }
    }

    /// <summary>根据 Code 删除</summary>
    public virtual async Task<Result<bool>> DeleteByCode(string code, string? clientIp = null)
    {
        try
        {
            var predicate = BuildStringEqualsExpression("Code", code);
            if (predicate == null)
                return Result<bool>.Fail("实体不包含 Code 属性");
            var entityResult = await _dbOrm.GetOneAsync<T>(predicate);
            if (!entityResult.Success || entityResult.Data == null)
                return Result<bool>.Fail("记录不存在或已被删除");

            var entity = entityResult.Data!;
            
            if (DeleteStrategyHelper.IsHardDelete<T>())
            {
                var result = await _dbOrm.DeleteByCodeAsync<T>(code);
                return result;
            }
            else
            {
                FillDeleteAudit(entity);
                await SoftDelete(entity);
                return Result<bool>.Ok(true);
            }
        }
        catch (Exception ex)
        {
            var ctx = _userContext.GetRequestContext();
            _auditLogger.Error($"删除{typeof(T).Name}", ex, _userContext.UserCode, _userContext.UserName,
                clientIp ?? ctx.ClientIp);
            _logger.LogError(ex, "DeleteByCode 错误，Type={Type}, Code={Code}", typeof(T).Name, code);
            return Result<bool>.Fail(FormatError(ex, "删除"));
        }
    }

    // ==================== 批量操作（事务保护） ====================

    /// <summary>批量新增（事务保护）</summary>
    public virtual async Task<Result<List<T>>> InsertBatch(IEnumerable<T> entities, string? clientIp = null)
    {
        try
        {
            var list = entities.ToList();

            // 预缓存 Code 属性反射信息（避免循环中重复反射）
            var codeProp = typeof(T).GetProperty("Code",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            foreach (var entity in list)
            {
                // 自动生成 Code（如果实体有 Code 属性且为空）
                if (codeProp != null && codeProp.PropertyType == typeof(string))
                {
                    var currentCode = codeProp.GetValue(entity) as string;
                    if (string.IsNullOrEmpty(currentCode))
                    {
                        codeProp.SetValue(entity, Guid.NewGuid().ToString("N"));
                    }
                }
                FillCreateAudit(entity);
            }

            using var tx = _dbOrm.BeginTransaction();
            try
            {
                foreach (var entity in list)
                    await _dbOrm.InsertAsync(entity);
                tx.Commit();
            }
            catch
            {
                try { tx.Rollback(); } catch { /* 忽略回滚失败 */ }
                throw;
            }

            var ctx = _userContext.GetRequestContext();
            _auditLogger.Info($"批量新增{typeof(T).Name}", _userContext.UserCode, _userContext.UserName,
                clientIp ?? ctx.ClientIp, $"数量={list.Count}");

            return Result<List<T>>.Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "InsertBatch 错误，Type={Type}", typeof(T).Name);
            return Result<List<T>>.Fail(FormatError(ex, "批量新增"));
        }
    }

    /// <summary>
    ///     批量按 Code 删除（软删除/硬删除由实体特性决定）
    /// </summary>
    public virtual async Task<Result<int>> DeleteBatch(IEnumerable<string> codes, bool hardDelete = false, string? clientIp = null)
    {
        try
        {
            var codeList = codes.ToList();
            using var tx = _dbOrm.BeginTransaction();
            try
            {
                int count = 0;
                if (hardDelete)
                {
                    // 硬删除：直接使用 Code 批量删除
                    var deleteResult = await _dbOrm.DeleteByCodeBatchAsync<T>(codeList);
                    if (deleteResult.Success)
                        count = deleteResult.Data;
                }
                else
                {
                    // 软删除：先查询再更新
                    foreach (var code in codeList)
                    {
                        var predicate = BuildStringEqualsExpression("Code", code);
                        if (predicate == null) continue;
                        var entityResult = await _dbOrm.GetOneAsync<T>(predicate);
                        if (entityResult.Success && entityResult.Data != null)
                        {
                            var entity = entityResult.Data;
                            FillDeleteAudit(entity);
                            await SoftDelete(entity);
                            count++;
                        }
                    }
                }

                tx.Commit();

                var ctx = _userContext.GetRequestContext();
                _auditLogger.Info($"批量删除{typeof(T).Name}", _userContext.UserCode, _userContext.UserName,
                    clientIp ?? ctx.ClientIp, $"数量={count}");

                return Result<int>.Ok(count);
            }
            catch
            {
                try { tx.Rollback(); } catch { /* 忽略回滚失败 */ }
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeleteBatch 错误，Type={Type}", typeof(T).Name);
            return Result<int>.Fail(FormatError(ex, "批量删除"));
        }
    }

    // ==================== 审计字段填充 ====================

    private void FillCreateAudit(T entity)
    {
        dynamic dyn = entity;
        try { dyn.CreateTime = DateTime.UtcNow; } catch { /* 实体可能没有该属性 */ }
        try { dyn.CreateBy = _userContext.UserCode; } catch { /* 实体可能没有该属性 */ }
    }

    private void FillUpdateAudit(T entity)
    {
        dynamic dyn = entity;
        try { dyn.UpdateTime = DateTime.UtcNow; } catch { /* 实体可能没有该属性 */ }
        try { dyn.UpdateBy = _userContext.UserCode; } catch { /* 实体可能没有该属性 */ }
    }

    private void FillDeleteAudit(T entity)
    {
        dynamic dyn = entity;
        try { dyn.DeleteTime = DateTime.UtcNow; } catch { /* 实体可能没有该属性 */ }
        try { dyn.DeleteBy = _userContext.UserCode; } catch { /* 实体可能没有该属性 */ }
    }

    private async Task SoftDelete(T entity)
    {
        if (entity is ISoftDelete softDelete)
        {
            // 通过接口统一设置三个软删除字段
            softDelete.IsDeleted = true;
            softDelete.DeleteBy = _userContext.UserCode;
            softDelete.DeleteTime = DateTime.UtcNow;
            await _dbOrm.UpdateAsync(entity, new[] { "IsDeleted", "DeleteTime", "DeleteBy" });
        }
        else
        {
            // 向后兼容：未实现 ISoftDelete 但有 IsDeleted 字段的实体
            dynamic dyn = entity;
            try { dyn.IsDeleted = true; } catch { /* 无 IsDeleted 则跳过 */ }
            await _dbOrm.UpdateAsync(entity, new[] { "IsDeleted" });
        }
    }

    // ==================== 表达式构建辅助方法 ====================

    /// <summary>
    ///     构建字符串属性相等表达式（使用反射，兼容无 Code 属性的实体）
    /// </summary>
    private static Expression<Func<T, bool>>? BuildStringEqualsExpression(string propertyName, string value)
    {
        var property = typeof(T).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (property == null || property.PropertyType != typeof(string))
            return null;

        var param = Expression.Parameter(typeof(T), "e");
        var propertyAccess = Expression.Property(param, property);
        var constant = Expression.Constant(value);
        var equality = Expression.Equal(propertyAccess, constant);
        return Expression.Lambda<Func<T, bool>>(equality, param);
    }

    /// <summary>
    ///     构建字符串属性为 null 的表达式
    /// </summary>
    private static Expression<Func<T, bool>>? BuildStringNullExpression(string propertyName)
    {
        var property = typeof(T).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (property == null || property.PropertyType != typeof(string))
            return null;

        var param = Expression.Parameter(typeof(T), "e");
        var propertyAccess = Expression.Property(param, property);
        var nullConstant = Expression.Constant(null, typeof(string));
        var equality = Expression.Equal(propertyAccess, nullConstant);
        return Expression.Lambda<Func<T, bool>>(equality, param);
    }

    // ==================== 错误格式化 ====================

    /// <summary>
    ///     将异常转换为用户友好的错误信息
    /// </summary>
    private static string FormatError(Exception ex, string operationName)
    {
        var msg = ex.Message;

        if (msg.Contains("Duplicate") || msg.Contains("UNIQUE") || msg.Contains("duplicate"))
            return $"{operationName}失败：数据已存在（唯一约束冲突）";

        if (msg.Contains("foreign key") || msg.Contains("FOREIGN KEY"))
            return $"{operationName}失败：数据被其他业务引用，无法操作";

        if (msg.Contains("Cannot insert the value NULL") || msg.Contains("cannot be null"))
            return $"{operationName}失败：必填字段为空";

        return $"{operationName}失败：{msg}";
    }

    // ==================== 辅助方法 ====================

    /// <summary>
    ///     获取实体的 Id 和 Code 标识符（使用反射替代 dynamic）
    /// </summary>
    private static (object? id, string? code) GetEntityIdentifiers(T? entity)
    {
        if (entity == default || entity == null)
            return (null, null);

        object? id = default;
        string? code = default;

        var idProp = ResolveProperty(typeof(T), "Id");
        if (idProp != null)
            id = idProp.GetValue(entity);

        var codeProp = ResolveProperty(typeof(T), "Code");
        if (codeProp != null)
            code = codeProp.GetValue(entity) as string;

        return (id, code);
    }

    /// <summary>
    ///     按名字取属性，优先最派生的声明
    ///
    ///     背景：多个实体用 <c>new</c> 隐藏了 BaseEntity 的同名列且类型不同
    ///     （典型：<c>public new string Id</c> 遮蔽基类 <c>long Id</c>）。
    ///     此时 <c>Type.GetProperty(name, Public|Instance)</c> 会抛
    ///     <c>AmbiguousMatchException</c>（“Ambiguous match found for … Id”）。
    ///     而该异常发生在 Insert 成功之后、写审计日志之前，会被 catch 转成
    ///     <c>Result.Fail</c> —— 数据实际已落库，接口却返回失败（Updated=0），
    ///     前端表现为“保存成功但没保存”。
    ///
    ///     这里逐层向上查找，命中即返回（= 最派生的那份声明）。
    /// </summary>
    private static System.Reflection.PropertyInfo? ResolveProperty(Type type, string name)
    {
        for (var t = type; t != null && t != typeof(object); t = t.BaseType)
        {
            var prop = t.GetProperty(name,
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.DeclaredOnly);
            if (prop != null)
                return prop;
        }
        return null;
    }
}

/// <summary>
///     子节点计数行（用于 SqlQueryAsync 强类型映射）
/// </summary>
public class ChildrenCountRow
{
    public string ParentCode { get; set; } = string.Empty;
    public int Cnt { get; set; }
}

// ==================== 树形扩展方法 ====================

/// <summary>
///     实体服务的树形扩展方法
///     提供 GetRootNodes / GetChildren / GetChildrenCount 等树操作
/// </summary>
public static class TreeEntityExtensions
{
    /// <summary>获取根节点列表（ParentCode 为 null 或 rootParentCode 的节点）</summary>
    public static async Task<List<T>> GetRootNodes<T>(this EntityService<T> service, string? rootParentCode = null)
        where T : class, ITreeEntity, new()
    {
        return await service.GetViewList(rootParentCode ?? null);
    }

    /// <summary>获取指定父节点的直接子级</summary>
    public static async Task<List<T>> GetChildren<T>(this EntityService<T> service, string parentCode)
        where T : class, ITreeEntity, new()
    {
        return await service.GetViewList(parentCode);
    }

    /// <summary>获取指定父节点的子节点数量</summary>
    public static async Task<int> GetChildrenCount<T>(this EntityService<T> service, string parentCode)
        where T : class, ITreeEntity, new()
    {
        var counts = await service.GetChildrenCountBatch(new List<string> { parentCode });
        return counts.GetValueOrDefault(parentCode, 0);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using YZH.Core.DataBase;
using YZH.Core.Stand.Models;

namespace YZH.Core.Api.Services;

/// <summary>
///     事务工作单元 — 通用回调模式
/// 
/// 核心能力：
/// 1. 支持多实体类型混合操作（如同时更新 User + UserRole + Menu）
/// 2. 回调内所有 saveChanges:false 的操作统一提交
/// 3. 嵌套事务支持（已有事务时共享上层事务）
/// 4. 统一异常处理和日志记录
/// 5. 返回 IOperationResult（操作是否成功由基类判断）
/// 
/// 使用方式对标参考架构的 _orm.UseTransaction(db => {...}, out txErr):
///   var result = workUnit.Execute(() => {
///       userService.Insert(user, userId, userName, saveChanges: false);
///       roleService.Update(roleId, userId, userName, saveChanges: false);
///       auditService.Log("UserCreated", user.Code);
///   });
///   if (!result.Success) return BadRequest(result.Message);
/// </summary>
public class WorkUnit : IDisposable
{
    private readonly BaseDbContext _context;
    private IDbContextTransaction? _transaction;
    private bool _nesting;
    private bool _completed;

    public WorkUnit(BaseDbContext context)
    {
        _context = context;
        _nesting = context.Database.CurrentTransaction != null;
    }

    /// <summary>
    ///     在事务中执行操作（回调模式）
    ///     回调内所有 saveChanges:false 的操作在退出时统一提交
    /// 
    ///     如果当前已有事务（嵌套调用），则直接执行回调（共享上层事务）
    /// </summary>
    public IOperationResult Execute(Action action)
    {
        if (_nesting)
        {
            try
            {
                action();
                return OperationResult.Ok();
            }
            catch (Exception ex)
            {
                return OperationResult.Fail($"事务操作失败：{ex.Message}", ex);
            }
        }

        using var transaction = _context.Database.BeginTransaction();
        try
        {
            action();
            _context.SaveChanges();
            transaction.Commit();
            _completed = true;
            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            try { transaction.Rollback(); }
            catch (Exception rollbackEx)
            {
                return OperationResult.Fail($"事务回滚失败：{rollbackEx.Message}", rollbackEx);
            }
            return OperationResult.Fail($"事务操作失败：{ex.Message}", ex);
        }
    }

    /// <summary>在事务中执行操作（带返回值）</summary>
    public IOperationResult<TResult> Execute<TResult>(Func<TResult> func)
    {
        if (_nesting)
        {
            try
            {
                var result = func();
                return OperationResult<TResult>.Ok(result);
            }
            catch (Exception ex)
            {
                return OperationResult<TResult>.Fail($"事务操作失败：{ex.Message}", ex);
            }
        }

        using var transaction = _context.Database.BeginTransaction();
        try
        {
            var result = func();
            _context.SaveChanges();
            transaction.Commit();
            _completed = true;
            return OperationResult<TResult>.Ok(result);
        }
        catch (Exception ex)
        {
            try { transaction.Rollback(); }
            catch (Exception rollbackEx)
            {
                return OperationResult<TResult>.Fail($"事务回滚失败：{rollbackEx.Message}", rollbackEx);
            }
            return OperationResult<TResult>.Fail($"事务操作失败：{ex.Message}", ex);
        }
    }

    /// <summary>异步事务回调</summary>
    public async Task<IOperationResult> ExecuteAsync(Func<Task> action)
    {
        if (_nesting)
        {
            try
            {
                await action();
                return OperationResult.Ok();
            }
            catch (Exception ex)
            {
                return OperationResult.Fail($"事务操作失败：{ex.Message}", ex);
            }
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            await action();
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            _completed = true;
            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            try { await transaction.RollbackAsync(); }
            catch { /* 回滚失败不掩盖原始异常 */ }
            return OperationResult.Fail($"事务操作失败：{ex.Message}", ex);
        }
    }

    /// <summary>异步事务回调（带返回值）</summary>
    public async Task<IOperationResult<TResult>> ExecuteAsync<TResult>(Func<Task<TResult>> func)
    {
        if (_nesting)
        {
            try
            {
                var result = await func();
                return OperationResult<TResult>.Ok(result);
            }
            catch (Exception ex)
            {
                return OperationResult<TResult>.Fail($"事务操作失败：{ex.Message}", ex);
            }
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var result = await func();
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            _completed = true;
            return OperationResult<TResult>.Ok(result);
        }
        catch (Exception ex)
        {
            try { await transaction.RollbackAsync(); }
            catch { /* 回滚失败不掩盖原始异常 */ }
            return OperationResult<TResult>.Fail($"事务操作失败：{ex.Message}", ex);
        }
    }

    public void Dispose()
    {
        if (!_nesting && !_completed && _transaction != null)
        {
            try { _transaction.Rollback(); } catch { /* 忽略 */ }
        }
        _transaction?.Dispose();
    }
}

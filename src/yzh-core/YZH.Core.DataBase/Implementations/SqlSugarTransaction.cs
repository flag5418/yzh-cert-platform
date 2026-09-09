using SqlSugar;
using YZH.Core.DataBase.Interfaces;

namespace YZH.Core.DataBase.Implementations;

/// <summary>
///     SqlSugar 事务实现
/// </summary>
public class SqlSugarTransaction : IDbTransaction
{
    private readonly IAdo _ado;
    private bool _disposed;

    public SqlSugarTransaction(IAdo ado)
    {
        _ado = ado;
    }

    public void Commit()
    {
        _ado.CommitTran();
    }

    public void Rollback()
    {
        _ado.RollbackTran();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            try { _ado.RollbackTran(); } catch { /* 忽略 */ }
            _disposed = true;
        }
    }
}

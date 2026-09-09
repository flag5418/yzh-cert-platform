namespace YZH.Core.DataBase.Interfaces;

/// <summary>
///     数据库事务接口
/// </summary>
public interface IDbTransaction : IDisposable
{
    void Commit();
    void Rollback();
}

using YZH.Core.DataBase.Interfaces;

namespace YZH.Core.DataBase.MultiDatabase;

/// <summary>
///     数据库上下文工厂接口
///     支持通过别名获取不同的数据库访问实例，实现跨数据库操作
///     不传别名 → 返回默认数据库
/// </summary>
public interface IDbContextFactory
{
    /// <summary>
    ///     获取默认数据库上下文（来自配置 Default 节点）
    /// </summary>
    IDbOrm GetDefault();

    /// <summary>
    ///     根据别名获取指定数据库上下文
    ///     如 "CAD2006" 返回 Oracle 数据库访问实例
    /// </summary>
    /// <param name="alias">数据库别名（对应 Connections 的 Key）</param>
    /// <returns>数据库访问实例</returns>
    IDbOrm GetByAlias(string alias);
}

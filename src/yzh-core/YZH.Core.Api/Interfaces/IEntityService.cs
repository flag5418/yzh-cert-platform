using System.Linq.Expressions;
using YZH.Core.Stand.Models;
using YZH.Core.Stand.Models.Request;
using YZH.Core.Stand.Models.Result;

namespace YZH.Core.Api.Interfaces;

/// <summary>
///     实体服务接口 - 提供原子能力层
///     所有实体操作服务实现此接口
/// </summary>
public interface IEntityService<T> where T : class, new()
{
    /// <summary>新增</summary>
    Task<Result<T>> Insert(T entity, string? clientIp = null);

    /// <summary>更新</summary>
    Task<Result<T>> Update(T entity, string? clientIp = null);

    /// <summary>按 Code 删除</summary>
    Task<Result<bool>> DeleteByCode(string code, string? clientIp = null);

    /// <summary>按 Code 批量删除</summary>
    Task<Result<int>> DeleteByCodeBatch(IEnumerable<string> codes, string? clientIp = null);

    /// <summary>按 Code 获取单条</summary>
    Task<Result<T?>> GetByCode(string code);

    /// <summary>分页查询</summary>
    Task<Result<PagedResult<T>>> GetPage(FilterRequest request);

    /// <summary>获取列表</summary>
    Task<Result<List<T>>> GetList(Expression<Func<T, bool>>? predicate = null);
}

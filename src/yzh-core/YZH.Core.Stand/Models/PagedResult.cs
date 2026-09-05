namespace YZH.Core.Stand.Models;

/// <summary>分页结果</summary>
public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int PageIndex,
    int PageSize
)
{
    public int TotalPages => (TotalCount + PageSize - 1) / PageSize;
    public bool HasPrevious => PageIndex > 1;
    public bool HasNext => PageIndex < TotalPages;
}

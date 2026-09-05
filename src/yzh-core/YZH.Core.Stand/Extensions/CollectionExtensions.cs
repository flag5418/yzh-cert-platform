namespace YZH.Core.Stand.Extensions;

/// <summary>集合扩展工具</summary>
public static class CollectionExtensions
{
    /// <summary>IsNullOrEmpty - 空判断</summary>
    public static bool IsNullOrEmpty<T>(this ICollection<T>? collection) =>
        collection == null || collection.Count == 0;

    /// <summary>IsNotNullOrEmpty - 非空判断</summary>
    public static bool IsNotNullOrEmpty<T>(this ICollection<T>? collection) =>
        collection != null && collection.Count > 0;

    /// <summary>AddIfNotContains - 不存在则添加</summary>
    public static void AddIfNotContains<T>(this ICollection<T> collection, T item)
    {
        if (!collection.Contains(item)) collection.Add(item);
    }

    /// <summary>RemoveAll - 按条件移除并返回移除的数量</summary>
    public static int RemoveAll<T>(this ICollection<T> collection, Func<T, bool> predicate)
    {
        var itemsToRemove = collection.Where(predicate).ToList();
        foreach (var item in itemsToRemove) collection.Remove(item);
        return itemsToRemove.Count;
    }

    /// <summary>Page - 内存分页</summary>
    public static IEnumerable<T> Page<T>(this IEnumerable<T> source, int pageIndex, int pageSize)
    {
        return source.Skip((pageIndex - 1) * pageSize).Take(pageSize);
    }

    /// <summary>ToPagedResult - 分页结果包装</summary>
    public static PagedResult<T> ToPagedResult<T>(this IEnumerable<T> source, int pageIndex, int pageSize)
    {
        var list = source.ToList();
        return new PagedResult<T>(
            list.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToList(),
            list.Count,
            pageIndex,
            pageSize
        );
    }

    /// <summary>ForEach - 遍历执行</summary>
    public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
    {
        foreach (var item in source) action(item);
    }

    /// <summary>DistinctBy - 按指定字段去重</summary>
    public static IEnumerable<T> DistinctBy<T, TKey>(this IEnumerable<T> source, Func<T, TKey> keySelector)
    {
        var seen = new HashSet<TKey>();
        foreach (var item in source)
        {
            if (seen.Add(keySelector(item))) yield return item;
        }
    }

    /// <summary>Shuffle - 随机打乱</summary>
    public static List<T> Shuffle<T>(this IEnumerable<T> source)
    {
        var list = source.ToList();
        var random = Random.Shared;
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        return list;
    }
}

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

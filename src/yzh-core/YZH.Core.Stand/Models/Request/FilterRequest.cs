namespace YZH.Core.Stand.Models.Request;

/// <summary>前端过滤请求（后端自动拼 SQL）</summary>
public class FilterRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SortField { get; set; }
    public string? SortOrder { get; set; }

    /// <summary>
    /// 过滤条件集合
    /// 后端自动将每个 FilterItem 解析为 SQL 条件
    /// 多个条件之间为 AND 关系
    /// </summary>
    public List<FilterItem> Filters { get; set; } = new();

    /// <summary>
    /// 是否显示已禁用/已删除的记录（默认 false，仅显示启用的记录）
    /// 前端通过"显示已禁用"开关控制此参数
    /// </summary>
    public bool ShowDisabled { get; set; } = false;
}

/// <summary>导出请求</summary>
public class ExportRequest
{
    /// <summary>过滤条件（同 FilterRequest.Filters）</summary>
    public List<FilterItem> Filters { get; set; } = new();

    /// <summary>导出格式：excel/csv</summary>
    public string Format { get; set; } = "excel";

    /// <summary>导出字段（为空表示导出全部）</summary>
    public string[]? Fields { get; set; }
}

/// <summary>导入结果</summary>
public class ImportResult
{
    /// <summary>总行数</summary>
    public int TotalRows { get; set; }

    /// <summary>成功行数</summary>
    public int SuccessRows { get; set; }

    /// <summary>失败行数</summary>
    public int FailedRows { get; set; }

    /// <summary>错误详情</summary>
    public List<ImportError> Errors { get; set; } = new();
}

/// <summary>导入错误</summary>
public class ImportError
{
    public int RowIndex { get; set; }
    public string Field { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

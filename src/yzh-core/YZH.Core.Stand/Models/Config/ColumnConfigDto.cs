using YZH.Core.Stand.Models.Config;

namespace YZH.Core.Stand.Models.Config;

/// <summary>
///     前端 DTO：列配置（PascalCase 命名，与数据库列名、实体属性名一致）
///     规则：实体属性是什么，JSON 就是什么，数据库列就是什么
/// </summary>
public class ColumnConfigDto
{
    public string FieldName { get; set; } = string.Empty;
    public string DesName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool XsFlag { get; set; }
    public bool BcFlag { get; set; }
    public bool Yxk { get; set; }
    public bool Enable { get; set; } = true;
    public bool Sortable { get; set; }
    public int? Width { get; set; }
    public string? Fixed { get; set; }
    public string? Align { get; set; }
    public string? DictCode { get; set; }
    public string? Format { get; set; }
    public int Row { get; set; }
    public int Col { get; set; }
    public int RowSpan { get; set; } = 1;
    public int ColSpan { get; set; } = 1;
    public object? Mrz { get; set; }
    /// <summary>分组索引（编辑模式控制）："0"=默认可编辑，"1"+=特定模式只读，"99"=详情全部只读</summary>
    public string? GroupIndex { get; set; }
    /// <summary>是否掩码显示（敏感字段如 key/secret/password）</summary>
    public bool Mask { get; set; }
}

/// <summary>
///     前端 DTO：下拉选项
/// </summary>
public class SelectOptionDto
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

using YZH.Core.Stand.Models.Config;

namespace YZH.Core.Stand.Models.Config;

/// <summary>
///     前端 DTO：实体配置（PascalCase 命名，与数据库列名、实体属性名一致）
/// </summary>
public class EntityConfigDto
{
    public string Title { get; set; } = string.Empty;
    public string? FillMode { get; set; }
    /// <summary>表单布局列数（1=单列，2=双列，0=自动：BcFlag字段≤10用1列，>10用2列）</summary>
    public int FormCols { get; set; } = 0;
    public List<ColumnConfigDto> Columns { get; set; } = new();
    public Dictionary<string, object>? NewEntity { get; set; }
    public Dictionary<string, EntityFieldSchema>? Schema { get; set; }

    // 兼容旧字段（来自 JSON 文件）
    public string? ConfigName { get; set; }
    public string? TableName { get; set; }

    // 运行时可注入
    public ToolbarConfigDto? Toolbar { get; set; }
    public RowButtonConfigDto? RowButtons { get; set; }
    public List<SearchFieldDto>? SearchFields { get; set; }

    /// <summary>启用/禁用字段名（默认 IsValid，前端根据此字段显示启用/禁用按钮）</summary>
    public string EnableField { get; set; } = "IsValid";
}

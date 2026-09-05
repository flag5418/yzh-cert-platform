namespace YZH.Core.Stand.Models;

/// <summary>
///     表格/表单的UI配置模型
///     对标老YZH架构的 GridConfig
///     XML/JSON 文件中的配置将反序列化为该模型
/// </summary>
public class GridConfig
{
    /// <summary>配置文件名</summary>
    public string ConfigName { get; set; } = string.Empty;

    /// <summary>对应的数据库表名</summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>表格标题</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>是否有合计行</summary>
    public bool FloorFlag { get; set; }

    /// <summary>列填充方式</summary>
    public FillMode FillMode { get; set; } = FillMode.AutoFix;

    /// <summary>所有列/字段定义</summary>
    public List<DefineColumn> Columns { get; set; } = new();

    /// <summary>工具栏按钮配置</summary>
    public ToolbarConfig Toolbar { get; set; } = new();

    /// <summary>分页配置</summary>
    public PaginationConfig Pagination { get; set; } = new();

    /// <summary>API端点前缀</summary>
    public string ApiEndpoint { get; set; } = string.Empty;

    /// <summary>布局列数（表单模式）</summary>
    public int LayoutColumns { get; set; } = 1;

    /// <summary>表单分组定义</summary>
    public List<FormGroup>? FormGroups { get; set; }
}

public class ToolbarConfig
{
    public bool Add { get; set; } = true;
    public bool Edit { get; set; } = true;
    public bool Delete { get; set; } = true;
    public bool Export { get; set; } = true;
    public bool Import { get; set; } = false;
}

public class PaginationConfig
{
    public int PageSize { get; set; } = 20;
    public int[] PageSizes { get; set; } = { 10, 20, 50, 100 };
}

public class FormGroup
{
    public string Name { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public List<DefineColumn> Fields { get; set; } = new();
}

public enum FillMode
{
    /// <summary>按比例填充</summary>
    AutoFix = 0,
    /// <summary>按像素填充</summary>
    PixFix = 1
}

namespace YZH.Core.Stand.Models.Config;

/// <summary>
///     工具栏按钮配置
/// </summary>
public class ToolbarConfig
{
    public bool Add { get; set; } = true;
    public bool Delete { get; set; } = true;
    public bool Export { get; set; } = false;
    public bool Import { get; set; } = false;
    public Dictionary<string, string>? CustomButtons { get; set; }
}

/// <summary>
///     行按钮配置
/// </summary>
public class RowButtonConfig
{
    public bool Edit { get; set; } = true;
    public bool Delete { get; set; } = true;
    public bool Enable { get; set; } = false;
    public Dictionary<string, string>? CustomButtons { get; set; }
}

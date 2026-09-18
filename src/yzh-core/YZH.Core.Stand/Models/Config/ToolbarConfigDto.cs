namespace YZH.Core.Stand.Models.Config;

/// <summary>
///     前端 DTO：工具栏配置
/// </summary>
public class ToolbarConfigDto
{
    public bool? Add { get; set; }
    public bool? Delete { get; set; }
    public bool? Export { get; set; }
    public bool? Import { get; set; }
    public Dictionary<string, string>? CustomButtons { get; set; }
}

/// <summary>
///     前端 DTO：行按钮配置
/// </summary>
public class RowButtonConfigDto
{
    public bool? Edit { get; set; }
    public bool? Delete { get; set; }
    public Dictionary<string, string>? CustomButtons { get; set; }
}

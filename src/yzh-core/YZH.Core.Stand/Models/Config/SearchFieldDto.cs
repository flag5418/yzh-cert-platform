namespace YZH.Core.Stand.Models.Config;

/// <summary>
///     前端 DTO：搜索字段配置
/// </summary>
public class SearchFieldDto
{
    public string Label { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    public string Operator { get; set; } = "like";
    public string ControlType { get; set; } = "input";
    public List<SelectOptionDto>? Options { get; set; }
    public int Width { get; set; } = 180;
}

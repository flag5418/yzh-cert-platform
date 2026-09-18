namespace YZH.Core.Stand.Models.Config;

/// <summary>搜索条件配置</summary>
public class SearchFieldConfig
{
    /// <summary>显示名称</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>字段名（对应实体属性）</summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>查询类型：like/eq/gt/lt/in</summary>
    public string Operator { get; set; } = "like";

    /// <summary>控件类型：input/select/date/cascader</summary>
    public string ControlType { get; set; } = "input";

    /// <summary>下拉选项（ControlType=select时有效）</summary>
    public List<SelectOption>? Options { get; set; }

    /// <summary>宽度（px）</summary>
    public int Width { get; set; } = 180;
}

using YZH.Core.Stand.Enums;

namespace YZH.Core.Stand.Models;

/// <summary>
///     GridConfig 的列/字段定义
///     对标老YZH架构的 DefineColumn，定义单个字段的UI行为
/// </summary>
public class DefineColumn
{
    /// <summary>行号（Grid布局）</summary>
    public int Row { get; set; }

    /// <summary>跨行数</summary>
    public int RowSpan { get; set; } = 1;

    /// <summary>列号（Grid布局）</summary>
    public int Col { get; set; }

    /// <summary>跨列数</summary>
    public int ColSpan { get; set; } = 1;

    /// <summary>选择配置（下拉/弹选参数）</summary>
    public string? ChooseStr { get; set; }

    /// <summary>分组索引（工作流阶段控制）</summary>
    public string GroupIndex { get; set; } = "0";

    /// <summary>合计标志</summary>
    public bool SumFlag { get; set; }

    /// <summary>允许空（必填校验）</summary>
    public bool YXK { get; set; }

    /// <summary>保存标志（是否持久化）</summary>
    public bool BCFlag { get; set; } = true;

    /// <summary>显示标志</summary>
    public bool XSFlag { get; set; } = true;

    /// <summary>默认值</summary>
    public string? MRZ { get; set; }

    /// <summary>控件类型</summary>
    public ControlType Type { get; set; } = ControlType.TextBox;

    /// <summary>宽度</summary>
    public decimal Width { get; set; } = 120;

    /// <summary>字段名（对应实体属性）</summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>显示标题</summary>
    public string DesName { get; set; } = string.Empty;

    /// <summary>显示顺序</summary>
    public int SXH { get; set; }

    // === 以下为Web端扩展属性 ===
    
    /// <summary>是否可排序</summary>
    public bool Sortable { get; set; }

    /// <summary>列固定方式：left/right/null</summary>
    public string? Fixed { get; set; }

    /// <summary>下拉选项数据源</summary>
    public List<SelectOption>? Options { get; set; }

    /// <summary>格式化字符串（日期/数字）</summary>
    public string? Format { get; set; }
}

public class SelectOption
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

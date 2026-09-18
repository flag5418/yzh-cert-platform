namespace YZH.Core.Stand.Models.Config;

/// <summary>
///     表格列填充模式
///     AutoFix：所有列等比例填满整表（适用于字段较少的页面）
///     PixFix：每列按 width 像素渲染，超出表格宽度时横向滚动（适用于字段较多的页面）
/// </summary>
public enum FillMode
{
    /// <summary>等比例填满整表（默认）</summary>
    AutoFix = 0,

    /// <summary>按像素宽度渲染（允许横向滚动）</summary>
    PixFix = 1
}

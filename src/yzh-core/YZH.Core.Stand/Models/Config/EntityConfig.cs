namespace YZH.Core.Stand.Models.Config;

/// <summary>
///     字段结构描述（后端反射生成，前端表单用）
/// </summary>
public class EntityFieldSchema
{
    /// <summary>字段类型：string/number/boolean/datetime</summary>
    public string Type { get; set; } = "string";

    /// <summary>默认值（直接用于初始化表单）</summary>
    public object? Default { get; set; }

    /// <summary>是否可选（允许为空）</summary>
    public bool Optional { get; set; }
}

/// <summary>
///     实体页面 UI 配置（一份配置同时驱动表格和表单）
///     
///     配置文件约定：
///     - 文件名 = 类名（如 Sys_User.json）
///     - 类上的 [Table] 特性 = 数据库表名
///     
///     字段控制逻辑：
///     - sxh：表格列排序号（升序排列）
///     - row/col/rowSpan/colSpan：表单 Grid 布局位置
///     - xsFlag：是否在表格中显示
///     - bcFlag：是否在表单中显示和编辑（false=隐藏）
///     - type：控件类型；Other=只读/纯展示（不在表单中渲染为可编辑控件）
///     - yxk：是否允许空（表单校验）
///     - mrz：默认值
///     
///     根级别精简为：title + fillMode + columns
/// </summary>
public class EntityConfig
{
    /// <summary>页面标题</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>表格列填充模式（AutoFix=等比例填满，PixFix=按像素宽度，适合字段多）</summary>
    public FillMode FillMode { get; set; } = FillMode.AutoFix;

    /// <summary>列/字段定义集合（同时驱动表格和表单）</summary>
    public List<DefineColumn> Columns { get; set; } = new();

    /// <summary>
    ///     空实体模板（camelCase 字段名 → 默认值）
    ///     来源：后端反射实体自动生成（EntitySchemaHelper.GetEmptyEntity）
    ///     用途：前端直接用这个对象初始化表单数据，不再手写 TS interface
    ///     
    ///     示例：
    ///     {
    ///       "code": "",
    ///       "userName": "",
    ///       "roleId": 0,
    ///       "enable": 1,
    ///       "gender": null,
    ///       "phoneNo": null,
    ///       "orgCode": null
    ///     }
    /// </summary>
    public Dictionary<string, object>? NewEntity { get; set; }

    /// <summary>
    ///     字段结构描述（camelCase 字段名 → 类型+默认值+可选）
    ///     来源：后端反射实体自动生成（EntitySchemaHelper.GetSchema）
    ///     用途：前端表单校验、控件类型推断
    /// </summary>
    public Dictionary<string, EntityFieldSchema>? Schema { get; set; }
}

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

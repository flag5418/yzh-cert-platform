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

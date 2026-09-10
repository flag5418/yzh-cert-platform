using System;

namespace YZH.Core.Api.Attributes;

/// <summary>
/// 接口说明特性
/// 用于标注 Controller 方法的接口信息
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class ApiDescriptionAttribute : Attribute
{
    /// <summary>接口说明</summary>
    public string Description { get; set; } = "";
    
    /// <summary>负责人</summary>
    public string Author { get; set; } = "";
    
    /// <summary>创建时间（自动填充）</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>更新时间（自动填充）</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="description">接口说明</param>
    /// <param name="author">负责人</param>
    public ApiDescriptionAttribute(string description = "", string author = "")
    {
        Description = description;
        Author = author;
    }
}

/// <summary>
/// 参数说明特性
/// 用于标注方法参数的说明信息
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
public class ParamDescriptionAttribute : Attribute
{
    /// <summary>参数说明</summary>
    public string Description { get; set; } = "";
    
    /// <summary>是否必填</summary>
    public bool Required { get; set; } = true;
    
    /// <summary>默认值</summary>
    public object? Default { get; set; } = null;
    
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="description">参数说明</param>
    /// <param name="required">是否必填</param>
    /// <param name="defaultValue">默认值</param>
    public ParamDescriptionAttribute(string description = "", bool required = true, object? defaultValue = null)
    {
        Description = description;
        Required = required;
        Default = defaultValue;
    }
}

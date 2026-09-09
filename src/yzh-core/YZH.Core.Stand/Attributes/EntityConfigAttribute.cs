namespace YZH.Core.Stand.Annotations;

/// <summary>
///     指定实体的 EntityConfig 配置文件名
///     不指定时使用实体类名 + .json
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class EntityConfigAttribute : Attribute
{
    public string ConfigFileName { get; }
    
    public EntityConfigAttribute(string configFileName)
    {
        ConfigFileName = configFileName;
    }
}

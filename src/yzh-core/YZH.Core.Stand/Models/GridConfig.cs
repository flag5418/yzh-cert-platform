namespace YZH.Core.Stand.Models;

/// <summary>
///     表格/表单的UI配置模型
///     对标老YZH架构的 GridConfig
///     XML/JSON 文件中的配置将反序列化为该模型
/// </summary>
public class GridConfig
{
    /// <summary>配置文件名</summary>
    public string ConfigName { get; set; } = string.Empty;

    /// <summary>对应的数据库表名</summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>主键字段名</summary>
    public string PrimaryKey { get; set; } = "Id";

    /// <summary>表格标题</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>所有列/字段定义</summary>
    public List<DefineColumn> Columns { get; set; } = new();

    /// <summary>工具栏按钮配置</summary>
    public ToolbarConfig Toolbar { get; set; } = new();

    /// <summary>行操作按钮配置</summary>
    public RowButtonConfig RowButtons { get; set; } = new();

    /// <summary>API端点前缀</summary>
    public string ApiEndpoint { get; set; } = string.Empty;

    /// <summary>布局列数（表单模式）</summary>
    public int LayoutColumns { get; set; } = 1;

    /// <summary>表单分组定义</summary>
    public List<FormGroup>? FormGroups { get; set; }

    /// <summary>
    ///     从 JSON 配置文件加载 GridConfig
    ///     配置文件路径：Assets/GridConfigs/{TypeName}.json
    ///     如果配置文件不存在，返回默认配置
    /// </summary>
    public static GridConfig GetGridConfig<T>() where T : BaseEntity
    {
        return GetGridConfig<T>(null);
    }

    /// <summary>
    ///     从 JSON 配置文件加载 GridConfig（支持自定义配置名）
    ///     配置文件路径：Assets/GridConfigs/{configName}.json
    ///     如果配置文件不存在，返回默认配置
    /// </summary>
    public static GridConfig GetGridConfig<T>(string? configName) where T : BaseEntity
    {
        var name = configName ?? typeof(T).Name;
        var configPath = Path.Combine("Assets", "GridConfigs", $"{name}.json");

        try
        {
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                return System.Text.Json.JsonSerializer.Deserialize<GridConfig>(json)
                    ?? new GridConfig { ConfigName = name, Title = name };
            }
        }
        catch
        {
            // 配置文件读取失败时返回默认配置
        }

        return new GridConfig
        {
            ConfigName = name,
            TableName = name,
            Title = name,
            Columns = new List<DefineColumn>()
        };
    }
}

public class ToolbarConfig
{
    public bool Add { get; set; } = true;
    public bool Edit { get; set; } = true;
    public bool Delete { get; set; } = true;
    public bool Export { get; set; } = true;
    public bool Import { get; set; } = false;
    /// <summary>自定义工具栏按钮</summary>
    public List<CustomButton>? CustomButtons { get; set; }
}

/// <summary>
///     自定义按钮定义
/// </summary>
public class CustomButton
{
    /// <summary>按钮键名</summary>
    public string Key { get; set; } = string.Empty;
    /// <summary>按钮显示文本</summary>
    public string Text { get; set; } = string.Empty;
    /// <summary>按钮图标</summary>
    public string? Icon { get; set; }
    /// <summary>按钮类型：primary/success/warning/danger</summary>
    public string Type { get; set; } = "primary";
    /// <summary>调用后端接口</summary>
    public string? Api { get; set; }
}

/// <summary>
///     行操作按钮配置
///     Key: 按钮显示文本
///     Value: 前端应调用的接口方法名（前端通过此字典知道按钮→接口的映射关系）
///     
///     前端约定：点击按钮时，统一传递当前行记录到对应接口
///     如：{ "审核通过": "approve", "退回": "reject" }
/// </summary>
public class RowButtonConfig
{
    /// <summary>内置按钮开关</summary>
    public bool Edit { get; set; } = true;
    public bool Delete { get; set; } = true;
    public bool View { get; set; } = true;

    /// <summary>
    ///     自定义行按钮字典
    ///     Key: 按钮显示文本（如"审核通过"）
    ///     Value: 接口方法名（Controller中对应的方法，如"Approve"）
    /// </summary>
    public Dictionary<string, string>? CustomButtons { get; set; }
}

public class FormGroup
{
    public string Name { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public List<DefineColumn> Fields { get; set; } = new();
}

public enum FillMode
{
    /// <summary>按比例填充</summary>
    AutoFix = 0,
    /// <summary>按像素填充</summary>
    PixFix = 1
}

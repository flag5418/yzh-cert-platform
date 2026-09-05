using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using YZH.Core.Stand.Enums;
using YZH.Core.Stand.Models;

namespace YZH.Core.Api;

[ApiController]
[Route("api/gridconfig")]
public class GridConfigController : ControllerBase
{
    private readonly IMemoryCache _cache;
    private readonly string _configsPath;

    public GridConfigController(IMemoryCache cache, IWebHostEnvironment env)
    {
        _cache = cache;
        _configsPath = System.IO.Path.Combine(env.ContentRootPath, "GridConfigs");
    }

    [HttpGet("{tableName}")]
    public IActionResult Get(string tableName)
    {
        var cacheKey = $"gridconfig_{tableName}";

        if (_cache.TryGetValue(cacheKey, out GridConfig? cached) && cached != null)
            return Ok(ApiResponse<GridConfig>.Ok(cached));

        var jsonPath = System.IO.Path.Combine(_configsPath, $"{tableName}.json");
        if (System.IO.File.Exists(jsonPath))
        {
            var json = System.IO.File.ReadAllText(jsonPath);
            var config = System.Text.Json.JsonSerializer.Deserialize<GridConfig>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (config != null)
            {
                _cache.Set(cacheKey, config, TimeSpan.FromHours(24));
                return Ok(ApiResponse<GridConfig>.Ok(config));
            }
        }

        return Ok(ApiResponse<GridConfig>.Fail($"未找到 {tableName} 的配置", 404));
    }
}

public static class GridConfigLoader
{
    public static GridConfig FromXml(string xmlPath)
    {
        var doc = System.Xml.Linq.XDocument.Load(xmlPath);
        var table = doc.Root;
        var config = new GridConfig
        {
            ConfigName = System.IO.Path.GetFileName(xmlPath),
            TableName = table?.Attribute("TableName")?.Value ?? string.Empty,
            FloorFlag = table?.Attribute("FloorFlag")?.Value == "True",
            FillMode = table?.Attribute("FillMode")?.Value == "1" ? FillMode.PixFix : FillMode.AutoFix
        };
        if (table == null) return config;

        foreach (var col in table.Elements("Column"))
        {
            var column = new DefineColumn
            {
                Row = int.Parse(col.Attribute("Row")?.Value ?? "0"),
                RowSpan = int.Parse(col.Attribute("RowSpan")?.Value ?? "1"),
                Col = int.Parse(col.Attribute("Col")?.Value ?? "0"),
                ColSpan = int.Parse(col.Attribute("ColSpan")?.Value ?? "1"),
                FieldName = col.Attribute("FieldName")?.Value ?? string.Empty,
                DesName = col.Attribute("DesName")?.Value ?? string.Empty,
                Width = decimal.Parse(col.Attribute("Width")?.Value ?? "120"),
                YXK = col.Attribute("YXK")?.Value == "1",
                BCFlag = col.Attribute("BCFlag")?.Value == "1",
                XSFlag = col.Attribute("XSFlag")?.Value == "1",
                GroupIndex = col.Attribute("GroupIndex")?.Value ?? "0",
                SXH = int.Parse(col.Attribute("SXH")?.Value ?? "0"),
                MRZ = col.Attribute("MRZ")?.Value ?? string.Empty
            };
            column.Type = col.Attribute("Type")?.Value switch
            {
                "文本" => ControlType.TextBox,
                "日期" => ControlType.DatePicker,
                "数字" => ControlType.Decimal,
                "选择" => ControlType.ButtonEdit,
                "密码" => ControlType.PasswordBox,
                "单选" => ControlType.CheckBox,
                "下拉" => ControlType.ComboBox,
                "备注" => ControlType.Memo,
                "只读" => ControlType.Label,
                "其他" => ControlType.Other,
                "按钮" => ControlType.Button,
                _ => ControlType.TextBox
            };
            config.Columns.Add(column);
        }
        return config;
    }
}

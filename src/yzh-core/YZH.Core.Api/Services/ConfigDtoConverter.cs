using System.Linq;
using YZH.Core.Stand.Models.Config;

namespace YZH.Core.Api.Services;

/// <summary>
///     配置 DTO 转换工具：EntityConfig → EntityConfigDto（PascalCase → camelCase）
///     所有 Controller 共用此转换，确保前后端字段命名一致。
///
///     ★ 2026-09-26 修复：本转换此前只复制 `DefineColumn` 的**字段子集**，
///     `Sxh` / `Options` / `Placeholder` 三个**已在 JSON 中声明**的属性被静默丢弃
///     （属 G18「写了就是死配置」）。现已补全 —— **新增 `DefineColumn` 属性时必须同步此处**，
///     否则 JSON 里的配置永远不会到达前端，且**没有任何报错**。
/// </summary>
public static class ConfigDtoConverter
{
    public static EntityConfigDto ToDto(EntityConfig config)
    {
        return new EntityConfigDto
        {
            Title = config.Title,
            FillMode = config.FillMode.ToString(),
            FormCols = config.FormCols,
            Columns = config.Columns?.Select(c => new ColumnConfigDto
            {
                FieldName = c.FieldName,
                DesName = c.DesName,
                Sxh = c.Sxh,
                Type = c.Type.ToString(),
                XsFlag = c.XsFlag,
                BcFlag = c.BcFlag,
                Yxk = c.Yxk,
                Enable = c.Enable,
                Sortable = c.Sortable,
                Width = c.Width > 0 ? (int)c.Width : null,
                Fixed = c.Fixed,
                Align = c.Align,
                DictCode = c.DictCode,
                Format = c.Format,
                Row = c.Row,
                Col = c.Col,
                RowSpan = c.RowSpan,
                ColSpan = c.ColSpan,
                Mrz = c.Mrz,
                GroupIndex = c.GroupIndex,
                Mask = c.Mask,
                Placeholder = c.Placeholder,
                Options = c.Options?.Select(o => new SelectOptionDto
                {
                    Label = o.Label,
                    Value = o.Value,
                }).ToList(),
            }).ToList() ?? new(),
            NewEntity = config.NewEntity,
            Schema = config.Schema,
            EnableField = config.EnableField,
            Toolbar = config.Toolbar,
            RowButtons = config.RowButtons,
            SearchFields = config.SearchFields?.Select(s => new SearchFieldDto
            {
                Label = s.Label,
                Field = s.Field,
                Operator = s.Operator,
                ControlType = s.ControlType,
                Width = s.Width,
                Options = s.Options?.Select(o => new SelectOptionDto { Label = o.Label, Value = o.Value }).ToList(),
            }).ToList(),
        };
    }
}

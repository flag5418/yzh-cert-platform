using YZH.Core.Stand.Models.Config;

namespace YZH.Core.Stand.Models.Config;

/// <summary>
///     前端 DTO：列配置（PascalCase 命名，与数据库列名、实体属性名一致）
///     规则：实体属性是什么，JSON 就是什么，数据库列就是什么
/// </summary>
public class ColumnConfigDto
{
    public string FieldName { get; set; } = string.Empty;
    public string DesName { get; set; } = string.Empty;
    /// <summary>
    ///     ★ 显示顺序（表格列顺序 / 表单字段顺序，升序）。2026-09-26 新增 ——
    ///     此前 `DefineColumn.Sxh` **未被 `ConfigDtoConverter` 复制**，属 G18「静默丢弃」。
    ///     ⚠️ 前端目前**只传输不消费**（未按 Sxh 排序）：部分配置的 Sxh 与数组顺序不一致
    ///     （如 `User.json` 为 1,2,3,5,0,4,6…），直接启用排序会把 `Sxh:0` 的列顶到最前。
    ///     启用前须先统一各 JSON 的 Sxh 取值。
    /// </summary>
    public int Sxh { get; set; }
    public string Type { get; set; } = string.Empty;
    public bool XsFlag { get; set; }
    public bool BcFlag { get; set; }
    public bool Yxk { get; set; }
    public bool Enable { get; set; } = true;
    public bool Sortable { get; set; }
    public int? Width { get; set; }
    public string? Fixed { get; set; }
    public string? Align { get; set; }
    public string? DictCode { get; set; }
    public string? Format { get; set; }
    public int Row { get; set; }
    public int Col { get; set; }
    public int RowSpan { get; set; } = 1;
    public int ColSpan { get; set; } = 1;
    public object? Mrz { get; set; }
    /// <summary>分组索引（编辑模式控制）："0"=默认可编辑，"1"+=特定模式只读，"99"=详情全部只读</summary>
    public string? GroupIndex { get; set; }
    /// <summary>是否掩码显示（敏感字段如 key/secret/password）</summary>
    public bool Mask { get; set; }

    /// <summary>
    ///     ★ 表单占位提示（可选）。为空时前端回落到「请输入{DesName}」/「请选择{DesName}」。
    ///     2026-09-26 新增 —— 此前 JSON 里写 `Placeholder` 会被**静默丢弃**（DTO 无此属性），
    ///     属于「写了就是死配置」。
    /// </summary>
    public string? Placeholder { get; set; }

    /// <summary>
    ///     ★ 枚举/下拉选项（可选）。双用途：
    ///     · 表单 → `select` / `radio` / `checkbox` 的选项来源（前端 `toFormFields`）
    ///     · 表格 → 映射为 `tagMap`，把原始值渲染成中文标签（前端 `toTableColumns`）
    ///     2026-09-26 新增 —— 此前下拉选项**无法声明式配置**，只能逐页前端注入。
    /// </summary>
    public List<SelectOptionDto>? Options { get; set; }
}

/// <summary>
///     前端 DTO：下拉选项
/// </summary>
public class SelectOptionDto
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

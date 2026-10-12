using System;
using System.Collections.Generic;
using CertPlatform.Shared.Office;
using CertPlatform.Shared.Workflow.Models;

namespace CertPlatform.Shared.Fill;

/// <summary>
/// <b>表格区域装配</b> —— 把 <see cref="TablePayload"/> 投影成 <see cref="OfficeFillRegion"/>
/// （列映射层），★ <b>⛔ 不落盘</b>。
///
/// <para><b>为什么单独抽出来</b>（2026-10-12 下沉）：专家端的
/// <c>DocumentFillOrchestrator</c> 要直接调这段逻辑，而它原来长在
/// <c>Admin.Services.Workflow.Skills.Fill.FillTableSkill</c> 里（后台端工作流 Skill）
/// ⇒ 违反三方结构铁律。现把<b>纯逻辑</b>放 Shared，后台端 Skill 只剩一层
/// <c>[Skill]</c> 壳（参数绑定 + <c>SkillResult</c> 包装）。</para>
///
/// <para><b>★ 对外统一签名，对内按 <see cref="FillSession.FileKind"/> 分派</b>（39 号 §11.3）：
/// Excel ⇒ <see cref="OfficeRegionKind.ExcelRange"/>；Word ⇒ <see cref="OfficeRegionKind.WordTable"/>。
/// ⇒ 同一份数据在两种文件下走不同分支，<b>规则配置无需知道文件类型</b>。</para>
///
/// <para><b>★ 行数对齐由层 1 做，⛔ 不在此实现</b>（39 号 §11.4）：
/// 数据行 &gt; 模板行 ⇒ 克隆最后一行；数据行 &lt; 模板行 ⇒ 多余行留空（⛔ 不删除）；列多 ⇒ 丢弃。</para>
///
/// <para><b>★ 列顺序 = 写入列序</b>：由 <see cref="TablePayload.Columns"/> 决定，
/// ⛔ 不靠行内字典的键序（字典无序 ⇒ 结果不可复现）。</para>
/// </summary>
public static class TableRegionBuilder
{
    /// <summary>本构建器的错误分类码（与 <see cref="WorkflowErrorCodes"/> 对齐）</summary>
    public const string CodePreconditionEmpty = WorkflowErrorCodes.PreconditionEmpty;
    public const string CodeConfigError = WorkflowErrorCodes.ConfigError;

    /// <summary>
    /// 列映射 → 按文件类型分派 → 追加区域指令。
    /// </summary>
    /// <param name="session">★ 填充会话（空 ⇒ 新建一个并返回在 <paramref name="session"/> 上）</param>
    /// <param name="table">表格数据</param>
    /// <param name="startRow">Excel 起始行（0-based）。&lt;0 时落 0</param>
    /// <param name="startCol">Excel 起始列（0-based）。&lt;0 时落 0</param>
    /// <param name="sheetName">Excel 工作表名（空 ⇒ 第 1 个工作表）</param>
    /// <returns>成功与否 / 失败原因 / 失败分类码 / 装配的行数</returns>
    public static (bool Ok, string? Error, string? ErrorCode, int RowCount) Append(
        FillSession session, TablePayload? table,
        int startRow = -1, int startCol = -1, string? sheetName = null)
    {
        if (table == null || table.Columns.Count == 0)
            return (false, "table.columns 不能为空", CodePreconditionEmpty, 0);
        if (table.Rows.Count == 0)
            return (false, "table.rows 不能为空", CodePreconditionEmpty, 0);

        // ① 列映射：按 Columns 的顺序，把每行的字典投影成 List<FillValue?>
        var matrix = new List<List<FillValue?>>(table.Rows.Count);
        foreach (var row in table.Rows)
        {
            var line = new List<FillValue?>(table.Columns.Count);
            foreach (var col in table.Columns)
            {
                // ★ 缺键 / 空值 ⇒ null（= 写空，⛔ 不报错、⛔ 不左移 —— 左移会让整行错位且不报错）
                if (!row.TryGetValue(col.FieldCode, out var raw) || string.IsNullOrWhiteSpace(raw))
                {
                    line.Add(null);
                    continue;
                }

                var (ok, fv, error) = FillValueFactory.TryCreate(
                    col.FieldCode, raw, col.ValueKind, col.NumberFormat);

                // ★ 类型不符 ⇒ 报错，⛔ 不静默写空（「声明 number 但值不是数字」若静默降级，
                //   Excel 里会变成左对齐的文本，可求和性丢失且打开文件看不出异常）
                if (!ok)
                    return (false, $"表格列 {col.FieldCode} 的值「{raw}」类型不符：{error}", CodeConfigError, 0);

                line.Add(fv);
            }
            matrix.Add(line);
        }

        // ② ★ 按 FileKind 分派定位参数形状（⛔ 不暴露给规则配置）
        var isExcel = string.Equals(session.FileKind, "excel", StringComparison.OrdinalIgnoreCase);

        var region = isExcel
            ? new OfficeFillRegion
            {
                Kind = OfficeRegionKind.ExcelRange,
                SheetName = string.IsNullOrWhiteSpace(sheetName) ? null : sheetName,
                StartRow = startRow < 0 ? 0 : startRow,
                StartCol = startCol < 0 ? 0 : startCol,
                Rows = matrix,
            }
            : new OfficeFillRegion
            {
                Kind = OfficeRegionKind.WordTable,
                TableTag = table.TableTag,
                Rows = matrix,
            };

        session.Request.Regions.Add(region);   // ★ 追加，⛔ 不覆盖已有区域（T5）
        session.Log("fill_table", $"{table.TableTag ?? "(无标签)"} {matrix.Count} rows");

        return (true, null, null, matrix.Count);
    }
}

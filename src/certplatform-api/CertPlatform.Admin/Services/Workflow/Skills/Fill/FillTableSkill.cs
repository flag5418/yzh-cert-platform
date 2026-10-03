using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CertPlatform.Shared.Office;

namespace CertPlatform.Admin.Services.Workflow.Skills.Fill
{
    /// <summary>
    /// 表格填写（操作类）—— 把 <see cref="TablePayload"/> 投影成
    /// <see cref="OfficeFillRegion"/>（**列映射层**），★ <b>⛔ 不落盘</b>。
    ///
    /// <para><b>★ 对外统一签名，对内按 <see cref="FillSession.FileKind"/> 分派</b>（39 号 §11.3）：
    /// Excel ⇒ <see cref="OfficeRegionKind.ExcelRange"/>（<c>SheetName</c>+<c>StartRow</c>+<c>StartCol</c>）；
    /// Word ⇒ <see cref="OfficeRegionKind.WordTable"/>（<c>TableTag</c>）。
    /// ⇒ 同一份 <see cref="TablePayload"/> 在两种文件下走不同分支，<b>规则配置无需知道文件类型</b>
    /// （这正是 38 号 §4.2「泄漏点②」要堵的）。</para>
    ///
    /// <para><b>★ 行数对齐由层 1 做，⛔ 不在此实现</b>（39 号 §11.4）：
    /// 数据行 &gt; 模板行 ⇒ 克隆最后一行；数据行 &lt; 模板行 ⇒ 多余行留空（⛔ 不删除）；列多 ⇒ 丢弃。</para>
    ///
    /// <para><b>★ 列顺序 = 写入列序</b>：由 <see cref="TablePayload.Columns"/> 决定，
    /// ⛔ 不靠行内字典的键序（字典无序 ⇒ 结果不可复现）。</para>
    /// </summary>
    [Skill(
        Code = "fill_table",
        Name = "表格填写",
        ReturnType = "json",
        Description = "把表格数据装配成区域填充指令（Word 表格 / Excel 区域）。★ 不落盘。"
    )]
    public static class FillTableSkill
    {
        /// <summary>本 Skill 编码</summary>
        public const string SkillCode = "fill_table";

        /// <summary>执行 —— 列映射 → 按文件类型分派 → 追加区域指令。</summary>
        /// <param name="session">★ 填充会话（编排器直接放对象实例；空 ⇒ 新建）</param>
        /// <param name="table">表格数据</param>
        /// <param name="start_row">
        /// Excel 起始行（0-based）。
        /// <para>⚠️ <b>&lt;0 时本实现落 0</b>（与 39 号 §11.2 代码一致）。
        /// 「按模板样板行自动探测」<b>尚未实现</b> —— 层 1 不做语义判断，
        /// 调用方（规则/界面）应显式给坐标，否则会从第 1 行开始写、可能盖掉表头。</para>
        /// </param>
        /// <param name="start_col">Excel 起始列（0-based）。&lt;0 时落 0（同上）。</param>
        /// <param name="sheet_name">Excel 工作表名（空 ⇒ 第 1 个工作表）</param>
        /// <param name="ct">取消令牌</param>
        public static Task<SkillResult> ExecuteAsync(
            [SkillParam(Description = "★ 填充会话（编排器直接放对象实例；空=新建）",
                        BindMode = SkillParamBindMode.LinkOrConstant)]
            FillSession? session = null,

            [SkillParam(Description = "表格数据：{table_tag, columns, rows, totals}",
                        BindMode = SkillParamBindMode.LinkOrConstant)]
            TablePayload? table = null,

            [SkillParam(Description = "Excel 起始行（0-based；<0 落 0）")]
            int start_row = -1,

            [SkillParam(Description = "Excel 起始列（0-based；<0 落 0）")]
            int start_col = -1,

            [SkillParam(Description = "Excel 工作表名（空=第 1 个工作表）")]
            string? sheet_name = null,

            CancellationToken ct = default)
        {
            if (table == null || table.Columns.Count == 0)
                return Task.FromResult(SkillResult.Fail("table.columns 不能为空"));
            if (table.Rows.Count == 0)
                return Task.FromResult(SkillResult.Fail("table.rows 不能为空"));

            session ??= new FillSession();

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
                        return Task.FromResult(SkillResult.Fail(
                            $"表格列 {col.FieldCode} 的值「{raw}」类型不符：{error}"));

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
                    SheetName = string.IsNullOrWhiteSpace(sheet_name) ? null : sheet_name,
                    StartRow = start_row < 0 ? 0 : start_row,
                    StartCol = start_col < 0 ? 0 : start_col,
                    Rows = matrix,
                }
                : new OfficeFillRegion
                {
                    Kind = OfficeRegionKind.WordTable,
                    TableTag = table.TableTag,
                    Rows = matrix,
                };

            session.Request.Regions.Add(region);          // ★ 追加，⛔ 不覆盖已有区域（T5）
            session.Log(SkillCode, $"{table.TableTag ?? "(无标签)"} {matrix.Count} rows");

            return Task.FromResult(SkillResult.Ok(new Dictionary<string, object>
            {
                ["session"] = session,
                ["region_count"] = session.Request.Regions.Count,
                ["row_count"] = matrix.Count,
                ["location"] = region.Describe(),
            }));
        }
    }
}

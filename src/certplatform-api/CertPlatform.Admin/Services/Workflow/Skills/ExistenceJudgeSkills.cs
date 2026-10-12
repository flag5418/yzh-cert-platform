using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CertPlatform.Admin.Services.Workflow.Skills;
using CertPlatform.Shared.Workflow.Models;
using CertPlatform.Shared.Workflow.Skills;
using YZH.Core.DataBase.Interfaces;
using CertPlatform.Shared.Domain.Scope;

namespace CertPlatform.Admin.Services.Workflow.Skills
{
    /// <summary>
    /// ★ 空值判定公共逻辑（3 个 Skill 共用）
    ///
    /// <para><b>为什么要单独做</b>（实测发现）：</para>
    /// <list type="bullet">
    /// <item><b>GetFieldSkill</b> 找不到记录时 → <c>SkillResult.Fail("未找到 field_code=...")</c>
    ///       ⇒ ★「字段不存在」会变成<b>节点失败</b>，无法参与逻辑判断</item>
    /// <item><b>NodeExecutor.docfield</b> 找不到记录时 → 抛
    ///       <c>InvalidOperationException</c> ⇒ 同上</item>
    /// <item>即使取到了记录，<c>ExtractedValue</c> 也可能是 <c>""</c>/<c>null</c>/空白</item>
    /// </list>
    /// <para>★ 本类统一给出<b>三态判定</b>：<c>has_value</c> / <c>is_empty</c> / <c>not_found</c>，
    /// 让工作流能明确区分「没有这条数据」与「有数据但是空的」。</para>
    /// </summary>

    // ══════════════════════════════════════════════════════════
    // Skill 1 · is_field_empty —— 判断某字段的值是否为空
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// ★ 判断某个字段的值是否为空
    ///
    /// <para><b>解决的问题</b>：<c>get_field</c> 找不到记录会直接 Fail（实测），
    /// 无法参与逻辑判断。本 Skill 返回<b>三态</b>布尔，永不 Fail。</para>
    ///
    /// <para><b>三态</b>：
    ///   · <c>has_value</c>  —— 有记录且值非空
    ///   · <c>is_empty</c>  —— 有记录但值为空（""/null/空白/"无"/"待填写"等占位符）
    ///   · <c>not_found</c> —— ★ 根本没有这条提取记录（企业未上传，或提取规则未配）</para>
    /// </summary>
    [Skill(
        Code = "is_field_empty",
        Name = "字段是否为空",
        ReturnType = "boolean",
        ResultPort = "result",
        Description = "★ 判断某字段是否有值：返回三态 has_value/is_empty/not_found，永不失败，可直接接 branch/compare"
    )]
    public static class IsFieldEmptySkill
    {
        public static async Task<SkillResult> ExecuteAsync(
            [SkillParam(Description = "字段编码 field_code", BindMode = SkillParamBindMode.ConstantOnly,
                        AllowEmpty = SkillParamEmptyPolicy.Deny)]
            string? field_code,

            [SkillParam(Description = "企业编码（空=虚拟企业样例数据）", BindMode = SkillParamBindMode.ConstantOnly,
                        AllowEmpty = SkillParamEmptyPolicy.Allow)]
            string? enterprise_code,

            [SkillParam(Description = "文件编码（可选，文件级过滤）", BindMode = SkillParamBindMode.ConstantOnly,
                        AllowEmpty = SkillParamEmptyPolicy.Allow)]
            string? file_code,

            [FromService] IDbOrm db = null!,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(field_code))
                return SkillResult.Fail("field_code 不能为空", WorkflowErrorCodes.PreconditionEmpty);

            var entCode = string.IsNullOrWhiteSpace(enterprise_code)
                ? CoreOrgCodes.StdEnv
                : enterprise_code!;

            // ★ 取全部候选（不只取一条）—— 多版本/多文件时要能看出"有几条都空"
            var all = await db.GetListAsync<ExtractionResult>(
                x => x.FieldCode == field_code! && x.EnterpriseCode == entCode, includeDisabled: true);
            var rows = (all.Data ?? new List<ExtractionResult>())
                .Where(r => string.IsNullOrWhiteSpace(file_code) || r.FileCode == file_code)
                .OrderByDescending(r => r.VersionNumber)
                .ThenByDescending(r => r.ExtractedAt)
                .ToList();

            EmptyJudge.Result result;

            if (rows.Count == 0)
            {
                result = new EmptyJudge.Result
                {
                    State = EmptyJudge.StateNotFound,
                    RecordCount = 0,
                    Message = $"★未找到 field_code={field_code} 的提取记录" +
                              $"（企业={entCode}{(string.IsNullOrWhiteSpace(file_code) ? "" : $", 文件={file_code}")}）" +
                              "—— 可能未上传对应文件，或未配置该字段的提取规则"
                };
            }
            else
            {
                // ★ 多行时：任一行有值即算有值
                var hit = rows.FirstOrDefault(r => !EmptyJudge.IsBlankText(r.ExtractedValue));
                if (hit != null)
                {
                    result = new EmptyJudge.Result
                    {
                        State = EmptyJudge.StateHasValue,
                        RawValue = hit.ExtractedValue,
                        Label = hit.FieldName,
                        Confidence = (double?)(hit.Confidence ?? 0m),
                        SourceFileCode = hit.FileCode,
                        SourceVersion = hit.VersionNumber,
                        RecordCount = rows.Count
                    };
                }
                else
                {
                    var first = rows[0];
                    result = new EmptyJudge.Result
                    {
                        State = EmptyJudge.StateIsEmpty,
                        RawValue = first.ExtractedValue,
                        Label = first.FieldName,
                        SourceFileCode = first.FileCode,
                        SourceVersion = first.VersionNumber,
                        RecordCount = rows.Count,
                        Message = $"★{first.FieldName ?? field_code} 共 {rows.Count} 条提取记录，" +
                                  "但值全部为空（可能是文档里未填写该内容）"
                    };
                }
            }

            return SkillResult.Ok(result.ToOutputs(), result.HasValue ? result.Confidence : 0.0);
        }
    }

    // ══════════════════════════════════════════════════════════
    // Skill 2 · is_table_empty —— 判断某个表格的数据是否为空
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// ★ 判断某个表格的数据是否为空
    ///
    /// <para><b>表格"空"的判定</b>（比字段严格）：
    ///   · 记录都取不到            → <c>not_found</c>
    ///   · 取到记录但 JSON 解析后
    ///       数组长度为 0 / 内容为 <c>[]</c> / 对象无数据键 → <c>is_empty</c>
    ///   · 行列数都 &gt; 0           → <c>has_value</c></para>
    /// </summary>
    [Skill(
        Code = "is_table_empty",
        Name = "表格是否为空",
        ReturnType = "boolean",
        ResultPort = "result",
        Description = "★ 判断某表格是否有数据：解析 JSON 后判行数，返回三态 has_value/is_empty/not_found，并回传 row_count/column_count"
    )]
    public static class IsTableEmptySkill
    {
        public static async Task<SkillResult> ExecuteAsync(
            [SkillParam(Description = "表格编码 table_code", BindMode = SkillParamBindMode.ConstantOnly,
                        AllowEmpty = SkillParamEmptyPolicy.Deny)]
            string? table_code,

            [SkillParam(Description = "企业编码（空=虚拟企业样例数据）", BindMode = SkillParamBindMode.ConstantOnly,
                        AllowEmpty = SkillParamEmptyPolicy.Allow)]
            string? enterprise_code,

            [SkillParam(Description = "文件编码（可选，文件级过滤）", BindMode = SkillParamBindMode.ConstantOnly,
                        AllowEmpty = SkillParamEmptyPolicy.Allow)]
            string? file_code,

            [FromService] IDbOrm db = null!,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(table_code))
                return SkillResult.Fail("table_code 不能为空", WorkflowErrorCodes.PreconditionEmpty);

            var entCode = string.IsNullOrWhiteSpace(enterprise_code)
                ? CoreOrgCodes.StdEnv
                : enterprise_code!;

            var all = await db.GetListAsync<TableExtractionResult>(
                x => x.TableCode == table_code! && x.EnterpriseCode == entCode, includeDisabled: true);
            var rows = (all.Data ?? new List<TableExtractionResult>())
                .Where(r => string.IsNullOrWhiteSpace(file_code) || r.FileCode == file_code)
                .OrderByDescending(r => r.VersionNumber)
                .ThenBy(r => r.TableIndex)
                .ToList();

            EmptyJudge.Result result;

            if (rows.Count == 0)
            {
                result = new EmptyJudge.Result
                {
                    State = EmptyJudge.StateNotFound,
                    RecordCount = 0,
                    Message = $"★未找到 table_code={table_code} 的提取记录" +
                              $"（企业={entCode}{(string.IsNullOrWhiteSpace(file_code) ? "" : $", 文件={file_code}")}）" +
                              "—— 可能未上传对应文件，或未配置该表格的提取规则"
                };
            }
            else
            {
                // ★ 逐行解析，任一行有数据即算有值
                var nonEmpty = rows
                    .Select(r => (row: r, parsed: ParseTable(r.ExtractedJson)))
                    .FirstOrDefault(x => x.parsed.RowCount > 0);

                if (nonEmpty.row != null)
                {
                    result = new EmptyJudge.Result
                    {
                        State = EmptyJudge.StateHasValue,
                        RawValue = $"rows={nonEmpty.parsed.RowCount}, cols={nonEmpty.parsed.ColumnCount}",
                        Label = table_code,
                        Confidence = (double?)(nonEmpty.row.Confidence ?? 0m),
                        SourceFileCode = nonEmpty.row.FileCode,
                        SourceVersion = nonEmpty.row.VersionNumber,
                        RecordCount = rows.Count
                    };
                    // ★ 额外回传行列数（供上层做"行数 ≥ N"之类的判定）
                    var outputs = result.ToOutputs();
                    outputs["row_count"]    = nonEmpty.parsed.RowCount;
                    outputs["column_count"] = nonEmpty.parsed.ColumnCount;
                    return SkillResult.Ok(outputs, result.Confidence);
                }

                var first = rows[0];
                result = new EmptyJudge.Result
                {
                    State = EmptyJudge.StateIsEmpty,
                    RawValue = first.ExtractedJson,
                    Label = table_code,
                    SourceFileCode = first.FileCode,
                    SourceVersion = first.VersionNumber,
                    RecordCount = rows.Count,
                    Message = $"★表格 {table_code} 共 {rows.Count} 条提取记录，但行数均为 0（提取到了表头但没有数据行）"
                };
            }

            var outputs2 = result.ToOutputs();
            outputs2["row_count"] = 0;
            return SkillResult.Ok(outputs2, 0.0);
        }

        /// <summary>表格 JSON 解析：容忍多种结构（数组 / {rows:[...]} / {data:[...]}）</summary>
        public static (int RowCount, int ColumnCount) ParseTable(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return (0, 0);
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var arr = root.ValueKind == JsonValueKind.Array ? root : default;
                if (arr.ValueKind != JsonValueKind.Array)
                {
                    foreach (var key in new[] { "rows", "data", "items", "list", "records" })
                    {
                        if (root.ValueKind == JsonValueKind.Object
                            && root.TryGetProperty(key, out var a)
                            && a.ValueKind == JsonValueKind.Array)
                        { arr = a; break; }
                    }
                }

                if (arr.ValueKind != JsonValueKind.Array) return (0, 0);

                // ★ 过滤掉"空壳行"（如全 null / 全空字符串的对象）
                var realRows = arr.EnumerateArray()
                    .Where(IsMeaningfulRow)
                    .ToList();

                if (realRows.Count == 0) return (0, 0);

                // 列数：取第一行的属性数
                var cols = realRows[0].ValueKind == JsonValueKind.Object
                    ? realRows[0].EnumerateObject().Count()
                    : 1;

                return (realRows.Count, cols);
            }
            catch
            {
                return (0, 0);
            }
        }

        private static bool IsMeaningfulRow(JsonElement e)
        {
            switch (e.ValueKind)
            {
                case JsonValueKind.Object:
                    return e.EnumerateObject().Any(p => !EmptyJudge.IsBlankText(
                        p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : p.Value.ToString()));
                case JsonValueKind.Array:
                    return e.EnumerateArray().Any(x => !EmptyJudge.IsBlankText(x.ToString()));
                default:
                    return !EmptyJudge.IsBlankText(e.ToString());
            }
        }
    }
}

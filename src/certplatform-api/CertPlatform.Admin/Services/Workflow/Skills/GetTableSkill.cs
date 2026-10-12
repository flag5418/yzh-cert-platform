
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CertPlatform.Admin.Services.Workflow.Skills;
using CertPlatform.Shared.Workflow.Models;
using YZH.Core.DataBase.Interfaces;
using CertPlatform.Shared.Domain.Scope;
using CertPlatform.Shared.Workflow.Skills;

namespace CertPlatform.Admin.Services.Workflow.Skills
{
    /// <summary>
    /// 获取表格数据：按 table_code + enterprise_code 查询表格提取结果。
    /// <para>移植自：旧 YZH.Builder .../Skills/GetTableSkill.cs</para>
    /// <para>迁移改写：EF VOLContext → IDbOrm.GetListAsync（SqlSugar 强类型查询，与 NodeExecutor docTable 同数据源）</para>
    ///
    /// <para><b>★★ 2026-10-12 两处修正</b>（与 <see cref="GetFieldSkill"/> 完全对称）：</para>
    /// <list type="number">
    /// <item>「取不到」不再 <c>Fail</c>，改为 <b>成功 + 空列表</b>（与 <c>docTable</c> 节点同口径）；
    ///       修复前失败会被 <c>SkillExecutor</c> 吞掉，<c>get_table → is_empty</c> 恒判「非空」。</item>
    /// <item><c>ResultPort = "rows"</c> —— 下游靠 <c>result</c> 拿到<b>行数组</b>，⛔ 不再是整包字典。</item>
    /// </list>
    /// </summary>
    [Skill(
        Code = "get_table",
        Name = "获取表格数据",
        ReturnType = "json",
        ResultPort = "rows",
        Description = "按表格编码和企业编码查询已提取的表格数据"
    )]
    public static class GetTableSkill
    {
        public static async Task<SkillResult> ExecuteAsync(
            [SkillParam(Description = "表格编码",
                        BindMode = SkillParamBindMode.ConstantOnly, AllowEmpty = SkillParamEmptyPolicy.Deny)]
            string table_code,
            [SkillParam(Description = "企业编码（空=标准样例企业=虚拟企业常量）",
                        BindMode = SkillParamBindMode.ConstantOnly, AllowEmpty = SkillParamEmptyPolicy.Allow)]
            string enterprise_code,
            [SkillParam(Description = "文件编码，可选",
                        BindMode = SkillParamBindMode.ConstantOnly, AllowEmpty = SkillParamEmptyPolicy.Allow)]
            string? file_code = null,
            [SkillParam(Description = "表格序号，可选",
                        BindMode = SkillParamBindMode.ConstantOnly, AllowEmpty = SkillParamEmptyPolicy.Allow)]
            int? table_index = null,
            [FromService] IDbOrm db = null!,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(table_code))
                return SkillResult.Fail("table_code 不能为空", WorkflowErrorCodes.PreconditionEmpty);

            // 企业编码兜底（与 NodeExecutor docTable 节点一致）
            var entCode = string.IsNullOrWhiteSpace(enterprise_code) ? CoreOrgCodes.StdEnv : enterprise_code;

            var tables = (await db.GetListAsync<CertPlatform.Shared.Entities.Doc.TableExtractionResult>(x =>
                x.TableCode == table_code && x.EnterpriseCode == entCode)).Data ?? new();

            if (!string.IsNullOrWhiteSpace(file_code))
                tables = tables.Where(x => x.FileCode == file_code).ToList();

            if (table_index.HasValue)
                tables = tables.Where(x => x.TableIndex == table_index.Value).ToList();

            var table = tables.OrderByDescending(x => x.ExtractedAt).FirstOrDefault();
            if (table == null)
            {
                // ★ 与 docTable 同口径：空表 + row_count=0（⛔ 不透出「全空单元格行」，
                //   否则 is_empty 会把 [{"item":"","value":""}] 判成「有值」）
                return SkillResult.Ok(new Dictionary<string, object>
                {
                    ["rows"] = new List<object>(),
                    ["row_count"] = 0,
                    ["extracted_json"] = string.Empty,
                    ["table_code"] = table_code,
                    ["table_index"] = -1,
                    ["has_value"] = false,
                    ["data_state"] = EmptyJudge.StateNotFound,
                    ["message"] = $"★未找到 table_code={table_code}, enterprise_code={entCode} 的提取记录" +
                                  "（可能未上传对应文件，或未配置该表格的提取规则）"
                }, 0.0);
            }

            var confidence = (double?)(table.Confidence ?? 0m);
            return SkillResult.Ok(new Dictionary<string, object>
            {
                ["rows"] = TryParseJson(table.ExtractedJson),
                ["extracted_json"] = table.ExtractedJson,
                ["table_code"] = table.TableCode,
                ["table_index"] = table.TableIndex,
                ["has_value"] = true,
                ["data_state"] = EmptyJudge.StateHasValue,
                ["confidence"] = confidence!
            }, confidence);
        }

        private static object TryParseJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return json;
            try
            {
                using var doc = JsonDocument.Parse(json);
                return JsonElementToObject(doc.RootElement);
            }
            catch (JsonException)
            {
                return json;
            }
        }

        /// <summary>JsonElement → 可序列化普通对象（避免 JsonElement 直接进序列化管道的兼容问题）</summary>
        private static object JsonElementToObject(JsonElement el)
        {
            switch (el.ValueKind)
            {
                case JsonValueKind.Object:
                    var dict = new Dictionary<string, object>();
                    foreach (var p in el.EnumerateObject())
                        dict[p.Name] = JsonElementToObject(p.Value);
                    return dict;
                case JsonValueKind.Array:
                    var list = new List<object>();
                    foreach (var item in el.EnumerateArray())
                        list.Add(JsonElementToObject(item));
                    return list;
                case JsonValueKind.String:
                    return el.GetString() ?? "";
                case JsonValueKind.Number:
                    return el.GetDouble();
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                default:
                    return el.GetRawText();
            }
        }
    }
}

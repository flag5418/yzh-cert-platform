
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CertPlatform.Admin.Services.Workflow.Skills;
using YZH.Core.DataBase.Interfaces;
using CertPlatform.Shared.Entities.Doc;

namespace CertPlatform.Admin.Services.Workflow.Skills
{
    /// <summary>
    /// 获取表格数据：按 table_code + enterprise_code 查询表格提取结果。
    /// <para>移植自：旧 YZH.Builder .../Skills/GetTableSkill.cs</para>
    /// <para>迁移改写：EF VOLContext → IDbOrm.GetListAsync（SqlSugar 强类型查询，与 NodeExecutor docTable 同数据源）</para>
    /// </summary>
    [Skill(
        Code = "get_table",
        Name = "获取表格数据",
        ReturnType = "json",
        Description = "按表格编码和企业编码查询已提取的表格数据"
    )]
    public static class GetTableSkill
    {
        public static async Task<SkillResult> ExecuteAsync(
            [SkillParam(Description = "表格编码")]
            string table_code,
            [SkillParam(Description = "企业编码（空=标准样例企业 YZH-STD-ENT）")]
            string enterprise_code,
            [SkillParam(Description = "文件编码，可选")]
            string? file_code = null,
            [SkillParam(Description = "表格序号，可选")]
            int? table_index = null,
            [FromService] IDbOrm db = null!,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(table_code))
                return SkillResult.Fail("table_code 不能为空");

            // 企业编码兜底（与 NodeExecutor docTable 节点一致）
            var entCode = string.IsNullOrWhiteSpace(enterprise_code) ? "YZH-STD-ENT" : enterprise_code;

            var tables = (await db.GetListAsync<CertPlatform.Shared.Entities.Doc.TableExtractionResult>(x =>
                x.TableCode == table_code && x.EnterpriseCode == entCode)).Data ?? new();

            if (!string.IsNullOrWhiteSpace(file_code))
                tables = tables.Where(x => x.FileCode == file_code).ToList();

            if (table_index.HasValue)
                tables = tables.Where(x => x.TableIndex == table_index.Value).ToList();

            var table = tables.OrderByDescending(x => x.ExtractedAt).FirstOrDefault();
            if (table == null)
                return SkillResult.Fail($"未找到 table_code={table_code}, enterprise_code={entCode}");

            var confidence = (double?)(table.Confidence ?? 0m);
            return SkillResult.Ok(new Dictionary<string, object>
            {
                ["rows"] = TryParseJson(table.ExtractedJson),
                ["extracted_json"] = table.ExtractedJson,
                ["table_code"] = table.TableCode,
                ["table_index"] = table.TableIndex,
                ["confidence"] = confidence
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

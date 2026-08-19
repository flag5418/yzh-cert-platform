using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VOL.Core.EFDbContext;
using VOL.Entity.CertPlatform.Ent;
using YZH.Core.Workflow;

namespace YZH.Core.Skills
{
    /// <summary>
    /// 获取表格数据：按 table_code + enterprise_code 查询表格提取结果。
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
            string table_code,
            string enterprise_code,
            [SkillParam(Description = "文件编码，可选")]
            string? file_code = null,
            [SkillParam(Description = "表格序号，可选")]
            int? table_index = null,
            [FromService] VOLContext db = null!,
            CancellationToken ct = default)
        {
            var query = db.Set<TableExtractionResult>()
                .Where(x => x.TableCode == table_code && x.EnterpriseCode == enterprise_code);

            if (!string.IsNullOrWhiteSpace(file_code))
                query = query.Where(x => x.FileCode == file_code);

            if (table_index.HasValue)
                query = query.Where(x => x.TableIndex == table_index.Value);

            var table = await query.OrderByDescending(x => x.ExtractedAt).FirstOrDefaultAsync(ct);
            if (table == null)
                return SkillResult.Fail($"未找到 table_code={table_code}, enterprise_code={enterprise_code}");

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
                return doc.RootElement.Clone();
            }
            catch (JsonException)
            {
                return json;
            }
        }
    }
}

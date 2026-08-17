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
    /// get_table Skill：按 table_code + enterprise_code 查询表格提取结果（B-09）。
    /// 评审 §3.3 修复：原实现未按 table_code 过滤（查询条件缺失），导致多表数据混淆；补上 table_code + enterprise_code 过滤。
    /// 依赖：ent_table_extraction_result 新增 table_code 列（phase10_wf_skill_upgrade.sql）。
    /// </summary>
    public class GetTableSkill : SkillBase
    {
        public override string SkillCode => "get_table";
        public override string SkillName => "获取表格数据";
        public override string Category => "data_access";
        public override bool SideEffect => true;
        public override string ReturnType => "json";

        public override IReadOnlyList<SkillParam> InputDecls { get; } = new[]
        {
            new SkillParam { Name = "table_code", Type = "string", Required = true, Description = "定义表编码（cert_doc_table_def.code）" },
            new SkillParam { Name = "enterprise_code", Type = "string", Required = true, Description = "企业编码（多租户隔离）" },
            new SkillParam { Name = "file_code", Type = "string", Required = false, Description = "文件编码（可选，文件级过滤）" },
            new SkillParam { Name = "table_index", Type = "number", Required = false, Description = "表格序号（可选，默认最新一条）" }
        };

        public override IReadOnlyList<SkillParam> OutputDecls { get; } = new[]
        {
            new SkillParam { Name = "rows", Type = "json", Required = true, Description = "表格行数据（extracted_json 解析结果）" },
            new SkillParam { Name = "extracted_json", Type = "json", Required = true, Description = "原始提取 JSON（兼容引用）" },
            new SkillParam { Name = "table_code", Type = "string", Required = true, Description = "定义表编码" },
            new SkillParam { Name = "confidence", Type = "number", Required = true, Description = "AI 提取可信度" }
        };

        private readonly VOLContext _db;
        public GetTableSkill(VOLContext db) { _db = db; }

        protected override async Task<SkillResult> ExecuteCoreAsync(SkillContext context, CancellationToken ct)
        {
            var tableCode = GetString(context, "table_code");
            var enterpriseCode = GetString(context, "enterprise_code");
            var fileCode = GetString(context, "file_code");
            var tableIndex = GetInt(context, "table_index");

            var query = _db.Set<TableExtractionResult>()
                .Where(x => x.TableCode == tableCode && x.EnterpriseCode == enterpriseCode);

            if (!string.IsNullOrWhiteSpace(fileCode))
                query = query.Where(x => x.FileCode == fileCode);

            if (tableIndex.HasValue)
                query = query.Where(x => x.TableIndex == tableIndex.Value);

            var table = await query.OrderByDescending(x => x.ExtractedAt).FirstOrDefaultAsync(ct);
            if (table == null)
                return SkillResult.Fail($"未找到 table_code={tableCode}, enterprise_code={enterpriseCode}");

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

        private static string GetString(SkillContext context, string key)
            => context.Inputs.TryGetValue(key, out var v) ? v?.ToString() ?? string.Empty : string.Empty;

        private static int? GetInt(SkillContext context, string key)
            => context.Inputs.TryGetValue(key, out var v) && int.TryParse(v?.ToString(), out var i) ? i : null;
    }
}

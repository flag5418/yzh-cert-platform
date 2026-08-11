using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VOL.Core.EFDbContext;
using VOL.Entity.CertPlatform.Ent;
using YZH.Core.Workflow;

namespace YZH.Core.Skills
{
    public class GetTableSkill : ISkillNode
    {
        public string SkillCode => "get_table";
        private readonly VOLContext _db;
        public GetTableSkill(VOLContext db) { _db = db; }

        public async Task<SkillResult> ExecuteAsync(SkillContext context, CancellationToken ct = default)
        {
            var tableCode = context.Inputs.TryGetValue("table_code", out var tc) ? tc?.ToString() : string.Empty;
            var fileCode = context.Inputs.TryGetValue("file_code", out var fc) ? fc?.ToString() : string.Empty;

            if (string.IsNullOrWhiteSpace(tableCode))
                return new SkillResult { Success = false, Error = "get_table 需要 table_code 入参" };

            var query = _db.Set<TableExtractionResult>().Where(x => x.ExtractedJson != null);
            if (!string.IsNullOrWhiteSpace(fileCode))
                query = query.Where(x => x.FileCode == fileCode);

            var table = await query.OrderByDescending(x => x.ExtractedAt).FirstOrDefaultAsync(ct);
            if (table == null)
                return new SkillResult { Success = false, Error = $"未找到 table_code={tableCode}" };

            var confidence = (double?)(table.Confidence ?? 0m);
            return new SkillResult
            {
                Success = true,
                Outputs = new Dictionary<string, object>
                {
                    ["table_code"] = table.Code,
                    ["extracted_json"] = table.ExtractedJson,
                    ["confidence"] = confidence,
                    ["table_index"] = table.TableIndex
                },
                Confidence = confidence
            };
        }
    }
}

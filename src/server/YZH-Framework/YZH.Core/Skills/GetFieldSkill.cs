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
    public class GetFieldSkill : ISkillNode
    {
        public string SkillCode => "get_field";
        private readonly VOLContext _db;
        public GetFieldSkill(VOLContext db) { _db = db; }

        public async Task<SkillResult> ExecuteAsync(SkillContext context, CancellationToken ct = default)
        {
            var labelTag = context.Inputs.TryGetValue("label_tag", out var lt) ? lt?.ToString() : string.Empty;
            var fileCode = context.Inputs.TryGetValue("file_code", out var fc) ? fc?.ToString() : string.Empty;

            if (string.IsNullOrWhiteSpace(labelTag))
                return new SkillResult { Success = false, Error = "get_field 需要 label_tag 入参" };

            var query = _db.Set<ExtractionResult>().Where(x => x.LabelTag == labelTag);
            if (!string.IsNullOrWhiteSpace(fileCode))
                query = query.Where(x => x.FileCode == fileCode);

            var field = await query.OrderByDescending(x => x.ExtractedAt).FirstOrDefaultAsync(ct);
            if (field == null)
                return new SkillResult { Success = false, Error = $"未找到 label_tag={labelTag}" };

            var confidence = (double?)(field.Confidence ?? 0m);
            return new SkillResult
            {
                Success = true,
                Outputs = new Dictionary<string, object>
                {
                    ["field_code"] = field.FieldCode,
                    ["field_value"] = field.ExtractedValue,
                    ["confidence"] = confidence,
                    ["label_tag"] = field.LabelTag,
                    ["is_manual_edited"] = field.IsManualEdited
                },
                Confidence = confidence
            };
        }
    }
}

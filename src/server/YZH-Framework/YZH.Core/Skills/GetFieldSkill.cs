using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VOL.Core.EFDbContext;
using VOL.Entity.CertPlatform.Ent;
using YZH.Core.Workflow;

namespace YZH.Core.Skills
{
    /// <summary>
    /// 获取字段值：按 field_code + enterprise_code 查询提取结果。
    /// </summary>
    [Skill(
        Code = "get_field",
        Name = "获取字段值",
        ReturnType = "json",
        Description = "按字段编码和企业编码查询已提取的文档字段值"
    )]
    public static class GetFieldSkill
    {
        public static async Task<SkillResult> ExecuteAsync(
            string field_code,
            string enterprise_code,
            [SkillParam(Description = "文件编码，可选，文件级过滤")]
            string? file_code = null,
            [FromService] VOLContext db = null!,
            CancellationToken ct = default)
        {
            var query = db.Set<ExtractionResult>()
                .Where(x => x.FieldCode == field_code && x.EnterpriseCode == enterprise_code);

            if (!string.IsNullOrWhiteSpace(file_code))
                query = query.Where(x => x.FileCode == file_code);

            var field = await query.OrderByDescending(x => x.ExtractedAt).FirstOrDefaultAsync(ct);
            if (field == null)
                return SkillResult.Fail($"未找到 field_code={field_code}, enterprise_code={enterprise_code}");

            var confidence = (double?)(field.Confidence ?? 0m);
            return SkillResult.Ok(new Dictionary<string, object>
            {
                ["field_code"] = field.FieldCode,
                ["field_name"] = field.FieldName ?? string.Empty,
                ["field_value"] = field.ExtractedValue ?? string.Empty,
                ["confidence"] = confidence,
                ["is_manual_edited"] = field.IsManualEdited
            }, confidence);
        }
    }
}

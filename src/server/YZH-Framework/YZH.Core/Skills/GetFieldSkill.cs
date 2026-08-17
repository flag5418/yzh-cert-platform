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
    /// get_field Skill：按 field_code + enterprise_code 查询提取结果（B-08）。
    /// 方案 C 整改：原 label_tag 查询键废弃，改用 field_code；enterprise_code 隔离多企业数据。
    /// </summary>
    public class GetFieldSkill : SkillBase
    {
        public override string SkillCode => "get_field";
        public override string SkillName => "获取字段值";
        public override string Category => "data_access";
        public override bool SideEffect => true;
        public override string ReturnType => "json";

        public override IReadOnlyList<SkillParam> InputDecls { get; } = new[]
        {
            new SkillParam { Name = "field_code", Type = "string", Required = true, Description = "字段编码（cert_doc_field_def.field_code）" },
            new SkillParam { Name = "enterprise_code", Type = "string", Required = true, Description = "企业编码（多租户隔离）" },
            new SkillParam { Name = "file_code", Type = "string", Required = false, Description = "文件编码（可选，文件级过滤）" }
        };

        public override IReadOnlyList<SkillParam> OutputDecls { get; } = new[]
        {
            new SkillParam { Name = "field_value", Type = "json", Required = true, Description = "提取值（字符串/JSON 结构）" },
            new SkillParam { Name = "field_name", Type = "string", Required = false, Description = "字段中文名" },
            new SkillParam { Name = "confidence", Type = "number", Required = true, Description = "AI 提取可信度" },
            new SkillParam { Name = "is_manual_edited", Type = "boolean", Required = false, Description = "是否人工复核过" }
        };

        private readonly VOLContext _db;
        public GetFieldSkill(VOLContext db) { _db = db; }

        protected override async Task<SkillResult> ExecuteCoreAsync(SkillContext context, CancellationToken ct)
        {
            var fieldCode = GetString(context, "field_code");
            var enterpriseCode = GetString(context, "enterprise_code");
            var fileCode = GetString(context, "file_code");

            var query = _db.Set<ExtractionResult>()
                .Where(x => x.FieldCode == fieldCode && x.EnterpriseCode == enterpriseCode);

            if (!string.IsNullOrWhiteSpace(fileCode))
                query = query.Where(x => x.FileCode == fileCode);

            var field = await query.OrderByDescending(x => x.ExtractedAt).FirstOrDefaultAsync(ct);
            if (field == null)
                return SkillResult.Fail($"未找到 field_code={fieldCode}, enterprise_code={enterpriseCode}");

            var confidence = (double?)(field.Confidence ?? 0m);
            return SkillResult.Ok(new Dictionary<string, object>
            {
                ["field_code"] = field.FieldCode,
                ["field_name"] = field.FieldName ?? string.Empty,
                ["field_value"] = field.ExtractedValue,
                ["confidence"] = confidence,
                ["is_manual_edited"] = field.IsManualEdited
            }, confidence);
        }

        private static string GetString(SkillContext context, string key)
            => context.Inputs.TryGetValue(key, out var v) ? v?.ToString() ?? string.Empty : string.Empty;
    }
}

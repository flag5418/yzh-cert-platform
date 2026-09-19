
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CertPlatform.Admin.Services.Workflow.Skills;
using YZH.Core.DataBase.Interfaces;
using CertPlatform.Shared.Entities.Ent;

namespace CertPlatform.Admin.Services.Workflow.Skills
{
    /// <summary>
    /// 获取字段值：按 field_code + enterprise_code 查询提取结果。
    /// <para>移植自：旧 YZH.Builder .../Skills/GetFieldSkill.cs</para>
    /// <para>迁移改写：EF VOLContext → IDbOrm.GetListAsync（SqlSugar 强类型查询，与 NodeExecutor docField 同数据源）</para>
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
            [SkillParam(Description = "字段编码，如 iso9001.ent_base.biz_lic.Name")]
            string field_code,
            [SkillParam(Description = "企业编码（空=标准样例企业 YZH-STD-ENT）")]
            string enterprise_code,
            [SkillParam(Description = "文件编码，可选，文件级过滤")]
            string? file_code = null,
            [FromService] IDbOrm db = null!,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(field_code))
                return SkillResult.Fail("field_code 不能为空");

            // 企业编码兜底（与 NodeExecutor docField 节点一致）
            var entCode = string.IsNullOrWhiteSpace(enterprise_code) ? "YZH-STD-ENT" : enterprise_code;

            var fields = (await db.GetListAsync<CertPlatform.Shared.Entities.Ent.ExtractionResult>(x =>
                x.FieldCode == field_code && x.EnterpriseCode == entCode)).Data ?? new();

            if (string.IsNullOrWhiteSpace(file_code))
            {
                var picked = fields.OrderByDescending(x => x.ExtractedAt).FirstOrDefault();
                if (picked == null)
                    return SkillResult.Fail($"未找到 field_code={field_code}, enterprise_code={entCode}");

                var confidence = (double?)(picked.Confidence ?? 0m);
                return SkillResult.Ok(new Dictionary<string, object>
                {
                    ["field_code"] = picked.FieldCode,
                    ["field_name"] = picked.FieldName ?? string.Empty,
                    ["field_value"] = picked.ExtractedValue ?? string.Empty,
                    ["confidence"] = confidence,
                    ["is_manual_edited"] = picked.IsManualEdited
                }, confidence);
            }

            // 文件级过滤
            var byFile = fields.Where(x => x.FileCode == file_code)
                .OrderByDescending(x => x.ExtractedAt).FirstOrDefault();
            if (byFile == null)
                return SkillResult.Fail($"未找到 field_code={field_code}, enterprise_code={entCode}, file_code={file_code}");

            var conf = (double?)(byFile.Confidence ?? 0m);
            return SkillResult.Ok(new Dictionary<string, object>
            {
                ["field_code"] = byFile.FieldCode,
                ["field_name"] = byFile.FieldName ?? string.Empty,
                ["field_value"] = byFile.ExtractedValue ?? string.Empty,
                ["confidence"] = conf,
                ["is_manual_edited"] = byFile.IsManualEdited
            }, conf);
        }
    }
}

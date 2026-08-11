using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YZH.Core.Extractor;
using YZH.Core.Extractor.Models;
using YZH.Core.Workflow;

namespace YZH.Core.Skills
{
    public class DocumentExtractSkill : ISkillNode
    {
        public string SkillCode => "document_extract";
        private readonly IFileExtractor _extractor;
        public DocumentExtractSkill(IFileExtractor extractor) { _extractor = extractor; }

        public async Task<SkillResult> ExecuteAsync(SkillContext context, CancellationToken ct = default)
        {
            var storagePath = context.Inputs.TryGetValue("storage_path", out var s) ? s?.ToString() : string.Empty;
            var convertedStoragePath = context.Inputs.TryGetValue("converted_storage_path", out var cs) ? cs?.ToString() : string.Empty;
            var convertStatus = context.Inputs.TryGetValue("convert_status", out var st) ? st?.ToString() : null;
            var convertMessage = context.Inputs.TryGetValue("convert_message", out var cm) ? cm?.ToString() : null;

            if (string.Equals(convertStatus, "pending", StringComparison.OrdinalIgnoreCase))
                return new SkillResult { Success = false, Error = "DOC/XLS 正在转换中，请稍后再试" };
            if (string.Equals(convertStatus, "failed", StringComparison.OrdinalIgnoreCase))
                return new SkillResult { Success = false, Error = $"旧版文件转换失败：{convertMessage ?? "未知原因"}" };

            var useConverted = string.Equals(convertStatus, "converted", StringComparison.OrdinalIgnoreCase)
                               && !string.IsNullOrWhiteSpace(convertedStoragePath);
            var effectivePath = useConverted ? convertedStoragePath : storagePath;

            if (string.IsNullOrWhiteSpace(effectivePath))
                return new SkillResult { Success = false, Error = "缺少 storage_path 入参" };

            FileExtractionResult extraction;
            try
            {
                extraction = await _extractor.ExtractAsync(effectivePath, ct: ct);
            }
            catch (Exception ex)
            {
                return new SkillResult { Success = false, Error = $"本地提取失败: {ex.Message}" };
            }

            var confidence = extraction.Fields.Count > 0
                ? (double?)(extraction.Fields.Where(f => f.Confidence.HasValue).DefaultIfEmpty(new ExtractedField { Confidence = 1m }).Min(f => (double)f.Confidence!.Value))
                : 1.0;

            return new SkillResult
            {
                Success = true,
                Outputs = new Dictionary<string, object>
                {
                    ["fields"] = extraction.Fields,
                    ["tables"] = extraction.Tables,
                    ["full_text"] = extraction.FullText ?? string.Empty,
                    ["effective_path"] = effectivePath,
                    ["is_converted_version"] = useConverted
                },
                Confidence = confidence
            };
        }
    }
}

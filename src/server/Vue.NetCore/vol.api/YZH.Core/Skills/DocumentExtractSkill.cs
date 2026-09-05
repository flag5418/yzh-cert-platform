using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using YZH.Core.Extractor;
using YZH.Core.Extractor.Models;
using YZH.Core.Workflow;

namespace YZH.Core.Skills
{
    /// <summary>
    /// 文档内容提取：本地解析 Word/Excel/PDF/Text，输出段落+表格+全文文本。非 AI，纯文件解析。
    /// </summary>
    [Skill(
        Code = "document_extract",
        Name = "文档内容提取",
        ReturnType = "json",
        Description = "本地解析 Word/Excel/PDF/Text 文件，输出结构化段落、表格和全文文本"
    )]
    public static class DocumentExtractSkill
    {
        public static async Task<SkillResult> ExecuteAsync(
            [SkillParam(Description = "源文件存储路径")]
            string storage_path,
            [SkillParam(Description = "转换后文件路径（旧版文件）")]
            string? converted_storage_path = null,
            [SkillParam(Description = "转换状态：pending/failed/converted")]
            string? convert_status = null,
            [SkillParam(Description = "转换消息（失败原因）")]
            string? convert_message = null,
            [FromService] IFileExtractor extractor = null!,
            CancellationToken ct = default)
        {
            if (string.Equals(convert_status, "pending", StringComparison.OrdinalIgnoreCase))
                return SkillResult.Fail("DOC/XLS 正在转换中，请稍后再试");

            if (string.Equals(convert_status, "failed", StringComparison.OrdinalIgnoreCase))
                return SkillResult.Fail($"旧版文件转换失败：{(string.IsNullOrEmpty(convert_message) ? "未知原因" : convert_message)}");

            var useConverted = string.Equals(convert_status, "converted", StringComparison.OrdinalIgnoreCase)
                               && !string.IsNullOrWhiteSpace(converted_storage_path);
            var effectivePath = useConverted ? converted_storage_path! : storage_path;

            if (string.IsNullOrWhiteSpace(effectivePath))
                return SkillResult.Fail("缺少 storage_path 入参");

            FileExtractionResult extraction;
            try
            {
                extraction = await extractor.ExtractAsync(effectivePath, ct: ct);
            }
            catch (Exception ex)
            {
                return SkillResult.Fail($"本地提取失败: {ex.Message}");
            }

            if (extraction.Status == ExtractStatus.Unsupported)
                return SkillResult.Fail(extraction.Message ?? "不支持的文件类型");

            if (extraction.Status == ExtractStatus.OcrRequired)
                return SkillResult.Ok(new Dictionary<string, object>
                {
                    ["sections"] = JsonSerializer.Serialize(Array.Empty<object>()),
                    ["tables"] = JsonSerializer.Serialize(Array.Empty<object>()),
                    ["full_text"] = string.Empty,
                    ["source_type"] = extraction.SourceType.ToString(),
                    ["file_name"] = extraction.FileName ?? string.Empty,
                    ["effective_path"] = effectivePath,
                    ["is_converted_version"] = useConverted
                });

            var sectionsJson = JsonSerializer.Serialize(extraction.Sections, new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            var tablesJson = JsonSerializer.Serialize(extraction.Tables, new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var confidence = extraction.Sections.Count > 0 ? 1.0 : 0.5;

            return SkillResult.Ok(new Dictionary<string, object>
            {
                ["sections"] = sectionsJson,
                ["tables"] = tablesJson,
                ["full_text"] = extraction.FullText ?? string.Empty,
                ["source_type"] = extraction.SourceType.ToString(),
                ["file_name"] = extraction.FileName ?? string.Empty,
                ["effective_path"] = effectivePath,
                ["is_converted_version"] = useConverted
            }, confidence);
        }
    }
}

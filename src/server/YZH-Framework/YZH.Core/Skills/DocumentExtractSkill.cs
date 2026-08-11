using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using YZH.Core.Extractor;
using YZH.Core.Extractor.Models;
using YZH.Core.Workflow;

namespace YZH.Core.Skills
{
    /// <summary>
    /// 包装 IFileExtractor 的本地提取 Skill。
    /// <para>衔接 Office convertStatus 状态机：pending/failed/converted。</para>
    /// <para>输出结构（JSON）：</para>
    /// <list type="bullet">
    ///   <item>sections: TextSection[] — 结构化段落列表（含页码/行号/位置JSON）</item>
    ///   <item>tables: ExtractedTable[] — 结构化表格列表</item>
    ///   <item>full_text: string — 扁平全文（兼容旧版）</item>
    ///   <item>source_type: string — Word/Excel/PDF/Text</item>
    ///   <item>file_name: string — 源文件名</item>
    ///   <item>effective_path: string — 实际提取路径</item>
    ///   <item>is_converted_version: bool — 是否使用转换后文件</item>
    /// </list>
    /// </summary>
    public class DocumentExtractSkill : ISkillNode
    {
        public string SkillCode => "document_extract";

        private readonly IFileExtractor _extractor;

        public DocumentExtractSkill(IFileExtractor extractor)
        {
            _extractor = extractor;
        }

        public async Task<SkillResult> ExecuteAsync(SkillContext context, CancellationToken ct = default)
        {
            var storagePath = context.Inputs.TryGetValue("storage_path", out var s) ? s?.ToString() : string.Empty;
            var convertedStoragePath = context.Inputs.TryGetValue("converted_storage_path", out var cs) ? cs?.ToString() : string.Empty;
            var convertStatus = context.Inputs.TryGetValue("convert_status", out var st) ? st?.ToString() : null;
            var convertMessage = context.Inputs.TryGetValue("convert_message", out var cm) ? cm?.ToString() : null;

            // pending：文件正在异步转换中
            if (string.Equals(convertStatus, "pending", StringComparison.OrdinalIgnoreCase))
                return new SkillResult { Success = false, Error = "DOC/XLS 正在转换中，请稍后再试" };

            // failed：转换失败
            if (string.Equals(convertStatus, "failed", StringComparison.OrdinalIgnoreCase))
                return new SkillResult { Success = false, Error = $"旧版文件转换失败：{convertMessage ?? "未知原因"}" };

            // converted / 原生 OOXML / PDF：选择提取路径
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

            if (extraction.Status == ExtractStatus.Unsupported)
                return new SkillResult { Success = false, Error = extraction.Message ?? "不支持的文件类型" };

            if (extraction.Status == ExtractStatus.OcrRequired)
                return new SkillResult
                {
                    Success = false,
                    Error = extraction.Message ?? "扫描件需 OCR 链路（暂未接入）",
                    Outputs = new Dictionary<string, object>
                    {
                        ["sections"] = JsonSerializer.Serialize(Array.Empty<object>()),
                        ["tables"] = JsonSerializer.Serialize(Array.Empty<object>()),
                        ["full_text"] = string.Empty,
                        ["source_type"] = extraction.SourceType.ToString(),
                        ["file_name"] = extraction.FileName,
                        ["effective_path"] = effectivePath,
                        ["is_converted_version"] = useConverted,
                        ["ocr_required"] = true
                    }
                };

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

            // 置信度：有 Sections 则 1.0（本地提取确定性高），否则 0.5
            var confidence = extraction.Sections.Count > 0 ? 1.0 : 0.5;

            return new SkillResult
            {
                Success = true,
                Outputs = new Dictionary<string, object>
                {
                    ["sections"] = sectionsJson,
                    ["tables"] = tablesJson,
                    ["full_text"] = extraction.FullText ?? string.Empty,
                    ["source_type"] = extraction.SourceType.ToString(),
                    ["file_name"] = extraction.FileName,
                    ["effective_path"] = effectivePath,
                    ["is_converted_version"] = useConverted,
                    ["ocr_required"] = false
                },
                Confidence = confidence
            };
        }
    }
}

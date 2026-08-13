using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VOL.Core.Extensions.AutofacManager;
using VOL.Entity.CertPlatform.Dir;
using VOL.Entity.CertPlatform.DocExtraction;
using VOL.Entity.CertPlatform.DocExtraction.DTOs;
using YZH.Core.AI.Prompt;
using YZH.Core.AI.Prompt.Models;
using YZH.Core.AI.Clients.Models;
using YZH.Core.Workflow;
using YZH.Core.Workflow.Models;
using YZH.Core.Skills;
using YZH.Core.Extractor.Models;
using VOL.Entity.CertPlatform.Wf;

namespace VOL.Builder.Services.CertPlatform
{
    public partial class DocExtractionRuleService
    {
        #region S5 接入 YZH.Core 四件套

        /// <summary>
        /// 从标准目录按 fileCode 查询文件信息（StoragePath / ConvertStatus 等）。
        /// </summary>
        private async Task<StandardDirectoryFile> GetFileInfoAsync(string fileCode)
        {
            return await repository.DbContext.Set<StandardDirectoryFile>()
                .FirstOrDefaultAsync(x => x.FileCode == fileCode);
        }

        /// <summary>
        /// 调用 IFileExtractor 提取文档内容，返回结构化结果（含 Sections + Tables + FullText）。
        /// </summary>
        private async Task<FileExtractionResult> ExtractDocumentContentAsync(StandardDirectoryFile fileInfo, string skill)
        {
            if (fileInfo == null) return FileExtractionResult.CreateBase("unknown");

            // .doc 旧格式必须依赖 doc→docx 转换产物才能提取（NPOI 无 HWPF）。
            // 转换未完成/失败时给出明确提示，而不是让用户看到笼统的“不支持的文件类型”。
            var fileExt = Path.GetExtension(fileInfo.FileName ?? "").TrimStart('.').ToLower();
            if (fileExt == "doc" && string.IsNullOrEmpty(fileInfo.ConvertedStoragePath))
            {
                var r = FileExtractionResult.CreateBase(fileInfo.FileName);
                r.Status = YZH.Core.Extractor.Models.ExtractStatus.Unsupported;
                r.Message = (fileInfo.ConvertStatus ?? "").ToLowerInvariant() switch
                {
                    "converting" => $"文件正在转换中（.doc→.docx），转换完成后即可分析，请稍后重试（{fileInfo.FileName}）",
                    "failed" => $"文件转换失败：{fileInfo.ConvertMessage ?? "未知原因"}，请重新上传或联系管理员（{fileInfo.FileName}）",
                    _ => $"文件尚未完成 doc→docx 转换，转换完成后即可分析，请稍后重试（{fileInfo.FileName}）"
                };
                return r;
            }

            var storagePath = fileInfo.ConvertedStoragePath ?? fileInfo.StoragePath;
            if (string.IsNullOrWhiteSpace(storagePath))
            {
                var r = FileExtractionResult.CreateBase(fileInfo.FileName);
                r.Status = YZH.Core.Extractor.Models.ExtractStatus.Unsupported;
                r.Message = "文件路径为空";
                return r;
            }

            var extractor = AutofacContainerModule.GetService<YZH.Core.Extractor.IFileExtractor>();
            if (extractor == null)
            {
                var r = FileExtractionResult.CreateBase(fileInfo.FileName);
                r.Status = YZH.Core.Extractor.Models.ExtractStatus.Unsupported;
                r.Message = "文件提取器不可用";
                return r;
            }

            try
            {
                // StoragePath 是 MinIO 对象路径，需下载到内存流后调用流式提取（提取器要求本地文件/可寻址流）
                var minio = AutofacContainerModule.GetService<VOL.Builder.IServices.CertPlatform.IMinIOHelper>();
                if (minio != null)
                {
                    var (stream, _) = await minio.DownloadAsync(storagePath);
                    using (stream)
                    {
                        return await extractor.ExtractAsync(stream, fileInfo.FileName);
                    }
                }

                // 无 MinIO 时回退：路径可能是本地文件
                return await extractor.ExtractAsync(storagePath);
            }
            catch
            {
                var r = FileExtractionResult.CreateBase(fileInfo.FileName);
                r.Status = YZH.Core.Extractor.Models.ExtractStatus.Failed;
                r.ErrorMessage = "提取异常";
                return r;
            }
        }

        /// <summary>
        /// 将结构化 Sections 转为 LLM 可读的带位置标记文本。
        /// <para>示例输出：[Page:1 Line:10] 段落内容...</para>
        /// </summary>
        private static string BuildStructuredContext(YZH.Core.Extractor.Models.FileExtractionResult extraction)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"# 文档类型：{extraction.SourceType}");
            sb.AppendLine($"# 文件名：{extraction.FileName}");
            sb.AppendLine($"# 段落总数：{extraction.Sections.Count} | 表格数：{extraction.Tables.Count}");
            sb.AppendLine();

            foreach (var sec in extraction.Sections)
            {
                var location = sec.PositionInfo != null ? $" [{sec.PositionInfo}]" : "";
                var typeTag = sec.SectionType != "paragraph" ? $" ({sec.SectionType})" : "";
                sb.AppendLine($"[Section:{sec.SectionIndex}{typeTag}{location}]");
                sb.AppendLine(sec.Content);
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// 调用 WorkflowEngine + LlmExtractSkill 做 AI 字段/表格推荐（analyze 模式）。
        /// </summary>
        private async Task<AIAnalyzeResponse> CallAIForAnalysisAsync(YZH.Core.Extractor.Models.FileExtractionResult extraction, string skill)
        {
            if (extraction == null)
                return new AIAnalyzeResponse { Fields = new(), Tables = new(), Message = "文档信息为空" };

            if (extraction.Status == YZH.Core.Extractor.Models.ExtractStatus.Unsupported)
                return new AIAnalyzeResponse
                {
                    Fields = new(),
                    Tables = new(),
                    // 提取器返回的 Message 已包含具体原因（如“转换中/转换失败/旧格式不支持”），直接透传给前端
                    Message = string.IsNullOrEmpty(extraction.Message) ? "不支持的文件类型，请确认文件已转换为 .docx/.xlsx 格式" : extraction.Message
                };

            if (extraction.Status == YZH.Core.Extractor.Models.ExtractStatus.OcrRequired)
                return new AIAnalyzeResponse
                {
                    Fields = new(),
                    Tables = new(),
                    Message = "该文档为扫描件（无文本层），需要 OCR 链路（暂未接入），请使用文字版 PDF 或 .docx/.xlsx 文件"
                };

            if (extraction.Sections.Count == 0)
                return new AIAnalyzeResponse { Fields = new(), Tables = new(), Message = "文档内容为空或提取失败" };

            var docContent = BuildStructuredContext(extraction);
            var workflowEngine = AutofacContainerModule.GetService<IWorkflowEngine>();
            if (workflowEngine == null)
                return new AIAnalyzeResponse { Fields = new(), Tables = new(), Message = "AI 分析服务未配置（LLM 工作流未注册），文档内容已提取成功，可先在“提示词与验证”页签配置提取规则" };

            var analyzePrompt = await BuildAnalysisPromptAsync(skill);
            var workflowJson = BuildExtractWorkflow(analyzePrompt);
            var ctx = new WorkflowContext
            {
                WorkflowInstanceId = Guid.NewGuid().ToString("N"),
                BusinessType = "file_upload",
                Inputs = new Dictionary<string, object>
                {
                    ["document_content"] = docContent,
                    ["prompt"] = analyzePrompt
                }
            };

            var result = await workflowEngine.RunAsync(workflowJson, ctx);
            if (!result.Success || !result.NodeOutputs.TryGetValue("n1", out var outputs))
                return new AIAnalyzeResponse { Fields = new(), Tables = new(), Message = "AI 分析失败" };

            var fields = MapAiFieldsToDtos(outputs);
            var tables = MapAiTablesToDtos(outputs);
            return new AIAnalyzeResponse { Fields = fields, Tables = tables, Message = "AI分析完成" };
        }

        /// <summary>
        /// 调用 WorkflowEngine + LlmExtractSkill 执行实际提取（verify 模式）。
        /// </summary>
        private async Task<ExtractionData> CallAIForExtractionAsync(YZH.Core.Extractor.Models.FileExtractionResult extraction, string prompt)
        {
            if (extraction == null || extraction.Sections.Count == 0 || string.IsNullOrWhiteSpace(prompt))
            {
                // 提取层未产生内容时，把具体原因（如“转换中/转换失败/不支持”）透传给前端
                var msg = extraction?.Message;
                if (!string.IsNullOrEmpty(msg))
                    return new ExtractionData { Fields = new(), Tables = new(), Message = msg };
                return new ExtractionData { Fields = new(), Tables = new() };
            }

            var docContent = BuildStructuredContext(extraction);
            var workflowEngine = AutofacContainerModule.GetService<IWorkflowEngine>();
            if (workflowEngine == null)
                return new ExtractionData { Fields = new(), Tables = new() };

            var workflowJson = BuildExtractWorkflow(prompt);
            var ctx = new WorkflowContext
            {
                WorkflowInstanceId = Guid.NewGuid().ToString("N"),
                BusinessType = "file_upload",
                Inputs = new Dictionary<string, object>
                {
                    ["document_content"] = docContent,
                    ["prompt"] = prompt
                }
            };

            var result = await workflowEngine.RunAsync(workflowJson, ctx);
            if (!result.Success || !result.NodeOutputs.TryGetValue("n1", out var outputs))
                return new ExtractionData { Fields = new(), Tables = new() };

            return MapOutputsToExtractionData(outputs);
        }

        #endregion

        #region 私有辅助方法

        /// <summary>
        /// 构建 AI 分析提示词（analyze 模式）：优先从 DB 读取 V2 模板，其次 V1，最后回退到内嵌默认。
        /// </summary>
        private async Task<string> BuildAnalysisPromptAsync(string skill)
        {
            // 优先尝试 V2 版本（中英文双名 + 提取值预览）
            var promptCodeV2 = $"analyze_{skill}_v2";
            var dbPromptV2 = await repository.DbContext.Set<PromptTemplate>()
                .FirstOrDefaultAsync(x => x.PromptCode == promptCodeV2 && x.IsActive == true && x.Enable == true);
            if (dbPromptV2 != null && !string.IsNullOrWhiteSpace(dbPromptV2.Template))
            {
                Console.WriteLine($"[DocExtractionRule] 📝 使用数据库提示词 V2: {promptCodeV2} (v{dbPromptV2.Version})");
                return dbPromptV2.Template;
            }

            // 其次尝试 V1 版本
            var promptCodeV1 = $"analyze_{skill}_v1";
            var dbPromptV1 = await repository.DbContext.Set<PromptTemplate>()
                .FirstOrDefaultAsync(x => x.PromptCode == promptCodeV1 && x.IsActive == true && x.Enable == true);
            if (dbPromptV1 != null && !string.IsNullOrWhiteSpace(dbPromptV1.Template))
            {
                Console.WriteLine($"[DocExtractionRule] 📝 使用数据库提示词 V1: {promptCodeV1} (v{dbPromptV1.Version})");
                return dbPromptV1.Template;
            }

            // 回退到内嵌默认
            Console.WriteLine($"[DocExtractionRule] 📝 使用内嵌默认提示词");
            return BuildDefaultAnalysisPrompt(skill);
        }

        private static string BuildDefaultAnalysisPrompt(string skill)
        {
            var skillDesc = skill.ToLowerInvariant() switch
            {
                "word" => "Word 文档",
                "excel" => "Excel 表格",
                "pdf" => "PDF 文档",
                _ => "文档"
            };
            return $@"你是专业的文档分析助手。请分析以下{skillDesc}的内容结构，推荐需要提取的字段和表格。
输出要求：
1. fields: 数组，每项含 field_code（英文驼峰）、field_name（中文名称）、field_type（string/number/date）、description
2. tables: 数组，每项含 table_code（英文驼峰）、table_name（中文名称）、description、columns（列定义数组，每项含 column_code / column_name / column_type）

只输出 JSON，不要任何解释文字。

{skillDesc}内容：
---
{{document_content}}
---";
        }

        /// <summary>
        /// 构建最小提取工作流 JSON（单节点 llm_extract）。
        /// </summary>
        private static string BuildExtractWorkflow(string prompt)
        {
            // 提示词中 {document_content} 占位符由 WorkflowEngine ResolveInputs 处理
            // 但 LlmExtractSkill 直接读取 context.Inputs["prompt"]，所以这里把 prompt 直接传入
            return @"{
                ""nodes"": [
                    {
                        ""node_id"": ""n1"",
                        ""skill_code"": ""llm_extract"",
                        ""inputs"": {
                            ""document_content"": ""{{input.document_content}}"",
                            ""prompt"": ""{{input.prompt}}""
                        },
                        ""output"": ""result""
                    }
                ],
                ""edges"": [],
                ""branches"": [],
                ""output_config"": { ""result_key"": ""result"" }
            }";
        }

        /// <summary>
        /// 将 LlmExtractSkill 输出映射为 AIAnalyzeResponse 的字段 DTO 列表（支持 V2 格式）。
        /// V2 格式：field_name_cn / field_name_en / extracted_value
        /// 兼容 V1 格式：field_code / field_name
        /// </summary>
        private static List<FieldDefDto> MapAiFieldsToDtos(IDictionary<string, object> outputs)
        {
            var result = new List<FieldDefDto>();
            if (!outputs.TryGetValue("fields", out var fieldsObj) || fieldsObj is not IEnumerable<object> fields)
                return result;

            foreach (var f in fields)
            {
                if (f is not IDictionary<string, object> fd) continue;

                // 优先读取 V2 格式字段
                var nameCn = fd.TryGetValue("field_name_cn", out var v1) ? v1?.ToString() : null;
                var nameEn = fd.TryGetValue("field_name_en", out var v2) ? v2?.ToString() : null;
                var extractedValue = fd.TryGetValue("extracted_value", out var v3) ? v3?.ToString() : null;
                var isRequired = fd.TryGetValue("is_required", out var v4) && bool.TryParse(v4?.ToString(), out var r) && r;

                // 兼容 V1 格式
                if (string.IsNullOrEmpty(nameCn))
                    nameCn = fd.TryGetValue("field_name", out var v5) ? v5?.ToString() : "";
                if (string.IsNullOrEmpty(nameEn))
                    nameEn = fd.TryGetValue("field_code", out var v6) ? v6?.ToString() : "";

                result.Add(new FieldDefDto
                {
                    Name = nameCn ?? "",
                    NameEn = nameEn ?? "",
                    Code = nameEn ?? nameCn ?? "",
                    DataType = fd.TryGetValue("field_type", out var t) ? t?.ToString() ?? "string" : "string",
                    Description = fd.TryGetValue("description", out var d) ? d?.ToString() ?? "" : "",
                    IsRequired = isRequired,
                    IsManual = false,
                    ExtractedValue = extractedValue ?? ""
                });
            }
            return result;
        }

        /// <summary>
        /// 将 LlmExtractSkill 输出映射为 AIAnalyzeResponse 的表格 DTO 列表（支持 V2 格式）。
        /// V2 格式：table_name_cn / table_name_en / extracted_data
        /// 兼容 V1 格式：table_code / table_name
        /// </summary>
        private static List<TableDefDto> MapAiTablesToDtos(IDictionary<string, object> outputs)
        {
            var result = new List<TableDefDto>();
            if (!outputs.TryGetValue("tables", out var tablesObj) || tablesObj is not IEnumerable<object> tables)
                return result;

            foreach (var t in tables)
            {
                if (t is not IDictionary<string, object> td) continue;

                // 读取列定义（支持 V2 和 V1 格式）
                var cols = new List<TableColumnDto>();
                if (td.TryGetValue("columns", out var colsObj) && colsObj is IEnumerable<object> colsList)
                {
                    foreach (var c in colsList)
                    {
                        if (c is not IDictionary<string, object> cd) continue;

                        // 优先 V2 格式
                        var colNameCn = cd.TryGetValue("column_name_cn", out var cv1) ? cv1?.ToString() : null;
                        var colNameEn = cd.TryGetValue("column_name_en", out var cv2) ? cv2?.ToString() : null;

                        // 兼容 V1 格式
                        if (string.IsNullOrEmpty(colNameCn))
                            colNameCn = cd.TryGetValue("column_name", out var cv3) ? cv3?.ToString() : "";
                        if (string.IsNullOrEmpty(colNameEn))
                            colNameEn = cd.TryGetValue("column_code", out var cv4) ? cv4?.ToString() : "";

                        cols.Add(new TableColumnDto
                        {
                            Name = colNameCn ?? "",
                            NameEn = colNameEn ?? "",
                            Code = colNameEn ?? colNameCn ?? "",
                            DataType = cd.TryGetValue("column_type", out var tp) ? tp?.ToString() ?? "string" : "string"
                        });
                    }
                }

                // 优先读取 V2 格式字段
                var tableNameCn = td.TryGetValue("table_name_cn", out var tv1) ? tv1?.ToString() : null;
                var tableNameEn = td.TryGetValue("table_name_en", out var tv2) ? tv2?.ToString() : null;
                var sheetName = td.TryGetValue("sheet_name", out var tv3) ? tv3?.ToString() : null;

                // 兼容 V1 格式
                if (string.IsNullOrEmpty(tableNameCn))
                    tableNameCn = td.TryGetValue("table_name", out var tv4) ? tv4?.ToString() : "";
                if (string.IsNullOrEmpty(tableNameEn))
                    tableNameEn = td.TryGetValue("table_code", out var tv5) ? tv5?.ToString() : "";

                // 读取提取的数据样例（V2 特有）
                var extractedData = new List<Dictionary<string, object>>();
                if (td.TryGetValue("extracted_data", out var edObj) && edObj is IEnumerable<object> edList)
                {
                    foreach (var row in edList)
                    {
                        if (row is IDictionary<string, object> rowDict)
                            extractedData.Add(new Dictionary<string, object>(rowDict));
                    }
                }

                result.Add(new TableDefDto
                {
                    Name = tableNameCn ?? "",
                    NameEn = tableNameEn ?? "",
                    Code = tableNameEn ?? tableNameCn ?? "",
                    Description = td.TryGetValue("description", out var d2) ? d2?.ToString() ?? "" : "",
                    SheetName = sheetName ?? "",
                    Columns = cols,
                    ExtractedData = extractedData
                });
            }
            return result;
        }

        /// <summary>
        /// 将 LlmExtractSkill 输出映射为 ExtractionData（verify 用）。
        /// </summary>
        private static ExtractionData MapOutputsToExtractionData(IDictionary<string, object> outputs)
        {
            var data = new ExtractionData
            {
                Fields = new Dictionary<string, object>(),
                Tables = new Dictionary<string, List<Dictionary<string, object>>>()
            };
            if (outputs.TryGetValue("fields", out var fieldsObj) && fieldsObj is IEnumerable<object> fields)
            {
                foreach (var f in fields)
                {
                    if (f is not IDictionary<string, object> fd) continue;
                    var code = fd.TryGetValue("field_code", out var c) ? c?.ToString() ?? "" : "";
                    var value = fd.TryGetValue("field_value", out var v) ? v : null;
                    if (!string.IsNullOrEmpty(code))
                        data.Fields[code] = value;
                }
            }
            if (outputs.TryGetValue("tables", out var tablesObj) && tablesObj is IEnumerable<object> tables)
            {
                foreach (var t in tables)
                {
                    if (t is not IDictionary<string, object> td) continue;
                    var tableCode = td.TryGetValue("table_code", out var tc) ? tc?.ToString() ?? "" : "";
                    if (string.IsNullOrEmpty(tableCode)) continue;
                    var rows = new List<Dictionary<string, object>>();
                    if (td.TryGetValue("rows", out var rowsObj) && rowsObj is IEnumerable<object> rowsList)
                    {
                        foreach (var row in rowsList)
                        {
                            if (row is IDictionary<string, object> rd)
                                rows.Add(rd.ToDictionary(kv => kv.Key, kv => (object)kv.Value));
                        }
                    }
                    data.Tables[tableCode] = rows;
                }
            }
            return data;
        }

        #endregion
    }
}

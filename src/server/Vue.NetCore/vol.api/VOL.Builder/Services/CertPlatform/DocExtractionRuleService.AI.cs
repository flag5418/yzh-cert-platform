using System;
using System.Collections.Generic;
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
        /// 调用 IFileExtractor 提取文档全文（用于 analyze / verify）。
        /// </summary>
        private async Task<string> ExtractDocumentContentAsync(StandardDirectoryFile fileInfo, string skill)
        {
            if (fileInfo == null) return string.Empty;
            var storagePath = fileInfo.ConvertedStoragePath ?? fileInfo.StoragePath;
            if (string.IsNullOrWhiteSpace(storagePath)) return string.Empty;

            var extractor = AutofacContainerModule.GetService<YZH.Core.Extractor.IFileExtractor>();
            if (extractor == null) return string.Empty;

            try
            {
                var result = await extractor.ExtractAsync(storagePath);
                return result.FullText ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// 调用 WorkflowEngine + LlmExtractSkill 做 AI 字段/表格推荐（analyze 模式）。
        /// </summary>
        private async Task<AIAnalyzeResponse> CallAIForAnalysisAsync(string docContent, string skill)
        {
            if (string.IsNullOrWhiteSpace(docContent))
                return new AIAnalyzeResponse { Fields = new(), Tables = new(), Message = "文档为空" };

            var workflowEngine = AutofacContainerModule.GetService<IWorkflowEngine>();
            if (workflowEngine == null)
                return new AIAnalyzeResponse { Fields = new(), Tables = new(), Message = "工作流引擎未注册" };

            var analyzePrompt = BuildAnalysisPrompt(skill);
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
        private async Task<ExtractionData> CallAIForExtractionAsync(string docContent, string prompt)
        {
            if (string.IsNullOrWhiteSpace(docContent) || string.IsNullOrWhiteSpace(prompt))
                return new ExtractionData { Fields = new(), Tables = new() };

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
        /// 构建 AI 分析提示词（analyze 模式）：推荐字段和表格。
        /// </summary>
        private static string BuildAnalysisPrompt(string skill)
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
        /// 将 LlmExtractSkill 输出映射为 AIAnalyzeResponse 的字段 DTO 列表。
        /// </summary>
        private static List<FieldDefDto> MapAiFieldsToDtos(IDictionary<string, object> outputs)
        {
            var result = new List<FieldDefDto>();
            if (!outputs.TryGetValue("fields", out var fieldsObj) || fieldsObj is not IEnumerable<object> fields)
                return result;
            foreach (var f in fields)
            {
                if (f is not IDictionary<string, object> fd) continue;
                result.Add(new FieldDefDto
                {
                    Name = fd.TryGetValue("field_code", out var code) ? code?.ToString() ?? "" : "",
                    Code = fd.TryGetValue("field_code", out var c2) ? c2?.ToString() ?? "" : "",
                    DataType = fd.TryGetValue("field_type", out var t) ? t?.ToString() ?? "string" : "string",
                    Description = fd.TryGetValue("description", out var d) ? d?.ToString() ?? "" : "",
                    IsManual = false
                });
            }
            return result;
        }

        /// <summary>
        /// 将 LlmExtractSkill 输出映射为 AIAnalyzeResponse 的表格 DTO 列表。
        /// </summary>
        private static List<TableDefDto> MapAiTablesToDtos(IDictionary<string, object> outputs)
        {
            var result = new List<TableDefDto>();
            if (!outputs.TryGetValue("tables", out var tablesObj) || tablesObj is not IEnumerable<object> tables)
                return result;
            foreach (var t in tables)
            {
                if (t is not IDictionary<string, object> td) continue;
                var cols = new List<TableColumnDto>();
                if (td.TryGetValue("columns", out var colsObj) && colsObj is IEnumerable<object> colsList)
                {
                    foreach (var c in colsList)
                    {
                        if (c is not IDictionary<string, object> cd) continue;
                        cols.Add(new TableColumnDto
                        {
                            Name = cd.TryGetValue("column_name", out var n) ? n?.ToString() ?? "" : "",
                            Code = cd.TryGetValue("column_code", out var c2) ? c2?.ToString() ?? "" : "",
                            DataType = cd.TryGetValue("column_type", out var tp) ? tp?.ToString() ?? "string" : "string"
                        });
                    }
                }
                result.Add(new TableDefDto
                {
                    Name = td.TryGetValue("table_name", out var n2) ? n2?.ToString() ?? "" : "",
                    Code = td.TryGetValue("table_code", out var c3) ? c3?.ToString() ?? "" : "",
                    Description = td.TryGetValue("description", out var d2) ? d2?.ToString() ?? "" : "",
                    Columns = cols
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

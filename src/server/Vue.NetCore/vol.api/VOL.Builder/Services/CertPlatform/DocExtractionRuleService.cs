using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using VOL.Core.Enums;
using VOL.Core.Extensions;
using VOL.Core.BaseProvider;
using VOL.Core.Extensions.AutofacManager;
using VOL.Core.Utilities;
using VOL.Entity.CertPlatform.DocExtraction;
using VOL.Entity.CertPlatform.DocExtraction.DTOs;
using VOL.Builder.IRepositories.CertPlatform;
using VOL.Builder.IServices.CertPlatform;

namespace VOL.Builder.Services.CertPlatform
{
    public partial class DocExtractionRuleService : ServiceBase<CertDocExtractionRule, ICertDocExtractionRuleRepository>
    , IDocExtractionRuleService, IDependency
    {
        [ActivatorUtilitiesConstructor]
        public DocExtractionRuleService(ICertDocExtractionRuleRepository repository)
            : base(repository)
        {
        }

        public static IDocExtractionRuleService Instance
        {
            get { return AutofacContainerModule.GetService<IDocExtractionRuleService>(); }
        }

        // 技能定义
        private static readonly List<SkillInfo> _skills = new List<SkillInfo>
        {
            new SkillInfo
            {
                Code = "word",
                Name = "Word文档提取",
                Description = "使用NPOI提取Word文档内容",
                SupportedExtensions = new List<string> { ".docx", ".doc" }
            },
            new SkillInfo
            {
                Code = "excel",
                Name = "Excel表格提取",
                Description = "使用NPOI提取Excel表格数据",
                SupportedExtensions = new List<string> { ".xlsx", ".xls", ".csv" }
            },
            new SkillInfo
            {
                Code = "pdf",
                Name = "PDF文档提取",
                Description = "提取PDF文档文本内容",
                SupportedExtensions = new List<string> { ".pdf" }
            }
        };

        /// <summary>
        /// AI自动分析文档
        /// </summary>
        public async Task<AIAnalyzeResponse> AIAnalyzeAsync(AIAnalyzeRequest request)
        {
            // 1. 获取文件信息
            var fileInfo = await GetFileInfoAsync(request.FileCode);
            if (string.IsNullOrEmpty(fileInfo.FileName))
            {
                throw new Exception("文件不存在");
            }

            // 2. 根据技能类型提取文档内容（结构化）
            var extraction = await ExtractDocumentContentAsync(fileInfo, request.Skill);

            // 3. 调用AI分析
            var aiResult = await CallAIForAnalysisAsync(extraction, request.Skill);

            return new AIAnalyzeResponse
            {
                Fields = aiResult.Fields,
                Tables = aiResult.Tables,
                // 透传提取层消息（如“转换中/转换失败/不支持的文件类型”），正常完成时保留“AI分析完成”
                Message = string.IsNullOrEmpty(aiResult.Message) ? "AI分析完成" : aiResult.Message
            };
        }

        /// <summary>
        /// 生成提取Prompt（结构化格式，只纳入 AI 推荐字段/表格，手动添加的字段不纳入 Prompt）
        /// </summary>
        public async Task<string> GeneratePromptAsync(GeneratePromptRequest request)
        {
            var promptBuilder = new System.Text.StringBuilder();

            promptBuilder.AppendLine("# 文档数据提取任务");
            promptBuilder.AppendLine();
            promptBuilder.AppendLine("你是一位专业的文档信息提取助手。请从以下文档内容中提取指定的字段和表格信息。");
            promptBuilder.AppendLine();

            // 只纳入 AI 推荐的字段（IsAiRecommended=true），手动添加的字段由审核员后续手动填写
            var aiFields = request.Fields?.Where(f => f.IsAiRecommended).ToList() ?? new List<FieldDefDto>();
            var aiTables = request.Tables?.Where(t => t.IsAiRecommended).ToList() ?? new List<TableDefDto>();

            // 字段提取说明（表格化展示，清晰易读）
            if (aiFields.Any())
            {
                promptBuilder.AppendLine("## 需要提取的字段");
                promptBuilder.AppendLine();
                promptBuilder.AppendLine("请从文档中提取以下字段，每个字段需按指定的英文名称（field_code）输出：");
                promptBuilder.AppendLine();
                promptBuilder.AppendLine("| 序号 | 字段名称(中文) | 英文名称(field_code) | 数据类型 | 是否必填 | 描述 |");
                promptBuilder.AppendLine("|------|---------------|---------------------|---------|---------|------|");
                for (int i = 0; i < aiFields.Count; i++)
                {
                    var field = aiFields[i];
                    var code = !string.IsNullOrEmpty(field.NameEn) ? field.NameEn : (!string.IsNullOrEmpty(field.Code) ? field.Code : field.Name);
                    promptBuilder.AppendLine($"| {i + 1} | {field.Name} | {code} | {field.DataType} | {(field.IsRequired ? "是" : "否")} | {field.Description} |");
                }
                promptBuilder.AppendLine();
            }

            // 表格提取说明（每个表格一个结构化块）
            if (aiTables.Any())
            {
                promptBuilder.AppendLine("## 需要提取的表格");
                promptBuilder.AppendLine();
                promptBuilder.AppendLine("请从文档中提取以下表格数据，每个表格的列需按指定的英文名称（column_code）输出：");
                promptBuilder.AppendLine();

                for (int i = 0; i < aiTables.Count; i++)
                {
                    var table = aiTables[i];
                    var tableCode = !string.IsNullOrEmpty(table.NameEn) ? table.NameEn : (!string.IsNullOrEmpty(table.Code) ? table.Code : table.Name);

                    promptBuilder.AppendLine($"### 表格 {i + 1}：{table.Name}");
                    promptBuilder.AppendLine($"- 英文名称(table_code)：{tableCode}");
                    if (!string.IsNullOrEmpty(table.Description))
                        promptBuilder.AppendLine($"- 描述：{table.Description}");
                    promptBuilder.AppendLine();

                    if (table.Columns?.Any() == true)
                    {
                        promptBuilder.AppendLine("| 序号 | 列名称(中文) | 英文名称(column_code) | 数据类型 | 是否必填 |");
                        promptBuilder.AppendLine("|------|-------------|----------------------|---------|---------|");
                        for (int j = 0; j < table.Columns.Count; j++)
                        {
                            var col = table.Columns[j];
                            var colCode = !string.IsNullOrEmpty(col.NameEn) ? col.NameEn : (!string.IsNullOrEmpty(col.Code) ? col.Code : col.Name);
                            promptBuilder.AppendLine($"| {j + 1} | {col.Name} | {colCode} | {col.DataType} | {(col.IsRequired ? "是" : "否")} |");
                        }
                        promptBuilder.AppendLine();
                    }
                }
            }

            promptBuilder.AppendLine("## 输出格式要求");
            promptBuilder.AppendLine();
            promptBuilder.AppendLine("请严格按照以下 JSON 格式返回提取结果（不要输出任何解释文字）：");
            promptBuilder.AppendLine();
            promptBuilder.AppendLine("```json");
            promptBuilder.AppendLine("{");
            promptBuilder.AppendLine("  \"fields\": [");
            promptBuilder.AppendLine("    {\"field_code\": \"company_name\", \"field_value\": \"北京某某科技有限公司\"},");
            promptBuilder.AppendLine("    {\"field_code\": \"cert_date\", \"field_value\": \"2026-08-14\"}");
            promptBuilder.AppendLine("  ],");
            promptBuilder.AppendLine("  \"tables\": [");
            promptBuilder.AppendLine("    {");
            promptBuilder.AppendLine("      \"table_code\": \"shareholder_info\",");
            promptBuilder.AppendLine("      \"rows\": [");
            promptBuilder.AppendLine("        {\"shareholder_name\": \"张三\", \"investment_amount\": 6000000, \"investment_ratio\": 0.6}");
            promptBuilder.AppendLine("      ]");
            promptBuilder.AppendLine("    }");
            promptBuilder.AppendLine("  ]");
            promptBuilder.AppendLine("}");
            promptBuilder.AppendLine("```");
            promptBuilder.AppendLine();
            promptBuilder.AppendLine("**注意事项：**");
            promptBuilder.AppendLine("1. fields 数组中每项必须包含 field_code 和 field_value 两个字段");
            promptBuilder.AppendLine("2. field_code 必须使用上表中的英文名称，区分大小写");
            promptBuilder.AppendLine("3. field_value 为提取到的实际值，无法找到时返回空字符串 \"\"");
            promptBuilder.AppendLine("4. tables 数组中每项必须包含 table_code 和 rows 两个字段");
            promptBuilder.AppendLine("5. rows 中每行的键名必须使用列定义中的英文名称(column_code)");
            promptBuilder.AppendLine("6. 表格如果没有提取到数据，rows 返回空数组 []");
            promptBuilder.AppendLine();

            promptBuilder.AppendLine("## 文档内容");
            promptBuilder.AppendLine();
            promptBuilder.AppendLine("{{document_content}}");

            return promptBuilder.ToString();
        }

        /// <summary>
        /// 验证Prompt
        /// </summary>
        public async Task<VerifyPromptResponse> VerifyPromptAsync(VerifyPromptRequest request)
        {
            try
            {
                // 1. 获取文件信息
                var fileInfo = await GetFileInfoAsync(request.FileCode);
                if (string.IsNullOrEmpty(fileInfo.FileName))
                {
                    return new VerifyPromptResponse
                    {
                        Success = false,
                        Message = "文件不存在"
                    };
                }

                // 2. 获取规则信息（确定技能类型 + 文档内容缓存）
                var rule = await repository
                    .FindAsIQueryable(x => x.FileCode == request.FileCode)
                    .FirstOrDefaultAsync();

                var skill = rule?.Skill ?? "word";

                // 3. 获取文档内容：优先从数据库缓存读取，无缓存则自动提取并存储
                string docContent = null;

                if (rule != null && !string.IsNullOrWhiteSpace(rule.DocContent))
                {
                    // 有缓存，直接使用
                    docContent = rule.DocContent;
                    Console.WriteLine($"[DocExtractionRule] 📄 使用缓存的文档内容 (FileCode={request.FileCode}, length={docContent.Length})");
                }
                else
                {
                    // 无缓存，自动提取文档内容
                    var extraction = await ExtractDocumentContentAsync(fileInfo, skill);

                    // 提取层有明确原因（转换中/失败/不支持）时透传
                    if (!string.IsNullOrEmpty(extraction.Message))
                    {
                        return new VerifyPromptResponse
                        {
                            Success = false,
                            Message = extraction.Message
                        };
                    }

                    if (extraction.Sections.Count == 0)
                    {
                        return new VerifyPromptResponse
                        {
                            Success = false,
                            Message = "文档内容为空或提取失败"
                        };
                    }

                    docContent = BuildStructuredContext(extraction);

                    // 存储到数据库缓存
                    if (rule != null)
                    {
                        rule.DocContent = docContent;
                        rule.ModifyDate = DateTime.Now;
                        await repository.SaveChangesAsync();
                        Console.WriteLine($"[DocExtractionRule] 💾 文档内容已缓存到数据库 (FileCode={request.FileCode}, length={docContent.Length})");
                    }
                }

                // 4. 调用AI执行提取（使用已获取的文档内容）
                var extractionResult = await CallAIForExtractionAsync(docContent, request.Prompt);

                if (!string.IsNullOrEmpty(extractionResult?.Message))
                {
                    return new VerifyPromptResponse
                    {
                        Success = false,
                        Message = extractionResult.Message,
                        Data = extractionResult
                    };
                }

                return new VerifyPromptResponse
                {
                    Success = true,
                    Message = "验证成功",
                    Data = extractionResult
                };
            }
            catch (Exception ex)
            {
                return new VerifyPromptResponse
                {
                    Success = false,
                    Message = $"验证失败: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 保存提取规则
        /// </summary>
        public async Task<bool> SaveExtractionRuleAsync(SaveExtractionRuleRequest request)
        {
            using var transaction = await repository.DbContext.Database.BeginTransactionAsync();
            try
            {
                // 1. 查找或创建规则
                var rule = await repository
                    .FindAsIQueryable(x => x.FileCode == request.FileCode)
                    .FirstOrDefaultAsync();

                if (rule == null)
                {
                    rule = new CertDocExtractionRule
                    {
                        Code = GenerateRuleCode(request.FileCode),
                        FileCode = request.FileCode,
                        CreateDate = DateTime.Now
                    };
                    repository.Add(rule);
                }

                // 2. 更新规则信息
                rule.Skill = request.Skill;
                rule.Prompt = request.Prompt;
                rule.IsValid = request.IsValid;
                rule.Status = request.IsValid ? "configured" : "failed";
                rule.ModifyDate = DateTime.Now;

                await repository.SaveChangesAsync();

                // 3. 删除旧的字段定义（通过rule_code关联）
                var oldFields = await repository.DbContext.Set<CertDocFieldDef>()
                    .Where(x => x.RuleCode == rule.Code)
                    .ToListAsync();
                repository.DbContext.Set<CertDocFieldDef>().RemoveRange(oldFields);

                // 4. 保存新的字段定义
                if (request.Fields?.Any() == true)
                {
                    var sortOrder = 0;
                    foreach (var fieldDto in request.Fields)
                    {
                        var field = new CertDocFieldDef
                        {
                            Code = GenerateFieldCode(rule.Code, fieldDto.Name),
                            RuleCode = rule.Code,
                            FieldName = fieldDto.Name,
                            FieldCode = string.IsNullOrEmpty(fieldDto.Code)
                                ? fieldDto.Name.ToPascalCase()
                                : fieldDto.Code,
                            DataType = fieldDto.DataType,
                            Description = fieldDto.Description,
                            IsManual = fieldDto.IsManual,
                            IsAiRecommended = fieldDto.IsAiRecommended,
                            SortOrder = sortOrder++,
                            CreateDate = DateTime.Now
                        };
                        repository.DbContext.Set<CertDocFieldDef>().Add(field);
                    }
                }

                // 5. 删除旧的表格定义（通过rule_code关联）
                var oldTables = await repository.DbContext.Set<CertDocTableDef>()
                    .Where(x => x.RuleCode == rule.Code)
                    .ToListAsync();
                var oldTableCodes = oldTables.Select(x => x.Code).ToList();

                // 6. 删除旧的表格字段定义（通过table_code关联）
                var oldTableFields = await repository.DbContext.Set<CertDocTableFieldDef>()
                    .Where(x => oldTableCodes.Contains(x.TableCode))
                    .ToListAsync();
                repository.DbContext.Set<CertDocTableFieldDef>().RemoveRange(oldTableFields);
                repository.DbContext.Set<CertDocTableDef>().RemoveRange(oldTables);

                // 7. 保存新的表格定义
                if (request.Tables?.Any() == true)
                {
                    var tableSortOrder = 0;
                    foreach (var tableDto in request.Tables)
                    {
                        var tableCode = GenerateTableCode(rule.Code, tableDto.Name);
                        var table = new CertDocTableDef
                        {
                            Code = tableCode,
                            RuleCode = rule.Code,
                            TableName = tableDto.Name,
                            TableCode = string.IsNullOrEmpty(tableDto.Code)
                                ? tableDto.Name.ToPascalCase()
                                : tableDto.Code,
                            Description = tableDto.Description,
                            SortOrder = tableSortOrder++,
                            CreateDate = DateTime.Now
                        };
                        repository.DbContext.Set<CertDocTableDef>().Add(table);
                        await repository.SaveChangesAsync();

                        // 保存表格字段
                        if (tableDto.Columns?.Any() == true)
                        {
                            var colSortOrder = 0;
                            foreach (var colDto in tableDto.Columns)
                            {
                                var col = new CertDocTableFieldDef
                                {
                                    Code = GenerateTableFieldCode(tableCode, colDto.Name),
                                    TableCode = tableCode,
                                    ColumnName = colDto.Name,
                                    ColumnCode = string.IsNullOrEmpty(colDto.Code)
                                        ? colDto.Name.ToPascalCase()
                                        : colDto.Code,
                                    DataType = colDto.DataType,
                                    SortOrder = colSortOrder++,
                                    CreateDate = DateTime.Now
                                };
                                repository.DbContext.Set<CertDocTableFieldDef>().Add(col);
                            }
                        }
                    }
                }

                await repository.SaveChangesAsync();
                await transaction.CommitAsync();
                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// 获取规则详情
        /// </summary>
        public async Task<RuleDetailResponse> GetRuleDetailAsync(string fileCode)
        {
            var rule = await repository
                .FindAsIQueryable(x => x.FileCode == fileCode)
                .FirstOrDefaultAsync();

            if (rule == null)
            {
                return null;
            }

            // 获取字段定义（通过rule_code关联）
            var fields = await repository.DbContext.Set<CertDocFieldDef>()
                .Where(x => x.RuleCode == rule.Code)
                .OrderBy(x => x.SortOrder)
                .Select(x => new FieldDefDto
                {
                    Name = x.FieldName,
                    Code = x.FieldCode,
                    DataType = x.DataType,
                    Description = x.Description,
                    IsManual = x.IsManual,
                    IsAiRecommended = x.IsAiRecommended
                })
                .ToListAsync();

            // 获取表格定义（通过rule_code关联）
            var tables = await repository.DbContext.Set<CertDocTableDef>()
                .Where(x => x.RuleCode == rule.Code)
                .OrderBy(x => x.SortOrder)
                .ToListAsync();

            var tableDtos = new List<TableDefDto>();
            foreach (var table in tables)
            {
                // 获取表格字段（通过table_code关联）
                var columns = await repository.DbContext.Set<CertDocTableFieldDef>()
                    .Where(x => x.TableCode == table.Code)
                    .OrderBy(x => x.SortOrder)
                    .Select(x => new TableColumnDto
                    {
                        Name = x.ColumnName,
                        Code = x.ColumnCode,
                        DataType = x.DataType
                    })
                    .ToListAsync();

                tableDtos.Add(new TableDefDto
                {
                    Name = table.TableName,
                    Code = table.TableCode,
                    Description = table.Description,
                    Columns = columns
                });
            }

            return new RuleDetailResponse
            {
                Id = rule.Id,
                Code = rule.Code,
                FileCode = rule.FileCode,
                Skill = rule.Skill,
                Prompt = rule.Prompt,
                IsValid = rule.IsValid,
                Status = rule.Status,
                Fields = fields,
                Tables = tableDtos,
                CreateDate = rule.CreateDate,
                ModifyDate = rule.ModifyDate
            };
        }

        /// <summary>
        /// 删除规则
        /// </summary>
        public async Task<bool> DeleteRuleAsync(string fileCode)
        {
            var rule = await repository
                .FindAsIQueryable(x => x.FileCode == fileCode)
                .FirstOrDefaultAsync();

            if (rule == null)
            {
                return false;
            }

            // 级联删除字段和表格定义（通过code关联）
            var fields = await repository.DbContext.Set<CertDocFieldDef>()
                .Where(x => x.RuleCode == rule.Code)
                .ToListAsync();
            repository.DbContext.Set<CertDocFieldDef>().RemoveRange(fields);

            var tables = await repository.DbContext.Set<CertDocTableDef>()
                .Where(x => x.RuleCode == rule.Code)
                .ToListAsync();
            var tableCodes = tables.Select(x => x.Code).ToList();

            var tableFields = await repository.DbContext.Set<CertDocTableFieldDef>()
                .Where(x => tableCodes.Contains(x.TableCode))
                .ToListAsync();
            repository.DbContext.Set<CertDocTableFieldDef>().RemoveRange(tableFields);

            repository.DbContext.Set<CertDocTableDef>().RemoveRange(tables);
            repository.DbContext.Set<CertDocExtractionRule>().Remove(rule);

            await repository.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// 获取AI配置
        /// </summary>
        public async Task<AIConfigDto> GetAIConfigAsync()
        {
            Console.WriteLine("[AIConfig] Starting GetAIConfigAsync...");
            Console.WriteLine("[AIConfig] Repository: " + (repository != null ? "not null" : "NULL"));
            if (repository == null)
            {
                return new AIConfigDto { Provider = "qwen", Model = "qwen-turbo" };
            }
            var config = await repository.DbContext.Set<AIConfig>()
                .Where(x => x.IsEnabled)
                .FirstOrDefaultAsync();
            Console.WriteLine("[AIConfig] Config found: " + (config != null ? "yes" : "no"));

            if (config == null)
            {
                // 返回默认配置
                return new AIConfigDto
                {
                    Provider = "qwen",
                    Model = "qwen-turbo",
                    Temperature = 0.7f,
                    MaxTokens = 4096
                };
            }

            return new AIConfigDto
            {
                Provider = config.Provider,
                ApiKey = config.ApiKey,
                Model = config.Model,
                Temperature = config.Temperature,
                MaxTokens = config.MaxTokens
            };
        }

        /// <summary>
        /// 更新AI配置
        /// </summary>
        public async Task<bool> UpdateAIConfigAsync(AIConfigDto configDto)
        {
            var config = await repository.DbContext.Set<AIConfig>()
                .FirstOrDefaultAsync();

            if (config == null)
            {
                config = new AIConfig
                {
                    Code = "default-ai-config",
                    CreateDate = DateTime.Now
                };
                repository.DbContext.Set<AIConfig>().Add(config);
            }

            config.Provider = configDto.Provider;
            config.ApiKey = configDto.ApiKey;
            config.Model = configDto.Model;
            config.Temperature = configDto.Temperature;
            config.MaxTokens = configDto.MaxTokens;
            config.ModifyDate = DateTime.Now;

            await repository.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// 获取可用技能列表
        /// </summary>
        public List<SkillInfo> GetSkills()
        {
            return _skills;
        }

        #region 私有方法

        /// <summary>
        /// 生成规则编码
        /// </summary>
        private string GenerateRuleCode(string fileCode)
        {
            return $"RULE-{fileCode}-{DateTime.Now:yyyyMMddHHmmss}";
        }

        /// <summary>
        /// 生成字段定义编码
        /// </summary>
        private string GenerateFieldCode(string ruleCode, string fieldName)
        {
            return $"{ruleCode}-FIELD-{fieldName.ToPascalCase()}";
        }

        /// <summary>
        /// 生成表格定义编码
        /// </summary>
        private string GenerateTableCode(string ruleCode, string tableName)
        {
            return $"{ruleCode}-TABLE-{tableName.ToPascalCase()}";
        }

        /// <summary>
        /// 生成表格字段定义编码
        /// </summary>
        private string GenerateTableFieldCode(string tableCode, string columnName)
        {
            return $"{tableCode}-COL-{columnName.ToPascalCase()}";
        }

        #endregion
    }

    /// <summary>
    /// 字符串扩展
    /// </summary>
    public static class StringExtensions
    {
        /// <summary>
        /// 转换为PascalCase
        /// </summary>
        public static string ToPascalCase(this string str)
        {
            if (string.IsNullOrEmpty(str))
                return str;

            // 移除特殊字符，转为PascalCase
            var words = str.Split(new[] { ' ', '_', '-', '（', '）', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);
            var result = string.Concat(words.Select(w =>
                char.ToUpper(w[0]) + w.Substring(1).ToLower()));

            return result;
        }
    }
}

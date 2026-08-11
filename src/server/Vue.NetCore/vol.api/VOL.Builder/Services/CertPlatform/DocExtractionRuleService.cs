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
            if (fileInfo == null)
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
                Message = "AI分析完成"
            };
        }

        /// <summary>
        /// 生成提取Prompt
        /// </summary>
        public async Task<string> GeneratePromptAsync(GeneratePromptRequest request)
        {
            var promptBuilder = new System.Text.StringBuilder();

            promptBuilder.AppendLine("# 文档数据提取任务");
            promptBuilder.AppendLine();
            promptBuilder.AppendLine("请从以下文档内容中提取指定信息：");
            promptBuilder.AppendLine();

            // 字段提取说明
            if (request.Fields?.Any() == true)
            {
                promptBuilder.AppendLine("## 需要提取的字段：");
                foreach (var field in request.Fields)
                {
                    promptBuilder.AppendLine($"- {field.Name} ({field.DataType}): {field.Description}");
                    if (field.IsManual)
                    {
                        promptBuilder.AppendLine($"  [标记为需手动补充]");
                    }
                }
                promptBuilder.AppendLine();
            }

            // 表格提取说明
            if (request.Tables?.Any() == true)
            {
                promptBuilder.AppendLine("## 需要提取的表格：");
                foreach (var table in request.Tables)
                {
                    promptBuilder.AppendLine($"### {table.Name}");
                    promptBuilder.AppendLine($"描述: {table.Description}");
                    if (table.Columns?.Any() == true)
                    {
                        promptBuilder.AppendLine("列定义：");
                        foreach (var col in table.Columns)
                        {
                            promptBuilder.AppendLine($"  - {col.Name} ({col.DataType})");
                        }
                    }
                    promptBuilder.AppendLine();
                }
            }

            promptBuilder.AppendLine("## 输出格式要求：");
            promptBuilder.AppendLine("请以JSON格式返回提取结果：");
            promptBuilder.AppendLine("```json");
            promptBuilder.AppendLine("{");
            promptBuilder.AppendLine("  \"fields\": {");
            promptBuilder.AppendLine("    \"字段名\": \"提取的值\"");
            promptBuilder.AppendLine("  },");
            promptBuilder.AppendLine("  \"tables\": {");
            promptBuilder.AppendLine("    \"表格名\": [");
            promptBuilder.AppendLine("      {\"列名\": \"值\"}");
            promptBuilder.AppendLine("    ]");
            promptBuilder.AppendLine("  }");
            promptBuilder.AppendLine("}");
            promptBuilder.AppendLine("```");

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
                if (fileInfo == null)
                {
                    return new VerifyPromptResponse
                    {
                        Success = false,
                        Message = "文件不存在"
                    };
                }

                // 2. 获取规则信息（确定技能类型）
                var rule = await repository
                    .FindAsIQueryable(x => x.FileCode == request.FileCode)
                    .FirstOrDefaultAsync();

                var skill = rule?.Skill ?? "word";

                // 3. 提取文档内容（结构化）
                var extraction = await ExtractDocumentContentAsync(fileInfo, skill);

                // 4. 调用AI执行提取
                var extractionResult = await CallAIForExtractionAsync(extraction, request.Prompt);

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
                    IsManual = x.IsManual
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

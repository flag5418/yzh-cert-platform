extern alias SharedEntities;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Interfaces;
using SharedDoc = SharedEntities::CertPlatform.Shared.DocExtraction;
using SharedEntities::YZH.Entity.Admin.Platform.Dir;
using SharedEntities::YZH.Entity.Admin.Platform.Doc;

namespace CertPlatform.Admin.Services.DocExtraction;

/// <summary>
/// 文档提取规则服务 — AI 编排（partial）
/// <para>对照旧 DocExtractionRuleService.AI.cs（620 行）：</para>
/// <para>· 业务编排逻辑完整保留（analyze 推荐 / verify 提取 / 试运行 / 缓存 / AI 日志）</para>
/// <para>· 执行器替换：IFileExtractor → DocumentConvertClient（anydoc→Markdown，旧格式直转）</para>
/// <para>·          IWorkflowEngine+LlmExtractSkill → LlmInvokeService（OpenAI 兼容直连）</para>
/// <para>· 上下文载体：旧 BuildStructuredContext 伪文本 → Markdown（GFM 表格行列语义，LLM 原生理解）</para>
/// <para>· 配置链（V1.1 §9.3）：ai_extract_enabled 总开关 → cert_sys_config 六键 → wf_prompt_template</para>
/// </summary>
public partial class DocExtractionRuleService
{
    // 依赖字段声明在主体文件（_convertClient/_llm/_storage/_configuration），此处直接使用

    // ========================================================
    // AI 配置读写（对照旧 GetAIConfigAsync/UpdateAIConfigAsync：cert_ai_config 页面级 + cert_sys_config 兑底）
    // ========================================================

    /// <summary>获取 AI 配置：cert_ai_config 优先（页面级偏好），无记录时返回 cert_sys_config 兑底默认</summary>
    public async Task<AIConfigDto> GetAIConfigAsync()
    {
        var entity = (await _db.GetOneAsync<AiConfig>(x => x.IsEnabled)).Data;
        var settings = await GetAiSettingsAsync();

        if (entity == null)
        {
            // 返回系统参数兑底配置
            return new AIConfigDto
            {
                Provider = settings.Provider,
                ApiKey = settings.ApiKey,
                Model = settings.Model,
                Temperature = settings.Temperature,
                MaxTokens = settings.MaxTokens
            };
        }

        return new AIConfigDto
        {
            Provider = entity.Provider,
            ApiKey = string.IsNullOrEmpty(entity.ApiKey) ? settings.ApiKey : entity.ApiKey,
            Model = entity.Model,
            Temperature = entity.Temperature,
            MaxTokens = entity.MaxTokens
        };
    }

    /// <summary>更新 AI 配置（写 cert_ai_config；模型名权威仍在 cert_sys_config）</summary>
    public async Task<(bool Success, string Message)> UpdateAIConfigAsync(AIConfigDto configDto)
    {
        try
        {
            var entity = (await _db.GetOneAsync<AiConfig>(x => x.Code == "default-ai-config")).Data;
            if (entity == null)
            {
                entity = new AiConfig
                {
                    Code = "default-ai-config",
                    CreateTime = DateTime.Now
                };
            }

            entity.Provider = configDto.Provider;
            entity.ApiKey = configDto.ApiKey;
            entity.Model = configDto.Model;
            entity.Temperature = configDto.Temperature;
            entity.MaxTokens = configDto.MaxTokens;
            entity.UpdateTime = DateTime.Now;

            if (entity.Id == 0)
                await _db.InsertAsync(entity);
            else
                await _db.UpdateAsync(entity);

            return (true, "保存成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DocExtractionRule] AI 配置保存失败");
            return (false, $"保存失败：{ex.Message}");
        }
    }

    // ========================================================
    // AI 配置（系统参数唯一真相源，对照旧 GetActiveModelFromSysConfig 扩展为六键 + 开关）
    // ========================================================

    /// <summary>AI 连接配置（cert_sys_config 六键）</summary>
    private class AiSettings
    {
        public bool Enabled { get; set; } = true;
        public string Provider { get; set; } = "qianwen";
        public string ApiKey { get; set; } = "";
        public string BaseUrl { get; set; } = "https://dashscope.aliyuncs.com/compatible-mode/v1";
        public string Model { get; set; } = "qwen-turbo";
        public int MaxTokens { get; set; } = 4096;
        public float Temperature { get; set; } = 0.7f;
    }

    /// <summary>
    /// 读取 AI 设置。优先级：cert_ai_config（页面级）覆盖 ← cert_sys_config 六键（权威源）
    /// 模型名唯一真相源：cert_sys_config.ai_model_name（对照旧逻辑原样保留）
    /// </summary>
    private async Task<AiSettings> GetAiSettingsAsync()
    {
        var s = new AiSettings();

        var rows = await _db.SqlQueryAsync<ConfigKV>(
            "SELECT ConfigKey, ConfigValue FROM cert_sys_config WHERE Category = 'ai_model' AND IsDeleted = 0");
        foreach (var row in rows.Data ?? new())
        {
            switch (row.ConfigKey)
            {
                case "ai_extract_enabled": s.Enabled = !string.Equals(row.ConfigValue, "false", StringComparison.OrdinalIgnoreCase); break;
                case "ai_provider": s.Provider = row.ConfigValue ?? s.Provider; break;
                case "ai_api_key": s.ApiKey = row.ConfigValue ?? ""; break;
                case "ai_base_url": s.BaseUrl = row.ConfigValue ?? s.BaseUrl; break;
                case "ai_model_name": s.Model = row.ConfigValue ?? s.Model; break;
                case "ai_max_tokens":
                    if (int.TryParse(row.ConfigValue, out var mt)) s.MaxTokens = mt;
                    break;
                case "ai_temperature":
                    if (float.TryParse(row.ConfigValue, out var tp)) s.Temperature = tp;
                    break;
            }
        }
        return s;
    }

    private class ConfigKV
    {
        public string ConfigKey { get; set; } = "";
        public string? ConfigValue { get; set; }
    }

    // ========================================================
    // 文件信息（对照旧 GetFileInfoAsync：模板优先、实际文件兜底）
    // ========================================================

    /// <summary>标准文件信息（用于转换/预览定位）</summary>
    private record FileInfoResult(
        string? FileName, string? StoragePath,
        string? PreviewPdfPath, string? MarkdownPath, string? MarkdownStatus, string? MarkdownMessage);

    /// <summary>
    /// 按 standardFileCode 查询标准文件信息。
    /// 查询顺序：cert_file_requirement（FR-xxx 模板）→ cert_standard_directory_file（FL-xxx 实际文件）
    /// </summary>
    private async Task<FileInfoResult> GetFileInfoAsync(string standardFileCode)
    {
        if (string.IsNullOrEmpty(standardFileCode))
            return new FileInfoResult(null, null, null, null, null, null);

        // 1. 优先：文件要求的模板文件（Code=FR-xxx）
        // ⚠️ 列名以实际表结构为准：本环境 cert_file_requirement 采用新架构 PascalCase 列
        //    （Code/FileNameTemplate/IsValid/IsDeleted…），不存在 template_storage_path / template_file_name，
        //    旧版 snake_case 列名会让该查询持续抛 Unknown column 并刷爆日志。
        //    模板文件存储列缺失时该分支自然降级到「实际标准目录文件」，不影响可用性。
        var fr = (await _db.SqlQueryAsync<FrRow>(
            "SELECT FileNameTemplate, TemplateFileName, TemplateStoragePath FROM cert_file_requirement " +
            "WHERE Code = @c AND IsValid = 1 AND IsDeleted = 0",
            new { c = standardFileCode })).Data?.FirstOrDefault();
        if (fr != null && !string.IsNullOrEmpty(fr.TemplateStoragePath))
            return new FileInfoResult(fr.TemplateFileName ?? fr.FileNameTemplate, fr.TemplateStoragePath, null, null, null, null);

        // 2. 兜底：实际上传的标准目录文件（FileCode=FL-xxx）
        var dir = (await _db.GetOneAsync<StandardDirectoryFile>(x => x.FileCode == standardFileCode)).Data;
        if (dir != null && !string.IsNullOrEmpty(dir.StoragePath))
            return new FileInfoResult(dir.FileName, dir.StoragePath, dir.PreviewPdfPath, dir.MarkdownPath, dir.MarkdownStatus, dir.MarkdownMessage);

        return new FileInfoResult(null, null, null, null, null, null);
    }

    private class FrRow
    {
        public string? TemplateFileName { get; set; }
        public string? TemplateStoragePath { get; set; }
        public string? FileNameTemplate { get; set; }
    }

    // ========================================================
    // 文档内容：→ Markdown（替代旧 ExtractDocumentContentAsync + BuildStructuredContext）
    // ========================================================

    /// <summary>
    /// 获取文档 Markdown 上下文：优先 MinIO 产物 → cert_doc_extraction_rule.doc_content 缓存 → 实时转换
    /// </summary>
    private async Task<(string? Markdown, string? Error)> GetDocumentMarkdownAsync(string fileCode, string fileName)
    {
        // 1. MinIO 产物（转换队列已完成）
        var fileInfo = await GetFileInfoAsync(fileCode);
        if (!string.IsNullOrEmpty(fileInfo.MarkdownPath) && fileInfo.MarkdownStatus == "completed")
        {
            try
            {
                var (stream, _) = await _storage.DownloadAsync(fileInfo.MarkdownPath.TrimStart('/'));
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                var md = Encoding.UTF8.GetString(ms.ToArray());
                if (!string.IsNullOrWhiteSpace(md)) return (md, null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[DocExtractionRule] Markdown 产物读取失败，回退实时转换: {FileCode}", fileCode);
            }
        }

        // 2. 实时转换（anydoc 直转，毫秒级）
        if (string.IsNullOrEmpty(fileInfo.FileName) || string.IsNullOrEmpty(fileInfo.StoragePath))
            return (null, "未找到可分析的文件：请确认该标准已上传模板文件（文件要求），或该文件已上传到标准目录");

        try
        {
            var (stream, _) = await _storage.DownloadAsync(fileInfo.StoragePath.TrimStart('/'));
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);

            var result = await _convertClient.ConvertToMarkdownAsync(fileInfo.FileName, ms.ToArray());
            if (!result.Success || result.Content == null)
                return (null, result.Message);

            return (Encoding.UTF8.GetString(result.Content), null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DocExtractionRule] 实时转换失败: {FileCode}", fileCode);
            return (null, $"文档转换失败：{ex.Message}");
        }
    }

    // ========================================================
    // 控制器辅助：预览/Markdown 产物（D-6）
    // ========================================================

    /// <summary>获取文档 Markdown（file-markdown 端点用）</summary>
    public async Task<(string? Markdown, string? Error)> GetFileMarkdownForPreviewAsync(string fileCode)
    {
        var fileInfo = await GetFileInfoAsync(fileCode);
        if (string.IsNullOrEmpty(fileInfo.FileName))
            return (null, "未找到文件");
        return await GetDocumentMarkdownAsync(fileCode, fileInfo.FileName);
    }

    /// <summary>获取文档预览 PDF 字节（file-preview 端点用；无产物时实时转换）</summary>
    public async Task<(byte[]? Content, string? FileName, string? Error)> GetFilePreviewPdfAsync(string fileCode)
    {
        var fileInfo = await GetFileInfoAsync(fileCode);
        if (string.IsNullOrEmpty(fileInfo.FileName) || string.IsNullOrEmpty(fileInfo.StoragePath))
            return (null, null, "未找到文件");

        // PDF 原样透传
        var ext = Path.GetExtension(fileInfo.FileName).ToLowerInvariant();
        if (ext == ".pdf")
        {
            var (stream, _) = await _storage.DownloadAsync(fileInfo.StoragePath.TrimStart('/'));
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            return (ms.ToArray(), Path.GetFileNameWithoutExtension(fileInfo.FileName), null);
        }

        // 优先已有产物
        if (!string.IsNullOrEmpty(fileInfo.PreviewPdfPath))
        {
            try
            {
                var (stream, _) = await _storage.DownloadAsync(fileInfo.PreviewPdfPath.TrimStart('/'));
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                return (ms.ToArray(), Path.GetFileNameWithoutExtension(fileInfo.FileName), null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[DocExtractionRule] PDF 产物读取失败，回退实时转换: {FileCode}", fileCode);
            }
        }

        // 实时转换（LibreOffice）
        try
        {
            var (stream, _) = await _storage.DownloadAsync(fileInfo.StoragePath.TrimStart('/'));
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            var result = await _convertClient.ConvertToPdfAsync(fileInfo.FileName, ms.ToArray());
            if (!result.Success || result.Content == null)
                return (null, null, result.Message);
            return (result.Content, Path.GetFileNameWithoutExtension(fileInfo.FileName), null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DocExtractionRule] 预览转换失败: {FileCode}", fileCode);
            return (null, null, $"预览转换失败：{ex.Message}");
        }
    }

    // ========================================================
    // AI 分析（analyze 模式，对照旧 AIAnalyzeAsync + CallAIForAnalysisAsync）
    // ========================================================

    /// <summary>
    /// AI 自动分析文档：提取 Markdown 上下文 → LLM 推荐字段/表格
    /// </summary>
    public async Task<AIAnalyzeResponse> AIAnalyzeAsync(AIAnalyzeRequest request)
    {
        var settings = await GetAiSettingsAsync();
        if (!settings.Enabled)
            return new AIAnalyzeResponse
            {
                Message = "AI 提取未启用（系统参数 ai_extract_enabled=false），请手动定义字段与表格"
            };

        // 1. 文件名（推导 skill + 提示词选择）
        var fileInfo = await GetFileInfoAsync(request.FileCode);
        if (string.IsNullOrEmpty(fileInfo.FileName))
            return new AIAnalyzeResponse { Message = "未找到可分析的文件：请确认该标准已上传模板文件（文件要求），或该文件已上传到标准目录" };

        var skill = ResolveSkill(fileInfo.FileName);

        // 2. Markdown 上下文
        var (markdown, error) = await GetDocumentMarkdownAsync(request.FileCode, fileInfo.FileName);
        if (error != null) return new AIAnalyzeResponse { Message = error };
        if (string.IsNullOrWhiteSpace(markdown))
            return new AIAnalyzeResponse { Message = "文档内容为空或转换失败" };

        // 3. 提示词（DB 模板 analyze_{skill} 优先，回退内嵌默认）
        var analyzePrompt = await BuildAnalysisPromptAsync(skill);

        // 4. LLM 调用
        var llmResult = await _llm.CompleteAsync(new SharedDoc::LlmInvokeRequest
        {
            BaseUrl = settings.BaseUrl,
            ApiKey = settings.ApiKey,
            Model = settings.Model,
            Temperature = settings.Temperature,
            MaxTokens = settings.MaxTokens,
            Prompt = analyzePrompt,
            DocumentContent = markdown,
            ForceJson = true
        });

        await LogAiUsageAsync(_db, _logger, "analyze", skill, request.FileCode, settings, llmResult);

        if (!llmResult.Success || llmResult.Json == null)
            return new AIAnalyzeResponse { Message = string.IsNullOrEmpty(llmResult.Message) ? "AI 分析失败" : llmResult.Message };

        // 5. 输出映射（V2/V1 格式兼容，对照旧 MapAiFieldsToDtos/MapAiTablesToDtos）
        var fields = MapAiFieldsToDtos(llmResult.Json.RootElement);
        var tables = MapAiTablesToDtos(llmResult.Json.RootElement);

        var msg = "AI分析完成";
        if (fields.Count == 0 && tables.Count == 0)
            msg = "AI 未识别到可推荐的字段/表格，请手动定义";

        return new AIAnalyzeResponse { Fields = fields, Tables = tables, Message = msg };
    }

    // ========================================================
    // Prompt 验证（verify 模式，对照旧 VerifyPromptAsync L219-352）
    // ========================================================

    /// <summary>
    /// 验证 Prompt：按用户 Prompt 实际提取 → sample_data 落库 → status=configured
    /// </summary>
    public async Task<VerifyPromptResponse> VerifyPromptAsync(VerifyPromptRequest request)
    {
        try
        {
            var settings = await GetAiSettingsAsync();
            if (!settings.Enabled)
                return new VerifyPromptResponse { Success = false, Message = "AI 提取未启用（系统参数 ai_extract_enabled=false）" };

            // 1. 规则（含 doc_content 缓存定位）
            var rule = (await _db.GetOneAsync<DocExtractionRule>(x => x.StandardFileCode == request.FileCode)).Data;
            var skill = rule?.Skill ?? "word";

            // 2. 文档内容：优先规则缓存，无缓存则提取
            string docContent;
            if (rule != null && !string.IsNullOrWhiteSpace(rule.DocContent))
            {
                docContent = rule.DocContent;
                _logger.LogInformation("[DocExtractionRule] 使用缓存的文档内容 (FileCode={FC}, len={Len})", request.FileCode, docContent.Length);
            }
            else
            {
                var (markdown, error) = await GetDocumentMarkdownAsync(request.FileCode, request.FileCode);
                if (error != null) return new VerifyPromptResponse { Success = false, Message = error };
                docContent = markdown ?? "";

                // 缓存到规则（对照旧逻辑：避免重复验证时重复转换）
                if (rule != null)
                {
                    rule.DocContent = docContent;
                    rule.UpdateTime = DateTime.Now;
                    await _db.UpdateAsync(rule);
                    _logger.LogInformation("[DocExtractionRule] 文档内容已缓存 (FileCode={FC}, len={Len})", request.FileCode, docContent.Length);
                }
            }

            // 3. AI 提取
            var llmResult = await _llm.CompleteAsync(new SharedDoc::LlmInvokeRequest
            {
                BaseUrl = settings.BaseUrl,
                ApiKey = settings.ApiKey,
                Model = settings.Model,
                Temperature = settings.Temperature,
                MaxTokens = settings.MaxTokens,
                Prompt = request.Prompt,
                DocumentContent = docContent,
                ForceJson = true
            });

            await LogAiUsageAsync(_db, _logger, "verify", skill, request.FileCode, settings, llmResult);

            if (!llmResult.Success)
                return new VerifyPromptResponse { Success = false, Message = llmResult.Message, Data = new ExtractionData { Message = llmResult.Message } };

            var extraction = MapOutputsToExtractionData(llmResult.Json!.RootElement);
            if (extraction == null)
                return new VerifyPromptResponse { Success = false, Message = "AI 返回内容无法解析为提取结果" };

            // 映射后为空：AI 有返回但不符合提取格式（或文档确实无可提取内容）
            // → 明确报错而不是静默「验证成功 + 空结果」，否则前端只看到空白
            var extractedCount = (extraction.Fields?.Count ?? 0) + (extraction.Tables?.Count ?? 0);
            if (extractedCount == 0)
                return new VerifyPromptResponse
                {
                    Success = false,
                    Message = "AI 未提取到任何字段或表格：请确认提示词为「文档数据提取任务」格式（field_code/field_value），或所选模板带 extracted_value 格式；也可先执行「自动分析」生成提示词",
                    Data = extraction
                };

            // 4. 验证成功：sample_data 落库（供工作流 test-field/test-table 使用）
            if (rule != null && extraction != null)
            {
                var sampleDict = new Dictionary<string, object>();
                if (extraction.Fields != null)
                    foreach (var kv in extraction.Fields)
                        if (!string.IsNullOrEmpty(kv.Key) && kv.Value != null)
                            sampleDict[kv.Key] = kv.Value;
                if (extraction.Tables != null)
                    foreach (var kv in extraction.Tables)
                        if (!string.IsNullOrEmpty(kv.Key) && kv.Value != null)
                            sampleDict[kv.Key] = kv.Value;

                rule.SampleData = JsonSerializer.Serialize(sampleDict, JsonOptions);
                rule.DocIsValid = true;
                rule.VerifyMessage = "验证成功";
                rule.Status = sampleDict.Count > 0 ? "configured" : rule.Status;
                rule.UpdateTime = DateTime.Now;
                await _db.UpdateAsync(rule);
            }

            return new VerifyPromptResponse { Success = true, Message = "验证成功", Data = extraction };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DocExtractionRule] 验证失败: {FileCode}", request.FileCode);
            return new VerifyPromptResponse { Success = false, Message = $"验证失败: {ex.Message}" };
        }
    }

    // ========================================================
    // Prompt 生成（对照旧 GeneratePromptAsync L97-218：结构化提取任务格式，完整保留）
    // ========================================================

    /// <summary>
    /// 生成提取 Prompt（结构化格式，只纳入 AI 推荐字段/表格，手动添加的字段不纳入）
    /// </summary>
    public Task<string> GeneratePromptAsync(GeneratePromptRequest request)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# 文档数据提取任务");
        sb.AppendLine();
        sb.AppendLine("你是一位专业的文档信息提取助手。请从以下文档内容中提取指定的字段和表格信息。");
        sb.AppendLine();

        // 只纳入 AI 推荐的字段（IsAiRecommended=true），手动添加的字段由后续手动填写
        var aiFields = request.Fields?.Where(f => f.IsAiRecommended).ToList() ?? new();
        var aiTables = request.Tables?.Where(t => t.IsAiRecommended).ToList() ?? new();

        if (aiFields.Any())
        {
            sb.AppendLine("## 需要提取的字段");
            sb.AppendLine();
            sb.AppendLine("请从文档中提取以下字段，每个字段需按指定的英文名称（field_code）输出：");
            sb.AppendLine();
            sb.AppendLine("| 序号 | 字段名称(中文) | 英文名称(field_code) | 数据类型 | 是否必填 | 描述 |");
            sb.AppendLine("|------|---------------|---------------------|---------|---------|------|");
            for (int i = 0; i < aiFields.Count; i++)
            {
                var field = aiFields[i];
                var code = !string.IsNullOrEmpty(field.NameEn) ? field.NameEn : (!string.IsNullOrEmpty(field.Code) ? field.Code : field.Name);
                sb.AppendLine($"| {i + 1} | {field.Name} | {code} | {field.DataType} | {(field.IsRequired ? "是" : "否")} | {field.Description} |");
            }
            sb.AppendLine();
        }

        if (aiTables.Any())
        {
            sb.AppendLine("## 需要提取的表格");
            sb.AppendLine();
            sb.AppendLine("请从文档中提取以下表格数据，每个表格的列需按指定的英文名称（column_code）输出：");
            sb.AppendLine();

            for (int i = 0; i < aiTables.Count; i++)
            {
                var table = aiTables[i];
                var tableCode = !string.IsNullOrEmpty(table.NameEn) ? table.NameEn : (!string.IsNullOrEmpty(table.Code) ? table.Code : table.Name);

                sb.AppendLine($"### 表格 {i + 1}：{table.Name}");
                sb.AppendLine($"- 英文名称(table_code)：{tableCode}");
                if (!string.IsNullOrEmpty(table.Description))
                    sb.AppendLine($"- 描述：{table.Description}");
                sb.AppendLine();

                if (table.Columns?.Any() == true)
                {
                    sb.AppendLine("| 序号 | 列名称(中文) | 英文名称(column_code) | 数据类型 | 是否必填 |");
                    sb.AppendLine("|------|-------------|----------------------|---------|---------|");
                    for (int j = 0; j < table.Columns.Count; j++)
                    {
                        var col = table.Columns[j];
                        var colCode = !string.IsNullOrEmpty(col.NameEn) ? col.NameEn : (!string.IsNullOrEmpty(col.Code) ? col.Code : col.Name);
                        sb.AppendLine($"| {j + 1} | {col.Name} | {colCode} | {col.DataType} | {(col.IsRequired ? "是" : "否")} |");
                    }
                    sb.AppendLine();
                }
            }
        }

        sb.AppendLine("## 输出格式要求");
        sb.AppendLine();
        sb.AppendLine("请严格按照以下 JSON 格式返回提取结果（不要输出任何解释文字）：");
        sb.AppendLine();
        sb.AppendLine("```json");
        sb.AppendLine("{");
        sb.AppendLine("  \"fields\": [");
        sb.AppendLine("    {\"field_code\": \"company_name\", \"field_value\": \"北京某某科技有限公司\"},");
        sb.AppendLine("    {\"field_code\": \"cert_date\", \"field_value\": \"2026-08-14\"}");
        sb.AppendLine("  ],");
        sb.AppendLine("  \"tables\": [");
        sb.AppendLine("    {");
        sb.AppendLine("      \"table_code\": \"shareholder_info\",");
        sb.AppendLine("      \"rows\": [");
        sb.AppendLine("        {\"shareholder_name\": \"张三\", \"investment_amount\": 6000000, \"investment_ratio\": 0.6}");
        sb.AppendLine("      ]");
        sb.AppendLine("    }");
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("**注意事项：**");
        sb.AppendLine("1. fields 数组中每项必须包含 field_code 和 field_value 两个字段");
        sb.AppendLine("2. field_code 必须使用上表中的英文名称，区分大小写");
        sb.AppendLine("3. field_value 为提取到的实际值，无法找到时返回空字符串 \"\"");
        sb.AppendLine("4. tables 数组中每项必须包含 table_code 和 rows 两个字段");
        sb.AppendLine("5. rows 中每行的键名必须使用列定义中的英文名称(column_code)");
        sb.AppendLine("6. 表格如果没有提取到数据，rows 返回空数组 []");
        sb.AppendLine();

        sb.AppendLine("## 文档内容");
        sb.AppendLine();
        sb.AppendLine("{document_content}");

        return Task.FromResult(sb.ToString());
    }

    // ========================================================
    // 配置期试运行（对照旧 TestFieldAsync L915 / TestTableAsync L990：sample_data 驱动）
    // ========================================================

    /// <summary>测试字段提取（标准文档模式：从 sample_data 取值）</summary>
    public async Task<TestFieldResponse> TestFieldAsync(TestFieldRequest request)
    {
        var rule = (await _db.GetOneAsync<DocExtractionRule>(x => x.Code == request.RuleCode)).Data;
        if (rule == null)
            return new TestFieldResponse { FieldCode = request.FieldCode, Message = "规则不存在" };

        var field = (await _db.GetListAsync<DocFieldDef>(x => x.RuleCode == request.RuleCode && x.FieldCode == request.FieldCode)).Data?.FirstOrDefault();
        if (field == null)
            return new TestFieldResponse { FieldCode = request.FieldCode, Message = $"字段 {request.FieldCode} 不存在" };

        if (string.IsNullOrWhiteSpace(rule.SampleData))
            return new TestFieldResponse { FieldCode = request.FieldCode, Message = "该规则尚未执行验证，请先在文档提取规则页面执行验证" };

        try
        {
            using var sample = JsonDocument.Parse(rule.SampleData);
            if (sample.RootElement.ValueKind == JsonValueKind.Object &&
                sample.RootElement.TryGetProperty(request.FieldCode, out var fieldValue))
            {
                var actual = ConvertJsonElement(fieldValue);
                return new TestFieldResponse
                {
                    FieldCode = request.FieldCode,
                    Value = actual,
                    Confidence = 1.0,
                    Message = "提取成功（来源：sample_data）"
                };
            }
            return new TestFieldResponse { FieldCode = request.FieldCode, Message = $"sample_data 中无字段 {request.FieldCode} 的值" };
        }
        catch (Exception ex)
        {
            return new TestFieldResponse { FieldCode = request.FieldCode, Message = $"解析失败：{ex.Message}" };
        }
    }

    /// <summary>测试表格提取（标准文档模式：从 sample_data 取行数据）</summary>
    public async Task<TestTableResponse> TestTableAsync(TestTableRequest request)
    {
        var rule = (await _db.GetOneAsync<DocExtractionRule>(x => x.Code == request.RuleCode)).Data;
        if (rule == null)
            return new TestTableResponse { TableCode = request.TableCode, Message = "规则不存在" };

        var table = (await _db.GetListAsync<DocTableDef>(x => x.RuleCode == request.RuleCode && x.TableCode == request.TableCode)).Data?.FirstOrDefault();
        if (table == null)
            return new TestTableResponse { TableCode = request.TableCode, Message = $"表格 {request.TableCode} 不存在" };

        if (string.IsNullOrWhiteSpace(rule.SampleData))
            return new TestTableResponse { TableCode = request.TableCode, Message = "该规则尚未执行验证，请先在文档提取规则页面执行验证" };

        try
        {
            using var sample = JsonDocument.Parse(rule.SampleData);
            if (sample.RootElement.ValueKind == JsonValueKind.Object &&
                sample.RootElement.TryGetProperty(request.TableCode, out var rowsValue) &&
                rowsValue.ValueKind == JsonValueKind.Array)
            {
                var rows = new List<Dictionary<string, object>>();
                foreach (var row in rowsValue.EnumerateArray())
                {
                    var dict = new Dictionary<string, object>();
                    foreach (var prop in row.EnumerateObject())
                        dict[prop.Name] = ConvertJsonElement(prop.Value) ?? "";
                    rows.Add(dict);
                }
                return new TestTableResponse { TableCode = request.TableCode, Rows = rows, Confidence = 1.0, Message = "提取成功（来源：sample_data）" };
            }
            return new TestTableResponse { TableCode = request.TableCode, Message = $"sample_data 中无表格 {request.TableCode} 的数据" };
        }
        catch (Exception ex)
        {
            return new TestTableResponse { TableCode = request.TableCode, Message = $"解析失败：{ex.Message}" };
        }
    }

    // ========================================================
    // 输出映射（对照旧 MapAiFieldsToDtos / MapAiTablesToDtos / MapOutputsToExtractionData）
    // ========================================================

    /// <summary>
    /// AI 输出 → 字段 DTO（V2：field_name_cn/en/extracted_value；兼容 V1：field_code/name。
    /// V2 模式丢弃未提取到实际值的字段，避免把文档中不存在的字段列入规则）
    /// </summary>
    private static List<FieldDefDto> MapAiFieldsToDtos(JsonElement root)
    {
        var result = new List<FieldDefDto>();
        if (!root.TryGetProperty("fields", out var fields) || fields.ValueKind != JsonValueKind.Array)
            return result;

        var fieldsList = fields.EnumerateArray().ToList();
        var usesV2 = fieldsList.Any(fd =>
            (fd.ValueKind == JsonValueKind.Object) &&
            (fd.TryGetProperty("extracted_value", out _) || fd.TryGetProperty("field_name_cn", out _)));

        foreach (var fd in fieldsList)
        {
            if (fd.ValueKind != JsonValueKind.Object) continue;

            var nameCn = GetString(fd, "field_name_cn") ?? GetString(fd, "field_name") ?? "";
            var nameEn = GetString(fd, "field_name_en") ?? GetString(fd, "field_code") ?? "";
            var extractedValue = GetString(fd, "extracted_value");
            var isRequired = GetBool(fd, "is_required");

            // V2 模式：只保留有实际提取值的字段
            if (usesV2 && string.IsNullOrWhiteSpace(extractedValue)) continue;

            result.Add(new FieldDefDto
            {
                Name = nameCn,
                NameEn = nameEn,
                Code = string.IsNullOrEmpty(nameEn) ? nameCn : nameEn,
                DataType = GetString(fd, "field_type") ?? "string",
                Description = GetString(fd, "description") ?? "",
                IsRequired = isRequired,
                IsManual = false,
                ExtractedValue = extractedValue ?? ""
            });
        }
        return result;
    }

    /// <summary>
    /// AI 输出 → 表格 DTO（V2：table_name_cn/en/extracted_data；兼容 V1。
    /// V2 模式丢弃没有真实提取数据的表格）
    /// </summary>
    private static List<TableDefDto> MapAiTablesToDtos(JsonElement root)
    {
        var result = new List<TableDefDto>();
        if (!root.TryGetProperty("tables", out var tables) || tables.ValueKind != JsonValueKind.Array)
            return result;

        var tablesList = tables.EnumerateArray().ToList();
        var usesV2 = tablesList.Any(td =>
            (td.ValueKind == JsonValueKind.Object) &&
            (td.TryGetProperty("extracted_data", out _) || td.TryGetProperty("table_name_cn", out _)));

        foreach (var td in tablesList)
        {
            if (td.ValueKind != JsonValueKind.Object) continue;

            var tableNameCn = GetString(td, "table_name_cn") ?? GetString(td, "table_name") ?? "";
            var tableNameEn = GetString(td, "table_name_en") ?? GetString(td, "table_code") ?? "";

            var cols = new List<TableColumnDto>();
            if (td.TryGetProperty("columns", out var colsEl) && colsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var cd in colsEl.EnumerateArray())
                {
                    if (cd.ValueKind != JsonValueKind.Object) continue;
                    var colNameCn = GetString(cd, "column_name_cn") ?? GetString(cd, "column_name") ?? "";
                    var colNameEn = GetString(cd, "column_name_en") ?? GetString(cd, "column_code") ?? "";
                    cols.Add(new TableColumnDto
                    {
                        Name = colNameCn,
                        NameEn = colNameEn,
                        Code = string.IsNullOrEmpty(colNameEn) ? colNameCn : colNameEn,
                        DataType = GetString(cd, "column_type") ?? "string",
                        IsRequired = GetBool(cd, "column_is_required") || GetBool(cd, "is_required")
                    });
                }
            }

            // 提取的数据样例（V2 特有）
            var extractedData = new List<Dictionary<string, object>>();
            if (td.TryGetProperty("extracted_data", out var ed) && ed.ValueKind == JsonValueKind.Array)
            {
                foreach (var row in ed.EnumerateArray())
                {
                    if (row.ValueKind != JsonValueKind.Object) continue;
                    var dict = new Dictionary<string, object>();
                    foreach (var prop in row.EnumerateObject())
                        dict[prop.Name] = ConvertJsonElement(prop.Value) ?? "";
                    extractedData.Add(dict);
                }
            }

            // V2 模式：只保留有真实提取数据的表格
            if (usesV2 && extractedData.Count == 0) continue;

            result.Add(new TableDefDto
            {
                Name = tableNameCn,
                NameEn = tableNameEn,
                Code = string.IsNullOrEmpty(tableNameEn) ? tableNameCn : tableNameEn,
                Description = GetString(td, "description") ?? "",
                SheetName = GetString(td, "sheet_name") ?? "",
                Columns = cols,
                ExtractedData = extractedData
            });
        }
        return result;
    }

    /// <summary>
    /// AI 输出 → ExtractionData（verify 用）。
    /// <para>支持两种提示词格式（与 analyze 的 MapAiFieldsToDtos / MapAiTablesToDtos 对齐）：</para>
    /// <para>· V1（GeneratePromptAsync 生成的“提取任务”格式）：
    ///   fields[].{field_code, field_value} / tables[].{table_code, rows}</para>
    /// <para>· V2（wf_prompt_template 分析模板，如 analyze_word / extract_all）：
    ///   fields[].{field_name_cn, field_name_en, extracted_value} /
    ///   tables[].{table_name_cn, table_name_en, columns, extracted_data}</para>
    /// <para>⚠️ 只认 V1 时，选“文档结构分析”这类模板会让提取结果整片丢失（映射后为空），
    /// 表现为“点了分析没有任何字段/表格”。</para>
    /// <para>兜底：fields 为对象时按「键→值」直接透传（中文名 → 值）。</para>
    /// </summary>
    /// <summary>判断 JSON 值是否含实际内容（V2 空值/空串视为未提取到）</summary>
    private static bool HasJsonValue(JsonElement? el)
    {
        if (el == null) return false;
        var v = el.Value;
        if (v.ValueKind == JsonValueKind.Null || v.ValueKind == JsonValueKind.Undefined) return false;
        if (v.ValueKind == JsonValueKind.String) return !string.IsNullOrWhiteSpace(v.GetString());
        if (v.ValueKind == JsonValueKind.Array) return v.GetArrayLength() > 0;
        return true;
    }

    private static ExtractionData? MapOutputsToExtractionData(JsonElement root)
    {
        var data = new ExtractionData
        {
            Fields = new Dictionary<string, object>(),
            Tables = new Dictionary<string, List<Dictionary<string, object>>>()
        };

        // fields
        if (root.TryGetProperty("fields", out var fields))
        {
            if (fields.ValueKind == JsonValueKind.Array)
            {
                var fieldsList = fields.EnumerateArray().ToList();
                // V2 判定：带 extracted_value（分析模板特有）→ 空值字段不入结果（避免把未提取到的字段写进 sample_data）
                var usesV2 = fieldsList.Any(fd => fd.ValueKind == JsonValueKind.Object
                    && fd.TryGetProperty("extracted_value", out _));

                foreach (var f in fieldsList)
                {
                    if (f.ValueKind != JsonValueKind.Object) continue;
                    // 编码优先级：V1 field_code → V2 field_name_en → V1 field_name → V2 field_name_cn
                    var code = GetString(f, "field_code")
                               ?? GetString(f, "field_name_en")
                               ?? GetString(f, "field_name")
                               ?? GetString(f, "field_name_cn")
                               ?? "";
                    if (string.IsNullOrEmpty(code)) continue;

                    var value = GetRaw(f, "field_value")
                                ?? GetRaw(f, "value")
                                ?? GetRaw(f, "extracted_value");

                    // V2 语义（与 MapAiFieldsToDtos 一致）：丢弃未提取到实际值的字段
                    if (usesV2 && !HasJsonValue(value)) continue;
                    data.Fields[code] = value == null ? "" : (ConvertJsonElement(value.Value) ?? "");
                }
            }
            else if (fields.ValueKind == JsonValueKind.Object)
            {
                foreach (var kv in fields.EnumerateObject())
                    data.Fields[kv.Name] = ConvertJsonElement(kv.Value) ?? "";
            }
        }

        // tables
        if (root.TryGetProperty("tables", out var tables) && tables.ValueKind == JsonValueKind.Array)
        {
            foreach (var t in tables.EnumerateArray())
            {
                if (t.ValueKind != JsonValueKind.Object) continue;
                // V2 判定（带 extracted_data / table_name_cn）：无真实提取行的表格不入结果
                var isV2Table = t.TryGetProperty("extracted_data", out _) || t.TryGetProperty("table_name_cn", out _);
                // 编码优先级：V1 table_code → V2 table_name_en → V1 table_name → V2 table_name_cn
                var tableCode = GetString(t, "table_code")
                                ?? GetString(t, "table_name_en")
                                ?? GetString(t, "table_name")
                                ?? GetString(t, "table_name_cn")
                                ?? "";
                if (string.IsNullOrEmpty(tableCode)) continue;

                // 行数组：V1 rows → V2 extracted_data
                var rows = new List<Dictionary<string, object>>();
                var rowsEl = t.TryGetProperty("rows", out var r1) && r1.ValueKind == JsonValueKind.Array
                    ? r1
                    : (t.TryGetProperty("extracted_data", out var r2) && r2.ValueKind == JsonValueKind.Array ? r2 : default);
                if (rowsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var row in rowsEl.EnumerateArray())
                    {
                        if (row.ValueKind != JsonValueKind.Object) continue;
                        var dict = new Dictionary<string, object>();
                        foreach (var prop in row.EnumerateObject())
                            dict[prop.Name] = ConvertJsonElement(prop.Value) ?? "";
                        rows.Add(dict);
                    }
                }
                if (isV2Table && rows.Count == 0) continue;
                data.Tables[tableCode] = rows;
            }
        }

        return data;
    }

    // ========================================================
    // 提示词模板（DB 模板 analyze_{skill} 优先，回退内嵌默认；对照旧 BuildAnalysisPromptAsync）
    // ========================================================

    private async Task<string> BuildAnalysisPromptAsync(string skill)
    {
        var promptCode = $"analyze_{skill}";
        var rows = await _db.SqlQueryAsync<PromptRow>(
            "SELECT template FROM wf_prompt_template WHERE prompt_code = @c AND is_active = 1 AND IsDeleted = 0 ORDER BY version DESC LIMIT 1",
            new { c = promptCode });
        var dbPrompt = rows.Data?.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(dbPrompt?.template))
        {
            _logger.LogInformation("[DocExtractionRule] 使用数据库提示词: {Code}", promptCode);
            return dbPrompt.template;
        }

        _logger.LogInformation("[DocExtractionRule] 使用内嵌默认提示词: {Code}", promptCode);
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

规则（必须遵守）：
- 内容中的 Markdown 表格（| 开头的行块）属于表格数据，表格内的单元格内容（如""质量方针""、""质量目标""）禁止作为 fields 提取
- 表格内容只能通过 tables 提取，每个表格只需输出名称与列定义 columns，不要将表格内容拆成独立字段
- fields 只从普通段落中提取，且必须能在文档中找到实际内容
- 字段名称必须与文档中的实际标签一致，禁止角色替换（如把""总经理""改写为""编制人""、把""管理者代表""改写为""审核人/批准人""）；文档中不存在的字段一律不要输出

只输出 JSON，不要任何解释文字。

{skillDesc}内容：
---
{{document_content}}
---";
    }

    // ========================================================
    // AI 调用日志（对照旧 LogAIUsageAsync：写 cert_ai_usage_log 供费用监控）
    // ========================================================

    private static async Task LogAiUsageAsync(
        IDbOrm db, ILogger logger, string businessSkill, string skill, string fileCode,
        AiSettings settings, SharedDoc::LlmInvokeResponse result)
    {
        try
        {
            var totalTokens = (result.PromptTokens ?? 0) + (result.CompletionTokens ?? 0);
            await db.SqlExecuteAsync(
                @"INSERT INTO cert_ai_usage_log
                  (call_id, code, business_type, business_ref, skill, provider, model,
                   prompt_tokens, completion_tokens, total_tokens, duration_ms, success, error_message, CreateTime)
                  VALUES (@CallId, @Code, 'doc_extraction', @BusinessRef, @Skill, @Provider, @Model,
                   @PromptTokens, @CompletionTokens, @TotalTokens, @DurationMs, @Success, @Error, NOW())",
                new
                {
                    CallId = Guid.NewGuid().ToString("N"),
                    Code = Guid.NewGuid().ToString("N"),
                    BusinessRef = fileCode,
                    Skill = businessSkill,
                    Provider = settings.Provider,
                    Model = settings.Model,
                    PromptTokens = result.PromptTokens ?? 0,
                    CompletionTokens = result.CompletionTokens ?? 0,
                    TotalTokens = totalTokens,
                    DurationMs = result.DurationMs,
                    Success = result.Success,
                    Error = result.Success ? null : (result.Message ?? "").Limit(500)
                });
        }
        catch (Exception ex)
        {
            // 日志失败不影响主流程
            logger.LogWarning(ex, "[DocExtractionRule] AI 用量日志写入失败");
        }
    }

    // ========================================================
    // JsonElement 辅助
    // ========================================================

    private static string? GetString(JsonElement el, string name)
        => el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static bool GetBool(JsonElement el, string name)
        => el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

    private static JsonElement? GetRaw(JsonElement el, string name)
        => el.TryGetProperty(name, out var v) ? v : null;

    private static object? ConvertJsonElement(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String => el.GetString(),
        JsonValueKind.Number => el.TryGetInt64(out var l) ? l : el.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => el.GetRawText()
    };
}

/// <summary>AI 用量消息长度截断扩展</summary>
internal static class AiLogExtensions
{
    public static string Limit(this string s, int max) => s.Length <= max ? s : s[..max];
}

/// <summary>提示词模板行 DTO</summary>
public class PromptRow
{
    public string? template { get; set; }
}

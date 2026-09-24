
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
using SqlSugar;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Interfaces;
using CertPlatform.Shared.DocExtraction;
using CertPlatform.Shared.Entities.Dir;
using CertPlatform.Shared.Entities.Doc;
using CertPlatform.Shared.Entities.Sys;
using CertPlatform.Shared.Entities.Wf;
using CertPlatform.Shared.Entities.Cert;

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
            var isNew = entity == null;
            if (isNew)
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

            // 准则 A：新增 vs 更新只看 Code 是否已落库（禁止 Id == 0 分流）
            if (isNew)
                await _db.InsertAsync(entity);
            else
            {
                if (string.IsNullOrWhiteSpace(entity.Code))
                    return (false, "更新失败：缺少业务键 Code");
                await _db.UpdateAsync(entity);
            }

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

        var rows = await _db.Client.Queryable<SysConfig>()
            .Where(x => x.Category == "ai_model" && !x.IsDeleted)
            .Select(x => new ConfigKV { ConfigKey = x.ConfigKey, ConfigValue = x.ConfigValue })
            .ToListAsync();
        foreach (var row in rows)
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
        var fr = (await _db.Client.Queryable<FileRequirement>()
            .Where(x => x.Code == standardFileCode)
            .Where("IsValid = 1 AND IsDeleted = 0")
            .Select(x => new FrRow
            {
                FileNameTemplate = x.FileNameTemplate,
                TemplateFileName = x.TemplateFileName,
                TemplateStoragePath = x.TemplateStoragePath
            })
            .FirstAsync());
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

        // 3.1 结构化上下文（【正文】/【表格 n】分区）+ 模板占位符渲染
        var (defFields, defTables) = await LoadRuleDefsAsync(request.FileCode);
        var structured = BuildStructuredContext(markdown);
        var (renderedPrompt, inlineContent) = RenderPrompt(analyzePrompt, structured, defFields, defTables, null);

        // 4. LLM 调用
        var llmResult = await _llm.CompleteAsync(new CertPlatform.Shared.DocExtraction.LlmInvokeRequest
        {
            BaseUrl = settings.BaseUrl,
            ApiKey = settings.ApiKey,
            Model = settings.Model,
            Temperature = settings.Temperature,
            MaxTokens = settings.MaxTokens,
            Prompt = renderedPrompt,
            DocumentContent = inlineContent,
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

            // 1. 规则（含 doc_content 缓存定位）+ 已配置的字段/表格清单（固定提示词的唯一依据）
            var rule = (await _db.GetOneAsync<DocExtractionRule>(x => x.StandardFileCode == request.FileCode)).Data;
            var skill = rule?.Skill ?? ResolveSkill(request.FileCode);
            var (defFields, defTables) = await LoadRuleDefsAsync(request.FileCode);

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

            // 3. 提示词：用户提示词为空 → 使用「固定提示词」
            //    （由本文件已配置的字段/表格清单生成，保证字段与表格结构性分离；
            //      DB 模板里的 {{fields_json}} / {{tables_json}} / {{document_content}} 占位符在这里渲染）
            var userPrompt = (request.Prompt ?? "").Trim();
            var useFixedPrompt = string.IsNullOrEmpty(userPrompt);
            var promptTemplate = useFixedPrompt ? BuildFixedExtractionPrompt(defFields, defTables) : userPrompt;
            var structured = BuildStructuredContext(docContent);
            var (renderedPrompt, inlineContent) = RenderPrompt(promptTemplate, structured, defFields, defTables, rule?.Prompt);

            // 4. AI 提取
            var llmResult = await _llm.CompleteAsync(new CertPlatform.Shared.DocExtraction.LlmInvokeRequest
            {
                BaseUrl = settings.BaseUrl,
                ApiKey = settings.ApiKey,
                Model = settings.Model,
                Temperature = settings.Temperature,
                // 提取结果要包含全部字段 + 表格行，4096 会截断 JSON（表现为「AI 返回内容无法解析为 JSON」）
                MaxTokens = Math.Max(settings.MaxTokens, 8192),
                Prompt = renderedPrompt,
                DocumentContent = inlineContent,
                ForceJson = true
            });

            await LogAiUsageAsync(_db, _logger, "verify", skill, request.FileCode, settings, llmResult);

            if (!llmResult.Success)
                return new VerifyPromptResponse { Success = false, Message = llmResult.Message, Data = new ExtractionData { Message = llmResult.Message } };

            var extraction = MapOutputsToExtractionData(llmResult.Json!.RootElement, defFields, defTables);
            if (extraction == null)
                return new VerifyPromptResponse { Success = false, Message = "AI 返回内容无法解析为提取结果" };

            // 依据定义过滤：AI 未被要求输出的字段/表格（跑偏内容）不计入，避免「表格列名混进字段」

            // 映射后为空：AI 有返回但不符合提取格式（或文档确实无可提取内容）
            // → 明确报错而不是静默「验证成功 + 空结果」，否则前端只看到空白
            var extractedCount = (extraction.Fields?.Count ?? 0) + (extraction.Tables?.Count ?? 0);
            if (extractedCount == 0)
                return new VerifyPromptResponse
                {
                    Success = false,
                    Message = "AI 未提取到任何字段或表格：可在弹窗中清空提示词改用「固定提示词」（按本文件已配置的字段/表格生成），或先在「文档提取规则」页执行自动分析并保存规则",
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

    private static ExtractionData? MapOutputsToExtractionData(
        JsonElement root, List<FieldDefDto>? defFields = null, List<TableDefDto>? defTables = null)
    {
        var data = new ExtractionData
        {
            Fields = new Dictionary<string, object>(),
            Tables = new Dictionary<string, List<Dictionary<string, object>>>()
        };

        // 定义索引：编码（不区分大小写）+ 中文名 → 编码。
        // 传入定义时，结果键一律归一为「字段/列表编码」，中文名不再作为输出键
        // （否则中文键既是字段名又是表格名，前端无从区分，工作流转引也拿不到值）。
        var fieldByCode = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var fieldByCn = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in defFields ?? new())
        {
            var code = !string.IsNullOrEmpty(d.Code) ? d.Code : (d.NameEn ?? "");
            if (string.IsNullOrEmpty(code)) continue;
            fieldByCode[code] = code;
            if (!string.IsNullOrEmpty(d.Name)) fieldByCn[d.Name] = code;
        }

        string? ResolveFieldCode(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var key = raw.Trim();
            if (fieldByCode.TryGetValue(key, out var byCode)) return byCode;
            if (fieldByCn.TryGetValue(key, out var byCn)) return byCn;
            // 无定义时不做任何过滤（保持旧行为：有什么取什么）
            return defFields == null || defFields.Count == 0 ? key : null;
        }

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
                    // 允许 fields 项写成 {"字段名": "值"} 这种键值对象（小模型常见输出）
                    if (f.ValueKind != JsonValueKind.Object) continue;

                    var rawCode = GetString(f, "field_code")
                                  ?? GetString(f, "field_name_en")
                                  ?? GetString(f, "fieldCode")
                                  ?? GetString(f, "field_name")
                                  ?? GetString(f, "field_name_cn")
                                  ?? GetString(f, "fieldName");

                    var value = GetRaw(f, "field_value")
                                ?? GetRaw(f, "value")
                                ?? GetRaw(f, "extracted_value")
                                ?? GetRaw(f, "extractedValue");

                    var code = ResolveFieldCode(rawCode);
                    if (code == null)
                    {
                        // 无 field_code 键时，尝试把该对象当作 {中文名: 值} 单键对象
                        foreach (var kv in f.EnumerateObject())
                        {
                            var k = ResolveFieldCode(kv.Name);
                            if (k != null && HasJsonValue(kv.Value))
                            {
                                data.Fields[k] = ConvertJsonElement(kv.Value) ?? "";
                                break;
                            }
                        }
                        continue;
                    }

                    // V2 语义（与 MapAiFieldsToDtos 一致）：丢弃未提取到实际值的字段
                    if (usesV2 && !HasJsonValue(value)) continue;
                    // 定义内的字段即使为空也写入（前端按定义展示「未提取到」，避免整项消失）
                    data.Fields[code] = value == null ? "" : (ConvertJsonElement(value.Value) ?? "");
                }
            }
            else if (fields.ValueKind == JsonValueKind.Object)
            {
                foreach (var kv in fields.EnumerateObject())
                {
                    var code = ResolveFieldCode(kv.Name);
                    if (code == null) continue;
                    data.Fields[code] = ConvertJsonElement(kv.Value) ?? "";
                }
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
                var rawTableCode = GetString(t, "table_code")
                                ?? GetString(t, "table_name_en")
                                ?? GetString(t, "tableCode")
                                ?? GetString(t, "table_name")
                                ?? GetString(t, "table_name_cn")
                                ?? GetString(t, "tableName");

                // 定义匹配：编码或中文名命中则用定义编码（保证工作流/前端拿到稳定键）；无定义时沿用 AI 给的键
                var defTable = MatchTableDef(defTables, rawTableCode);
                var tableCode = defTable?.Code ?? rawTableCode ?? "";
                if (string.IsNullOrEmpty(tableCode)) continue;

                // 行数组：V1 rows → V2 extracted_data
                var rows = new List<Dictionary<string, object>>();
                var rowsEl = t.TryGetProperty("rows", out var r1) && r1.ValueKind == JsonValueKind.Array
                    ? r1
                    : (t.TryGetProperty("extracted_data", out var r2) && r2.ValueKind == JsonValueKind.Array ? r2 : default);

                // 列名（用于「二维数组行」按位置绑定，以及把中文行键归一为列编码）
                var colMap = BuildColumnMap(defTable, t);

                if (rowsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var row in rowsEl.EnumerateArray())
                    {
                        var dict = RowToDict(row, colMap);
                        if (dict != null) rows.Add(dict);
                    }
                }
                else if (t.TryGetProperty("rows", out var single) && single.ValueKind == JsonValueKind.Object)
                {
                    // rows 直接是单行对象
                    var dict = RowToDict(single, colMap);
                    if (dict != null) rows.Add(dict);
                }

                if (isV2Table && rows.Count == 0) continue;
                data.Tables[tableCode] = rows;
            }
        }

        return data;
    }

    /// <summary>按编码或中文名匹配表格定义（不区分大小写）</summary>
    private static TableDefDto? MatchTableDef(List<TableDefDto>? defs, string? raw)
    {
        if (defs == null || defs.Count == 0 || string.IsNullOrWhiteSpace(raw)) return null;
        var key = raw.Trim();
        return defs.FirstOrDefault(d =>
                   string.Equals(d.Code, key, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(d.NameEn, key, StringComparison.OrdinalIgnoreCase))
               ?? defs.FirstOrDefault(d => string.Equals(d.Name, key, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 构建列映射：{中文列名 / AI 列编码 → 目标列编码}
    /// <para>目标编码优先取「表格定义的列编码」（工作流按列编码取值）；无定义时退回 AI 输出的列名</para>
    /// </summary>
    private static (List<string> Ordered, Dictionary<string, string> Map) BuildColumnMap(TableDefDto? defTable, JsonElement tableEl)
    {
        var ordered = new List<string>();
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (defTable != null && defTable.Columns.Count > 0)
        {
            foreach (var c in defTable.Columns)
            {
                var code = !string.IsNullOrEmpty(c.Code) ? c.Code : (c.NameEn ?? c.Name);
                ordered.Add(code);
                if (!string.IsNullOrEmpty(c.Name)) map[c.Name] = code;
                if (!string.IsNullOrEmpty(c.NameEn)) map[c.NameEn] = code;
                map[code] = code;
            }
        }

        // AI 自报的列定义（定义缺失时用于二维数组行绑定）
        if (tableEl.TryGetProperty("columns", out var cols) && cols.ValueKind == JsonValueKind.Array)
        {
            foreach (var cd in cols.EnumerateArray())
            {
                if (cd.ValueKind != JsonValueKind.Object) continue;
                var cn = GetString(cd, "column_name_cn") ?? GetString(cd, "column_name") ?? GetString(cd, "name");
                var en = GetString(cd, "column_name_en") ?? GetString(cd, "column_code") ?? GetString(cd, "code");
                var target = !string.IsNullOrEmpty(en) ? en : cn;
                if (string.IsNullOrEmpty(target)) continue;
                if (!ordered.Contains(target)) ordered.Add(target);
                if (!string.IsNullOrEmpty(cn) && !map.ContainsKey(cn)) map[cn] = target;
                if (!string.IsNullOrEmpty(en)) map.TryAdd(en, en);
                map.TryAdd(target, target);
            }
        }

        return (ordered, map);
    }

    /// <summary>
    /// 单行 → 字典（键归一为列编码）。支持两种行形态：
    /// <para>· 对象行 {"角色":"编制","姓名":"张三"}（V2 常见）</para>
    /// <para>· 二维数组行 ["编制","张三"]（V1 extract_all 模板）——按列顺序绑定，
    ///   之前只认对象行，导致这类表格全部落空（表现为「表格数据为空」）</para>
    /// </summary>
    private static Dictionary<string, object>? RowToDict(JsonElement row, (List<string> Ordered, Dictionary<string, string> Map) colMap)
    {
        var dict = new Dictionary<string, object>();

        if (row.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in row.EnumerateObject())
            {
                var key = colMap.Map.TryGetValue(prop.Name, out var mapped) ? mapped : prop.Name;
                dict[key] = ConvertJsonElement(prop.Value) ?? "";
            }
            return dict;
        }

        if (row.ValueKind == JsonValueKind.Array)
        {
            var cells = row.EnumerateArray().ToList();
            for (int i = 0; i < cells.Count; i++)
            {
                var key = i < colMap.Ordered.Count ? colMap.Ordered[i] : $"column_{i + 1}";
                dict[key] = ConvertJsonElement(cells[i]) ?? "";
            }
            return dict;
        }

        return null;
    }

    // ========================================================
    // 结构化上下文 + 固定提示词（字段/表格结构性分离）
    // ========================================================

    /// <summary>结构化上下文体积上限（超出时截断【正文】，表格区块完整保留）</summary>
    private const int MaxContextChars = 48000;

    /// <summary>
    /// 读取该文件已配置的字段/表格定义（无规则时返回空集合）。
    /// <para>定义是「固定提示词」与「结果中文回显」的唯一依据。</para>
    /// </summary>
    private async Task<(List<FieldDefDto> Fields, List<TableDefDto> Tables)> LoadRuleDefsAsync(string? standardFileCode)
    {
        var fields = new List<FieldDefDto>();
        var tables = new List<TableDefDto>();
        if (string.IsNullOrWhiteSpace(standardFileCode)) return (fields, tables);

        var ruleCode = (await _db.GetOneAsync<DocExtractionRule>(x => x.StandardFileCode == standardFileCode)).Data?.Code;
        if (string.IsNullOrEmpty(ruleCode)) return (fields, tables);

        var fRows = (await _db.GetListAsync<DocFieldDef>(x => x.RuleCode == ruleCode)).Data ?? new();
        fields = fRows.OrderBy(x => x.Sort).Select(x => new FieldDefDto
        {
            Name = x.FieldName,
            NameEn = x.FieldCode,
            Code = x.FieldCode,
            DataType = x.DataType,
            Description = x.Description,
            IsManual = x.IsManual,
            IsAiRecommended = x.IsAiRecommended ?? true
        }).ToList();

        var tRows = (await _db.GetListAsync<DocTableDef>(x => x.RuleCode == ruleCode)).Data ?? new();
        foreach (var t in tRows.OrderBy(x => x.Sort))
        {
            var cRows = (await _db.GetListAsync<DocTableFieldDef>(x => x.TableCode == t.Code)).Data ?? new();
            tables.Add(new TableDefDto
            {
                Name = t.TableName,
                NameEn = t.TableCode,
                Code = t.TableCode,
                Description = t.Description,
                Columns = cRows.OrderBy(x => x.Sort).Select(x => new TableColumnDto
                {
                    Name = x.ColumnName,
                    NameEn = x.ColumnCode,
                    Code = x.ColumnCode,
                    DataType = x.DataType
                }).ToList()
            });
        }
        return (fields, tables);
    }

    /// <summary>
    /// 把 Markdown 拆成【正文】+【表格 n】的结构化上下文。
    /// <para>为什么需要：直接喂裸 GFM Markdown 时，模型无法稳定区分「表格单元格」与
    /// 「正文字段」，实测会把表格列名/单元格当成字段输出，或把表格数据留空。
    /// 显式分区后，表格内容只能进 tables（与 DB 分析模板里已经写好的
    /// 「表格内容处理规则」语义对齐）。</para>
    /// </summary>
    private static string BuildStructuredContext(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return "";

        var lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var body = new List<string>();
        var tables = new List<List<string>>();
        List<string>? cur = null;

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            if (line.TrimStart().StartsWith("|"))
            {
                cur ??= new List<string>();
                cur.Add(line.Trim());
                continue;
            }
            if (cur != null) { tables.Add(cur); cur = null; }
            body.Add(line);
        }
        if (cur != null) tables.Add(cur);

        var sb = new StringBuilder();
        sb.AppendLine("【正文】（普通段落/标题，字段只能来自这里）");
        var bodyText = CollapseBlankLines(body);
        sb.AppendLine(bodyText.Length > MaxContextChars
            ? bodyText[..MaxContextChars] + "\n…（正文过长已截断；表格区块完整保留）"
            : bodyText);

        for (int i = 0; i < tables.Count; i++)
        {
            var t = tables[i];
            var headers = SplitTableRow(t[0]);
            var dataRows = Math.Max(0, t.Count - 2);
            sb.AppendLine();
            sb.AppendLine($"【表格 {i + 1}】（{dataRows} 行数据，列：{string.Join(" | ", headers)}）");
            foreach (var l in t) sb.AppendLine(l);
        }

        return sb.ToString().TrimEnd();
    }

    private static string CollapseBlankLines(List<string> lines)
    {
        var sb = new StringBuilder();
        var blanks = 0;
        foreach (var l in lines)
        {
            if (string.IsNullOrWhiteSpace(l))
            {
                if (++blanks > 1) continue;
            }
            else blanks = 0;
            sb.AppendLine(l);
        }
        return sb.ToString().Trim();
    }

    private static List<string> SplitTableRow(string row)
        => row.Trim().Trim('|').Split('|').Select(c => c.Trim()).ToList();

    /// <summary>
    /// 生成「固定提示词」：完全由本文件已配置的字段/表格清单驱动。
    /// <para>与自由分析模板（analyze_*）的区别：这里把提取目标锁死为清单，
    /// 并显式规定「表格区块内容不得作为字段」+「行键用 column_code」，
    /// 从提示词层面消除“表格列名被当成字段、表格数据为空”两类错误。</para>
    /// </summary>
    private static string BuildFixedExtractionPrompt(List<FieldDefDto> fields, List<TableDefDto> tables)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# 文档数据提取任务（固定契约）");
        sb.AppendLine();
        sb.AppendLine("你是严谨的文档信息提取助手。**只能**按下方的字段清单与表格清单提取数据，不得新增、改名、合并。");
        sb.AppendLine();

        sb.AppendLine("## 一、字段清单（只能出现在 fields 里）");
        sb.AppendLine();
        if (fields.Count == 0)
        {
            sb.AppendLine("（本文件未配置字段，fields 输出空数组 []）");
        }
        else
        {
            sb.AppendLine("| # | 字段名称(中文) | field_code | 类型 | 必填 | 说明 |");
            sb.AppendLine("|---|---------------|------------|------|------|------|");
            for (int i = 0; i < fields.Count; i++)
            {
                var f = fields[i];
                sb.AppendLine($"| {i + 1} | {f.Name} | {f.NameEn ?? f.Code} | {f.DataType} | {(f.IsRequired ? "是" : "否")} | {f.Description} |");
            }
        }
        sb.AppendLine();

        sb.AppendLine("## 二、表格清单（只能出现在 tables 里）");
        sb.AppendLine();
        if (tables.Count == 0)
        {
            sb.AppendLine("（本文件未配置表格，tables 输出空数组 []）");
        }
        else
        {
            for (int i = 0; i < tables.Count; i++)
            {
                var t = tables[i];
                sb.AppendLine($"### 表格 {i + 1}：{t.Name}");
                sb.AppendLine($"- table_code: {t.NameEn ?? t.Code}");
                if (!string.IsNullOrEmpty(t.Description)) sb.AppendLine($"- 说明：{t.Description}");
                if (t.Columns.Count > 0)
                {
                    sb.AppendLine("- 列（rows 的键必须用 column_code）：");
                    sb.AppendLine();
                    sb.AppendLine("| # | 列名称(中文) | column_code | 类型 |");
                    sb.AppendLine("|---|-------------|-------------|------|");
                    for (int j = 0; j < t.Columns.Count; j++)
                    {
                        var c = t.Columns[j];
                        sb.AppendLine($"| {j + 1} | {c.Name} | {c.NameEn ?? c.Code} | {c.DataType} |");
                    }
                }
                sb.AppendLine();
            }
        }

        sb.AppendLine("## 三、输出格式（严格 JSON，不要解释文字、不要 Markdown 围栏）");
        sb.AppendLine();
        sb.AppendLine("{");
        sb.AppendLine("  \"fields\": [{\"field_code\": \"<字段清单里的 field_code>\", \"field_value\": \"<从文档中提取的值>\"}],");
        sb.AppendLine("  \"tables\": [{\"table_code\": \"<表格清单里的 table_code>\", \"rows\": [{\"<column_code>\": \"值\"}]}]");
        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine("## 四、硬性规则");
        sb.AppendLine();
        sb.AppendLine("1. fields / tables 只允许出现清单中的 field_code / table_code，清单外的内容一律忽略。");
        sb.AppendLine("2. 【表格 n】区块属于表格：其中的表头与单元格内容**不得**作为字段输出，表格数据只能写进对应表格的 rows。");
        sb.AppendLine("3. rows 每行的键必须是列清单里的 column_code（不要用中文列名）。");
        sb.AppendLine("4. 文档中找不到的字段：field_value 输出空字符串 \"\"；没有数据的表格：rows 输出 []。");
        sb.AppendLine("5. 值保持文档原文内容；日期尽量规范为 YYYY-MM-DD。");
        sb.AppendLine("6. 清单中的每张表格都必须出现在 tables 里（即使为空数组）；单张表格最多输出 50 行，文档中不足 50 行则全部输出，不要因为篇幅省略表格或只填部分表格。");
        sb.AppendLine("7. 字段值只给实际值（不要复制大段正文）；整个 JSON 尽量控制在 3000 tokens 内，**优先保证 JSON 完整合法**（截断的 JSON 等于提取失败）。");
        sb.AppendLine();
        sb.AppendLine("## 五、文档内容（【正文】为普通段落；【表格 n】为表格区块）");
        sb.AppendLine();
        sb.AppendLine("{{document_content}}");

        return sb.ToString();
    }

    /// <summary>
    /// 渲染提示词占位符，并决定文档上下文是「内联进提示词」还是「由调用层追加」。
    /// <para>支持：{{document_content}} / {document_content}、{{fields_json}}、{{tables_json}}、{{prompt}}</para>
    /// <para>⚠️ wf_prompt_template 里的 extract_all/verify_all 带这些占位符；旧实现从不替换，
    /// 模型拿到字面量 {{tables_json}} 只能自己编结构 → 字段/表格混在一起。</para>
    /// </summary>
    private static (string Prompt, string InlineContent) RenderPrompt(
        string template, string structuredContext,
        List<FieldDefDto> fields, List<TableDefDto> tables, string? rulePrompt)
    {
        var prompt = template ?? "";
        var hasContentSlot = prompt.Contains("{{document_content}}") || prompt.Contains("{document_content}");

        if (hasContentSlot)
        {
            prompt = prompt.Replace("{{document_content}}", structuredContext)
                           .Replace("{document_content}", structuredContext);
        }

        prompt = prompt.Replace("{{fields_json}}", BuildFieldsJson(fields))
                       .Replace("{{tables_json}}", BuildTablesJson(tables))
                       .Replace("{{prompt}}", BuildDefsBrief(fields, tables, rulePrompt));

        // 模板自带内容槽 → 上下文已内联，避免调用层在末尾重复追加
        return (prompt, hasContentSlot ? "" : structuredContext);
    }

    /// <summary>{{fields_json}} 占位符的替身：显式 field_code / 中文名 / 类型 / 是否必填</summary>
    private static string BuildFieldsJson(List<FieldDefDto> fields)
        => JsonSerializer.Serialize(fields.Select(f => new
        {
            field_code = f.NameEn ?? f.Code,
            field_name = f.Name,
            field_type = f.DataType,
            is_required = f.IsRequired,
            description = f.Description
        }), JsonOptions);

    /// <summary>{{tables_json}} 占位符的替身：表格 + 列（column_code）定义</summary>
    private static string BuildTablesJson(List<TableDefDto> tables)
        => JsonSerializer.Serialize(tables.Select(t => new
        {
            table_code = t.NameEn ?? t.Code,
            table_name = t.Name,
            description = t.Description,
            columns = t.Columns.Select(c => new
            {
                column_code = c.NameEn ?? c.Code,
                column_name = c.Name,
                column_type = c.DataType
            })
        }), JsonOptions);

    /// <summary>{{prompt}} 占位符的替身：字段/表格清单的紧凑中文描述</summary>
    private static string BuildDefsBrief(List<FieldDefDto> fields, List<TableDefDto> tables, string? rulePrompt)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(rulePrompt) &&
            !rulePrompt.Contains("{{prompt}}") &&
            !rulePrompt.Contains("{{document_content}}"))
        {
            sb.AppendLine(rulePrompt.Trim());
            sb.AppendLine();
        }

        sb.AppendLine("【字段清单】");
        foreach (var f in fields)
            sb.AppendLine($"- {f.Name}（field_code={f.NameEn ?? f.Code}，类型={f.DataType}）");
        if (fields.Count == 0) sb.AppendLine("-（无）");

        sb.AppendLine();
        sb.AppendLine("【表格清单】（rows 的键必须用 column_code）");
        foreach (var t in tables)
        {
            sb.AppendLine($"- {t.Name}（table_code={t.NameEn ?? t.Code}）：" +
                string.Join("、", t.Columns.Select(c => $"{c.Name}({c.NameEn ?? c.Code})")));
        }
        if (tables.Count == 0) sb.AppendLine("-（无）");

        return sb.ToString().TrimEnd();
    }

    // ========================================================
    // 提示词模板（DB 模板 analyze_{skill} 优先，回退内嵌默认；对照旧 BuildAnalysisPromptAsync）
    // ========================================================

    private async Task<string> BuildAnalysisPromptAsync(string skill)
    {
        var promptCode = $"analyze_{skill}";
        var dbPrompt = await _db.Client.Queryable<PromptTemplate>()
            .Where(x => x.PromptCode == promptCode && x.IsActive)
            .OrderByDescending(x => x.Version)
            .Select(x => new PromptRow { template = x.Template })
            .FirstAsync();
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
- 优先穷举「标签」冒号「值」信息对：扫描所有以中文冒号「：」或英文冒号「:」分隔的键值对，冒号前为字段名、冒号后为字段值；值为空的跳过，同一标签多次出现只保留一个字段
- 文档中不存在真实表格结构（Markdown 表格行列）时 tables 必须输出 []，禁止根据文件名、目录名、标题或常识臆造表格
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
        AiSettings settings, CertPlatform.Shared.DocExtraction.LlmInvokeResponse result)
    {
        try
        {
            var totalTokens = (result.PromptTokens ?? 0) + (result.CompletionTokens ?? 0);
            var log = new AiUsageLog
            {
                CallId = Guid.NewGuid().ToString("N"),
                Code = Guid.NewGuid().ToString("N"),
                BusinessType = "doc_extraction",
                BusinessRef = fileCode,
                Skill = businessSkill,
                Provider = settings.Provider,
                Model = settings.Model,
                PromptTokens = result.PromptTokens ?? 0,
                CompletionTokens = result.CompletionTokens ?? 0,
                TotalTokens = totalTokens,
                DurationMs = result.DurationMs,
                Success = result.Success,
                ErrorMessage = result.Success ? null : (result.Message ?? "").Limit(500),
                CreateTime = DateTime.Now
            };
            await db.Client.Insertable(log).ExecuteCommandAsync();
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

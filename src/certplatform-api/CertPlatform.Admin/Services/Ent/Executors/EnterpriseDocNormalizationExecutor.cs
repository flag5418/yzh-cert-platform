using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Queue;
using CertPlatform.Admin.Entities.Doc;
using CertPlatform.Shared.Entities.Cert;
using CertPlatform.Shared.Entities.Dir;
using CertPlatform.Shared.Office;
using CertPlatform.Shared.Office.Word;
using CertPlatform.Shared.Office.Excel;
using CertPlatform.Shared.Storage;
using CertPlatform.Admin.Services.Workflow.Skills.Fill.Ai;
using CertPlatform.Shared.Fill;
using CertPlatform.Shared.Constants;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Admin.Services.Ent.Executors
{
    /// <summary>
    /// 企业资料规范化执行引擎
    /// <para>核心职责：将企业上传的原始资料，按标准文件要求，通过 AI 和规则填充到模板中，生成标准化的体系文档。</para>
    /// <para>对应 46 号技术方案的实现。</para>
    /// </summary>
    public class EnterpriseDocNormalizationExecutor : IYzhTaskExecutor
    {
        /// <summary>
        ///     ★ 任务类型常量 —— 由 <see cref="EnterpriseDocNormalizationExecutorAdapter"/> <b>复用</b>。
        ///     <para>⛔ 不要在两处各写一遍字面量：<c>QueueManager</c> 按 <c>TaskType</c> 建字典分发，
        ///     两处不一致 = 任务<b>静默分发不到</b>（不报错）。</para>
        /// </summary>
        public const string TaskTypeName = "ent_doc_normalize";

        public string TaskType => TaskTypeName;

        private readonly IDbOrm _db;
        private readonly ILogger<EnterpriseDocNormalizationExecutor> _logger;
        private readonly IAiFillInvoker _aiInvoker;
        private readonly SourceResolver _sourceResolver;
        private readonly IObjectStorage _storage;

        public EnterpriseDocNormalizationExecutor(
            IDbOrm db,
            ILogger<EnterpriseDocNormalizationExecutor> logger,
            IAiFillInvoker aiInvoker,
            IObjectStorage storage)
        {
            _db = db;
            _logger = logger;
            _aiInvoker = aiInvoker;
            _storage = storage;
            _sourceResolver = new SourceResolver(db);
        }

        public async Task<TaskExecutionResult> ExecuteAsync(YzhQueueTask task, CancellationToken ct)
        {
            var payload = JsonSerializer.Deserialize<NormalizationPayload>(task.Payload ?? "{}");
            if (payload == null || string.IsNullOrEmpty(payload.EnterpriseCode) || string.IsNullOrEmpty(payload.StandardCode))
            {
                return new TaskExecutionResult { Success = false, Message = "任务参数无效：缺少 EnterpriseCode 或 StandardCode", Retryable = false };
            }

            _logger.LogInformation("[EntNorm] 开始规范化任务: Enterprise={Ent}, Std={Std}, Stage={Stage}", 
                payload.EnterpriseCode, payload.StandardCode, payload.StageCode);

            try
            {
                // 1. 获取企业信息快照（用于全局参数取值）
                var ent = (await _db.GetOneAsync<Enterprise>(x => x.Code == payload.EnterpriseCode)).Data;
                var entInfo = MapToInfo(ent);

                // 2. 获取当前标准下所有需要处理的文件清单
                // ⚠️ 模板行标记为 YzhVirtualEnterprise.Code
                var stdFiles = (await _db.GetListAsync<StandardDirectoryFile>(x => 
                    x.EnterpriseCode == YzhVirtualEnterprise.Code && 
                    x.StandardCode == payload.StandardCode &&
                    x.InstanceState != "archived" &&
                    x.IsValid == 1)).Data ?? new List<StandardDirectoryFile>();

                // 3. 预加载企业原始资料的 Markdown 内容（召回用）
                var (originalDocsMarkdown, retrievedDocCodes) = await LoadEnterpriseOriginalDocsMarkdownAsync(payload.EnterpriseCode, payload.StageCode);

                // 4. 获取配置以拿到 OrgCode
                var config = (await _db.GetOneAsync<StandardDirectoryConfig>(x => 
                    x.StandardCode == payload.StandardCode && 
                    x.StageCode == payload.StageCode && 
                    x.EnterpriseCode == YzhVirtualEnterprise.Code &&
                    x.IsValid == 1)).Data;
                
                var orgCode = config?.OrgCode ?? "";

                foreach (var stdFile in stdFiles)
                {
                    await ProcessStandardFileAsync(stdFile, payload, entInfo, originalDocsMarkdown, retrievedDocCodes, orgCode, ct);
                }

                return new TaskExecutionResult { Success = true, Message = $"成功处理 {stdFiles.Count} 个标准文件要求" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EntNorm] 规范化执行失败: {Msg}", ex.Message);
                return new TaskExecutionResult { Success = false, Message = ex.Message };
            }
        }

        private async Task ProcessStandardFileAsync(
            StandardDirectoryFile stdFile, 
            NormalizationPayload payload, 
            CertPlatform.Shared.Fill.EnterpriseInfo entInfo,
            string originalDocsMarkdown,
            List<string> retrievedDocCodes,
            string orgCode,
            CancellationToken ct)
        {
            // 1. 获取模板和锚点
            var template = (await _db.GetOneAsync<DocTemplate>(x => x.StandardFileCode == stdFile.Code && x.IsValid == 1)).Data;
            if (template == null)
            {
                _logger.LogWarning("[EntNorm] 未找到标准文件 {FileCode} 的有效模板", stdFile.Code);
                return;
            }

            var anchors = (await _db.GetListAsync<DocTemplateAnchor>(x => x.TemplateCode == template.Code && x.IsValid == 1)).Data 
                ?? new List<DocTemplateAnchor>();

            // 2. 取值逻辑
            var fillValues = new Dictionary<string, FillValue>(StringComparer.Ordinal);
            var aiAnchors = new List<DocTemplateAnchor>();
            var skillTrace = new List<string>();

            foreach (var anchor in anchors)
            {
                var spec = _sourceResolver.Parse(anchor.SourceSpec);
                if (spec == null || spec.Sources.Count == 0) continue;

                bool resolved = false;
                foreach (var entry in spec.Sources)
                {
                    if (entry.Kind == "global")
                    {
                        var (ok, val) = await _sourceResolver.TryResolveGlobalAsync(entry, anchor, entInfo, payload.StandardCode, payload.StageCode, orgCode);
                        if (ok && val != null)
                        {
                            fillValues[anchor.AnchorRef] = val;
                            resolved = true;
                            skillTrace.Add($"global:{anchor.AnchorRef}");
                            break;
                        }
                    }
                    else if (entry.Kind == "manual")
                    {
                        var (ok, val) = await _sourceResolver.TryResolveManualAsync(entry, anchor, payload.EnterpriseCode);
                        if (ok && val != null)
                        {
                            fillValues[anchor.AnchorRef] = val;
                            resolved = true;
                            skillTrace.Add($"manual:{anchor.AnchorRef}");
                            break;
                        }
                    }
                    else if (entry.Kind == "ai")
                    {
                        aiAnchors.Add(anchor);
                        resolved = true; // 标记为已处理，后续统一通过 AI 提取
                        break;
                    }
                }
            }

            // 3. 处理 AI 提取
            var aiSuggestions = new List<DocAiSuggestion>();
            if (aiAnchors.Count > 0)
            {
                await ProcessAiAnchorsAsync(aiAnchors, template, originalDocsMarkdown, fillValues, aiSuggestions, skillTrace, payload, orgCode, ct);
            }

            // 4. 执行填充与持久化
            if (fillValues.Count > 0 || anchors.Count > 0) // 即使没值也要尝试填充（清空锚点）
            {
                await ExecuteFillAndSaveAsync(stdFile, template, fillValues, aiSuggestions, skillTrace, retrievedDocCodes, payload, orgCode, ct);
            }
        }

        private async Task ProcessAiAnchorsAsync(
            List<DocTemplateAnchor> aiAnchors, 
            DocTemplate template,
            string docsMarkdown,
            Dictionary<string, FillValue> fillValues,
            List<DocAiSuggestion> aiSuggestions,
            List<string> skillTrace,
            NormalizationPayload payload,
            string orgCode,
            CancellationToken ct)
        {
            var batchCode = Guid.NewGuid().ToString();
            // 按 promptGroup 分组
            var groups = aiAnchors.GroupBy(a => _sourceResolver.Parse(a.SourceSpec)?.Sources.FirstOrDefault(s => s.Kind == "ai")?.PromptGroup ?? "default");

            foreach (var group in groups)
            {
                var buildCtx = new AiFillBuildContext
                {
                    DocumentName = template.FileName,
                    StandardCode = template.StandardCode,
                    EnterpriseDocs = docsMarkdown,
                    Anchors = group.Select(a => new AiFillAnchorSpec
                    {
                        AnchorCode = a.AnchorRef,
                        Instruction = a.Remark ?? a.AnchorRef,
                        ValueKind = a.ValueType,
                        Section = "field", // 目前主要支持字段提取
                        PromptGroup = group.Key
                    }).ToList()
                };

                var request = await AiFillPromptBuilder.BuildBatchAsync(_db, null, buildCtx, orgCode, ct);
                var startTime = DateTime.Now;
                var aiResult = await _aiInvoker.InvokeAsync(_db, request, ct);
                var duration = (int)(DateTime.Now - startTime).TotalMilliseconds;

                skillTrace.Add($"ai_batch:{group.Key}:{group.Count()}");

                if (aiResult.Success && aiResult.Root != null && aiResult.Root.TryGetValue("fields", out var fieldsObj) && fieldsObj is Dictionary<string, object> fields)
                {
                    foreach (var anchor in group)
                    {
                        if (fields.TryGetValue(anchor.AnchorRef, out var fieldVal) && fieldVal is Dictionary<string, object> fieldObj)
                        {
                            if (fieldObj.TryGetValue("value", out var val) && val != null)
                            {
                                var valStr = val.ToString();
                                if (!string.IsNullOrEmpty(valStr))
                                {
                                    var (ok, fv, _) = FillValueFactory.TryCreate(anchor.AnchorRef, valStr, anchor.ValueType, anchor.NumberFormat);
                                    if (ok && fv != null)
                                    {
                                        fv.Source = "ai";
                                        fv.Confidence = fieldObj.TryGetValue("confidence", out var c) && c is double d ? d : 1.0;
                                        fillValues[anchor.AnchorRef] = fv;

                                        // 记录建议
                                        aiSuggestions.Add(new DocAiSuggestion
                                        {
                                            OrgCode = orgCode,
                                            EnterpriseCode = payload.EnterpriseCode,
                                            StageCode = payload.StageCode,
                                            StandardCode = payload.StandardCode,
                                            TemplateFileCode = template.StandardFileCode,
                                            AnchorCode = anchor.AnchorRef,
                                            BatchCode = batchCode,
                                            SuggestedValue = valStr,
                                            ValueKind = anchor.ValueType,
                                            NumberFormat = anchor.NumberFormat,
                                            Confidence = (decimal)fv.Confidence,
                                            SourceSnippet = fieldObj.TryGetValue("note", out var note) ? note.ToString() : null,
                                            Reason = fieldObj.TryGetValue("note", out var reason) ? reason.ToString() : null,
                                            SkillCode = "src_ai_field",
                                            ModelName = aiResult.Model,
                                            PromptTokens = aiResult.PromptTokens ?? 0,
                                            CompletionTokens = aiResult.CompletionTokens ?? 0,
                                            DurationMs = duration
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private async Task ExecuteFillAndSaveAsync(
            StandardDirectoryFile stdFile, 
            DocTemplate template, 
            Dictionary<string, FillValue> fillValues,
            List<DocAiSuggestion> aiSuggestions,
            List<string> skillTrace,
            List<string> retrievedDocCodes,
            NormalizationPayload payload,
            string orgCode,
            CancellationToken ct)
        {
            var startTime = DateTime.Now;

            // 1. 读取模板字节（优先用归一后的产物）
            var templatePath = template.StoragePath;
            var (stream, _) = await _storage.DownloadAsync(templatePath, ct);
            using var ms = new System.IO.MemoryStream();
            await stream.CopyToAsync(ms, ct);
            var templateBytes = ms.ToArray();
            
            var fillRequest = new OfficeFillRequest
            {
                Template = templateBytes,
                Values = fillValues
            };

            // 2. 调用写入器
            OfficeFillResult result;
            if (template.FileKind == "docx")
            {
                var writer = new WordFillWriter();
                result = writer.Fill(fillRequest);
            }
            else if (template.FileKind == "xlsx")
            {
                var writer = new ExcelFillWriter();
                result = writer.Fill(fillRequest);
            }
            else
            {
                _logger.LogWarning("[EntNorm] 不支持的文件类型 {Kind}", template.FileKind);
                return;
            }

            // 3. 保存产物
            var outPath = PathBuilder.EnterpriseFile(payload.EnterpriseCode, payload.StandardCode, payload.StageCode, stdFile.FolderPath, stdFile.FileName);
            await _storage.UploadAsync(outPath, new System.IO.MemoryStream(result.Output), result.Output.Length, 
                template.FileKind == "docx" ? "application/vnd.openxmlformats-officedocument.wordprocessingml.document" : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", 
                ct);

            var duration = (int)(DateTime.Now - startTime).TotalMilliseconds;

            // 4. 持久化日志与建议
            await _db.Client.AsTenant().BeginTranAsync();
            try
            {
                // A. 记录执行日志
                var log = new DocFillLog
                {
                    OrgCode = orgCode,
                    EnterpriseCode = payload.EnterpriseCode,
                    StageCode = payload.StageCode,
                    StandardCode = payload.StandardCode,
                    TemplateFileCode = stdFile.Code,
                    FileKind = template.FileKind == "docx" ? "word" : "excel",
                    OutputStoragePath = outPath,
                    TotalAnchors = result.Report.Total,
                    ResolvedCount = result.Report.Resolved,
                    PendingCount = result.Report.Pending,
                    Completion = (decimal)result.Report.Completion,
                    RegionCount = result.Report.Regions.Count,
                    ClonedRows = result.Report.Regions.Sum(r => r.ClonedRows),
                    Verified = result.Report.Verified,
                    LeftoverTokens = result.Report.LeftoverTokens,
                    PendingsJson = JsonSerializer.Serialize(result.Report.Pendings),
                    RetrievedDocCodes = retrievedDocCodes,
                    SkillTrace = skillTrace,
                    DurationMs = duration,
                    Status = result.Report.Completion >= 1.0 ? "success" : "partial"
                };
                await _db.InsertAsync(log);

                // B. 记录 AI 建议
                if (aiSuggestions.Count > 0)
                {
                    await _db.InsertBatchAsync(aiSuggestions);
                }

                // C. 更新文件实例状态
                // 查找该企业该标准下对应的实例行
                var instance = (await _db.GetOneAsync<StandardDirectoryFile>(x => 
                    x.EnterpriseCode == payload.EnterpriseCode && 
                    x.StandardFileCode == stdFile.Code &&
                    x.IsValid == 1)).Data;

                if (instance != null)
                {
                    instance.InstanceState = "filled";
                    instance.StoragePath = outPath;
                    instance.VersionNumber++; // 递增版本号
                    instance.UpdateTime = DateTime.Now;
                    // ⛔ 必须**点名列**，不能 `UpdateAsync(instance)`：
                    //   单参重载 = **全列写回**（陷阱 ㉑）—— 本次只改了 4 列，
                    //   却把整行其余列按内存快照覆盖回去，会抹掉并发写入
                    //   （且与「只改我关心的列」的意图不符）。守卫 R-A 专门拦这一条。
                    await _db.UpdateAsync(instance,
                        nameof(StandardDirectoryFile.InstanceState),
                        nameof(StandardDirectoryFile.StoragePath),
                        nameof(StandardDirectoryFile.VersionNumber),
                        nameof(StandardDirectoryFile.UpdateTime));
                }
                else
                {
                    // 如果不存在实例行，则创建（按 02 号 §二：槽位版本链）
                    var newInstance = new StandardDirectoryFile
                    {
                        ConfigCode = stdFile.ConfigCode,
                        EnterpriseCode = payload.EnterpriseCode,
                        StandardCode = payload.StandardCode,
                        StageCode = payload.StageCode,
                        StandardFileCode = stdFile.Code,
                        FileName = stdFile.FileName,
                        FileType = stdFile.FileType,
                        FullPath = stdFile.FullPath, // 继承模板路径
                        StoragePath = outPath,
                        InstanceState = "filled",
                        VersionNumber = 1,
                        IsValid = 1
                    };
                    await _db.InsertAsync(newInstance);
                }

                await _db.Client.AsTenant().CommitTranAsync();
            }
            catch
            {
                await _db.Client.AsTenant().RollbackTranAsync();
                throw;
            }
        }

        private async Task<(string Markdown, List<string> Codes)> LoadEnterpriseOriginalDocsMarkdownAsync(string entCode, string stageCode)
        {
            // 召回该企业该阶段的所有已完成 Markdown 转换的文档
            var docs = (await _db.GetListAsync<StandardDirectoryFile>(x => 
                x.EnterpriseCode == entCode && 
                x.StageCode == stageCode && 
                x.MarkdownStatus == "completed" &&
                x.IsValid == 1)).Data ?? new List<StandardDirectoryFile>();

            var sb = new System.Text.StringBuilder();
            var codes = new List<string>();
            foreach (var doc in docs)
            {
                if (string.IsNullOrEmpty(doc.MarkdownPath)) continue;
                codes.Add(doc.Code);
                var (stream, _) = await _storage.DownloadAsync(doc.MarkdownPath);
                using var reader = new System.IO.StreamReader(stream);
                var md = await reader.ReadToEndAsync();
                sb.AppendLine($"--- DOCUMENT: {doc.FileName} ---");
                sb.AppendLine(md);
            }
            
            return (sb.ToString(), codes);
        }

        private CertPlatform.Shared.Fill.EnterpriseInfo MapToInfo(Enterprise? e) => e == null ? new CertPlatform.Shared.Fill.EnterpriseInfo() : new CertPlatform.Shared.Fill.EnterpriseInfo
        {
            Code = e.Code ?? "",
            Name = e.Name ?? "",
            ShortName = e.ShortName,
            CreditCode = e.CreditCode,
            LegalPerson = e.LegalPerson,
            Province = e.Province,
            City = e.City,
            Address = e.Address,
            IndustryType = e.IndustryType,
            EmployeeCount = e.EmployeeCount,
            CertScope = e.CertScope,
            ContactName = e.ContactName,
            ContactPhone = e.ContactPhone,
            ContactEmail = e.ContactEmail,
            EnterpriseNo = e.EnterpriseNo,
            ArchiveDate = e.ArchiveDate,
        };

        public Task OnTaskStateChangedAsync(YzhQueueTask task, string newStatus, string message)
        {
            return Task.CompletedTask;
        }

        public class NormalizationPayload
        {
            public string EnterpriseCode { get; set; } = string.Empty;
            public string StageCode { get; set; } = string.Empty;
            public string StandardCode { get; set; } = string.Empty;
        }
    }
}

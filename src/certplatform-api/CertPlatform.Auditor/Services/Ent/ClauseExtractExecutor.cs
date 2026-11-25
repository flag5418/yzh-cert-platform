using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CertPlatform.Auditor.Entities.Dir;
using CertPlatform.Shared.DocExtraction;
using CertPlatform.Shared.Domain.Clauses;
using CertPlatform.Shared.Domain.Scope;
using CertPlatform.Shared.Entities.Cert;
using CertPlatform.Shared.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Interfaces;
using YzhQueueTask = YZH.Core.Stand.Models.Queue.YzhQueueTask;

namespace CertPlatform.Auditor.Services.Ent
{
    /// <summary>
    /// <b>按标准提取条款执行器</b>（TaskType = <c>enterprise_clause_extract</c>，36 号 §八 P2）。
    ///
    /// <para><b>粒度 = 文档 × 标准 = 一个任务 = 一行产物</b>。同一份《质量手册》在 iso9001 与
    /// iso13485 下各跑一次（条款号体系不同，无法合并）⇒ 单标准可独立重跑、独立失败、独立留痕。</para>
    ///
    /// <para><b>★ 输入只有 Markdown</b>（<c>cert_enterprise_doc.MarkdownPath</c>）：
    /// PDF 是给人看的预览，Markdown 才是「企业原文」的机器可读形态。没 Markdown ⇒ 直接
    /// <c>skipped</c> 并写清原因，⛔ 不退回 PDF 解析（PDF 文本层常是乱码，静默出错比报错更贵）。</para>
    ///
    /// <para><b>★★ 重跑判据</b>：<c>cert_document_extraction.SourceSha256 == cert_enterprise_doc.Sha256</c>
    /// 且 <c>IsStale = 0</c> 且 <c>ExtractStatus = done</c> ⇒ <c>skipped</c>（不调模型，省钱且幂等）。
    /// 任一不满足 ⇒ 重跑。</para>
    ///
    /// <para><b>★ 单标准失败不阻断</b>：本执行器一次只处理一个标准，失败只写自己那一行；
    /// 同文档的其它标准由各自的队列任务独立跑 —— 这正是「按 (FileCode, StandardCode) 拆开」的目的。</para>
    ///
    /// <para><b>⚠️ 长文档分段</b>：一次塞不下时按段落边界切块、逐块调用、结果按条款号合并去重
    /// （保留 <c>Confidence</c> 更高的一条，<c>Excerpts</c> 取并集）。⛔ 不静默截断 ——
    /// 截断等于「后半本手册永远提不出来」，而且不会报错。</para>
    ///
    /// <para><b>★ 单例 + 根容器懒解析</b>：<c>QueueManager</c> 构造注入 <c>IEnumerable&lt;IYzhTaskExecutor&gt;</c>
    /// （含本类）⇒ 构造器注入 <c>IDbOrm</c> 会形成 DI 循环，必须运行时从作用域解析。</para>
    /// </summary>
    public class ClauseExtractExecutor : IYzhTaskExecutor
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ClauseExtractExecutor> _logger;

        /// <summary>
        /// 单块送模型的文档字符数上限。
        /// <para>★ 2026-11-23 二调：6000 字/块仍会在「第 2 段（1.0–5.1，含 4.4 过程清单 + 0.7 职责权限）」
        /// 撞 8192 输出上限被截断。降到 2800：让 4.4/0.7 这类密集章节拆成更小块，单块条款更少、
        /// 输出量再降一半，配合并发总时长仍控制在 1 分钟左右。</para>
        /// </summary>
        public const int ChunkChars = 2800;

        /// <summary>单次模型调用的条款 Markdown 分片上限。</summary>
        private const int StandardTextChars = 30_000;

        /// <summary>最多切几块（防误传超大文件把额度烧光；超出的部分记入 Message ⛔ 不静默丢）</summary>
        public const int MaxChunks = 16;

        private const int MaxStandardChunks = 8;
        private const int MaxModelCalls = 64;

        /// <summary>文档分段的 LLM 并发度。各段互不依赖（合并阶段才按批次序做并集），
        /// 并行后墙钟≈最慢单段；手册拆成约 10 段后并发 8，仍能压到 1 分钟以内。</summary>
        private const int MaxConcurrentExtractCalls = 8;

        public ClauseExtractExecutor(
            IServiceProvider serviceProvider,
            ILogger<ClauseExtractExecutor> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public string TaskType => EnterpriseDocQueue.TaskTypeExtract;

        public async Task<TaskExecutionResult> ExecuteAsync(YzhQueueTask task, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(task.Payload))
                return new TaskExecutionResult { Success = false, Message = "Payload 为空", Retryable = false };

            DocumentExtractPayload? payload;
            try
            {
                payload = JsonSerializer.Deserialize<DocumentExtractPayload>(task.Payload,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException ex)
            {
                return new TaskExecutionResult { Success = false, Message = $"Payload 解析失败：{ex.Message}", Retryable = false };
            }

            if (payload == null || string.IsNullOrWhiteSpace(payload.FileCode) || string.IsNullOrWhiteSpace(payload.StandardCode))
                return new TaskExecutionResult { Success = false, Message = "Payload 缺少 FileCode / StandardCode", Retryable = false };

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<IDbOrm>();
                var storage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();
                var core = scope.ServiceProvider.GetRequiredService<ICoreResourceQuery>();
                var llm = scope.ServiceProvider.GetRequiredService<LlmInvokeService>();
                var prompts = scope.ServiceProvider.GetRequiredService<DocExtractPromptService>();
                var store = scope.ServiceProvider.GetRequiredService<DocumentExtractionService>();
                var docSvc = scope.ServiceProvider.GetRequiredService<EnterpriseDocService>();

                // ★ 执行时读行（⛔ 不信入队快照）：排队期间可能被替换/删除
                var doc = (await db.GetOneIgnoreValidAsync<EnterpriseDoc>(x => x.Code == payload.FileCode)).Data;
                if (doc == null || doc.IsDeleted)
                {
                    _logger.LogWarning("[条款提取] 文件行不存在或已删除: {Code}", payload.FileCode);
                    return new TaskExecutionResult { Success = false, Message = "文件行不存在或已删除", Retryable = false };
                }

                // ── ① 前置：必须已有 Markdown ──
                if (doc.MarkdownStatus != "completed" || string.IsNullOrWhiteSpace(doc.MarkdownPath))
                {
                    var why = doc.MarkdownStatus == "unsupported"
                        ? "该文件格式无法转成 Markdown（图片/扫描件请先走 OCR 或人工录入），本次不提取"
                        : $"Markdown 未就绪（{doc.MarkdownStatus}），本次不提取";
                    await WriteSkipAsync(store, doc, payload.StandardCode, why, null);
                    await docSvc.RecomputeExtractStatusAsync(doc.Code!);
                    return new TaskExecutionResult { Success = true, Message = why, Retryable = false };
                }

                // ── ② 标准 ──
                var standard = await core.GetStandardByCodeAsync(payload.StandardCode);
                if (standard == null)
                {
                    var why = "认证标准不存在或已停用，请先在「认证计划」中修正标准";
                    await WriteSkipAsync(store, doc, payload.StandardCode, why, null);
                    await docSvc.RecomputeExtractStatusAsync(doc.Code!);
                    return new TaskExecutionResult { Success = false, Message = why, Retryable = false };
                }

                // 仅在实际模板、标准、模型和转换文本均已读取后才能判断缓存。
                var existing = await store.GetAsync(doc.Code!, payload.StandardCode);

                // ── ④ 条款清单（仅 clause_full 需要；模式提取不依赖条款清单） ──
                // 注意：不能在模式分支前解析并因“条款数为 0”直接跳过，
                // 否则过程/指导/记录这类本就不需要逐条款的文档会被整份丢弃。

                // ── ⑤ 读 Markdown ──
                string markdown;
                try
                {
                    var (stream, _) = await storage.DownloadAsync(doc.MarkdownPath!.TrimStart('/'));
                    using var reader = new StreamReader(stream, Encoding.UTF8, true);
                    markdown = await reader.ReadToEndAsync();
                }
                catch (Exception ex)
                {
                    var why = $"Markdown 读取失败：{ex.Message}";
                    await store.UpsertAsync(doc.Code!, payload.StandardCode, doc.EnterpriseCode,
                        null, Array.Empty<string>(), doc.Sha256 ?? string.Empty,
                        DocExtractPromptService.PromptCode, DocExtractPromptService.PromptVersion,
                        ExtractRowStatus.Failed, why, null, cancellationToken);
                    await docSvc.RecomputeExtractStatusAsync(doc.Code!);
                    _logger.LogError(ex, "[条款提取] Markdown 读取失败: {Path}", doc.MarkdownPath);
                    return new TaskExecutionResult { Success = false, Message = why, Retryable = true };
                }

                if (string.IsNullOrWhiteSpace(markdown))
                {
                    var why = "Markdown 内容为空，无可提取内容";
                    await store.UpsertAsync(doc.Code!, payload.StandardCode, doc.EnterpriseCode,
                        null, Array.Empty<string>(), doc.Sha256 ?? string.Empty,
                        DocExtractPromptService.PromptCode, DocExtractPromptService.PromptVersion,
                        ExtractRowStatus.Skipped, why, null, cancellationToken);
                    await docSvc.RecomputeExtractStatusAsync(doc.Code!);
                    return new TaskExecutionResult { Success = true, Message = why, Retryable = false };
                }

                // ── ⑤.1 画像路由：过程/记录/索引/审核 4 类走新提示词；手册逐条款暂留旧链 ──
                var extractionMode = EnterpriseDocExtractionRouter.Resolve(doc.DocProfileJson, doc.FileName);
                if (EnterpriseDocExtractionRouter.IsSkip(extractionMode))
                {
                    var why = "文档画像判定为无标准相关性（ExtractionMode=none），跳过提取";
                    await store.UpsertAsync(doc.Code!, payload.StandardCode, doc.EnterpriseCode,
                        null, Array.Empty<string>(), doc.Sha256 ?? string.Empty,
                        DocExtractPromptService.PromptCodeProfile, DocExtractPromptService.PromptVersion,
                        ExtractRowStatus.Skipped, why, null, cancellationToken);
                    await docSvc.RecomputeExtractStatusAsync(doc.Code!);
                    return new TaskExecutionResult { Success = true, Message = why, Retryable = false };
                }

                if (EnterpriseDocExtractionRouter.IsModePrompt(extractionMode))
                    return await ExecuteModePromptAsync(
                        db, llm, prompts, store, docSvc, doc, standard, existing,
                        markdown, extractionMode, payload.StandardCode, payload.Force, cancellationToken);

                // ── ④ 条款清单：只有 clause_full/legacy 逐条款链才需要 ──
                var (clauses, clauseNote) = await ResolveClausesAsync(db, core, standard);
                if (clauses.Count == 0)
                {
                    var why = string.IsNullOrEmpty(clauseNote)
                        ? $"标准【{standard.StandardCode}】下没有需要核对企业资料的条款"
                        : clauseNote;
                    await store.UpsertAsync(doc.Code!, payload.StandardCode, doc.EnterpriseCode,
                        null, Array.Empty<string>(), doc.Sha256 ?? string.Empty,
                        DocExtractPromptService.PromptCode, DocExtractPromptService.PromptVersion,
                        ExtractRowStatus.Skipped, why, null, cancellationToken);
                    await docSvc.RecomputeExtractStatusAsync(doc.Code!);
                    return new TaskExecutionResult { Success = true, Message = why, Retryable = false };
                }

                // 条款输入使用派生的条款 Markdown，不能退回原始标准 Markdown。
                var fullStandardMarkdown = string.Empty;
                string? standardReadError = null;
                var standardReadRetryable = false;
                try
                {
                    fullStandardMarkdown = await ClauseMarkdownInput.LoadAsync(storage, standard);
                    foreach (var clause in clauses)
                        ClauseMarkdownInput.ReadSection(fullStandardMarkdown, clause);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    standardReadError = ex.Message;
                    standardReadRetryable = ex is not InvalidOperationException;
                    _logger.LogWarning(ex, "[条款提取] 条款 Markdown 读取或校验失败: {Path}", standard.ClauseMarkdownPath);
                }

                List<string> standardChunks;
                try
                {
                    if (standardReadError != null)
                        throw new InvalidOperationException(standardReadError);
                    standardChunks = BuildStandardChunks(fullStandardMarkdown, StandardTextChars, MaxStandardChunks);
                }
                catch (InvalidOperationException ex)
                {
                    var why = standardReadError == null ? ex.Message : $"条款 Markdown 不可用：{standardReadError}";
                    await store.UpsertAsync(doc.Code!, payload.StandardCode, doc.EnterpriseCode,
                        null, Array.Empty<string>(), doc.Sha256 ?? string.Empty,
                        DocExtractPromptService.PromptCode, DocExtractPromptService.PromptVersion,
                        ExtractRowStatus.Failed, why, null, cancellationToken);
                    await docSvc.RecomputeExtractStatusAsync(doc.Code!);
                    return new TaskExecutionResult { Success = false, Message = why, Retryable = standardReadRetryable };
                }

                // ── ⑥ 提示词 + AI 参数 ──
                var (template, promptScope, _) = await prompts.ResolveAsync(standard.StandardCode, cancellationToken);
                var settings = await AiConnSettings.ReadAsync(db, cancellationToken);
                if (!settings.IsUsable)
                {
                    var why = "AI 参数未配置：请在「系统管理 → 系统参数」里填写 ai_api_key 与 ai_base_url";
                    await store.UpsertAsync(doc.Code!, payload.StandardCode, doc.EnterpriseCode,
                        null, Array.Empty<string>(), doc.Sha256 ?? string.Empty,
                        DocExtractPromptService.PromptCode, DocExtractPromptService.PromptVersion,
                        ExtractRowStatus.Failed, why, null, cancellationToken);
                    await docSvc.RecomputeExtractStatusAsync(doc.Code!);
                    return new TaskExecutionResult { Success = false, Message = why, Retryable = false };
                }

                // ── ⑦ 分段 → 逐段调用 → 合并 ──
                var chunks = SplitIntoChunks(markdown, ChunkChars, MaxChunks);
                var truncated = chunks.Sum(c => c.Length) < markdown.Length;
                var callCount = chunks.Count * standardChunks.Count;
                if (truncated || callCount > MaxModelCalls)
                {
                    var why = truncated
                        ? $"企业文档超过 {MaxChunks} 段，未进行不完整提取"
                        : $"文档与标准分片需调用 {callCount} 次，超过单任务上限 {MaxModelCalls} 次，未开始提取";
                    await store.UpsertAsync(doc.Code!, payload.StandardCode, doc.EnterpriseCode,
                        null, Array.Empty<string>(), doc.Sha256 ?? string.Empty,
                        DocExtractPromptService.PromptCode, DocExtractPromptService.PromptVersion,
                        ExtractRowStatus.Failed, why, null, cancellationToken);
                    await docSvc.RecomputeExtractStatusAsync(doc.Code!);
                    return new TaskExecutionResult { Success = false, Message = why, Retryable = false };
                }

                var clauseList = DocExtractPromptService.BuildClauseList(
                    clauses.Select(c => (c.ClauseNumber ?? string.Empty, c.Title ?? string.Empty)));
                var markdownHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(markdown))).ToLowerInvariant();
                var fingerprint = DocumentExtractionFingerprint.Compute(
                    "doc-clause-extract-v6", doc.Sha256 ?? "", doc.VersionNumber.ToString(),
                    markdownHash, standard.Code ?? "", standard.StandardCode ?? "",
                    standard.VersionYear.ToString(), standard.StandardName ?? "", clauseList,
                    fullStandardMarkdown, JsonSerializer.Serialize(clauses.Select(c => new
                    {
                        c.ClauseNumber, c.Title, c.Description
                    })), template, settings.BaseUrl ?? "", settings.Model ?? "",
                    ChunkChars.ToString(), MaxChunks.ToString(), StandardTextChars.ToString(),
                    MaxStandardChunks.ToString(), MaxModelCalls.ToString(), "0.1", "8192");
                if (!payload.Force
                    && existing is { IsDeleted: false, IsStale: false, ExtractStatus: ExtractRowStatus.Done }
                    && !string.IsNullOrEmpty(doc.Sha256)
                    && string.Equals(existing.SourceSha256, doc.Sha256, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(ReadFingerprint(existing.ContentJson), fingerprint, StringComparison.Ordinal))
                {
                    return new TaskExecutionResult { Success = true, Message = "提取输入未变化，沿用已有结果", Retryable = false };
                }

                var merged = new Dictionary<string, ExtractedClause>(StringComparer.Ordinal);
                var allowed = clauses.Select(c => (c.ClauseNumber ?? string.Empty).Trim())
                                     .Where(x => x.Length > 0)
                                     .ToHashSet(StringComparer.Ordinal);

                var failMessages = new List<string>();
                var droppedNumbers = 0;

                // ★ 2026-11-23：改并发。原串行「每段一次调用」让 5 段手册跑 3+ 分钟；
                //   各段互不依赖（合并阶段才按批次序做并集），并行后墙钟≈最慢单段。
                var responses = new LlmInvokeResponse?[callCount];
                var batchLabels = new string[callCount];

                await Parallel.ForEachAsync(
                    Enumerable.Range(0, callCount),
                    new ParallelOptions { MaxDegreeOfParallelism = MaxConcurrentExtractCalls, CancellationToken = cancellationToken },
                    async (batch, ct) =>
                    {
                        ct.ThrowIfCancellationRequested();
                        var i = batch / standardChunks.Count;
                        var j = batch % standardChunks.Count;
                        var standardMarkdown = standardChunks[j];
                        var batchLabel = $"文档第 {i + 1}/{chunks.Count} 段、标准第 {j + 1}/{standardChunks.Count} 段";
                        batchLabels[batch] = batchLabel;

                        var prompt = DocExtractPromptService.Render(
                            template,
                            $"{standard.StandardName}（{standard.StandardCode}）",
                            standard.StandardCode ?? string.Empty,
                            doc.FileName ?? string.Empty,
                            clauseList,
                            chunks[i]);

                        // 条款 Markdown 仅供理解条款，不是待提取的企业文档。
                        prompt += $"\n\n【标准条款 Markdown 分片 {j + 1}/{standardChunks.Count}，仅用于理解条款，不是待提取的文档内容】\n"
                               + "这是由条款表生成的内容，可能存在正文不完整或摘要；当前仅包含一个连续片段，可能不含完整条款或其上下文。"
                               + "企业文档里若没有对应证据，不要凭标准编造摘录。\n"
                               + standardMarkdown;

                        _logger.LogInformation(
                            "[条款提取] {File}×{Std} {Batch}：prompt {Chars} 字符（文档段 {DocChars}，条款 Markdown {StdChars}）",
                            doc.Code, standard.StandardCode, batchLabel,
                            prompt.Length, chunks[i].Length, standardMarkdown.Length);

                        responses[batch] = await llm.CompleteAsync(new LlmInvokeRequest
                        {
                            BaseUrl = settings.BaseUrl,
                            ApiKey = settings.ApiKey,
                            Model = settings.Model,
                            Temperature = 0.1f,
                            MaxTokens = 8192,
                            Prompt = prompt,
                            ForceJson = true,
                            TimeoutSeconds = 600,
                        });
                    });

                // 合并阶段仍按批次序串行做，保证结果确定、与旧版一致。
                for (var batch = 0; batch < callCount; batch++)
                {
                    var resp = responses[batch]!;
                    var batchLabel = batchLabels[batch];

                    if (!resp.Success || resp.Json == null)
                    {
                        // 单段失败不立刻放弃：其余段可能成功，先记下来
                        failMessages.Add($"{batchLabel}失败：{resp.Message}");
                        _logger.LogWarning("[条款提取] {File}×{Std} {Batch} 失败: {Msg}",
                            doc.Code, standard.StandardCode, batchLabel, resp.Message);
                        continue;
                    }

                    using (resp.Json)
                    {
                        if (resp.JsonRepaired
                            || string.Equals(resp.FinishReason, "length", StringComparison.OrdinalIgnoreCase)
                            || (resp.CompletionTokens.HasValue && resp.CompletionTokens.Value >= 8192))
                        {
                            failMessages.Add($"{batchLabel}输出被截断或疑似达到输出上限");
                            continue;
                        }
                        if (!TryProp(resp.Json.RootElement, "clauses", out var clauseArray)
                            || clauseArray.ValueKind != JsonValueKind.Array)
                        {
                            failMessages.Add($"{batchLabel}缺少有效的 clauses 数组");
                            continue;
                        }
                        var (got, dropped) = ReadClauses(resp.Json.RootElement, allowed);
                        droppedNumbers += dropped;
                        foreach (var c in got)
                        {
                            if (merged.TryGetValue(c.ClauseNumber, out var prev))
                            {
                                // 同一条款在多段命中 ⇒ 摘录取并集，置信度取高
                                foreach (var ex in c.Excerpts)
                                    if (!prev.Excerpts.Contains(ex, StringComparer.Ordinal)) prev.Excerpts.Add(ex);
                                if (c.Confidence > prev.Confidence)
                                {
                                    prev.Confidence = c.Confidence;
                                    if (!string.IsNullOrWhiteSpace(c.Anchor)) prev.Anchor = c.Anchor;
                                }
                            }
                            else
                            {
                                merged[c.ClauseNumber] = c;
                            }
                        }
                    }
                }

                // 全部段都失败时保留失败原因，不发布空产物。
                if (merged.Count == 0 && failMessages.Count == callCount)
                {
                    var why = "全部段落调用失败：" + string.Join("；", failMessages.Take(3));
                    await store.UpsertAsync(doc.Code!, payload.StandardCode, doc.EnterpriseCode,
                        null, Array.Empty<string>(), doc.Sha256 ?? string.Empty,
                        DocExtractPromptService.PromptCode, DocExtractPromptService.PromptVersion,
                        ExtractRowStatus.Failed, why, null, cancellationToken);
                    await docSvc.RecomputeExtractStatusAsync(doc.Code!);
                    return new TaskExecutionResult { Success = false, Message = why, Retryable = true };
                }

                // ── ⑧ 组装 JSON + 落库 ──
                var ordered = merged.Values
                    .OrderBy(c => ClauseSortKey(c.ClauseNumber))
                    .ThenBy(c => c.ClauseNumber, StringComparer.Ordinal)
                    .ToList();

                foreach (var clause in ordered)
                {
                    for (var index = 0; index < clause.Excerpts.Count; index++)
                    {
                        var text = clause.Excerpts[index];
                        var start = DocumentSourceLocator.LocateUnique(markdown, text);
                        clause.ExcerptLocations.Add(new ExtractedExcerptLocation
                        {
                            ExcerptIndex = index,
                            Start = start,
                            End = start.HasValue ? start.Value + text.Length : null,
                            Status = start.HasValue ? "matched"
                                : markdown.Contains(text, StringComparison.Ordinal) ? "ambiguous" : "unverified"
                        });
                    }

                    var section = DocumentManualSectionLocator.Locate(markdown, clause.ClauseNumber, clause.Anchor);
                    if (section.HasValue)
                    {
                        var text = markdown[section.Value.Start..section.Value.End];
                        if (!clause.Excerpts.Contains(text, StringComparer.Ordinal))
                        {
                            var index = clause.Excerpts.Count;
                            clause.Excerpts.Add(text);
                            clause.ExcerptLocations.Add(new ExtractedExcerptLocation
                            {
                                ExcerptIndex = index,
                                Start = section.Value.Start,
                                End = section.Value.End,
                                Status = "matched",
                                Kind = "section_candidate"
                            });
                        }
                    }
                }

                // ★ 入库前统一清洗：定位（LocateUnique）必须用原始文本做逐字匹配，
                // 清洗必须放在定位之后、序列化之前 —— 这样 Start/End 仍指向原文真实位置，
                // 而存入 ContentJson 的摘录/锚点/标题已无 Markdown/表格/排版噪声。
                foreach (var clause in ordered)
                {
                    clause.Title = CleanText(clause.Title);
                    clause.Anchor = CleanText(clause.Anchor);
                    for (var i = 0; i < clause.Excerpts.Count; i++)
                    {
                        clause.Excerpts[i] = CleanText(clause.Excerpts[i]);
                    }
                    clause.Excerpts.RemoveAll(string.IsNullOrEmpty);
                }

                var manualIndex = ManualIndexParser.Parse(markdown);
                var payloadDoc = new ExtractedDoc
                {
                    DocMeta = new ExtractedDocMeta
                    {
                        FileName = doc.FileName ?? string.Empty,
                        StandardCode = standard.StandardCode ?? string.Empty,
                        StandardName = standard.StandardName ?? string.Empty,
                        Model = settings.Model,
                        SourceSha256 = doc.Sha256 ?? string.Empty,
                        SourceVersion = doc.VersionNumber,
                        MarkdownSha256 = markdownHash,
                        ExtractionFingerprint = fingerprint,
                        ExtractedAt = DateTime.Now,
                        Chunks = chunks.Count,
                    },
                    Clauses = ordered,
                    ManualIndex = manualIndex,
                    EvidenceSources = manualIndex is null
                        ? new List<ManualIndexParser.ClauseEvidenceSource>()
                        : ManualIndexParser.BuildEvidenceSources(
                            ordered.Select(c => c.ClauseNumber), manualIndex),
                };

                var json = JsonSerializer.Serialize(payloadDoc, new JsonSerializerOptions
                {
                    // 中文不转 \uXXXX：ContentJson 要能被人直接读懂（排障时省一次反序列化）
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                });

                var notes = new List<string>();
                if (promptScope == "fallback")
                    notes.Add("库里没有保存过提取提示词，本次用的是代码内兜底正文，建议确认后保存");
                if (chunks.Count > 1) notes.Add($"长文档已分 {chunks.Count} 段提取");
                if (standardChunks.Count > 1) notes.Add($"条款 Markdown 分 {standardChunks.Count} 段处理，完整覆盖文本不代表语义召回完整");
                if (truncated) notes.Add($"文档超过 {MaxChunks}×{ChunkChars} 字符，超出部分未提取");
                if (droppedNumbers > 0) notes.Add($"丢弃 {droppedNumbers} 条编号不在清单内的输出（模型自造编号）");
                var unverifiedCount = ordered.Sum(c => c.ExcerptLocations.Count(x => x.Status != "matched"));
                if (unverifiedCount > 0) notes.Add($"{unverifiedCount} 条摘录未能唯一匹配转换原文，需人工核实");
                var indexOverflow = string.Join(",", ordered.Select(c => c.ClauseNumber).Distinct(StringComparer.Ordinal)).Length > 500;
                if (indexOverflow) notes.Add("条款关联较多，反查使用完整提取内容");
                if (failMessages.Count > 0) notes.Add($"{failMessages.Count} 段失败：" + string.Join("；", failMessages.Take(2)));
                if (!string.IsNullOrEmpty(clauseNote)) notes.Add(clauseNote);

                var message = notes.Count == 0
                    ? $"提取 {ordered.Count} 条条款"
                    : $"提取 {ordered.Count} 条条款；" + string.Join("；", notes);

                var status = truncated || failMessages.Count > 0
                    ? ExtractRowStatus.Failed
                    : ExtractRowStatus.Done;
                var currentDoc = (await db.GetOneIgnoreValidAsync<EnterpriseDoc>(x => x.Code == payload.FileCode)).Data;
                if (currentDoc == null || currentDoc.IsDeleted || currentDoc.VersionNumber != doc.VersionNumber
                    || !string.Equals(currentDoc.Sha256, doc.Sha256, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(currentDoc.MarkdownPath, doc.MarkdownPath, StringComparison.Ordinal)
                    || currentDoc.MarkdownStatus != "completed")
                    return new TaskExecutionResult
                    {
                        Success = false,
                        Message = "提取期间文件版本或转换状态发生变化，未发布本次结果，请重新提取",
                        Retryable = true
                    };
                var (ok, err) = await store.UpsertAsync(
                    doc.Code!, payload.StandardCode, doc.EnterpriseCode,
                    json, ordered.Select(c => c.ClauseNumber).ToList(),
                    doc.Sha256 ?? string.Empty,
                    DocExtractPromptService.PromptCode, DocExtractPromptService.PromptVersion,
                    status, message, null, cancellationToken);

                if (!ok)
                {
                    _logger.LogError("[条款提取] 产物落库失败 {File}×{Std}: {Err}", doc.Code, standard.StandardCode, err);
                    return new TaskExecutionResult { Success = false, Message = $"产物落库失败：{err}", Retryable = true };
                }

                await docSvc.RecomputeExtractStatusAsync(doc.Code!);

                _logger.LogInformation("[条款提取] {Status} {File}×{Std}：{N} 条（{Chunks} 段，丢弃 {Drop}）",
                    status, doc.Code, standard.StandardCode, ordered.Count, chunks.Count, droppedNumbers);

                return new TaskExecutionResult
                {
                    Success = status == ExtractRowStatus.Done,
                    Message = message,
                    Retryable = status == ExtractRowStatus.Failed && !truncated,
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[条款提取] 任务异常: {TaskCode}", task.Code);
                return new TaskExecutionResult { Success = false, Message = ex.Message, Retryable = true };
            }
        }

        /// <summary>
        /// 过程/记录/索引/审核 4 类模式提取：一次调用、原样保留模型 JSON，写入
        /// <c>ContentJson.ModeOutput</c>。⛔ 不把模型输出硬塞进 <c>Clauses[]</c>。
        /// </summary>
        private async Task<TaskExecutionResult> ExecuteModePromptAsync(
            IDbOrm db,
            LlmInvokeService llm,
            DocExtractPromptService prompts,
            DocumentExtractionService store,
            EnterpriseDocService docSvc,
            EnterpriseDoc doc,
            ISOStandard standard,
            DocumentExtraction? existing,
            string markdown,
            string extractionMode,
            string standardCode,
            bool force,
            CancellationToken cancellationToken)
        {
            var promptCode = EnterpriseDocExtractionRouter.PromptCodeForMode(extractionMode);
            if (promptCode.Length == 0)
                return new TaskExecutionResult { Success = false, Message = $"未知提取模式：{extractionMode}", Retryable = false };

            var (template, promptScope, _) = await prompts.ResolveByCodeAsync(promptCode, standard.StandardCode, cancellationToken);
            if (string.IsNullOrWhiteSpace(template))
            {
                var why = $"提示词 {promptCode} 未落库，拒绝提取（跑旧提示词会污染结果）";
                await store.UpsertAsync(doc.Code!, standardCode, doc.EnterpriseCode,
                    null, Array.Empty<string>(), doc.Sha256 ?? string.Empty,
                    promptCode, DocExtractPromptService.PromptVersion,
                    ExtractRowStatus.Failed, why, null, cancellationToken);
                await docSvc.RecomputeExtractStatusAsync(doc.Code!);
                return new TaskExecutionResult { Success = false, Message = why, Retryable = false };
            }

            var settings = await AiConnSettings.ReadAsync(db, cancellationToken);
            if (!settings.IsUsable)
            {
                var why = "AI 参数未配置：请在「系统管理 → 系统参数」里填写 ai_api_key 与 ai_base_url";
                await store.UpsertAsync(doc.Code!, standardCode, doc.EnterpriseCode,
                    null, Array.Empty<string>(), doc.Sha256 ?? string.Empty,
                    promptCode, DocExtractPromptService.PromptVersion,
                    ExtractRowStatus.Failed, why, null, cancellationToken);
                await docSvc.RecomputeExtractStatusAsync(doc.Code!);
                return new TaskExecutionResult { Success = false, Message = why, Retryable = false };
            }

            // 记录类要逐行枚举表格（33 行培训/19 行台账），4000 输出额度会被截断 ⇒ 判 failed 并把旧结果作废。
            // 手册章节模式要逐章给摘要+要点（手册可能 10+ 章），同样给更大额度。
            var maxOut = extractionMode is EnterpriseDocExtractionRouter.RecordProfile
                    or EnterpriseDocExtractionRouter.ManualChapter
                ? 8000 : 4000;

            var markdownHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(markdown))).ToLowerInvariant();
            var fingerprint = DocumentExtractionFingerprint.Compute(
                "doc-mode-extract-v1", doc.Sha256 ?? "", doc.VersionNumber.ToString(),
                markdownHash, standard.Code ?? "", standard.StandardCode ?? "",
                standard.VersionYear.ToString(), standard.StandardName ?? "",
                extractionMode, doc.DocProfileJson ?? "", template,
                settings.BaseUrl ?? "", settings.Model ?? "", "0.2", maxOut.ToString());

            if (!force
                && existing is { IsDeleted: false, IsStale: false, ExtractStatus: ExtractRowStatus.Done }
                && !string.IsNullOrEmpty(doc.Sha256)
                && string.Equals(existing.SourceSha256, doc.Sha256, StringComparison.OrdinalIgnoreCase)
                && string.Equals(ReadFingerprint(existing.ContentJson), fingerprint, StringComparison.Ordinal))
            {
                return new TaskExecutionResult { Success = true, Message = "提取输入未变化，沿用已有结果", Retryable = false };
            }

            var prompt = DocExtractPromptService.RenderMode(template, doc.DocProfileJson ?? "{}", markdown);
            _logger.LogInformation("[模式提取] {File}×{Std} mode={Mode} prompt={Chars} 字符",
                doc.Code, standard.StandardCode, extractionMode, prompt.Length);

            var resp = await llm.CompleteAsync(new LlmInvokeRequest
            {
                BaseUrl = settings.BaseUrl,
                ApiKey = settings.ApiKey,
                Model = settings.Model,
                Temperature = 0.2f,
                MaxTokens = maxOut,
                Prompt = prompt,
                ForceJson = true,
                TimeoutSeconds = 600,
            });

            if (!resp.Success || resp.Json == null)
            {
                var why = $"模型调用失败：{resp.Message}";
                await store.UpsertAsync(doc.Code!, standardCode, doc.EnterpriseCode,
                    null, Array.Empty<string>(), doc.Sha256 ?? string.Empty,
                    promptCode, DocExtractPromptService.PromptVersion,
                    ExtractRowStatus.Failed, why, null, cancellationToken);
                await docSvc.RecomputeExtractStatusAsync(doc.Code!);
                return new TaskExecutionResult { Success = false, Message = why, Retryable = true };
            }

            JsonElement modeOutput;
            using (resp.Json)
            {
                // ⚠️ JsonRepaired 不再算失败：模型偶尔把 JSON 包一层代码块/多几句解释，
                //    修复后内容完整，判失败会把上一份好结果作废（2026-11-16 实测丢过 2 份记录）。
                //    真正的截断只认 FinishReason=length 或 token 打满。
                if (resp.JsonRepaired)
                    _logger.LogInformation("[模式提取] {File} 的 JSON 需要修复后解析（内容完整，按成功处理）", doc.Code);

                if (string.Equals(resp.FinishReason, "length", StringComparison.OrdinalIgnoreCase)
                    || (resp.CompletionTokens.HasValue && resp.CompletionTokens.Value >= maxOut))
                {
                    var why = "模型输出被截断或疑似达到输出上限";
                    await store.UpsertAsync(doc.Code!, standardCode, doc.EnterpriseCode,
                        null, Array.Empty<string>(), doc.Sha256 ?? string.Empty,
                        promptCode, DocExtractPromptService.PromptVersion,
                        ExtractRowStatus.Failed, why, null, cancellationToken);
                    await docSvc.RecomputeExtractStatusAsync(doc.Code!);
                    return new TaskExecutionResult { Success = false, Message = why, Retryable = true };
                }

                if (resp.Json.RootElement.ValueKind != JsonValueKind.Object)
                {
                    var why = "模型输出不是 JSON 对象";
                    await store.UpsertAsync(doc.Code!, standardCode, doc.EnterpriseCode,
                        null, Array.Empty<string>(), doc.Sha256 ?? string.Empty,
                        promptCode, DocExtractPromptService.PromptVersion,
                        ExtractRowStatus.Failed, why, null, cancellationToken);
                    await docSvc.RecomputeExtractStatusAsync(doc.Code!);
                    return new TaskExecutionResult { Success = false, Message = why, Retryable = true };
                }

                // 审核/评审只删系统结论字段；过程/记录/索引不动原文
                modeOutput = DocExtractGuard.Sanitize(extractionMode, resp.Json.RootElement);

                // ★ 手册章节模式：条款号由代码从手册目录（ManualIndexParser）确定性挂载，
                //   ⛔ 不采信模型自报的条款号（那是串条款/漏条款的根源）。
                if (extractionMode == EnterpriseDocExtractionRouter.ManualChapter)
                {
                    var node = JsonNode.Parse(modeOutput.GetRawText())?.AsObject();
                    if (node != null)
                    {
                        AttachManualChapterClauses(node, markdown);
                        modeOutput = JsonSerializer.SerializeToElement(node);
                    }
                }

                // 文件控制类：模型只交元数据，目录条目/总数由代码从 Markdown 表格解析，避免漏行/算错/输出过长
                if (extractionMode == EnterpriseDocExtractionRouter.DocumentControl)
                {
                    var node = JsonNode.Parse(modeOutput.GetRawText())?.AsObject();
                    if (node != null)
                    {
                        DocControlTableParser.Populate(node, markdown);
                        modeOutput = JsonSerializer.SerializeToElement(node);
                    }
                }

                // 表格数据识别：RowCount 一律由代码按「有数据的行」重算，不采信模型自报条数；
                //   空行/占位行/合计备注行不计记录；摘要出现「等 X」但只有 1 条时只告警、不改原文。
                modeOutput = RecordQualityGuard.Apply(extractionMode, modeOutput);

                // PDCA 定位 + 部门归集：代码按条款算，模型不参与
                modeOutput = DocPdcaClassifier.Apply(extractionMode, modeOutput);

                // 指导书证据指针必须能在原文找到；防止把别的文档记录串进来
                modeOutput = DocExtractGuard.ValidateEvidenceAgainstText(extractionMode, modeOutput, markdown);
            }

            var payloadDoc = new ExtractedDoc
            {
                DocMeta = new ExtractedDocMeta
                {
                    FileName = doc.FileName ?? string.Empty,
                    StandardCode = standard.StandardCode ?? string.Empty,
                    StandardName = standard.StandardName ?? string.Empty,
                    Model = settings.Model,
                    SourceSha256 = doc.Sha256 ?? string.Empty,
                    SourceVersion = doc.VersionNumber,
                    MarkdownSha256 = markdownHash,
                    ExtractionFingerprint = fingerprint,
                    ExtractionMode = extractionMode,
                    PromptCode = promptCode,
                    PromptScope = promptScope,
                    ExtractedAt = DateTime.Now,
                    Chunks = 1,
                },
                ExtractionMode = extractionMode,
                ModeOutput = modeOutput,
                Clauses = new List<ExtractedClause>(),
            };

            var json = JsonSerializer.Serialize(payloadDoc, new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            });

            // 提取期间文件被替换/回滚 ⇒ 不发布本次结果
            var currentDoc = (await db.GetOneIgnoreValidAsync<EnterpriseDoc>(x => x.Code == doc.Code)).Data;
            if (currentDoc == null || currentDoc.IsDeleted || currentDoc.VersionNumber != doc.VersionNumber
                || !string.Equals(currentDoc.Sha256, doc.Sha256, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(currentDoc.MarkdownPath, doc.MarkdownPath, StringComparison.Ordinal)
                || currentDoc.MarkdownStatus != "completed")
                return new TaskExecutionResult
                {
                    Success = false,
                    Message = "提取期间文件版本或转换状态发生变化，未发布本次结果，请重新提取",
                    Retryable = true
                };

            var message = $"按 {extractionMode} 提取完成" + (promptScope == "stored" ? "（库内提示词）" : $"（{promptScope}）");
            var (ok, err) = await store.UpsertAsync(
                doc.Code!, standardCode, doc.EnterpriseCode,
                json, Array.Empty<string>(), doc.Sha256 ?? string.Empty,
                promptCode, DocExtractPromptService.PromptVersion,
                ExtractRowStatus.Done, message, null, cancellationToken);

            if (!ok)
            {
                _logger.LogError("[模式提取] 产物落库失败 {File}×{Std}: {Err}", doc.Code, standard.StandardCode, err);
                return new TaskExecutionResult { Success = false, Message = $"产物落库失败：{err}", Retryable = true };
            }

            await docSvc.RecomputeExtractStatusAsync(doc.Code!);
            // 旧画像常常没有 OwnerDept；模式产物已有部门时补回画像，页面/按部门检索才可用
            await docSvc.MergeModeOutputProfileAsync(doc, extractionMode, modeOutput);
            _logger.LogInformation("[模式提取] {Status} {File}×{Std} mode={Mode}",
                ExtractRowStatus.Done, doc.Code, standard.StandardCode, extractionMode);

            return new TaskExecutionResult { Success = true, Message = message, Retryable = false };
        }

        // ========================================================
        // 手册章节模式：手册章号 → ISO 条款号的确定性挂载
        // ========================================================

        /// <summary>章节号头部：<c>4.1</c> / <c>7.1.1</c>（从「4.1理解组织及其环境」这类串里取号）</summary>
        private static readonly Regex ChapterHead = new Regex(@"^(\d+(?:\.\d+){0,2})", RegexOptions.Compiled);

        /// <summary>
        /// 手册章节模式（<c>manual_chapter</c>）专用：把模型按章节组织的内容（<c>Chapters[]</c>）
        /// 按手册「0.0 目录与标准条款对应表」映射为 ISO 条款号，<b>确定性挂载</b>到
        /// <c>ModeOutput.PrimaryClauses</c>，供下游投影（<see cref="ModeOutputProjector.ClauseNumbers"/>）
        /// 与召回（RM1 条款轴）读取。
        ///
        /// <para><b>★ 为什么由代码挂载</b>：手册章号与 ISO 条款号<b>不是一一对应</b>
        /// （如实测 <c>4.5 → 7.5.1</c>、<c>9.1.4 → 空</c>），只有手册自己的目录表知道这层映射。
        /// 模型自报条款号正是「串条款 / 漏条款」的根源 ⇒ ⛔ 一律不采信，只信目录表。</para>
        ///
        /// <para><b>章号取法</b>：<c>Section</c>/<c>Chapter</c>/<c>Clause</c>/<c>Number</c> 任一字段；
        /// 取不到时按「<c>4.4.2 → 4.4 → 4</c>」逐级向上找到最近的目录映射（只取<b>最具体</b>的一级）。</para>
        /// </summary>
        private static void AttachManualChapterClauses(JsonObject node, string markdown)
        {
            var index = ManualIndexParser.Parse(markdown);
            if (index == null || index.TocMapping.Count == 0) return;

            // 章号 → ISO 条款号（同一章号只认第一次；目录表里 IsoClause 为空的条目跳过）
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var row in index.TocMapping)
            {
                var section = (row.Section ?? string.Empty).Trim().TrimEnd('.');
                var clause = (row.IsoClause ?? string.Empty).Trim();
                if (section.Length == 0 || clause.Length == 0) continue;
                if (!map.ContainsKey(section)) map[section] = clause;
            }
            if (map.Count == 0) return;

            var chapters = node["Chapters"] as JsonArray;
            if (chapters == null) return;

            var clauses = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            void Consider(string? rawSection)
            {
                var section = (rawSection ?? string.Empty).Trim().TrimEnd('.');
                if (section.Length == 0) return;
                var m = ChapterHead.Match(section);
                if (m.Success) section = m.Groups[1].Value;

                // 逐级向上取「最近的目录映射」：4.4.2 → 4.4 → 4，命中即止（最具体优先）
                var cur = section;
                while (cur.Length > 0)
                {
                    if (map.TryGetValue(cur, out var clause))
                    {
                        if (seen.Add(clause)) clauses.Add(clause);
                        return;
                    }
                    var dot = cur.LastIndexOf('.');
                    if (dot <= 0) return;
                    cur = cur[..dot];
                }
            }

            foreach (var item in chapters)
            {
                switch (item)
                {
                    case JsonObject co:
                        Consider(FirstString(co, "Section", "Chapter", "Clause", "Number", "SectionNo"));
                        break;
                    case JsonValue v when v.TryGetValue<string>(out var s):
                        Consider(s);
                        break;
                }
            }

            if (clauses.Count == 0) return;

            var outArr = new JsonArray();
            foreach (var c in clauses) outArr.Add(c);
            node["PrimaryClauses"] = outArr;
            node.Remove("SupportClauses");
        }

        private static string? FirstString(JsonObject obj, params string[] names)
        {
            foreach (var name in names)
            {
                if (obj[name] is JsonValue v && v.TryGetValue<string>(out var s)
                    && !string.IsNullOrWhiteSpace(s)) return s;
            }
            return null;
        }

        // ========================================================
        // 条款清单
        // ========================================================

        /// <summary>
        /// 取「需要核对企业资料」的条款：叶子条款 ∩ <c>DocCheckMode ≠ none</c>。
        ///
        /// <para><b>★ 为什么要按 <c>DocCheckMode</c> 过滤</b>：清单里的每一条都是模型的「可选答案」。
        /// 把不需要资料核对的条款塞进去，模型会为它们硬凑内容 ⇒ 锚点率崩、审核员无法复核。</para>
        ///
        /// <para><b>⚠️ 该标准一条 <c>DocCheckMode</c> 都没配</b> ⇒ 退回「全部叶子条款」并在
        /// <c>Message</c> 里说清楚（⛔ 不静默返回空清单：那会让用户以为「这份文档什么都没提到」，
        /// 实际是「属性还没配」——两者的处置完全不同）。</para>
        /// </summary>
        private static async Task<(List<ISOClause> Clauses, string? Note)> ResolveClausesAsync(
            IDbOrm db, ICoreResourceQuery core, ISOStandard standard)
        {
            var all = await core.GetClausesAsync(standard.Code);
            if (all.Count == 0)
                return (new List<ISOClause>(), $"标准【{standard.StandardCode}】还没有条款数据，请先在标准库维护条款");

            var parentCodes = all.Where(c => !string.IsNullOrEmpty(c.ParentCode))
                                 .Select(c => c.ParentCode!)
                                 .ToHashSet(StringComparer.Ordinal);
            var leaves = all.Where(c => !parentCodes.Contains(c.Code)).ToList();

            // ⚠️ SqlSugar 对闭包集合 Contains 翻译不可靠 ⇒ 全取 + 内存过滤
            var attrs = await db.Client.Queryable<ClauseAttrRow>()
                .Where(x => x.StandardCode == standard.StandardCode
                            && x.AttrCode == "DocCheckMode"
                            && !x.IsDeleted && x.IsValid == 1)
                .ToListAsync() ?? new List<ClauseAttrRow>();
            var leafCodes = leaves.Select(c => c.Code).ToHashSet(StringComparer.Ordinal);
            attrs = attrs.Where(a => !string.IsNullOrEmpty(a.ClauseCode) && leafCodes.Contains(a.ClauseCode)).ToList();

            if (attrs.Count == 0)
            {
                return (Order(leaves),
                    $"标准【{standard.StandardCode}】的条款还没配 DocCheckMode，本次按全部叶子条款提取（建议先跑条款属性预生成）");
            }

            var groups = attrs.GroupBy(a => a.ClauseCode, StringComparer.Ordinal).ToList();
            var duplicates = groups.Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            var modeByCode = groups.Where(g => g.Count() == 1)
                .ToDictionary(g => g.Key, g => (g.Single().AttrValue ?? string.Empty).Trim().ToLowerInvariant(), StringComparer.Ordinal);

            // 未配 DocCheckMode 的条款按「要查」处理（保守：宁可多查，不静默漏条款）
            var kept = leaves.Where(c =>
            {
                var mode = modeByCode.GetValueOrDefault(c.Code ?? string.Empty);
                return mode == null || mode != "none";
            }).ToList();

            return (Order(kept), duplicates.Count == 0 ? null
                : $"部分条款的 DocCheckMode 存在重复，已保留这些条款待核实：{string.Join(",", duplicates)}");
        }

        private static List<ISOClause> Order(List<ISOClause> list)
            => list.OrderBy(c => c.Sort)
                   .ThenBy(c => c.ClauseNumber, StringComparer.Ordinal)
                   .ToList();

        // ========================================================
        // 模型输出 → ExtractedClause
        // ========================================================

        /// <summary>
        /// 模型输出的条款号 → 标准清单里的规范条款号。
        /// <para><b>规则</b>：先 <see cref="ClauseRefs.NormalizeNumber"/> 抽纯编号；精确命中优先；
        /// 未命中时逐级去掉最后一段，回退到最近的长父级（如手册把 4.1 写成 4.1.1 ⇒ 归到 4.1）。
        /// ⛔ 不允许把完全不相干的编号猜成某个条款。</para>
        /// </summary>
        private static string ResolveAllowedNumber(string number, HashSet<string> allowed)
        {
            if (number.Length == 0) return string.Empty;
            if (allowed.Contains(number)) return number;

            var candidate = number;
            while (true)
            {
                var dot = candidate.LastIndexOf('.');
                if (dot <= 0) return string.Empty;
                candidate = candidate[..dot];
                if (allowed.Contains(candidate)) return candidate;
            }
        }

        /// <summary>
        /// 读 <c>{ clauses: [...] }</c>。<b>编号不在清单内的一律丢弃</b>（⛔ 模型自造编号会污染下游
        /// 「按条款查证据」的关联，而且不报错）。
        /// </summary>
        private static (List<ExtractedClause> Got, int Dropped) ReadClauses(
            JsonElement root, HashSet<string> allowed)
        {
            var got = new List<ExtractedClause>();
            var dropped = 0;

            if (root.ValueKind != JsonValueKind.Object) return (got, dropped);

            // 顶层键也按大小写不敏感找（模型偶发写 "Clauses"）
            JsonElement arr = default;
            var hasArr = false;
            foreach (var p in root.EnumerateObject())
            {
                if (string.Equals(p.Name, "clauses", StringComparison.OrdinalIgnoreCase))
                {
                    arr = p.Value; hasArr = true; break;
                }
            }
            if (!hasArr || arr.ValueKind != JsonValueKind.Array) return (got, dropped);

            foreach (var item in arr.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) { dropped++; continue; }

                var num = ClauseRefs.NormalizeNumber(Str(item, "ClauseNumber"));
                var canonical = ResolveAllowedNumber(num, allowed);
                if (canonical.Length == 0) { dropped++; continue; }

                var c = new ExtractedClause
                {
                    ClauseNumber = canonical,
                    Title = Str(item, "Title").Trim(),
                    Anchor = Str(item, "Anchor").Trim(),
                    Confidence = Num(item, "Confidence"),
                };

                if (TryProp(item, "Excerpts", out var ex) && ex.ValueKind == JsonValueKind.Array)
                {
                    foreach (var e in ex.EnumerateArray())
                    {
                        if (e.ValueKind != JsonValueKind.String) continue;
                        var s = (e.GetString() ?? string.Empty).Trim();
                        if (s.Length > 0) c.Excerpts.Add(s);
                    }
                }

                // 兜底：模型只给了 Excerpts 没给 Anchor ⇒ 用第一条摘录当前 40 字（锚点率判据靠它）
                if (c.Anchor.Length == 0 && c.Excerpts.Count > 0)
                {
                    var first = c.Excerpts[0];
                    c.Anchor = first.Length <= 40 ? first : first[..40];
                }

                got.Add(c);
            }

            return (got, dropped);
        }

        /// <summary>
        /// <b>入库前清洗</b>：摘录/锚点会被下游「审核发现 → 证据生成」直接逐字复用与定位，
        /// 不再二次加工 ⇒ 必须在写 <see cref="DocumentExtraction.ContentJson"/> 前把
        /// Markdown / 表格 / 排版噪声一次性清掉（⛔ 脏数据入库 = 后续锚点匹配失败 + 证据串味）。
        /// </summary>
        private static string CleanText(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

            var s = raw!;

            // ① 表格噪声：竖线分隔符 + 职责三角 ▲△（《职能分配表》/《目录对应表》漏进摘录）
            s = s.Replace("▲", string.Empty)
                 .Replace("△", string.Empty)
                 .Replace("|", string.Empty);

            // ② Markdown 强调/代码符号 + HTML 换行标签
            s = s.Replace("**", string.Empty)
                 .Replace("`", string.Empty)
                 .Replace("<br>", " ", StringComparison.OrdinalIgnoreCase)
                 .Replace("<br/>", " ", StringComparison.OrdinalIgnoreCase)
                 .Replace("<br />", " ", StringComparison.OrdinalIgnoreCase);

            // ③ 每行去首尾空白、丢弃空行（模型输出常见「摘录内部换行」）
            var lines = s.Split('\n')
                         .Select(l => l.Trim())
                         .Where(l => l.Length > 0);
            s = string.Join('\n', lines);

            // ④ 压缩连续空白（制表符/多空格 → 单空格）、去零宽字符（BOM/ZWNBSP）
            s = Regex.Replace(s, @"[ \t]{2,}", " ");
            s = s.Replace("\u200B", string.Empty)
                 .Replace("\uFEFF", string.Empty);

            return s.Trim();
        }

        /// <summary>取字段（大小写不敏感）—— 见 <see cref="Str"/> 的注释</summary>
        private static bool TryProp(JsonElement obj, string name, out JsonElement value)
        {
            value = default;
            if (obj.ValueKind != JsonValueKind.Object) return false;
            if (obj.TryGetProperty(name, out value)) return true;
            foreach (var p in obj.EnumerateObject())
            {
                if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = p.Value;
                    return true;
                }
            }
            return false;
        }

        private static string? ReadFingerprint(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (!TryProp(doc.RootElement, "DocMeta", out var meta)
                    || meta.ValueKind != JsonValueKind.Object) return null;
                return Str(meta, "ExtractionFingerprint");
            }
            catch (JsonException) { return null; }
        }

        /// <summary>
        /// 取字段（<b>大小写不敏感</b>）：模型偶发把 <c>ClauseNumber</c> 写成 <c>clauseNumber</c>，
        /// 严格匹配会整条丢弃且不报错（表现为「明明有内容却提取 0 条」）。
        /// </summary>
        private static string Str(JsonElement obj, string name)
        {
            if (TryProp(obj, name, out var v) && v.ValueKind == JsonValueKind.String)
                return v.GetString() ?? string.Empty;
            return string.Empty;
        }

        private static double Num(JsonElement obj, string name)
        {
            if (!TryProp(obj, name, out var v)) return 0;

            if (v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d))
                return d > 1 ? 1 : (d < 0 ? 0 : d);
            if (v.ValueKind == JsonValueKind.String && double.TryParse(v.GetString(), out var s))
                return s > 1 ? 1 : (s < 0 ? 0 : s);
            return 0;
        }

        // ========================================================
        // 分段
        // ========================================================

        /// <summary>切分条款 Markdown；内容缺失或无法在预算内完整覆盖时拒绝提取。</summary>
        public static List<string> BuildStandardChunks(string text, int chunkChars, int maxChunks)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new InvalidOperationException("条款 Markdown 缺失或为空，无法按条款提取");
            if (chunkChars <= 0 || maxChunks <= 0)
                throw new InvalidOperationException("条款 Markdown 分片预算无效");

            var chunks = SplitIntoChunks(text, chunkChars, maxChunks);
            if (chunks.Sum(chunk => chunk.Length) != text.Length)
                throw new InvalidOperationException($"条款 Markdown 超过 {maxChunks} 段，未进行不完整提取");
            return chunks;
        }

        /// <summary>
        /// 按<b>标题边界</b>切块（⛔ 不硬切字符：硬切会把一句话劈开，模型摘录的锚点就对不上原文）。
        /// <para>先按 Markdown 标题（# / ## / ### / ####）切成「节」，再贪婪装入不超过
        /// <paramref name="chunkChars"/> 的块；单节超长时按换行兜底切。质量手册这类大文档因此
        /// 每个大章节自成一块，单块命中条款更少，输出不会撞 max_tokens 上限。</para>
        /// <para>块数上限 <paramref name="maxChunks"/>：超出的尾部<b>不送模型</b>，由调用方在
        /// <c>Message</c> 里如实说明（⛔ 不静默丢）。</para>
        /// </summary>
        public static List<string> SplitIntoChunks(string text, int chunkChars, int maxChunks)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(text)) return result;
            if (chunkChars <= 0) chunkChars = 30000;
            if (maxChunks <= 0) maxChunks = 1;

            if (text.Length <= chunkChars)
            {
                result.Add(text);
                return result;
            }

            var current = new StringBuilder(text.Length);
            foreach (var section in SplitByHeadings(text))
            {
                if (result.Count >= maxChunks) break;

                if (section.Length > chunkChars)
                {
                    if (current.Length > 0)
                    {
                        result.Add(current.ToString());
                        current.Clear();
                        if (result.Count >= maxChunks) break;
                    }
                    foreach (var piece in SplitOversized(section, chunkChars))
                    {
                        if (result.Count >= maxChunks) break;
                        result.Add(piece);
                    }
                    continue;
                }

                if (current.Length > 0 && current.Length + section.Length > chunkChars)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                current.Append(section);
            }
            if (current.Length > 0 && result.Count < maxChunks)
                result.Add(current.ToString());
            return result;
        }

        /// <summary>按 Markdown 标题行（行首 1–4 个 # 后跟空格/制表符）把文本切成「节」。</summary>
        private static List<string> SplitByHeadings(string text)
        {
            var sections = new List<string>();
            var sectionStart = 0;
            var idx = 0;
            var n = text.Length;
            while (idx < n)
            {
                var lineEnd = text.IndexOf('\n', idx);
                if (lineEnd < 0) lineEnd = n;
                var lineLength = lineEnd - idx;
                if (IsHeadingLine(text, idx, lineLength) && idx > sectionStart)
                {
                    sections.Add(text.Substring(sectionStart, idx - sectionStart));
                    sectionStart = idx;
                }
                idx = lineEnd + 1;
            }
            if (sectionStart < n)
                sections.Add(text.Substring(sectionStart));
            return sections;
        }

        private static bool IsHeadingLine(string text, int start, int length)
        {
            var hash = 0;
            while (hash < length && hash < 4 && text[start + hash] == '#') hash++;
            if (hash < 1) return false;
            return hash < length && (text[start + hash] == ' ' || text[start + hash] == '\t');
        }

        /// <summary>单节超过预算时按换行边界兜底切（保留 UTF-16 代理对，避免拆坏字符）。</summary>
        private static List<string> SplitOversized(string text, int chunkChars)
        {
            var result = new List<string>();
            var offset = 0;
            while (offset < text.Length)
            {
                var length = Math.Min(chunkChars, text.Length - offset);
                if (offset + length < text.Length)
                {
                    var newline = text.LastIndexOf('\n', offset + length - 1, length);
                    if (newline >= offset) length = newline - offset + 1;
                    else if (length > 1 && text[offset + length - 1] == '\r' && text[offset + length] == '\n')
                        length--;
                    else if (length > 1 && char.IsHighSurrogate(text[offset + length - 1])
                             && char.IsLowSurrogate(text[offset + length]))
                        length--;
                }
                result.Add(text.Substring(offset, length));
                offset += length;
            }
            return result;
        }

        /// <summary>条款号自然序（<c>4.2.4</c> &lt; <c>4.2.10</c> —— 逐段数字比较，⛔ 不用字符串序）</summary>
        public static IComparable ClauseSortKey(string? clauseNumber)
        {
            var parts = (clauseNumber ?? string.Empty)
                .Split('.', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => int.TryParse(p, out var n) ? n : 9999)
                .ToArray();
            return string.Join(".", parts.Select(p => p.ToString("D6")));
        }

        // ========================================================
        // 跳过/失败的最小写回
        // ========================================================

        private static async Task WriteSkipAsync(
            DocumentExtractionService store, EnterpriseDoc doc, string standardCode, string why, string? userCode)
        {
            await store.UpsertAsync(doc.Code!, standardCode, doc.EnterpriseCode,
                null, Array.Empty<string>(), doc.Sha256 ?? string.Empty,
                DocExtractPromptService.PromptCode, DocExtractPromptService.PromptVersion,
                ExtractRowStatus.Skipped, why, userCode, CancellationToken.None);
        }

        public Task OnTaskStateChangedAsync(YzhQueueTask task, string newStatus, string message)
        {
            _logger.LogInformation("[条款提取] 任务状态变更: {TaskCode} → {Status} ({Message})",
                task.Code, newStatus, message);
            return Task.CompletedTask;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CertPlatform.Auditor.Entities.Dir;
using CertPlatform.Shared.DocExtraction;
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

        /// <summary>单块送模型的文档字符数上限（约 1.2 万汉字 token，留足输出空间）</summary>
        public const int ChunkChars = 30000;

        /// <summary>最多切几块（防误传超大文件把额度烧光；超出的部分记入 Message ⛔ 不静默丢）</summary>
        public const int MaxChunks = 8;

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

                // ── ③ 幂等：字节没变且产物已完成 ⇒ 跳过 ──
                var existing = await store.GetAsync(doc.Code!, payload.StandardCode);
                if (existing is { IsDeleted: false, IsStale: false, ExtractStatus: ExtractRowStatus.Done }
                    && !string.IsNullOrEmpty(doc.Sha256)
                    && string.Equals(existing.SourceSha256, doc.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("[条款提取] 命中幂等（hash 未变）: {File} × {Std}", doc.Code, standard.StandardCode);
                    return new TaskExecutionResult { Success = true, Message = "文档未变化，沿用已有提取结果", Retryable = false };
                }

                // ── ④ 条款清单（DocCheckMode ≠ none） ──
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
                var truncated = chunks.Count == MaxChunks && chunks.Sum(c => c.Length) < markdown.Length;

                var clauseList = DocExtractPromptService.BuildClauseList(
                    clauses.Select(c => (c.ClauseNumber ?? string.Empty, c.Title ?? string.Empty)));

                var merged = new Dictionary<string, ExtractedClause>(StringComparer.Ordinal);
                var allowed = clauses.Select(c => (c.ClauseNumber ?? string.Empty).Trim())
                                     .Where(x => x.Length > 0)
                                     .ToHashSet(StringComparer.Ordinal);

                var failMessages = new List<string>();
                var droppedNumbers = 0;

                for (var i = 0; i < chunks.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var prompt = DocExtractPromptService.Render(
                        template,
                        $"{standard.StandardName}（{standard.StandardCode}）",
                        standard.StandardCode ?? string.Empty,
                        doc.FileName ?? string.Empty,
                        clauseList,
                        chunks[i]);

                    var resp = await llm.CompleteAsync(new LlmInvokeRequest
                    {
                        BaseUrl = settings.BaseUrl,
                        ApiKey = settings.ApiKey,
                        Model = settings.Model,
                        Temperature = 0.1f,
                        MaxTokens = 8192,
                        Prompt = prompt,
                        ForceJson = true,
                    });

                    if (!resp.Success || resp.Json == null)
                    {
                        // 单段失败不立刻放弃：其余段可能成功，先记下来
                        failMessages.Add($"第 {i + 1}/{chunks.Count} 段失败：{resp.Message}");
                        _logger.LogWarning("[条款提取] {File}×{Std} 第 {I}/{N} 段失败: {Msg}",
                            doc.Code, standard.StandardCode, i + 1, chunks.Count, resp.Message);
                        continue;
                    }

                    using (resp.Json)
                    {
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

                // 全部段都失败 ⇒ 才判 failed（部分成功按部分成功记，⛔ 不假装全好）
                if (merged.Count == 0 && failMessages.Count == chunks.Count)
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

                var payloadDoc = new ExtractedDoc
                {
                    DocMeta = new ExtractedDocMeta
                    {
                        FileName = doc.FileName ?? string.Empty,
                        StandardCode = standard.StandardCode ?? string.Empty,
                        StandardName = standard.StandardName ?? string.Empty,
                        Model = settings.Model,
                        ExtractedAt = DateTime.Now,
                        Chunks = chunks.Count,
                    },
                    Clauses = ordered,
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
                if (truncated) notes.Add($"文档超过 {MaxChunks}×{ChunkChars} 字符，超出部分未提取");
                if (droppedNumbers > 0) notes.Add($"丢弃 {droppedNumbers} 条编号不在清单内的输出（模型自造编号）");
                if (failMessages.Count > 0) notes.Add($"{failMessages.Count} 段失败：" + string.Join("；", failMessages.Take(2)));
                if (!string.IsNullOrEmpty(clauseNote)) notes.Add(clauseNote);

                var message = notes.Count == 0
                    ? $"提取 {ordered.Count} 条条款"
                    : $"提取 {ordered.Count} 条条款；" + string.Join("；", notes);

                var status = ExtractRowStatus.Done;
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

                _logger.LogInformation("[条款提取] 完成 {File}×{Std}：{N} 条（{Chunks} 段，丢弃 {Drop}）",
                    doc.Code, standard.StandardCode, ordered.Count, chunks.Count, droppedNumbers);

                return new TaskExecutionResult
                {
                    Success = true,
                    Message = message,
                    Retryable = false,
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

            if (attrs.Count == 0)
            {
                return (Order(leaves),
                    $"标准【{standard.StandardCode}】的条款还没配 DocCheckMode，本次按全部叶子条款提取（建议先跑条款属性预生成）");
            }

            var modeByClause = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var a in attrs)
            {
                if (string.IsNullOrEmpty(a.ClauseCode)) continue;
                modeByClause[a.ClauseCode] = (a.AttrValue ?? string.Empty).Trim().ToLowerInvariant();
            }

            // 未配 DocCheckMode 的条款按「要查」处理（保守：宁可多查，不静默漏条款）
            var kept = leaves.Where(c =>
                !modeByClause.TryGetValue(c.Code ?? string.Empty, out var mode) || mode != "none").ToList();

            return (Order(kept), null);
        }

        private static List<ISOClause> Order(List<ISOClause> list)
            => list.OrderBy(c => c.Sort)
                   .ThenBy(c => c.ClauseNumber, StringComparer.Ordinal)
                   .ToList();

        // ========================================================
        // 模型输出 → ExtractedClause
        // ========================================================

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

                var num = Str(item, "ClauseNumber").Trim();
                if (num.Length == 0 || !allowed.Contains(num)) { dropped++; continue; }

                var c = new ExtractedClause
                {
                    ClauseNumber = num,
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

        /// <summary>
        /// 按<b>段落边界</b>切块（⛔ 不硬切字符：硬切会把一句话劈开，模型摘录的锚点就对不上原文）。
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

            var paragraphs = text.Replace("\r\n", "\n").Split('\n');
            var sb = new StringBuilder();

            foreach (var para in paragraphs)
            {
                // 单段本身就超限 ⇒ 硬切（否则永远装不进去）
                if (para.Length > chunkChars)
                {
                    if (sb.Length > 0) { result.Add(sb.ToString()); sb.Clear(); }
                    for (var i = 0; i < para.Length; i += chunkChars)
                    {
                        var len = Math.Min(chunkChars, para.Length - i);
                        result.Add(para.Substring(i, len));
                        if (result.Count >= maxChunks) return result;
                    }
                    continue;
                }

                if (sb.Length + para.Length + 1 > chunkChars)
                {
                    result.Add(sb.ToString());
                    sb.Clear();
                    if (result.Count >= maxChunks) return result;
                }

                sb.Append(para).Append('\n');
            }

            if (sb.Length > 0 && result.Count < maxChunks) result.Add(sb.ToString());
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

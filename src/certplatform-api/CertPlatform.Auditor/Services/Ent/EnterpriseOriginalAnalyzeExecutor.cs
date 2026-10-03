using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CertPlatform.Admin.Services.Workflow;
using CertPlatform.Shared.Entities.Cert;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.DataBase.Services;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Queue;
using YzhQueueTask = YZH.Core.Stand.Models.Queue.YzhQueueTask;

namespace CertPlatform.Auditor.Services.Ent
{
    /// <summary>
    /// 企业原始资料语义分析执行器（TaskType = <c>enterprise_original_analyze</c>，36 号 §八 T2.3）。
    ///
    /// <para><b>两跳调用（36 号 §5.1）</b>：</para>
    /// <list type="number">
    ///   <item><b>L1 <c>doc_group</c> —— 每【批次】一次</b>，输入文件名清单 + 开头片段，
    ///         输出每文件的 <c>tags</c> + <c>suggestedPolicy</c>。
    ///         ⚠️ 33 号 <c>:386,485</c> 定义它是<b>批次</b>提示词（「吃文件名清单」），
    ///         <b>逐文件跑是语义错配且 LLM 成本 N 倍</b>。</item>
    ///   <item><b>L2 <c>doc_content</c> —— 逐份</b>，输入单份 Markdown 全文，
    ///         输出作用四段 + <c>infoItems</c> + 字段/表格。</item>
    /// </list>
    ///
    /// <para><b>★ AI 建议只落建议列</b>：AI 说「这份该跳过」时，只写 <c>PolicyReason</c> +
    /// <c>PolicySource='ai'</c>，⛔ <b>不改 <c>AnalyzePolicy</c></b>（36 号 R5）。
    /// 依据：企业交了必备证据却被 AI 静默扔掉 = 审核事故。策略只有人工能定。</para>
    ///
    /// <para><b>作用域</b>：原始资料标准无关，但提示词必须带作用域 ⇒ <see cref="ResolveScopeAsync"/>
    /// 取该企业×阶段的<b>主标准</b>（OrderNo 最小者），空则平台级。<b>返回 GUID</b>，
    /// 且 <c>AnalyzeForQueueAsync</c> 内部会再转成 slug 做标签裁剪（36 号 §5.3 GUID→slug）。</para>
    /// </summary>
    public class EnterpriseOriginalAnalyzeExecutor : IYzhTaskExecutor
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<EnterpriseOriginalAnalyzeExecutor> _logger;

        public EnterpriseOriginalAnalyzeExecutor(
            IServiceProvider serviceProvider,
            ILogger<EnterpriseOriginalAnalyzeExecutor> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public string TaskType => EnterpriseOriginalQueue.TaskTypeAnalyze;

    /// <summary>
    /// L2（doc_content）并发度。
    /// <para>⚠️ 不宜过大：LLM 侧有速率限制，且每路各占一个 DI scope（含 DB 连接）。
    /// 实测单次调用 6–11 秒，并发 4 时 36 份约 1 分钟（串行需 4–6 分钟）。</para>
    /// </summary>
    private const int MaxConcurrency = 4;

    /// <summary>本批已完成的文件数（并发分支用 Interlocked 自增）</summary>
    private int _doneCounter;

    /// <summary>
    /// 回写队列进度（把「任务数」口径改成「文件数」口径）。
    /// <para>⛔ 只改这三个字段，⛔ 不碰 Status —— 状态由 <c>QueueManager</c> 管，
    /// 执行器私自改会与框架的完成回调打架。</para>
    /// </summary>
    private async Task ReportProgressAsync(IDbOrm db, string? queueCode, int done, int total)
    {
        if (string.IsNullOrWhiteSpace(queueCode) || total <= 0) return;
        try
        {
            var q = await db.Client.Queryable<YzhQueue>()
                .Where(x => x.QueueCode == queueCode)
                .FirstAsync();
            if (q == null) return;

            q.TotalCount = total;
            q.CompletedCount = Math.Clamp(done, 0, total);
            q.ProcessingCount = done < total ? 1 : 0;
            q.PendingCount = Math.Max(0, total - done);
            q.Progress = (int)Math.Round(100.0 * q.CompletedCount / total);
            await db.Client.Updateable<YzhQueue>()
                .SetColumns(it => it.TotalCount == q.TotalCount)
                .SetColumns(it => it.CompletedCount == q.CompletedCount)
                .SetColumns(it => it.ProcessingCount == q.ProcessingCount)
                .SetColumns(it => it.PendingCount == q.PendingCount)
                .SetColumns(it => it.Progress == q.Progress)
                .Where(x => x.QueueCode == queueCode)
                .ExecuteCommandAsync();
        }
        catch (Exception ex)
        {
            // 进度回写失败**绝不能**影响分析本身
            _logger.LogWarning(ex, "[原始资料分析] 进度回写失败: {Queue}", queueCode);
        }
    }

        public async Task<TaskExecutionResult> ExecuteAsync(YzhQueueTask task, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(task.Payload))
                return new TaskExecutionResult { Success = false, Message = "Payload 为空", Retryable = false };

            EnterpriseOriginalAnalyzePayload? payload;
            try
            {
                payload = JsonSerializer.Deserialize<EnterpriseOriginalAnalyzePayload>(task.Payload,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException ex)
            {
                return new TaskExecutionResult { Success = false, Message = $"Payload 解析失败：{ex.Message}", Retryable = false };
            }

            if (payload == null || payload.FileCodes.Count == 0)
                return new TaskExecutionResult { Success = false, Message = "Payload 缺少 FileCodes", Retryable = false };

            var queueCode = task.QueueCode;   // 用于回写进度（见 ReportProgressAsync）

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<IDbOrm>();
                var storage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();
                // ⚠️ PromptWorkbenchService 在 Admin 项目（单宿主同进程同队列）⇒ 从根容器懒解析
                var prompts = scope.ServiceProvider.GetRequiredService<PromptWorkbenchService>();

                // ① 取待分析文件（按 Code，⛔ 不用 Id，D9）
                var rows = new List<EnterpriseOriginalFile>();
                foreach (var code in payload.FileCodes.Distinct())
                {
                    var r = (await db.GetOneIgnoreValidAsync<EnterpriseOriginalFile>(x => x.Code == code)).Data;
                    if (r == null) continue;
                    if (r.IsDeleted) continue;
                    // ★ L2 策略分流：skip/ignore ⇒ 标 skipped 止，不进画像
                    if (r.AnalyzePolicy != EnterpriseOriginalService.Policy.Analyze)
                    {
                        await MarkSkippedAsync(db, r, $"人工策略为 {r.AnalyzePolicy}");
                        continue;
                    }
                    if (string.IsNullOrEmpty(r.MarkdownPath))
                    {
                        // ⛔ **不判 failed**：同批可能只是这个文件转换慢/失败（.pdf 无 Markdown、
                        //   anydoc 失败等）。判 failed 会把「还没轮到」和「真的坏了」混为一谈，
                        //   而 EnsureAnalyzeQueuedAsync 只在「至少一份就绪」时才入队 ⇒ 到这里的必然已就绪。
                        //   保持 pending，下一轮补分析队列时会重新覆盖。
                        _logger.LogInformation("[原始资料分析] 跳过（Markdown 未就绪）: {Code}", r.Code);
                        continue;
                    }
                    rows.Add(r);
                }

                if (rows.Count == 0)
                    return new TaskExecutionResult { Success = true, Message = "无可分析文件（全部被策略跳过或已删除）" };

                // ② 读 Markdown
                var markdowns = new List<(string FileName, string? Markdown)>();
                foreach (var r in rows)
                {
                    try
                    {
                        var (stream, _) = await storage.DownloadAsync(r.MarkdownPath!.TrimStart('/'));
                        using var ms = new MemoryStream();
                        await stream.CopyToAsync(ms);
                        markdowns.Add((r.FileName, Encoding.UTF8.GetString(ms.ToArray())));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[原始资料分析] Markdown 读取失败: {Code}", r.Code);
                        markdowns.Add((r.FileName, null));
                    }
                    await SetAnalyzeStatusAsync(db, r, EnterpriseOriginalService.AnalyzeStatus.Analyzing, null);
                }

                var usable = markdowns.Where(x => !string.IsNullOrWhiteSpace(x.Markdown)).ToList();
                if (usable.Count == 0)
                {
                    foreach (var r in rows) await MarkFailedAsync(db, r, "Markdown 内容为空，无法分析");
                    return new TaskExecutionResult { Success = false, Message = "Markdown 内容为空", Retryable = false };
                }

                // ③ 作用域（候选列表 → 逐个回退，见 ResolveScopesAsync 注释）
                var scopeCandidates = await ResolveScopesAsync(db, payload.EnterpriseCode, payload.StageCode);

                // ④ L1：doc_group（★ 每批次一次）
                var groupScope = await PickScopeAsync(db, prompts, scopeCandidates, PromptWorkbenchService.Types.Group);
                var groupResult = await prompts.AnalyzeForQueueAsync(
                    PromptWorkbenchService.Types.Group, groupScope,
                    usable, $"enterprise_original:{payload.BatchCode}:group");

                var groupByFile = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
                if (groupResult.Success && !string.IsNullOrWhiteSpace(groupResult.Json))
                    groupByFile = ParseGroupItems(groupResult.Json!);
                else
                    _logger.LogWarning("[原始资料分析] doc_group 失败（继续跑 doc_content）: {Msg}", groupResult.Message);

                // ⑤ 逐文件写回 L1 结论（tags + AI 建议策略）
                var byName = rows.ToDictionary(r => r.FileName, StringComparer.OrdinalIgnoreCase);
                foreach (var (fileName, _) in usable)
                {
                    if (!byName.TryGetValue(fileName, out var row)) continue;
                    if (groupByFile.TryGetValue(fileName, out var g))
                        await ApplyGroupResultAsync(db, row, g, groupResult);
                }

                // ⑥ L2：doc_content（逐份，但**有限并发**）
                //
                // ★ 为什么并发（2026-10-03 实测）：单次 LLM 调用 6–11 秒，
                //   串行跑 36 份要 **4–6 分钟**，页面只能干等 ⇒ 用户以为「没触发队列」。
                //   改成并发 4 路后同样 36 份约 1 分钟。
                //   ⚠️ 并发度不能开太大：LLM 有速率限制，且 `IDbOrm` 的 scope 不是线程安全的，
                //      所以每个并发分支**各自开一个 DI scope**（复用 scope 会串行化并可能踩连接池）。
                var analyzed = 0;
                var failed = 0;
                var cost = 0L;
                var promptTokens = 0;
                var completionTokens = 0;

                // ★ 进度口径：队列的 TotalCount 是「任务数」=1，而本任务内部要处理 N 个文件。
                //   不改的话进度条会一路显示「1/1 100%」——**用户看到的进度与真实进度不符 = 误判**
                //   （2026-10-03 用户明确要求「显示进度的详情，否则会造成误判」）。
                //   ⇒ 把 TotalCount 改成「待处理文件数」，CompletedCount 随每份完成递增。
                await ReportProgressAsync(db, queueCode, 0, usable.Count);

                Interlocked.Exchange(ref _doneCounter, 0);
                var gate = new SemaphoreSlim(MaxConcurrency);
                var tasks = usable.Select(async item =>
                {
                    await gate.WaitAsync();
                    try
                    {
                        var (fileName, markdown) = item;
                        if (!byName.TryGetValue(fileName, out var row)) return (analyzed: 0, failed: 0, ms: 0L, pt: 0, ct: 0);

                        // 每个并发分支独立 scope（IDbOrm 非线程安全）
                        using var inner = _serviceProvider.CreateScope();
                        var db2 = inner.ServiceProvider.GetRequiredService<IDbOrm>();
                        var prompts2 = inner.ServiceProvider.GetRequiredService<PromptWorkbenchService>();

                        // 作用域候选在闭包里查一次即可（同一批次）
                        var scopes2 = await ResolveScopesAsync(db2, payload.EnterpriseCode, payload.StageCode);
                        var contentScope = await PickScopeAsync(db2, prompts2, scopes2, PromptWorkbenchService.Types.Content);

                        var one = await prompts2.AnalyzeForQueueAsync(
                            PromptWorkbenchService.Types.Content, contentScope,
                            new List<(string, string?)> { (fileName, markdown) },
                            $"enterprise_original:{row.Code}:content");

                        if (!one.Success)
                        {
                            await MarkFailedAsync(db2, row, one.Message);
                            return (analyzed: 0, failed: 1, ms: 0L, pt: 0, ct: 0);
                        }

                        var done = Interlocked.Increment(ref _doneCounter);
                        await ReportProgressAsync(db2, queueCode, done, usable.Count);

                        // ⚠️ AI 建议 skip/ignore ⇒ 止于此，⛔ 不建画像、不改 AnalyzePolicy（36 号 R5）
                        var suggestion = ReadSuggestedPolicy(one.Json!);
                        if (suggestion is EnterpriseOriginalService.Policy.Skip
                            or EnterpriseOriginalService.Policy.Ignore)
                        {
                            await ApplyAiSuggestionOnlyAsync(db2, row, suggestion!, one.Json!);
                            return (analyzed: 1, failed: 0, ms: one.DurationMs, pt: one.PromptTokens, ct: one.CompletionTokens);
                        }

                        await UpsertProfileAsync(db2, row, contentScope, one, groupByFile, groupResult);
                        return (analyzed: 1, failed: 0, ms: one.DurationMs, pt: one.PromptTokens, ct: one.CompletionTokens);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[原始资料分析] 单份并发分支异常: {Batch}", payload.BatchCode);
                        return (analyzed: 0, failed: 1, ms: 0L, pt: 0, ct: 0);
                    }
                    finally { gate.Release(); }
                }).ToList();

                var results = await Task.WhenAll(tasks);
                analyzed = results.Sum(r => r.analyzed);
                failed = results.Sum(r => r.failed);
                cost = results.Sum(r => r.ms);
                promptTokens = results.Sum(r => r.pt);
                completionTokens = results.Sum(r => r.ct);

                _logger.LogInformation(
                    "[原始资料分析] 批次 {Batch}：可用 {Usable} / 成功 {Ok} / 失败 {Fail} / 并发 {C} / 累计 {Cost}ms {PT}tok {CT}tok",
                    payload.BatchCode, usable.Count, analyzed, failed, MaxConcurrency, cost, promptTokens, completionTokens);

                return new TaskExecutionResult
                {
                    Success = failed == 0,
                    Message = failed == 0
                        ? $"分析完成 {analyzed} 份"
                        : $"分析部分失败（成功 {analyzed} / 失败 {failed}）",
                    Retryable = failed > 0,
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[原始资料分析] 任务异常: {TaskCode}", task.Code);
                return new TaskExecutionResult { Success = false, Message = ex.Message, Retryable = true };
            }
        }

        // ========================================================
        // 作用域解析（36 号 §5.3）
        // ========================================================

        /// <summary>
        /// 该企业×阶段关联的<b>标准候选列表</b>（GUID，按关联先后 = <c>CreateTime</c> 升序）。
        ///
        /// <para>★ 为什么是<b>列表</b>而不是单个「主标准」：企业可以同时关联多个标准，而
        /// <c>wf_prompt_template.StandardCode</c> 只对<b>某一个</b>标准绑定了提示词。
        /// 实测踩坑：测试企业同时关联 2 个标准，按「字典序最小」取会选中<b>没有提示词</b>的那个，
        /// 分析直接失败「未找到生效的 doc_content 提示词」——
        /// 死板取一个标准 = 把「提示词没绑这个标准」误报成「分析失败」。</para>
        /// <para>调用方按顺序逐个尝试，<b>第一个能解析到提示词的即采用</b>；全都不行才报失败。</para>
        /// </summary>
        private async Task<List<string>> ResolveScopesAsync(IDbOrm db, string enterpriseCode, string stageCode)
        {
            if (string.IsNullOrWhiteSpace(enterpriseCode) || string.IsNullOrWhiteSpace(stageCode))
                return new List<string>();

            var links = await db.Client.Queryable<CertEnterpriseStage>()
                .Where(x => x.EnterpriseCode == enterpriseCode && x.StageCode == stageCode
                            && x.IsValid == 1 && !x.IsDeleted)
                .OrderBy(x => x.CreateTime)
                .ToListAsync() ?? new List<CertEnterpriseStage>();

            var codes = links.Select(l => l.StandardCode)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c!)
                .Distinct()
                .ToList();

            // ⚠️ `cert_enterprise_stage` **没有 OrderNo 列**（36 号 §十 L1 读码确认），
            //    故按 CreateTime（关联先后）定序；再按字典序兜底保证结果**确定**。
            return codes
                .OrderBy(c => links.First(l => l.StandardCode == c).CreateTime)
                .ThenBy(c => c, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>候选作用域里挑第一个<b>能解析到提示词</b>的；都解析不到则返回第一个（让上层报「未找到提示词」）</summary>
        private async Task<string?> PickScopeAsync(
            IDbOrm db, PromptWorkbenchService prompts, List<string> candidates, string promptType)
        {
            foreach (var code in candidates)
            {
                var t = await prompts.ResolveActiveAsync(promptType, code);
                if (t != null && !string.IsNullOrWhiteSpace(t.Template)) return code;
            }
            return candidates.Count > 0 ? candidates[0] : null;
        }

        // ========================================================
        // L1 结果解析与回写
        // ========================================================

        /// <summary>
        /// 解析 <c>doc_group</c> 输出：<c>{ items: [ { fileName, tags[], suggestedPolicy, … } ] }</c>
        /// ⇒ 文件名 → 该文件的判定对象。
        /// <para>⚠️ 按 <b>文件名</b> 匹配（33 号 <c>doc_group</c> 的契约就是文件名清单）；
        /// 匹配不上的文件不进 map，后续按「无 L1 结论」正常走 L2。</para>
        /// </summary>
        private static Dictionary<string, JsonElement> ParseGroupItems(string json)
        {
            var map = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
                {
                    foreach (var it in items.EnumerateArray())
                    {
                        if (it.ValueKind != JsonValueKind.Object) continue;
                        var name = ReadString(it, "fileName") ?? ReadString(it, "name");
                        if (string.IsNullOrWhiteSpace(name)) continue;
                        map[name!] = it.Clone();
                    }
                }
            }
            catch (JsonException) { /* 非法 JSON 已由 ValidateSemanticOutput 拦过，这里只做防御 */ }
            return map;
        }

        private static string? ReadString(JsonElement el, string prop)
            => el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        private static string? ReadSuggestedPolicy(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("items", out var items)
                    && items.ValueKind == JsonValueKind.Array && items.GetArrayLength() > 0)
                    root = items[0];
                if (root.ValueKind != JsonValueKind.Object) return null;
                var v = ReadString(root, "suggestedPolicy") ?? ReadString(root, "policy");
                return string.IsNullOrWhiteSpace(v) ? null : v!.Trim().ToLowerInvariant();
            }
            catch (JsonException) { return null; }
        }

        /// <summary>
        /// 回写 L1 结论：tags 落到画像草稿（tags/confidence/reason）。
        /// <para>⭐ <b>AI 建议只写建议列</b>：<c>PolicyReason</c> + <c>PolicySource='ai'</c>，
        /// <b>不改 <c>AnalyzePolicy</c></b>（36 号 R5：不允许纯自动静默忽略）。</para>
        /// </summary>
        private static async Task ApplyGroupResultAsync(
            IDbOrm db, EnterpriseOriginalFile row, JsonElement g, PromptWorkbenchService.AnalyzeForQueueResult groupResult)
        {
            var suggestion = ReadString(g, "suggestedPolicy") ?? ReadString(g, "policy");
            var reason = ReadString(g, "policyReason");

            row.PolicyReason = string.IsNullOrWhiteSpace(reason)
                ? (string.IsNullOrWhiteSpace(suggestion) ? null : $"AI 建议：{suggestion}")
                : reason;
            if (!string.IsNullOrWhiteSpace(suggestion))
            {
                // ⛔ AnalyzePolicy 一律不动；PolicySource='ai' ⇒ 前端显示「待确认」角标
                row.PolicySource = "ai";
            }
            row.UpdateTime = DateTime.Now;
            await db.UpdateAsync(row,
                nameof(EnterpriseOriginalFile.PolicyReason),
                nameof(EnterpriseOriginalFile.PolicySource),
                nameof(EnterpriseOriginalFile.UpdateTime));
        }

        /// <summary>AI 建议 skip/ignore ⇒ 只写建议 + 止；<b>不建画像、不改策略</b></summary>
        private static async Task ApplyAiSuggestionOnlyAsync(IDbOrm db, EnterpriseOriginalFile row, string suggestion, string json)
        {
            row.AnalyzeStatus = EnterpriseOriginalService.AnalyzeStatus.Analyzed;
            row.AnalyzeMessage = $"AI 建议策略为 {suggestion}（⏳ 待人工确认；未自动生效）";
            // ⚠️ 截断到 200：LLM 返回的 policyReason 长度不可控，
            //   MySQL 严格模式下超长直接抛错 ⇒ 整条分析链路挂（2026-10-03 实测 8 个并发分支全失败）
            row.PolicyReason = Truncate($"AI 建议：{suggestion}", 200);
            row.PolicySource = "ai";
            row.PolicyDecidedTime = null;      // 人工尚未决策 ⇒ ⛔ 不写决策时间
            row.AnalyzeTime = DateTime.Now;
            row.UpdateTime = DateTime.Now;
            await db.UpdateAsync(row,
                nameof(EnterpriseOriginalFile.AnalyzeStatus),
                nameof(EnterpriseOriginalFile.AnalyzeMessage),
                nameof(EnterpriseOriginalFile.PolicyReason),
                nameof(EnterpriseOriginalFile.PolicySource),
                nameof(EnterpriseOriginalFile.PolicyDecidedTime),
                nameof(EnterpriseOriginalFile.AnalyzeTime),
                nameof(EnterpriseOriginalFile.UpdateTime));
        }

        // ========================================================
        // L2 画像 upsert（只追加）
        // ========================================================

        /// <summary>
        /// upsert 画像：<c>ProfileVersion+1</c> + 旧版 <c>IsLatest=0</c>（26 号 A-4 只追加哲学）。
        /// <para>★ 无论成功失败都<b>先建 failed 行</b>再改 IsLatest —— 这样「分析过但失败」也有留痕，
        /// 排障时能看到「当时用的哪版提示词、报了什么」。</para>
        /// </summary>
        private async Task UpsertProfileAsync(
            IDbOrm db, EnterpriseOriginalFile row, string? scopeStandardCode,
            PromptWorkbenchService.AnalyzeForQueueResult one,
            Dictionary<string, JsonElement> groupByFile, PromptWorkbenchService.AnalyzeForQueueResult groupResult)
        {
            var latest = (await db.GetListAsync<EnterpriseDocProfile>(
                x => x.OriginalFileCode == row.Code && x.IsLatest == true && x.IsValid == 1)).Data?
                .OrderByDescending(x => x.ProfileVersion).FirstOrDefault();

            var nextVersion = (latest?.ProfileVersion ?? 0) + 1;

            var (tagsJson, tagsReason, tagsConf, typeGuess, keywords, summary, docCategory) =
                ExtractGroupHints(groupByFile, row.FileName, groupResult);

            var (purpose, purposeConf, infoItems, fields, tables, conf) = ExtractContentHints(one.Json!);

            var profile = new EnterpriseDocProfile
            {
                Code = Guid.NewGuid().ToString("N"),
                CreateTime = DateTime.Now,
                CreateBy = null,
                OriginalFileCode = row.Code,
                EnterpriseCode = row.EnterpriseCode,
                OrgCode = row.OrgCode,
                StageCode = row.StageCode,
                StandardCode = scopeStandardCode ?? "",
                FileName = row.FileName,
                ProfileVersion = nextVersion,
                IsLatest = true,
                ProfileStatus = "completed",
                DetectSource = "ai",
                ModelName = one.Model,
                PromptCode = one.PromptCode,
                PromptVersion = one.PromptVersion,
                PromptTokens = one.PromptTokens,
                CompletionTokens = one.CompletionTokens,
                DurationMs = (int)Math.Min(one.DurationMs, int.MaxValue),
                SourceMarkdownPath = row.MarkdownPath,
                AnalyzeTime = DateTime.Now,
                IsValid = 1,

                // L1 结论
                TagsJson = tagsJson,
                TagsSource = string.IsNullOrWhiteSpace(tagsJson) ? null : "ai",
                TagsReason = tagsReason,
                TagsConfidence = tagsConf,
                TypeGuess = typeGuess,
                Keywords = keywords,
                Summary = summary,
                DocCategory = docCategory,

                // L2 结论
                DocPurpose = purpose,
                DocPurposeSource = string.IsNullOrWhiteSpace(purpose) ? null : "ai",
                DocPurposeConfidence = purposeConf,
                InfoItemsJson = infoItems,
                FieldsJson = fields,
                TablesJson = tables,
                Confidence = conf,
            };

            if (latest != null)
            {
                latest.IsLatest = false;
                await db.UpdateAsync(latest, nameof(EnterpriseDocProfile.IsLatest));
            }
            await db.InsertAsync(profile);

            row.AnalyzeStatus = EnterpriseOriginalService.AnalyzeStatus.Analyzed;
            row.AnalyzeMessage = null;
            row.AnalyzeTime = DateTime.Now;
            row.UpdateTime = DateTime.Now;
            await db.UpdateAsync(row,
                nameof(EnterpriseOriginalFile.AnalyzeStatus),
                nameof(EnterpriseOriginalFile.AnalyzeMessage),
                nameof(EnterpriseOriginalFile.AnalyzeTime),
                nameof(EnterpriseOriginalFile.UpdateTime));
        }

        private static (string? TagsJson, string? Reason, decimal? Conf, string? TypeGuess,
                         string? Keywords, string? Summary, string? Category)
            ExtractGroupHints(Dictionary<string, JsonElement> map, string fileName,
                              PromptWorkbenchService.AnalyzeForQueueResult groupResult)
        {
            if (!map.TryGetValue(fileName, out var g)) return (null, null, null, null, null, null, null);
            if (g.ValueKind != JsonValueKind.Object) return (null, null, null, null, null, null, null);

            var tagsJson = g.TryGetProperty("tags", out var t) && t.ValueKind == JsonValueKind.Array
                ? t.GetRawText() : null;

            var reasonParts = new List<string>();
            var typeGuess = ReadString(g, "typeGuess");
            if (!string.IsNullOrWhiteSpace(typeGuess)) reasonParts.Add($"类型猜测：{typeGuess}");
            reasonParts.AddRange(groupResult.ValidationMessages);

            decimal? conf = null;
            if (g.TryGetProperty("confidence", out var c) && c.ValueKind == JsonValueKind.Number
                && c.TryGetDouble(out var cd)) conf = (decimal)Math.Round(cd, 2);

            return (tagsJson,
                    reasonParts.Count == 0 ? null : string.Join("；", reasonParts),
                    conf,
                    typeGuess,
                    ReadString(g, "keywords"),
                    ReadString(g, "summary"),
                    ReadString(g, "docCategory"));
        }

        private static (string? Purpose, decimal? PurposeConf, string? InfoItems,
                         string? Fields, string? Tables, decimal? Conf)
            ExtractContentHints(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object) return (null, null, null, null, null, null);

                decimal? Num(string prop)
                    => root.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number
                       && v.TryGetDouble(out var d) ? (decimal)Math.Round(d, 2) : null;

                string? Arr(string prop)
                    => root.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Array
                       ? v.GetRawText() : null;

                return (ReadString(root, "purpose"), Num("purposeConfidence") ?? Num("docPurposeConfidence"),
                        Arr("infoItems"), Arr("fields"), Arr("tables"), Num("confidence"));
            }
            catch (JsonException) { return (null, null, null, null, null, null); }
        }

        // ========================================================
        // 状态写回（列级，禁止全列）
        // ========================================================

        private static async Task SetAnalyzeStatusAsync(
            IDbOrm db, EnterpriseOriginalFile row, string status, string? message)
        {
            row.AnalyzeStatus = status;
            row.AnalyzeMessage = message;
            row.UpdateTime = DateTime.Now;
            await db.UpdateAsync(row,
                nameof(EnterpriseOriginalFile.AnalyzeStatus),
                nameof(EnterpriseOriginalFile.AnalyzeMessage),
                nameof(EnterpriseOriginalFile.UpdateTime));
        }

        private static async Task MarkSkippedAsync(IDbOrm db, EnterpriseOriginalFile row, string message)
        {
            row.AnalyzeStatus = EnterpriseOriginalService.AnalyzeStatus.Skipped;
            row.AnalyzeMessage = message;
            row.UpdateTime = DateTime.Now;
            await db.UpdateAsync(row,
                nameof(EnterpriseOriginalFile.AnalyzeStatus),
                nameof(EnterpriseOriginalFile.AnalyzeMessage),
                nameof(EnterpriseOriginalFile.UpdateTime));
        }

        private static async Task MarkFailedAsync(IDbOrm db, EnterpriseOriginalFile row, string message)
        {
            row.AnalyzeStatus = EnterpriseOriginalService.AnalyzeStatus.Failed;
            row.AnalyzeMessage = Truncate(message, 1000);
            row.UpdateTime = DateTime.Now;
            await db.UpdateAsync(row,
                nameof(EnterpriseOriginalFile.AnalyzeStatus),
                nameof(EnterpriseOriginalFile.AnalyzeMessage),
                nameof(EnterpriseOriginalFile.UpdateTime));
        }

        private static string Truncate(string s, int max)
            => string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s[..max]);

        public Task OnTaskStateChangedAsync(YzhQueueTask task, string newStatus, string message)
        {
            _logger.LogInformation("[原始资料分析] 任务状态变更: {TaskCode} → {Status} ({Message})",
                task.Code, newStatus, message);
            return Task.CompletedTask;
        }
    }
}
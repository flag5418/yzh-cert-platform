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
using CertPlatform.Shared.Fill;
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
    /// <para><b>★★★ 作用域（M6，2026-10-06 改）</b>：原始资料<b>文件</b>标准无关，但<b>提示词按标准绑定</b>
    /// ⇒ 同一份资料在标准 A 与标准 B 下的「分组 / 文档作用」<b>本就不同</b>，必须
    /// <b>按标准各分析一次</b>，每个 (文件, 标准) 产<b>一行</b>画像（选项 A 多行画像）。</para>
    /// <list type="bullet">
    ///   <item>标准来源：载荷 <c>StandardCodes</c> 优先；为空则按「企业 × 阶段」关联解析出
    ///         <b>所有</b>能解析到 <c>doc_content</c> 提示词的标准（见 <see cref="ResolveStandardsAsync"/>）。</item>
    ///   <item>⛔ <b>不再</b>「只挑第一个能用的标准」—— 旧 <c>PickScopeAsync</c> 是 M6 的设计目标偏差，
    ///         会让后分析的标准<b>顶掉</b>先前标准的画像。</item>
    ///   <item><b>返回 GUID</b>，<c>AnalyzeForQueueAsync</c> 内部会再转成 slug 做标签裁剪（36 号 §5.3）。</item>
    /// </list>
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

    /// <summary>
    /// ★ <b>M8-2（2026-10-06）</b>：<c>doc_group</c> 单批上限。
    ///
    /// <para>33 号定义 <c>doc_group</c> 是「<b>批次</b>提示词，吃文件名清单」⇒ 理论上一次能塞整批；
    /// 但实测 101 份时被 <c>PromptWorkbenchService</c> 的测试页常数（20）整批拒绝，
    /// 而执行器只 <c>LogWarning</c> 继续跑 ⇒ <b>分组全丢且页面显示成功</b>（静默降级）。
    /// ⇒ 改为在这里<b>主动切片</b>，逐批调、合并结果。</para>
    /// <para>⛔ 与测试页的 <c>MaxTestFiles</c> <b>不是同一个东西</b>：那个是「页面单次试跑」的防护，
    /// 这个是队列侧的批处理粒度。两者取值可不同，但都不得小于 1。</para>
    /// </summary>
    private const int GroupBatchSize = 20;

    /// <summary>
    /// 回写队列进度（把「任务数」口径改成「分析单元数」口径）。
    /// <para>⛔ 只改这几个字段，⛔ 不碰 Status —— 状态由 <c>QueueManager</c> 管，
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

    /// <summary>
    /// 文件级聚合状态（M6 连带）：<c>AnalyzeStatus</c> 在文件表上是**单列**，
    /// 而分析单元是「文件 × 标准」⇒ 逐标准写会互相覆盖，故先累积、最后一次性定终态。
    /// </summary>
    private sealed class FileUnitState
    {
        /// <summary>该文件**成功**分析的标准数</summary>
        public int Ok { get; set; }
        /// <summary>该文件**失败**分析的标准数</summary>
        public int Fail { get; set; }
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
                        //   anydoc 失败等）。判 failed 会把「还没轮到」和「真的坏了」混为一谈。
                        //   2026-10-06 起分析段由文件级编排器在同一任务内、转换段成功后才调
                        //   ⇒ 正常路径下到这里的必然已就绪；此分支只兜「历史批次队列 / 并发改行」。
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

                // ③ ★ M6：确定本批要分析的**标准列表**
                //   · 载荷 StandardCodes 优先（入队方显式指定）
                //   · 为空 ⇒ 按「企业 × 阶段」关联解析出**所有**能解析到 doc_content 提示词的标准
                //   ⛔ 关键：不再「只挑第一个能用的标准」—— 那是 M6 的设计目标偏差：
                //      一阶段多标准时只产一份画像，且 upsert 的 latest 查询不含标准
                //      ⇒ **后分析的标准顶掉先前标准的画像**（匹配时读错标准的分组/作用）。
                var standards = await ResolveStandardsAsync(db, prompts, payload);
                if (standards.Count == 0)
                {
                    const string msg = "该企业×阶段关联的标准均未配置生效的 doc_content 提示词，无法分析";
                    foreach (var r in rows) await MarkFailedAsync(db, r, msg);
                    return new TaskExecutionResult { Success = false, Message = msg, Retryable = false };
                }

                // ④ 进度口径 = 文件数 × 标准数（一个「文件 × 标准」= 一个分析单元）
                //   不改的话进度条会一路显示「1/1 100%」——**用户看到的进度与真实进度不符 = 误判**
                //   （2026-10-03 用户明确要求「显示进度的详情，否则会造成误判」）。
                var totalUnits = usable.Count * standards.Count;
                await ReportProgressAsync(db, queueCode, 0, totalUnits);
                var doneCounter = 0;

                var byName = rows.ToDictionary(r => r.FileName, StringComparer.OrdinalIgnoreCase);

                // ★ M6 连带：文件表 `AnalyzeStatus` 是**文件级单列**，而分析单元是「文件 × 标准」
                //   ⇒ 逐标准写会互相覆盖。改为**先累积、最后聚合写回**（全部标准跑完才定终态）。
                var fileUnits = usable.ToDictionary(
                    x => x.FileName, _ => new FileUnitState(), StringComparer.OrdinalIgnoreCase);

                var groupFailures = new List<string>();
                var analyzed = 0;
                var failed = 0;
                var cost = 0L;
                var promptTokens = 0;
                var completionTokens = 0;

                // ⑤ ★ M6：**逐标准**各跑一轮（doc_group + doc_content），各产**一行**画像
                foreach (var std in standards)
                {
                    // 5.1 L1：doc_group（★ M8-2：按 ≤GroupBatchSize 切片逐批合并；⛔ 不再静默降级）
                    var (groupByFile, gFail) = await RunGroupBatchedAsync(
                        prompts, std, usable, payload.BatchCode);
                    foreach (var f in gFail)
                        groupFailures.Add($"[标准 {ShortCode(std)}] {f}");
                    if (gFail.Count > 0)
                        _logger.LogError("[原始资料分析] 标准 {Std} 的 doc_group 有 {N} 批失败：{Detail}",
                            std, gFail.Count, string.Join(" | ", gFail));

                    // 5.2 写回 L1 结论（tags + AI 建议策略；文件级单列 ⇒ 首个写入者胜，见方法注释）
                    foreach (var (fileName, _) in usable)
                    {
                        if (!byName.TryGetValue(fileName, out var row)) continue;
                        if (groupByFile.TryGetValue(fileName, out var g))
                            await ApplyGroupResultAsync(db, row, g);
                    }

                    // 5.3 L2：doc_content（逐份，但**有限并发**）
                    //
                    // ★ 为什么并发（2026-10-03 实测）：单次 LLM 调用 6–11 秒，
                    //   串行跑 36 份要 **4–6 分钟**，页面只能干等 ⇒ 用户以为「没触发队列」。
                    //   改成并发 4 路后同样 36 份约 1 分钟。
                    //   ⚠️ 并发度不能开太大：LLM 有速率限制，且 `IDbOrm` 的 scope 不是线程安全的，
                    //      所以每个并发分支**各自开一个 DI scope**（复用 scope 会串行化并可能踩连接池）。
                    var gate = new SemaphoreSlim(MaxConcurrency);
                    var tasks = usable.Select(async item =>
                    {
                        await gate.WaitAsync();
                        try
                        {
                            var (fileName, markdown) = item;
                            if (!byName.TryGetValue(fileName, out var row))
                                return (FileName: fileName, analyzed: 0, failed: 0, ms: 0L, pt: 0, ct: 0);

                            // 每个并发分支独立 scope（IDbOrm 非线程安全）
                            using var inner = _serviceProvider.CreateScope();
                            var db2 = inner.ServiceProvider.GetRequiredService<IDbOrm>();
                            var prompts2 = inner.ServiceProvider.GetRequiredService<PromptWorkbenchService>();

                            // ★ 语义精要预处理（字数 > 2000 且尚未生成）
                            //   ⚠️ 精要是**源文档**的压缩，与标准无关 ⇒ 文件级缓存正确，多标准共用。
                            var analysisInput = markdown;
                            if (markdown.Length > 2000 && string.IsNullOrWhiteSpace(row.EssentialSummary))
                            {
                                var essentialScope = await PickAnyScopeAsync(
                                    prompts2, standards, PromptWorkbenchService.Types.Essential);
                                if (essentialScope != null)
                                {
                                    var summaryRes = await prompts2.AnalyzeForQueueAsync(
                                        PromptWorkbenchService.Types.Essential, essentialScope,
                                        new List<(string, string?)> { (fileName, markdown) },
                                        $"enterprise_original:{row.Code}:essential");

                                    if (summaryRes.Success && !string.IsNullOrWhiteSpace(summaryRes.Json))
                                    {
                                        row.EssentialSummary = ExtractSummaryFromJson(summaryRes.Json);
                                        row.UpdateTime = DateTime.Now;
                                        await db2.UpdateAsync(row,
                                            nameof(EnterpriseOriginalFile.EssentialSummary),
                                            nameof(EnterpriseOriginalFile.UpdateTime));

                                        // 后续分析使用精要内容替代原文，节约 Token 并防止溢出
                                        analysisInput = row.EssentialSummary;
                                    }
                                }
                            }
                            else if (!string.IsNullOrWhiteSpace(row.EssentialSummary))
                            {
                                // 已有缓存，直接使用
                                analysisInput = row.EssentialSummary;
                            }

                            // ★ M6：作用域 = 本轮标准（std），⛔ 不再「按文件挑一个标准」
                            var one = await prompts2.AnalyzeForQueueAsync(
                                PromptWorkbenchService.Types.Content, std,
                                new List<(string, string?)> { (fileName, analysisInput) },
                                $"enterprise_original:{row.Code}:content:{ShortCode(std)}");

                            if (!one.Success)
                            {
                                await MarkFailedAsync(db2, row, one.Message);
                                return (FileName: fileName, analyzed: 0, failed: 1, ms: 0L, pt: 0, ct: 0);
                            }

                            var done = Interlocked.Increment(ref doneCounter);
                            await ReportProgressAsync(db2, queueCode, done, totalUnits);

                            // ⚠️ AI 建议 skip/ignore ⇒ 止于此，⛔ 不建画像、不改 AnalyzePolicy（36 号 R5）
                            var suggestion = ReadSuggestedPolicy(one.Json!);
                            if (suggestion is EnterpriseOriginalService.Policy.Skip
                                or EnterpriseOriginalService.Policy.Ignore)
                            {
                                await ApplyAiSuggestionOnlyAsync(db2, row, suggestion!, one.Json!);
                                return (FileName: fileName, analyzed: 1, failed: 0, ms: one.DurationMs, pt: one.PromptTokens, ct: one.CompletionTokens);
                            }

                            await UpsertProfileAsync(db2, row, std, one, groupByFile);
                            return (FileName: fileName, analyzed: 1, failed: 0, ms: one.DurationMs, pt: one.PromptTokens, ct: one.CompletionTokens);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "[原始资料分析] 单份并发分支异常: {Batch}/{Std}",
                                payload.BatchCode, std);
                            return (FileName: item.FileName, analyzed: 0, failed: 1, ms: 0L, pt: 0, ct: 0);
                        }
                        finally { gate.Release(); }
                    }).ToList();

                    var results = await Task.WhenAll(tasks);
                    analyzed += results.Sum(r => r.analyzed);
                    failed += results.Sum(r => r.failed);
                    cost += results.Sum(r => r.ms);
                    promptTokens += results.Sum(r => r.pt);
                    completionTokens += results.Sum(r => r.ct);

                    // 累积到文件级聚合（每个文件累计「成功/失败的标准数」）
                    foreach (var r in results)
                    {
                        if (!fileUnits.TryGetValue(r.FileName, out var st)) continue;
                        st.Ok += r.analyzed;
                        st.Fail += r.failed;
                    }
                }

                // ⑥ ★ M6 连带：文件级**聚合**状态（全部标准跑完才定终态，⛔ 不被后一个标准覆盖）
                foreach (var (fileName, st) in fileUnits)
                {
                    if (!byName.TryGetValue(fileName, out var row)) continue;

                    // 全部单元都被 AI 建议跳过（Ok=0 且 Fail=0）⇒ 保留 ApplyAiSuggestionOnlyAsync
                    // 已写的「AI 建议策略为 X（待人工确认）」状态，⛔ 不要覆盖成空 message。
                    if (st.Ok == 0 && st.Fail == 0) continue;

                    if (st.Ok == 0 && st.Fail > 0)
                    {
                        await MarkFailedAsync(db, row, $"全部 {standards.Count} 个标准分析失败");
                    }
                    else
                    {
                        row.AnalyzeStatus = EnterpriseOriginalService.AnalyzeStatus.Analyzed;
                        row.AnalyzeMessage = st.Fail > 0
                            ? $"部分标准分析失败（成功 {st.Ok} / 失败 {st.Fail}，共 {standards.Count} 个标准）"
                            : null;
                        row.AnalyzeTime = DateTime.Now;
                        row.UpdateTime = DateTime.Now;
                        await db.UpdateAsync(row,
                            nameof(EnterpriseOriginalFile.AnalyzeStatus),
                            nameof(EnterpriseOriginalFile.AnalyzeMessage),
                            nameof(EnterpriseOriginalFile.AnalyzeTime),
                            nameof(EnterpriseOriginalFile.UpdateTime));
                    }
                }

                _logger.LogInformation(
                    "[原始资料分析] 批次 {Batch}：标准 {StdCount} 个 / 可用 {Usable} / 成功 {Ok} / 失败 {Fail} / 分组失败批 {GFail} / 并发 {C} / 累计 {Cost}ms {PT}tok {CT}tok",
                    payload.BatchCode, standards.Count, usable.Count, analyzed, failed,
                    groupFailures.Count, MaxConcurrency, cost, promptTokens, completionTokens);

                // ★ M8-2：分组失败**如实回报**，⛔ 不再「Success=true + 一条 LogWarning」
                //   （旧行为：doc_group 整批被拒 ⇒ groupByFile 空 ⇒ 分组全丢，页面却显示分析成功。）
                var msgParts = new List<string> { $"分析完成：{standards.Count} 个标准 × {usable.Count} 份" };
                if (analyzed > 0) msgParts.Add($"成功 {analyzed}");
                if (failed > 0) msgParts.Add($"失败 {failed}");
                if (groupFailures.Count > 0)
                    msgParts.Add($"分组失败 {groupFailures.Count} 批（{groupFailures[0]}）");

                return new TaskExecutionResult
                {
                    Success = failed == 0 && groupFailures.Count == 0,
                    Message = string.Join("；", msgParts),
                    // ★ 只有「逐份内容分析失败」才重试；分组失败重试会整批重跑
                    //   （画像版本无谓递增、LLM 成本翻倍）⇒ 不重试，只如实回报
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

        /// <summary>
        /// ★ <b>M6（2026-10-06）</b>：确定本批要分析的<b>标准列表</b>（GUID，有序）。
        ///
        /// <para><b>与旧 <c>PickScopeAsync</c> 的区别（这是 M6 的核心）</b>：旧实现是
        /// 「候选里挑<b>第一个</b>能解析到提示词的标准」，注释自认其动机是「解决提示词绑定问题」——
        /// 那是<b>把「提示词没绑这个标准」误报成「分析失败」</b>的权宜之计，代价是
        /// <b>一个阶段关联多标准时只产一份画像</b>，而 <c>upsert</c> 的 latest 查询又不含标准
        /// ⇒ 后分析的标准会<b>顶掉</b>先前标准的画像。本方法改为<b>全遍历</b>。</para>
        ///
        /// <para><b>判定口径</b>：一个标准「可分析」⇔ 它能解析到<b>生效的 <c>doc_content</c> 提示词</b>
        /// （<c>doc_content</c> 是产出画像的必要条件）。<c>doc_group</c> 缺失<b>不算</b>不可分析 ——
        /// 那样只是拿不到分组，仍应产出「有作用、无标签」的画像，并由
        /// <see cref="RunGroupBatchedAsync"/> 的失败清单<b>如实回报</b>。</para>
        /// </summary>
        private async Task<List<string>> ResolveStandardsAsync(
            IDbOrm db, PromptWorkbenchService prompts, EnterpriseOriginalAnalyzePayload payload)
        {
            // ① 载荷显式指定 ⇒ 只分析其中真能解析到提示词的（解析不到的**如实告警**，⛔ 不静默）
            var wanted = (payload.StandardCodes ?? new List<string>())
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c!.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (wanted.Count > 0)
            {
                var usable = new List<string>();
                foreach (var code in wanted)
                {
                    if (await HasContentPromptAsync(prompts, code)) usable.Add(code);
                    else _logger.LogWarning(
                        "[原始资料分析] 载荷指定的标准 {Std} 未配置生效的 doc_content 提示词，跳过", code);
                }
                return usable;
            }

            // ② 载荷为空 ⇒ 按企业×阶段解析**全部**可分析标准（兼容旧入队方）
            var candidates = await ResolveScopesAsync(db, payload.EnterpriseCode, payload.StageCode);
            var list = new List<string>();
            foreach (var code in candidates)
                if (await HasContentPromptAsync(prompts, code)) list.Add(code);

            if (candidates.Count > 0 && list.Count < candidates.Count)
                _logger.LogInformation(
                    "[原始资料分析] 企业×阶段关联 {Total} 个标准，其中 {Ok} 个配置了 doc_content 提示词（其余跳过）",
                    candidates.Count, list.Count);

            return list;
        }

        /// <summary>该标准是否配置了<b>生效的</b> <c>doc_content</c> 提示词（产出画像的必要条件）</summary>
        private static async Task<bool> HasContentPromptAsync(PromptWorkbenchService prompts, string? standardCode)
        {
            var t = await prompts.ResolveActiveAsync(PromptWorkbenchService.Types.Content, standardCode);
            return t != null && !string.IsNullOrWhiteSpace(t.Template);
        }

        /// <summary>
        /// ★ <b>M8-2</b>：把整批切成 ≤<see cref="GroupBatchSize"/> 的小批，逐批调 <c>doc_group</c>，
        /// 结果**合并进同一个字典**。
        ///
        /// <para>⛔ <b>不再静默降级</b>：任一批失败都进 <c>Failures</c> 由调用方<b>如实回报</b>，
        /// 而<b>不是</b>「只 <c>LogWarning</c> 然后当作成功」（旧行为会让页面显示「分析成功」而分组全丢）。</para>
        /// </summary>
        /// <returns><c>ByFile</c> = fileName → 该文件的 doc_group 结论；<c>Failures</c> = 失败批的描述（空 = 全成功）</returns>
        private async Task<(Dictionary<string, JsonElement> ByFile, List<string> Failures)> RunGroupBatchedAsync(
            PromptWorkbenchService prompts, string? standardCode,
            List<(string FileName, string? Markdown)> usable, string batchCode)
        {
            var byFile = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            var failures = new List<string>();

            for (var i = 0; i < usable.Count; i += GroupBatchSize)
            {
                var slice = usable.Skip(i).Take(GroupBatchSize).ToList();
                var batchNo = i / GroupBatchSize + 1;

                var res = await prompts.AnalyzeForQueueAsync(
                    PromptWorkbenchService.Types.Group, standardCode, slice,
                    $"enterprise_original:{batchCode}:group:{ShortCode(standardCode)}:{batchNo}");

                if (res.Success && !string.IsNullOrWhiteSpace(res.Json))
                {
                    foreach (var kv in SemanticHints.ParseGroupItems(res.Json!))
                        byFile[kv.Key] = kv.Value;
                }
                else
                {
                    failures.Add($"第 {batchNo} 批（{slice.Count} 份）失败：{res.Message}");
                }
            }

            return (byFile, failures);
        }

        /// <summary>
        /// 在给定标准列表里挑第一个<b>能解析到指定类型提示词</b>的（用于 <c>doc_essential</c> 这类
        /// 「与标准无关、随便用哪个标准都行」的提示词）。全都不行则返回 <c>null</c>。
        /// </summary>
        private static async Task<string?> PickAnyScopeAsync(
            PromptWorkbenchService prompts, List<string> standards, string promptType)
        {
            foreach (var code in standards)
            {
                var t = await prompts.ResolveActiveAsync(promptType, code);
                if (t != null && !string.IsNullOrWhiteSpace(t.Template)) return code;
            }
            return null;
        }

        /// <summary>日志/计费键里用的短码（GUID 前 8 位）—— ⛔ 不用于任何业务判定</summary>
        private static string ShortCode(string? code)
            => string.IsNullOrWhiteSpace(code) ? "platform" : code!.Substring(0, Math.Min(8, code.Length));

        // ========================================================
        // L1 结果解析与回写
        // ========================================================

        // ★ 2026-10-05：`ParseGroupItems` / `ExtractGroupHints` / `ExtractContentHints`
        //   已**下沉到 `CertPlatform.Shared.Fill.SemanticHints`**。
        //
        //   原因：Admin 侧（`StandardDocContractController` 的标准文档语义分析）需要**同一套**
        //   提取口径，而 `Admin` ⛔ 不引用 `Auditor`（引用方向是 Auditor → Admin）⇒ 只能下沉到
        //   双方都引用的 `Shared`。⛔ **不要在本类里再抄一份** —— 复制即漂移，33 号的输出校验
        //   口径一旦分叉，两张表的画像字段就会长出不同形状。

        /// <summary>转发到 <see cref="SemanticHints.ReadString"/>（本类 <c>ReadSuggestedPolicy</c> 仍在用）</summary>
        private static string? ReadString(JsonElement el, string prop) => SemanticHints.ReadString(el, prop);

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
        ///
        /// <para>★ <b>M6（2026-10-06）</b>：<c>PolicyReason</c>/<c>PolicySource</c> 在<b>文件表</b>上是
        /// <b>单列</b>（一行一文件），而 <c>doc_group</c> 的结论是「文件 × 标准」⇒ 多标准下后一个标准
        /// 会顶掉前一个的建议。取<b>「首个写入者胜」</b>：已有 AI 建议时不覆盖。</para>
        /// <para>⚠️ 这是「文件级单列 vs 标准级结论」的<b>已知取舍</b>（已登记待裁）——
        /// 若将来要把 AI 建议也做成「按标准」，须在画像表加列，⛔ 不要在文件表上再塞 JSON。</para>
        /// </summary>
        private static async Task ApplyGroupResultAsync(IDbOrm db, EnterpriseOriginalFile row, JsonElement g)
        {
            // ★ M6：首个写入者胜 —— 已有 AI 建议（PolicySource='ai' 且有原因）则不再覆盖
            if (string.Equals(row.PolicySource, "ai", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(row.PolicyReason))
                return;

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
        ///
        /// <para>★★ <b>M6（2026-10-06）：版本号与 IsLatest 都<b>按 (文件, 标准) 独立</b>。</b></para>
        /// <list type="bullet">
        ///   <item>旧实现 latest 查询<b>只按 <c>OriginalFileCode</c></b> ⇒ 标准 B 分析时把标准 A 的
        ///         <c>IsLatest</c> 置 0，<b>A 的画像被顶掉</b>（正是用户说的「匹配时会出现很大的问题」）。</item>
        ///   <item>现在 <c>ProfileVersion</c> 按 (文件, 标准) 各自递增、<c>IsLatest</c> 只对
        ///         <b>同文件 + 同标准</b> 的旧行置 0 ⇒ 各标准互不干扰。</item>
        ///   <item>⚠️ 与 <c>uk_file_ver</c> 唯一键<b>必须同步</b>：DB 侧须为
        ///         (<c>OriginalFileCode</c>, <c>StandardCode</c>, <c>ProfileVersion</c>)，
        ///         否则多标准写入会<b>撞唯一键</b>。</item>
        /// </list>
        /// </summary>
        private async Task UpsertProfileAsync(
            IDbOrm db, EnterpriseOriginalFile row, string? scopeStandardCode,
            PromptWorkbenchService.AnalyzeForQueueResult one,
            Dictionary<string, JsonElement> groupByFile)
        {
            var std = scopeStandardCode ?? "";

            // ★ M6：latest 必须**同时**限定标准，否则跨标准顶掉
            var latest = (await db.GetListAsync<EnterpriseDocProfile>(
                x => x.OriginalFileCode == row.Code && x.StandardCode == std
                     && x.IsLatest == true && x.IsValid == 1)).Data?
                .OrderByDescending(x => x.ProfileVersion).FirstOrDefault();

            var nextVersion = (latest?.ProfileVersion ?? 0) + 1;

            var (tagsJson, tagsReason, tagsConf, typeGuess, keywords, summary, docCategory) =
                SemanticHints.ExtractGroupHints(groupByFile, row.FileName, one.ValidationMessages);

            var (purpose, purposeConf, infoItems, fields, tables, conf) =
                SemanticHints.ExtractContentHints(one.Json!);

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
                EssentialSummary = row.EssentialSummary, // ★ 带入精要内容

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
                // ★ M6：只置「同文件 + 同标准」的旧行（latest 查询已限定 StandardCode）
                latest.IsLatest = false;
                await db.UpdateAsync(latest, nameof(EnterpriseDocProfile.IsLatest));
            }
            await db.InsertAsync(profile);

            // ⚠️ 同步更新主行的 EssentialSummary（如果画像里带了的话，做个备份）
            if (!string.IsNullOrWhiteSpace(profile.EssentialSummary) && string.IsNullOrWhiteSpace(row.EssentialSummary))
            {
                row.EssentialSummary = profile.EssentialSummary;
                await db.UpdateAsync(row, nameof(EnterpriseOriginalFile.EssentialSummary));
            }

            // ★ M6 连带：**不在这里**写文件级 `AnalyzeStatus`。
            //   文件表的状态是**单列**，而分析单元是「文件 × 标准」⇒ 逐标准写会互相覆盖。
            //   改由 ExecuteAsync 在**全部标准跑完后**按 `FileUnitState` 聚合写回（见 ⑥ 段）。
        }

        private static string? ExtractSummaryFromJson(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("summary", out var s))
                    return s.GetString();
                return json; // 兜底返回全文
            }
            catch { return json; }
        }

        // ★ `ExtractGroupHints` / `ExtractContentHints` 已下沉到
        //   `CertPlatform.Shared.Fill.SemanticHints`（见上方注释），此处不再保留实现。

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
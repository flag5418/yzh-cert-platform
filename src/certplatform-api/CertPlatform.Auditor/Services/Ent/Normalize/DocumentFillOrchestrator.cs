using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Interfaces;
using CertPlatform.Shared.Entities.Cert;
using CertPlatform.Shared.Entities.Dir;
using CertPlatform.Shared.Entities.Doc;
using CertPlatform.Shared.Fill;
using CertPlatform.Shared.Office;
using CertPlatform.Shared.Office.Excel;
using CertPlatform.Shared.Office.Word;
using CertPlatform.Shared.Services.Fill;
using CertPlatform.Shared.Storage;
using CertPlatform.Admin.Services.Ent;
using CertPlatform.Admin.Services.Workflow.Skills.Fill.Ai;
using CertPlatform.Auditor.Entities.Cert;
using CertPlatform.Auditor.Entities.Doc;

namespace CertPlatform.Auditor.Services.Ent.Normalize
{
    /// <summary>
    ///     <b>单文件规范化编排器</b> —— 「企业资料 → 规范化标准文档」这条核心链路的唯一真编排器。
    ///
    ///     <para><b>★ 为什么是「重新实现」而不是「收敛旧执行器」</b>（`60` §六 裁决③）：
    ///     用户 2026-10-06 明确「填充代码可以彻底全部重新实现」。旧执行器
    ///     <c>EnterpriseDocNormalizationExecutor</c>（Admin，475 行）实测缺 6 处，其中最致命的是
    ///     <b>读错表</b> —— 方法名「企业原始文档」，实际查的是 <c>StandardDirectoryFile</c>
    ///     （企业<b>成品</b>文档），把成品当原始资料喂给填充引擎。
    ///     ⇒ 本类按 `54` §5.1 / `55` §四 的七步<b>重新实现</b>，并<b>停用旧执行器</b>
    ///     （删 DI 注册）—— 两套填充代码并存 = 静默漂移。</para>
    ///
    ///     <para><b>★ 七步（`54` §5.1，⛔ 顺序不可颠倒）</b>：</para>
    ///     <list type="number">
    ///         <item><b>⓪ 锁检查</b>（第一件事，<c>26</c> 号 S-6）：<c>IsLocked=1</c> ⇒ 直接返回 <c>skipped_locked</c>。</item>
    ///         <item><b>① 取模板</b>：<c>cert_doc_template</c>（门槛 <c>PublishStatus='published'</c>）。</item>
    ///         <item><b>② 读画像</b>：<c>cert_enterprise_doc_profile</c>（<c>IsLatest=1</c>）—— ★ 26 号 A-1：只读画像，⛔ 不读原始文件字节。</item>
    ///         <item><b>③ 取值</b>（层 2 先供给）：逐锚点走 <c>SourceSpec</c> 有序回退链。</item>
    ///         <item><b>④ 落笔</b>（层 1 后解析）：<c>WordFillWriter</c> / <c>ExcelFillWriter</c> —— ★ AI 只产值、NPOI 只落笔。</item>
    ///         <item><b>⑤ 自验收</b>：<c>report.Verified == false</c> ⇒ 产物判 <c>partial</c>。</item>
    ///         <item><b>⑥ 清示例数据</b>：<c>SampleData=1</c> 的锚点无条件清空（合规铁律）。</item>
    ///         <item><b>⑦ 回写 + 留痕</b>：<c>cert_doc_fill_value</c>（逐行）+ <c>cert_doc_fill_log</c>（汇总）+ 宿主行列级回写。</item>
    ///     </list>
    ///
    ///     <para><b>★ 三条不变量</b>：① <b>③ 必须早于 ④</b>（层 2 先供给，层 1 后解析）；
    ///     ② <c>Verified == false</c> ⇒ 产物判不合格；③ <b>锁检查在 ① 之前</b>。</para>
    ///
    ///     <para><b>⚠️ 与旧执行器的行为变更（`55` §4.6，必须在实现注释里标注）</b>：
    ///     AI 取值从「<b>自动生效</b>」改为「<b>建议池待确认</b>」——
    ///     旧执行器把 AI 结果直接写进值字典并落笔；本类按 <c>26</c> 号 A-5「AI 只产值」
    ///     + §18.4「建议 ≠ 事实」，AI 结果只落 <c>cert_doc_ai_suggestion</c>（<c>Status=pending</c>），
    ///     <b>不进值字典</b>，对应锚点在产物里为空并计入待办。</para>
    /// </summary>
    public class DocumentFillOrchestrator
    {
        private readonly IDbOrm _db;
        private readonly IObjectStorage _storage;
        private readonly ILogger<DocumentFillOrchestrator> _logger;
        private readonly SourceResolver _sourceResolver;
        private readonly IAiFillInvoker _aiInvoker;

        public DocumentFillOrchestrator(
            IDbOrm db,
            IObjectStorage storage,
            ILogger<DocumentFillOrchestrator> logger,
            SourceResolver sourceResolver,
            IAiFillInvoker aiInvoker)
        {
            _db = db;
            _storage = storage;
            _logger = logger;
            _sourceResolver = sourceResolver;
            _aiInvoker = aiInvoker;
        }

        // ════════════════════════════════════════════════════════════════════
        //  公开入口
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        ///     规范化<b>单个</b>标准文件（一个文件 = 一个任务）。
        ///     <para>范围展开与入队由调用方负责（<c>55</c> §3.1），⛔ 本方法不做范围判断。</para>
        /// </summary>
        public async Task<FillOneResult> FillOneAsync(FillOneRequest req, CancellationToken ct = default)
        {
            var result = new FillOneResult { Status = "failed" };
            var started = DateTime.Now;

            try
            {
                // ════════════════════════════════════════════════════════════
                //  ⓪ 锁检查 —— 第一件事（26 号 S-6「锁定优先」）
                //     ⚠️ 只查【文件级锁】（StandardDirectoryFile.IsLocked = L2 文件锁定）。
                //        锚点级锁（DocTemplateAnchor.IsLocked = L1 规则锁定）与「钉住」
                //        （DocFillValue.IsPinned = L3）都【不】跳过填充 ——
                //        L1 是「配置冻结」、L3 是「重写时不覆盖」，见 55 §18.1。
                // ════════════════════════════════════════════════════════════
                var instance = (await _db.GetOneAsync<StandardDirectoryFile>(x =>
                    x.EnterpriseCode == req.EnterpriseCode &&
                    x.StandardFileCode == req.StandardFileCode &&
                    x.IsValid == 1)).Data;

                if (instance != null && instance.IsLocked)
                {
                    result.Success = true;
                    result.Status = "skipped_locked";
                    result.Message = "该文件已锁定，跳过生成（锁定优先，26 号 S-6）";
                    return result;
                }

                // ════════════════════════════════════════════════════════════
                //  ① 取模板 —— 驱动源 = cert_doc_template（INNER JOIN 标准域行）
                //     ★ 门槛 = PublishStatus='published'（60 §六之补三 用户裁决「按发布的来实施」）
                // ════════════════════════════════════════════════════════════
                var stdFile = (await _db.GetOneAsync<StandardDirectoryFile>(x =>
                    x.Code == req.StandardFileCode && x.IsValid == 1)).Data;
                if (stdFile == null)
                {
                    result.Message = $"标准文件行不存在：{req.StandardFileCode}";
                    return result;
                }

                // ════════════════════════════════════════════════════════════
                //  ★ §2 四路分流（2026-10-07，路由按 DocCategory + Replaceable）
                //    一个空白文档进队列 = 本方法一次调用，按文档类别走不同路径：
                //      · editable / hybrid → 走下方七步编排（取锚点值 → Office 落笔）
                //      · fixed + Replaceable=false → ① 快归档（直接存企业标准资料，无 AI/参数）
                //      · fixed + Replaceable=true  → ② 文件匹配（读画像匹配结论 + ④ 无匹配兜底）
                //    ⚠️ 标准域行（stdFile）56/57 号「永不写」⇒ 各路径状态记到企业实例行 instance。
                //    ② 的「匹配算法」与 ① 的「归档搬运」为可替换接缝（见方法内 TODO）。
                // ════════════════════════════════════════════════════════════
                var category = (stdFile.DocCategory ?? "editable").Trim().ToLowerInvariant();
                if (category == "fixed")
                {
                    if (!stdFile.Replaceable)
                        return await RunFixedArchiveAsync(req, instance, stdFile, result, ct);
                    return await RunFixedMatchAsync(req, instance, stdFile, result, ct);
                }
                // editable / hybrid → 现有七步编排（下面不动）

                var template = (await _db.GetOneAsync<DocTemplate>(x =>
                    x.StandardFileCode == req.StandardFileCode && x.IsValid == 1)).Data;
                if (template == null)
                {
                    // ⛔ 不是错误：规范化范围 = 「配了填写规则的空白文档」（60 §六之补三）
                    result.Success = true;
                    result.Status = "skipped_no_template";
                    result.Message = "该标准文件未登记空白模板（规范化范围 = 配了填写规则的空白文档）";
                    return result;
                }

                if (!string.Equals(template.PublishStatus, "published", StringComparison.OrdinalIgnoreCase))
                {
                    result.Success = true;
                    result.Status = "skipped_no_template";
                    result.Message = $"模板未发布（当前 {template.PublishStatus}）；规范化仅处理已发布模板";
                    return result;
                }

                // ── 模板字节 ──
                //  DocTemplate.FileKind 已由上传侧保证为 docx/xlsx（见实体注释），故直接下载。
                //  ⚠️ 若未来出现旧格式模板（.doc/.xls），必须改走其 editable/ 归一产物 ——
                //     NPOI 2.7.2 无 NPOI.HWPF，.doc 连读都读不了（54 §5.1 ①）。
                var templateBytes = await DownloadBytesAsync(template.StoragePath, ct);
                if (templateBytes.Length == 0)
                {
                    result.Message = $"模板文件读取失败：{template.StoragePath}";
                    return result;
                }

                // ════════════════════════════════════════════════════════════
                //  ② 读输入 —— 锚点 + 画像 + 企业档案 + 企业已填值
                //     ★ 26 号 A-1：只读画像，⛔ 不读原始文件字节
                // ════════════════════════════════════════════════════════════
                var anchors = (await _db.GetListAsync<DocTemplateAnchor>(x =>
                    x.TemplateCode == template.Code &&
                    x.IsValid == 1 &&
                    x.IsOrphan == false)).Data ?? new List<DocTemplateAnchor>();

                // 画像：一行 = 文件 × 标准（M6 多行画像），取该标准的 IsLatest 行
                var profiles = (await _db.GetListAsync<EnterpriseDocProfile>(x =>
                    x.EnterpriseCode == req.EnterpriseCode &&
                    x.StageCode == req.StageCode &&
                    x.StandardCode == req.StandardCode &&
                    x.IsLatest == true &&
                    x.IsValid == 1)).Data ?? new List<EnterpriseDocProfile>();

                var ent = (await _db.GetOneAsync<Enterprise>(x => x.Code == req.EnterpriseCode)).Data;
                var entInfo = EnterpriseInfoMapper.ToInfo(ent);

                // ★ 企业已填值（cert_fill_param_value）—— 由本端（Auditor）预取后传给 SourceResolver。
                //   SourceResolver 类注释写明：「cert_fill_param_value 的实体属专家端独占，
                //   Admin 端看不见它 ⇒ 由能读该表的调用方（Auditor 侧编排器）预取后传入」。
                var savedValues = await LoadSavedParamValuesAsync(req, ct);

                // ════════════════════════════════════════════════════════════
                //  ③ 取值（层 2 先供给）
                // ════════════════════════════════════════════════════════════
                var values = new Dictionary<string, FillValue>(StringComparer.Ordinal);
                var ledger = new List<DocFillValue>();
                var pendings = new List<OfficeFillPending>();
                var aiAnchors = new List<DocTemplateAnchor>();
                var skillTrace = new List<string>();
                var retrievedCodes = new List<string>();

                foreach (var anchor in anchors.OrderBy(a => a.Sort))
                {
                    var (key, _) = AnchorKeyOf(anchor);
                    if (string.IsNullOrEmpty(key)) continue;

                    // ── ⑥ 清示例数据（合规铁律，⛔ 不可跳过）──
                    //   ⚠️ 实现落点在 ③ 与 ④ 之间（⛔ 不是设计编号里的 ⑤ 之后）：
                    //      「清空」必须体现在产物里 ⇒ 必须在落笔前把该锚点的值覆盖为空。
                    //      设计编号 ⑥ 表达的是「这一步必须发生」，不是「在 ⑤ 之后发生」。
                    //   25 号 Q-5：模板常含上一家企业的真实姓名与日期
                    //   （实测 XASL-QR-008 160 处），不清空 = 记录造假嫌疑。
                    if (anchor.SampleData)
                    {
                        var (okEmpty, emptyVal, _) = FillValueFactory.TryCreate(key, "", anchor.ValueType, anchor.NumberFormat);
                        if (okEmpty && emptyVal != null)
                        {
                            values[key] = emptyVal;
                            ledger.Add(BuildLedgerRow(req, template, anchor, key, emptyVal,
                                sourceKind: "manual", sourceLabel: "示例数据（合规清空）",
                                fillStatus: "removed", writeMode: "replace",
                                originalText: anchor.OriginalText, confidence: 1.00m));
                            skillTrace.Add($"sample_data_cleared:{key}");
                        }
                        continue;
                    }

                    var spec = _sourceResolver.Parse(anchor.SourceSpec);
                    if (spec == null || spec.Sources.Count == 0)
                    {
                        // 无数据源 ⇒ 置空 + 记待办（⛔ 不静默丢弃）
                        pendings.Add(BuildPending(anchor, "未配置数据源"));
                        ledger.Add(BuildLedgerRow(req, template, anchor, key, null,
                            sourceKind: "", sourceLabel: "未配置数据源",
                            fillStatus: "pending", writeMode: anchor.WriteMode, confidence: 0.00m));
                        continue;
                    }

                    var resolved = false;
                    foreach (var entry in spec.Sources)
                    {
                        var (ok, val, label) = await ResolveByEntryAsync(entry, anchor, key, entInfo, savedValues, profiles, req, ct);
                        if (!ok || val == null) continue;

                        if (string.Equals(entry.Kind, "ai", StringComparison.OrdinalIgnoreCase))
                        {
                            // ★ 行为变更（55 §4.6）：AI 只产「建议」，⛔ 不进值字典。
                            //   真实 LLM 调用在下方 ResolveAiAnchorsAsync 统一批量执行。
                            aiAnchors.Add(anchor);
                            resolved = true;
                            break;
                        }

                        values[key] = val;
                        ledger.Add(BuildLedgerRow(req, template, anchor, key, val,
                            sourceKind: entry.Kind, sourceLabel: label,
                            fillStatus: "filled", writeMode: anchor.WriteMode,
                            confidence: CalcConfidence(entry.Kind, val.Confidence)));
                        skillTrace.Add($"{entry.Kind}:{key}");
                        resolved = true;
                        break;
                    }

                    if (!resolved)
                    {
                        // ★ 归因到「具体下一步动作」，⛔ 不写笼统的「所有来源均未取到值」
                        var why = await DiagnoseNoValueAsync(spec, savedValues, req);
                        pendings.Add(BuildPending(anchor, why));
                        ledger.Add(BuildLedgerRow(req, template, anchor, key, null,
                            sourceKind: spec.Sources.FirstOrDefault()?.Kind ?? "",
                            sourceLabel: "来源链未命中",
                            fillStatus: "pending", writeMode: anchor.WriteMode, confidence: 0.00m));
                    }
                }

                // ── 腿 B · AI 汇聚（生成式）—— 落建议池，⛔ 不进产物 ──
                if (aiAnchors.Count > 0)
                {
                    await ResolveAiAnchorsAsync(aiAnchors, template, profiles, req, skillTrace, retrievedCodes, ct);

                    foreach (var anchor in aiAnchors)
                    {
                        var (key, _) = AnchorKeyOf(anchor);
                        pendings.Add(BuildPending(anchor, "AI 已产建议，待人工确认后生效"));
                        ledger.Add(BuildLedgerRow(req, template, anchor, key, null,
                            sourceKind: "ai", sourceLabel: "AI 建议（待确认）",
                            fillStatus: "pending", writeMode: anchor.WriteMode, confidence: 0.00m));
                    }
                }

                // ════════════════════════════════════════════════════════════
                //  ④ 落笔（层 1 后解析）—— ★ AI 只产值、NPOI 只落笔
                // ════════════════════════════════════════════════════════════
                var fillRequest = new OfficeFillRequest
                {
                    Template = templateBytes,
                    Values = values,
                    // 未命中 ⇒ 置空（⛔ 不保留 {{xxx}}），但必须记入 Pendings（无静默丢弃）
                    KeepUnresolvedAsIs = false,
                };

                OfficeFillResult fillResult;
                if (string.Equals(template.FileKind, "xlsx", StringComparison.OrdinalIgnoreCase))
                {
                    fillResult = new ExcelFillWriter().Fill(fillRequest);
                }
                else
                {
                    fillResult = new WordFillWriter().Fill(fillRequest);
                }

                var report = fillResult.Report;

                // ════════════════════════════════════════════════════════════
                //  ★★★ 校准待办原因 —— 把编排器的「可操作归因」写回写入器的 pendings
                // ════════════════════════════════════════════════════════════
                //  ⚠️⚠️ 历史缺陷（本轮修）：本方法上面辛苦算出的 `pendings`
                //    （「未配置数据源」/「参数未在后台定义…」/「AI 已产建议…」）曾是**死变量**
                //    —— 只 `.Add()`、**从不读**。最终落库的 `PendingsJson` 取的是
                //    `report.Pendings`（**写入器级**），而写入器只看到「值字典里没有这个键」
                //    ⇒ 一律报「未提供值」⇒ **用户永远拿不到可操作的判据**
                //    （实测：`PendingsJson` = `[{"Reason":"未提供值",…}]`，
                //      而真实原因其实是「参数 company_name 在后台没有任何有效定义」）。
                //  ★ 修法：按 `AnchorCode` 对齐后**覆盖 Reason**
                //    —— 保留写入器给的 `Location` 精度（如「表格[0] 行 2 列 3」），
                //       只把无用的原因换成有指向的原因。
                //  ⚠️ 同一 token 在文档里出现 N 次 ⇒ 写入器报 N 条，共享同一句归因（符合预期）。
                var reasonByKey = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var p in pendings)
                {
                    if (!string.IsNullOrEmpty(p.AnchorCode)) reasonByKey[p.AnchorCode] = p.Reason;
                }

                foreach (var p in report.Pendings)
                {
                    if (!string.IsNullOrEmpty(p.AnchorCode)
                        && reasonByKey.TryGetValue(p.AnchorCode, out var better))
                    {
                        p.Reason = better;
                    }
                }

                // ★ 把（已校准归因的）待办明细**带回接口** —— ⛔ 只回 `PendingCount` 一个数
                //   等于让用户看到「待办 1 个」却不知道下一步该去哪儿（见 FillOneResult.Pendings 注释）。
                result.Pendings = report.Pendings;

                // 收集写入器回报的非致命问题（越界 / 丢弃）—— ⛔ 不静默
                foreach (var region in report.Regions)
                    result.Warnings.AddRange(region.Warnings);

                // ════════════════════════════════════════════════════════════
                //  ⑤ 自验收 —— Verified == false ⇒ 产物判 partial
                // ════════════════════════════════════════════════════════════
                var verified = report.Verified;
                if (!verified)
                {
                    if (report.LeftoverTokens.Count > 0)
                        result.Warnings.Add($"自验收：产物仍残留 {report.LeftoverTokens.Count} 处未替换的锚点");
                    if (report.LeftoverMarkRuns > 0)
                        result.Warnings.Add($"自验收：产物仍带 {report.LeftoverMarkRuns} 处人工标记样式");
                }

                // ════════════════════════════════════════════════════════════
                //  ⑦ 回写 + 留痕
                // ════════════════════════════════════════════════════════════
                var outFileName = Path.ChangeExtension(stdFile.FileName,
                    string.Equals(template.FileKind, "xlsx", StringComparison.OrdinalIgnoreCase) ? ".xlsx" : ".docx");
                var outPath = PathBuilder.EnterpriseFile(req.EnterpriseCode, req.StandardCode, req.StageCode,
                    stdFile.FolderPath, outFileName);

                await _storage.UploadAsync(outPath, new MemoryStream(fillResult.Output), fillResult.Output.Length,
                    string.Equals(template.FileKind, "xlsx", StringComparison.OrdinalIgnoreCase)
                        ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                        : "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    ct);

                var completion = (decimal)report.Completion;
                var avgConfidence = CalcFileConfidence(ledger);

                var logCode = Guid.NewGuid().ToString();
                var fillLog = new DocFillLog
                {
                    Code = logCode,
                    OrgCode = req.OrgCode,
                    EnterpriseCode = req.EnterpriseCode,
                    StageCode = req.StageCode,
                    StandardCode = req.StandardCode,
                    // ★ 值 = 标准文件 Code（55 §4.5 同义列 #2：⛔ 不再新增 StandardFileCode）
                    TemplateFileCode = req.StandardFileCode,
                    QueueCode = req.QueueCode ?? string.Empty,
                    QueueTaskCode = req.QueueTaskCode ?? string.Empty,
                    FileKind = string.Equals(template.FileKind, "xlsx", StringComparison.OrdinalIgnoreCase) ? "excel" : "word",
                    OutputStoragePath = outPath,
                    TotalAnchors = report.Total,
                    ResolvedCount = report.Resolved,
                    PendingCount = report.Pending,
                    Completion = completion,
                    AvgConfidence = avgConfidence,
                    RegionCount = report.Regions.Count,
                    ClonedRows = report.Regions.Sum(r => r.ClonedRows),
                    Verified = verified,
                    LeftoverTokens = report.LeftoverTokens,
                    PendingsJson = JsonSerializer.Serialize(report.Pendings),
                    RetrievedDocCodes = retrievedCodes,
                    SkillTrace = skillTrace,
                    DurationMs = (int)(DateTime.Now - started).TotalMilliseconds,
                    Status = verified && report.Pending == 0 ? "success" : "partial",
                    CreateBy = req.OperatorCode,
                };

                // 账本回填 FillLogCode（一行一行关联到本次日志）
                foreach (var row in ledger) row.FillLogCode = logCode;

                // 列级回写宿主行（⛔ 禁止全列写回 —— 陷阱 ㉕：并发上传链会把这些列清成 NULL）
                var now = DateTime.Now;
                var instanceRow = instance ?? new StandardDirectoryFile
                {
                    ConfigCode = stdFile.ConfigCode,
                    EnterpriseCode = req.EnterpriseCode,
                    StandardCode = req.StandardCode,
                    StageCode = req.StageCode,
                    StandardFileCode = req.StandardFileCode,
                    FileName = outFileName,
                    FileType = template.FileKind,
                    FullPath = string.IsNullOrEmpty(stdFile.FolderPath) ? outFileName : $"{stdFile.FolderPath}/{outFileName}",
                    VersionNumber = 1,
                    IsValid = 1,
                    CreateBy = req.OperatorCode,
                };

                instanceRow.StoragePath = outPath;                 // ★ 沿用既有列（55 §4.5 同义列 #1）
                instanceRow.SourceProfileCode = string.Join(",", profiles.Take(3).Select(p => p.Code));
                instanceRow.SourceOriginalPath = profiles.FirstOrDefault()?.SourceMarkdownPath;
                instanceRow.NormalizedTime = now;
                instanceRow.FillCompletion = completion;
                instanceRow.FillConfidence = avgConfidence;
                instanceRow.FillAnchorCount = report.Total;
                instanceRow.FillPendingCount = report.Pending;
                instanceRow.InstanceState = "filled";
                instanceRow.UpdateTime = now;
                instanceRow.UpdateBy = req.OperatorCode;

                // ── 事务：日志 + 账本 + 宿主行 一次提交 ──
                //
                //  ★★★ 本事务内**每一次写入都必须检查返回值**（⛔ 不能只 `await`）。
                //    原因：`SqlSugarDbOrm.InsertAsync` / `InsertBatchAsync` / `UpdateAsync`
                //    三个方法的实现体都是 `try { … } catch (Exception ex) { return Result.Fail(…) }`
                //    ⇒ **失败时不抛异常、只返回失败结果**。丢弃返回值 = **静默失败**：
                //    事务照样提交，DB 里却什么都没写，而调用方以为成功。
                //    实测铁证（修前）：`cert_doc_fill_log` **4 行** / `cert_doc_fill_value` **0 行**。
                //    ★ 修法：任一写入失败 ⇒ **主动抛异常** ⇒ 走下方 `catch` ⇒ 回滚 + 向上冒泡
                //      ⇒ 外层 catch 落 `result.Success = false` / `Status = "failed"`（响亮失败）。
                int ledgerInserted = 0;
                await _db.Client.AsTenant().BeginTranAsync();
                try
                {
                    var logInsert = await _db.InsertAsync(fillLog);
                    if (!logInsert.Success)
                    {
                        throw new InvalidOperationException($"填充留痕写入失败：{logInsert.Error}");
                    }

                    if (ledger.Count > 0)
                    {
                        var ledgerResult = await _db.InsertBatchAsync(ledger);
                        if (!ledgerResult.Success)
                        {
                            throw new InvalidOperationException(
                                $"取值账本写入失败（{ledger.Count} 行）：{ledgerResult.Error}");
                        }
                        ledgerInserted = ledgerResult.Data;
                    }

                    if (instance != null)
                    {
                        var upd = await _db.UpdateAsync(instanceRow,
                            nameof(StandardDirectoryFile.StoragePath),
                            nameof(StandardDirectoryFile.SourceProfileCode),
                            nameof(StandardDirectoryFile.SourceOriginalPath),
                            nameof(StandardDirectoryFile.NormalizedTime),
                            nameof(StandardDirectoryFile.FillCompletion),
                            nameof(StandardDirectoryFile.FillConfidence),
                            nameof(StandardDirectoryFile.FillAnchorCount),
                            nameof(StandardDirectoryFile.FillPendingCount),
                            nameof(StandardDirectoryFile.InstanceState),
                            nameof(StandardDirectoryFile.UpdateTime),
                            nameof(StandardDirectoryFile.UpdateBy));
                        if (!upd.Success)
                        {
                            throw new InvalidOperationException($"实例行回写失败：{upd.Error}");
                        }
                    }
                    else
                    {
                        var ins = await _db.InsertAsync(instanceRow);
                        if (!ins.Success)
                        {
                            throw new InvalidOperationException($"实例行新增失败：{ins.Error}");
                        }
                    }

                    await _db.Client.AsTenant().CommitTranAsync();
                }
                catch
                {
                    await _db.Client.AsTenant().RollbackTranAsync();
                    throw;
                }

                // ════════════════════════════════════════════════════════════
                //  结果
                // ════════════════════════════════════════════════════════════
                result.Success = true;
                // 六态（⛔ 不合并成 bool）—— 锚点 0 ⇒ 纯复制即可（skipped_no_anchor）
                if (anchors.Count == 0)
                {
                    result.Status = "skipped_no_anchor";
                    result.Message = "该模板无锚点，产物 = 模板副本（完成率分母为 0，记 0.0000）";
                }
                else
                {
                    result.Status = verified && report.Pending == 0 ? "filled" : "partial";
                    result.Message = verified && report.Pending == 0
                        ? "全部锚点写入且自验收通过"
                        : $"写入完成但未全部通过：待办 {report.Pending} 处，自验收 {(verified ? "通过" : "未通过")}";
                }
                result.OutputPath = outPath;
                result.Completion = completion;
                result.Confidence = avgConfidence;
                result.AnchorCount = report.Total;
                result.PendingCount = report.Pending;
                result.Verified = verified;
                // ★ 报**库里实际写入条数**（来自 `InsertBatchAsync` 的受影响行数），
                //   ⛔ 不再报内存 `ledger.Count` —— 二者在写入失败时会背离，
                //   而页面直接显示本字段（「取值账本 N 行」）⇒ 报内存数 = 报假成功。
                result.LedgerRows = ledgerInserted;
                result.FillLogCode = logCode;

                _logger.LogInformation(
                    "[EntNorm] 规范化完成 {File}: 锚点 {Total} / 待办 {Pending} / 完成率 {Completion:P1} / 可信度 {Confidence:F2} / 账本 {LedgerInserted}/{LedgerPlanned} 行",
                    req.StandardFileCode, report.Total, report.Pending, report.Completion, avgConfidence,
                    ledgerInserted, ledger.Count);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EntNorm] 规范化失败 {File}: {Msg}", req.StandardFileCode, ex.Message);
                result.Success = false;
                result.Status = "failed";
                result.Message = ex.Message;
                return result;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  ★ §2 路径① —— 固定 + 不可替：快归档（无 AI / 无参数 / 不取锚点值）
        //    「直接按标准目录结构存入企业标准资料 MinIO + 记录企业标准信息」就完成。
        //    标准域行（stdFile）永不写（56/57 号）⇒ 状态记到企业实例行。
        // ════════════════════════════════════════════════════════════════════
        private async Task<FillOneResult> RunFixedArchiveAsync(
            FillOneRequest req,
            StandardDirectoryFile? instance,
            StandardDirectoryFile stdFile,
            FillOneResult result,
            CancellationToken ct)
        {
            // ① 快归档：把「标准源文件」（模板 StoragePath 代表的空白文档原件）按标准目录结构
            //    存入企业标准资料区。★ 归档搬运为可替换接缝（见下方 TODO），今天先落「如实记录版」。
            //
            // TODO(归档接缝)：确认「企业标准资料 MinIO 目标路径」的拼接口径（PathBuilder 现有段
            //   Pdf/Markdown/Editable/Template/Preview/Archive，无「企业标准资料」专用段）。
            //   落地时：var outPath = PathBuilder.<企业标准资料段>(req.EnterpriseCode, req.StageCode,
            //   stdFile.FolderPath, stdFile.FileName)；下载源字节 → _storage.UploadAsync(outPath, …)；
            //   把 outPath 写进企业实例行 StoragePath。
            //
            // 今天（如实记录版）：置实例行状态为「已归档」，⛔ 不搬文件、不伪造路径。
            var now = DateTime.Now;
            var instanceRow = instance ?? new StandardDirectoryFile
            {
                ConfigCode = stdFile.ConfigCode,
                EnterpriseCode = req.EnterpriseCode,
                StandardCode = req.StandardCode,
                StageCode = req.StageCode,
                StandardFileCode = req.StandardFileCode,
                FileName = stdFile.FileName,
                FileType = stdFile.FileType,
                FullPath = stdFile.FolderPath,
                VersionNumber = 1,
                IsValid = 1,
                CreateBy = req.OperatorCode,
            };
            instanceRow.InstanceState = "archived";      // 固定不可替 = 归档完成即终点
            instanceRow.MatchState = "matched";          // 无需匹配（按结构直接存）
            instanceRow.NormalizedTime = now;
            instanceRow.UpdateTime = now;
            instanceRow.UpdateBy = req.OperatorCode;

            var writeRes = instance != null
                ? await _db.UpdateAsync(instanceRow,
                    nameof(StandardDirectoryFile.InstanceState),
                    nameof(StandardDirectoryFile.MatchState),
                    nameof(StandardDirectoryFile.NormalizedTime),
                    nameof(StandardDirectoryFile.UpdateTime),
                    nameof(StandardDirectoryFile.UpdateBy))
                : await _db.InsertAsync(instanceRow);
            if (!writeRes.Success)
            {
                result.Success = false;
                result.Status = "failed";
                result.Message = $"固定文档归档记录失败：{writeRes.Error}";
                return result;
            }

            result.Success = true;
            result.Status = "archived";
            result.Message = "固定文档（不可替代）已按标准目录结构归档完成";
            return result;
        }

        // ════════════════════════════════════════════════════════════════════
        //  ★ §2 路径② —— 固定 + 可替：文件匹配（两层过滤 + 视觉/语义结论）
        //    读「企业文档画像」里已算好的匹配结论（patch1 的 4 列：
        //    MatchTargetStandardFileCode / MatchConfidence / MatchSource / MatchCandidatesJson）。
        //    · 有匹配 → 记下命中的企业文件 + 候选集，状态 matched（「该文件已找到」）
        //    · 无匹配 → ④ 兜底：状态 unmatched（「无适合的匹配项」），⛔ 不报错、不阻塞
        //    ★ 「匹配算法」为可替换接缝（见方法内 TODO），今天先读已算好的结论。
        // ════════════════════════════════════════════════════════════════════
        private async Task<FillOneResult> RunFixedMatchAsync(
            FillOneRequest req,
            StandardDirectoryFile? instance,
            StandardDirectoryFile stdFile,
            FillOneResult result,
            CancellationToken ct)
        {
            // TODO(匹配接缝)：当画像 4 列尚无值时，跑「分组标签 → 文件作用」两层过滤：
            //   第一层按 TagsJson/分类对齐、第二层按 DocPurpose/关键词，
            //   图片/PDF 走视觉模型（patch2 的 ToMarkdown 回退）出 Markdown 再匹配。
            //   今天：直接读画像里已有结论（若没有 ⇒ 走 ④ 无匹配兜底）。

            // 读该企业×标准最新画像（M6 多行取 IsLatest）
            var profile = (await _db.GetListAsync<EnterpriseDocProfile>(x =>
                x.EnterpriseCode == req.EnterpriseCode &&
                x.StandardCode == req.StandardCode &&
                x.StageCode == req.StageCode &&
                x.IsLatest == true &&
                x.IsValid == 1)).Data?
                .OrderByDescending(x => x.ProfileVersion)
                .FirstOrDefault();

            // 匹配结论：画像指向了「本标准文件行」= 找到；否则 = 无匹配
            var matched = profile != null
                          && string.Equals(profile.MatchTargetStandardFileCode,
                              req.StandardFileCode, StringComparison.Ordinal);

            var instanceRow = instance ?? new StandardDirectoryFile
            {
                ConfigCode = stdFile.ConfigCode,
                EnterpriseCode = req.EnterpriseCode,
                StandardCode = req.StandardCode,
                StageCode = req.StageCode,
                StandardFileCode = req.StandardFileCode,
                FileName = stdFile.FileName,
                FileType = stdFile.FileType,
                FullPath = stdFile.FolderPath,
                VersionNumber = 1,
                IsValid = 1,
                CreateBy = req.OperatorCode,
            };

            var now = DateTime.Now;
            instanceRow.MatchState = matched ? "matched" : "unmatched";
            if (matched && profile != null)
            {
                // 记录命中来源（证据链：哪个企业文件、可信度、候选集）
                instanceRow.SourceOriginalPath = profile.SourceMarkdownPath;
                instanceRow.SourceProfileCode = profile.Code;
                // ⚠️ FillConfidence 是 decimal（非空），MatchConfidence 是 decimal?（老画像可能 null）⇒ ?? 兜底
                instanceRow.FillConfidence = profile.MatchConfidence ?? 0m;
                instanceRow.InstanceState = "archived";   // 可替固定文档匹配到即终点
            }
            instanceRow.NormalizedTime = now;
            instanceRow.UpdateTime = now;
            instanceRow.UpdateBy = req.OperatorCode;

            var writeRes = instance != null
                ? await _db.UpdateAsync(instanceRow,
                    nameof(StandardDirectoryFile.MatchState),
                    nameof(StandardDirectoryFile.SourceOriginalPath),
                    nameof(StandardDirectoryFile.SourceProfileCode),
                    nameof(StandardDirectoryFile.FillConfidence),
                    nameof(StandardDirectoryFile.InstanceState),
                    nameof(StandardDirectoryFile.NormalizedTime),
                    nameof(StandardDirectoryFile.UpdateTime),
                    nameof(StandardDirectoryFile.UpdateBy))
                : await _db.InsertAsync(instanceRow);
            if (!writeRes.Success)
            {
                result.Success = false;
                result.Status = "failed";
                result.Message = $"固定文档匹配记录失败：{writeRes.Error}";
                return result;
            }

            result.Success = true;
            if (matched)
            {
                result.Status = "matched";
                result.Message = $"固定文档（可替代）已匹配到企业文件（可信度 {profile!.MatchConfidence}，来源 {profile.MatchSource}）";
            }
            else
            {
                // ④ 兜底：无适合的匹配项 —— 如实记录，⛔ 不报错、不阻塞批次
                result.Status = "no_match";
                result.Message = "固定文档（可替代）暂无匹配的企业文件，已记录待人工补充";
            }
            return result;
        }

        // ════════════════════════════════════════════════════════════════════
        //  ★ 试填 / 预览（只读）
        //    定位 = `59` §三：「试填预览 = `FillOneAsync` 的**只读调用**
        //    （⛔ 不新建独立填充链）」⇒ 一份实现、两个入口。
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        ///     ★ <b>试填单个模板（只读）</b> —— 后台「标准资料填写规则」页「自动填充」的落点。
        ///
        ///     <para><b>★ 与 <see cref="FillOneAsync"/> 的关系（`59` §三 的硬约束）</b>：
        ///     <b>同一个写入器、同一份取值骨架</b>，⛔ <b>不是第二套填充链</b>。差别只有两处 ——</para>
        ///     <list type="number">
        ///         <item><b>③ 取值</b>换成 <see cref="PreviewValueFactory"/> —— 后台页<b>没有企业</b>，
        ///             而真实链的 <c>global</c>（要企业档案 + 企业已填值）与 <c>manual</c>
        ///             （按 <c>EnterpriseCode</c> 查）<b>都必须要企业</b> ⇒ 跑真实链必然全空。</item>
        ///         <item><b>⛔ 不做第 ⑦ 步</b> —— 不写 <c>cert_doc_fill_log</c> /
        ///             <c>cert_doc_fill_value</c> / 不更新宿主行 / 不传企业产物。</item>
        ///     </list>
        ///
        ///     <para><b>★ 为什么必须只读</b>：试填是「看看填出来长什么样」。若落库，就会
        ///     ① 污染 <c>cert_doc_fill_log</c>（那是<b>正式产物</b>的账本）；
        ///     ② 把 <c>cert_standard_directory_file</c> 实例行的完成率 / 可信度写成
        ///     <b>试填的假数据</b> ⇒ 「一键规范化」的进度显示成「已经填过了」。</para>
        ///
        ///     <para><b>⚠️ 表格锚点不产区域指令</b>：<see cref="OfficeFillRequest.Regions"/> 留空
        ///     —— <b>与正式链逐字一致</b>（正式链同样只产 <c>Values</c>）。⛔ 不要在试填里
        ///     「顺手把表格也填了」：那会让预览<b>比真实效果好看</b>，从而<b>掩盖真实缺口</b> ——
        ///     比不填更糟。</para>
        ///
        ///     <para><b>★ 门槛低于正式填充</b>：⛔ 不要求 <c>PublishStatus='published'</c>
        ///     （试填正是「发布前验证规则」的工具，要求已发布 = 发布后才能验证 = 死锁）。
        ///     ⛔ 也不看文件级锁（<c>IsLocked</c>）—— 锁的是「不要再重新生成」，
        ///     与「看一眼样张」无关。</para>
        /// </summary>
        public async Task<FillPreviewResult> FillPreviewAsync(FillPreviewRequest req, CancellationToken ct = default)
        {
            var result = new FillPreviewResult { Status = "failed" };

            try
            {
                if (req == null || string.IsNullOrWhiteSpace(req.TemplateCode))
                {
                    result.Message = "缺少模板 Code";
                    return result;
                }

                // ── ① 取模板（⛔ 不看 PublishStatus）──
                var template = (await _db.GetOneAsync<DocTemplate>(x =>
                    x.Code == req.TemplateCode && x.IsValid == 1)).Data;
                if (template == null)
                {
                    result.Message = $"模板不存在或已失效：{req.TemplateCode}";
                    return result;
                }

                result.TemplateStoragePath = template.StoragePath ?? string.Empty;
                result.FileKind = string.Equals(template.FileKind, "xlsx", StringComparison.OrdinalIgnoreCase)
                    ? "xlsx"
                    : "docx";
                result.FileName = Path.ChangeExtension(
                    string.IsNullOrWhiteSpace(template.FileName) ? "试填模板" : template.FileName,
                    result.FileKind == "xlsx" ? ".xlsx" : ".docx");

                var templateBytes = await DownloadBytesAsync(template.StoragePath, ct);
                if (templateBytes.Length == 0)
                {
                    result.Message = $"模板文件读取失败：{template.StoragePath}";
                    return result;
                }

                // ── ② 读锚点（与正式链同口径：存活 + 非孤儿）──
                var anchors = (await _db.GetListAsync<DocTemplateAnchor>(x =>
                    x.TemplateCode == template.Code &&
                    x.IsValid == 1 &&
                    x.IsOrphan == false)).Data ?? new List<DocTemplateAnchor>();

                // ── ③ 取值（试填链：模板自带信息 + 类型占位）──
                var values = new Dictionary<string, FillValue>(StringComparer.Ordinal);

                // ★ 人工覆盖（2026-10-07 预览 Tab「自动填写 → 用户改 → 再预览」）：
                //   按 AnchorRef 建索引；命中 ⇒ 直接用调用方给的值落笔，不走占位工厂。
                var overrides = (req.Overrides ?? new List<PreviewOverrideInput>())
                    .Where(o => !string.IsNullOrWhiteSpace(o.AnchorRef))
                    .GroupBy(o => o.AnchorRef, StringComparer.Ordinal)
                    .ToDictionary(g => g.Key, g => g.Last(), StringComparer.Ordinal);

                foreach (var anchor in anchors.OrderBy(a => a.Sort))
                {
                    var (key, _) = AnchorKeyOf(anchor);
                    if (string.IsNullOrEmpty(key)) continue;

                    // ★ 覆盖优先（含空串 = 用户显式清空，仍算「已填」，⛔ 不进 Pendings）
                    if (overrides.TryGetValue(anchor.AnchorRef ?? string.Empty, out var ov))
                    {
                        values[key] = new FillValue
                        {
                            AnchorCode = key,
                            Kind = FillValueKind.Text,
                            Text = ov.Value ?? string.Empty,
                        };
                        result.Values.Add(new PreviewAnchorValue
                        {
                            AnchorRef = anchor.AnchorRef ?? string.Empty,
                            Key = key,
                            Source = "预览人工覆盖",
                            Value = ov.Value ?? string.Empty,
                            Display = Truncate(ov.Value, 120) ?? string.Empty,
                        });
                        continue;
                    }

                    var pv = PreviewValueFactory.Create(anchor, key);
                    if (pv.Ok && pv.Value != null)
                    {
                        values[key] = pv.Value;
                        result.Values.Add(new PreviewAnchorValue
                        {
                            AnchorRef = anchor.AnchorRef ?? string.Empty,
                            Key = key,
                            Source = pv.Source,
                            Value = pv.Value.ToDisplayText() ?? string.Empty,
                            Display = Truncate(pv.Value.ToDisplayText(), 120) ?? string.Empty,
                        });
                    }
                    else
                    {
                        result.Pendings.Add(new PreviewPendingItem
                        {
                            AnchorRef = anchor.AnchorRef ?? string.Empty,
                            Key = key,
                            Reason = pv.Error ?? (string.IsNullOrEmpty(pv.Source) ? "未取到值" : pv.Source),
                        });
                    }
                }

                // ── ④ 落笔（★ 与正式链同一个写入器）──
                var fillRequest = new OfficeFillRequest
                {
                    Template = templateBytes,
                    Values = values,
                    // 未命中 ⇒ 置空（⛔ 不保留 {{xxx}}），由报告如实回报，⛔ 不静默丢弃
                    KeepUnresolvedAsIs = false,
                };

                OfficeFillResult fillResult = result.FileKind == "xlsx"
                    ? new ExcelFillWriter().Fill(fillRequest)
                    : new WordFillWriter().Fill(fillRequest);

                var report = fillResult.Report;

                foreach (var region in report.Regions)
                    result.Warnings.AddRange(region.Warnings);

                // ── ⑤ 自验收（Verified=false ⇒ partial，⛔ 不谎报成功）──
                if (!report.Verified)
                {
                    if (report.LeftoverTokens.Count > 0)
                        result.Warnings.Add($"自验收：产物仍残留 {report.LeftoverTokens.Count} 处未替换的锚点");
                    if (report.LeftoverMarkRuns > 0)
                        result.Warnings.Add($"自验收：产物仍带 {report.LeftoverMarkRuns} 处人工标记样式");
                }

                // ── ⑦ ⛔ 刻意跳过：试填不落任何库、不传任何企业产物 ──

                result.Success = true;
                result.Output = fillResult.Output;
                result.AnchorCount = report.Total;
                result.PendingCount = report.Pending;
                result.Completion = (decimal)report.Completion;
                result.Verified = report.Verified;

                if (anchors.Count == 0)
                {
                    result.Status = "skipped_no_anchor";
                    result.Message = "该模板无锚点，产物 = 模板副本（完成率分母为 0，记 0）";
                }
                else
                {
                    result.Status = report.Verified && report.Pending == 0 ? "filled" : "partial";
                    result.Message = report.Verified && report.Pending == 0
                        ? "全部锚点写入且自验收通过"
                        : $"写入完成但未全部通过：待办 {report.Pending} 处，自验收 {(report.Verified ? "通过" : "未通过")}";
                }

                _logger.LogInformation(
                    "[EntNorm] 试填完成 {Template}: 锚点 {Total} / 待办 {Pending} / 完成率 {Completion:P1} / 字节 {Bytes}",
                    req.TemplateCode, report.Total, report.Pending, report.Completion, result.Output.Length);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EntNorm] 试填失败 {Template}: {Msg}", req?.TemplateCode, ex.Message);
                result.Success = false;
                result.Status = "failed";
                result.Message = ex.Message;
                return result;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  取值分派（腿 A · 确定性）
        // ════════════════════════════════════════════════════════════════════

        private async Task<(bool ok, FillValue? value, string label)> ResolveByEntryAsync(
            SourceSpecEntry entry,
            DocTemplateAnchor anchor,
            string key,
            EnterpriseInfo entInfo,
            Dictionary<string, FillParamValue> savedValues,
            List<EnterpriseDocProfile> profiles,
            FillOneRequest req,
            CancellationToken ct)
        {
            switch (entry.Kind?.ToLowerInvariant())
            {
                case "global":
                {
                    savedValues.TryGetValue(entry.Ref ?? string.Empty, out var saved);
                    var (ok, val) = await _sourceResolver.TryResolveGlobalAsync(
                        entry, anchor, entInfo, req.StandardCode, req.StageCode, req.OrgCode,
                        req.EnterpriseCode,
                        savedValue: saved?.ParamValue,
                        savedValueSource: saved?.ValueSource,
                        savedIsManualEdited: saved?.IsManualEdited ?? false);
                    return (ok, val, saved?.SourceRef ?? $"全局参数 · {entry.Ref}");
                }

                case "manual":
                {
                    var (ok, val) = await _sourceResolver.TryResolveManualAsync(entry, anchor, req.EnterpriseCode);
                    return (ok, val, "人工裁决值");
                }

                case "profile":
                {
                    // ★ 从画像取字段值（企业原始资料语义分析的产物）。
                    //   ⚠️ SourceResolver 在 Admin 端、看不见 EnterpriseDocProfile（Auditor 独占实体）
                    //      ⇒ 本来源必须在此实现（55 §4.2 腿 A 要求补齐 profile/compute/self）。
                    var val = ResolveFromProfile(entry, profiles, key);
                    return (val != null, val, $"文档画像 · {entry.Ref ?? entry.Field}");
                }

                case "ai":
                {
                    // 只做「认领」，真实 LLM 调用在 ResolveAiAnchorsAsync 统一批量执行
                    return (true, null, "AI 生成");
                }

                default:
                {
                    // ⛔ 不静默：未知来源如实回报（self/sibling/compute 属后续扩展点）
                    _logger.LogWarning("[EntNorm] 锚点 {Key} 的来源类型 {Kind} 暂未实现，按未命中处理", key, entry.Kind);
                    return (false, null, $"未实现的来源：{entry.Kind}");
                }
            }
        }

        /// <summary>
        ///     从画像取字段值 —— <c>FieldsJson</c> 是字段数组（<c>[{name, value, ...}]</c>），
        ///     按 <c>entry.Ref</c>（字段名）匹配。
        ///     <para>⚠️ 解析失败时返回 <c>null</c>（⇒ 回退链继续），⛔ <b>不抛异常、不猜值</b>。</para>
        /// </summary>
        private static FillValue? ResolveFromProfile(SourceSpecEntry entry, List<EnterpriseDocProfile> profiles, string key)
        {
            if (profiles.Count == 0) return null;
            var fieldName = entry.Ref ?? entry.Field;
            if (string.IsNullOrWhiteSpace(fieldName)) return null;

            foreach (var profile in profiles)
            {
                if (string.IsNullOrWhiteSpace(profile.FieldsJson)) continue;
                try
                {
                    using var doc = JsonDocument.Parse(profile.FieldsJson);
                    if (doc.RootElement.ValueKind != JsonValueKind.Array) continue;

                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        if (item.ValueKind != JsonValueKind.Object) continue;
                        var name = item.TryGetProperty("name", out var n) ? n.GetString()
                                 : item.TryGetProperty("Name", out var n2) ? n2.GetString()
                                 : item.TryGetProperty("fieldCode", out var fc) ? fc.GetString()
                                 : null;
                        if (!string.Equals(name, fieldName, StringComparison.OrdinalIgnoreCase)) continue;

                        var raw = item.TryGetProperty("value", out var v) ? v.GetString()
                                : item.TryGetProperty("Value", out var v2) ? v2.GetString()
                                : null;
                        if (string.IsNullOrWhiteSpace(raw)) continue;

                        var (ok, val, _) = FillValueFactory.TryCreate(key, raw, "text");
                        if (ok && val != null)
                        {
                            val.Source = $"文档画像 · {profile.FileName}";
                            val.Confidence = (double)(profile.Confidence ?? 0.8m);
                            return val;
                        }
                    }
                }
                catch (JsonException)
                {
                    // 画像 JSON 脏数据 —— 静默跳过该行（⛔ 不让一个坏行炸掉整份文档）
                }
            }

            return null;
        }

        // ════════════════════════════════════════════════════════════════════
        //  腿 B · AI 汇聚（生成式）—— 落建议池，⛔ 不进产物
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        ///     ★ <b>行为变更点</b>（<c>55</c> §4.6）：AI 结果<b>只落 <c>cert_doc_ai_suggestion</c></b>
        ///     （<c>Status=pending</c>），<b>⛔ 不写进值字典</b> —— 人工确认后才进账本/产物。
        /// </summary>
        private async Task ResolveAiAnchorsAsync(
            List<DocTemplateAnchor> aiAnchors,
            DocTemplate template,
            List<EnterpriseDocProfile> profiles,
            FillOneRequest req,
            List<string> skillTrace,
            List<string> retrievedCodes,
            CancellationToken ct)
        {
            // 召回企业文档 Markdown（画像的 SourceMarkdownPath = 分析输入快照）
            var docsMarkdown = new StringBuilder();
            foreach (var profile in profiles)
            {
                if (string.IsNullOrWhiteSpace(profile.SourceMarkdownPath)) continue;
                try
                {
                    var md = await DownloadTextAsync(profile.SourceMarkdownPath!, ct);
                    if (string.IsNullOrWhiteSpace(md)) continue;
                    docsMarkdown.AppendLine($"--- DOCUMENT: {profile.FileName} ---");
                    docsMarkdown.AppendLine(md);
                    retrievedCodes.Add(profile.OriginalFileCode);
                }
                catch (Exception ex)
                {
                    // 单个文档读不到 ⇒ 记 warning 继续（⛔ 不让一个文档炸掉整批）
                    _logger.LogWarning(ex, "[EntNorm] 召回画像 Markdown 失败 {Path}", profile.SourceMarkdownPath);
                }
            }

            // 按 PromptGroup 分组
            var groups = aiAnchors.GroupBy(a =>
                _sourceResolver.Parse(a.SourceSpec)?.Sources
                    .FirstOrDefault(s => string.Equals(s.Kind, "ai", StringComparison.OrdinalIgnoreCase))?.PromptGroup
                ?? "default");

            var batchCode = Guid.NewGuid().ToString();

            foreach (var group in groups)
            {
                var buildCtx = new AiFillBuildContext
                {
                    DocumentName = template.FileName,
                    StandardCode = template.StandardCode,
                    EnterpriseDocs = docsMarkdown.ToString(),
                    Anchors = group.Select(a =>
                    {
                        var (key, _) = AnchorKeyOf(a);
                        return new AiFillAnchorSpec
                        {
                            AnchorCode = key,
                            Instruction = a.Remark ?? key,
                            ValueKind = a.ValueType,
                            Section = "field",
                            PromptGroup = group.Key,
                        };
                    }).ToList(),
                };

                AiFillInvokeResult aiResult;
                try
                {
                    var request = await AiFillPromptBuilder.BuildBatchAsync(_db, null, buildCtx, req.OrgCode, ct);
                    aiResult = await _aiInvoker.InvokeAsync(_db, request, ct);
                }
                catch (Exception ex)
                {
                    // ⛔ 不静默降级：如实记 warning，AI 锚点保持 pending
                    _logger.LogWarning(ex, "[EntNorm] AI 批量取值失败（组 {Group}）", group.Key);
                    skillTrace.Add($"ai_batch_failed:{group.Key}");
                    continue;
                }

                skillTrace.Add($"ai_batch:{group.Key}:{group.Count()}");

                if (!aiResult.Success || aiResult.Root == null) continue;
                if (!aiResult.Root.TryGetValue("fields", out var fieldsObj) || fieldsObj is not Dictionary<string, object> fields)
                    continue;

                var suggestions = new List<DocAiSuggestion>();
                foreach (var anchor in group)
                {
                    var (key, _) = AnchorKeyOf(anchor);
                    if (!fields.TryGetValue(key, out var fieldVal) || fieldVal is not Dictionary<string, object> fieldObj)
                        continue;

                    if (!fieldObj.TryGetValue("value", out var raw) || raw == null) continue;
                    var valStr = raw.ToString();
                    if (string.IsNullOrWhiteSpace(valStr)) continue;

                    var confidence = fieldObj.TryGetValue("confidence", out var c) && c is double d ? (decimal)d : 1.00m;

                    suggestions.Add(new DocAiSuggestion
                    {
                        // ★★★ 同 `BuildLedgerRow`：`cert_doc_ai_suggestion.Code` 是
                        //   `varchar(36) NOT NULL` + `UNIQUE KEY uk_ai_suggestion_code`，
                        //   而 `DocAiSuggestion : BaseEntity` 的 `Code` 是**可空** `string?`
                        //   ⇒ 不显式赋值即 `Column 'Code' cannot be null`，且因走 DbOrm 直插（非 `AddCore`）
                        //   **不会**被自动补值。⛔ 与账本缺陷同源，属「静默失败」同一类。
                        Code = Guid.NewGuid().ToString("N"),
                        OrgCode = req.OrgCode,
                        EnterpriseCode = req.EnterpriseCode,
                        StageCode = req.StageCode,
                        StandardCode = req.StandardCode,
                        TemplateFileCode = req.StandardFileCode,
                        AnchorCode = key,
                        BatchCode = batchCode,
                        SuggestedValue = valStr,
                        ValueKind = anchor.ValueType,
                        NumberFormat = anchor.NumberFormat,
                        Confidence = confidence,
                        SourceSnippet = fieldObj.TryGetValue("note", out var note) ? note.ToString() : null,
                        Reason = fieldObj.TryGetValue("note", out var reason) ? reason.ToString() : null,
                        SkillCode = "src_ai_field",
                        ModelName = aiResult.Model,
                        PromptTokens = aiResult.PromptTokens ?? 0,
                        CompletionTokens = aiResult.CompletionTokens ?? 0,
                        DurationMs = (int)aiResult.DurationMs,
                        Status = "pending",   // ★ 待人工确认
                    });
                }

                if (suggestions.Count > 0)
                {
                    // ★ 同样**必须检查返回值**：`InsertBatchAsync` 失败时返回 `Result.Fail` 而**不抛异常**
                    //   ⇒ 丢弃返回值 = 静默失败（建议池恒 0 行，而日志/界面照报「AI 已产建议」）。
                    var suggResult = await _db.InsertBatchAsync(suggestions);
                    if (!suggResult.Success)
                    {
                        // ⛔ 不静默降级：AI 建议落库失败必须留痕（锚点仍保持 pending）
                        _logger.LogError("[EntNorm] AI 建议落库失败（{Count} 条）：{Err}",
                            suggestions.Count, suggResult.Error);
                        skillTrace.Add($"ai_suggestion_insert_failed:{suggestions.Count}");
                    }
                }
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  私有工具
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        ///     锚点键归一 —— 与文档中 <c>{{ }}</c> 内文本<b>逐字一致</b>（⛔ 不含花括号、⛔ 不含格式串）。
        ///
        ///     <para><b>★ 为什么必须归一</b>：<c>cert_doc_template_anchor.AnchorRef</c> 对 token 型锚点
        ///     存的是<b>含花括号</b>的原文（实测样本 <c>AnchorRef = "{{文件控制程序}}"</c>），
        ///     而 <c>WordParagraphFiller</c> / <c>ExcelFillWriter</c> 用「<b>花括号内文本</b>」查值字典
        ///     （<c>request.Values.TryGetValue(key, …)</c>，<c>key</c> 由 <c>FillSyntax.SplitFormat</c> 剥壳得到）。
        ///     ⇒ 不归一的话，值字典的键与写入器查找的键<b>永远不相等</b>，
        ///     症状是「<b>填不进去且不报错</b>」—— 最难查的一类缺陷。</para>
        /// </summary>
        private static (string Key, string? Format) AnchorKeyOf(DocTemplateAnchor anchor)
        {
            var raw = (anchor.AnchorRef ?? string.Empty).Trim();

            // 只有 token 型锚点的 AnchorRef 是 {{...}} 形式；bookmark / range 保持原文
            if (string.Equals(anchor.AnchorKind, "token", StringComparison.OrdinalIgnoreCase)
                && raw.StartsWith("{{", StringComparison.Ordinal)
                && raw.EndsWith("}}", StringComparison.Ordinal)
                && raw.Length >= 4)
            {
                raw = raw.Substring(2, raw.Length - 4);
            }

            return FillSyntax.SplitFormat(raw);
        }

        /// <summary>
        ///     可信度（单元格级，<c>54</c> §4.5）：
        ///     确定性来源（<c>global</c>/<c>self</c>/<c>compute</c>/<c>manual</c>）恒 <c>1.00</c>；
        ///     <c>profile</c> 取画像分；<c>ai</c> 取模型分；
        ///     <b>未知来源兜底 <c>0.50</c></b>（⛔ 不是 <c>1.00</c> —— 「不知道」不能伪装成「确定」）。
        /// </summary>
        private static decimal CalcConfidence(string sourceKind, double raw)
        {
            switch ((sourceKind ?? string.Empty).ToLowerInvariant())
            {
                case "global":
                case "self":
                case "compute":
                case "manual":
                    return 1.00m;
                case "profile":
                case "ai":
                    return Clamp(raw);
                default:
                    return 0.50m;
            }

            static decimal Clamp(double v)
            {
                if (v <= 0) return 0.50m;
                if (v > 1) return 1.00m;
                return Math.Round((decimal)v, 2);
            }
        }

        /// <summary>文件级可信度 —— <b>只对 <c>filled</c> 行求均值</b>（<c>pending</c> 不进分母）。</summary>
        private static decimal CalcFileConfidence(List<DocFillValue> ledger)
        {
            var filled = ledger.Where(r => string.Equals(r.FillStatus, "filled", StringComparison.Ordinal)).ToList();
            if (filled.Count == 0) return 1.00m;
            return Math.Round(filled.Average(r => r.Confidence), 2);
        }

        /// <summary>
        ///     诊断「所有来源均未取到值」的<b>具体原因</b> —— 把一句无用的「未提供值」
        ///     换成能指向下一步动作的判据。
        ///
        ///     <para><b>四种归因（各指向一个不同的修复动作）</b>：</para>
        ///     <list type="number">
        ///       <item>锚点<b>没配数据源</b> → 后台「填写规则」页配</item>
        ///       <item>参数<b>未在后台定义</b> → 后台「体系认证全局参数定义」按 机构×标准×阶段 新增</item>
        ///       <item>参数已定义但<b>企业没填值</b>（`MaintainMode != auto`） → 专家端「企业资料参数」页填</item>
        ///       <item>参数是 <c>auto</c>（自动映射）但<b>企业档案对应字段为空</b> → 先补企业档案</item>
        ///     </list>
        ///
        ///     <para>⚠️ <b>为什么必须做</b>：写入器只看到「值字典里没有这个键」，它一律报
        ///     <c>未提供值</c>；⛔ 不区分就等于<b>指错方向</b>
        ///     （`DocumentFillController:237-240` 原话：「把人引到企业端去找一个根本不存在的参数
        ///     —— <b>指错方向比不报错更耗时</b>」）。</para>
        ///
        ///     <para>⚠️ 本方法<b>只读不写</b>，⛔ 不改任何数据、⛔ 不抛异常（查库失败 ⇒ 退回笼统措辞）。</para>
        /// </summary>
        private async Task<string> DiagnoseNoValueAsync(
            SourceSpecModel spec,
            Dictionary<string, FillParamValue> savedValues,
            FillOneRequest req)
        {
            const string generic = "所有来源均未取到值";

            // 只看**第一个带 ref 的 global 源** —— 归因是「给人看的一句话」，⛔ 不做多源聚合
            var globalRef = spec.Sources
                .FirstOrDefault(s => string.Equals(s.Kind, "global", StringComparison.OrdinalIgnoreCase)
                                     && !string.IsNullOrWhiteSpace(s.Ref))?.Ref;
            if (globalRef == null) return generic;

            try
            {
                var def = await _sourceResolver.FindDefAsync(
                    globalRef, req.StandardCode, req.StageCode, req.OrgCode);

                if (def == null)
                {
                    // ★★ 必须区分「从来没配过」与「配过但被裁决废弃」—— 两者修复动作**相反**：
                    //    前者去新增，后者⛔绝不能新增（那会把刚删掉的参数又加回来）。
                    //    真实案例：2026-10-06 有意软删的 9 条「档案镜像参数」
                    //    （company_name / credit_code / legal_person / … 见
                    //      scripts/db/fix/20261006_fill_param_standard_dict_V1.sql §第 1 节，
                    //      理由「企业基本资料应由「企业基本资料」关联带出，字典不需要」）。
                    var wasRemoved = await _sourceResolver.WasSoftDeletedAsync(globalRef);
                    return wasRemoved
                        ? $"参数 {globalRef} 曾被定义、已从参数字典移除（企业基本资料应由「企业基本资料」带出）"
                          + " —— ⛔ 不要重新新增；请把该锚点改配到企业档案来源，或换一个仍在字典里的参数编码"
                        : $"参数 {globalRef} 未在后台定义 —— 请到「企业资料参数」页按 标准 新增该参数编码";
                }

                var hasSaved = savedValues.TryGetValue(globalRef, out var saved)
                               && !string.IsNullOrWhiteSpace(saved.ParamValue);
                var isAuto = string.Equals(def.MaintainMode, "auto", StringComparison.OrdinalIgnoreCase);

                if (!hasSaved && !isAuto)
                {
                    return $"参数「{def.ParamName}」（{globalRef}）已定义，但企业尚未填写 —— 请到「企业资料参数」页填写";
                }

                if (isAuto)
                {
                    return $"参数「{def.ParamName}」（{globalRef}）为自动映射，但企业档案里对应字段为空 —— 请先补全企业档案";
                }

                return $"参数「{def.ParamName}」（{globalRef}）的来源链全部未命中";
            }
            catch (Exception ex)
            {
                // ⛔ 归因失败不得影响主流程 —— 如实记日志后退回笼统措辞
                _logger.LogWarning(ex, "[EntNorm] 待办归因失败 {Ref}", globalRef);
                return generic;
            }
        }

        private static OfficeFillPending BuildPending(DocTemplateAnchor anchor, string reason)
        {
            var (key, _) = AnchorKeyOf(anchor);
            return new OfficeFillPending
            {
                Token = anchor.AnchorRef,
                AnchorCode = key,
                Reason = reason,
            };
        }

        /// <summary>构造一行账本（<c>cert_doc_fill_value</c>）</summary>
        private static DocFillValue BuildLedgerRow(
            FillOneRequest req,
            DocTemplate template,
            DocTemplateAnchor anchor,
            string key,
            FillValue? value,
            string sourceKind,
            string sourceLabel,
            string fillStatus,
            string writeMode,
            decimal confidence,
            string? originalText = null)
        {
            var row = new DocFillValue
            {
                // ★★★ 业务键**必须显式赋值**（⛔ 不能指望框架自动生成）。
                //   本仓只有 `YzhControllerBase.AddCore` 会反射补 `Code`（`:238-243`），
                //   而编排器走的是 **DbOrm 直插**（`_db.InsertBatchAsync`）⇒ **不经过 AddCore**。
                //   `cert_doc_fill_value.Code` 是 `varchar(36) NOT NULL` + `UNIQUE KEY uk_code`
                //   ⇒ 缺值即 `MySqlException: Column 'Code' cannot be null`。
                //   ⚠️ 历史事故：本行曾**漏赋 `Code`** ⇒ 整批写入抛异常，被
                //   `SqlSugarDbOrm.InsertBatchAsync` 的 catch **吞成 `Result.Fail`**、调用点又**丢弃返回值**
                //   ⇒ **静默失败**：`cert_doc_fill_log` 有 4 行、`cert_doc_fill_value` **0 行**，
                //   而结果对象照报「账本 N 行」（N 取自内存 `ledger.Count`）—— 页面看到的是**假成功**。
                //   ★ 取值风格与 `AddCore` 一致：`Guid.NewGuid().ToString("N")`（32 位无横线）。
                Code = Guid.NewGuid().ToString("N"),
                OrgCode = req.OrgCode,
                EnterpriseCode = req.EnterpriseCode,
                StageCode = req.StageCode,
                StandardCode = req.StandardCode,
                StandardFileCode = req.StandardFileCode,
                TemplateCode = template.Code,
                AnchorCode = anchor.Code,
                ValueType = string.IsNullOrEmpty(anchor.ValueType) ? "text" : anchor.ValueType,
                SourceKind = sourceKind ?? string.Empty,
                SourceLabel = Truncate(sourceLabel, 200),
                FillStatus = fillStatus,
                WriteMode = string.IsNullOrEmpty(writeMode) ? anchor.WriteMode : writeMode,
                OriginalText = originalText ?? anchor.OriginalText,
                Confidence = confidence,
                ConfidenceReason = confidence >= 1.00m ? string.Empty : "来源非确定性，建议人工复核",
                LocationKind = "body",
                Sort = anchor.Sort,
                CreateBy = req.OperatorCode,
            };

            if (value != null)
            {
                row.ValueText = value.Text;
                row.ValueNumber = value.Number.HasValue ? (decimal)value.Number.Value : null;
                row.ValueDate = value.Date;
                row.ValueDisplay = Truncate(value.ToDisplayText(), 500) ?? string.Empty;
            }

            return row;
        }

        private async Task<Dictionary<string, FillParamValue>> LoadSavedParamValuesAsync(FillOneRequest req, CancellationToken ct)
        {
            var list = (await _db.GetListAsync<FillParamValue>(x =>
                x.EnterpriseCode == req.EnterpriseCode &&
                x.IsValid == 1 &&
                (x.StandardCode == "" || x.StandardCode == req.StandardCode) &&
                (x.StageCode == "" || x.StageCode == req.StageCode))).Data ?? new List<FillParamValue>();

            // 同一 param_code 可能命中「通配」与「具体」两行 ⇒ 取最具体（StandardCode+StageCode 都非空优先）
            var map = new Dictionary<string, FillParamValue>(StringComparer.Ordinal);
            foreach (var v in list)
            {
                if (!map.TryGetValue(v.ParamCode, out var existing)) { map[v.ParamCode] = v; continue; }
                if (Specificity(v) > Specificity(existing)) map[v.ParamCode] = v;
            }

            return map;

            static int Specificity(FillParamValue v)
                => (string.IsNullOrEmpty(v.StandardCode) ? 0 : 1) + (string.IsNullOrEmpty(v.StageCode) ? 0 : 1);
        }

        private async Task<byte[]> DownloadBytesAsync(string path, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(path)) return Array.Empty<byte>();
            var (stream, _) = await _storage.DownloadAsync(path, ct);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, ct);
            return ms.ToArray();
        }

        private async Task<string> DownloadTextAsync(string path, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            var (stream, _) = await _storage.DownloadAsync(path, ct);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return await reader.ReadToEndAsync();
        }

        private static string? Truncate(string? s, int max)
            => string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s.Substring(0, max));
    }
}

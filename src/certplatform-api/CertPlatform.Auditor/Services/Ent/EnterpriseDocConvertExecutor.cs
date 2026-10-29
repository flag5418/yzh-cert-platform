using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CertPlatform.Auditor.Entities.Dir;
using CertPlatform.Shared.DocExtraction;
using CertPlatform.Shared.Storage;
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
    /// 企业文档转换执行器（TaskType = <c>enterprise_doc_convert</c>，36 号 §4.1）。
    ///
    /// <para><b>职责</b>：① 格式归一（.doc/.xls → .docx/.xlsx）② 双产物
    /// （<b>PDF 用于预览</b> + <b>Markdown 保留企业原文、作为提取的唯一输入</b>）。</para>
    ///
    /// <para><b>★ 与老 <see cref="EnterpriseOriginalIngestExecutor"/> 的三点差异</b>：</para>
    /// <list type="number">
    ///   <item><b>表不同</b>：读写的行从 <c>cert_enterprise_original_file</c> 换成 <c>cert_enterprise_doc</c>
    ///         （去阶段、去分析列）。</item>
    ///   <item><b>没有分析段</b>：老执行器后接 <c>doc_group</c>/<c>doc_content</c> 打标签 + 画像；
    ///         新链路改成下游 <c>enterprise_clause_extract</c>（文档 × 标准），因为「按标准提取条款」
    ///         才是目标 1/2「判断」的输入，标签画像不再是必需中间层。</item>
    ///   <item><b>没有 AnalyzePolicy</b>：老模块的 skip/ignore 是「不进分析」的软开关；
    ///         新模块的跳过判据是「计划里没有这个标准」和「Markdown 没产出」，不需要人工策略列。</item>
    /// </list>
    ///
    /// <para><b>★ 转换能力不重复实现</b>：全部委托 <see cref="IFileConvertCore"/>
    /// （与标准目录 / 企业资料库共用同一份）。本类只负责「上传产物 + 按列写回」。</para>
    ///
    /// <para><b>⚠️ 状态枚举铁律</b>：<c>ConvertStatus</c> / <c>MarkdownStatus</c> 只能写
    /// <c>none/pending/converting/completed/failed/unsupported</c>，与前端 <c>convertStatus.ts</c> 逐字对齐；
    /// 写 <c>converted</c> ⇒ 徽标显示「未知状态」且<b>零报错</b>。</para>
    ///
    /// <para><b>★ 单例 + 根容器懒解析</b>：<c>QueueManager</c> 构造注入 <c>IEnumerable&lt;IYzhTaskExecutor&gt;</c>
    /// （含本类）⇒ 构造器注入会形成 DI 循环，必须运行时从根容器解析。</para>
    /// </summary>
    public class EnterpriseDocConvertExecutor : IYzhTaskExecutor
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<EnterpriseDocConvertExecutor> _logger;

        public EnterpriseDocConvertExecutor(
            IServiceProvider serviceProvider,
            ILogger<EnterpriseDocConvertExecutor> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public string TaskType => EnterpriseDocQueue.TaskTypeConvert;

        /// <summary>状态取值（与 <c>cert-share/src/utils/convertStatus.ts</c> 逐字一致）</summary>
        private static class St
        {
            public const string None = "none";
            public const string Pending = "pending";
            public const string Converting = "converting";
            public const string Completed = "completed";
            public const string Failed = "failed";
            public const string Unsupported = "unsupported";
        }

        public async Task<TaskExecutionResult> ExecuteAsync(YzhQueueTask task, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(task.Payload))
                return new TaskExecutionResult { Success = false, Message = "Payload 为空", Retryable = false };

            EnterpriseDocConvertPayload? payload;
            try
            {
                payload = JsonSerializer.Deserialize<EnterpriseDocConvertPayload>(task.Payload,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException ex)
            {
                return new TaskExecutionResult { Success = false, Message = $"Payload 解析失败：{ex.Message}", Retryable = false };
            }

            if (payload == null || string.IsNullOrWhiteSpace(payload.Code))
                return new TaskExecutionResult { Success = false, Message = "Payload 缺少 Code", Retryable = false };

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<IDbOrm>();
                var storage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();
                var core = scope.ServiceProvider.GetRequiredService<IFileConvertCore>();

                // ★ 执行时读行（⛔ 不信入队快照）：排队期间可能被人工替换/删除
                var row = (await db.GetOneIgnoreValidAsync<EnterpriseDoc>(x => x.Code == payload.Code)).Data;
                if (row == null)
                {
                    _logger.LogWarning("[企业文档转换] 文件行不存在: {Code}", payload.Code);
                    return new TaskExecutionResult { Success = false, Message = "文件行不存在", Retryable = false };
                }

                var fileName = row.FileName;
                var sourcePath = row.StoragePath;
                if (string.IsNullOrWhiteSpace(sourcePath))
                {
                    await FailBothAsync(db, row, "缺少存储路径（字节未落库）");
                    return new TaskExecutionResult { Success = false, Message = "缺少存储路径", Retryable = false };
                }

                // ① 读源字节
                byte[] content;
                try
                {
                    var (stream, _) = await storage.DownloadAsync(sourcePath.TrimStart('/'));
                    using var ms = new MemoryStream();
                    await stream.CopyToAsync(ms);
                    content = ms.ToArray();
                }
                catch (Exception ex)
                {
                    await FailBothAsync(db, row, $"源文件读取失败：{ex.Message}");
                    _logger.LogError(ex, "[企业文档转换] 源读取失败: {Code}", row.Code);
                    return new TaskExecutionResult { Success = false, Message = "源文件读取失败", Retryable = true };
                }

                if (content.Length == 0)
                {
                    await FailBothAsync(db, row, "源文件内容为空");
                    return new TaskExecutionResult { Success = false, Message = "源文件内容为空", Retryable = false };
                }

                cancellationToken.ThrowIfCancellationRequested();

                // ② 格式归一：老企业常交 .doc/.xls，转换容器只吃新格式
                var (finalName, finalContent) = await NormalizeAsync(core, fileName, content);
                if (finalContent == null)
                {
                    await FailBothAsync(db, row, "格式归一失败");
                    return new TaskExecutionResult { Success = false, Message = "格式归一失败", Retryable = false };
                }

                // ③ 预览链（PDF）—— 失败不影响提取链
                var pdfOk = await RunPdfChainAsync(db, storage, row, finalName, sourcePath, finalContent);

                // ④ 提取链（Markdown）—— 失败不影响预览链
                var mdOk = await RunMarkdownChainAsync(db, storage, row, finalName, sourcePath, finalContent);

                // ⑤ 聚合状态：Markdown 就绪 ⇒ 待提取；否则保持 none（没东西可提取）
                await SetExtractPendingAsync(db, row, mdOk);

                // ⑥ 自动接力「按标准提取条款」（P2）
                // ★ 尽力而为：提取队列建不出来**不能**让「转换成功」变成「转换失败」——
                //   用户会以为文件坏了。失败只记日志 + 由用户手动点「开始提取」补救。
                if (mdOk)
                {
                    try
                    {
                        var docSvc = scope.ServiceProvider.GetRequiredService<EnterpriseDocService>();
                        var (queued, qerr) = await docSvc.EnqueueExtractForFileAsync(row.Code!, "convert");
                        if (!string.IsNullOrEmpty(qerr))
                            _logger.LogWarning("[企业文档转换] 提取接力未入队 {Code}: {Err}", row.Code, qerr);
                        else
                            _logger.LogInformation("[企业文档转换] 提取接力入队 {Code}：{N} 个任务", row.Code, queued);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[企业文档转换] 提取接力异常（不阻断转换）: {Code}", row.Code);
                    }
                }

                _logger.LogInformation("[企业文档转换] {Code} PDF={Pdf} MD={Md}", row.Code, pdfOk, mdOk);

                return new TaskExecutionResult
                {
                    Success = pdfOk && mdOk,
                    Message = pdfOk && mdOk
                        ? "转换完成（PDF + Markdown）"
                        : $"转换部分失败（PDF={(pdfOk ? "OK" : "FAIL")} / MD={(mdOk ? "OK" : "FAIL")}）",
                    Retryable = !(pdfOk && mdOk),
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[企业文档转换] 任务异常: {TaskCode}", task.Code);
                return new TaskExecutionResult { Success = false, Message = ex.Message, Retryable = true };
            }
        }

        // ========================================================
        // ① 格式归一
        // ========================================================

        private static readonly Dictionary<string, string> NormalizeMap = new(StringComparer.OrdinalIgnoreCase)
        {
            [".doc"] = ".docx",
            [".xls"] = ".xlsx",
            [".ppt"] = ".pptx",
            [".odt"] = ".odt",
            [".ods"] = ".ods",
            [".odp"] = ".odp",
        };

        /// <summary>老格式（doc/xls/ppt）→ 新格式；已是新格式或不在归一表内则原样返回</summary>
        private static async Task<(string FileName, byte[]? Content)> NormalizeAsync(
            IFileConvertCore core, string fileName, byte[] content)
        {
            var ext = Path.GetExtension(fileName ?? "").ToLowerInvariant();
            if (!NormalizeMap.TryGetValue(ext, out var newExt)) return (fileName, content);
            if (string.Equals(ext, newExt, StringComparison.OrdinalIgnoreCase)) return (fileName, content);

            var r = await core.ConvertToFormatAsync(fileName, content, newExt.TrimStart('.'));
            if (!r.Success || r.Content == null)
            {
                // ⚠️ 归一失败**不阻断**：LibreOffice 的 PDF 转换本身就吃 .doc，
                //   先按原字节继续，实在不行再由双产物链报 failed
                return (fileName, content);
            }

            var dot = fileName.LastIndexOf('.');
            var renamed = dot > 0 ? fileName[..dot] + newExt : fileName + newExt;
            return (renamed, r.Content);
        }

        // ========================================================
        // ③ 预览链（PDF）
        // ========================================================

        private async Task<bool> RunPdfChainAsync(
            IDbOrm db, IObjectStorage storage, EnterpriseDoc row,
            string fileName, string sourcePath, byte[] content)
        {
            var core = _serviceProvider.GetRequiredService<IFileConvertCore>();

            await SetPdfStatusAsync(db, row, St.Converting, null);

            var r = await core.ConvertToPdfAsync(fileName, sourcePath, content);
            if (!r.Success)
            {
                await SetPdfStatusAsync(db, row, r.Status, r.Message);
                return false;
            }

            try
            {
                if (!r.Passthrough && r.Content != null && !string.IsNullOrEmpty(r.TargetPath))
                {
                    await storage.UploadAsync(r.TargetPath!.TrimStart('/'),
                        new MemoryStream(r.Content), r.Content.Length, r.ContentType ?? "application/pdf");
                }

                row.PreviewPdfPath = r.TargetPath;
                await SetPdfStatusAsync(db, row, St.Completed, r.Message);
                return true;
            }
            catch (Exception ex)
            {
                await SetPdfStatusAsync(db, row, St.Failed, $"产物上传失败：{ex.Message}");
                return false;
            }
        }

        // ========================================================
        // ④ 提取链（Markdown）
        // ========================================================

        private async Task<bool> RunMarkdownChainAsync(
            IDbOrm db, IObjectStorage storage, EnterpriseDoc row,
            string fileName, string sourcePath, byte[] content)
        {
            var core = _serviceProvider.GetRequiredService<IFileConvertCore>();

            await SetMdStatusAsync(db, row, St.Converting, null);

            var r = await core.ConvertToMarkdownAtAsync(fileName, sourcePath, content);
            if (!r.Success || r.Content == null || string.IsNullOrEmpty(r.TargetPath))
            {
                // ★ 图片 / PDF 走 anydoc 出不了 Markdown（unsupported）时，回退到<b>视觉模型 OCR</b>。
                //   ⛔ 不伪造内容：OCR 也失败则落到 failed 分支，如实报能力边界，
                //   ⛔ 绝不写占位文本（占位流进 LLM 会零报错地出错误结果）。
                if (UploadFilePolicy.IsImageOrPdf(row.FileType)
                    && _serviceProvider.GetService<IOcrProvider>() is { IsAvailable: true })
                {
                    var md = await core.OcrToMarkdownAsync(fileName, sourcePath, content);
                    if (md.Success && md.Content != null && md.Content.Length > 0
                        && !string.IsNullOrEmpty(md.TargetPath))
                    {
                        try
                        {
                            await storage.UploadAsync(md.TargetPath!.TrimStart('/'),
                                new MemoryStream(md.Content), md.Content.Length, "text/markdown");
                            row.MarkdownPath = md.TargetPath;
                            row.ConvertDate = DateTime.Now;
                            await SetMdStatusAsync(db, row, St.Completed,
                                string.IsNullOrWhiteSpace(md.Message)
                                    ? "Markdown 由视觉模型 OCR 提取（ai_vision_config）"
                                    : md.Message);
                            _logger.LogInformation("[企业文档转换] 视觉 OCR 回退成功: {Code} → {Path}",
                                row.Code, md.TargetPath);
                            return true;
                        }
                        catch (Exception ex)
                        {
                            await SetMdStatusAsync(db, row, St.Failed, $"视觉 OCR 产物上传失败：{ex.Message}");
                            return false;
                        }
                    }
                    _logger.LogInformation("[企业文档转换] 视觉 OCR 未产出 Markdown（{Msg}），按能力边界处理: {Code}",
                        md.Message, row.Code);
                }

                // ⛔ unsupported 是**能力边界**不是故障：如实报出来让用户走「人工填写」
                await SetMdStatusAsync(db, row, r.Status, r.Message);
                return false;
            }

            try
            {
                await storage.UploadAsync(r.TargetPath!.TrimStart('/'),
                    new MemoryStream(r.Content), r.Content.Length, r.ContentType ?? "text/markdown");

                row.MarkdownPath = r.TargetPath;
                row.ConvertDate = DateTime.Now;
                await SetMdStatusAsync(db, row, St.Completed, r.Message);
                return true;
            }
            catch (Exception ex)
            {
                await SetMdStatusAsync(db, row, St.Failed, $"产物上传失败：{ex.Message}");
                return false;
            }
        }

        // ========================================================
        // 状态写回（⚠️ 必须列级写，禁止 UpdateAsync(entity) 全列回写）
        // ========================================================

        /// <summary>
        /// 列级写回的必要性：PDF 链与 Markdown 链虽在同一任务内串行，但<b>重传/取消/人工改</b>
        /// 可与本任务并发。若用 <c>UpdateAsync(entity)</c> 全列写回，后完成方会把先完成方
        /// 刚写的字段覆盖回旧快照（<c>OfficeConvertService</c> 类注释记录的 2026-09-26 事故就是它，零报错）。
        /// </summary>
        private static async Task SetPdfStatusAsync(IDbOrm db, EnterpriseDoc row, string status, string? message)
        {
            row.ConvertStatus = status;
            row.ConvertMessage = message;
            if (status == St.Completed) row.ConvertDate = DateTime.Now;
            row.UpdateTime = DateTime.Now;
            await db.UpdateAsync(row,
                nameof(EnterpriseDoc.PreviewPdfPath),
                nameof(EnterpriseDoc.ConvertStatus),
                nameof(EnterpriseDoc.ConvertMessage),
                nameof(EnterpriseDoc.ConvertDate),
                nameof(EnterpriseDoc.UpdateTime));
        }

        private static async Task SetMdStatusAsync(IDbOrm db, EnterpriseDoc row, string status, string? message)
        {
            row.MarkdownStatus = status;
            row.MarkdownMessage = message;
            row.UpdateTime = DateTime.Now;
            await db.UpdateAsync(row,
                nameof(EnterpriseDoc.MarkdownPath),
                nameof(EnterpriseDoc.MarkdownStatus),
                nameof(EnterpriseDoc.MarkdownMessage),
                nameof(EnterpriseDoc.ConvertDate),
                nameof(EnterpriseDoc.UpdateTime));
        }

        /// <summary>
        /// Markdown 就绪 ⇒ <c>ExtractStatus='pending'</c>（等提取）；没就绪 ⇒ 保持 <c>none</c>。
        /// <para>⚠️ 只在本行仍是 <c>none</c> 时才置 pending —— 重新转换不应把已经 <c>done</c>
        /// 的提取状态打回 pending（提取产物仍在，是否重跑由上层按 <c>SourceSha256</c> 决定）。</para>
        /// </summary>
        private static async Task SetExtractPendingAsync(IDbOrm db, EnterpriseDoc row, bool mdOk)
        {
            var next = mdOk ? "pending" : "none";
            if (row.ExtractStatus == next) return;
            // 已 done / running 的不要降级（重转同 hash 时状态应保持）
            if (row.ExtractStatus is "done" or "running" or "partial") return;

            row.ExtractStatus = next;
            row.UpdateTime = DateTime.Now;
            await db.UpdateAsync(row,
                nameof(EnterpriseDoc.ExtractStatus),
                nameof(EnterpriseDoc.UpdateTime));
        }

        private static async Task FailBothAsync(IDbOrm db, EnterpriseDoc row, string message)
        {
            row.ConvertStatus = St.Failed;
            row.ConvertMessage = message;
            row.MarkdownStatus = St.Failed;
            row.MarkdownMessage = message;
            row.UpdateTime = DateTime.Now;
            await db.UpdateAsync(row,
                nameof(EnterpriseDoc.ConvertStatus),
                nameof(EnterpriseDoc.ConvertMessage),
                nameof(EnterpriseDoc.MarkdownStatus),
                nameof(EnterpriseDoc.MarkdownMessage),
                nameof(EnterpriseDoc.UpdateTime));
        }

        public Task OnTaskStateChangedAsync(YzhQueueTask task, string newStatus, string message)
        {
            _logger.LogInformation("[企业文档转换] 任务状态变更: {TaskCode} → {Status} ({Message})",
                task.Code, newStatus, message);
            return Task.CompletedTask;
        }
    }
}

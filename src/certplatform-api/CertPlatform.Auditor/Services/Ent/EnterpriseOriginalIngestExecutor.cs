using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
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
    /// 企业原始资料入库执行器（TaskType = <c>enterprise_original_ingest</c>，36 号 §八 T1.5）。
    ///
    /// <para><b>职责</b>：① 格式归一（.doc/.xls → .docx/.xlsx）② 双产物（PDF 预览 + Markdown 分析输入）。
    /// ⚠️ <b>分析段不在本类</b>：2026-10-06 起由文件级编排器 <see cref="EnterpriseOriginalFileExecutor"/>
    /// 在转换段之后<b>同一任务内串行</b>调用本类 + 分析执行器（本类亦可被历史 ingest 队列直接执行）。</para>
    ///
    /// <para><b>★ 单例 + 根容器懒解析</b>：<c>QueueManager</c> 构造注入 <c>IEnumerable&lt;IYzhTaskExecutor&gt;</c>
    /// （含本类）⇒ 构造器注入会形成 DI 循环，必须运行时从根容器解析。
    /// 照抄 <c>OfficeConvertTaskExecutor</c> 的既有注释与写法（<c>OfficeConvertTaskExecutor.cs:67-69</c>）。</para>
    ///
    /// <para><b>★ 转换能力不重复实现</b>：全部委托 <see cref="IFileConvertCore"/>
    /// （36 号 T1.1 抽出到 <c>CertPlatform.Shared</c>，与标准目录 / 企业资料库共用同一份）。
    /// 本类只负责「上传产物 + 按链写列」。</para>
    ///
    /// <para><b>⚠️ 状态枚举铁律</b>：<c>ConvertStatus</c> / <c>MarkdownStatus</c> 只能写
    /// <c>none/pending/converting/completed/failed/unsupported</c>，与前端 <c>convertStatus.ts</c> 逐字对齐；
    /// 写 <c>converted</c> ⇒ 徽标显示「未知状态」且<b>零报错</b>。</para>
    /// </summary>
    public class EnterpriseOriginalIngestExecutor : IYzhTaskExecutor
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<EnterpriseOriginalIngestExecutor> _logger;

        public EnterpriseOriginalIngestExecutor(
            IServiceProvider serviceProvider,
            ILogger<EnterpriseOriginalIngestExecutor> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public string TaskType => EnterpriseOriginalQueue.TaskTypeIngest;

        public async Task<TaskExecutionResult> ExecuteAsync(YzhQueueTask task, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(task.Payload))
                return new TaskExecutionResult { Success = false, Message = "Payload 为空", Retryable = false };

            EnterpriseOriginalIngestPayload? payload;
            try
            {
                payload = JsonSerializer.Deserialize<EnterpriseOriginalIngestPayload>(task.Payload,
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

                var row = (await db.GetOneIgnoreValidAsync<EnterpriseOriginalFile>(x => x.Code == payload.Code)).Data;
                if (row == null)
                {
                    _logger.LogWarning("[原始资料入库] 文件行不存在: {Code}", payload.Code);
                    return new TaskExecutionResult { Success = false, Message = "文件行不存在", Retryable = false };
                }

                // ★ L2 策略分流：skip / ignore ⇒ 只落档，**不入转换**
                //   （36 号 §3.5：AnalyzePolicy 只作用于 L2「数据来源」层，L0 存档永不忽略）
                if (payload.AnalyzePolicy is EnterpriseOriginalService.Policy.Skip
                    or EnterpriseOriginalService.Policy.Ignore)
                {
                    MarkSkipped(db, row, payload.AnalyzePolicy);
                    _logger.LogInformation("[原始资料入库] 人工策略={Policy}，只落档不入转换: {Code}",
                        payload.AnalyzePolicy, row.Code);
                    return new TaskExecutionResult { Success = true, Message = "策略为跳过/忽略，只落档" };
                }

                var fileName = row.FileName;
                var sourcePath = string.IsNullOrWhiteSpace(payload.SourcePath) ? row.StoragePath : payload.SourcePath;

                // ① 读源字节
                byte[]? content;
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
                    _logger.LogError(ex, "[原始资料入库] 源读取失败: {Code}", row.Code);
                    return new TaskExecutionResult { Success = false, Message = "源文件读取失败", Retryable = true };
                }

                if (content == null || content.Length == 0)
                {
                    await FailBothAsync(db, row, "源文件内容为空");
                    return new TaskExecutionResult { Success = false, Message = "源文件内容为空", Retryable = false };
                }

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

                _logger.LogInformation("[原始资料入库] {Code} PDF={Pdf} MD={Md}", row.Code, pdfOk, mdOk);

                // ⛔ 2026-10-06 起**不再链式入队 analyze**：批次双队列方案已由「文件级编排器」
                //   （EnterpriseOriginalFileExecutor）取代 —— 转换段跑完由编排器在同一任务内
                //   串行接分析段，不存在跨队列补建/抢跑问题（旧逻辑见 git 历史）。

                // 任一链失败都把任务标记为可重试，便于前端「重试失败项」
                return new TaskExecutionResult
                {
                    Success = pdfOk && mdOk,
                    Message = pdfOk && mdOk ? "入库完成" : $"入库部分失败（PDF={(pdfOk ? "OK" : "FAIL")} / MD={(mdOk ? "OK" : "FAIL")}）",
                    Retryable = !(pdfOk && mdOk),
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[原始资料入库] 任务异常: {TaskCode}", task.Code);
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
        private async Task<(string FileName, byte[]? Content)> NormalizeAsync(
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
        // ③ 预览链
        // ========================================================

        private async Task<bool> RunPdfChainAsync(
            IDbOrm db, IObjectStorage storage, EnterpriseOriginalFile row,
            string fileName, string sourcePath, byte[] content)
        {
            var core = _serviceProvider.GetRequiredService<IFileConvertCore>();

            await SetPdfStatusAsync(db, row, EnterpriseOriginalService.ConvertStatus.Converting, null);

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
                await SetPdfStatusAsync(db, row, EnterpriseOriginalService.ConvertStatus.Completed, r.Message);
                return true;
            }
            catch (Exception ex)
            {
                await SetPdfStatusAsync(db, row, EnterpriseOriginalService.ConvertStatus.Failed, $"产物上传失败：{ex.Message}");
                return false;
            }
        }

        // ========================================================
        // ④ 提取链
        // ========================================================

        private async Task<bool> RunMarkdownChainAsync(
            IDbOrm db, IObjectStorage storage, EnterpriseOriginalFile row,
            string fileName, string sourcePath, byte[] content)
        {
            var core = _serviceProvider.GetRequiredService<IFileConvertCore>();

            await SetMdStatusAsync(db, row, EnterpriseOriginalService.ConvertStatus.Converting, null);

            var r = await core.ConvertToMarkdownAtAsync(fileName, sourcePath, content);
            if (!r.Success || r.Content == null || string.IsNullOrEmpty(r.TargetPath))
            {
                // ★ T1（2026-10-07）：图片 / PDF 走 anydoc 出不了 Markdown（unsupported）时，
                //   回退到<b>视觉模型 OCR</b>（<c>IOcrProvider.ToMarkdownAsync</c>，读同一行 <c>ai_vision_config</c>）
                //   产出 Markdown。得到 Markdown 后，分析段（AnalyzeExecutor）的 L1/L2 文本管道
                //   会像普通文本文件一样自然产出 分类/作用/标签 ⇒ 图片/PDF 也能进画像。
                //   ⛔ 不伪造内容：OCR 也失败（未接入/模型不可用/超大图）则落到下方 failed 分支，
                //   如实报能力边界，⛔ 绝不写占位文本（占位流进 LLM 会零报错地出错误结果）。
                if (UploadFilePolicy.IsImageOrPdf(row.FileType)
                    && _serviceProvider.GetService<IOcrProvider>() is { IsAvailable: true } ocr)
                {
                    var md = await ocr.ToMarkdownAsync(fileName, content);
                    if (md.Success && md.Content != null && md.Content.Length > 0)
                    {
                        var targetPath = PathBuilder.Product(sourcePath, PathBuilder.MarkdownSegment, ".md");
                        if (string.IsNullOrEmpty(targetPath))
                        {
                            await SetMdStatusAsync(db, row, EnterpriseOriginalService.ConvertStatus.Failed,
                                "源文件缺少存储路径，无法派生 Markdown 产物路径");
                            return false;
                        }
                        try
                        {
                            await storage.UploadAsync(targetPath.TrimStart('/'),
                                new MemoryStream(md.Content), md.Content.Length, "text/markdown");
                            row.MarkdownPath = targetPath;
                            row.ConvertDate = DateTime.Now;
                            await SetMdStatusAsync(db, row,
                                EnterpriseOriginalService.ConvertStatus.Completed,
                                "Markdown 由视觉模型 OCR 提取（ai_vision_config）");
                            _logger.LogInformation("[原始资料入库] 视觉 OCR 回退成功: {Code} → {Path}",
                                row.Code, targetPath);
                            return true;
                        }
                        catch (Exception ex)
                        {
                            await SetMdStatusAsync(db, row,
                                EnterpriseOriginalService.ConvertStatus.Failed,
                                $"视觉 OCR 产物上传失败：{ex.Message}");
                            return false;
                        }
                    }
                    // OCR 不可用 / 失败 ⇒ 落到下方失败分支，如实报能力边界（不静默）
                    _logger.LogInformation("[原始资料入库] 视觉 OCR 未产出 Markdown（{Msg}），按能力边界处理: {Code}",
                        md.Message, row.Code);
                }

                // ⛔ unsupported 是**能力边界**不是故障：如实报出来让用户走「人工填写」，
                //   ⛔ 绝不伪造占位内容 —— 占位文本流到 LLM 会生成一份「格式正确的错误结果」且零报错
                await SetMdStatusAsync(db, row, r.Status, r.Message);
                return false;
            }

            try
            {
                await storage.UploadAsync(r.TargetPath!.TrimStart('/'),
                    new MemoryStream(r.Content), r.Content.Length, r.ContentType ?? "text/markdown");

                row.MarkdownPath = r.TargetPath;
                row.ConvertDate = DateTime.Now;
                await SetMdStatusAsync(db, row, EnterpriseOriginalService.ConvertStatus.Completed, r.Message);
                return true;
            }
            catch (Exception ex)
            {
                await SetMdStatusAsync(db, row, EnterpriseOriginalService.ConvertStatus.Failed, $"产物上传失败：{ex.Message}");
                return false;
            }
        }

        // ========================================================
        // 状态写回（⚠️ 必须列级写，禁止 UpdateAsync(entity) 全列回写）
        // ========================================================

        /// <summary>
        /// 列级写回的必要性：ingest 与 analyze 是<b>两个独立队列任务、可并发</b>。
        /// 若用 <c>UpdateAsync(entity)</c> 全列写回，后完成方会把先完成方刚写的字段覆盖回旧快照
        /// （<c>OfficeConvertService</c> 类注释记录的 2026-09-26 事故就是它，零报错）。
        /// </summary>
        private static async Task SetPdfStatusAsync(IDbOrm db, EnterpriseOriginalFile row, string status, string? message)
        {
            row.ConvertStatus = status;
            row.ConvertMessage = message;
            if (status == EnterpriseOriginalService.ConvertStatus.Completed) row.ConvertDate = DateTime.Now;
            row.UpdateTime = DateTime.Now;
            await db.UpdateAsync(row,
                nameof(EnterpriseOriginalFile.PreviewPdfPath),
                nameof(EnterpriseOriginalFile.ConvertStatus),
                nameof(EnterpriseOriginalFile.ConvertMessage),
                nameof(EnterpriseOriginalFile.ConvertDate),
                nameof(EnterpriseOriginalFile.UpdateTime));
        }

        private static async Task SetMdStatusAsync(IDbOrm db, EnterpriseOriginalFile row, string status, string? message)
        {
            row.MarkdownStatus = status;
            row.MarkdownMessage = message;
            row.UpdateTime = DateTime.Now;
            await db.UpdateAsync(row,
                nameof(EnterpriseOriginalFile.MarkdownPath),
                nameof(EnterpriseOriginalFile.MarkdownStatus),
                nameof(EnterpriseOriginalFile.MarkdownMessage),
                nameof(EnterpriseOriginalFile.ConvertDate),
                nameof(EnterpriseOriginalFile.UpdateTime));
        }

        private static async Task FailBothAsync(IDbOrm db, EnterpriseOriginalFile row, string message)
        {
            row.ConvertStatus = EnterpriseOriginalService.ConvertStatus.Failed;
            row.ConvertMessage = message;
            row.MarkdownStatus = EnterpriseOriginalService.ConvertStatus.Failed;
            row.MarkdownMessage = message;
            row.AnalyzeStatus = EnterpriseOriginalService.AnalyzeStatus.Failed;
            row.AnalyzeMessage = message;
            row.UpdateTime = DateTime.Now;
            await db.UpdateAsync(row,
                nameof(EnterpriseOriginalFile.ConvertStatus),
                nameof(EnterpriseOriginalFile.ConvertMessage),
                nameof(EnterpriseOriginalFile.MarkdownStatus),
                nameof(EnterpriseOriginalFile.MarkdownMessage),
                nameof(EnterpriseOriginalFile.AnalyzeStatus),
                nameof(EnterpriseOriginalFile.AnalyzeMessage),
                nameof(EnterpriseOriginalFile.UpdateTime));
        }

        /// <summary>人工预设 skip/ignore ⇒ 只落档，标记 analyzed + skipped（不是 failed）</summary>
        private static async Task MarkSkipped(IDbOrm db, EnterpriseOriginalFile row, string policy)
        {
            row.AnalyzeStatus = EnterpriseOriginalService.AnalyzeStatus.Skipped;
            row.AnalyzeMessage = $"人工设定策略为 {policy}，只落档不做语义分析";
            row.ConvertStatus = EnterpriseOriginalService.ConvertStatus.None;
            row.MarkdownStatus = EnterpriseOriginalService.ConvertStatus.None;
            row.UpdateTime = DateTime.Now;
            await db.UpdateAsync(row,
                nameof(EnterpriseOriginalFile.AnalyzeStatus),
                nameof(EnterpriseOriginalFile.AnalyzeMessage),
                nameof(EnterpriseOriginalFile.ConvertStatus),
                nameof(EnterpriseOriginalFile.MarkdownStatus),
                nameof(EnterpriseOriginalFile.UpdateTime));
        }

        public Task OnTaskStateChangedAsync(YzhQueueTask task, string newStatus, string message)
        {
            _logger.LogInformation("[原始资料入库] 任务状态变更: {TaskCode} → {Status} ({Message})",
                task.Code, newStatus, message);
            return Task.CompletedTask;
        }
    }

    /// <summary>ingest 队列载荷</summary>
    public class EnterpriseOriginalIngestPayload
    {
        public string Code { get; set; } = "";
        public string EnterpriseCode { get; set; } = "";
        public string StageCode { get; set; } = "";
        public string FileName { get; set; } = "";
        public string SourcePath { get; set; } = "";
        public string FileType { get; set; } = "";
        public string AnalyzePolicy { get; set; } = "analyze";
        /// <summary>上传批次 Code；空 = 单文件触发（策略改单 / 回滚）</summary>
        public string BatchCode { get; set; } = "";
    }
}
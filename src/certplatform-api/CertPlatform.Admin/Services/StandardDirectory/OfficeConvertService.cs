
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Interfaces;
using CertPlatform.Shared.DocExtraction;
using CertPlatform.Shared.Storage;
using CertPlatform.Admin.Entities.Dir;

namespace CertPlatform.Admin.Services.StandardDirectory;

/// <summary>
/// 文件转换服务（V4：三链 —— 预览 / 提取 / ★ 归一）
///
/// <para><b>预览链</b>：Office → PDF（LibreOffice 容器）→ 产物列 <c>PreviewPdfPath</c>，
/// 状态列 <c>ConvertStatus</c>/<c>ConvertMessage</c>。PDF / 图片原样透传（产物路径 = 源路径）。</para>
///
/// <para><b>提取链</b>：任意格式 → Markdown（anydoc 容器）→ 产物列 <c>MarkdownPath</c>，
/// 状态列 <c>MarkdownStatus</c>/<c>MarkdownMessage</c>。</para>
///
/// <para><b>★ 归一链（2026-10-03，S-1）</b>：<c>.doc → .docx</c> / <c>.xls → .xlsx</c> /
/// <c>.ppt → .pptx</c>（LibreOffice 容器）→ 产物列 <c>EditableStoragePath</c>（<c>editable/</c> 段），
/// 状态列 <c>EditableStatus</c>/<c>EditableMessage</c>。
/// <para>存在的唯一理由：<b>NPOI 2.7.2 没有 <c>NPOI.HWPF</c></b> ⇒ <c>.doc</c> 连读都读不了，
/// 而本库 91.5% 的文件是旧二进制格式 ⇒ 不归一，填写引擎就没有输入。</para></para>
///
/// <para><b>产物路径</b>：统一由 <see cref="CodeGeneratorService.BuildProductPath"/> 从**源路径派生**
/// （<c>.../pdf/{原文件名}.pdf</c>、<c>.../markdown/{原文件名}.md</c>），不再重新拼装编码段。</para>
///
/// <para><b>OCR</b>：anydoc 判定「需要 OCR」（退出码 3）时交给 <see cref="IOcrProvider"/>。
/// 当前默认实现不具备能力 → 状态置 <c>unsupported</c> 并写入明确原因，
/// <b>上传流程照常完成</b>，用户可手工定义字段与表格后人工填写。</para>
///
/// <para><b>中间产物</b>（<c>doc2docx</c>/<c>xls2xlsx</c> → <c>ConvertedStoragePath</c>）：
/// 保留仅为**排空存量队列任务**；新的入队点不再产生该类型（决策 D-5）。</para>
///
/// <para><b>★ 转换内核已抽出（36 号 T1.1，2026-10-03）</b>：本类自 2026-10-03 起<b>不再自己调
/// <see cref="DocumentConvertClient"/></b>，两条链（PDF / Markdown）的<b>转换能力</b>全部委托给
/// <c>CertPlatform.Shared.DocExtraction.IFileConvertCore</c>（实现在
/// <c>CertPlatform.Shared/DocExtraction/FileConvertCore.cs</c>）。</para>
/// <para>抽核心理由：企业原始资料（36 号）需要<b>第二条</b>文件转换链（表和列都不同），
/// 不抽就等于把「PDF/图片透传 + anydoc 转 md + OCR 兜底 + 产物路径派生」抄第二遍 ——
/// 而状态机抄第二遍的代价本仓已付过一次：下面记录的 2026-09-26 并发写回事故。</para>
/// <para><b>本类保留的职责（不可下沉）</b>：① 查 DB 行 ② <b>按链写列</b>（列白名单不同）
/// ③ 上传产物 ④ 与既有队列的衔接。<b>转换能力一律不重复实现。</b></para>
///
/// <para><b>★ 并发写入约束（务必先读再改）</b>：三条链（PDF / Markdown / 归一）是**独立队列任务、
/// 并发执行**。队列框架的资源锁**不参与调度**（<c>QueueManager.GetNextPendingTaskAsync</c> 只按
/// <c>Status='pending'</c> 取任务，不看锁），因此同一文件的三个任务
/// **必然可能同时在跑**。各自在开头 <c>GetOneIgnoreValidAsync</c> 拿到一份内存快照 ——
/// 若用 <c>UpdateAsync(实体)</c> 写**所有列**，后完成的一方会把先完成方刚写的字段
/// **覆盖回自己快照里的旧值**。
/// <para>实测事故（2026-09-26）：Markdown 链先完成（<c>MarkdownPath=.../markdown/x.md</c>、
/// <c>MarkdownStatus=completed</c>），PDF 链后完成 → 全列写回 → <c>MarkdownPath</c> 变 NULL、
/// <c>MarkdownStatus</c> 退回 <c>pending</c>，**零报错、日志还打印了成功**。</para>
/// <para>⇒ 本文件**所有**写入必须走 <see cref="SavePdfChainAsync"/> /
/// <see cref="SaveMarkdownChainAsync"/> / <see cref="SaveEditableChainAsync"/> /
/// <see cref="SaveLegacyChainAsync"/>，
/// **禁止**直接调用 <c>_db.UpdateAsync(file)</c>。</para>
/// </summary>
public class OfficeConvertService
{
    private readonly IDbOrm _db;
    private readonly IObjectStorage _storage;
    /// <summary>★ 转换内核（36 号 T1.1）：PDF / Markdown 的转换能力<b>不在本类实现</b></summary>
    private readonly IFileConvertCore _core;
    private readonly ILogger<OfficeConvertService> _logger;

    public OfficeConvertService(
        IDbOrm db,
        IObjectStorage storage,
        IFileConvertCore core,
        ILogger<OfficeConvertService> logger)
    {
        _db = db;
        _storage = storage;
        _core = core;
        _logger = logger;
    }

    /// <summary>
    /// 执行文件转换（队列任务入口，按 ConvertType 分流）
    /// </summary>
    public async Task<bool> ConvertAsync(FileConvertPayload payload)
    {
        _logger.LogInformation("开始转换: {FileCode} ({ConvertType})", payload.Code, payload.ConvertType);

        // 1. 查找文件记录
        //    ★ 必须用 GetOneIgnoreValidAsync，**不能用 GetOneAsync**：
        //      转换期间文件被刻意置 IsValid = 0（列表隐藏，见 RetryFailedConversionsAsync），
        //      而 GetOneAsync 会自动追加 `IsValid = 1` → **执行器找不到自己的文件**，
        //      任务恒失败（日志只有一句「文件记录不存在」），IsValid 也永远回不到 1
        //      → 文件在目录里永久消失且无法再转换。实测复现于 2026-09-26。
        //      （同 REFERENCE §二十 ⑱ 的 includeDisabled 陷阱，此处是 GetOne 版本）
        var file = (await _db.GetOneIgnoreValidAsync<StandardDirectoryFile>(
            x => x.Code == payload.Code)).Data;
        if (file == null)
        {
            _logger.LogWarning("文件记录不存在: {FileCode}", payload.Code);
            return false;
        }

        // 2. 按转换类型分流
        return payload.ConvertType switch
        {
            "doc2pdf" or "xls2pdf" or "office2pdf" or "docx2pdf" or "xlsx2pdf" or "ppt2pdf" or "pptx2pdf"
                => await ConvertToPdfAsync(file, payload),
            "anydoc2md" or "doc2md" or "docx2md" or "xls2md" or "xlsx2md" or "pdf2md" or "office2md"
                => await ConvertToMarkdownAsync(file, payload),
            // ★ 归一链（S-1，2026-10-03）：旧二进制格式 → OOXML，产物走 editable/ 段。
            //   ⚠️ 刻意用新名字而不复用下面的 doc2docx/xls2xlsx —— 那两个是【遗留中间产物链】的
            //      存量队列排空入口（写 ConvertedStoragePath），语义与写入列都不同。
            "office2editable" or "doc2editable" or "xls2editable" or "ppt2editable"
                => await ConvertToEditableAsync(file, payload),
            // 仅用于排空存量队列任务（新入队点不再产生）
            "doc2docx" or "xls2xlsx"
                => await ConvertToOfficeIntermediateAsync(file, payload),
            // 空/未知 → 双产物（PDF 预览 + Markdown 提取），任一失败不影响另一个
            _ => await ConvertAutoAsync(file, payload)
        };
    }

    /// <summary>
    /// 自动双产物转换：PDF（预览）+ Markdown（提取），任一失败不影响另一个
    /// <para>⚠️ 同任务内**串行**执行，因此两次写入不会互相覆盖；跨任务并发仍受
    /// <see cref="SavePdfChainAsync"/>/<see cref="SaveMarkdownChainAsync"/> 的列隔离保护。</para>
    /// </summary>
    private async Task<bool> ConvertAutoAsync(StandardDirectoryFile file, FileConvertPayload payload)
    {
        var pdfOk = await ConvertToPdfAsync(file, payload);
        var mdOk = await ConvertToMarkdownAsync(file, payload);
        return pdfOk && mdOk;
    }

    // ========================================================
    // 分链写入（★ 并发安全的核心：只写本链拥有的列）
    // ========================================================

    /// <summary>预览链（PDF）拥有的列</summary>
    private static readonly string[] PdfChainColumns =
    {
        nameof(StandardDirectoryFile.PreviewPdfPath),
        nameof(StandardDirectoryFile.ConvertStatus),
        nameof(StandardDirectoryFile.ConvertMessage),
        nameof(StandardDirectoryFile.ConvertDate),
        nameof(StandardDirectoryFile.IsValid),
        nameof(StandardDirectoryFile.UpdateTime)
    };

    /// <summary>提取链（Markdown）拥有的列</summary>
    private static readonly string[] MarkdownChainColumns =
    {
        nameof(StandardDirectoryFile.MarkdownPath),
        nameof(StandardDirectoryFile.MarkdownStatus),
        nameof(StandardDirectoryFile.MarkdownMessage),
        nameof(StandardDirectoryFile.MarkdownDate),
        nameof(StandardDirectoryFile.UpdateTime)
    };

    /// <summary>遗留中间产物链拥有的列（仅供排空存量任务）</summary>
    private static readonly string[] LegacyChainColumns =
    {
        nameof(StandardDirectoryFile.ConvertedStoragePath),
        nameof(StandardDirectoryFile.ConvertStatus),
        nameof(StandardDirectoryFile.ConvertMessage),
        nameof(StandardDirectoryFile.ConvertDate),
        nameof(StandardDirectoryFile.IsValid),
        nameof(StandardDirectoryFile.UpdateTime)
    };

    /// <summary>
    /// ★ 归一链（<c>editable</c>）拥有的列（2026-10-03，S-1）。
    ///
    /// <para>⚠️ <b>刻意不含 <see cref="StandardDirectoryFile.IsValid"/></b>：该列属于<b>预览链</b>
    /// （见 <see cref="MarkPdfCompletedAsync"/> 注释 —— 两条链都写它会出现「提取成功把 IsValid 置 1、
    /// 而 PDF 其实还在转」的假就绪）。归一不是「文件可用」的标志，PDF 才是。</para>
    /// </summary>
    private static readonly string[] EditableChainColumns =
    {
        nameof(StandardDirectoryFile.EditableStoragePath),
        nameof(StandardDirectoryFile.EditableStatus),
        nameof(StandardDirectoryFile.EditableMessage),
        nameof(StandardDirectoryFile.EditableDate),
        nameof(StandardDirectoryFile.UpdateTime)
    };

    /// <summary>
    /// 预览链写回（**只写 PDF 链的列**，绝不触碰 Markdown 链字段）。
    /// <para>详见类注释「并发写入约束」—— 用 <c>UpdateAsync(file)</c> 全列写回会造成静默数据丢失。</para>
    /// <para>⚠️ 本链新增字段时必须同步加进 <see cref="PdfChainColumns"/>，否则**写不进去且不报错**。</para>
    /// </summary>
    private Task SavePdfChainAsync(StandardDirectoryFile file)
    {
        file.UpdateTime = DateTime.Now;
        return _db.UpdateAsync(file, PdfChainColumns);
    }

    /// <summary>
    /// 提取链写回（**只写 Markdown 链的列**，绝不触碰 PDF 链字段）。
    /// <para>⚠️ 本链新增字段时必须同步加进 <see cref="MarkdownChainColumns"/>。</para>
    /// </summary>
    private Task SaveMarkdownChainAsync(StandardDirectoryFile file)
    {
        file.UpdateTime = DateTime.Now;
        return _db.UpdateAsync(file, MarkdownChainColumns);
    }

    /// <summary>
    /// 遗留中间产物链写回。
    /// <para>⚠️ 该链的 <c>ConvertStatus</c> 与预览链**共用同一列**，理论上仍可能与 PDF 任务竞争；
    /// 但它只为排空存量队列存在，且存量队列里同一文件不会再同时排 PDF 任务，故可接受。</para>
    /// </summary>
    private Task SaveLegacyChainAsync(StandardDirectoryFile file)
    {
        file.UpdateTime = DateTime.Now;
        return _db.UpdateAsync(file, LegacyChainColumns);
    }

    /// <summary>
    /// ★ 归一链写回（**只写 <c>Editable*</c> 四列**，绝不触碰 PDF / Markdown 链字段）。
    /// <para>⚠️ 本链新增字段时必须同步加进 <see cref="EditableChainColumns"/>，否则**写不进去且不报错**。</para>
    /// </summary>
    private Task SaveEditableChainAsync(StandardDirectoryFile file)
    {
        file.UpdateTime = DateTime.Now;
        return _db.UpdateAsync(file, EditableChainColumns);
    }

    // ========================================================
    // 预览链：→ PDF
    // ========================================================

    /// <summary>
    /// 预览链：→ PDF，产物上传 MinIO，回写 <c>PreviewPdfPath</c> + <c>ConvertStatus</c>
    /// <para>★ 转换能力委托 <see cref="IFileConvertCore"/>（含 PDF / 图片<b>原样透传</b>判定）。
    /// 本方法只负责「上传产物 + 按链写列」。</para>
    /// </summary>
    private async Task<bool> ConvertToPdfAsync(StandardDirectoryFile file, FileConvertPayload payload)
    {
        file.ConvertStatus = "converting";
        file.ConvertMessage = null;
        await SavePdfChainAsync(file);

        try
        {
            var (ok, content, message) = await DownloadSourceAsync(file, payload);
            if (!ok || content == null)
                return await FailPdfAsync(file, message);

            var core = await _core.ConvertToPdfAsync(file.FileName, file.StoragePath, content);
            if (!core.Success)
                return await FailPdfAsync(file, core.Message);

            if (!core.Passthrough && core.Content != null)
            {
                using var targetStream = new MemoryStream(core.Content);
                await _storage.UploadAsync(
                    core.TargetPath!.TrimStart('/'), targetStream, core.Content.Length,
                    core.ContentType ?? "application/pdf");
            }

            file.PreviewPdfPath = core.TargetPath;
            await MarkPdfCompletedAsync(file);
            _logger.LogInformation("PDF 转换完成: {FileCode} → {Path}{Passthrough}",
                file.Code, file.PreviewPdfPath, core.Passthrough ? "（透传）" : "");
            return true;
        }
        catch (Exception ex)
        {
            return await FailPdfAsync(file, $"转换异常：{ex.Message}", log: true, ex: ex, fileCode: file.Code);
        }
    }

    private async Task<bool> FailPdfAsync(
        StandardDirectoryFile file, string? message, bool log = false, Exception? ex = null, string? fileCode = null)
    {
        file.ConvertStatus = "failed";
        file.ConvertMessage = message;
        await SavePdfChainAsync(file);
        if (log && ex != null) _logger.LogError(ex, "PDF 转换异常: {FileCode}", fileCode);
        else _logger.LogWarning("PDF 转换失败: {FileCode}: {Msg}", file.Code, message);
        return false;
    }

    // ========================================================
    // 提取链：→ Markdown
    // ========================================================

    /// <summary>
    /// 提取链：→ Markdown，产物上传 MinIO，回写 <c>MarkdownPath</c> + <c>MarkdownStatus</c>
    /// <para>★ 转换能力委托 <see cref="IFileConvertCore"/>（含 anydoc 退出码 3 的 OCR 兜底与
    /// 「能力未接入 ⇒ unsupported 而非伪造内容」判定）。本方法只负责「上传产物 + 按链写列」。</para>
    /// </summary>
    private async Task<bool> ConvertToMarkdownAsync(StandardDirectoryFile file, FileConvertPayload payload)
    {
        file.MarkdownStatus = "converting";
        file.MarkdownMessage = null;
        await SaveMarkdownChainAsync(file);

        try
        {
            var (ok, content, message) = await DownloadSourceAsync(file, payload);
            if (!ok || content == null)
                return await FailMarkdownAsync(file, message);

            var core = await _core.ConvertToMarkdownAtAsync(file.FileName, file.StoragePath, content);
            if (!core.Success || core.Content == null || string.IsNullOrEmpty(core.TargetPath))
            {
                // ⛔ unsupported 是【能力边界】不是故障：上传流程照常完成，用户手工定义字段后人工填写
                file.MarkdownStatus = core.Status;
                file.MarkdownMessage = core.Message;
                await SaveMarkdownChainAsync(file);
                _logger.LogInformation("Markdown 未自动提取: {FileCode} ({Status}): {Msg}",
                    file.Code, core.Status, core.Message);
                return false;
            }

            using (var targetStream = new MemoryStream(core.Content))
                await _storage.UploadAsync(
                    core.TargetPath.TrimStart('/'), targetStream, core.Content.Length,
                    core.ContentType ?? "text/markdown");

            file.MarkdownPath = core.TargetPath;
            file.MarkdownStatus = "completed";
            file.MarkdownMessage = core.Message;
            file.MarkdownDate = DateTime.Now;
            await SaveMarkdownChainAsync(file);

            _logger.LogInformation("Markdown 转换完成: {FileCode} → {Path}{Msg}",
                file.Code, file.MarkdownPath, string.IsNullOrEmpty(core.Message) ? "" : $"（{core.Message}）");
            return true;
        }
        catch (Exception ex)
        {
            file.MarkdownStatus = "failed";
            file.MarkdownMessage = $"转换异常：{ex.Message}";
            await SaveMarkdownChainAsync(file);
            _logger.LogError(ex, "Markdown 转换异常: {FileCode}", file.Code);
            return false;
        }
    }

    private async Task<bool> FailMarkdownAsync(StandardDirectoryFile file, string? message)
    {
        file.MarkdownStatus = "failed";
        file.MarkdownMessage = message;
        await SaveMarkdownChainAsync(file);
        _logger.LogWarning("Markdown 转换失败: {FileCode}: {Msg}", file.Code, message);
        return false;
    }

    /// <summary>
    /// 标记 PDF 预览链完成：置状态 + **恢复可见性**
    ///
    /// <para>★ 为什么在这里置 <c>IsValid = 1</c>：<c>RetryFailedConversionsAsync</c> 入队时把文件置
    /// <c>IsValid = 0</c>（转换期间隐藏）。原实现由「中间产物链」完成时置回 1；中间产物链已停用，
    /// 若不在此处恢复，**重试过的文件会永久隐藏在目录树里**（零报错，最难查）。</para>
    ///
    /// <para>⚠️ <c>IsValid</c> 属于预览链的列：提取链**不写**它。若让提取链也写，
    /// 两条链并发时会出现「提取成功把 IsValid 置 1，而 PDF 其实还在转」的假就绪。</para>
    /// </summary>
    private async Task MarkPdfCompletedAsync(StandardDirectoryFile file)
    {
        file.ConvertStatus = "completed";
        file.ConvertMessage = null;
        file.ConvertDate = DateTime.Now;
        file.IsValid = 1;
        await SavePdfChainAsync(file);
    }

    /// <summary>落库 Markdown 产物（含状态与时间）</summary>
    private async Task<bool> SaveMarkdownAsync(StandardDirectoryFile file, byte[] content, string? viaTag)
    {
        var targetPath = CodeGeneratorService.BuildProductPath(
            file.StoragePath, CodeGeneratorService.ProductKindMarkdown, ".md");
        if (string.IsNullOrEmpty(targetPath))
        {
            file.MarkdownStatus = "failed";
            file.MarkdownMessage = "源文件缺少存储路径，无法派生产物路径";
            await SaveMarkdownChainAsync(file);
            return false;
        }

        using (var targetStream = new MemoryStream(content))
            await _storage.UploadAsync(targetPath.TrimStart('/'), targetStream, content.Length, "text/markdown");

        file.MarkdownPath = targetPath;
        file.MarkdownStatus = "completed";
        file.MarkdownMessage = viaTag == "ocr" ? "内容由 OCR 提取" : null;
        file.MarkdownDate = DateTime.Now;
        await SaveMarkdownChainAsync(file);

        _logger.LogInformation("Markdown 转换完成: {FileCode} → {Path}{Via}",
            file.Code, file.MarkdownPath, viaTag == null ? "" : $"（{viaTag}）");
        return true;
    }

    // ========================================================
    // 中间产物链（仅排空存量队列任务）
    // ========================================================

    /// <summary>
    /// 中间产物链：doc→docx / xls→xlsx，回写 <c>ConvertedStoragePath</c>。
    /// <para>⚠️ 新入队点已不再产生该类型（决策 D-5：停止写入新值、字段保留）；
    /// 本方法仅为排空历史队列任务而保留。</para>
    /// </summary>
    private async Task<bool> ConvertToOfficeIntermediateAsync(StandardDirectoryFile file, FileConvertPayload payload)
    {
        file.ConvertStatus = "converting";
        await SaveLegacyChainAsync(file);

        try
        {
            var (ok, content, message) = await DownloadSourceAsync(file, payload);
            if (!ok || content == null)
            {
                file.ConvertStatus = "failed";
                file.ConvertMessage = message;
                await SaveLegacyChainAsync(file);
                return false;
            }

            var ext = payload.ConvertType == "doc2docx" ? ".docx" : ".xlsx";

            // ★ 归一能力同样走内核（36 号 T1.1），legacy 链与 ingest 归一步调同一份实现
            var result = await _core.ConvertToFormatAsync(file.FileName, content, ext.TrimStart('.'));
            if (!result.Success || result.Content == null)
            {
                file.ConvertStatus = "failed";
                file.ConvertMessage = result.Message;
                await SaveLegacyChainAsync(file);
                return false;
            }

            // 遗留路径：仍写 converted 兄弟路径（存量任务语义）
            var targetPath = payload.TargetPath?.TrimStart('/')
                             ?? BuildLegacySiblingPath(file.StoragePath, ext).TrimStart('/');
            using (var targetStream = new MemoryStream(result.Content))
                await _storage.UploadAsync(targetPath, targetStream, result.Content.Length, result.ContentType ?? "application/octet-stream");

            file.ConvertedStoragePath = "/" + targetPath;
            file.ConvertStatus = "completed";
            file.ConvertDate = DateTime.Now;
            file.IsValid = 1;
            await SaveLegacyChainAsync(file);

            _logger.LogInformation("中间产物转换完成（遗留）: {FileCode} → {Path}", file.Code, file.ConvertedStoragePath);
            return true;
        }
        catch (Exception ex)
        {
            file.ConvertStatus = "failed";
            file.ConvertMessage = ex.Message;
            await SaveLegacyChainAsync(file);
            _logger.LogError(ex, "中间产物转换异常: {FileCode}", file.Code);
            return false;
        }
    }

    // ========================================================
    // ★ 归一链（S-1，2026-10-03）：旧二进制格式 → OOXML
    // ========================================================

    /// <summary>
    /// 该文件是否需要归一为 OOXML（<b>全项目唯一判据</b>）。
    ///
    /// <para>返回目标扩展名（<b>不含点</b>）：<c>.doc → "docx"</c> / <c>.xls → "xlsx"</c> /
    /// <c>.ppt → "pptx"</c>；返回 <c>null</c> = 该文件不需要归一。</para>
    ///
    /// <para><b>为什么必须有这条链</b>：填写引擎用 NPOI，而 NPOI 2.7.2 <b>没有 <c>NPOI.HWPF</c></b>
    /// ⇒ <c>.doc</c> <b>连读都读不了</b>。本库实测 668 份中 <c>.doc</c> 567 份、<c>.xls</c> 44 份
    /// （合计 <b>91.5%</b>）—— 不归一，填写引擎就没有输入，模板侧几乎全部不可用。</para>
    ///
    /// <para>⚠️ <b>入队点必须调用本方法</b>（<c>StandardDirectoryService.BuildConvertPayloads</c> /
    /// <c>BuildBackfillTasks</c>），不得另写一份扩展名判断 —— 两处规则漂移的后果是
    /// 「页面说需要归一、执行器说不需归一」（或反之），且两处都<b>零报错</b>。</para>
    /// </summary>
    public static string? EditableTargetFormat(string? fileName)
        => Path.GetExtension(fileName ?? "").ToLowerInvariant() switch
        {
            ".doc" => "docx",
            ".xls" => "xlsx",
            ".ppt" => "pptx",
            _ => null
        };

    /// <summary>
    /// ★ 归一链：<c>.doc → .docx</c> / <c>.xls → .xlsx</c> / <c>.ppt → .pptx</c>，
    /// 产物上传 MinIO，回写 <c>EditableStoragePath</c> + <c>EditableStatus</c>。
    ///
    /// <para>★ 转换能力委托 <see cref="IFileConvertCore.ConvertToFormatAsync"/>
    /// （走 LibreOffice 容器），本方法只负责「派生产物路径 + 上传 + 按链写列」——
    /// ⛔ 不重写转换能力（36 号 T1.1 铁律）。</para>
    ///
    /// <para><b>与遗留中间产物链（<c>doc2docx</c>）的三点差异</b>：</para>
    /// <list type="number">
    ///   <item>写 <c>EditableStoragePath</c>（新列）而非已停用的 <c>ConvertedStoragePath</c>（决策 D-5）。</item>
    ///   <item>产物走 <c>editable/</c> 段（<see cref="PathBuilder.Product"/> 派生）而非同目录兄弟路径 ——
    ///         兄弟路径会与既有业务文件撞名（同 stem 不同扩展名的文件本项目实测存在）。</item>
    ///   <item><b>不写 <c>IsValid</c></b>（那是预览链的列，见 <see cref="EditableChainColumns"/>）。</item>
    /// </list>
    ///
    /// <para><b>幂等</b>：产物路径由源路径派生，重复执行只覆盖同一个 key，不产生垃圾对象。</para>
    /// </summary>
    private async Task<bool> ConvertToEditableAsync(StandardDirectoryFile file, FileConvertPayload payload)
    {
        var targetFormat = EditableTargetFormat(file.FileName);
        if (targetFormat == null)
        {
            // 入队点已按同一判据过滤，走到这里说明判据漂移或存量队列里有脏任务。
            // ⚠️ 刻意【不改任何列】：EditableStatus 的 NULL 语义是「不需要归一」，
            //    在此写值会让该语义依赖「执行器有没有跑到」，不可靠。
            _logger.LogInformation("归一链跳过（该格式无需归一）: {FileCode} {FileName}", file.Code, file.FileName);
            return true;
        }

        file.EditableStatus = "converting";
        file.EditableMessage = null;
        await SaveEditableChainAsync(file);

        try
        {
            var (ok, content, message) = await DownloadSourceAsync(file, payload);
            if (!ok || content == null)
                return await FailEditableAsync(file, message);

            // 产物路径：与 pdf/ markdown/ 对称的 editable/ 段，保留完整原文件名（构造保证唯一）
            var targetPath = PathBuilder.Product(file.StoragePath, PathBuilder.EditableSegment, "." + targetFormat);
            if (string.IsNullOrEmpty(targetPath))
                return await FailEditableAsync(file, "源文件缺少存储路径，无法派生产物路径");

            var result = await _core.ConvertToFormatAsync(file.FileName, content, targetFormat);
            if (!result.Success || result.Content == null)
                return await FailEditableAsync(file, result.Message);

            using (var targetStream = new MemoryStream(result.Content))
                await _storage.UploadAsync(
                    targetPath.TrimStart('/'), targetStream, result.Content.Length,
                    result.ContentType ?? "application/octet-stream");

            file.EditableStoragePath = targetPath;
            file.EditableStatus = "completed";
            file.EditableMessage = result.Message;
            file.EditableDate = DateTime.Now;
            await SaveEditableChainAsync(file);

            _logger.LogInformation("归一完成: {FileCode} {FileName} → {Path}", file.Code, file.FileName, targetPath);
            return true;
        }
        catch (Exception ex)
        {
            return await FailEditableAsync(file, $"归一异常：{ex.Message}", ex);
        }
    }

    private async Task<bool> FailEditableAsync(StandardDirectoryFile file, string? message, Exception? ex = null)
    {
        file.EditableStatus = "failed";
        file.EditableMessage = message;
        await SaveEditableChainAsync(file);
        if (ex != null) _logger.LogError(ex, "归一异常: {FileCode}", file.Code);
        else _logger.LogWarning("归一失败: {FileCode}: {Msg}", file.Code, message);
        return false;
    }

    // ========================================================
    // 辅助
    // ========================================================

    /// <summary>下载源文件字节（优先 payload.SourcePath，其次 file.StoragePath）</summary>
    private async Task<(bool Ok, byte[]? Content, string Message)> DownloadSourceAsync(
        StandardDirectoryFile file, FileConvertPayload payload)
    {
        var rawPath = payload.SourcePath;
        if (string.IsNullOrWhiteSpace(rawPath)) rawPath = file.StoragePath;
        if (string.IsNullOrWhiteSpace(rawPath)) return (false, null, "源文件存储路径为空");

        try
        {
            var (stream, _) = await _storage.DownloadAsync(rawPath.TrimStart('/'));
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            if (ms.Length == 0) return (false, null, "源文件内容为空");
            return (true, ms.ToArray(), "");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "源文件读取失败: {FileCode} {Path}", file.Code, rawPath);
            return (false, null, $"源文件读取失败：{ex.Message}");
        }
    }

    /// <summary>遗留同目录兄弟路径（仅供排空存量 doc2docx/xls2xlsx 任务使用）</summary>
    private static string BuildLegacySiblingPath(string? storagePath, string ext)
    {
        if (string.IsNullOrEmpty(storagePath))
            return "converted/" + Guid.NewGuid().ToString("N") + ext;
        var dir = Path.GetDirectoryName(storagePath.TrimStart('/'))?.Replace('\\', '/') ?? "";
        var stem = Path.GetFileNameWithoutExtension(storagePath);
        return string.IsNullOrEmpty(dir) ? $"{stem}{ext}" : $"{dir}/{stem}{ext}";
    }
}

/// <summary>
/// 文件转换载荷（队列任务 payload）
/// </summary>
public class FileConvertPayload
{
    public string Code { get; set; } = "";
    public string FileName { get; set; } = "";
    public string SourcePath { get; set; } = "";
    public string TargetPath { get; set; } = "";

    /// <summary>
    /// 转换类型：
    /// <para><c>office2pdf</c>（预览链，含 doc2pdf/xls2pdf/docx2pdf/… 别名）</para>
    /// <para><c>anydoc2md</c>（提取链，含 doc2md/pdf2md/… 别名）</para>
    /// <para><c>office2editable</c>（★ 归一链，含 doc2editable/xls2editable/ppt2editable 别名；
    /// 产物写 <c>EditableStoragePath</c>，供 NPOI 填写引擎读）</para>
    /// <para><c>doc2docx</c>/<c>xls2xlsx</c>（遗留中间产物，仅排空存量任务）</para>
    /// <para>空 → 自动双产物（PDF + Markdown）</para>
    /// </summary>
    public string ConvertType { get; set; } = "";

    /// <summary>企业编码（G-2d：非空且≠虚拟企业 ⇒ 转换成功后自动追加 doc_extract 提取任务）</summary>
    public string EnterpriseCode { get; set; } = "";

    /// <summary>阶段编码（透传给 doc_extract payload）</summary>
    public string StageCode { get; set; } = "";

    /// <summary>
    /// 转换成功后是否自动追加 <c>doc_extract</c> 提取任务（10 号 图 3 分流，S2；D4 默认 true）。
    /// <para><c>false</c> = 只转换不提取，槽位状态留 <c>none</c>（原因写 ExtractMessage）。</para>
    /// </summary>
    public bool AutoExtract { get; set; } = true;
}

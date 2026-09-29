using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using CertPlatform.Auditor.Services.Ent;
using CertPlatform.Shared.Entities.Dir;
using YZH.Core.Stand.Models.Result;
using YZH.Core.Api.Controllers;

namespace CertPlatform.Auditor.Controllers;

/// <summary>
/// 企业资料管理控制器（专家端 <c>/resources</c>，05 分册 §二 目标端点）。
///
/// <para><b>契约</b>：统一 <see cref="ApiResponse"/> 信封（<c>success</c> 唯一判据；业务失败 HTTP 200）；
/// 字段 <b>PascalCase 逐字一致</b>（AGENTS.md ③）—— 写错大小写 = 前端渲染空行且零报错。</para>
///
/// <para><b>工作区守卫</b>：不在此处重复实现，由 <see cref="EnterpriseFileService"/> 的
/// <c>OwnershipErrorAsync</c> 对每个公开方法统一把关（一处收口，避免漏网）。</para>
///
/// <para><b>与本页前端的历史路由</b>：<c>replace</c> / <c>delete</c> / <c>restore</c> /
/// <c>trigger-extract</c> / <c>extraction-result</c> / <c>versions</c> / <c>history</c> 保留原路由名
/// （不改为 <c>files/{fileCode}/…</c> 风格），避免与在飞前端脱节；
/// 已删除：<c>pending-list</c> / <c>file-list</c> / <c>upload/suggest</c> / 旧 multipart <c>upload/confirm</c>。</para>
/// </summary>
[ApiController]
[Route("api/Auditor/[controller]")]
public class EnterpriseFileController : WebControllerBase
{
    private readonly EnterpriseFileService _service;
    public EnterpriseFileController(EnterpriseFileService service) => _service = service;

    // ========================================================
    // 一、左树 / 阶段汇总 / 标准卡片主数据
    // ========================================================

    /// <summary>左树：本工作区企业 → 该企业关联的认证阶段（每阶段带标准数）</summary>
    [HttpPost("stage-tree")]
    public async Task<IActionResult> StageTree()
        => Ok(ApiResponse<object?>.Ok(await _service.StageTreeAsync()));

    /// <summary>阶段汇总：该企业该阶段下每个关联标准的应上传/已就位/转换中/缺失</summary>
    [HttpPost("stage-overview")]
    public async Task<IActionResult> StageOverview([FromBody] EnterpriseFileRequest req)
        => Ok(ApiResponse<object?>.Ok(await _service.StageOverviewAsync(req.EnterpriseCode ?? "", req.StageCode ?? "")));

    /// <summary>标准卡片主数据：文件夹树 + 槽位全集（带就位状态）</summary>
    [HttpPost("standard-directory")]
    public async Task<IActionResult> StandardDirectory([FromBody] EnterpriseFileRequest req)
        => Ok(ApiResponse<object?>.Ok(await _service.StandardDirectoryAsync(
            req.EnterpriseCode ?? "", req.StageCode ?? "", req.StandardCode ?? "")));

    /// <summary>就位检查报告（纯读对账，无写副作用）</summary>
    [HttpPost("file-check")]
    public async Task<IActionResult> FileCheck([FromBody] EnterpriseFileRequest req)
        => Ok(ApiResponse<object?>.Ok(await _service.FileCheckAsync(req.EnterpriseCode ?? "", req.StageCode ?? "")));

    // ========================================================
    // 二、多标准分发预览（需求 3 核心，纯计算）
    // ========================================================

    /// <summary>分发预览：一次选择的文件按每个关联标准独立匹配（M0–M3）</summary>
    [HttpPost("upload/plan")]
    public async Task<IActionResult> PlanDispatch([FromBody] DispatchPlanRequest req)
    {
        var files = (req.Files ?? new List<DispatchPlanFile>())
            .Where(f => !string.IsNullOrWhiteSpace(f.FileName))
            .Select(f => new DispatchMatcher.IncomingFile
            {
                FileName = f.FileName!,
                RelativePath = f.RelativePath ?? f.FileName!,
                FileSize = f.FileSize
            })
            .ToList();

        return Ok(ApiResponse<object?>.Ok(await _service.PlanDispatchAsync(
            req.EnterpriseCode ?? "", req.StageCode ?? "", files)));
    }

    // ========================================================
    // 三、四段式上传（槽位模式 / 指派模式）
    // ========================================================

    /// <summary>Step1：建上传任务 + 预建/重置目标行，下发 TaskId 与建议存储路径</summary>
    [HttpPost("upload/init")]
    public async Task<IActionResult> UploadInit([FromBody] UploadInitRequest req)
    {
        var (err, data) = await _service.UploadInitAsync(
            req.EnterpriseCode ?? "", req.StageCode ?? "", req.StandardCode ?? "",
            req.Items ?? new List<UploadInitItem>());
        return err == null ? Ok(ApiResponse<object?>.Ok(data)) : Ok(ApiResponse<object?>.Fail(err));
    }

    /// <summary>Step2：逐文件传字节（按 DB 记录重算路径，前端不传路径）</summary>
    [HttpPost("upload/file")]
    [RequestSizeLimit(200_000_000)]
    public async Task<IActionResult> UploadFile([FromForm] UploadFileStepDto dto)
    {
        if (dto?.File == null || dto.File.Length == 0)
            return Ok(ApiResponse<object?>.Fail("请选择文件"));

        using var stream = dto.File.OpenReadStream();
        var (ok, err) = await _service.UploadFileStepAsync(
            dto.FileCode ?? "", dto.TaskId ?? "", stream, dto.File.Length, dto.File.FileName);
        return ok
            ? Ok(ApiResponse<object?>.Ok(data: new { FileCode = dto.FileCode, FileName = dto.File.FileName }))
            : Ok(ApiResponse<object?>.Fail(err!));
    }

    /// <summary>Step3：激活 + 入 file_convert 队列</summary>
    [HttpPost("upload/confirm")]
    public async Task<IActionResult> UploadConfirm([FromBody] UploadTaskRequest req)
    {
        var (err, data) = await _service.UploadConfirmAsync(req.TaskId ?? "", req.EnterpriseCode ?? "");
        return err == null ? Ok(ApiResponse<object?>.Ok(data)) : Ok(ApiResponse<object?>.Fail(err));
    }

    /// <summary>Step4：回滚（取消队列 + 删对象 + 撤草稿行）</summary>
    [HttpPost("upload/cancel")]
    public async Task<IActionResult> UploadCancel([FromBody] UploadTaskRequest req)
    {
        var (err, data) = await _service.UploadCancelAsync(req.TaskId ?? "", req.EnterpriseCode ?? "");
        return err == null ? Ok(ApiResponse<object?>.Ok(data)) : Ok(ApiResponse<object?>.Fail(err));
    }

    // ========================================================
    // 四、替换 / 移除 / 恢复（保留原路由名，见类注释）
    // ========================================================

    [HttpPost("replace")]
    [RequestSizeLimit(200_000_000)]
    public async Task<IActionResult> Replace(
        [FromForm] IFormFile? file,
        [FromForm] string? FileCode,
        [FromForm] string? EnterpriseCode,
        [FromForm] string? Reason,
        [FromForm] string? ExpectedModifyTime)
    {
        if (file == null || file.Length == 0)
            return Ok(ApiResponse<object?>.Fail("请选择文件"));
        using var stream = file.OpenReadStream();
        var r = await _service.ReplaceFileAsync(FileCode ?? "", EnterpriseCode ?? "", stream, file.Length, file.FileName,
            Reason, ParseModifyTime(ExpectedModifyTime));
        return r.Success ? Ok(ApiResponse<object?>.Ok()) : Ok(ApiResponse<object?>.Fail(r.Error));
    }

    /// <summary>槽位模式替代入口：<c>POST files/{fileCode}/replace</c>（新契约，与 <c>replace</c> 等价）</summary>
    [HttpPost("files/{fileCode}/replace")]
    [RequestSizeLimit(200_000_000)]
    public async Task<IActionResult> ReplaceByRoute(
        string fileCode,
        [FromForm] IFormFile? file,
        [FromForm] string? EnterpriseCode,
        [FromForm] string? Reason,
        [FromForm] string? ExpectedModifyTime)
    {
        if (file == null || file.Length == 0)
            return Ok(ApiResponse<object?>.Fail("请选择文件"));
        using var stream = file.OpenReadStream();
        var r = await _service.ReplaceFileAsync(fileCode, EnterpriseCode ?? "", stream, file.Length, file.FileName,
            Reason, ParseModifyTime(ExpectedModifyTime));
        return r.Success ? Ok(ApiResponse<object?>.Ok()) : Ok(ApiResponse<object?>.Fail(r.Error));
    }

    [HttpPost("delete")]
    public async Task<IActionResult> Delete([FromBody] EnterpriseFileDeleteRequest req)
    {
        var r = await _service.DeleteFileAsync(req.FileCode ?? "", req.EnterpriseCode ?? "", req.Reason, req.ExpectedModifyTime);
        return r.Success ? Ok(ApiResponse<object?>.Ok()) : Ok(ApiResponse<object?>.Fail(r.Error));
    }

    /// <summary>新契约：<c>POST files/{fileCode}/delete</c></summary>
    [HttpPost("files/{fileCode}/delete")]
    public async Task<IActionResult> DeleteByRoute(string fileCode, [FromBody] EnterpriseFileDeleteRequest req)
    {
        var r = await _service.DeleteFileAsync(fileCode, req.EnterpriseCode ?? "", req.Reason, req.ExpectedModifyTime);
        return r.Success ? Ok(ApiResponse<object?>.Ok()) : Ok(ApiResponse<object?>.Fail(r.Error));
    }

    [HttpPost("restore")]
    public async Task<IActionResult> Restore([FromBody] EnterpriseFileRestoreRequest req)
    {
        var r = await _service.RestoreFileAsync(req.FileCode ?? "", req.EnterpriseCode ?? "", req.VersionNumber ?? 0, req.Reason);
        return r.Success ? Ok(ApiResponse<object?>.Ok()) : Ok(ApiResponse<object?>.Fail(r.Error));
    }

    // ========================================================
    // 五、下载 / 预览
    // ========================================================

    /// <summary>下载（含预览产物、归档版本）：白名单 + 企业段归属双重校验</summary>
    [HttpGet("download")]
    public async Task<IActionResult> Download([FromQuery] string? storagePath)
    {
        var (err, stream, contentType, fileName) = await _service.DownloadAsync(storagePath ?? "");
        if (err != null || stream == null)
            return Ok(ApiResponse<object?>.Fail(err ?? "文件不存在"));
        return new FileStreamResult(stream, contentType ?? "application/octet-stream")
        {
            FileDownloadName = fileName ?? "download"
        };
    }

    /// <summary>预览 PDF 产物（无产物时回落源文件：PDF/图片透传场景）</summary>
    [HttpGet("file-preview/{fileCode}")]
    public async Task<IActionResult> FilePreview(string fileCode, [FromQuery] string enterpriseCode)
    {
        var path = await _service.GetPreviewPathAsync(fileCode, enterpriseCode ?? "", markdown: false);
        return await FileByPath(path);
    }

    /// <summary>预览 Markdown 产物（可选）</summary>
    [HttpGet("file-markdown/{fileCode}")]
    public async Task<IActionResult> FileMarkdown(string fileCode, [FromQuery] string enterpriseCode)
    {
        var path = await _service.GetPreviewPathAsync(fileCode, enterpriseCode ?? "", markdown: true);
        return await FileByPath(path);
    }

    // ========================================================
    // 六、转换队列
    // ========================================================

    /// <summary>该标准目录是否有运行中队列（前端 5s 轮询）</summary>
    [HttpPost("active-queue")]
    public async Task<IActionResult> ActiveQueue([FromBody] ActiveQueueRequest req)
        => Ok(ApiResponse<object?>.Ok(await _service.GetActiveQueueAsync(req.ConfigCode ?? "", req.EnterpriseCode ?? "")));

    /// <summary>取消队列</summary>
    [HttpPost("queue/cancel")]
    public async Task<IActionResult> CancelQueue([FromBody] CancelQueueRequest req)
    {
        var r = await _service.CancelQueueAsync(req.QueueCode ?? "", req.EnterpriseCode ?? "");
        return r.Success ? Ok(ApiResponse<object?>.Ok()) : Ok(ApiResponse<object?>.Fail(r.Error));
    }

    // ========================================================
    // 七、提取（真链保留，偏差 D10：不删端点、不返回假成功）
    // ========================================================

    [HttpPost("trigger-extract")]
    public async Task<IActionResult> TriggerExtract([FromBody] EnterpriseFileRequest req)
    {
        var r = await _service.TriggerExtractAsync(req.FileCode ?? "", req.EnterpriseCode ?? "");
        return r.Success ? Ok(ApiResponse<object?>.Ok()) : Ok(ApiResponse<object?>.Fail(r.Error));
    }

    [HttpGet("extraction-result/{fileCode}")]
    public async Task<IActionResult> GetExtractionResult(string fileCode, [FromQuery] string enterpriseCode)
        => Ok(ApiResponse<object?>.Ok(await _service.GetExtractionResultAsync(fileCode, enterpriseCode ?? "")));

    // ========================================================
    // 八、版本 / 历史
    // ========================================================

    [HttpGet("versions/{fileCode}")]
    public async Task<IActionResult> GetVersions(string fileCode, [FromQuery] string enterpriseCode)
        => Ok(ApiResponse<object?>.Ok(await _service.GetVersionsAsync(fileCode, enterpriseCode ?? "")));

    [HttpGet("history/{fileCode}")]
    public async Task<IActionResult> GetHistory(string fileCode, [FromQuery] string enterpriseCode)
        => Ok(ApiResponse<object?>.Ok(await _service.GetHistoryAsync(fileCode, enterpriseCode ?? "")));

    // ========================================================
    // 九、私有辅助
    // ========================================================

    /// <summary>把存储路径当文件回（预览用）；路径为空/非法 = 业务失败（HTTP 200 + success=false）</summary>
    private async Task<IActionResult> FileByPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return Ok(ApiResponse<object?>.Fail("预览产物不存在，请等待转换完成"));
        var (err, stream, contentType, fileName) = await _service.DownloadAsync(path);
        if (err != null || stream == null)
            return Ok(ApiResponse<object?>.Fail(err ?? "文件不存在"));
        return new FileStreamResult(stream, contentType ?? "application/octet-stream")
        {
            FileDownloadName = fileName ?? "preview"
        };
    }

    /// <summary>乐观锁时间戳（前端行 UpdateTime/CreateTime 原文回传）；空/不可解析 = 跳过校验</summary>
    private static DateTime? ParseModifyTime(string? raw)
        => DateTime.TryParse(raw, out var t) ? t : null;

    // ========================================================
    // 十、请求模型（DTO 字段 PascalCase，与 DB 列名逐字一致）
    // ========================================================

    public class EnterpriseFileRequest
    {
        public string? EnterpriseCode { get; set; }
        public string? StageCode { get; set; }
        public string? StandardCode { get; set; }
        public string? FileCode { get; set; }
    }

    public class EnterpriseFileDeleteRequest
    {
        public string? FileCode { get; set; }
        public string? EnterpriseCode { get; set; }
        public string? Reason { get; set; }
        public DateTime? ExpectedModifyTime { get; set; }
    }

    public class EnterpriseFileRestoreRequest
    {
        public string? FileCode { get; set; }
        public string? EnterpriseCode { get; set; }
        public int? VersionNumber { get; set; }
        public string? Reason { get; set; }
    }

    public class DispatchPlanFile
    {
        public string? FileName { get; set; }
        public string? RelativePath { get; set; }
        public long FileSize { get; set; }
    }

    public class DispatchPlanRequest
    {
        public string? EnterpriseCode { get; set; }
        public string? StageCode { get; set; }
        public List<DispatchPlanFile>? Files { get; set; }
    }

    public class UploadInitRequest
    {
        public string? EnterpriseCode { get; set; }
        public string? StageCode { get; set; }
        public string? StandardCode { get; set; }
        public List<UploadInitItem>? Items { get; set; }
    }

    public class UploadFileStepDto
    {
        public IFormFile? File { get; set; }
        public string? FileCode { get; set; }
        public string? TaskId { get; set; }
    }

    public class UploadTaskRequest
    {
        public string? TaskId { get; set; }
        public string? EnterpriseCode { get; set; }
    }

    public class ActiveQueueRequest
    {
        public string? ConfigCode { get; set; }
        public string? EnterpriseCode { get; set; }
        public string? StageCode { get; set; }
        public string? StandardCode { get; set; }
    }

    public class CancelQueueRequest
    {
        public string? QueueCode { get; set; }
        public string? EnterpriseCode { get; set; }
    }
}

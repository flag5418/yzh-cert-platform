using System.Collections.Generic;
using System.Threading.Tasks;
using CertPlatform.Auditor.Services.Ent;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Controllers;
using YZH.Core.Stand.Models.Result;

namespace CertPlatform.Auditor.Controllers
{
    /// <summary>
    /// 企业原始资料管理控制器（专家端 <c>/enterprise-original</c>，36 号 §6.1）。
    ///
    /// <para><b>契约</b>：统一 <see cref="ApiResponse"/> 信封（<c>success</c> 唯一判据；业务失败 HTTP 200）；
    /// 字段 <b>PascalCase 逐字一致</b>（AGENTS.md ③）。</para>
    ///
    /// <para><b>★ 端标记</b>：路由 <c>api/Auditor/[controller]</c>，与后台端 <c>api/Admin/*</c> 对称
    /// （2026-10-03 统一；此前后台端零端标记，见 24 号清单 §五 A2）。</para>
    ///
    /// <para><b>★ D9</b>：所有定位/删除/更新端点只收 <c>FileCode</c>（业务键），⛔ 不收 <c>Id</c>。</para>
    ///
    /// <para><b>⛔ 控制器只增不改名</b>：改名 → <c>ApiCode</c> 变 → 角色-接口关联**静默断裂</c>
    /// （AGENTS.md 编码强制约定 ②）。本控制器尚未做过 ApiSync，重关联在 P3 T3.1。</para>
    /// </summary>
    [ApiController]
    [Route("api/Auditor/[controller]")]
    public class EnterpriseOriginalController : WebControllerBase
    {
        private readonly EnterpriseOriginalService _service;
        public EnterpriseOriginalController(EnterpriseOriginalService service) => _service = service;

        // ========================================================
        // 一、左树 / 列表 / 状态条
        // ========================================================

        /// <summary>左树：本工作区企业 → 认证阶段（带原始资料计数）</summary>
        [HttpPost("stage-tree")]
        public async Task<IActionResult> StageTree()
            => Ok(ApiResponse<object?>.Ok(await _service.StageTreeAsync()));

        /// <summary>
        /// 该企业该阶段的文件列表。
        /// <para><b>★ 语义过滤</b>：<c>TagCodes</c> 按受控标签过滤（并集）—— 这是「分类过滤」的唯一入口，
        /// 目录（<c>RelFolderPath</c>）做不到，因为企业交上来时的物理目录 ≠ 业务分类。</para>
        /// <para><b>★ 可用性过滤</b>：<c>OnlyUsable=true</c> 只返回「转换 ∧ 分析」双条件都成功的行，
        /// 从根上排除「转成功但分析失败」的半成品（36 号 §六 铁律）。</para>
        /// </summary>
        [HttpPost("list")]
        public async Task<IActionResult> List([FromBody] ListRequest req)
            => Ok(ApiResponse<object?>.Ok(await _service.ListAsync(
                req.EnterpriseCode ?? "", req.StageCode ?? "",
                req.TagCodes, req.OnlyUsable ?? false, req.GroupByTag ?? false)));

        /// <summary>状态条：转换中 / 分析中 / 失败 / 可用性 计数 + 运行中队列</summary>
        [HttpPost("status-bar")]
        public async Task<IActionResult> StatusBar([FromBody] ScopeRequest req)
            => Ok(ApiResponse<object?>.Ok(await _service.StatusBarAsync(
                req.EnterpriseCode ?? "", req.StageCode ?? "")));

        /// <summary>★ 队列明细：排队位置 + 进度 + 每份文件的任务状态与失败原因</summary>
        [HttpPost("queue-detail")]
        public async Task<IActionResult> QueueDetail([FromBody] ScopeRequest req)
            => Ok(ApiResponse<object?>.Ok(await _service.QueueDetailAsync(
                req.EnterpriseCode ?? "", req.StageCode ?? "")));

        // ========================================================
        // 二、五段式上传
        // ========================================================

        /// <summary>Step0：预检（类型白名单 / 大小上限 / Sha256 幂等预估），纯计算不落库</summary>
        [HttpPost("upload/plan")]
        public async Task<IActionResult> UploadPlan([FromBody] UploadPlanRequest req)
        {
            var (files, err) = NormalizeFiles(req);
            if (err != null) return Ok(ApiResponse<object?>.Fail(err));
            return Ok(ApiResponse<object?>.Ok(await _service.PlanUploadAsync(
                req.EnterpriseCode ?? "", req.StageCode ?? "", files)));
        }

        /// <summary>Step1：建批次 + 按 D7 建/命中文件行，下发 TaskId 与建议存储路径</summary>
        [HttpPost("upload/init")]
        public async Task<IActionResult> UploadInit([FromBody] UploadPlanRequest req)
        {
            var (files, err) = NormalizeFiles(req);
            if (err != null) return Ok(ApiResponse<object?>.Fail(err));

            var (e, data) = await _service.UploadInitAsync(
                req.EnterpriseCode ?? "", req.StageCode ?? "", files);
            return e == null ? Ok(ApiResponse<object?>.Ok(data)) : Ok(ApiResponse<object?>.Fail(e));
        }

        /// <summary>Step2：逐文件传字节（按 DB 行重算路径，前端不传路径）</summary>
        [HttpPost("upload/file")]
        [RequestSizeLimit(200_000_000)]
        public async Task<IActionResult> UploadFile([FromForm] UploadFileStepDto dto)
        {
            if (dto?.File == null || dto.File.Length == 0)
                return Ok(ApiResponse<object?>.Fail("请选择文件"));

            using var stream = dto.File.OpenReadStream();
            var (ok, err, data) = await _service.UploadFileStepAsync(
                dto.FileCode ?? "", dto.TaskId ?? "", stream, dto.File.Length,
                dto.File.FileName, dto.Sha256 ?? "");
            return ok ? Ok(ApiResponse<object?>.Ok(data)) : Ok(ApiResponse<object?>.Fail(err!));
        }

        /// <summary>Step3：激活 + 入 enterprise_original_ingest 队列</summary>
        [HttpPost("upload/confirm")]
        public async Task<IActionResult> UploadConfirm([FromBody] UploadTaskRequest req)
        {
            var (err, data) = await _service.UploadConfirmAsync(
                req.TaskId ?? "", req.EnterpriseCode ?? "");
            return err == null ? Ok(ApiResponse<object?>.Ok(data)) : Ok(ApiResponse<object?>.Fail(err));
        }

        /// <summary>Step4：回滚（取消队列 + 删对象 + 撤本批次行）</summary>
        [HttpPost("upload/cancel")]
        public async Task<IActionResult> UploadCancel([FromBody] UploadTaskRequest req)
        {
            var (err, data) = await _service.UploadCancelAsync(
                req.TaskId ?? "", req.EnterpriseCode ?? "");
            return err == null ? Ok(ApiResponse<object?>.Ok(data)) : Ok(ApiResponse<object?>.Fail(err));
        }

        // ========================================================
        // 三、删除 / 版本 / 回滚
        // ========================================================

        [HttpPost("delete")]
        public async Task<IActionResult> Delete([FromBody] FileCodeRequest req)
        {
            var r = await _service.DeleteAsync(
                req.FileCode ?? "", req.EnterpriseCode ?? "", req.Reason);
            return r.Success ? Ok(ApiResponse<object?>.Ok()) : Ok(ApiResponse<object?>.Fail(r.Error));
        }

        [HttpGet("versions/{fileCode}")]
        public async Task<IActionResult> Versions(string fileCode, [FromQuery] string? enterpriseCode)
            => Ok(ApiResponse<object?>.Ok(await _service.GetVersionsAsync(fileCode, enterpriseCode ?? "")));

        [HttpPost("restore")]
        public async Task<IActionResult> Restore([FromBody] RestoreRequest req)
        {
            var r = await _service.RestoreVersionAsync(
                req.FileCode ?? "", req.EnterpriseCode ?? "", req.VersionNumber ?? 0, req.Reason);
            return r.Success ? Ok(ApiResponse<object?>.Ok(r.Data)) : Ok(ApiResponse<object?>.Fail(r.Error));
        }

        // ========================================================
        // 四、分析策略（36 号 §3.5 三层）
        // ========================================================

        [HttpPost("policy/set")]
        public async Task<IActionResult> SetPolicy([FromBody] PolicyRequest req)
        {
            var r = await _service.SetPolicyAsync(
                req.FileCode ?? "", req.EnterpriseCode ?? "",
                req.AnalyzePolicy ?? "", req.PolicyReason, req.Reanalyze ?? false);
            return r.Success ? Ok(ApiResponse<object?>.Ok(r.Data)) : Ok(ApiResponse<object?>.Fail(r.Error));
        }

        [HttpPost("policy/batch")]
        public async Task<IActionResult> BatchPolicy([FromBody] BatchPolicyRequest req)
        {
            var r = await _service.BatchSetPolicyAsync(
                req.FileCodes ?? new List<string>(), req.EnterpriseCode ?? "",
                req.AnalyzePolicy ?? "", req.PolicyReason, req.Reanalyze ?? false);
            return r.Success ? Ok(ApiResponse<object?>.Ok(r.Data)) : Ok(ApiResponse<object?>.Fail(r.Error));
        }

        // ========================================================
        // 五、画像读取与人工修正（D6）
        // ========================================================

        [HttpGet("profile/{fileCode}")]
        public async Task<IActionResult> Profile(string fileCode, [FromQuery] string? enterpriseCode)
            => Ok(ApiResponse<object?>.Ok(await _service.GetProfileAsync(fileCode, enterpriseCode ?? "")));

        [HttpPost("profile/correct")]
        public async Task<IActionResult> CorrectProfile([FromBody] CorrectProfileDto dto)
        {
            var r = await _service.CorrectProfileAsync(dto);
            return r.Success ? Ok(ApiResponse<object?>.Ok(r.Data)) : Ok(ApiResponse<object?>.Fail(r.Error));
        }

        // ========================================================
        // 六、下载 / 预览
        // ========================================================

        [HttpGet("download")]
        public async Task<IActionResult> Download(
            [FromQuery] string? storagePath, [FromQuery] string? enterpriseCode)
        {
            var (err, stream, contentType, fileName) = await _service.DownloadAsync(
                storagePath ?? "", enterpriseCode);
            if (err != null || stream == null)
                return Ok(ApiResponse<object?>.Fail(err ?? "文件不存在"));
            return new FileStreamResult(stream, contentType ?? "application/octet-stream")
            {
                FileDownloadName = fileName ?? "download"
            };
        }

        [HttpGet("file-preview/{fileCode}")]
        public async Task<IActionResult> FilePreview(string fileCode, [FromQuery] string? enterpriseCode)
            => await FileByPath(await _service.GetPreviewPathAsync(fileCode, enterpriseCode ?? "", markdown: false));

        [HttpGet("file-markdown/{fileCode}")]
        public async Task<IActionResult> FileMarkdown(string fileCode, [FromQuery] string? enterpriseCode)
            => await FileByPath(await _service.GetPreviewPathAsync(fileCode, enterpriseCode ?? "", markdown: true));

        private async Task<IActionResult> FileByPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return Ok(ApiResponse<object?>.Fail("预览产物不存在，请等待转换完成"));
            var (err, stream, contentType, fileName) = await _service.DownloadAsync(path, null);
            if (err != null || stream == null)
                return Ok(ApiResponse<object?>.Fail(err ?? "文件不存在"));
            return new FileStreamResult(stream, contentType ?? "application/octet-stream")
            {
                FileDownloadName = fileName ?? "preview"
            };
        }

        // ========================================================
        // 七、请求模型（DTO 字段 PascalCase，与 DB 列名逐字一致）
        // ========================================================

        /// <summary>把请求里的上传项摊平成服务层的 <see cref="PlanItemDto"/>；空文件名直接剔除</summary>
        private static (List<PlanItemDto> Files, string? Error) NormalizeFiles(UploadPlanRequest req)
        {
            if (req.Items == null || req.Items.Count == 0)
                return (new List<PlanItemDto>(), "请先选择要上传的文件");

            var files = new List<PlanItemDto>();
            foreach (var it in req.Items)
            {
                if (string.IsNullOrWhiteSpace(it.FileName)) continue;
                files.Add(new PlanItemDto
                {
                    FileName = it.FileName,
                    RelFolderPath = it.RelFolderPath,
                    FileSize = it.FileSize,
                    Sha256 = it.Sha256,
                });
            }
            if (files.Count == 0) return (new List<PlanItemDto>(), "请先选择要上传的文件");
            return (files, null);
        }

        public class ScopeRequest
        {
            public string? EnterpriseCode { get; set; }
            public string? StageCode { get; set; }
        }

        /// <summary>列表查询（含语义过滤 + 可用性过滤）</summary>
        public class ListRequest : ScopeRequest
        {
            /// <summary>★ 按受控标签过滤（并集）。值必须 ∈ <c>cert_tag_dict.TagCode</c></summary>
            public List<string>? TagCodes { get; set; }

            /// <summary>true = 只返回「转换 ∧ 分析」双条件都成功的行（填写期可用）</summary>
            public bool? OnlyUsable { get; set; }

            /// <summary>true = 额外返回按标签的分组聚合（语义分组视图）</summary>
            public bool? GroupByTag { get; set; }
        }

        public class FileCodeRequest
        {
            /// <summary>★ 文件业务键（⛔ 不用 Id）</summary>
            public string? FileCode { get; set; }
            public string? EnterpriseCode { get; set; }
            public string? Reason { get; set; }
        }

        public class UploadPlanFileDto
        {
            public string? FileName { get; set; }
            public string? RelFolderPath { get; set; }
            public long FileSize { get; set; }
            public string? Sha256 { get; set; }
        }

        public class UploadPlanRequest
        {
            public string? EnterpriseCode { get; set; }
            public string? StageCode { get; set; }
            public List<UploadPlanFileDto>? Items { get; set; }
        }

        public class UploadFileStepDto
        {
            public IFormFile? File { get; set; }
            public string? FileCode { get; set; }
            public string? TaskId { get; set; }
            /// <summary>可选；服务端收到字节后会自行计算并以那个为准（D7 判定的权威依据）</summary>
            public string? Sha256 { get; set; }
        }

        public class UploadTaskRequest
        {
            public string? TaskId { get; set; }
            public string? EnterpriseCode { get; set; }
        }

        public class RestoreRequest : FileCodeRequest
        {
            public int? VersionNumber { get; set; }
        }

        public class PolicyRequest : FileCodeRequest
        {
            /// <summary><c>analyze/skip/ignore</c></summary>
            public string? AnalyzePolicy { get; set; }
            public string? PolicyReason { get; set; }
            /// <summary>true = 保存后立即入队重算（策略立刻生效）</summary>
            public bool? Reanalyze { get; set; }
        }

        public class BatchPolicyRequest
        {
            public List<string>? FileCodes { get; set; }
            public string? EnterpriseCode { get; set; }
            public string? AnalyzePolicy { get; set; }
            public string? PolicyReason { get; set; }
            public bool? Reanalyze { get; set; }
        }
    }
}
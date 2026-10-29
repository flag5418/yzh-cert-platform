using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CertPlatform.Auditor.Services.Ent;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Controllers;
using YZH.Core.Stand.Models.Result;

namespace CertPlatform.Auditor.Controllers
{
    /// <summary>
    /// 企业文档控制器（专家端 <c>/resources</c>，36 号 §四）—— 上传模块重写版。
    ///
    /// <para><b>★★★ 与老 <see cref="EnterpriseOriginalController"/> 的契约差异</b>：
    /// <b>任何端点都不收 <c>StageCode</c></b>（阶段维度已废除），
    /// <b>上传端点也不收 <c>StandardCode</c></b>（标准只在提取阶段出现）。</para>
    ///
    /// <para><b>契约</b>：统一 <see cref="ApiResponse"/> 信封（<c>success</c> 唯一判据；业务失败 HTTP 200）；
    /// 字段 <b>PascalCase 逐字一致</b>（AGENTS.md ③）。</para>
    ///
    /// <para><b>★ 端标记</b>：路由 <c>api/Auditor/[controller]</c>，与后台端 <c>api/Admin/*</c> 对称。</para>
    ///
    /// <para><b>★ 定位一律用 <c>FileCode</c></b>（业务键），⛔ 不收 <c>Id</c>。</para>
    /// </summary>
    [ApiController]
    [Route("api/Auditor/[controller]")]
    public class EnterpriseDocController : WebControllerBase
    {
        private readonly EnterpriseDocService _service;
        private readonly DocumentExtractionService _extractions;

        public EnterpriseDocController(EnterpriseDocService service, DocumentExtractionService extractions)
        {
            _service = service;
            _extractions = extractions;
        }

        // ========================================================
        // 一、左树 / 列表 / 状态条
        // ========================================================

        /// <summary>左树：本工作区企业（<b>一层，无阶段</b>），每家企业带文件计数 + 计划标准</summary>
        [HttpPost("tree")]
        public async Task<IActionResult> Tree()
            => Ok(ApiResponse<object?>.Ok(await _service.EnterpriseTreeAsync()));

        /// <summary>
        /// 该企业的文档列表（无阶段）。
        /// <para><c>StandardCode</c> 非空 = 只返回该标准下有提取产物的文件（「按标准看覆盖」视图）。</para>
        /// </summary>
        [HttpPost("list")]
        public async Task<IActionResult> List([FromBody] ListRequest req)
            => Ok(ApiResponse<object?>.Ok(await _service.ListAsync(
                req.EnterpriseCode ?? "", req.StandardCode, req.Keyword, req.OnlyFailed ?? false)));

        /// <summary>状态条：转换 / 提取计数 + 该企业是否有运行中队列</summary>
        [HttpPost("status-bar")]
        public async Task<IActionResult> StatusBar([FromBody] EnterpriseRequest req)
            => Ok(ApiResponse<object?>.Ok(await _service.StatusBarAsync(req.EnterpriseCode ?? "")));

        /// <summary>★ 队列明细：排队位置 + 进度 + 每份文件的任务状态与失败原因</summary>
        [HttpPost("queue-detail")]
        public async Task<IActionResult> QueueDetail([FromBody] EnterpriseRequest req)
            => Ok(ApiResponse<object?>.Ok(await _service.QueueDetailAsync(req.EnterpriseCode ?? "")));

        /// <summary>★ 取已生成的 Markdown <b>文本</b>（页面内核对用）</summary>
        [HttpGet("markdown/{fileCode}")]
        public async Task<IActionResult> Markdown(string fileCode, [FromQuery] string? enterpriseCode)
            => Ok(ApiResponse<object?>.Ok(await _service.GetMarkdownAsync(fileCode, enterpriseCode ?? "")));

        // ========================================================
        // 二、五段式上传（无阶段、无标准）
        // ========================================================

        /// <summary>Step0：预检（类型白名单 / 大小上限 / Sha256 幂等预估），纯计算不落库</summary>
        [HttpPost("upload/plan")]
        public async Task<IActionResult> UploadPlan([FromBody] UploadPlanRequest req)
        {
            var (files, err) = NormalizeFiles(req);
            if (err != null) return Ok(ApiResponse<object?>.Fail(err));
            return Ok(ApiResponse<object?>.Ok(await _service.PlanUploadAsync(req.EnterpriseCode ?? "", files)));
        }

        /// <summary>Step1：建批次 + 建/命中文件行，下发 TaskId 与建议存储路径</summary>
        [HttpPost("upload/init")]
        public async Task<IActionResult> UploadInit([FromBody] UploadPlanRequest req)
        {
            var (files, err) = NormalizeFiles(req);
            if (err != null) return Ok(ApiResponse<object?>.Fail(err));

            var (e, data) = await _service.UploadInitAsync(req.EnterpriseCode ?? "", files);
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

        /// <summary>Step3：激活 + 入 <c>enterprise_doc_convert</c> 队列</summary>
        [HttpPost("upload/confirm")]
        public async Task<IActionResult> UploadConfirm([FromBody] UploadTaskRequest req)
        {
            var (err, data) = await _service.UploadConfirmAsync(req.TaskId ?? "", req.EnterpriseCode ?? "");
            return err == null ? Ok(ApiResponse<object?>.Ok(data)) : Ok(ApiResponse<object?>.Fail(err));
        }

        /// <summary>Step4：回滚（取消队列 + 删对象 + 撤本批次行）</summary>
        [HttpPost("upload/cancel")]
        public async Task<IActionResult> UploadCancel([FromBody] UploadTaskRequest req)
        {
            var (err, data) = await _service.UploadCancelAsync(req.TaskId ?? "", req.EnterpriseCode ?? "");
            return err == null ? Ok(ApiResponse<object?>.Ok(data)) : Ok(ApiResponse<object?>.Fail(err));
        }

        // ========================================================
        // 三、删除 / 版本 / 重新转换
        // ========================================================

        [HttpPost("delete")]
        public async Task<IActionResult> Delete([FromBody] FileCodeRequest req)
        {
            var r = await _service.DeleteAsync(req.FileCode ?? "", req.EnterpriseCode ?? "", req.Reason);
            return r.Success ? Ok(ApiResponse<object?>.Ok(r.Data)) : Ok(ApiResponse<object?>.Fail(r.Error));
        }

        /// <summary>
        /// ★ 批量删除 —— 页面上「删除整个文件夹」的落点。
        /// <para>⛔ 只收 <c>FileCodes</c>（业务键），不收文件夹路径：「哪些文件属于这个文件夹」
        /// 是前端按 <c>RelFolderPath</c> 算出的展示口径，后端不持有该概念。</para>
        /// </summary>
        [HttpPost("delete/batch")]
        public async Task<IActionResult> BatchDelete([FromBody] BatchDeleteRequest req)
        {
            var r = await _service.BatchDeleteAsync(
                req.FileCodes ?? new List<string>(), req.EnterpriseCode ?? "", req.Reason);
            return r.Success ? Ok(ApiResponse<object?>.Ok(r.Data)) : Ok(ApiResponse<object?>.Fail(r.Error));
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

        /// <summary>★ 重新转换（重跑 PDF + Markdown 两链），不动源文件、不改版本号</summary>
        [HttpPost("regenerate")]
        public async Task<IActionResult> Regenerate([FromBody] RegenerateRequest req)
        {
            var r = await _service.RegenerateAsync(
                req.FileCode ?? "", req.EnterpriseCode ?? "", req.ReExtract ?? true);
            return r.Success ? Ok(ApiResponse<object?>.Ok(r.Data)) : Ok(ApiResponse<object?>.Fail(r.Error));
        }

        // ========================================================
        // 三之二、按标准提取条款（P2）
        // ========================================================

        /// <summary>
        /// ★ 入队「按标准提取条款」：给 <c>FileCodes</c> = 只跑这几份；不给 = 整家企业。
        ///
        /// <para><b>只入队真需要跑的</b>（Markdown 就绪 + 该标准未完成 / 已过期 / hash 变了），
        /// 已完成的会被跳过并在 <c>Skipped</c> 里计数 —— 所以本端点<b>可以反复点</b>。</para>
        ///
        /// <para>⚠️ 没有有效计划 ⇒ <c>success=true</c> 但 <c>Queued=0</c> 且 <c>Message</c> 说清原因
        /// （⛔ 不返回 Fail：这不是「坏了」，是「还没建计划」）。</para>
        /// </summary>
        [HttpPost("extract")]
        public async Task<IActionResult> Extract([FromBody] ExtractRequest req)
        {
            var ent = req.EnterpriseCode ?? "";
            if (string.IsNullOrWhiteSpace(ent))
                return Ok(ApiResponse<object?>.Fail("缺少 enterpriseCode"));

            var (queued, skipped, err) = await _service.EnqueueExtractForEnterpriseAsync(
                ent, "manual", req.FileCodes);

            if (!string.IsNullOrEmpty(err))
            {
                // 「还没有有效计划」「没有可提取文档」是人话引导，⛔ 不当成失败弹红字
                return Ok(ApiResponse<object?>.Ok(new
                {
                    Queued = 0,
                    Skipped = 0,
                    Message = err,
                }));
            }

            return Ok(ApiResponse<object?>.Ok(new
            {
                Queued = queued,
                Skipped = skipped,
                Message = queued == 0
                    ? $"没有需要提取的内容（{skipped} 项已完成或缺少 Markdown）"
                    : $"已入队 {queued} 个提取任务（跳过 {skipped} 项已完成）",
            }));
        }

        /// <summary>
        /// ★ <b>「这个条款有哪些文档能作证」</b> —— 按条款号反查提取产物。
        /// <para>粒度是「文档 × 标准」，所以必须同时给标准（GUID）；条款号走
        /// <c>FIND_IN_SET</c> 精确匹配（⛔ 不是 LIKE）。</para>
        /// </summary>
        [HttpGet("extraction/by-clause")]
        public async Task<IActionResult> ExtractionByClause(
            [FromQuery] string standardCode, [FromQuery] string clauseNumber, [FromQuery] bool? onlyDone)
        {
            if (string.IsNullOrWhiteSpace(standardCode) || string.IsNullOrWhiteSpace(clauseNumber))
                return Ok(ApiResponse<object?>.Fail("缺少 standardCode / clauseNumber"));

            var rows = await _extractions.ListByClauseAsync(standardCode, clauseNumber, onlyDone ?? true);
            return Ok(ApiResponse<object?>.Ok(rows.Select(r => new
            {
                r.FileCode,
                r.EnterpriseCode,
                r.StandardCode,
                r.ClauseNumbers,
                r.ClauseCount,
                r.ExtractStatus,
                r.ExtractMessage,
                r.ExtractedAt,
                r.IsStale,
                r.PromptCode,
                r.PromptVersion,
            }).ToList()));
        }

        /// <summary>某文档的提取产物行（不展开 JSON —— 明细走 <c>list</c>）</summary>
        [HttpGet("extraction/{fileCode}")]
        public async Task<IActionResult> Extractions(string fileCode)
        {
            var rows = await _extractions.ListByFileAsync(fileCode);
            return Ok(ApiResponse<object?>.Ok(rows.Where(r => !r.IsDeleted).Select(r => new
            {
                r.FileCode,
                r.StandardCode,
                r.ClauseNumbers,
                r.ClauseCount,
                r.ExtractStatus,
                r.ExtractMessage,
                r.ExtractedAt,
                r.IsStale,
                r.SourceSha256,
                r.PromptCode,
                r.PromptVersion,
                ContentJson = r.ContentJson,
            }).ToList()));
        }

        // ========================================================
        // 四、标准来源（唯一入口）
        // ========================================================
        /// <summary>
        /// ★ 取当前有效计划 + 标准 Code 列表。
        /// <para><b>这是「标准从哪来」的唯一答案</b>：前端与提取编排都必须走这里，
        /// ⛔ 不许各自查 <c>cert_audit_plan</c> 再自己拆 <c>Standards</c>。</para>
        /// </summary>
        [HttpGet("plan-standards")]
        public async Task<IActionResult> PlanStandards([FromQuery] string enterpriseCode)
        {
            if (string.IsNullOrWhiteSpace(enterpriseCode))
                return Ok(ApiResponse<object?>.Fail("缺少 enterpriseCode"));

            var plan = await _service.GetActivePlanAsync(enterpriseCode);
            var (codes, reason) = await _service.ResolvePlanStandardCodesAsync(enterpriseCode);
            return Ok(ApiResponse<object?>.Ok(new
            {
                Plan = plan == null ? null : new { plan.Code, plan.Name, plan.StageCode, plan.Standards },
                StandardCodes = codes,
                // 空列表时说明为什么空（⛔ 不静默当「无标准」）
                Reason = reason,
            }));
        }

        // ========================================================
        // 五、下载 / 预览
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
        // 六、请求模型（DTO 字段 PascalCase，与 DB 列名逐字一致）
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

        /// <summary>★ 只有企业 —— 没有 <c>StageCode</c>（阶段维度已废除）</summary>
        public class EnterpriseRequest
        {
            public string? EnterpriseCode { get; set; }
        }

        /// <summary>列表查询</summary>
        public class ListRequest : EnterpriseRequest
        {
            /// <summary>非空 = 只返回该标准下有提取产物的文件（「按标准看覆盖」视图）</summary>
            public string? StandardCode { get; set; }

            /// <summary>文件名 / 文件夹路径模糊匹配</summary>
            public string? Keyword { get; set; }

            /// <summary>true = 只看转换失败 / 提取失败</summary>
            public bool? OnlyFailed { get; set; }
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

        /// <summary>★ 上传请求：<b>只有企业</b>，⛔ 无 StageCode、⛔ 无 StandardCode</summary>
        public class UploadPlanRequest
        {
            public string? EnterpriseCode { get; set; }
            public List<UploadPlanFileDto>? Items { get; set; }
        }

        public class UploadFileStepDto
        {
            public IFormFile? File { get; set; }
            public string? FileCode { get; set; }
            public string? TaskId { get; set; }
            /// <summary>可选；服务端收到字节后会自行计算并以那个为准</summary>
            public string? Sha256 { get; set; }
        }

        public class UploadTaskRequest
        {
            public string? TaskId { get; set; }
            public string? EnterpriseCode { get; set; }
        }

        /// <summary>重新转换请求</summary>
        public class RegenerateRequest : FileCodeRequest
        {
            /// <summary>true = 转换完成后提示重跑提取（⚠️ 本次不自动重跑，避免读到旧 Markdown）</summary>
            public bool? ReExtract { get; set; }
        }

        public class RestoreRequest : FileCodeRequest
        {
            public int? VersionNumber { get; set; }
        }

        /// <summary>★ 批量删除请求 —— 页面上「删除整个文件夹」用</summary>
        public class BatchDeleteRequest
        {
            /// <summary>★ 要删除的文件业务键集合（前端按文件夹子树展开后提交）</summary>
            public List<string>? FileCodes { get; set; }
            public string? EnterpriseCode { get; set; }
            public string? Reason { get; set; }
        }

        /// <summary>
        /// 提取入队请求。
        /// <para><c>FileCodes</c> 空 = 整家企业（后端按「Markdown 就绪 + 该标准未完成」自动过滤）。</para>
        /// </summary>
        public class ExtractRequest
        {
            public string? EnterpriseCode { get; set; }
            public List<string>? FileCodes { get; set; }
        }
    }
}

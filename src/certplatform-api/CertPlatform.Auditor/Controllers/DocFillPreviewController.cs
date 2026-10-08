using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using YZH.Core.Api.Controllers;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Result;
using CertPlatform.Auditor.Services;
using CertPlatform.Auditor.Services.Ent.Normalize;
using CertPlatform.Shared.DocExtraction;
using CertPlatform.Shared.Entities.Doc;
using CertPlatform.Shared.Storage;

namespace CertPlatform.Auditor.Controllers
{
    /// <summary>
    ///     ★ <b>试填 / 预览控制器</b>（`52` B7）—— 前缀 <c>/api/Auditor/DocFillPreview</c>
    ///
    ///     <para><b>职责（用户 2026-10-05 逐字）</b>：「试填，可以翻到<b>新建的 controller</b> 中，
    ///     因为<b>试填还有生成试填后的 pdf</b>，职责不一样」。⇒ 本控制器 = ① 试填 ② 生成试填后的 PDF。</para>
    ///
    ///     <para><b>★ 为什么落点是 Auditor 而不是 `52` 写的 <c>api/Admin/...</c></b>：
    ///     试填的唯一实现是 <see cref="DocumentFillOrchestrator"/>（<b>Auditor 工程</b>），
    ///     而依赖方向是 <c>Auditor → Admin</c>（单向），<c>Admin</c> <b>⛔ 不能引 <c>Auditor</c></b>。
    ///     把控制器放在 <c>Admin</c> 会因为拿不到编排器而**落不了地**。
    ///     用户原话只要求「新建一个 controller」，<c>api/Admin/…</c> 是文档作者补的路径 ⇒ 以依赖方向为准。</para>
    ///
    ///     <para><b>★ 为什么不继承 <c>YzhControllerBase&lt;DocTemplate&gt;</c></b>：那会顺带
    ///     暴露 <c>DocTemplate</c> 的整组 CRUD（<c>add</c>/<c>update</c>/<c>delete</c>）到专家端路由下 ——
    ///     模板的维护权在<b>后台端</b>，专家端只应「跑任务」。本控制器<b>没有自己的实体</b>，
    ///     故按 <c>AGENTS.md</c> ② 继承 <see cref="WebControllerBase"/>（非裸 <c>ControllerBase</c>，
    ///     已带 <c>[YZHAuthorize]</c> 强认证）。同款先例：<c>EnterpriseOriginalController</c> /
    ///     <c>EnterpriseFileController</c> / <c>ExpertTaskDataGapController</c>。</para>
    ///
    ///     <para><b>★ 与 <c>DocumentFillController</c>（能力演示）的关系</b>：后者输入是<b>文本模板</b>、
    ///     走文本引擎、<b>不落库</b>、需要一家真实企业；本控制器输入是<b>标准域的空白模板</b>、
    ///     走 Office 写入器、<b>不需要企业</b>（试填取值来自模板自带信息）。两者<b>不重复</b>，⛔ 不要合并。</para>
    ///
    ///     <para><b>契约</b>（`22` 号）：业务失败恒 HTTP 200、<c>success</c> 是唯一判据、
    ///     载荷 <b>PascalCase 逐字一致</b>（AGENTS.md ③）。</para>
    /// </summary>
    [ApiController]
    [Route("api/Auditor/[controller]")]
    public class DocFillPreviewController : WebControllerBase
    {
        private readonly DocumentFillOrchestrator _orchestrator;
        private readonly IFileConvertCore _convertCore;
        private readonly IObjectStorage _storage;
        private readonly IDbOrm _db;
        private readonly IUserContext _userContext;
        private readonly WorkspaceContextService _workspace;
        private readonly ILogger<DocFillPreviewController> _logger;

        public DocFillPreviewController(
            DocumentFillOrchestrator orchestrator,
            IFileConvertCore convertCore,
            IObjectStorage storage,
            IDbOrm db,
            IUserContext userContext,
            WorkspaceContextService workspace,
            ILogger<DocFillPreviewController> logger)
        {
            _orchestrator = orchestrator;
            _convertCore = convertCore;
            _storage = storage;
            _db = db;
            _userContext = userContext;
            _workspace = workspace;
            _logger = logger;
        }

        // ════════════════════════════════════════════════════════════════════
        //  一、★ 试填 + 生成 PDF（核心端点）
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        ///     ★ <b>试填一份空白模板并产出预览 PDF</b>。
        ///
        ///     <para><b>链路</b>（`52` §12.2，用户口径「先按 office 填写，再调后台方法形成 pdf」）：</para>
        ///     <list type="number">
        ///         <item>① <b>试填</b>：<c>DocumentFillOrchestrator.FillPreviewAsync</c>（<b>只读</b>，
        ///             复用正式链同一个写入器；取值来自模板自带信息 + 类型占位）。</item>
        ///         <item>② <b>转 PDF</b>：<c>IFileConvertCore.ConvertToPdfAsync</c> —— 纯内核，
        ///             「不做转换、不上传产物」的语义由接口注释保证。⛔ <b>不调 <c>OfficeConvertService</c></b>：
        ///             它绑定 <c>StandardDirectoryFile</c>，会写 <c>PreviewPdfPath</c>/<c>ConvertStatus</c> ——
        ///             试填<b>不是标准目录文件</b>，没有这些列可写。</item>
        ///         <item>③ <b>落 MinIO</b>：用<b>我们自己派生的</b>
        ///             <c>PathBuilder.PreviewFromTemplate(模板路径, ".pdf")</c>（<c>_preview/</c> 段、固定 key）。
        ///             ⚠️ <b>⛔ 不要用 <c>convert.TargetPath</c></b> —— 那是 <c>PathBuilder.Product</c> 派生的
        ///             <c>…/pdf/…</c>，与「源文件预览 PDF」（实测 185 行在用）<b>同 key 互相覆盖</b>。</item>
        ///         <item>④ <b>回写</b>：<c>cert_doc_template.PreviewPdfPath</c> + <c>PreviewTime</c>
        ///             （<b>列级</b>更新，⛔ 不全列写回）。</item>
        ///     </list>
        ///
        ///     <para><b>★ 门槛刻意低于正式填充</b>：⛔ 不要求 <c>PublishStatus='published'</c> ——
        ///     试填正是「发布前验证规则」的工具，要求已发布 = 发布后才能验证 = 死锁。
        ///     ⛔ 也不看文件级 <c>IsLocked</c>（锁的是「不要再重新生成」，与「看一眼样张」无关）。</para>
        ///
        ///     <para><b>★★★ 机构域：平台级账号放行（2026-10-07 用户裁定）</b>：
        ///     用户逐字 ——「<i>这个自动填写和具体规范化逻辑无关，只是正常的将锚点填写上信息，
        ///     然后执行填写，让这个文档内容将 <c>{{}}</c> 进行改写，让实施人员能看到试填后的结果，
        ///     检查文档格式是否正确</i>」。
        ///     ⇒ 试填是<b>模板级只读操作</b>，与「企业资料规范化」无关 ⇒
        ///     <b>未挂靠任何机构的平台级账号（后台实施人员）直接放行</b>，
        ///     ⛔ 不再报「当前账号未挂靠任何机构，无法定位工作区」。
        ///     ⚠️ 只放行「未挂靠任何机构」这一种；数据异常（节点不存在 / 不是 VirtualOrg）照旧拦。</para>
        ///
        ///     <para><b>⚠️ 失败语义</b>：试填成功但 PDF 转换失败时返回 <c>success=false</c>，
        ///     并把「试填已完成」的事实写进 <c>err</c> —— 本端点的交付物是<b>预览 PDF</b>，
        ///     拿不到 PDF 就是没交付；但用户必须知道「填充本身是通的」，
        ///     否则会去查规则配置（错误方向）。</para>
        /// </summary>
        [HttpPost("preview")]
        public async Task<IActionResult> Preview([FromBody] DocFillPreviewRequest req, CancellationToken ct)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.TemplateCode))
                return Ok(ApiResponse<object?>.Fail("缺少模板 Code"));

            // ── ⓪ 机构域解析（★ 2026-10-07：平台级账号放行）──
            var scope = await _workspace.ResolveScopeAsync(_userContext.UserCode);
            string? certBodyCode = scope.Success ? scope.Data?.CertBodyCode : null;

            if (string.IsNullOrWhiteSpace(certBodyCode))
            {
                // ★★ 平台级账号（未挂靠任何机构）= 后台「标准资料填写规则」页的实施人员。
                //    试填是**模板级只读操作**：取值走 `PreviewValueFactory`（模板自带信息 + 类型占位），
                //    ⛔ 不读企业 / 画像 / 企业已填值（见 `DocumentFillOrchestrator.FillPreviewAsync` 注释：
                //    「后台页没有企业，而真实链的 global/manual 都必须要企业 ⇒ 跑真实链必然全空」），
                //    与「企业资料规范化」无关 ⇒ **机构隔离在这里没有要保护的对象** ⇒ 放行。
                //
                //    ⚠️ 判据收窄到「未挂靠任何机构」这一种（`IsPlatformAccountAsync`）：
                //       「账号不存在 / 挂靠节点不存在 / 不是 VirtualOrg / 层级异常」都是**数据异常**，
                //       照旧拦（⛔ 不放过），否则会把真故障静默吞成「能用了」。
                if (!await _workspace.IsPlatformAccountAsync(_userContext.UserCode))
                    return Ok(ApiResponse<object?>.Fail(scope.Error ?? "无法定位当前工作区"));

                _logger.LogInformation(
                    "[DocFillPreview] 平台级账号试填，跳过机构归属校验 {User} / {Template}",
                    _userContext.UserCode, req.TemplateCode);
            }

            // ── ⓪′ 取模板（⛔ 不看 PublishStatus，见方法注释）──
            var template = (await _db.GetOneAsync<DocTemplate>(x =>
                x.Code == req.TemplateCode && x.IsValid == 1)).Data;
            if (template == null)
                return Ok(ApiResponse<object?>.Fail($"模板不存在或已失效：{req.TemplateCode}"));

            // ── ⓪″ 归属校验（★ 仅当解析到了机构域时才生效）──
            //    平台级账号 `certBodyCode == null` ⇒ 跳过。这不是「放松隔离」：
            //    隔离要保护的是**企业数据**，而试填只碰模板自己的 `PreviewPdfPath` / `PreviewTime` 两列，
            //    且模板的维护权本就在后台端（见类注释）。有机构域的人（专家账号）照旧受限。
            if (certBodyCode != null
                && !string.IsNullOrEmpty(template.OrgCode)
                && !string.Equals(template.OrgCode, certBodyCode, StringComparison.Ordinal))
            {
                return Ok(ApiResponse<object?>.Fail("无权访问其他工作区的模板"));
            }

            if (string.IsNullOrWhiteSpace(template.StoragePath))
                return Ok(ApiResponse<object?>.Fail("该模板未登记存储路径，无法试填"));

            // ── ① 试填（只读：⛔ 不写 cert_doc_fill_log / cert_doc_fill_value / 宿主行）──
            var fill = await _orchestrator.FillPreviewAsync(new FillPreviewRequest
            {
                TemplateCode = template.Code,
                OperatorCode = _userContext.UserCode,
                // ★ 预览 Tab「用户改后的值」原样透传（编排器内命中的锚点直接落笔）
                Overrides = req.Overrides ?? new List<PreviewOverrideInput>(),
            }, ct);

            if (!fill.Success || fill.Output.Length == 0)
            {
                return Ok(ApiResponse<object?>.Fail(
                    fill.Message ?? "试填失败（填充引擎未产出内容）"));
            }

            // ── ② 转 PDF（借内核的转换能力；它的 TargetPath 我们不用）──
            FileConvertCoreResult convert;
            try
            {
                convert = await _convertCore.ConvertToPdfAsync(fill.FileName, template.StoragePath, fill.Output);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DocFillPreview] 试填转 PDF 异常 {Template}", template.Code);
                return Ok(ApiResponse<object?>.Fail(
                    $"试填已生成（锚点 {fill.AnchorCount} / 待办 {fill.PendingCount}），"
                    + $"但转 PDF 异常：{ex.Message}"));
            }

            if (!convert.Success || convert.Content == null || convert.Content.Length == 0)
            {
                // ⛔ 不谎报成功；但把「试填本身是通的」如实告知，避免用户去查规则配置
                return Ok(ApiResponse<object?>.Fail(
                    $"试填已生成（锚点 {fill.AnchorCount} / 待办 {fill.PendingCount}），"
                    + $"但转 PDF 失败：{convert.Message ?? "转换内核未返回内容"}"));
            }

            // ── ③ 落 MinIO（★ 用我们自己的 _preview/ 路径，⛔ 不是 convert.TargetPath）──
            var previewPath = PathBuilder.PreviewFromTemplate(template.StoragePath, ".pdf");
            if (string.IsNullOrEmpty(previewPath))
                return Ok(ApiResponse<object?>.Fail("无法从模板路径派生试填预览路径（模板路径格式异常）"));

            try
            {
                using var ms = new MemoryStream(convert.Content);
                await _storage.UploadAsync(previewPath, ms, convert.Content.Length, "application/pdf", ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DocFillPreview] 试填预览上传失败 {Path}", previewPath);
                return Ok(ApiResponse<object?>.Fail($"试填预览上传失败：{ex.Message}"));
            }

            // ── ④ 回写两列（★ 列级更新；⛔ 不全列写回 —— 并发上传链会把这些列清成 NULL）──
            var now = DateTime.Now;
            var patch = new DocTemplate
            {
                Code = template.Code,
                PreviewPdfPath = previewPath,
                PreviewTime = now,
            };
            var update = await _db.UpdateAsync(patch,
                nameof(DocTemplate.PreviewPdfPath), nameof(DocTemplate.PreviewTime));
            if (!update.Success)
            {
                // ⚠️ 字节已落 MinIO、路径也已派生 ⇒ 前端照样能看；只是「有没有试填过」这个标记没记住。
                //    ⛔ 不回滚、不谎报失败，但**必须留痕**（否则下次排查「为什么没记住」无从下手）。
                _logger.LogWarning("[DocFillPreview] 试填预览路径回写失败 {Code}: {Err}",
                    template.Code, update.Error);
            }

            _logger.LogInformation(
                "[DocFillPreview] 试填预览完成 {Template}: 锚点 {Total} / 待办 {Pending} / 完成率 {Completion:P1} → {Path}",
                template.Code, fill.AnchorCount, fill.PendingCount, fill.Completion, previewPath);

            return Ok(ApiResponse<object?>.Ok(new
            {
                TemplateCode = template.Code,
                TemplateStoragePath = template.StoragePath,

                // ★ 前端「填充后预览」视图就用这个路径（`DocPreview` 走 preview-by-path，.pdf 原样透传）
                PreviewPdfPath = previewPath,
                PreviewTime = now,
                PathSaved = update.Success,

                FileKind = fill.FileKind,
                FileName = fill.FileName,

                Status = fill.Status,
                Message = fill.Message,

                AnchorCount = fill.AnchorCount,
                PendingCount = fill.PendingCount,
                Completion = fill.Completion,
                Verified = fill.Verified,

                // ⛔ 非致命问题如实回报（越界 / 丢弃 / 自验收残留），⛔ 不静默
                Warnings = fill.Warnings,
                Pendings = fill.Pendings,
                Values = fill.Values,
            }));
        }

        // ════════════════════════════════════════════════════════════════════
        //  二、试填预览状态（只读）—— 「有没有试填过」
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        ///     ★ <b>读试填预览状态</b> —— 切文件时驱动中栏「填充后预览」视图的可用性。
        ///
        ///     <para><b>为什么单独一个只读端点</b>：<c>directory-tree</c> 的文件叶子带的是
        ///     模板元信息（锚点数 / 发布状态），若把 <c>PreviewPdfPath</c> 也塞进树节点，
        ///     每次 <b>试填</b>都要让整棵树失效重取 —— 而试填是高频动作。
        ///     单文件粒度查询把「刷新」限制在<b>被试填的那一个文件</b>上。</para>
        ///
        ///     <para>⚠️ 不做「文件是否存在」的存储探测（<c>ExistsAsync</c>）：
        ///     那是一次网络往返，而「路径有值」在<b>本项目</b>已足够 —— 写路径与传字节是同一个
        ///     事务动作（上传失败就<b>不写</b>路径）。真丢对象时前端会拿到预览失败提示，
        ///     届时再查存储层（`yzh-diagnose-file-failure` 的入口）。</para>
        /// </summary>
        [HttpGet("preview-info")]
        public async Task<IActionResult> PreviewInfo([FromQuery] string templateCode)
        {
            if (string.IsNullOrWhiteSpace(templateCode))
                return Ok(ApiResponse<object?>.Fail("缺少模板 Code"));

            var template = (await _db.GetOneAsync<DocTemplate>(x =>
                x.Code == templateCode && x.IsValid == 1)).Data;
            if (template == null)
                return Ok(ApiResponse<object?>.Fail($"模板不存在或已失效：{templateCode}"));

            return Ok(ApiResponse<object?>.Ok(new
            {
                TemplateCode = template.Code,
                // ★ 空 = 从未试填过 ⇒ 前端据此禁用「填充后预览」视图
                HasPreview = !string.IsNullOrWhiteSpace(template.PreviewPdfPath),
                PreviewPdfPath = template.PreviewPdfPath ?? string.Empty,
                PreviewTime = template.PreviewTime,
            }));
        }

        // ════════════════════════════════════════════════════════════════════
        //  三、请求模型（DTO 字段 PascalCase，与 DB 列名逐字一致）
        // ════════════════════════════════════════════════════════════════════

        public sealed class DocFillPreviewRequest
        {
            /// <summary>空白模板 <c>cert_doc_template.Code</c>（⛔ 不是 <c>StandardFileCode</c>）</summary>
            public string? TemplateCode { get; set; }

            /// <summary>操作人 Code（留空 = 取当前登录用户）</summary>
            public string? OperatorCode { get; set; }

            /// <summary>
            ///     人工覆盖值（2026-10-07 预览 Tab「自动填写 → 用户改 → 再预览」）——
            ///     命中的锚点直接用 <c>Value</c> 落笔，⛔ 不回写任何库。
            /// </summary>
            public List<PreviewOverrideInput>? Overrides { get; set; }
        }
    }
}

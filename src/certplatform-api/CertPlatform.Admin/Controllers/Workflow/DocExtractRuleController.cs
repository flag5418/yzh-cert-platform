using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using YZH.Core.Api.Controllers;
using YZH.Core.Api.Services;
using YZH.Core.Stand.Helpers;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models;
using YZH.Core.Stand.Models.Config;
using YZH.Core.Stand.Models.Request;
using YZH.Core.Stand.Models.Result;
using YZH.Core.Api.Models.System;
using YZH.Core.DataBase.Interfaces;
using CertPlatform.Admin.Entities.Doc;
using CertPlatform.Admin.Entities.Workflow;
using CertPlatform.Admin.Services.Cert;
using CertPlatform.Shared.DocExtraction;
using CertPlatform.Shared.Domain.Scope;
using CertPlatform.Shared.Storage;

namespace CertPlatform.Admin.Controllers.Workflow;

/// <summary>
///     文档提取规则管理控制器
///
///     功能：
///     - 每标准一行，存储该国标 Markdown 知识库 + 提取提示词
///     - 标准 CRUD（左树数据源）
///     - /test：上传文档 → LLM → 返回 JSON（不落库）
///     - /prompt：从 cert_prompt_template 读取/保存提示词（作用域 = 标准Code）
///
///     路由前缀：/api/Admin/Workflow/DocExtractRule
///     末段 DocExtractRule ⇒ ApiCode 按框架规则生成
///
///     数据库：cert_doc_extract_rules（实体）+ cert_prompt_template（提示词）
///     前端路由：/business/doc-extract-rule
///
///     ★ 提示词存储策略（2026-10-29 统一）：
///       全局默认 ⇒ PromptCode=doc_clause_extract, ScopeKey=空
///       标准级   ⇒ PromptCode=doc_clause_extract, ScopeKey=标准Code（如 iso9001）
///       页面编辑的正文 = Auditor 侧 ClauseExtractExecutor 真正消费的那条（2026-11-10 从 doc_extract 切过来）
///       优先匹配标准级，缺失回落到全局默认。
/// </summary>
[ApiController]
[Route("api/Admin/Workflow/[controller]")]
public class DocExtractRuleController : YzhControllerBase<DocExtractRule>
{
    private readonly PromptTemplateService _promptTemplate;
    private readonly IFileConvertCore _convert;
    private readonly LlmInvokeService _llm;
    private readonly IDbOrm _db;
    private readonly ILogger<DocExtractRuleController> _logger;
    private readonly IObjectStorage _storage;
    private readonly ICoreResourceQuery _core;

    /// <summary>
    /// 注册页面要展示的占位符（与 DocExtractPromptService 的替换常量逐字一致）。
    /// <para>⚠️ 只影响前端提示，不参与渲染；渲染在 Auditor 侧 DocExtractPromptService.Render。</para>
    /// </summary>
    static DocExtractRuleController()
    {
        PromptTemplateService.RegisterPlaceholder("doc_clause_extract", "{{standard_name}}", "标准名称，如 GB/T 19001-2016 质量管理体系");
        PromptTemplateService.RegisterPlaceholder("doc_clause_extract", "{{standard_code}}", "标准短码，如 iso9001");
        PromptTemplateService.RegisterPlaceholder("doc_clause_extract", "{{document_name}}", "被分析文档的文件名");
        PromptTemplateService.RegisterPlaceholder("doc_clause_extract", "{{clause_list}}", "可命中的条款清单（编号 + 标题，一行一条）");
        PromptTemplateService.RegisterPlaceholder("doc_clause_extract", "{{document_content}}", "文档 Markdown 全文");
    }

    public DocExtractRuleController(
        EntityService<DocExtractRule> entityService,
        IUserContext userContext,
        PromptTemplateService promptTemplate,
        IFileConvertCore convert,
        LlmInvokeService llm,
        IDbOrm db,
        ILogger<DocExtractRuleController> logger,
        IObjectStorage storage,
        ICoreResourceQuery core)
        : base(entityService, userContext)
    {
        _promptTemplate = promptTemplate;
        _convert = convert;
        _llm = llm;
        _db = db;
        _logger = logger;
        _storage = storage;
        _core = core;
    }

    /// <summary>加载 EntityConfig（Workflow/DocExtractRule.json）</summary>
    protected override EntityConfig LoadConfig()
    {
        return EntityConfigHelper.GetConfig("Workflow/DocExtractRule");
    }

    /// <summary>★ 开发期强制暴露缺配置问题</summary>
    protected override bool StrictConfigLoad => true;

    /// <summary>
    /// 修改前校验：不能改 StandardCode（标准编码是"每标准一行"的锚点）
    /// 标准变了 → 应该新增一条，而不是改原来的
    /// </summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeUpdate(DocExtractRule entity)
    {
        var existing = await Entity.GetByCode(entity.Code);
        if (existing.Success && existing.Data != null && existing.Data.StandardCode != entity.StandardCode)
            return (false, "标准编码不可修改");
        return (true, null);
    }

    /// <summary>
    /// 删除前校验：检查是否存在测试数据关联（当前无关联表，此处预留钩子）
    /// </summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeDelete(string[] codes)
    {
        // TODO: 未来等 cert_doc_extract 等表上线后，检查关联数据
        return (true, null);
    }

    /// <summary>
    /// 列表查询后脱敏 MarkdownContent / PromptContent（太大，不在列表中传输全文）。
    /// ⚠️ 排除 "按 Code 取单条详情" 的场景——那个端点就是为取全文存在的。
    /// </summary>
    protected override void OnQueried(PagedResult<DocExtractRule> result)
    {
        // ★ 只对多行列表脱敏；单行详情（code 过滤器命中 1 条）保留全文。
        //   前端 getRuleDetail 复用 /filter 端点 + Code=eq 过滤器，故以 Items.Count=1 + 有 Code 过滤器判定。
        if (result.Items.Count > 1)
        {
            foreach (var row in result.Items)
            {
                row.MarkdownContent = null;
                row.PromptContent = null;
            }
        }
    }

    /// <summary>
    /// ★ 取单条详情（含 MarkdownContent + PromptContent 全文）。
    /// 前端点左树节点时按需调用，绕过 OnQueried 的列表脱敏。
    /// </summary>
    [HttpGet("detail/{code}")]
    public async Task<IActionResult> GetDetail(string code)
    {
        var result = await Entity.GetByCode(code);
        if (!result.Success || result.Data == null)
            return Ok(ApiResponse<DocExtractRule?>.Fail("记录不存在", 404));
        return Ok(ApiResponse<DocExtractRule?>.Ok(result.Data));
    }

    // ════════════════════════════════════════════════════════════════════
    // 提示词维护（委托通用 PromptTemplateService，PromptCode = "doc_clause_extract"）
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 读取某标准的文档提取提示词。
    /// <para>标准级（ScopeKey=标准Code）优先；缺失回落到全局默认（ScopeKey=空）。</para>
    /// </summary>
    /// <param name="standardCode">标准短码（如 iso9001）；空 = 取全局默认</param>
    [HttpGet("prompt")]
    public async Task<IActionResult> GetPrompt(
        [FromQuery] string? standardCode = null,
        [FromQuery] string? promptCode = null)
    {
        var code = string.IsNullOrWhiteSpace(promptCode) ? "doc_clause_extract" : promptCode.Trim();
        var template = await _promptTemplate.GetAsync(code, null, standardCode);
        return Ok(ApiResponse<DocExtractPromptDto>.Ok(new DocExtractPromptDto
        {
            PromptCode = code,
            StandardCode = standardCode,
            Template = template.Template,
            IsFallback = template.Status == "fallback",
            Placeholders = PromptTemplateService.GetPlaceholders(code),
        }));
    }

    /// <summary>
    /// 保存某标准的文档提取提示词（幂等 upsert）。
    /// <para>standardCode 空 = 保存全局默认；非空 = 保存该标准的专属提示词。</para>
    /// </summary>
    [HttpPost("prompt")]
    public async Task<IActionResult> SavePrompt([FromBody] DocExtractPromptSaveRequest request)
    {
        var promptCode = string.IsNullOrWhiteSpace(request.PromptCode)
            ? "doc_clause_extract"
            : request.PromptCode.Trim();
        var (ok, message) = await _promptTemplate.SaveAsync(
            promptCode,
            request.Template,
            null,
            request.StandardCode,
            promptCode == "doc_clause_extract" ? "文档提取提示词" : $"文档类型提示词（{promptCode}）",
            request.StandardCode is { Length: > 0 }
                ? $"标准【{request.StandardCode}】的专属提取提示词"
                : $"{promptCode} 的全局默认模板",
            UserContext.UserCode);

        return ok
            ? Ok(ApiResponse.Ok(message))
            : Ok(ApiResponse.BizFail(message));
    }

    /// <summary>
    /// 可选提示词类型（页面下拉数据源）。
    /// <para>★ 唯一事实源 = <see cref="DocExtractionPromptCatalog"/>，与 Auditor 运行时
    /// <c>EnterpriseDocExtractionRouter.PromptCodeForMode</c> 同一映射，前端不再硬编码。</para>
    /// </summary>
    [HttpGet("prompt/types")]
    public IActionResult GetPromptTypes()
        => Ok(ApiResponse<IReadOnlyList<DocExtractPromptTypeDto>>.Ok(
            DocExtractionPromptCatalog.All.Select(e => new DocExtractPromptTypeDto
            {
                PromptCode = e.PromptCode,
                Mode = e.Mode,
                DisplayName = e.DisplayName,
                Group = e.Group,
                Sort = e.Sort,
            }).ToList()));

    // ════════════════════════════════════════════════════════════════════
    // ★ 页面内试跑（2026-11-10）：上传文档 → 转 Markdown → 用「当前编辑正文」跑一次
    // ════════════════════════════════════════════════════════════════════

    /// <summary>legacy 兜底提示词 Code（与 DocExtractionPromptCatalog / cert_prompt_template 一致）</summary>
    private const string PromptCodeLegacy = "doc_clause_extract";

    /// <summary>画像总闸提示词 Code</summary>
    private const string ProfilePromptCode = "doc_profile";

    /// <summary>单次试跑送模型的文档字符上限（与 ClauseExtractExecutor.ChunkChars 同口径 3 万字）</summary>
    private const int MaxTestDocChars = 30_000;

    /// <summary>画像提示词注入的正文开头长度（与 <c>{{first_2000}}</c> 同口径）</summary>
    private const int ProfileHeadChars = 2_000;

    /// <summary>画像提示词注入的标题清单条数上限</summary>
    private const int ProfileMaxHeadings = 80;

    // 占位符（与 DocExtractPromptService / cert_prompt_template 逐字一致；本控制器在 Admin 侧，不反向依赖 Auditor，故本地声明）
    private const string PhDocProfile = "{{doc_profile}}";
    private const string PhDocumentText = "{{document_text}}";
    private const string PhFileName = "{{file_name}}";
    private const string PhFolderPath = "{{folder_path}}";
    private const string PhHeadings = "{{headings}}";
    private const string PhFirst2000 = "{{first_2000}}";
    // ★ V4「结构指纹 + 角色定位」：画像提示词新增两个输入占位符（代码确定性提取，零模型成本）
    private const string PhFingerprints = "{{fingerprints}}";
    private const string PhToc = "{{toc}}";

    /// <summary>
    /// <b>试跑文档提取提示词</b>（不落库、不写队列）。
    ///
    /// <para>入参 = 文件 + 标准短码 + <b>页面当前正文</b> + <b>提示词 Code</b>（未保存也能试，实现边改边看）。</para>
    /// <para>★ 按<b>提示词家族</b>渲染占位符（此前只认 legacy 的 5 个占位符，其它提示词一律拿不到文档内容 ⇒ 模型只会回「没给输入」）：
    /// <list type="bullet">
    ///   <item><c>doc_profile</c>（画像总闸）⇒ <c>{{file_name}}/{{folder_path}}/{{headings}}/{{first_2000}}</c></item>
    ///   <item><c>doc_extract_*</c>（9 类按类型提取）⇒ 先跑画像得 profile JSON，再注入 <c>{{doc_profile}}/{{document_text}}</c></item>
    ///   <item><c>doc_clause_extract</c>（legacy 兜底）⇒ <c>{{standard_name}}/{{standard_code}}/{{document_name}}/{{clause_list}}/{{document_content}}</c></item>
    /// </list></para>
    /// <para>链路与线上同源：<c>IFileConvertCore.ConvertToMarkdownInlineAsync</c>（anydoc → OCR 兜底）。
    /// ★ 2026-11-25：取消「小节模式」，只做<b>全文试跑</b>（小节切分会掩盖占位符渲染问题）。</para>
    /// </summary>
    [HttpPost("prompt/test")]
    [RequestSizeLimit(200_000_000)]
    public async Task<IActionResult> TestPrompt([FromForm] DocExtractPromptTestRequest req)
    {
        if (req == null || req.File == null || req.File.Length == 0)
            return Ok(ApiResponse.Fail("请先选择要测试的文档"));
        if (string.IsNullOrWhiteSpace(req.Template))
            return Ok(ApiResponse.Fail("提示词正文为空：请先在编辑器里填写提示词再试跑"));

        // ★ 上传契约闸门（唯一事实源 UploadFilePolicy）——前端 accept 可绕过，此处硬拦
        var verdict = UploadFilePolicy.Check(req.File.FileName);
        if (!verdict.Ok)
            return Ok(ApiResponse.Fail(verdict.Message));

        // 1) 文件 → Markdown（与线上同一条转换内核）
        byte[] bytes;
        using (var ms = new MemoryStream())
        {
            await req.File.CopyToAsync(ms);
            bytes = ms.ToArray();
        }
        var conv = await _convert.ConvertToMarkdownInlineAsync(req.File.FileName, bytes);
        if (!conv.Success || conv.Content == null || conv.Content.Length == 0)
            return Ok(ApiResponse.Fail("文档转换失败：" + (string.IsNullOrWhiteSpace(conv.Message) ? "未知原因" : conv.Message)));

        var markdown = Encoding.UTF8.GetString(conv.Content);

        // 2) 标准信息（页面传的是短码 iso9001；也容忍 GUID）
        var stdList = await _db.Client.Ado.SqlQueryAsync<StdRow>(
            "SELECT Code, StandardCode, StandardName FROM cert_iso_standard "
            + "WHERE (StandardCode = @key OR Code = @key) AND IsDeleted = 0 AND IsValid = 1",
            new { key = (req.StandardCode ?? "").Trim() });
        if (stdList == null || stdList.Count != 1)
            return Ok(ApiResponse.Fail("标准不存在或存在多个版本，请选择明确的标准版本"));
        var std = stdList.FirstOrDefault();
        var stdName = std?.StandardName ?? (req.StandardCode ?? "未知标准");
        var stdGuid = std?.Code ?? "";
        var stdShort = std?.StandardCode ?? (req.StandardCode ?? "");

        // 3) 按「提示词家族」分流渲染
        var promptCode = string.IsNullOrWhiteSpace(req.PromptCode) ? PromptCodeLegacy : req.PromptCode.Trim();
        object result = ResolveFamily(promptCode) switch
        {
            PromptFamily.Profile => await RunProfileTestAsync(req, markdown, stdShort, promptCode),
            PromptFamily.Mode => await RunModeTestAsync(req, markdown, stdShort, promptCode),
            _ => await RunLegacyTestAsync(req, markdown, stdName, stdShort, stdGuid),
        };
        return Ok(ApiResponse<object>.Ok(result));
    }

    /// <summary>提示词家族（决定占位符渲染方式）。</summary>
    private enum PromptFamily { Profile, Mode, Legacy }

    /// <summary>提示词 Code → 家族（判据 = 共享层 <see cref="DocExtractionPromptCatalog"/>，与运行时同一映射）。</summary>
    private static PromptFamily ResolveFamily(string promptCode)
    {
        var entry = DocExtractionPromptCatalog.All.FirstOrDefault(e =>
            string.Equals(e.PromptCode, promptCode, StringComparison.Ordinal));
        if (entry == null) return PromptFamily.Legacy;               // 未知 Code ⇒ 仍按 legacy 渲染，不静默丢占位符
        if (entry.Mode.Length == 0) return PromptFamily.Profile;     // doc_profile（画像总闸）
        return string.Equals(entry.Mode, "legacy", StringComparison.Ordinal)
            ? PromptFamily.Legacy
            : PromptFamily.Mode;
    }

    /// <summary>
    /// 画像总闸试跑（★ 整链）：先按画像提示词得画像 JSON，再据其 <c>ExtractionMode</c>
    /// 自动续跑对应的「按类型提取」提示词（手册→manual_chapter 等），
    /// 一次输出真正「按章节组织的内容」；画像 JSON 附在 <c>ProfileOutput</c> 供核对分流。
    /// <para>未续跑时（mode 缺失 / none / 库中缺该 mode 提示词）只返回画像，并在 <c>Note</c> 说明原因。</para>
    /// </summary>
    private async Task<object> RunProfileTestAsync(
        DocExtractPromptTestRequest req, string markdown, string standardShort, string promptCode)
    {
        var fileName = req.File!.FileName ?? "";
        var ai = await ReadAiSettingsAsync();

        // ① 画像判定
        var profilePrompt = FillProfilePlaceholders(req.Template!, fileName, markdown);
        var (pResp, pMs) = await CallModelAsync(ai, profilePrompt, 0.1f, 2_048);
        await LogUsageAsync(ai, pResp, standardShort);
        if (!pResp.Success)
            return Pack(req, markdown, profilePrompt, promptCode, ai.Model, pResp, pMs, "画像判定");

        var profileRaw = string.IsNullOrWhiteSpace(pResp.Content) ? "" : StripFence(pResp.Content);
        var profileJson = IsJson(profileRaw) ? profileRaw : "{}";

        // ② 画像 → ExtractionMode → 「按类型提取」提示词（唯一映射 = 共享层目录）
        var mode = ReadExtractionMode(profileJson);
        var modePromptCode = DocExtractionPromptCatalog.PromptCodeForMode(mode);
        if (modePromptCode.Length == 0)
        {
            var why = string.IsNullOrWhiteSpace(mode)
                ? "画像未给出 ExtractionMode，无法续跑按类型提取"
                : string.Equals(mode, "none", StringComparison.Ordinal)
                    ? "画像判定与标准无关（ExtractionMode=none），无需续跑按类型提取"
                    : $"画像判定的 ExtractionMode={mode} 暂无对应提取提示词，无法续跑";
            return Pack(req, markdown, profilePrompt, promptCode, ai.Model, pResp, pMs, "画像判定",
                why, profileJson, mode, null);
        }

        var modeTemplate = await _promptTemplate.GetAsync(modePromptCode, null, standardShort);
        if (string.IsNullOrWhiteSpace(modeTemplate.Template))
            return Pack(req, markdown, profilePrompt, promptCode, ai.Model, pResp, pMs, "画像判定",
                $"画像判定 ExtractionMode={mode}，但库中缺 {modePromptCode} 提示词，未续跑",
                profileJson, mode, modePromptCode);

        // ③ 续跑「按类型提取」：注入画像 + 全文（与 RunModeTestAsync 同口径）
        var extractPrompt = modeTemplate.Template
            .Replace(PhDocProfile, profileJson, StringComparison.Ordinal)
            .Replace(PhDocumentText, markdown, StringComparison.Ordinal);
        var maxTokens = Math.Min(ai.MaxTokens <= 0 ? 8_192 : ai.MaxTokens, 8_192);
        var (eResp, eMs) = await CallModelAsync(ai, extractPrompt, ai.Temperature, maxTokens);
        await LogUsageAsync(ai, eResp, standardShort);

        return Pack(req, markdown, extractPrompt, modePromptCode, ai.Model, eResp, eMs,
            $"画像→{mode} 按类型提取",
            $"画像判定 ExtractionMode={mode}，已自动续跑 {modePromptCode}",
            profileJson, mode, modePromptCode);
    }

    /// <summary>
    /// 按类型提取试跑：先跑 <c>doc_profile</c> 得画像 JSON，再注入 <c>{{doc_profile}}</c> 与 <c>{{document_text}}</c>。
    /// <para>⚠️ 线上 <c>{{doc_profile}}</c> 注入的是确定性画像（<c>EnterpriseDocProfileBuilder</c>）；
    /// 试跑为免 Admin 反向依赖 Auditor（守卫 A-R1），改用 doc_profile 提示词现场生成。</para>
    /// </summary>
    private async Task<object> RunModeTestAsync(
        DocExtractPromptTestRequest req, string markdown, string standardShort, string promptCode)
    {
        var profileJson = "{}";
        string? note = null;

        var profileTemplate = await _promptTemplate.GetAsync(ProfilePromptCode, null, standardShort);
        if (string.IsNullOrWhiteSpace(profileTemplate.Template))
        {
            note = "库中缺 doc_profile 提示词，已按空画像试跑";
        }
        else
        {
            var profilePrompt = FillProfilePlaceholders(profileTemplate.Template, req.File!.FileName ?? "", markdown);
            var ai = await ReadAiSettingsAsync();
            var pr = await _llm.CompleteAsync(new LlmInvokeRequest
            {
                BaseUrl = ai.BaseUrl,
                ApiKey = ai.ApiKey,
                Model = ai.Model,
                Temperature = 0.1f,
                MaxTokens = 2_048,
                Prompt = profilePrompt,
                ForceJson = true,
                TimeoutSeconds = 300,
            });
            await LogUsageAsync(ai, pr, standardShort);

            var pj = string.IsNullOrWhiteSpace(pr.Content) ? "" : StripFence(pr.Content);
            if (IsJson(pj))
                profileJson = pj;
            else
                note = "画像提示词输出不是合法 JSON，已按空画像试跑"
                       + (string.IsNullOrWhiteSpace(pr.Message) ? "" : "：" + pr.Message);
        }

        var prompt = (req.Template ?? string.Empty)
            .Replace(PhDocProfile, profileJson, StringComparison.Ordinal)
            .Replace(PhDocumentText, markdown, StringComparison.Ordinal);
        return await InvokeAndPackAsync(req, markdown, prompt, promptCode, standardShort, "按类型提取", note);
    }

    /// <summary>注入画像提示词的 6 个占位符（folder_path 无上传目录信息，置空）。</summary>
    private static string FillProfilePlaceholders(string template, string fileName, string markdown)
    {
        var headings = ExtractDocHeadings(markdown);
        var fingerprints = DocFingerprintExtractor.Extract(markdown, headings);
        return (template ?? string.Empty)
            .Replace(PhFileName, fileName ?? string.Empty, StringComparison.Ordinal)
            .Replace(PhFolderPath, string.Empty, StringComparison.Ordinal)
            .Replace(PhHeadings, string.Join("\n", headings.Select(h => h.Title)), StringComparison.Ordinal)
            .Replace(PhFirst2000, Head(markdown, ProfileHeadChars), StringComparison.Ordinal)
            .Replace(PhFingerprints, FormatFingerprints(fingerprints), StringComparison.Ordinal)
            .Replace(PhToc, FormatToc(headings), StringComparison.Ordinal);
    }

    /// <summary>调一次模型（试跑口径：ForceJson + 600s 超时），返回响应与实测耗时。</summary>
    private async Task<(LlmInvokeResponse Resp, long Ms)> CallModelAsync(
        AiCfg ai, string prompt, float temperature, int maxTokens)
    {
        var sw = Stopwatch.StartNew();
        var resp = await _llm.CompleteAsync(new LlmInvokeRequest
        {
            BaseUrl = ai.BaseUrl,
            ApiKey = ai.ApiKey,
            Model = ai.Model,
            Temperature = temperature,
            MaxTokens = maxTokens,
            Prompt = prompt,
            ForceJson = true,
            TimeoutSeconds = 600,
        });
        sw.Stop();
        return (resp, sw.ElapsedMilliseconds);
    }

    /// <summary>调一次模型并打包试跑结果（整篇口径；profile / mode 两族共用）。</summary>
    private async Task<object> InvokeAndPackAsync(
        DocExtractPromptTestRequest req, string markdown, string prompt,
        string promptCode, string standardShort, string familyName, string? note = null)
    {
        var ai = await ReadAiSettingsAsync();
        var maxTokens = Math.Min(ai.MaxTokens <= 0 ? 8_192 : ai.MaxTokens, 8_192);
        var (resp, ms) = await CallModelAsync(ai, prompt, ai.Temperature, maxTokens);
        await LogUsageAsync(ai, resp, standardShort);
        return Pack(req, markdown, prompt, promptCode, ai.Model, resp, ms, familyName, note);
    }

    /// <summary>
    /// 打包试跑结果（统一形状）。
    /// <para>★ 画像整链额外三字段：<c>ProfileOutput</c>（画像 JSON）、<c>RoutedMode</c>（画像判定的提取模式）、
    /// <c>RoutedPromptCode</c>（自动续跑的按类型提取提示词 Code）。</para>
    /// </summary>
    private static object Pack(
        DocExtractPromptTestRequest req, string markdown, string prompt, string promptCode,
        string model, LlmInvokeResponse resp, long durationMs, string familyName,
        string? note = null, string? profileOutput = null,
        string? routedMode = null, string? routedPromptCode = null)
    {
        var raw = string.IsNullOrWhiteSpace(resp.Content) ? "" : StripFence(resp.Content);
        return new
        {
            Success = resp.Success,
            Message = resp.Success ? $"试跑完成（{familyName}）" : resp.Message,
            Mode = promptCode,
            Model = model,
            DurationMs = durationMs,
            PromptTokens = resp.PromptTokens ?? 0,
            CompletionTokens = resp.CompletionTokens ?? 0,
            JsonValid = IsJson(raw),
            Output = raw,
            PromptChars = prompt.Length,
            ClauseCount = 0,
            AllClauseCount = 0,
            SkippedByDocCheckMode = 0,
            MarkdownLength = markdown.Length,
            Truncated = false,
            MarkdownHead = Head(markdown, 1_200),
            FileName = req.File?.FileName,
            Note = note,
            ProfileOutput = profileOutput,
            RoutedMode = routedMode,
            RoutedPromptCode = routedPromptCode,
        };
    }

    /// <summary>试跑失败但保持结果契约形状（避免前端字段缺失）。</summary>
    private static object TestFailure(
        string message, DocExtractPromptTestRequest req, string markdown, string promptCode)
        => new
        {
            Success = false,
            Message = message,
            Mode = promptCode,
            Model = "",
            DurationMs = 0L,
            PromptTokens = 0,
            CompletionTokens = 0,
            JsonValid = false,
            Output = "",
            PromptChars = 0,
            ClauseCount = 0,
            AllClauseCount = 0,
            SkippedByDocCheckMode = 0,
            MarkdownLength = markdown?.Length ?? 0,
            Truncated = false,
            MarkdownHead = Head(markdown ?? string.Empty, 1_200),
            FileName = req?.File?.FileName,
            Note = (string?)null,
            ProfileOutput = (string?)null,
            RoutedMode = (string?)null,
            RoutedPromptCode = (string?)null,
        };

    /// <summary>取字符串前 <paramref name="max"/> 个字符（超长即截断）。</summary>
    private static string Head(string text, int max)
        => string.IsNullOrEmpty(text) ? string.Empty
            : (text.Length > max ? text.Substring(0, max) : text);

    /// <summary>
    /// 从 Markdown 抽标题清单（画像提示词的 <c>{{headings}}</c>）。
    /// <para>兼容 <c># 标题</c> 与独占一行的加粗短标题（.doc 经 anydoc 转换后多为后者）。</para>
    /// </summary>
    private static string ExtractHeadings(string markdown)
        => string.Join("\n", ExtractDocHeadings(markdown).Select(h => h.Title));

    /// <summary>
    /// 从 Markdown 抽章节头（<c># 标题</c> 取 <c>#</c> 数当层级；独占一行的加粗短标题当 0 级）。
    /// <para><c>{{headings}}</c> / <c>{{toc}}</c> 与结构指纹提取共用这一份解析，⛔ 不各自为政。</para>
    /// </summary>
    private static List<DocHeading> ExtractDocHeadings(string markdown)
    {
        var list = new List<DocHeading>();
        if (string.IsNullOrWhiteSpace(markdown)) return list;

        foreach (var raw in markdown.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;

            string? title = null;
            var level = 0;
            var h = Regex.Match(line, @"^(#{1,6})\s+(.+?)\s*#*$");
            if (h.Success)
            {
                title = h.Groups[2].Value.Trim();
                level = h.Groups[1].Value.Length;
            }
            else
            {
                var b = Regex.Match(line, @"^\*\*(.{2,60}?)\*\*$");
                if (b.Success) title = b.Groups[1].Value.Trim();
            }

            if (string.IsNullOrWhiteSpace(title) || title.Length > 80) continue;
            list.Add(new DocHeading(title, level, true));
            if (list.Count >= ProfileMaxHeadings) break;
        }
        return list;
    }

    /// <summary>结构指纹 → 提示词可读正文（一行一条：权重 + Key + 证据）。</summary>
    private static string FormatFingerprints(IReadOnlyList<DocFingerprint> fingerprints)
        => fingerprints.Count == 0
            ? "（未命中任何结构指纹 ⇒ DocType / ExtractionMode 必须为 unclassified）"
            : string.Join("\n", fingerprints.Select(f => $"- [{f.Weight}] {f.Key}：{f.Evidence}"));

    /// <summary>章节头 → 提示词可读目录（一行一条：序号 + 章节名）。</summary>
    private static string FormatToc(IReadOnlyList<DocHeading> headings)
        => headings.Count == 0
            ? "（无标题）"
            : string.Join("\n", headings.Select((h, i) => $"{i + 1}. {h.Title}"));

    /// <summary>条款清单：过滤 DocCheckMode=none；没配过就退回全部（与线上一致）。</summary>
    private async Task<(List<DocClauseRef> Refs, int Skipped, string? Note)> ResolveClausesAsync(
        List<ClauseRow> rows, string standardShort)
    {
        var all = rows
            .Where(x => !string.IsNullOrWhiteSpace(x.ClauseNumber))
            .Select(x => new DocClauseRef(
                x.Code ?? "",
                x.ClauseNumber!.Trim(),
                x.Title ?? "",
                x.Sort))
            .ToList();

        if (all.Count == 0 || string.IsNullOrWhiteSpace(standardShort))
            return (all, 0, null);

        var attrs = await _db.Client.Ado.SqlQueryAsync<ClauseAttrRow>(
            "SELECT ClauseCode, AttrValue FROM cert_clause_attr "
            + "WHERE StandardCode = @code AND AttrCode = 'DocCheckMode' AND IsDeleted = 0 AND IsValid = 1",
            new { code = standardShort });

        var clauseCodes = all.Select(c => c.Code).ToHashSet(StringComparer.Ordinal);
        var matched = (attrs ?? new List<ClauseAttrRow>())
            .Where(a => !string.IsNullOrWhiteSpace(a.ClauseCode) && clauseCodes.Contains(a.ClauseCode))
            .ToList();
        if (matched.Count == 0)
            return (all, 0, $"标准【{standardShort}】的条款还没配 DocCheckMode，本次按全部条款试跑");

        var groups = matched.GroupBy(a => a.ClauseCode!, StringComparer.Ordinal).ToList();
        var duplicates = groups.Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        var modeByClause = groups.Where(g => g.Count() == 1)
            .ToDictionary(g => g.Key, g => (g.Single().AttrValue ?? "").Trim().ToLowerInvariant(), StringComparer.Ordinal);

        // 未配 DocCheckMode 的条款按「要查」处理，只剔除明确 none 的
        var kept = all
            .Where(c => !modeByClause.TryGetValue(c.Code, out var mode) || mode != "none")
            .ToList();
        return (kept, all.Count - kept.Count, duplicates.Count == 0 ? null
            : $"部分条款的 DocCheckMode 存在重复，已保留这些条款待核实：{string.Join(",", duplicates)}");
    }

    /// <summary>
    /// legacy 兜底试跑（<c>doc_clause_extract</c>）：整篇 30k + 全量条款清单 + 5 个占位符，
    /// 附标准条款 Markdown（仅供模型理解条款，不是企业文档），一次大 JSON。
    /// </summary>
    private async Task<object> RunLegacyTestAsync(
        DocExtractPromptTestRequest req,
        string markdown,
        string stdName,
        string stdShort,
        string stdGuid)
    {
        // 1) 条款清单（编号 + 标题；与线上 ClauseExtractExecutor 同口径）
        var queried = string.IsNullOrWhiteSpace(stdGuid)
            ? new List<ClauseRow>()
            : await _db.Client.Ado.SqlQueryAsync<ClauseRow>(
                "SELECT Code, ClauseNumber, Title, Sort FROM cert_iso_clause "
                + "WHERE StandardCode = @guid AND IsValid = 1 AND IsDeleted = 0 "
                + "GROUP BY Code, ClauseNumber, Title, Sort ORDER BY MIN(Sort)",
                new { guid = stdGuid });
        var clauseRows = queried ?? new List<ClauseRow>();

        var (clauseRefs, skippedByMode, filterNote) = await ResolveClausesAsync(clauseRows, stdShort);
        if (clauseRefs.Count == 0)
            return TestFailure("标准未配置有效条款，无法试跑", req, markdown, PromptCodeLegacy);

        // 2) 标准条款 Markdown（供模型理解条款，不是企业文档）
        string clauseMarkdown;
        try
        {
            var standard = await _core.GetStandardByCodeAsync(stdGuid)
                ?? throw new InvalidOperationException("标准不存在或已停用");
            clauseMarkdown = await ClauseMarkdownInput.LoadAsync(_storage, standard);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return TestFailure("条款 Markdown 不可用：" + ex.Message, req, markdown, PromptCodeLegacy);
        }
        if (clauseMarkdown.Length > MaxTestDocChars)
            return TestFailure("条款 Markdown 超过试跑输入上限，无法试跑", req, markdown, PromptCodeLegacy);

        var truncated = markdown.Length > MaxTestDocChars;
        var docForModel = truncated ? markdown.Substring(0, MaxTestDocChars) : markdown;

        var clauseList = string.Join("\n", clauseRefs
            .Select(c => "- " + c.ClauseNumber + (string.IsNullOrWhiteSpace(c.Title) ? "" : " " + c.Title)));

        var prompt = RenderPrompt(req.Template, stdName, stdShort, req.File!.FileName ?? "",
            clauseList, docForModel)
            + "\n\n【标准条款 Markdown，仅用于理解条款，不是企业文档】\n" + clauseMarkdown;

        var ai = await ReadAiSettingsAsync();
        var sw = Stopwatch.StartNew();
        var resp = await _llm.CompleteAsync(new LlmInvokeRequest
        {
            BaseUrl = ai.BaseUrl,
            ApiKey = ai.ApiKey,
            Model = ai.Model,
            Temperature = ai.Temperature,
            MaxTokens = ai.MaxTokens,
            Prompt = prompt,
            ForceJson = true,
            TimeoutSeconds = 600,
        });
        sw.Stop();

        await LogUsageAsync(ai, resp, stdShort);

        var raw = string.IsNullOrWhiteSpace(resp.Content) ? "" : StripFence(resp.Content);
        var jsonValid = IsJson(raw);
        var parsed = TryParseJson(raw);
        var extracted = 0;
        if (parsed is JsonObject po)
        {
            var arr = po["clauses"] as JsonArray ?? po["Clauses"] as JsonArray;
            extracted = arr?.Count ?? 0;
        }
        else if (parsed is JsonArray pa)
        {
            extracted = pa.Count;
        }

        return new
        {
            Success = resp.Success,
            Message = resp.Success ? "试跑完成（全文）" : resp.Message,
            Mode = PromptCodeLegacy,
            Model = ai.Model,
            DurationMs = sw.ElapsedMilliseconds,
            PromptTokens = resp.PromptTokens ?? 0,
            CompletionTokens = resp.CompletionTokens ?? 0,
            JsonValid = jsonValid,
            Output = raw,
            PromptChars = prompt.Length,
            ClauseCount = extracted > 0 ? extracted : clauseRefs.Count,
            AllClauseCount = clauseRows.Count,
            SkippedByDocCheckMode = skippedByMode,
            MarkdownLength = markdown.Length,
            Truncated = truncated,
            MarkdownHead = Head(markdown, 1_200),
            FileName = req.File.FileName,
            Note = filterNote,
        };
    }

    private static string RenderPrompt(
        string template, string stdName, string stdCode, string fileName,
        string clauseList, string documentContent)
        => template
            .Replace("{{standard_name}}", stdName, StringComparison.Ordinal)
            .Replace("{{standard_code}}", stdCode, StringComparison.Ordinal)
            .Replace("{{document_name}}", fileName, StringComparison.Ordinal)
            .Replace("{{clause_list}}", clauseList, StringComparison.Ordinal)
            .Replace("{{document_content}}", documentContent, StringComparison.Ordinal);

    private static bool IsJson(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return false;
        try { using var _ = JsonDocument.Parse(raw); return true; }
        catch (JsonException) { return false; }
    }

    private static JsonNode? TryParseJson(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        try { return JsonNode.Parse(raw); }
        catch (JsonException) { return null; }
    }

    /// <summary>从画像 JSON 取 <c>ExtractionMode</c>（PascalCase；兼容 camelCase）。</summary>
    private static string ReadExtractionMode(string profileJson)
    {
        if (TryParseJson(profileJson) is not JsonObject obj) return string.Empty;
        var node = obj["ExtractionMode"] ?? obj["extractionMode"];
        return (node?.ToString() ?? string.Empty).Trim();
    }

    /// <summary>AI 配置（口径与 PromptWorkbenchService / DocFillPromptController 一致）</summary>
    private async Task<AiCfg> ReadAiSettingsAsync()
    {
        var s = new AiCfg();
        var rows = await _db.Client.Queryable<SysConfig>()
            .Where(x => x.Category == "ai_model" && !x.IsDeleted)
            .Select(x => new { x.ConfigKey, x.ConfigValue })
            .ToListAsync();
        foreach (var row in rows)
        {
            switch (row.ConfigKey)
            {
                case "ai_api_key": s.ApiKey = row.ConfigValue ?? ""; break;
                case "ai_base_url": s.BaseUrl = row.ConfigValue ?? s.BaseUrl; break;
                case "ai_model_name": s.Model = row.ConfigValue ?? s.Model; break;
                case "ai_max_tokens":
                    if (int.TryParse(row.ConfigValue, out var mt)) s.MaxTokens = mt;
                    break;
                case "ai_temperature":
                    if (float.TryParse(row.ConfigValue, out var tp)) s.Temperature = tp;
                    break;
            }
        }
        return s;
    }

    /// <summary>计费留痕（失败不影响主流程）</summary>
    private async Task LogUsageAsync(AiCfg cfg, LlmInvokeResponse resp, string standardCode)
    {
        try
        {
            var log = new AiUsageLog
            {
                CallId = Guid.NewGuid().ToString("N"),
                Code = Guid.NewGuid().ToString("N"),
                BusinessType = "doc_extract_test",
                BusinessRef = "doc_extract_prompt_test:" + (standardCode ?? ""),
                Skill = "doc_extract_prompt_test",
                Provider = string.IsNullOrWhiteSpace(cfg.Provider) ? "qianwen" : cfg.Provider,
                Model = cfg.Model,
                PromptTokens = resp.PromptTokens ?? 0,
                CompletionTokens = resp.CompletionTokens ?? 0,
                TotalTokens = (resp.PromptTokens ?? 0) + (resp.CompletionTokens ?? 0),
                DurationMs = resp.DurationMs,
                Success = resp.Success,
                ErrorMessage = resp.Success ? null : (resp.Message ?? ""),
                CreateTime = DateTime.Now,
            };
            await _db.Client.Insertable(log).ExecuteCommandAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[DocExtractRule] 试跑计费落库失败（不影响结果）");
        }
    }

    /// <summary>去 ```json 围栏</summary>
    private static string StripFence(string text)
    {
        var t = (text ?? "").Trim();
        if (!t.StartsWith("```", StringComparison.Ordinal)) return t;
        var nl = t.IndexOf('\n');
        if (nl < 0) return t;
        t = t.Substring(nl + 1);
        var end = t.LastIndexOf("```", StringComparison.Ordinal);
        return (end >= 0 ? t.Substring(0, end) : t).Trim();
    }

    private sealed class AiCfg
    {
        public string ApiKey { get; set; } = "";
        public string BaseUrl { get; set; } = "https://dashscope.aliyuncs.com/compatible-mode/v1";
        public string Model { get; set; } = "qwen-flash";
        public string Provider { get; set; } = "qianwen";
        public int MaxTokens { get; set; } = 32768;
        public float Temperature { get; set; } = 0.2f;
    }

    private sealed class StdRow
    {
        public string Code { get; set; } = "";
        public string StandardCode { get; set; } = "";
        public string StandardName { get; set; } = "";
    }

    private sealed class ClauseRow
    {
        public string? Code { get; set; }
        public string? ClauseNumber { get; set; }
        public string? Title { get; set; }
        public int Sort { get; set; }
    }

    private sealed class ClauseAttrRow
    {
        public string? ClauseCode { get; set; }
        public string? AttrValue { get; set; }
    }

}

// ════════════════════════════════════════════════════════════════════════
// 契约 DTO
// ════════════════════════════════════════════════════════════════════════

/// <summary>文档提取提示词读取结果</summary>
public sealed class DocExtractPromptDto
{
    public string PromptCode { get; set; } = "doc_extract";
    public string? StandardCode { get; set; }
    public string Template { get; set; } = string.Empty;
    public bool IsFallback { get; set; }
    // PlaceholderDescriptor 是 Services.Cert 命名空间级 record（非 PromptTemplateService 嵌套类型）
    public IReadOnlyList<PlaceholderDescriptor> Placeholders { get; set; } = Array.Empty<PlaceholderDescriptor>();
}

/// <summary>文档提取提示词保存入参</summary>
public sealed class DocExtractPromptSaveRequest
{
    /// <summary>提示词代码（默认 doc_clause_extract；类型提示词传 doc_extract_xxx）</summary>
    public string? PromptCode { get; set; }
    public string Template { get; set; } = string.Empty;
    public string? StandardCode { get; set; }
}

/// <summary>可选提示词类型（页面下拉数据源，来源 = DocExtractionPromptCatalog）</summary>
public sealed class DocExtractPromptTypeDto
{
    public string PromptCode { get; set; } = string.Empty;
    /// <summary>对应的画像 ExtractionMode（画像总闸为空串）</summary>
    public string Mode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public int Sort { get; set; }
}

/// <summary>页面内试跑入参（multipart）</summary>
public sealed class DocExtractPromptTestRequest
{
    /// <summary>被测试的文档</summary>
    public IFormFile? File { get; set; }
    /// <summary>标准短码（iso9001 / iso14001 / iso45001 / iso13485）</summary>
    public string? StandardCode { get; set; }
    /// <summary>页面当前正文（未保存也能试）</summary>
    public string? Template { get; set; }
    /// <summary>
    /// 提示词代码（决定占位符渲染家族）：
    /// <c>doc_profile</c>（画像总闸）/ <c>doc_extract_xxx</c>（按类型提取）/ <c>doc_clause_extract</c>（兜底）。
    /// 空 = 兜底 doc_clause_extract。
    /// </summary>
    public string? PromptCode { get; set; }
}

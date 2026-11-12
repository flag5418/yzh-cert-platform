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
        ILogger<DocExtractRuleController> logger)
        : base(entityService, userContext)
    {
        _promptTemplate = promptTemplate;
        _convert = convert;
        _llm = llm;
        _db = db;
        _logger = logger;
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
    public async Task<IActionResult> GetPrompt([FromQuery] string? standardCode = null)
    {
        var template = await _promptTemplate.GetAsync("doc_clause_extract", null, standardCode);
        return Ok(ApiResponse<DocExtractPromptDto>.Ok(new DocExtractPromptDto
        {
            PromptCode = "doc_clause_extract",
            StandardCode = standardCode,
            Template = template.Template,
            IsFallback = template.Status == "fallback",
            Placeholders = PromptTemplateService.GetPlaceholders("doc_clause_extract"),
        }));
    }

    /// <summary>
    /// 保存某标准的文档提取提示词（幂等 upsert）。
    /// <para>standardCode 空 = 保存全局默认；非空 = 保存该标准的专属提示词。</para>
    /// </summary>
    [HttpPost("prompt")]
    public async Task<IActionResult> SavePrompt([FromBody] DocExtractPromptSaveRequest request)
    {
        var (ok, message) = await _promptTemplate.SaveAsync(
            "doc_clause_extract",
            request.Template,
            null,
            request.StandardCode,
            "文档提取提示词",
            string.IsNullOrEmpty(request.StandardCode)
                ? "文档提取提示词的全局默认模板"
                : $"标准【{request.StandardCode}】的专属提取提示词",
            UserContext.UserCode);

        return ok
            ? Ok(ApiResponse.Ok(message))
            : Ok(ApiResponse.BizFail(message));
    }

    // ════════════════════════════════════════════════════════════════════
    // ★ 页面内试跑（2026-11-10）：上传文档 → 转 Markdown → 用「当前编辑正文」跑一次
    // ════════════════════════════════════════════════════════════════════

    /// <summary>单次试跑送模型的文档字符上限（整篇模式，与 ClauseExtractExecutor.ChunkChars 同口径 3 万字）</summary>
    private const int MaxTestDocChars = 30_000;

    /// <summary>小节模式：单节送模型的字符上限（与 Auditor ProbeSectionAsync 同口径）</summary>
    private const int SectionMaxChars = 6_000;

    /// <summary>小节模式：一次调用最多处理多少条条款（越小输出越短、并发越高）</summary>
    private const int SectionClausesPerCall = 6;

    /// <summary>小节模式：并发路数（条款属性页 5 路无 429；试跑短输出用 8 路）</summary>
    private const int SectionConcurrency = 8;

    /// <summary>小节模式：单次输出上限（不要用全局 32768，否则又会写飞）</summary>
    private const int SectionMaxTokens = 4_096;

    /// <summary>小节模式：相邻小节合并后单次送模型的字符上限（6 个短小节 ≈ 8~10k）</summary>
    private const int SectionBatchMaxChars = 10_000;

    /// <summary>
    /// <b>试跑文档提取提示词</b>（不落库、不写队列）。
    ///
    /// <para>入参 = 文件 + 标准短码 + <b>页面当前正文</b>（未保存也能试，实现边改边看）。</para>
    /// <para>链路与线上同源：<c>IFileConvertCore.ConvertToMarkdownInlineAsync</c>（anydoc → OCR 兜底），
    /// 不是裸 anydoc —— 否则扫描件在测试环境永远测不出真实结果。</para>
    ///
    /// <para>★ 2026-11-12 增加 <c>Mode</c>：
    /// <c>Section</c>（默认）= 章节索引 + 按小节聚合 + 5 路并发；
    /// <c>Whole</c> = 旧的整篇一次大 JSON，仅用于对比。</para>
    /// </summary>
    [HttpPost("prompt/test")]
    [RequestSizeLimit(200_000_000)]
    public async Task<IActionResult> TestPrompt([FromForm] DocExtractPromptTestRequest req)
    {
        if (req == null || req.File == null || req.File.Length == 0)
            return Ok(ApiResponse.Fail("请先选择要测试的文档"));
        if (string.IsNullOrWhiteSpace(req.Template))
            return Ok(ApiResponse.Fail("提示词正文为空：请先在编辑器里填写提示词再试跑"));

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
            + "WHERE (StandardCode = @key OR Code = @key) AND IsDeleted = 0 AND IsValid = 1 LIMIT 1",
            new { key = (req.StandardCode ?? "").Trim() });
        var std = stdList?.FirstOrDefault();
        var stdName = std?.StandardName ?? (req.StandardCode ?? "未知标准");
        var stdGuid = std?.Code ?? "";
        var stdShort = std?.StandardCode ?? (req.StandardCode ?? "");

        // 3) 条款清单（编号 + 标题；小节模式还需要 Code/Sort 做章节映射）
        var clauseRows = string.IsNullOrWhiteSpace(stdGuid)
            ? new List<ClauseRow>()
            : await _db.Client.Ado.SqlQueryAsync<ClauseRow>(
                "SELECT Code, ClauseNumber, Title, Sort FROM cert_iso_clause "
                + "WHERE StandardCode = @guid AND IsValid = 1 AND IsDeleted = 0 "
                + "GROUP BY Code, ClauseNumber, Title, Sort ORDER BY MIN(Sort)",
                new { guid = stdGuid });

        // 4) DocCheckMode≠none 过滤：与线上 ClauseExtractExecutor 同口径
        var (clauseRefs, skippedByMode, filterNote) = await ResolveClausesAsync(
            clauseRows ?? new List<ClauseRow>(), stdShort);

        // 5) 两种模式
        var mode = string.Equals(req.Mode, "Whole", StringComparison.OrdinalIgnoreCase)
            ? "Whole"
            : "Section";

        object result = mode == "Whole"
            ? await RunWholeDocumentAsync(req, markdown, stdName, stdShort, stdGuid,
                clauseRows ?? new List<ClauseRow>(), clauseRefs, skippedByMode, filterNote)
            : await RunSectionAsync(req, markdown, stdName, stdShort, stdGuid,
                clauseRefs, clauseRows?.Count ?? 0, skippedByMode, filterNote);

        return Ok(ApiResponse<object>.Ok(result));
    }

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

        if (attrs == null || attrs.Count == 0)
            return (all, 0, $"标准【{standardShort}】的条款还没配 DocCheckMode，本次按全部条款试跑");

        var modeByClause = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var a in attrs)
        {
            if (string.IsNullOrWhiteSpace(a.ClauseCode)) continue;
            modeByClause[a.ClauseCode!] = (a.AttrValue ?? "").Trim().ToLowerInvariant();
        }

        // 未配 DocCheckMode 的条款按「要查」处理，只剔除明确 none 的
        var kept = all
            .Where(c => !modeByClause.TryGetValue(c.Code, out var mode) || mode != "none")
            .ToList();
        return (kept, all.Count - kept.Count, null);
    }

    /// <summary>旧整篇模式：整篇 30k + 全部条款 + 一次大 JSON。仅用于对比。</summary>
    private async Task<object> RunWholeDocumentAsync(
        DocExtractPromptTestRequest req,
        string markdown,
        string stdName,
        string stdShort,
        string stdGuid,
        List<ClauseRow> clauseRows,
        List<DocClauseRef> clauseRefs,
        int skippedByMode,
        string? filterNote)
    {
        var truncated = markdown.Length > MaxTestDocChars;
        var docForModel = truncated ? markdown.Substring(0, MaxTestDocChars) : markdown;

        var clauseList = string.Join("\n", clauseRefs
            .Select(c => "- " + c.ClauseNumber + (string.IsNullOrWhiteSpace(c.Title) ? "" : " " + c.Title)));

        var prompt = RenderPrompt(req.Template, stdName, stdShort, req.File!.FileName ?? "",
            clauseList, docForModel);

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
            Message = resp.Success ? "试跑完成（整篇模式）" : resp.Message,
            Mode = "Whole",
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
            MarkdownHead = markdown.Length > 1200 ? markdown.Substring(0, 1200) : markdown,
            FileName = req.File.FileName,
            SectionTotal = 1,
            SectionSucceeded = resp.Success ? 1 : 0,
            SectionFailed = resp.Success ? 0 : 1,
            SectionTruncated = 0,
            Note = filterNote,
        };
    }

    /// <summary>
    /// 把相邻小节合并成「最多 8 条条款 + 最多 1.2 万字」一次调用。
    /// <para>原实现是「一个小节一次调用」，79 个小节 = 79 次请求；合并后通常 10~20 次，
    /// 输出仍是每条几百 token 的小 JSON，质量口径不变。</para>
    /// </summary>
    private static List<SectionCall> BuildSectionCalls(
        List<(int Start, int End, string Text, string Title, string Path, List<DocClauseRef> Clauses)> sections)
    {
        var calls = new List<SectionCall>();
        SectionCall? current = null;
        var seenClauseNos = new HashSet<string>(StringComparer.Ordinal);

        void Flush()
        {
            if (current == null) return;
            if (current.Clauses.Count > 0 && current.Text.Length > 0) calls.Add(current);
            current = null;
        }

        foreach (var g in sections)
        {
            var clauses = g.Clauses
                .Where(c => !string.IsNullOrWhiteSpace(c.ClauseNumber) && seenClauseNos.Add(c.ClauseNumber))
                .ToList();
            if (clauses.Count == 0) continue;

            var truncated = g.Text.Length > SectionMaxChars;
            var text = truncated ? g.Text.Substring(0, SectionMaxChars) : g.Text;

            // 当前组已满（条款数或字符数）→ 先落一组
            if (current != null
                && (current.Clauses.Count + clauses.Count > SectionClausesPerCall
                    || current.Text.Length + text.Length > SectionBatchMaxChars))
            {
                Flush();
            }

            current ??= new SectionCall { Title = g.Title, Path = g.Path };
            if (current.Text.Length > 0) current.Text += "\n\n";
            current.Text += "===== 小节：" + g.Title + " =====\n" + text;
            current.Path = string.IsNullOrWhiteSpace(current.Path) ? g.Path : current.Path;
            current.Clauses.AddRange(clauses);
            current.Truncated |= truncated;

            if (current.Clauses.Count >= SectionClausesPerCall) Flush();
        }

        Flush();
        return calls;
    }

    /// <summary>
    /// 小节模式：章节索引 → 按小节聚合条款 → 每 8 条一组 → 5 路并发。
    /// <para>输入从 27k 降到 6k/节，输出从 10k tokens 降到几百~1k/节。</para>
    /// </summary>
    private async Task<object> RunSectionAsync(
        DocExtractPromptTestRequest req,
        string markdown,
        string stdName,
        string stdShort,
        string stdGuid,
        List<DocClauseRef> clauseRefs,
        int allClauseCount,
        int skippedByMode,
        string? filterNote)
    {
        var sw = Stopwatch.StartNew();
        var map = DocSectionIndex.Build(markdown, clauseRefs, stdShort, stdName);

        var sections = map.Sections
            .Where(s => s.CoverageMode != "missing" && s.CharStart >= 0 && s.CharEnd > s.CharStart)
            .GroupBy(s => (s.CharStart, s.CharEnd))
            .Select(g =>
            {
                var start = g.Key.CharStart;
                var end = Math.Min(g.Key.CharEnd, markdown.Length);
                var text = start < end ? markdown.Substring(start, end - start) : "";
                var title = g.First().HeadingTitle;
                var path = g.First().HeadingPath;
                var clauses = g
                    .Select(x => new DocClauseRef(x.ClauseCode, x.ClauseNo, x.ClauseTitle, 0))
                    .GroupBy(x => x.ClauseNumber)
                    .Select(x => x.First())
                    .ToList();
                return (Start: start, End: end, Text: text, Title: title, Path: path, Clauses: clauses);
            })
            .Where(g => g.Text.Length > 0 && g.Clauses.Count > 0)
            .OrderBy(g => g.Start)
            .ToList();

        var calls = BuildSectionCalls(sections);

        if (calls.Count == 0)
        {
            sw.Stop();
            return new
            {
                Success = false,
                Message = "没有可定位的小节：文档标题/条款编号与标准清单对不上，建议切回整篇模式对比",
                Mode = "Section",
                Model = "",
                DurationMs = sw.ElapsedMilliseconds,
                PromptTokens = 0,
                CompletionTokens = 0,
                JsonValid = true,
                Output = "{\"mode\":\"Section\",\"sections\":[],\"clauses\":[]}",
                PromptChars = 0,
                ClauseCount = clauseRefs.Count,
                AllClauseCount = allClauseCount,
                SkippedByDocCheckMode = skippedByMode,
                MarkdownLength = markdown.Length,
                Truncated = false,
                MarkdownHead = markdown.Length > 1200 ? markdown.Substring(0, 1200) : markdown,
                FileName = req.File!.FileName,
                SectionTotal = 0,
                SectionSucceeded = 0,
                SectionFailed = 0,
                SectionTruncated = 0,
                MissingSections = map.MissingCount,
                Note = filterNote,
            };
        }

        var ai = await ReadAiSettingsAsync();
        var maxTokens = Math.Min(ai.MaxTokens <= 0 ? SectionMaxTokens : ai.MaxTokens, SectionMaxTokens);
        var gate = new SemaphoreSlim(SectionConcurrency, SectionConcurrency);
        var results = new SectionCallResult[calls.Count];

        await Task.WhenAll(calls.Select(async (call, index) =>
        {
            await gate.WaitAsync();
            try
            {
                var clauseList = string.Join("\n", call.Clauses
                    .Select(c => "- " + c.ClauseNumber + (string.IsNullOrWhiteSpace(c.Title) ? "" : " " + c.Title)));
                var prompt = RenderPrompt(req.Template, stdName, stdShort, req.File!.FileName ?? "",
                    clauseList, call.Text);
                var resp = await _llm.CompleteAsync(new LlmInvokeRequest
                {
                    BaseUrl = ai.BaseUrl,
                    ApiKey = ai.ApiKey,
                    Model = ai.Model,
                    Temperature = 0.1f,
                    MaxTokens = maxTokens,
                    Prompt = prompt,
                    ForceJson = true,
                    TimeoutSeconds = 300,
                });
                results[index] = new SectionCallResult
                {
                    Call = call,
                    Prompt = prompt,
                    Response = resp,
                };
            }
            catch (Exception ex)
            {
                results[index] = new SectionCallResult
                {
                    Call = call,
                    Prompt = "",
                    Response = new LlmInvokeResponse { Success = false, Message = ex.Message },
                    Exception = ex.Message,
                };
            }
            finally
            {
                gate.Release();
            }
        }));
        gate.Dispose();
        sw.Stop();

        // 计费落库：SqlSugar 客户端非线程安全，统一在并发结束后串行写
        foreach (var r in results)
        {
            if (r?.Response != null && !string.IsNullOrWhiteSpace(r.Prompt))
                await LogUsageAsync(ai, r.Response, stdShort);
        }

        var mergedClauses = new JsonArray();
        var clauseMap = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        var knownClauseNos = clauseRefs
            .Select(c => DocSectionIndex.NormalizeClauseNo(c.ClauseNumber))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.Ordinal);
        var sectionsArray = new JsonArray();
        var succeeded = 0;
        var failed = 0;
        long promptTokens = 0;
        long completionTokens = 0;
        var promptChars = 0;
        var sectionTruncated = 0;

        for (var i = 0; i < results.Length; i++)
        {
            var r = results[i];
            if (r == null) { failed++; continue; }
            var resp = r.Response!;
            if (resp.Success) succeeded++; else failed++;
            promptTokens += resp.PromptTokens ?? 0;
            completionTokens += resp.CompletionTokens ?? 0;
            promptChars += r.Prompt.Length;
            if (r.Call.Truncated) sectionTruncated++;

            var raw = string.IsNullOrWhiteSpace(resp.Content) ? "" : StripFence(resp.Content);
            var parsed = TryParseJson(raw);
            if (parsed != null) MergeSectionClauses(parsed, mergedClauses, clauseMap, knownClauseNos);

            var clauseNumbers = new JsonArray();
            foreach (var c in r.Call.Clauses)
                clauseNumbers.Add(JsonValue.Create(c.ClauseNumber));

            sectionsArray.Add(new JsonObject
            {
                ["Index"] = i + 1,
                ["SectionTitle"] = r.Call.Title,
                ["SectionPath"] = r.Call.Path,
                ["ClauseNumbers"] = clauseNumbers,
                ["Truncated"] = r.Call.Truncated,
                ["Success"] = resp.Success,
                ["Message"] = resp.Message ?? "",
                ["DurationMs"] = resp.DurationMs,
                ["PromptChars"] = r.Prompt.Length,
                ["PromptTokens"] = resp.PromptTokens ?? 0,
                ["CompletionTokens"] = resp.CompletionTokens ?? 0,
                ["Output"] = parsed ?? JsonValue.Create(raw),
            });
        }

        var root = new JsonObject
        {
            ["mode"] = "Section",
            ["sections"] = sectionsArray,
            ["clauses"] = mergedClauses,
            ["metrics"] = new JsonObject
            {
                ["SectionTotal"] = calls.Count,
                ["SectionSucceeded"] = succeeded,
                ["SectionFailed"] = failed,
                ["PromptTokens"] = promptTokens,
                ["CompletionTokens"] = completionTokens,
                ["PromptChars"] = promptChars,
                ["DurationMs"] = sw.ElapsedMilliseconds,
                ["ClauseCount"] = clauseRefs.Count,
                ["AllClauseCount"] = allClauseCount,
                ["SkippedByDocCheckMode"] = skippedByMode,
                ["MissingSections"] = map.MissingCount,
                ["SectionTruncated"] = sectionTruncated,
            },
        };

        var message = failed == 0
            ? $"试跑完成（小节模式：{calls.Count}/{calls.Count} 段）"
            : succeeded > 0
                ? $"试跑完成，部分小节失败：{succeeded}/{calls.Count} 段成功"
                : "试跑失败：所有小节调用失败";

        return new
        {
            Success = succeeded > 0,
            Message = message,
            Mode = "Section",
            Model = ai.Model,
            DurationMs = sw.ElapsedMilliseconds,
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            JsonValid = true,
            Output = root.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = false,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            }),
            PromptChars = promptChars,
            ClauseCount = mergedClauses.Count,
            AllClauseCount = allClauseCount,
            SkippedByDocCheckMode = skippedByMode,
            MarkdownLength = markdown.Length,
            Truncated = false,
            MarkdownHead = markdown.Length > 1200 ? markdown.Substring(0, 1200) : markdown,
            FileName = req.File!.FileName,
            SectionTotal = calls.Count,
            SectionSucceeded = succeeded,
            SectionFailed = failed,
            SectionTruncated = sectionTruncated,
            MissingSections = map.MissingCount,
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

    /// <summary>把每段输出里的 clauses 合并到顶层；同编号合并 Excerpts，保留最可信的 Anchor。</summary>
    private static void MergeSectionClauses(
        JsonNode parsed, JsonArray merged, Dictionary<string, JsonObject> byClause,
        HashSet<string> knownClauseNos)
    {
        IEnumerable<JsonNode?> items;
        if (parsed is JsonObject obj)
        {
            var arr = obj["clauses"] as JsonArray ?? obj["Clauses"] as JsonArray;
            if (arr == null) return;
            items = arr.ToList();
        }
        else if (parsed is JsonArray arr)
        {
            items = arr.ToList();
        }
        else return;

        foreach (var item in items)
        {
            if (item is not JsonObject clause) continue;
            var rawNo = clause["ClauseNumber"]?.GetValue<string>()
                        ?? clause["clauseNumber"]?.GetValue<string>() ?? "";
            var no = CanonicalClauseNo(rawNo, knownClauseNos);
            if (string.IsNullOrWhiteSpace(no)) continue;
            // 条款清单外的编号直接丢弃（防止模型把目录行/章节标题当条款）
            if (knownClauseNos.Count > 0 && !knownClauseNos.Contains(no)) continue;

            clause["ClauseNumber"] = no;

            if (byClause.TryGetValue(no, out var existing))
            {
                MergeExcerpts(existing, clause);
                continue;
            }

            var clone = (JsonObject)clause.DeepClone();
            byClause[no] = clone;
            merged.Add(clone);
        }
    }

    /// <summary>
    /// 模型常把标题一起写进 ClauseNumber（“9.1.2 顾客满意”“9.2.1 组织应…”），
    /// 先抽前导编号，再按标准清单做最长前缀匹配。
    /// </summary>
    private static string CanonicalClauseNo(string raw, HashSet<string> known)
    {
        var s = DocSectionIndex.NormalizeClauseNo(raw);
        if (string.IsNullOrWhiteSpace(s)) return "";
        if (known.Contains(s)) return s;

        var m = Regex.Match(s, @"^([0-9]+(?:\.[0-9]+)*)");
        if (m.Success)
        {
            var no = DocSectionIndex.NormalizeClauseNo(m.Groups[1].Value);
            if (known.Contains(no)) return no;
        }

        var hit = known
            .Where(k => s.StartsWith(k + " ", StringComparison.Ordinal)
                        || s.StartsWith(k + "\t", StringComparison.Ordinal)
                        || s.StartsWith(k + "、", StringComparison.Ordinal)
                        || s.StartsWith(k + "：", StringComparison.Ordinal))
            .OrderByDescending(k => k.Length)
            .FirstOrDefault();
        return hit ?? s;
    }

    private static void MergeExcerpts(JsonObject target, JsonObject source)
    {
        var dst = target["Excerpts"] as JsonArray;
        var src = source["Excerpts"] as JsonArray;
        if (dst != null && src != null)
        {
            var seen = dst.Select(x => x?.GetValue<string>() ?? "").ToHashSet(StringComparer.Ordinal);
            foreach (var e in src)
            {
                var text = e?.GetValue<string>() ?? "";
                if (!string.IsNullOrWhiteSpace(text) && seen.Add(text))
                    dst.Add(JsonValue.Create(text));
            }
        }

        var targetConf = target["Confidence"]?.GetValue<double>() ?? 0;
        var sourceConf = source["Confidence"]?.GetValue<double>() ?? 0;
        if (sourceConf > targetConf)
            target["Confidence"] = sourceConf;

        if (string.IsNullOrWhiteSpace(target["Anchor"]?.GetValue<string>()))
            target["Anchor"] = source["Anchor"]?.GetValue<string>();
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

    private sealed class SectionCall
    {
        public string Title { get; set; } = "";
        public string Path { get; set; } = "";
        public string Text { get; set; } = "";
        public List<DocClauseRef> Clauses { get; set; } = new();
        public bool Truncated { get; set; }
    }

    private sealed class SectionCallResult
    {
        public SectionCall Call { get; set; } = null!;
        public string Prompt { get; set; } = "";
        public LlmInvokeResponse Response { get; set; } = null!;
        public string? Exception { get; set; }
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
    public string Template { get; set; } = string.Empty;
    public string? StandardCode { get; set; }
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
    /// <summary>试跑模式：Section（默认，章节索引+小节并发）/ Whole（整篇一次大 JSON，仅对比）</summary>
    public string? Mode { get; set; }
}

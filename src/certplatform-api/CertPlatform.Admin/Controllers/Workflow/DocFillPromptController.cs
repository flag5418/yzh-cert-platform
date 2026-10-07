using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using YZH.Core.Api.Controllers;
using YZH.Core.Api.Services;
using YZH.Core.Api.Models.System;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Helpers;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Config;
using YZH.Core.Stand.Models.Result;
using CertPlatform.Admin.Entities.Doc;
using CertPlatform.Shared.DocExtraction;

namespace CertPlatform.Admin.Controllers.Workflow;

/// <summary>
/// 全文填写提示词控制器（「标准文档填写规则」页面的「全文规则」Tab）
///
/// <para><b>路由前缀</b>：<c>/api/Admin/Workflow/DocFillPrompt</c></para>
/// <para><b>数据库</b>：<c>cert_doc_fill_prompt</c></para>
///
/// <para><b>业务定位</b>：与 <c>DocTemplateAnchor</c>（逐锚点规则）<b>并列的第二条填写通路</b>。
/// 锚点规则管「这个格子填什么」；本表管「整篇文档怎么组织、口吻怎么写」。
/// <c>cert_doc_template.FillPromptCode</c> 引用本表 <c>PromptCode</c>（空 = 不走全文规则）。</para>
///
/// <para><b>★ 版本语义（不覆盖）</b>：<c>uk_org_prompt_ver(OrgCode, PromptCode, Version)</c> ⇒
/// 改提示词<b>不更新旧行，而是新增一版</b>（<c>Version = max + 1</c>），留痕以支撑
/// 「当时为什么这么写」的追溯。所以：</para>
/// <list type="bullet">
/// <item><c>POST add</c> = 新增一版（<c>Version</c> 未显式给出时自动 +1）</item>
/// <item><c>POST update</c> = 改本版内容（<c>Version</c> 不变）</item>
/// </list>
///
/// <para><b>★ <c>IsDefault</c> 排他</b>：同一 <c>(OrgCode, PromptCode)</c> 下只能有一条为 1。
/// DB 只有普通索引 <c>idx_default</c>（⛔ 不是唯一索引）⇒ <b>由本控制器保证</b>：
/// 置 1 前先把同 Code 的其它行清零。</para>
/// </summary>
[ApiController]
[Route("api/Admin/Workflow/DocFillPrompt")]
public class DocFillPromptController : YzhControllerBase<DocFillPrompt>
{
    private readonly IDbOrm _db;
    private readonly LlmInvokeService _llm;
    private readonly ILogger<DocFillPromptController> _logger;

    public DocFillPromptController(
        EntityService<DocFillPrompt> entityService,
        IUserContext userContext,
        IDbOrm db,
        LlmInvokeService llm,
        ILogger<DocFillPromptController> logger)
        : base(entityService, userContext)
    {
        _db = db;
        _llm = llm;
        _logger = logger;
    }

    /// <summary>★ 开发期强制暴露缺配置问题（缺 JSON 直接 throw，不静默空白）</summary>
    protected override bool StrictConfigLoad => true;

    /// <summary>加载 EntityConfig 配置</summary>
    protected override EntityConfig LoadConfig()
    {
        return EntityConfigHelper.GetConfig<DocFillPrompt>();
    }

    // ════════════════════════════════════════════════════════════════════
    // 一、写入覆写（版本自增 + IsDefault 排他 + 含已删查重）
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 新增一版：<c>Version &lt;= 0</c> 时自动取 <c>max(Version) + 1</c>；
    /// <c>IsDefault = true</c> 时先清掉同 <c>(OrgCode, PromptCode)</c> 其它行的默认位。
    /// </summary>
    public override async Task<Result<DocFillPrompt>> AddCore(DocFillPrompt entity)
    {
        Normalize(entity);

        // ★ 版本自增必须早于 Validate（Validate 要求 Version > 0，而「未指定」时它是 0）
        var siblings = await LoadSiblingsAsync(entity.OrgCode, entity.PromptCode);
        if (entity.Version <= 0)
            entity.Version = siblings.Count == 0 ? 1 : siblings.Max(s => s.Version) + 1;

        var err = Validate(entity);
        if (err != null) return Result<DocFillPrompt>.Fail(err);

        // uk_org_prompt_ver 不含 IsDeleted ⇒ 含已删查重（同版本号已存在 ⇒ 就地复活）
        var sameVersion = siblings.FirstOrDefault(s => s.Version == entity.Version);
        if (sameVersion != null && !sameVersion.IsDeleted)
            return Result<DocFillPrompt>.Fail(
                $"「{entity.PromptCode}」已存在版本 {entity.Version}（{sameVersion.PromptName}）。"
                + "若要改内容请编辑该版本，若要出新版请留空版本号由系统自增。");

        if (sameVersion != null)
        {
            // 复活已删的同版本行（沿用同 Code，避免产生第二行撞唯一键）
            entity.Code = sameVersion.Code;
            entity.Id = sameVersion.Id;
            entity.CreateTime = sameVersion.CreateTime;
            entity.CreateBy = sameVersion.CreateBy;
            entity.IsDeleted = false;
            entity.DeleteBy = null;
            entity.DeleteTime = null;
            entity.UpdateTime = DateTime.Now;

            var upd = await _db.UpdateAsync(entity);
            if (!upd.Success) return Result<DocFillPrompt>.Fail(upd.Error ?? "复活提示词版本失败");

            _logger.LogInformation("[DocFillPrompt] 复活已软删版本：{PromptCode} v{Ver}", entity.PromptCode, entity.Version);

            // ★ 排他放在写成功之后 —— 顺序反了会在写失败时留下「一个默认都没有」的不一致态
            if (entity.IsDefault)
                await ClearDefaultAsync(entity.OrgCode, entity.PromptCode, exceptCode: entity.Code);

            return Result<DocFillPrompt>.Ok(entity);
        }

        var added = await base.AddCore(entity);
        if (!added.Success) return added;

        if (entity.IsDefault)
            await ClearDefaultAsync(entity.OrgCode, entity.PromptCode, exceptCode: added.Data?.Code);

        return added;
    }

    /// <summary>修改本版内容（<c>Version</c> 不变；改 <c>IsDefault</c> 时同步排他）</summary>
    public override async Task<Result<DocFillPrompt>> UpdateCore(DocFillPrompt entity)
    {
        if (string.IsNullOrWhiteSpace(entity.Code))
            return Result<DocFillPrompt>.Fail("更新失败：缺少业务键 Code");

        var current = await _db.GetOneAsync<DocFillPrompt>(p => p.Code == entity.Code);
        if (current.Data == null)
            return Result<DocFillPrompt>.Fail("该提示词版本不存在或已删除");

        Normalize(entity);

        // ★ 版本号不可改（它是唯一键成员，改了等于换行；要出新版请用「新增」）
        entity.Version = current.Data.Version;

        var err = Validate(entity);
        if (err != null) return Result<DocFillPrompt>.Fail(err);

        var updated = await base.UpdateCore(entity);
        if (!updated.Success) return updated;

        // ★ 排他放在写成功之后（同 AddCore）
        if (entity.IsDefault)
            await ClearDefaultAsync(entity.OrgCode, entity.PromptCode, exceptCode: entity.Code);

        return updated;
    }

    // ════════════════════════════════════════════════════════════════════
    // 二、自定义端点
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>取生效提示词</b>：给定 <c>PromptCode</c> + <c>OrgCode</c>，返回填充引擎<b>实际会用</b>的那一版。
    /// <para><b>选取口径（全项目唯一实现，⛔ 别处不得另写一份）</b>：
    /// ① 机构专属（<c>OrgCode</c> 命中）优先于全局（<c>OrgCode = ''</c>）；
    /// ② 同层级内 <c>IsDefault = 1</c> 优先；
    /// ③ 再取 <c>Version</c> 最大者。</para>
    /// <para>返回 <c>Found=false</c> 时，填充引擎应按「不走全文规则」处理（不报错）。</para>
    /// </summary>
    [HttpGet("resolve")]
    public async Task<IActionResult> Resolve([FromQuery] string promptCode, [FromQuery] string? orgCode)
    {
        if (string.IsNullOrWhiteSpace(promptCode))
            return Ok(ApiResponse<object>.Fail("请指定 PromptCode"));

        var org = (orgCode ?? string.Empty).Trim();

        var candidates = await _db.Client.Queryable<DocFillPrompt>()
            .Where(p => p.PromptCode == promptCode
                        && (p.OrgCode == string.Empty || p.OrgCode == org)
                        && p.IsDeleted == false
                        && p.IsValid == 1)
            .ToListAsync();

        if (candidates.Count == 0)
            return Ok(ApiResponse<object>.Ok(new { Found = false, PromptCode = promptCode, OrgCode = org }));

        var picked = candidates
            .OrderByDescending(p => org.Length > 0 && p.OrgCode == org)   // ① 更具体优先
            .ThenByDescending(p => p.IsDefault)                          // ② 默认优先
            .ThenByDescending(p => p.Version)                            // ③ 版本新优先
            .First();

        return Ok(ApiResponse<object>.Ok(new
        {
            Found = true,
            PromptCode = promptCode,
            OrgCode = org,
            Picked = new
            {
                picked.Code,
                picked.OrgCode,
                picked.PromptCode,
                picked.PromptName,
                picked.SystemPrompt,
                picked.UserTemplate,
                picked.OutputSchema,
                picked.Model,
                picked.Temperature,
                picked.MaxTokens,
                picked.Version,
                picked.IsDefault,
                picked.Status,
            },
            CandidateCount = candidates.Count,
            Candidates = candidates
                .OrderByDescending(p => p.Version)
                .Select(p => new { p.Code, p.OrgCode, p.Version, p.IsDefault, p.Status }),
        }));
    }

    /// <summary><b>版本历史</b>：同一 <c>PromptCode</c> 的全部版本（含已软删），按版本降序。</summary>
    /// <remarks>
    /// <b>★ 可见范围与 <c>resolve</c> 逐字一致</b>（<c>OrgCode = ''</c> 或命中查询机构）——
    /// 前端「编辑生效版本」先 resolve 拿 <c>Picked.Code</c>，再回本端点取**整行**提交 update；
    /// 两处范围不一致时会出现「resolve 选中的行在 versions 里查不到」⇒ 编辑无从保存。
    /// </remarks>
    [HttpGet("versions")]
    public async Task<IActionResult> Versions([FromQuery] string promptCode, [FromQuery] string? orgCode)
    {
        if (string.IsNullOrWhiteSpace(promptCode))
            return Ok(ApiResponse<object>.Fail("请指定 PromptCode"));

        var org = (orgCode ?? string.Empty).Trim();

        var rows = await _db.Client.Queryable<DocFillPrompt>()
            .Where(p => p.PromptCode == promptCode
                        && (p.OrgCode == string.Empty || p.OrgCode == org))
            .OrderByDescending(p => p.Version)
            .Select(p => new
            {
                p.Code,
                p.OrgCode,
                p.PromptCode,
                p.PromptName,
                p.SystemPrompt,
                p.UserTemplate,
                p.OutputSchema,
                p.Model,
                p.Temperature,
                p.MaxTokens,
                p.Version,
                p.IsDefault,
                p.Status,
                p.Sort,
                p.IsValid,
                p.IsDeleted,
                p.Remark,
                p.UpdateTime,
            })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(new
        {
            PromptCode = promptCode,
            OrgCode = org,
            Total = rows.Count,
            Items = rows,
        }));
    }

    /// <summary>
    /// <b>可选提示词清单</b>：按 <c>PromptCode</c> 聚合，供「挂接提示词」下拉使用。
    ///
    /// <para>【为什么必须有这个端点】</para>
    /// <para>本页「全文填写规则」页签原先是 <c>:disabled="!promptCode"</c>，而新模板的
    /// <c>FillPromptCode</c> <b>必然是空的</b> ⇒ 页签点不开；就算点开了，面板里
    /// <b>只有「版本表 + 设为默认」，没有任何新建/挂接入口</b> —— 先有鸡还是先有蛋。
    /// 要打破死循环，第一步就是让前端能拿到「现有提示词有哪些」。</para>
    ///
    /// <para>【口径】</para>
    /// <list type="bullet">
    /// <item>⛔ 不含已软删行（软删 = 停用，不该出现在候选里）</item>
    /// <item>同一 <c>PromptCode</c> 跨 <c>OrgCode</c> 聚合；<c>Orgs</c> 列出它有哪些机构版本（空串 = 全局）</item>
    /// <item><c>ActiveVersion</c> = 该 Code 下 <c>IsDefault=1 且 Status='active'</c> 的最大版本号（无则 null）</item>
    /// </list>
    /// </summary>
    [HttpGet("codes")]
    public async Task<IActionResult> Codes([FromQuery] string? keyword)
    {
        var kw = (keyword ?? string.Empty).Trim();

        // ⛔ 走 Client.Queryable（无隐式软删过滤），本方法自己显式排除已删
        var rows = await _db.Client.Queryable<DocFillPrompt>().ToListAsync();
        rows = rows.Where(p => !p.IsDeleted).ToList();

        if (kw.Length > 0)
        {
            rows = rows
                .Where(p => (p.PromptCode ?? string.Empty).Contains(kw, StringComparison.OrdinalIgnoreCase)
                         || (p.PromptName ?? string.Empty).Contains(kw, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var items = rows
            .GroupBy(p => p.PromptCode ?? string.Empty, StringComparer.Ordinal)
            .Where(g => g.Key.Length > 0)
            .Select(g => new
            {
                PromptCode = g.Key,
                // 名称取最新版本那条（改名后应显示新名）
                PromptName = g.OrderByDescending(p => p.Version).First().PromptName,
                VersionCount = g.Count(),
                MaxVersion = g.Max(p => p.Version),
                Orgs = g.Select(p => p.OrgCode ?? string.Empty)
                        .Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList(),
                ActiveVersion = g.Where(p => p.IsDefault && p.Status == "active")
                                 .Select(p => (int?)p.Version).Max(),
                UpdateTime = g.Max(p => p.UpdateTime),
            })
            .OrderBy(i => i.PromptCode, StringComparer.Ordinal)
            .ToList();

        return Ok(ApiResponse<object>.Ok(new { Total = items.Count, Items = items }));
    }

    // ════════════════════════════════════════════════════════════════════
    // 二·补、★ 自动生成全文填写提示词（2026-10-07 用户裁决：「自动生成 = 后端调 LLM」）
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>按「锚点清单 + 文档作用」LLM 生成全文填写提示词正文</b>。
    ///
    /// <para><b>定位</b>：「标准文档填写规则」页「全局填写规则」卡片的「自动生成」按钮。
    /// 生成结果<b>不落库</b>（与 <c>PromptWorkbenchService.GenerateAsync</c> 同口径）——
    /// 由用户在文本框里改，点保存才写（保存走既有 add/update 端点）。</para>
    ///
    /// <para><b>两种模式</b>：<c>CurrentPrompt</c> 为空 = 从零生成；非空 = 在其基础上优化
    /// （保留结构与已有业务口径，不全量重写）。</para>
    ///
    /// <para><b>契约</b>（`22` 号）：业务失败恒 HTTP 200、<c>success</c> 唯一判据、载荷 PascalCase。</para>
    /// </summary>
    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] GenerateRequest req, CancellationToken ct)
    {
        var purpose = (req?.DocPurpose ?? string.Empty).Trim();
        var anchors = (req?.Anchors ?? new List<GenerateAnchor>())
            .Where(a => !string.IsNullOrWhiteSpace(a?.AnchorRef))
            .ToList();

        if (purpose.Length == 0 && anchors.Count == 0)
            return Ok(ApiResponse<object>.Fail("缺少生成依据：文档作用与锚点清单至少给一项"));

        var settings = await GetAiSettingsAsync();
        var prompt = BuildGeneratePrompt(req!, purpose, anchors);

        var resp = await _llm.CompleteAsync(new LlmInvokeRequest
        {
            BaseUrl = settings.BaseUrl,
            ApiKey = settings.ApiKey,
            Model = settings.Model,
            Temperature = settings.Temperature,
            MaxTokens = settings.MaxTokens,
            Prompt = prompt,
            // ★ 产出是「提示词正文」而非 JSON —— ForceJson 会把正文强包成 JSON 字符串
            ForceJson = false,
        });
        await LogGenerateUsageAsync(settings, resp);

        if (!resp.Success)
            return Ok(ApiResponse<object>.Fail("AI 生成失败：" + (string.IsNullOrWhiteSpace(resp.Message) ? "未知错误" : resp.Message)));

        var text = StripFence(resp.Content);
        if (string.IsNullOrWhiteSpace(text))
            return Ok(ApiResponse<object>.Fail("AI 未返回内容（请重试）"));

        return Ok(ApiResponse<object>.Ok(new
        {
            Prompt = text,
            Model = settings.Model,
            DurationMs = resp.DurationMs,
            Optimize = !string.IsNullOrWhiteSpace(req!.CurrentPrompt),
        }));
    }

    /// <summary>组装生成提示词（★ 上下文 = 文档作用 + 锚点清单 + 现有正文，⛔ 不查库 —— 由前端传入）</summary>
    private static string BuildGeneratePrompt(GenerateRequest req, string purpose, List<GenerateAnchor> anchors)
    {
        var sb = new StringBuilder();
        sb.AppendLine("你是 ISO 体系认证审核领域的资深文档工程专家。请为下列标准文档生成一份「全文填写提示词」。");
        sb.AppendLine();
        sb.AppendLine("【这份提示词的用途】");
        sb.AppendLine("它会被文档填写引擎在**整篇文档层面**引用，负责组织文档整体结构、统一行文口吻与颗粒度；");
        sb.AppendLine("而每个具体格子填什么，由各锚点自己的规则负责 —— ⛔ 提示词不要越界去规定单个锚点的取值。");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(req.DocRole))
        {
            sb.AppendLine($"【文档角色】{req.DocRole.Trim()}");
            sb.AppendLine();
        }

        if (purpose.Length > 0)
        {
            sb.AppendLine("【文档作用（用户填写，必须贴合）】");
            sb.AppendLine(purpose);
            sb.AppendLine();
        }

        if (anchors.Count > 0)
        {
            sb.AppendLine($"【锚点清单（共 {anchors.Count} 个位置会被自动填写；提示词须与之呼应，⛔ 不得与这些位置的规则冲突）】");
            foreach (var a in anchors)
            {
                var vt = string.IsNullOrWhiteSpace(a.ValueType) ? "text" : a.ValueType!.Trim();
                var src = string.IsNullOrWhiteSpace(a.Source) ? "" : $"，来源：{a.Source!.Trim()}";
                sb.AppendLine($"- {a.AnchorRef!.Trim()}（值类型 {vt}{src}）");
            }
            sb.AppendLine();
        }

        var isOptimize = !string.IsNullOrWhiteSpace(req.CurrentPrompt);
        if (isOptimize)
        {
            sb.AppendLine("【模式】优化 —— 下方是用户现有提示词正文，请保留其结构与业务口径，只改不足处（⛔ 不要全量重写）。");
            sb.AppendLine();
            sb.AppendLine("--- 现有正文开始 ---");
            sb.AppendLine(req.CurrentPrompt!.Trim());
            sb.AppendLine("--- 现有正文结束 ---");
            sb.AppendLine();
        }

        sb.AppendLine("【输出要求】");
        sb.AppendLine("1. 直接输出提示词正文：Markdown 指令文本，按小节组织（如：整体定位 / 行文口吻 / 结构要求 / 与锚点的协同 / 禁止事项）。");
        sb.AppendLine("2. ⛔ 只输出正文本身：不要 JSON、不要解释你为什么这么写、不要代码围栏（```）、不要“以下是提示词”之类开场白。");
        sb.AppendLine("3. 全文用中文，面向填写引擎执行，口吻具体可操作（禁止“视情况而定”这类空话）。");
        sb.AppendLine($"4. 篇幅约 {Math.Max(400, anchors.Count * 80)}–1200 字。");
        return sb.ToString();
    }

    /// <summary>去围栏（模型偶尔仍会给 ```markdown … ``` 包一层）</summary>
    private static string StripFence(string? s)
    {
        var t = (s ?? string.Empty).Trim();
        if (!t.StartsWith("```", StringComparison.Ordinal)) return t;
        var nl = t.IndexOf('\n');
        if (nl < 0) return t;
        t = t[(nl + 1)..];
        var end = t.LastIndexOf("```", StringComparison.Ordinal);
        return (end >= 0 ? t[..end] : t).Trim();
    }

    // ── AI 配置（口径与 PromptWorkbenchService.GetAiSettingsAsync 逐字一致；⛔ 不另立口径）──

    private sealed class AiSettings
    {
        public string ApiKey { get; set; } = "";
        public string BaseUrl { get; set; } = "https://dashscope.aliyuncs.com/compatible-mode/v1";
        public string Model { get; set; } = "qwen-flash";
        public string Provider { get; set; } = "qianwen";
        public int MaxTokens { get; set; } = 32768;
        public float Temperature { get; set; } = 0.2f;
    }

    private async Task<AiSettings> GetAiSettingsAsync()
    {
        var s = new AiSettings();
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
                case "ai_provider": s.Provider = row.ConfigValue ?? s.Provider; break;
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

    /// <summary>计费留痕（口径同 PromptWorkbenchService.LogUsageAsync；日志失败不影响主流程）</summary>
    private async Task LogGenerateUsageAsync(AiSettings settings, LlmInvokeResponse result)
    {
        try
        {
            var log = new AiUsageLog
            {
                CallId = Guid.NewGuid().ToString("N"),
                Code = Guid.NewGuid().ToString("N"),
                BusinessType = "doc_fill_prompt",
                BusinessRef = "doc_fill_prompt_generate",
                Skill = "doc_fill_prompt",
                Provider = string.IsNullOrWhiteSpace(settings.Provider) ? "qianwen" : settings.Provider,
                Model = settings.Model,
                PromptTokens = result.PromptTokens ?? 0,
                CompletionTokens = result.CompletionTokens ?? 0,
                TotalTokens = (result.PromptTokens ?? 0) + (result.CompletionTokens ?? 0),
                DurationMs = result.DurationMs,
                Success = result.Success,
                ErrorMessage = result.Success ? null : (result.Message.Length > 500 ? result.Message[..500] : result.Message),
                CreateTime = DateTime.Now,
            };
            await _db.Client.Insertable(log).ExecuteCommandAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[DocFillPrompt] AI 用量日志写入失败");
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // 二·补·DTO
    // ════════════════════════════════════════════════════════════════════

    /// <summary>自动生成请求（DTO 字段 PascalCase，`22` 号契约）</summary>
    public sealed class GenerateRequest
    {
        /// <summary>文档作用（全局规则 Tab「文档作用」文本框内容）</summary>
        public string? DocPurpose { get; set; }

        /// <summary>文档角色（可空）</summary>
        public string? DocRole { get; set; }

        /// <summary>现有提示词正文；非空 = 优化模式</summary>
        public string? CurrentPrompt { get; set; }

        /// <summary>锚点清单（前端从当前模板锚点行组装）</summary>
        public List<GenerateAnchor>? Anchors { get; set; }
    }

    /// <summary>生成上下文中的一个锚点（只传生成所需字段，⛔ 不传整行实体）</summary>
    public sealed class GenerateAnchor
    {
        /// <summary>锚点引用 → <c>cert_doc_template_anchor.AnchorRef</c></summary>
        public string? AnchorRef { get; set; }

        /// <summary>值类型（text / number / date / bool）</summary>
        public string? ValueType { get; set; }

        /// <summary>取值来源摘要（SourceSummary / SourceSpec 摘要，可空）</summary>
        public string? Source { get; set; }
    }

    // ════════════════════════════════════════════════════════════════════
    // 三、私有：规范化 / 校验 / 版本 / 排他
    // ════════════════════════════════════════════════════════════════════

    /// <summary>规范化：<c>OrgCode</c> NOT NULL（空 = 全局），数值列落合法区间</summary>
    private static void Normalize(DocFillPrompt p)
    {
        p.OrgCode = (p.OrgCode ?? string.Empty).Trim();
        p.PromptCode = (p.PromptCode ?? string.Empty).Trim();
        p.PromptName = (p.PromptName ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(p.Status)) p.Status = "draft";
        else p.Status = p.Status.Trim();

        if (p.MaxTokens <= 0) p.MaxTokens = 4000;
        if (p.Temperature < 0m) p.Temperature = 0m;
        if (p.Temperature > 2m) p.Temperature = 2m;
    }

    /// <summary>L3 提交期校验</summary>
    private static string? Validate(DocFillPrompt p)
    {
        if (string.IsNullOrWhiteSpace(p.PromptCode)) return "Prompt 编码（PromptCode）不能为空";
        if (string.IsNullOrWhiteSpace(p.PromptName)) return "名称（PromptName）不能为空";
        if (p.MaxTokens <= 0) return "最大 Token 必须大于 0";
        if (p.Version <= 0) return "版本号必须大于 0";

        var status = p.Status ?? string.Empty;
        if (status.Length > 0 && status != "draft" && status != "active" && status != "archived")
            return $"状态「{status}」不合法（可选：draft / active / archived）";

        return null;
    }

    /// <summary>取同 <c>(OrgCode, PromptCode)</c> 的全部版本，<b>含已软删</b>（唯一键查重必需）</summary>
    private async Task<List<DocFillPrompt>> LoadSiblingsAsync(string orgCode, string promptCode)
    {
        return await _db.Client.Queryable<DocFillPrompt>()
            .Where(p => p.OrgCode == orgCode && p.PromptCode == promptCode)
            .ToListAsync();
    }

    /// <summary>
    /// 清掉同 <c>(OrgCode, PromptCode)</c> 下其它行的 <c>IsDefault</c>（<b>列级写入</b>，只动这一列）。
    /// <para>DB 只有普通索引 ⇒ 唯一性由本方法保证；⛔ 不要依赖数据库报错来发现两条默认。</para>
    /// </summary>
    private async Task ClearDefaultAsync(string orgCode, string promptCode, string? exceptCode)
    {
        var others = await _db.Client.Queryable<DocFillPrompt>()
            .Where(p => p.OrgCode == orgCode && p.PromptCode == promptCode
                        && p.IsDefault == true && p.IsDeleted == false)
            .ToListAsync();

        foreach (var o in others)
        {
            if (!string.IsNullOrEmpty(exceptCode)
                && string.Equals(o.Code, exceptCode, StringComparison.Ordinal))
                continue;

            o.IsDefault = false;
            o.UpdateTime = DateTime.Now;

            var upd = await _db.UpdateAsync(o, nameof(DocFillPrompt.IsDefault), nameof(DocFillPrompt.UpdateTime));
            if (!upd.Success)
            {
                // 不中断主流程（默认位不唯一不会导致填充失败，只会让 resolve 有歧义），但必须留痕
                _logger.LogWarning("[DocFillPrompt] 清除旧默认位失败：{Code} → {Err}", o.Code, upd.Error);
            }
        }
    }
}

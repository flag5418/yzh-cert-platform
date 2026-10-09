using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using CertPlatform.Admin.Services.Workflow;
using CertPlatform.Admin.Entities.Wf;
using YZH.Core.Api.Controllers;
using YZH.Core.Api.Services;
using YZH.Core.Stand.Helpers;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Config;
using YZH.Core.Stand.Models.Result;

namespace CertPlatform.Admin.Controllers.Workflow;

/// <summary>
/// Prompt 模板管理控制器
/// <para>路由前缀：/api/PromptTemplate</para>
/// <para>继承 YzhControllerBase 获取标准 CRUD 端点：filter / add / update / delete / config</para>
/// <para>额外提供：activate（激活）/ <b>generate（AI 生成草稿）</b> / <b>test（上传文件试跑）</b> / standards（标准下拉）</para>
/// </summary>
[ApiController]
/// <para><b>★ 端标记（2026-10-03）</b>：路由加 <c>Admin/</c> 段，与专家端 <c>/api/Auditor/*</c> 对称。
/// <para>背景：后台端 20 个 Controller 此前零端标记，4 个连业务域前缀都没有（<c>api/AIUsage</c>
/// <c>api/PromptTemplate</c> <c>api/ValidationRule</c> <c>api/ReportDefinition</c>），
/// 且 <c>api/System/[controller]</c> 与框架层 <c>YZH.Core.Web</c> 的 <c>api/System/*</c> 撞前缀。</para>
/// <para><b>不影响授权</b>：<c>ApiCode = Sha256("{METHOD}|{路由末段}|{动作名}")</c>（ApiScanner.cs:326-331）
/// 只取路由<b>末段</b>作控制器名，本 Controller 的末段未变 ⇒ <c>ApiCode</c> 不变 ⇒
/// <b>角色-接口关联不断裂</b>，无需重跑 ApiSync。</para>
[Route("api/Admin/Workflow/PromptTemplate")]
public class PromptTemplateController : YzhControllerBase<PromptTemplate>
{
    private readonly PromptTemplateService _service;
    private readonly PromptWorkbenchService _workbench;

    public PromptTemplateController(
        EntityService<PromptTemplate> entityService,
        PromptTemplateService service,
        PromptWorkbenchService workbench,
        IUserContext userContext)
        : base(entityService, userContext)
    {
        _service = service;
        _workbench = workbench;
    }

    /// <summary>加载 EntityConfig（Workflow/PromptTemplate.json）</summary>
    protected override EntityConfig LoadConfig()
    {
        return EntityConfigHelper.GetConfig("Workflow/PromptTemplate");
    }

    #region 标准端点（继承自 YzhControllerBase）

    // GET  /api/PromptTemplate/config - 获取表格配置
    // POST /api/PromptTemplate/filter - 分页查询
    // POST /api/PromptTemplate/add - 新增
    // POST /api/PromptTemplate/update - 修改
    // POST /api/PromptTemplate/delete - 删除

    #endregion

    #region 业务端点

    /// <summary>获取指定类型当前生效的提示词（技能优先匹配，回退通用）</summary>
    [HttpGet("active/{promptType}")]
    public async Task<IActionResult> GetActive(string promptType, [FromQuery] string? skillTarget = null)
    {
        var entity = await _service.GetActiveAsync(promptType, skillTarget);
        if (entity == null)
            return Ok(ApiResponse.Fail("未找到生效的提示词"));

        return Ok(ApiResponse<PromptTemplateDto>.Ok(PromptTemplateDto.From(entity)));
    }

    /// <summary>激活提示词（同类型其他提示词置为不生效）</summary>
    [HttpPost("action/activate")]
    public async Task<IActionResult> Activate([FromQuery] string code)
    {
        var ok = await _service.ActivateAsync(code);
        return ok ? Ok(ApiResponse.Ok("激活成功")) : Ok(ApiResponse.Fail("激活失败"));
    }

    // ========================================================
    // ★ 提示词工作台（2026-10-02）—— AI 生成草稿 + 上传试跑
    // ========================================================

    /// <summary>
    /// 【工作台】按「标准 + 类型」定位生效提示词（三层回退：标准级 → 平台级）
    /// <para>GET /api/PromptTemplate/workbench/resolve?promptType=doc_group&amp;standardCode=xxx</para>
    /// </summary>
    [HttpGet("workbench/resolve")]
    public async Task<IActionResult> Resolve([FromQuery] string promptType, [FromQuery] string? standardCode)
    {
        if (string.IsNullOrWhiteSpace(promptType))
            return Ok(ApiResponse.Fail("promptType 不能为空"));

        var entity = await _workbench.ResolveActiveAsync(promptType, standardCode);
        // 未命中 ≠ 错误：「还没有提示词」是正常业务态（用户尚未配置），返 200 + data:null。
        // 前端 `resolveActivePrompt` 用了 `res.data || null`，故此变更不影响既有显示逻辑。
        if (entity == null)
            return Ok(ApiResponse<PromptTemplateDto>.Ok(null));

        return Ok(ApiResponse<PromptTemplateDto>.Ok(PromptTemplateDto.From(entity)));
    }

    /// <summary>
    /// 【工作台】AI 生成 / 优化提示词草稿（**不落库**，返回正文由用户确认后保存）
    /// <para>POST /api/PromptTemplate/workbench/generate</para>
    /// <para>★ <c>currentTemplate</c> 非空 = 优化用户现有手写提示词（不全量重写）；
    /// 为空 = 按标准从零生成。</para>
    /// <para>★ 返回强类型 <see cref="PromptWorkbenchService.GenerateResult"/>（显式 camelCase），
    /// 不用匿名对象 —— 匿名对象会走 PascalCase，与 DTO 惯例不一致 ⇒ 前端读 <c>data.prompt</c> 得 undefined。</para>
    /// </summary>
    [HttpPost("workbench/generate")]
    public async Task<IActionResult> Generate([FromBody] PromptGenerateRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.PromptType))
            return Ok(ApiResponse.Fail("promptType 不能为空"));

        // ★ 2026-10-09：包 try/catch —— 生成链路里的意外异常（如 DB/配置读取失败）转成业务错误，
        //   把真实原因回给前端，避免裸 500 被 GlobalExceptionFilter 脱敏成「操作失败，请联系管理员」
        //   （开发环境）或被前端 STATUS_FALLBACK[500] 显示成「服务器内部错误，请稍后重试」，掩盖真实原因。
        try
        {
            var r = await _workbench.GenerateAsync(
                req.PromptType!, req.StandardCode, req.ExtraRequirement, req.CurrentTemplate);
            return r.Success
                ? Ok(ApiResponse<PromptWorkbenchService.GenerateResult>.Ok(r, "生成成功"))
                : Ok(ApiResponse.Fail(r.Message));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse.Fail("AI 生成失败：" + ex.Message));
        }
    }

    /// <summary>
    /// 【工作台】上传文件试跑提示词：转 Markdown → 落 Redis → 跑提示词 → 返回结果。
    /// <para>POST /api/PromptTemplate/workbench/test（multipart）</para>
    /// <para><b>两种调用</b>（2026-10-02 勘误：Markdown 可复用，不再「文件即弃」）：</para>
    /// <list type="bullet">
    ///   <item><b>带 files</b>：转换 → 写缓存（覆写 cacheKey）→ 跑 LLM → 回传 <c>cacheKey</c></item>
    ///   <item><b>只带 cacheKey</b>：读缓存直接跑 LLM（**零转换、零上传**）；
    ///         缓存过期 → 返回「测试缓存已过期，请重新上传文件」</item>
    /// </list>
    /// <para>★ <c>template</c> 可传「页面上未保存的编辑内容」，实现真正的边改边试。</para>
    /// </summary>
    [HttpPost("workbench/test")]
    [RequestSizeLimit(200_000_000)]
    public async Task<IActionResult> Test([FromForm] PromptTestRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.PromptType))
            return Ok(ApiResponse.Fail("promptType 不能为空"));

        var hasFiles = req.Files != null && req.Files.Any(x => x != null && x.Length > 0);
        if (!hasFiles && string.IsNullOrWhiteSpace(req.CacheKey))
            return Ok(ApiResponse.Fail("请先选择要测试的文件"));

        var files = new List<(string FileName, byte[] Content)>();
        if (hasFiles)
        {
            foreach (var f in req.Files!)
            {
                if (f == null || f.Length == 0) continue;
                using var ms = new MemoryStream();
                await f.CopyToAsync(ms);
                files.Add((f.FileName, ms.ToArray()));
            }
        }

        var r = await _workbench.TestAsync(
            req.PromptType!, req.Template, req.StandardCode, files, req.CacheKey);
        // ★ 试跑失败也返回完整结果（含转换日志 + 实际提示词），便于排查「到底哪一步不对」
        return Ok(ApiResponse<PromptWorkbenchService.TestResult>.Ok(r, r.Message));
    }

    /// <summary>【工作台】标准下拉：供选择提示词的适用标准</summary>
    [HttpGet("workbench/standards")]
    public async Task<IActionResult> Standards()
    {
        var list = await _service.GetStandardOptionsAsync();
        return Ok(ApiResponse<List<PromptTemplateService.StandardOption>>.Ok(list));
    }

    /// <summary>
    /// 【工作台】列出「某类型 + 某标准」下的全部提示词（含平台级 StandardCode 为空的行）。
    /// <para>GET /api/PromptTemplate/workbench/list?promptType=doc_group&amp;standardCode=xxx</para>
    /// <para>⚠️ 不走通用 <c>filter</c>：通用列表默认带 <c>IsValid=1</c> 且分页，
    /// 而工作台要的是「这个标准下到底有几条、哪条生效」，一次全量返回更直接。</para>
    /// </summary>
    [HttpGet("workbench/list")]
    public async Task<IActionResult> List([FromQuery] string? promptType, [FromQuery] string? standardCode)
    {
        var rows = await _workbench.ListAsync(promptType, standardCode);
        var list = rows.Select(PromptTemplateDto.From).ToList();
        return Ok(ApiResponse<List<PromptTemplateDto>>.Ok(list));
    }

    /// <summary>
    /// 【工作台】保存提示词（按 <c>PromptCode</c> 幂等 upsert：无则新增、有则覆盖正文并置为生效）。
    /// <para>POST /api/PromptTemplate/workbench/save</para>
    /// <para>⚠️ 不走通用 <c>add</c>/<c>update</c>：那两个走 <c>EntityService</c> 的
    /// 「<c>updateFields</c> = 全部 BcFlag 列」全量写回，会把服务端生成的 <c>Code</c>/<c>IsActive</c> 覆盖成前端值。</para>
    /// <para>⛔ 版本不再 +1（2026-10-02 裁决：提示词不做版本管理）；
    /// 模型参数不在本请求内（统一 AI 配置，Q3=a）。</para>
    /// </summary>
    [HttpPost("workbench/save")]
    public async Task<IActionResult> Save([FromBody] PromptSaveRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.PromptCode))
            return Ok(ApiResponse.Fail("提示词编码（PromptCode）不能为空"));
        if (string.IsNullOrWhiteSpace(req.Template))
            return Ok(ApiResponse.Fail("提示词内容不能为空"));

        var entity = new PromptTemplate
        {
            PromptCode = req.PromptCode!.Trim(),
            PromptName = string.IsNullOrWhiteSpace(req.PromptName) ? req.PromptCode!.Trim() : req.PromptName!.Trim(),
            PromptType = req.PromptType?.Trim() ?? "",
            SkillTarget = string.IsNullOrWhiteSpace(req.SkillTarget) ? null : req.SkillTarget!.Trim(),
            StandardCode = string.IsNullOrWhiteSpace(req.StandardCode) ? null : req.StandardCode!.Trim(),
            Template = req.Template,
            Description = req.Description,
            // ModelName / MaxTokens / Temperature 故意不赋值（保持 null）：
            // SaveAsync 更新分支会把库中已有值原样保留，NC 链路读它们不受影响。
        };

        var (ok, msg) = await _service.SaveAsync(entity);
        return ok ? Ok(ApiResponse.Ok(msg)) : Ok(ApiResponse.Fail(msg));
    }

    /// <summary>【工作台】删除提示词（逻辑禁用 IsValid = 0）</summary>
    [HttpPost("workbench/delete")]
    public async Task<IActionResult> Remove([FromQuery] string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return Ok(ApiResponse.Fail("提示词编码不能为空"));

        var ok = await _service.DeleteAsync(code);
        return ok ? Ok(ApiResponse.Ok("删除成功")) : Ok(ApiResponse.Fail("删除失败：提示词不存在"));
    }

    /// <summary>【工作台】切换生效状态（同类型其他提示词自动置为不生效）</summary>
    [HttpPost("workbench/activate")]
    public async Task<IActionResult> WorkbenchActivate([FromQuery] string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return Ok(ApiResponse.Fail("提示词编码不能为空"));

        var ok = await _service.ActivateAsync(code);
        return ok ? Ok(ApiResponse.Ok("已设为生效")) : Ok(ApiResponse.Fail("操作失败：提示词不存在"));
    }

    #endregion
}

public class PromptTemplateDto
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("promptCode")] public string PromptCode { get; set; } = string.Empty;
    [JsonPropertyName("promptName")] public string PromptName { get; set; } = string.Empty;
    [JsonPropertyName("promptType")] public string PromptType { get; set; } = string.Empty;
    [JsonPropertyName("skillTarget")] public string? SkillTarget { get; set; }
    [JsonPropertyName("standardCode")] public string? StandardCode { get; set; }
    [JsonPropertyName("template")] public string? Template { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("isActive")] public bool IsActive { get; set; }
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("creator")] public string? Creator { get; set; }
    [JsonPropertyName("createTime")] public DateTime? CreateTime { get; set; }
    [JsonPropertyName("updateTime")] public DateTime? UpdateTime { get; set; }

    // ⛔ 2026-10-02 起不再下发 ModelName / MaxTokens / Temperature / Version：
    //    本工作台「统一 AI 配置」（Q3=a）—— 模型参数一律读 cert_sys_config，
    //    版本管理已裁决不做。NC 链路 BuildNcPromptSkill 仍读库里的行级参数，不受影响。

    public static PromptTemplateDto From(PromptTemplate e) => new()
    {
        Id = e.Id,
        PromptCode = e.PromptCode,
        PromptName = e.PromptName,
        PromptType = e.PromptType,
        SkillTarget = e.SkillTarget,
        StandardCode = e.StandardCode,
        Template = e.Template,
        Description = e.Description,
        IsActive = e.IsActive,
        Status = e.Status,
        Creator = e.CreateBy,
        CreateTime = e.CreateTime,
        UpdateTime = e.UpdateTime,
    };
}

/// <summary>
/// 【工作台】AI 生成 / 优化提示词草稿请求
/// <para>⚠️ 命名必须带 <c>Prompt</c> 前缀：同命名空间下 <c>CertPlatform.Shared.DocExtraction</c>
/// 已有 <c>GeneratePromptRequest</c>，无前缀同名会<b>遮蔽</b>它并导致 <c>DocExtractionRuleController</c> 编译失败（CS1503）。</para>
/// </summary>
public class PromptGenerateRequest
{
    /// <summary>要生成的类型：doc_group（分类）/ doc_content（作用）</summary>
    public string? PromptType { get; set; }
    /// <summary>适用标准 Code（GUID），仅用于给元提示词提供上下文</summary>
    public string? StandardCode { get; set; }
    /// <summary>额外要求（用户自由输入，可选）</summary>
    public string? ExtraRequirement { get; set; }
    /// <summary>
    /// ★ 现有提示词正文（2026-10-02 新增）。非空 = AI **优化**这段手写内容（不全量重写）；
    /// 空 = 按标准从零生成。
    /// </summary>
    public string? CurrentTemplate { get; set; }
}

/// <summary>【工作台】上传文件试跑请求（multipart/form-data）</summary>
public class PromptTestRequest
{
    /// <summary>
    /// 待测试的文件（可多个；分类提示词可多文件，作用提示词取第一个）。
    /// <para>★ 可为空 —— 此时必须提供 <see cref="CacheKey"/> 走「读缓存复用 Markdown」路径。</para>
    /// </summary>
    public List<IFormFile>? Files { get; set; }
    /// <summary>提示词类型：doc_group / doc_content</summary>
    public string? PromptType { get; set; }
    /// <summary>★ 页面上未保存的编辑内容（优先于库里的生效版本，实现边改边试）</summary>
    public string? Template { get; set; }
    /// <summary>适用标准 Code（GUID）</summary>
    public string? StandardCode { get; set; }
    /// <summary>
    /// ★ Markdown 缓存键（2026-10-02 新增）。
    /// <para>传 files + cacheKey → 转换后覆写该 key；只传 cacheKey → 读缓存（零转换）。</para>
    /// <para>为空时后端生成并回传，前端存起来供下次复用。</para>
    /// </summary>
    public string? CacheKey { get; set; }
}

/// <summary>【工作台】保存提示词请求（字段名 = 实体属性名，PascalCase 逐字一致）</summary>
public class PromptSaveRequest
{
    public string? PromptCode { get; set; }
    public string? PromptName { get; set; }
    public string? PromptType { get; set; }
    public string? StandardCode { get; set; }
    public string? SkillTarget { get; set; }
    public string? Template { get; set; }
    public string? Description { get; set; }

    // ⛔ 已移除 ModelName / MaxTokens / Temperature（2026-10-02 统一 AI 配置，Q3=a）。
    //    更新时由 PromptTemplateService.SaveAsync **保留原值**，避免把 NC 链路
    //    BuildNcPromptSkill 在读的行级参数抹成 NULL。
}

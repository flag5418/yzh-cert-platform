extern alias SharedEntities;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using CertPlatform.Admin.Services.Workflow;
using SharedEntities::YZH.Entity.Admin.Platform.Wf;

namespace CertPlatform.Admin.Controllers.Workflow;

/// <summary>
/// Prompt 模板管理控制器（业务层移植）
/// <para>路由前缀：/api/PromptTemplate（对齐旧 YZH.WebApi PromptTemplateController 的 api/prompt-template，</para>
/// <para>新前端 cert-share/api/workflow/prompt-template.ts 使用 PascalCase 路径，ASP.NET 路由大小写不敏感）。</para>
/// <para>旧端点：list（GET，按类型/技能筛选）/ {promptCode} / active/{promptType} / save / delete / activate，</para>
/// <para>新前端契约：POST getList（筛选条件走 body）+ save / delete?promptCode= / activate?promptCode=。</para>
/// <para>⚠️ getList 直接返回数组（非 {code,data} 包装）：前端 `tableData.value = res || []` 直接消费数组。</para>
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PromptTemplateController : ControllerBase
{
    private readonly PromptTemplateService _service;

    public PromptTemplateController(PromptTemplateService service)
    {
        _service = service;
    }

    #region 查询

    /// <summary>获取提示词列表（新前端契约：筛选条件走 body）</summary>
    [HttpPost("getList")]
    public async Task<IActionResult> GetList([FromBody] PromptTemplateQuery? query)
    {
        var list = await _service.GetListAsync(query?.PromptType, query?.SkillTarget);
        return Ok(list.Select(PromptTemplateDto.From));
    }

    /// <summary>获取提示词列表（旧 GET 契约兼容）</summary>
    [HttpGet("list")]
    [HttpGet]
    public async Task<IActionResult> GetListByQuery(
        [FromQuery] string? promptType = null,
        [FromQuery] string? skillTarget = null)
    {
        var list = await _service.GetListAsync(promptType, skillTarget);
        return Ok(list.Select(PromptTemplateDto.From));
    }

    /// <summary>根据编码获取单条提示词</summary>
    [HttpGet("{promptCode}")]
    public async Task<IActionResult> GetByCode(string promptCode)
    {
        var entity = await _service.GetByCodeAsync(promptCode);
        if (entity == null)
            return Ok(new { code = 404, success = false, message = "提示词不存在" });

        return Ok(new { code = 200, success = true, data = PromptTemplateDto.From(entity) });
    }

    /// <summary>获取指定类型当前生效的提示词（技能优先匹配，回退通用）</summary>
    [HttpGet("active/{promptType}")]
    public async Task<IActionResult> GetActive(string promptType, [FromQuery] string? skillTarget = null)
    {
        var entity = await _service.GetActiveAsync(promptType, skillTarget);
        if (entity == null)
            return Ok(new { code = 404, success = false, message = "未找到生效的提示词" });

        return Ok(new { code = 200, success = true, data = PromptTemplateDto.From(entity) });
    }

    #endregion

    #region 写入

    /// <summary>创建或更新提示词（按 prompt_code 幂等）</summary>
    [HttpPost("save")]
    public async Task<IActionResult> Save([FromBody] PromptTemplate entity)
    {
        var (ok, message) = await _service.SaveAsync(entity);
        return Ok(new { code = ok ? 200 : 400, success = ok, message });
    }

    /// <summary>删除提示词（逻辑删除）</summary>
    [HttpPost("delete")]
    public async Task<IActionResult> Delete([FromQuery] string promptCode)
    {
        var ok = await _service.DeleteAsync(promptCode);
        return Ok(new { code = ok ? 200 : 400, success = ok, message = ok ? "删除成功" : "删除失败" });
    }

    /// <summary>激活提示词（同类型其他提示词置为不生效）</summary>
    [HttpPost("activate")]
    public async Task<IActionResult> Activate([FromQuery] string promptCode)
    {
        var ok = await _service.ActivateAsync(promptCode);
        return Ok(new { code = ok ? 200 : 400, success = ok, message = ok ? "激活成功" : "激活失败" });
    }

    #endregion
}

/// <summary>
/// 列表查询条件（前端 body：{ promptType, skillTarget }）
/// </summary>
public class PromptTemplateQuery
{
    public string? PromptType { get; set; }
    public string? SkillTarget { get; set; }
}

/// <summary>
/// Prompt 模板响应 DTO
/// <para>camelCase 契约：新前端按 camelCase 读取（row.promptCode / row.isActive ...），</para>
/// <para>而新后端默认 PascalCase 序列化，故显式标注 [JsonPropertyName]（对齐 DocExtraction DTO 惯例）。</para>
/// </summary>
public class PromptTemplateDto
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("promptCode")] public string PromptCode { get; set; } = string.Empty;
    [JsonPropertyName("promptName")] public string PromptName { get; set; } = string.Empty;
    [JsonPropertyName("promptType")] public string PromptType { get; set; } = string.Empty;
    [JsonPropertyName("skillTarget")] public string? SkillTarget { get; set; }
    [JsonPropertyName("template")] public string? Template { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("version")] public int Version { get; set; }
    [JsonPropertyName("isActive")] public bool IsActive { get; set; }
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("creator")] public string? Creator { get; set; }
    [JsonPropertyName("createDate")] public DateTime? CreateDate { get; set; }
    [JsonPropertyName("modifyDate")] public DateTime? ModifyDate { get; set; }

    public static PromptTemplateDto From(PromptTemplate e) => new()
    {
        Id = e.Id,
        PromptCode = e.PromptCode,
        PromptName = e.PromptName,
        PromptType = e.PromptType,
        SkillTarget = e.SkillTarget,
        Template = e.Template,
        Description = e.Description,
        Version = e.Version,
        IsActive = e.IsActive,
        Status = e.Status,
        Creator = e.Creator,
        CreateDate = e.CreateDate,
        ModifyDate = e.ModifyDate,
    };
}

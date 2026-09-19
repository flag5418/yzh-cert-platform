using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using CertPlatform.Admin.Services.Workflow;
using CertPlatform.Shared.Entities.Wf;
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
/// <para>额外提供 activate 操作：POST /api/PromptTemplate/action/activate?code=xxx</para>
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PromptTemplateController : YzhControllerBase<PromptTemplate>
{
    private readonly PromptTemplateService _service;

    public PromptTemplateController(EntityService<PromptTemplate> entityService, PromptTemplateService service, IUserContext userContext)
        : base(entityService, userContext)
    {
        _service = service;
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
            return BadRequest(ApiResponse.Fail("未找到生效的提示词"));

        return Ok(ApiResponse<PromptTemplateDto>.Ok(PromptTemplateDto.From(entity)));
    }

    /// <summary>激活提示词（同类型其他提示词置为不生效）</summary>
    [HttpPost("action/activate")]
    public async Task<IActionResult> Activate([FromQuery] string code)
    {
        var ok = await _service.ActivateAsync(code);
        return ok ? Ok(ApiResponse.Ok("激活成功")) : BadRequest(ApiResponse.Fail("激活失败"));
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
    [JsonPropertyName("template")] public string? Template { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("version")] public int Version { get; set; }
    [JsonPropertyName("isActive")] public bool IsActive { get; set; }
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("creator")] public string? Creator { get; set; }
    [JsonPropertyName("createTime")] public DateTime? CreateTime { get; set; }
    [JsonPropertyName("updateTime")] public DateTime? UpdateTime { get; set; }

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
        Creator = e.CreateBy,
        CreateTime = e.CreateTime,
        UpdateTime = e.UpdateTime,
    };
}

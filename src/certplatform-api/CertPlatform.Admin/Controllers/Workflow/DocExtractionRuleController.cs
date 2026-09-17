extern alias SharedEntities;

using Microsoft.AspNetCore.Mvc;
using CertPlatform.Admin.Services.DocExtraction;
using SharedEntities::YZH.Entity.Admin.Platform.Doc;

namespace CertPlatform.Admin.Controllers.Workflow;

/// <summary>
/// 文档提取规则控制器（业务层 #17 移植）
/// <para>路由前缀：/api/Workflow/DocExtractionRule</para>
/// <para>对照旧 DocExtractionRuleController（251 行，15 端点）：</para>
/// <para>· analyze / generate-prompt / verify / save / configured-rules / {standardFileCode} 详情 /</para>
/// <para>·  {ruleCode}/fields-tables / {standardFileCode}/delete / ai-config 读写 / skills /</para>
/// <para>·  test-field / test-table；旧 POST list 废弃（新基类统一 POST filter）</para>
/// <para>· 新增 file-preview（PDF 流）/ file-markdown（Markdown 文本），对应 D-6 双产物</para>
/// </summary>
[ApiController]
[Route("api/Workflow/[controller]")]
public class DocExtractionRuleController : ControllerBase
{
    private readonly DocExtractionRuleService _service;

    public DocExtractionRuleController(DocExtractionRuleService service)
    {
        _service = service;
    }

    #region AI 分析

    /// <summary>AI 自动分析文档（推荐字段/表格）</summary>
    [HttpPost("analyze")]
    public async Task<IActionResult> AIAnalyze([FromBody] AIAnalyzeRequest request)
    {
        try
        {
            var result = await _service.AIAnalyzeAsync(request);
            return Ok(new { code = 200, data = result, message = result.Message });
        }
        catch (Exception ex)
        {
            return Ok(new { code = 500, message = $"AI 分析失败：{ex.Message}" });
        }
    }

    /// <summary>生成提取 Prompt</summary>
    [HttpPost("generate-prompt")]
    public async Task<IActionResult> GeneratePrompt([FromBody] GeneratePromptRequest request)
    {
        try
        {
            var prompt = await _service.GeneratePromptAsync(request);
            return Ok(new { code = 200, data = prompt });
        }
        catch (Exception ex)
        {
            return Ok(new { code = 500, message = $"生成 Prompt 失败：{ex.Message}" });
        }
    }

    /// <summary>验证 Prompt（实际提取并落 sample_data）</summary>
    [HttpPost("verify")]
    public async Task<IActionResult> VerifyPrompt([FromBody] VerifyPromptRequest request)
    {
        try
        {
            var result = await _service.VerifyPromptAsync(request);
            return Ok(new { code = result.Success ? 200 : 400, data = result, message = result.Message });
        }
        catch (Exception ex)
        {
            return Ok(new { code = 500, message = $"验证失败：{ex.Message}" });
        }
    }

    #endregion

    #region 规则 CRUD

    /// <summary>保存提取规则（单事务：规则 + 字段 + 表格定义 + 同步提取值）</summary>
    [HttpPost("save")]
    public async Task<IActionResult> SaveExtractionRule([FromBody] SaveExtractionRuleRequest request)
    {
        try
        {
            var (ok, message) = await _service.SaveExtractionRuleAsync(request);
            return Ok(new { code = ok ? 200 : 400, message });
        }
        catch (Exception ex)
        {
            return Ok(new { code = 500, message = $"保存失败：{ex.Message}" });
        }
    }

    /// <summary>获取已配置提取规则的文档列表（供工作流配置页选择）</summary>
    [HttpGet("configured-rules")]
    public async Task<IActionResult> GetConfiguredRules()
    {
        var rules = await _service.GetConfiguredRulesAsync();
        return Ok(new { code = 200, data = rules });
    }

    /// <summary>获取规则详情（按规则键 standardFileCode）</summary>
    [HttpGet("{standardFileCode}")]
    public async Task<IActionResult> GetRuleDetail(string standardFileCode)
    {
        var detail = await _service.GetRuleDetailAsync(standardFileCode);
        if (detail == null)
            return Ok(new { code = 404, message = "该文件尚未配置提取规则" });
        return Ok(new { code = 200, data = detail });
    }

    /// <summary>获取规则的字段和表格定义（供工作流 docField/docTable 节点选择）</summary>
    [HttpGet("{ruleCode}/fields-tables")]
    public async Task<IActionResult> GetFieldsAndTables(string ruleCode)
    {
        var result = await _service.GetFieldsAndTablesAsync(ruleCode);
        return Ok(new { code = 200, data = result });
    }

    /// <summary>删除规则（级联删字段/表格定义/提取结果）</summary>
    [HttpPost("{standardFileCode}/delete")]
    public async Task<IActionResult> DeleteRule(string standardFileCode)
    {
        var ok = await _service.DeleteRuleAsync(standardFileCode);
        return Ok(new { code = ok ? 200 : 404, message = ok ? "删除成功" : "规则不存在" });
    }

    #endregion

    #region AI 配置 / 技能

    /// <summary>获取 AI 配置（cert_ai_config + cert_sys_config 六键）</summary>
    [HttpGet("ai-config")]
    public async Task<IActionResult> GetAIConfig()
    {
        var config = await _service.GetAIConfigAsync();
        return Ok(new { code = 200, data = config });
    }

    /// <summary>更新 AI 配置</summary>
    [HttpPost("ai-config")]
    public async Task<IActionResult> UpdateAIConfig([FromBody] AIConfigDto config)
    {
        var (ok, message) = await _service.UpdateAIConfigAsync(config);
        return Ok(new { code = ok ? 200 : 400, message });
    }

    /// <summary>获取可用技能列表（按扩展名推导 word/excel/pdf）</summary>
    [HttpGet("skills")]
    public IActionResult GetSkills()
    {
        return Ok(new { code = 200, data = _service.GetSkills() });
    }

    #endregion

    #region 配置期试运行

    /// <summary>测试字段提取（工作流 docField 节点配置期验证）</summary>
    [HttpPost("test-field")]
    public async Task<IActionResult> TestField([FromBody] TestFieldRequest request)
    {
        var result = await _service.TestFieldAsync(request);
        return Ok(new { code = 200, data = result });
    }

    /// <summary>测试表格提取（工作流 docTable 节点配置期验证）</summary>
    [HttpPost("test-table")]
    public async Task<IActionResult> TestTable([FromBody] TestTableRequest request)
    {
        var result = await _service.TestTableAsync(request);
        return Ok(new { code = 200, data = result });
    }

    #endregion

    #region 文档内容（D-6 双产物）

    /// <summary>获取文档 Markdown（提取上下文；无产物时实时转换）</summary>
    [HttpGet("file-markdown")]
    public async Task<IActionResult> GetFileMarkdown([FromQuery] string fileCode)
    {
        var (markdown, error) = await _service.GetFileMarkdownForPreviewAsync(fileCode);
        if (error != null) return Ok(new { code = 400, message = error });
        return Ok(new { code = 200, data = markdown });
    }

    /// <summary>获取文档预览 PDF 流（无产物时实时转换）</summary>
    [HttpGet("file-preview")]
    public async Task<IActionResult> GetFilePreview([FromQuery] string fileCode)
    {
        var (content, fileName, error) = await _service.GetFilePreviewPdfAsync(fileCode);
        if (error != null || content == null)
            return Ok(new { code = 400, message = error ?? "预览产物生成失败" });
        return File(content, "application/pdf", $"{fileName}.pdf");
    }

    #endregion
}


using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using CertPlatform.Admin.Services.StandardDirectory;
using YZH.Core.Api.Controllers;
using YZH.Core.Api.Services;
using YZH.Core.Stand.Helpers;
using YZH.Core.Stand.Models;
using YZH.Core.Stand.Models.Config;
using YZH.Core.Stand.Models.Result;
using YZH.Core.Stand.Interfaces;

namespace CertPlatform.Admin.Controllers.Foundation;

/// <summary>
///     ISO 标准管理控制器
///     
///     功能：ISO 标准的增删改查（单表 CRUD）
///     路由前缀：/api/Foundation/ISOStandard
///     
///     继承 YzhControllerBase 获得能力：
///     - GET    /config              获取页面配置（EntityConfig）
///     - POST   /filter              分页查询
///     - POST   /add                 新增
///     - POST   /update              修改
///     - POST   /delete              软删除
///     - POST   /toggle-valid        切换有效标志
///     - POST   /export              导出
///     - POST   /import              批量导入
///     - POST   /action/{methodName} 自定义行操作
///     
///     数据库：cert_iso_standard
///     
///     前端路由映射：
///     - path: '/cert/iso-standard'
///     - component: '@/pages/foundation/iso-standard/index.vue'
/// </summary>
[ApiController]

/// <para><b>★ 端标记（2026-10-03）</b>：路由加 <c>Admin/</c> 段，与专家端 <c>/api/Auditor/*</c> 对称。
/// <para>背景：后台端 20 个 Controller 此前零端标记，4 个连业务域前缀都没有（<c>api/AIUsage</c>
/// <c>api/PromptTemplate</c> <c>api/ValidationRule</c> <c>api/ReportDefinition</c>），
/// 且 <c>api/System/[controller]</c> 与框架层 <c>YZH.Core.Web</c> 的 <c>api/System/*</c> 撞前缀。</para>
/// <para><b>不影响授权</b>：<c>ApiCode = Sha256("{METHOD}|{路由末段}|{动作名}")</c>（ApiScanner.cs:326-331）
/// 只取路由<b>末段</b>作控制器名，本 Controller 的末段未变 ⇒ <c>ApiCode</c> 不变 ⇒
/// <b>角色-接口关联不断裂</b>，无需重跑 ApiSync。</para>
[Route("api/Admin/Foundation/[controller]")]
public class ISOStandardController : YzhControllerBase<ISOStandard>
{
    private readonly StandardSourceService _source;

    public ISOStandardController(
        EntityService<ISOStandard> entityService,
        IUserContext userContext,
        StandardSourceService source)
        : base(entityService, userContext)
    {
        _source = source;
    }

    // ========================================================
    // 自定义配置
    // ========================================================

    /// <summary>
    /// 加载 EntityConfig 配置
    /// 配置文件路径：Assets/EntityConfigs/Foundation/ISOStandard.json
    /// </summary>
    protected override EntityConfig LoadConfig()
    {
        return EntityConfigHelper.GetConfig<ISOStandard>();
    }

    // ========================================================
    // 业务校验
    // ========================================================

    /// <summary>
    /// 新增前校验：标准名称唯一 + 同版本下标准编号唯一
    /// </summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeAdd(
        ISOStandard entity)
    {
        if (string.IsNullOrWhiteSpace(entity.StandardName))
            return (false, "标准名称不能为空");

        var nameExists = await Entity.ExistsAsync(s =>
            s.StandardName == entity.StandardName &&
            s.VersionYear == entity.VersionYear);
        if (nameExists.Data)
            return (false, $"版本 {entity.VersionYear} 下标准名称【{entity.StandardName}】已存在");

        var exists = await Entity.ExistsAsync(s =>
            s.StandardCode == entity.StandardCode &&
            s.VersionYear == entity.VersionYear);

        if (exists.Data)
            return (false, $"版本 {entity.VersionYear} 下标准编号【{entity.StandardCode}】已存在");

        return (true, null);
    }

    /// <summary>
    /// 修改前校验：标准名称+版本年份唯一（排除自身）+ 同版本下标准编号唯一（排除自身）
    /// </summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeUpdate(
        ISOStandard entity)
    {
        if (string.IsNullOrWhiteSpace(entity.StandardName))
            return (false, "标准名称不能为空");

        var nameExists = await Entity.ExistsAsync(s =>
            s.Code != entity.Code &&
            s.StandardName == entity.StandardName &&
            s.VersionYear == entity.VersionYear);
        if (nameExists.Data)
            return (false, $"版本 {entity.VersionYear} 下标准名称【{entity.StandardName}】已存在");

        var exists = await Entity.ExistsAsync(s =>
            s.Code != entity.Code &&
            s.StandardCode == entity.StandardCode &&
            s.VersionYear == entity.VersionYear);

        if (exists.Data)
            return (false, $"版本 {entity.VersionYear} 下标准编号【{entity.StandardCode}】已存在");

        return (true, null);
    }

    // ════════════════════════════════════════════════════════════════════
    // ★ 标准原文 → Markdown → 条款（V3 知识层，2026-11-10）
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>上传标准文件</b>（PDF / DOCX）→ 转 Markdown 落库 → 返回条款解析预览。
    /// <para>不写条款：前端看到预览、确认后再调 <see cref="ApplyClauses"/>。</para>
    /// </summary>
    [HttpPost("upload-markdown")]
    [RequestSizeLimit(200_000_000)]
    public async Task<IActionResult> UploadMarkdown([FromForm] StandardSourceUploadRequest req)
    {
        if (req?.File == null || req.File.Length == 0)
            return Ok(ApiResponse.Fail("请选择标准原文文件（PDF / DOCX）"));
        if (string.IsNullOrWhiteSpace(req.StandardCode))
            return Ok(ApiResponse.Fail("缺少标准编码"));

        byte[] bytes;
        using (var ms = new MemoryStream())
        {
            await req.File.CopyToAsync(ms);
            bytes = ms.ToArray();
        }

        var r = await _source.UploadAsync(req.StandardCode, string.IsNullOrWhiteSpace(req.File.FileName) ? "standard.pdf" : req.File.FileName, bytes);
        return r.Success
            ? Ok(ApiResponse<object>.Ok(r.Data, r.Message))
            : Ok(ApiResponse.Fail(r.Message));
    }

    /// <summary><b>只解析、不写库</b>：对已存 Markdown 的标准重新生成条款预览。</summary>
    [HttpPost("preview-clauses")]
    public async Task<IActionResult> PreviewClauses([FromBody] StandardSourceKeyRequest req)
    {
        if (string.IsNullOrWhiteSpace(req?.StandardCode))
            return Ok(ApiResponse.Fail("缺少标准编码"));

        var data = await _source.PreviewAsync(req.StandardCode);
        if (data == null)
            return Ok(ApiResponse.Fail("该标准还没有原文 Markdown，请先上传标准文件"));

        return Ok(ApiResponse<object>.Ok(data, $"解析出 {data.ParsedCount} 条条款"));
    }

    /// <summary>
    /// <b>写入条款</b>：默认只补缺失编号；<c>Overwrite=true</c> 时<b>先软删除该标准现有条款及属性，再按本次解析结果全量重建</b>。
    /// <para>用于「上传错标准」或标准换版；旧条款/属性保留在库里可追溯，但不再参与查询。</para>
    /// </summary>
    [HttpPost("apply-clauses")]
    public async Task<IActionResult> ApplyClauses([FromBody] StandardClauseApplyRequest req)
    {
        if (string.IsNullOrWhiteSpace(req?.StandardCode))
            return Ok(ApiResponse.Fail("缺少标准编码"));

        var r = await _source.ApplyAsync(req.StandardCode, req.Overwrite);
        return r.Success
            ? Ok(ApiResponse<object>.Ok(r, r.Message))
            : Ok(ApiResponse.Fail(r.Message));
    }

    /// <summary>
    /// <b>补建条款 Markdown</b>：只按现有条款表重渲染派生视图，不重新解析原文、不写条款。
    /// <para>用于存量标准（已有条款但还没有 ClauseMarkdownPath）或生成失败后的重试。</para>
    /// </summary>
    [HttpPost("rebuild-clause-markdown")]
    public async Task<IActionResult> RebuildClauseMarkdown([FromBody] StandardSourceKeyRequest req)
    {
        if (string.IsNullOrWhiteSpace(req?.StandardCode))
            return Ok(ApiResponse.Fail("缺少标准编码"));

        var r = await _source.RebuildClauseMarkdownAsync(req.StandardCode);
        return r.Success
            ? Ok(ApiResponse<object>.Ok(r, r.Message))
            : Ok(ApiResponse.Fail(r.Message));
    }

    /// <summary><b>预览条款 Markdown</b>（只读，不重新生成）。</summary>
    [HttpGet("clause-markdown")]
    public async Task<IActionResult> ClauseMarkdown([FromQuery] string? standardCode)
    {
        if (string.IsNullOrWhiteSpace(standardCode))
            return Ok(ApiResponse.Fail("缺少标准编码"));

        var data = await _source.PreviewClauseMarkdownAsync(standardCode);
        if (data == null)
            return Ok(ApiResponse.Fail("该标准还没有条款 Markdown，请先执行「写入条款」或补建"));

        return Ok(ApiResponse<object>.Ok(data, $"条款 Markdown 共 {data.Length} 字"));
    }

}

/// <summary>上传标准原文入参（multipart）</summary>
public sealed class StandardSourceUploadRequest
{
    public IFormFile? File { get; set; }
    /// <summary>标准短码（iso9001）或 Code（GUID）</summary>
    public string? StandardCode { get; set; }
}

/// <summary>按标准取预览入参</summary>
public sealed class StandardSourceKeyRequest
{
    public string? StandardCode { get; set; }
}

/// <summary>写入条款入参</summary>
public sealed class StandardClauseApplyRequest
{
    public string? StandardCode { get; set; }
    /// <summary>true = 同时更新已存在编号的标题/正文（默认 false）</summary>
    public bool Overwrite { get; set; }
}

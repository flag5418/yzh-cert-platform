using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Attributes;
using YZH.Core.Api.Services;
using YZH.Core.Stand.Models;
using YZH.Core.Stand.Models.Result;
using CertPlatform.Shared.Entities.Cert;

namespace CertPlatform.Auditor.Controllers;

/// <summary>
/// 认证计划 —— 只读查询控制器（标准 / 条款 / 阶段）
///
/// <para><b>路由</b>：<c>api/Auditor/CertQuery</c></para>
/// <para><b>用途</b>：专家端「认证计划」向导拉取标准目录、条款树、阶段目录。</para>
/// <para><b>为什么放 Auditor 端</b>：标准/条款/阶段的写接口在 <c>/api/Admin/</c>（admin-only），
/// 专家端禁止引用 Admin（A-R1）。三个实体均在 <c>CertPlatform.Shared</c>，Auditor 可直接
/// 用 <see cref="EntityService{T}"/> 只读查询，无需跨端引用。</para>
/// </summary>
[ApiController]
[YZHAuthorize]
[Route("api/Auditor/[controller]")]
public class CertQueryController : ControllerBase
{
    private readonly EntityService<ISOStandard> _stdSvc;
    private readonly EntityService<ISOClause> _clauseSvc;
    private readonly EntityService<CertStage> _stageSvc;

    public CertQueryController(
        EntityService<ISOStandard> stdSvc,
        EntityService<ISOClause> clauseSvc,
        EntityService<CertStage> stageSvc)
    {
        _stdSvc = stdSvc;
        _clauseSvc = clauseSvc;
        _stageSvc = stageSvc;
    }

    /// <summary>标准目录（含简称 Abbrev）</summary>
    [HttpGet("standards")]
    public async Task<ActionResult<ApiResponse<object?>>> Standards()
    {
        var list = await _stdSvc.GetListAsync(s => s.IsValid == 1);
        if (!list.Success)
            return Ok(ApiResponse<object?>.Fail(list.Error ?? "查询标准失败"));

        var data = list.Data!
            .Select(s => new
            {
                Code = s.Code,
                StandardCode = s.StandardCode,
                StandardName = s.StandardName,
                Abbrev = string.IsNullOrWhiteSpace(s.Abbrev) ? DeriveAbbrev(s.StandardCode) : s.Abbrev
            })
            .ToList();
        return Ok(ApiResponse<object?>.Ok(data));
    }

    /// <summary>条款列表（按标准过滤；standardCode = 标准的 Code GUID）</summary>
    [HttpGet("clauses")]
    public async Task<ActionResult<ApiResponse<object?>>> Clauses([FromQuery] string? standardCode)
    {
        Expression<Func<ISOClause, bool>> pred = c => c.IsValid == 1;
        if (!string.IsNullOrWhiteSpace(standardCode))
            pred = c => c.IsValid == 1 && c.StandardCode == standardCode;

        var list = await _clauseSvc.GetListAsync(pred);
        if (!list.Success)
            return Ok(ApiResponse<object?>.Fail(list.Error ?? "查询条款失败"));

        var data = list.Data!
            .Select(c => new
            {
                Code = c.Code,
                StandardCode = c.StandardCode,
                ClauseNumber = c.ClauseNumber,
                Title = c.Title,
                ParentCode = c.ParentCode
            })
            .ToList();
        return Ok(ApiResponse<object?>.Ok(data));
    }

    /// <summary>阶段目录（cert_cert_stage）</summary>
    [HttpGet("stages")]
    public async Task<ActionResult<ApiResponse<object?>>> Stages()
    {
        var list = await _stageSvc.GetListAsync(s => s.IsValid == 1);
        if (!list.Success)
            return Ok(ApiResponse<object?>.Fail(list.Error ?? "查询阶段失败"));

        var data = list.Data!
            .Select(s => new
            {
                Code = s.Code,
                StageCode = s.StageCode,
                StageName = s.StageName,
                Category = s.Category
            })
            .ToList();
        return Ok(ApiResponse<object?>.Ok(data));
    }

    /// <summary>标准简称兜底推导（未在标准表维护 Abbrev 时）</summary>
    private static string DeriveAbbrev(string? standardCode)
    {
        if (string.IsNullOrWhiteSpace(standardCode)) return string.Empty;
        return standardCode.ToLowerInvariant() switch
        {
            "iso9001" => "e",
            "iso14001" => "n",
            "iso45001" => "s",
            "iso13485" => "m",
            "iso22000" => "f",
            "iso27001" => "i",
            "iso20000" => "t",
            _ => standardCode.Length >= 3 ? standardCode.Substring(0, 3) : standardCode
        };
    }
}

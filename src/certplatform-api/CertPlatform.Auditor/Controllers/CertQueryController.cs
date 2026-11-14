using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Attributes;
using YZH.Core.Api.Services;
using YZH.Core.Stand.Models;
using YZH.Core.Stand.Models.Result;
using CertPlatform.Auditor.Entities.Expert;
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
    private readonly EntityService<CertAuditPlanDetail> _detailSvc;
    private readonly EntityService<ClauseAttrRow> _clauseAttrSvc;

    public CertQueryController(
        EntityService<ISOStandard> stdSvc,
        EntityService<ISOClause> clauseSvc,
        EntityService<CertStage> stageSvc,
        EntityService<CertAuditPlanDetail> detailSvc,
        EntityService<ClauseAttrRow> clauseAttrSvc)
    {
        _stdSvc = stdSvc;
        _clauseSvc = clauseSvc;
        _stageSvc = stageSvc;
        _detailSvc = detailSvc;
        _clauseAttrSvc = clauseAttrSvc;
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
                VersionYear = s.VersionYear,
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

    /// <summary>
    /// 条款详细内容（详情页点条款编号时的小弹窗）。
    /// <para>按计划明细 <c>detailCode</c> 取其 <c>Clauses</c>（ISOClause.Code）精确取条款，
    /// 避免同编号跨标准歧义；返回编号 / 标题 / 原文摘要 / 标准名。</para>
    /// </summary>
    [HttpGet("clause-content")]
    public async Task<ActionResult<ApiResponse<object?>>> ClauseContent([FromQuery] string? detailCode)
    {
        if (string.IsNullOrWhiteSpace(detailCode))
            return Ok(ApiResponse<object?>.Fail("缺少 detailCode"));

        var dl = await _detailSvc.GetListAsync(d => d.Code == detailCode && !d.IsDeleted);
        var detail = dl.Success ? dl.Data?.FirstOrDefault() : null;
        if (detail == null)
            return Ok(ApiResponse<object?>.Fail("计划明细不存在"));

        var codes = (detail.Clauses ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(c => c.Trim())
            .Where(c => c.Length > 0)
            .Distinct()
            .ToArray();
        if (codes.Length == 0)
            return Ok(ApiResponse<object?>.Ok(new List<object>()));

        var cl = await _clauseSvc.GetListAsync(c => codes.Contains(c.Code!) && c.IsValid == 1 && !c.IsDeleted);
        if (!cl.Success)
            return Ok(ApiResponse<object?>.Fail(cl.Error ?? "查询条款失败"));

        var clauses = cl.Data ?? new List<ISOClause>();
        var clauseCodeList = clauses
            .Select(c => c.Code ?? string.Empty)
            .Where(c => c.Length > 0)
            .Distinct()
            .ToList();

        // ★ 条款属性一并带回：属性表用 ClauseCode 指向条款（不是复制正文），
        //   这里把「条款正文 + 属性」合成一份响应，页面/分析不用再查两次。
        var attrResult = clauseCodeList.Count == 0
            ? null
            : await _clauseAttrSvc.GetListAsync(a =>
                clauseCodeList.Contains(a.ClauseCode) && !a.IsDeleted && a.IsValid == 1);
        var attrsByClause = (attrResult?.Data ?? new List<ClauseAttrRow>())
            .GroupBy(a => a.ClauseCode, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(a => a.Sort)
                      .Select(a => new CertClauseAttrItem { AttrCode = a.AttrCode, AttrValue = a.AttrValue })
                      .ToList(),
                StringComparer.Ordinal);

        var stdCodes = clauses.Select(c => c.StandardCode ?? string.Empty).Where(c => c.Length > 0).Distinct().ToArray();
        var stds = stdCodes.Length == 0
            ? new List<ISOStandard>()
            : (await _stdSvc.GetListAsync(s => stdCodes.Contains(s.Code!))).Data ?? new List<ISOStandard>();
        var stdMap = stds.Where(s => !string.IsNullOrEmpty(s.Code)).ToDictionary(s => s.Code!, s => s);

        var data = clauses
            .OrderBy(c => c.Sort)
            .ThenBy(c => c.ClauseNumber)
            .Select(c =>
            {
                var std = stdMap.TryGetValue(c.StandardCode ?? string.Empty, out var hit) ? hit : null;
                return new
                {
                    c.Code,
                    c.ClauseNumber,
                    c.Title,
                    c.Description,
                    Attributes = attrsByClause.TryGetValue(c.Code ?? string.Empty, out var attrs)
                        ? attrs
                        : new List<CertClauseAttrItem>(),
                    StandardName = std?.StandardName,
                    Abbrev = std == null
                        ? string.Empty
                        : (string.IsNullOrWhiteSpace(std.Abbrev) ? DeriveAbbrev(std.StandardCode) : std.Abbrev)
                };
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

/// <summary>条款属性（只带编码与值；中文标签由前端/属性池映射）</summary>
public sealed class CertClauseAttrItem
{
    public string AttrCode { get; set; } = string.Empty;
    public string? AttrValue { get; set; }
}

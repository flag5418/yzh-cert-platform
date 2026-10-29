using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Controllers;
using YZH.Core.Api.Services;
using YZH.Core.Stand.Helpers;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models;
using YZH.Core.Stand.Models.Config;
using YZH.Core.Stand.Models.Result;
using CertPlatform.Auditor.Entities.Expert;

namespace CertPlatform.Auditor.Controllers;

/// <summary>
/// 认证计划控制器（专家端 —— 按条款为核心的体系认证入场计划）
///
/// <para><b>路由</b>：<c>api/Auditor/AuditPlan</c></para>
/// <para><b>数据表</b>：<c>cert_audit_plan</c>（主表）+ <c>cert_audit_plan_detail</c>（明细）。</para>
///
/// <para><b>主表</b>继承 <see cref="YzhControllerBase{T}"/>，提供 filter / add / update / delete /
/// toggle-valid 标准端点；向导一次性提交「主表 + 明细」走自定义 <c>save</c> 端点。</para>
/// </summary>
[ApiController]
[Route("api/Auditor/[controller]")]
public class AuditPlanController : YzhControllerBase<CertAuditPlan>
{
    private readonly EntityService<CertAuditPlanDetail> _detailSvc;

    /// <summary>
    /// 本次请求内被自动作废的计划条数（供 <c>save</c> 回显）。
    /// <para>⚠️ 控制器是每请求实例，跨 await 读同一字段安全；⛔ 不要做成 static。</para>
    /// </summary>
    private int _invalidatedCount;

    public AuditPlanController(
        EntityService<CertAuditPlan> entityService,
        IUserContext userContext,
        EntityService<CertAuditPlanDetail> detailSvc)
        : base(entityService, userContext)
    {
        _detailSvc = detailSvc;
    }

    /// <summary>缺 EntityConfig 时直接抛错（开发期暴露）</summary>
    protected override bool StrictConfigLoad => true;

    protected override EntityConfig LoadConfig()
    {
        return EntityConfigHelper.GetConfig<CertAuditPlan>();
    }

    // ========================================================
    // 校验钩子
    // ========================================================

    protected override Task<(bool ok, string? msg)> OnBeforeAdd(CertAuditPlan entity)
    {
        if (string.IsNullOrWhiteSpace(entity.EnterpriseCode))
            return Task.FromResult<(bool, string?)>((false, "请选择企业"));
        if (string.IsNullOrWhiteSpace(entity.StageCode))
            return Task.FromResult<(bool, string?)>((false, "请选择阶段"));
        if (string.IsNullOrWhiteSpace(entity.Standards))
            return Task.FromResult<(bool, string?)>((false, "请至少选择一个认证标准"));
        if (entity.IsValid == 0) entity.IsValid = 1;
        return Task.FromResult<(bool, string?)>((true, null));
    }

    protected override Task<(bool ok, string? msg)> OnBeforeUpdate(CertAuditPlan entity)
    {
        if (string.IsNullOrWhiteSpace(entity.Code))
            return Task.FromResult<(bool, string?)>((false, "更新失败：缺少业务键 Code"));
        return Task.FromResult<(bool, string?)>((true, null));
    }

    // ========================================================
    // ★★ 一企业一有效计划（36 号 §4.0，2026-10-29 用户裁定）
    // ========================================================

    /// <summary>
    /// 作废同一企业下<b>其它</b>有效计划（<c>IsValid 1 → 0</c>）。
    ///
    /// <para><b>为什么在 <c>OnAfterAdd</c> 而不是 <c>OnBeforeAdd</c></b>：
    /// 放 before 时，若插入失败（唯一键/DB 异常），旧计划已被作废 ⇒ 该企业<b>零有效计划</b>，
    /// 上传模块随即拿不到标准。必须先插成功再作废旧的。</para>
    ///
    /// <para><b>为什么靠代码而不靠唯一索引</b>：MySQL 没有部分唯一索引；
    /// 建 <c>(EnterpriseCode, IsValid)</c> 唯一索引会把<b>历史作废行</b>也挡掉（同一企业只能有 1 条 IsValid=0）。</para>
    ///
    /// <para><b>⛔ 只作废、不删行、不删明细</b>：历史计划与其部门×条款关系必须留痕，
    /// 否则「上一轮计划审了什么」永久丢失。</para>
    /// </summary>
    /// <param name="enterpriseCode">企业 Code</param>
    /// <param name="keepCode">要保留的计划 Code（null = 全部作废）</param>
    /// <returns>实际作废条数</returns>
    private async Task<int> InvalidateOtherPlansAsync(string? enterpriseCode, string? keepCode)
    {
        _invalidatedCount = 0;
        if (string.IsNullOrWhiteSpace(enterpriseCode)) return 0;

        // ⚠️ ORM 没有全局查询过滤器（IsValid / IsDeleted 都要手写）⇒ 必须显式带上
        var list = await Entity.GetListAsync(p =>
            p.EnterpriseCode == enterpriseCode && p.IsValid == 1 && !p.IsDeleted);
        if (!list.Success || list.Data == null || list.Data.Count == 0) return 0;

        // keepCode 的比较放内存里做：SQL 里 p.Code != null 是 NULL 语义（永不成立），易踩坑
        var others = list.Data
            .Where(p => string.IsNullOrEmpty(keepCode) || p.Code != keepCode)
            .ToList();
        if (others.Count == 0) return 0;

        var stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        int n = 0;
        foreach (var old in others)
        {
            old.IsValid = 0;
            old.Remark = Trim($"[{stamp} 自动作废] 同一企业只允许一个有效计划，已被新计划取代。{old.Remark}", 500);
            var r = await Entity.Update(old, UserContext.ClientIp);
            if (r.Success) n++;
        }
        _invalidatedCount = n;
        return n;
    }

    /// <summary>截断到 <paramref name="max"/> 字符（DB 列宽硬约束，超长会报 1406）</summary>
    private static string Trim(string? s, int max)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return s.Length <= max ? s : s.Substring(0, max);
    }

    /// <summary>新增成功后作废该企业其它有效计划</summary>
    protected override async Task OnAfterAdd(CertAuditPlan entity)
    {
        await InvalidateOtherPlansAsync(entity.EnterpriseCode, entity.Code);
    }

    /// <summary>更新成功后作废该企业其它有效计划（仅当本计划是有效的）</summary>
    protected override async Task OnAfterUpdate(CertAuditPlan entity)
    {
        if (entity.IsValid == 1)
            await InvalidateOtherPlansAsync(entity.EnterpriseCode, entity.Code);
    }

    // ========================================================
    // ★ 当前有效计划（上传/提取模块的唯一标准来源）
    // ========================================================

    /// <summary>
    /// 取某企业的<b>当前有效计划</b>（含标准 Code 列表）。
    ///
    /// <para><b>★ 这是「标准从哪来」的唯一答案</b>（36 号 §9.1）：上传资料时<b>不带标准</b>，
    /// 标准只在提取阶段由本端点给出。前端与后端提取编排都必须走这里，
    /// ⛔ 不许各自查 <c>cert_audit_plan</c> 再自己拆 <c>Standards</c>。</para>
    ///
    /// <para><b>无有效计划</b> ⇒ 返回 <c>Ok</c> + <c>plan = null</c>（不是 Fail）：
    /// 「还没做计划」是正常业务态，不是错误。调用方据此提示「请先创建认证计划」。</para>
    /// </summary>
    [HttpGet("active")]
    public async Task<ActionResult<ApiResponse<object?>>> GetActivePlan([FromQuery] string enterpriseCode)
    {
        if (string.IsNullOrWhiteSpace(enterpriseCode))
            return Ok(ApiResponse<object?>.Fail("缺少 enterpriseCode"));

        var list = await Entity.GetListAsync(p =>
            p.EnterpriseCode == enterpriseCode && p.IsValid == 1 && !p.IsDeleted);
        if (!list.Success)
            return Ok(ApiResponse<object?>.Fail(list.Error ?? "查询计划失败"));

        // 理论上最多 1 条；若历史脏数据有 2 条，取 Sort 最小者并如实告知（⛔ 不静默挑一条）
        var rows = (list.Data ?? new List<CertAuditPlan>())
            .OrderBy(p => p.Sort).ThenBy(p => p.CreateTime).ToList();
        if (rows.Count == 0)
            return Ok(ApiResponse<object?>.Ok(new { plan = (CertAuditPlan?)null, standardCodes = new List<string>(), warnings = new List<string>() }));

        var plan = rows[0];
        var warnings = new List<string>();
        if (rows.Count > 1)
            warnings.Add($"该企业存在 {rows.Count} 个有效计划（数据异常），已按 Sort 取第一个：{plan.Name}");

        var standardCodes = SplitCodes(plan.Standards);
        if (standardCodes.Count == 0)
            warnings.Add("当前计划的认证标准为空，提取阶段将无标准可跑");

        return Ok(ApiResponse<object?>.Ok(new { plan, standardCodes, warnings }));
    }

    /// <summary>拆分逗号分隔的 Code 串（去空、去重、保持顺序）</summary>
    private static List<string> SplitCodes(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>();
        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (seen.Add(part)) result.Add(part);
        }
        return result;
    }

    // ========================================================
    // 向导提交：主表 + 明细 一次性保存
    // ========================================================

    /// <summary>明细 DTO</summary>
    public class PlanDetailDto
    {
        public string Clauses { get; set; } = string.Empty;
        public string Departments { get; set; } = string.Empty;
        public string DeptNames { get; set; } = string.Empty;
        public string ClauseNumbers { get; set; } = string.Empty;
        public string? Remark { get; set; }
    }

    /// <summary>保存计划 DTO</summary>
    public class SavePlanDto
    {
        public CertAuditPlan Plan { get; set; } = new();
        public List<PlanDetailDto> Details { get; set; } = new();
    }

    /// <summary>
    /// 保存计划（主表 + 明细）。Code 为空 = 新增；非空 = 更新（先删旧明细再写新明细）。
    /// </summary>
    [HttpPost("save")]
    public async Task<ActionResult<ApiResponse<object?>>> Save([FromBody] SavePlanDto dto)
    {
        if (dto?.Plan == null)
            return Ok(ApiResponse<object?>.Fail("缺少计划数据"));

        var plan = dto.Plan;
        if (string.IsNullOrWhiteSpace(plan.EnterpriseCode)
            || string.IsNullOrWhiteSpace(plan.StageCode)
            || string.IsNullOrWhiteSpace(plan.Standards))
            return Ok(ApiResponse<object?>.Fail("企业 / 阶段 / 标准 不能为空"));

        // 一企业一有效计划：计数由 OnAfterAdd / OnAfterUpdate 回填，先清零
        _invalidatedCount = 0;

        try
        {
            if (string.IsNullOrWhiteSpace(plan.Code))
            {
                // 新增主表（AddCore 自动生成 Code + 审计字段）
                var add = await AddCore(plan);
                if (!add.Success)
                    return Ok(ApiResponse<object?>.Fail(add.Error ?? "创建计划失败"));
                plan = add.Data!;
            }
            else
            {
                // 更新主表
                var upd = await UpdateCore(plan);
                if (!upd.Success)
                    return Ok(ApiResponse<object?>.Fail(upd.Error ?? "更新计划失败"));

                // 回读：UpdateCore 只回显入参（Id=0、CreateTime=更新时间），前端展示需真实行
                var fresh = await Entity.GetListAsync(p => p.Code == plan.Code);
                if (fresh.Success && fresh.Data != null && fresh.Data.Count > 0)
                    plan = fresh.Data[0];

                // 删除旧明细（整表重建，简单可靠）
                var old = await _detailSvc.GetListAsync(d => d.PlanCode == plan.Code);
                if (old.Success && old.Data != null && old.Data.Count > 0)
                {
                    var codes = old.Data.Select(d => d.Code!).ToArray();
                    await _detailSvc.DeleteBatch(codes, hardDelete: true, clientIp: UserContext.ClientIp);
                }
            }

            // 写入新明细
            var savedDetails = new List<CertAuditPlanDetail>();
            for (int i = 0; i < dto.Details.Count; i++)
            {
                var d = dto.Details[i];
                var detail = new CertAuditPlanDetail
                {
                    PlanCode = plan.Code!,
                    Clauses = d.Clauses,
                    Departments = d.Departments,
                    DeptNames = d.DeptNames,
                    ClauseNumbers = d.ClauseNumbers,
                    Sort = i,
                    IsValid = 1,
                    Remark = d.Remark
                };
                var ins = await _detailSvc.Insert(detail, UserContext.ClientIp);
                if (ins.Success) savedDetails.Add(ins.Data!);
            }

            return Ok(ApiResponse<object?>.Ok(new
            {
                plan,
                details = savedDetails,
                // 一企业一有效计划：本次自动作废的旧计划条数（0 = 该企业此前无有效计划）
                invalidatedPlans = _invalidatedCount
            }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<object?>.Fail($"保存失败：{ex.Message}"));
        }
    }

    /// <summary>查询某计划的明细列表</summary>
    [HttpGet("details")]
    public async Task<ActionResult<ApiResponse<object?>>> GetDetails([FromQuery] string planCode)
    {
        if (string.IsNullOrWhiteSpace(planCode))
            return Ok(ApiResponse<object?>.Fail("缺少 planCode"));

        var list = await _detailSvc.GetListAsync(d => d.PlanCode == planCode);
        if (!list.Success)
            return Ok(ApiResponse<object?>.Fail(list.Error ?? "查询明细失败"));

        return Ok(ApiResponse<object?>.Ok(list.Data ?? new List<CertAuditPlanDetail>()));
    }
}

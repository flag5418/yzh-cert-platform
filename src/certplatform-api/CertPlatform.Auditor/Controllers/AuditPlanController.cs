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
using CertPlatform.Shared.Domain.Clauses;
using CertPlatform.Shared.Domain.Scope;

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
    private readonly EntityService<CertAuditMember> _memberSvc;
    private readonly EntityService<CertTask> _taskSvc;
    private readonly ICoreResourceQuery _core;

    public AuditPlanController(
        EntityService<CertAuditPlan> entityService,
        IUserContext userContext,
        EntityService<CertAuditPlanDetail> detailSvc,
        EntityService<CertAuditMember> memberSvc,
        EntityService<CertTask> taskSvc,
        ICoreResourceQuery core)
        : base(entityService, userContext)
    {
        _detailSvc = detailSvc;
        _memberSvc = memberSvc;
        _taskSvc = taskSvc;
        _core = core;
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
    // ★★ 计划只增不改不删（2026-11-03 用户裁定）
    // ========================================================
    //
    // 旧实现会把同一企业的其它计划置 IsValid=0，结果「新建一次计划，旧计划全部消失」。
    // 用户要求：建立计划必须留痕（保留创建时间、按时间倒序），因此：
    //   · 新建计划不再作废任何旧计划；
    //   · 计划不允许删除（历史留存）；
    //   · 「当前计划」由 GetActivePlan 按 CreateTime 最新给出。

    /// <summary>计划不允许删除 —— 历史计划必须留痕</summary>
    protected override Task<(bool ok, string? msg)> OnBeforeDelete(string[] codes)
    {
        return Task.FromResult<(bool, string?)>(
            (false, "认证计划不允许删除；如需更换范围，请新建计划，旧计划保留以供追溯"));
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

        // ★ 计划只增不废 ⇒ 同一企业可能有多条有效计划，取「创建时间最新」的那条作为当前计划
        var rows = (list.Data ?? new List<CertAuditPlan>())
            .OrderByDescending(p => p.CreateTime).ThenByDescending(p => p.Sort).ToList();
        if (rows.Count == 0)
            return Ok(ApiResponse<object?>.Ok(new { plan = (CertAuditPlan?)null, standardCodes = new List<string>(), warnings = new List<string>() }));

        var plan = rows[0];
        var warnings = new List<string>();
        if (rows.Count > 1)
            warnings.Add($"该企业存在 {rows.Count} 个有效计划，已按创建时间取最新一个：{plan.Name}");

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
        /// <summary>
        /// 条款业务键 JSON（可选）：<c>[{"StandardNo":"iso9001","ClauseNumber":"4.1"}]</c>。
        /// <para>前端不传时由后端按 <see cref="Clauses"/> 的 Code 反查条款号补全。</para>
        /// </summary>
        public string? ClauseRefsJson { get; set; }
        /// <summary>负责审核员（专家组成员 Code，可空 = 后续在准备阶段分配）</summary>
        public string? OwnerCode { get; set; }
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

        // ★ 计划支持重新调整（2026-11-03 用户纠正）：带 Code 走更新（部门 / 条款约定 / 审核员均可改），
        //   不带 Code 走新建。历史计划一律保留（不再「新建即作废旧计划」），删除仍被禁止。
        var isUpdate = !string.IsNullOrWhiteSpace(plan.Code);

        // 审核员（可选）：必须来自专家组成员池；姓名以成员池为准，不采信前端传值。
        // 先校验再落库 —— 否则计划已写入、明细才失败，会留下一个没有分工的空计划。
        var ownerNames = new Dictionary<string, string>();
        foreach (var d in dto.Details)
        {
            var oc = d.OwnerCode?.Trim();
            if (string.IsNullOrEmpty(oc) || ownerNames.ContainsKey(oc)) continue;
            var mem = await _memberSvc.GetListAsync(m => m.Code == oc && !m.IsDeleted && m.IsValid == 1);
            var hit = mem.Success ? mem.Data?.FirstOrDefault() : null;
            if (hit == null)
                return Ok(ApiResponse<object?>.Fail($"审核员不在专家组成员池中：{oc}，请先在「专家组设置」里维护"));
            ownerNames[oc] = hit.Name;
        }

        try
        {
            if (!isUpdate)
            {
                // 新增主表（AddCore 自动生成 Code + 审计字段）
                var add = await AddCore(plan);
                if (!add.Success)
                    return Ok(ApiResponse<object?>.Fail(add.Error ?? "创建计划失败"));
                plan = add.Data!;
            }
            else
            {
                // 更新主表（企业 / 阶段 / 标准 / 名称 可调整）
                // ★ 必须先按 Code 取现有行再覆盖可改字段：UpdateCore 按 BcFlag 全列写回，
                //   直接拿前端传来的「部分字段」更新会把 CreateTime / CreateBy 等审计列清空。
                var cur = await Entity.GetListAsync(p => p.Code == plan.Code && !p.IsDeleted);
                var row = cur.Success ? cur.Data?.FirstOrDefault() : null;
                if (row == null)
                    return Ok(ApiResponse<object?>.Fail($"计划不存在：{plan.Code}"));

                // 已关闭的计划不允许再调整（只能查看）
                if (row.Status == "closed")
                    return Ok(ApiResponse<object?>.Fail("计划已关闭，不能再调整；如需继续，请新建计划"));

                row.Name = plan.Name;
                row.EnterpriseCode = plan.EnterpriseCode;
                row.StageCode = plan.StageCode;
                row.Standards = plan.Standards;
                row.IsValid = 1;

                var upd = await UpdateCore(row);
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
                var ownerCode = d.OwnerCode?.Trim();
                var resolved = await ResolveDetailRefsAsync(d);
                var detail = new CertAuditPlanDetail
                {
                    PlanCode = plan.Code!,
                    Clauses = resolved.Clauses,
                    Departments = d.Departments,
                    DeptNames = d.DeptNames,
                    ClauseNumbers = d.ClauseNumbers,
                    ClauseRefsJson = resolved.RefsJson,
                    OwnerCode = string.IsNullOrEmpty(ownerCode) ? null : ownerCode,
                    OwnerName = string.IsNullOrEmpty(ownerCode) ? null : ownerNames[ownerCode],
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
                details = savedDetails
            }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<object?>.Fail($"保存失败：{ex.Message}"));
        }
    }

    /// <summary>
    /// 过滤无正文父级标题：只保留 <c>cert_iso_clause.Description</c> 非空的条款。
    /// <para>4（组织环境）只有标题没有正文，真实审核报告不会把它列成条款。</para>
    /// </summary>
    private async Task<List<ClauseRef>> FilterContentRefsAsync(List<ClauseRef> refs)
    {
        var valid = new List<ClauseRef>();
        foreach (var group in refs.GroupBy(r => r.StandardNo, StringComparer.Ordinal))
        {
            var clauses = await _core.GetClausesByNumbersAsync(
                group.Key, group.Select(r => r.ClauseNumber));
            var contentNos = clauses
                .Where(c => !string.IsNullOrWhiteSpace(c.Description))
                .Select(c => ClauseRefs.NormalizeNumber(c.ClauseNumber))
                .ToHashSet(StringComparer.Ordinal);
            foreach (var r in group)
            {
                if (contentNos.Contains(ClauseRefs.NormalizeNumber(r.ClauseNumber)))
                    valid.Add(r);
            }
        }
        return valid;
    }

    /// <summary>
    /// 解析计划明细的条款业务键：
    /// ① 前端已传 <c>ClauseRefsJson</c> ⇒ 直接归一；
    /// ② 否则按 <c>Clauses</c> 的旧 Code 反查条款号与标准短码；
    /// ③ 再按业务键解析当前有效 Code 回填 <c>Clauses</c>（ClauseCode 只作缓存）。
    /// </summary>
    private async Task<(string? Clauses, string? RefsJson)> ResolveDetailRefsAsync(PlanDetailDto d)
    {
        var refs = ClauseRefs.Parse(d.ClauseRefsJson);
        if (refs.Count == 0)
        {
            var codes = SplitCodes(d.Clauses);
            if (codes.Count > 0)
            {
                var clauses = await _core.GetClausesByCodesAsync(codes);
                var clauseByCode = clauses
                    .Where(c => !string.IsNullOrEmpty(c.Code))
                    .ToDictionary(c => c.Code!, c => c, StringComparer.Ordinal);
                var standards = await _core.GetValidStandardsAsync();
                var stdByGuid = standards
                    .Where(s => !string.IsNullOrEmpty(s.Code))
                    .ToDictionary(s => s.Code!, s => s, StringComparer.Ordinal);

                foreach (var code in codes)
                {
                    var clause = clauseByCode.GetValueOrDefault(code);
                    if (clause == null) continue;
                    var std = stdByGuid.GetValueOrDefault(clause.StandardCode ?? string.Empty);
                    if (std == null) continue;
                    refs.Add(new ClauseRef
                    {
                        StandardNo = std.StandardCode,
                        ClauseNumber = ClauseRefs.NormalizeNumber(clause.ClauseNumber)
                    });
                }
                refs = ClauseRefs.Clean(refs);
            }
        }

        if (refs.Count == 0) return (d.Clauses, null);

        // 按业务键重新解析当前 Code：标准重建后旧 Code 失效，这一步是把计划拉回可用状态。
        // ★ 只保留有正文的条款：父级标题（如 4 组织环境）Description 为空，不能进计划/检查，
        //   否则真实审核报告里的条款会被章标题污染。
        var currentByKey = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var group in refs.GroupBy(r => r.StandardNo, StringComparer.Ordinal))
        {
            var clauses = await _core.GetClausesByNumbersAsync(
                group.Key, group.Select(r => r.ClauseNumber));
            foreach (var c in clauses)
            {
                if (string.IsNullOrEmpty(c.Code)) continue;
                if (string.IsNullOrWhiteSpace(c.Description)) continue;
                currentByKey[group.Key + "|" + ClauseRefs.NormalizeNumber(c.ClauseNumber)] = c.Code;
            }
        }

        var currentCodes = new List<string>();
        var validRefs = new List<ClauseRef>();
        foreach (var r in refs)
        {
            var code = currentByKey.GetValueOrDefault(r.StandardNo + "|" + ClauseRefs.NormalizeNumber(r.ClauseNumber));
            if (!string.IsNullOrEmpty(code) && !currentCodes.Contains(code))
            {
                currentCodes.Add(code);
                validRefs.Add(r);
            }
        }

        if (validRefs.Count == 0)
            return (null, null);

        return (string.Join(",", currentCodes), ClauseRefs.ToJson(validRefs));
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

        // 读侧兜底：历史计划明细可能把 4 这类无正文父级标题也存进了 refs。
        // 计划页按 refs 找回条款 Code，若不清理，界面仍会显示“4.1 + 4”。
        var details = list.Data ?? new List<CertAuditPlanDetail>();
        foreach (var d in details)
        {
            var refs = ClauseRefs.Parse(d.ClauseRefsJson);
            if (refs.Count == 0) continue;
            var valid = await FilterContentRefsAsync(refs);
            if (valid.Count == 0) continue;
            d.ClauseRefsJson = ClauseRefs.ToJson(valid);
            d.ClauseNumbers = string.Join(",", valid.Select(r =>
                ClauseRefs.NormalizeNumber(r.ClauseNumber)));
        }

        return Ok(ApiResponse<object?>.Ok(details));
    }

    /// <summary>关闭计划请求</summary>
    public class PlanCodeRequest
    {
        public string? Code { get; set; }
    }

    /// <summary>
    /// 关闭计划（2026-11-03）。
    /// <para>关闭后该计划对应的认证准备 / 认证检查一并作废：所有写操作返回 409，只允许查看。</para>
    /// <para>★ 只改业务状态 <c>Status='closed'</c>，不动 <c>IsValid</c>（启用/禁用是另一维度）。</para>
    /// </summary>
    [HttpPost("close")]
    public async Task<ActionResult<ApiResponse<object?>>> Close([FromBody] PlanCodeRequest req)
    {
        if (string.IsNullOrWhiteSpace(req?.Code))
            return Ok(ApiResponse<object?>.Fail("缺少计划 Code"));

        var cur = await Entity.GetListAsync(p => p.Code == req.Code && !p.IsDeleted);
        var row = cur.Success ? cur.Data?.FirstOrDefault() : null;
        if (row == null)
            return Ok(ApiResponse<object?>.Fail($"计划不存在：{req.Code}"));

        if (row.Status == "closed")
            return Ok(ApiResponse<object?>.Ok(new { Code = row.Code, Status = row.Status }));

        // 队列正在写库时不允许关闭：关闭会让页面立刻只读，而队列仍在后台落库
        var running = await _taskSvc.GetListAsync(t =>
            t.PlanCode == row.Code && t.Status == "running" && !t.IsDeleted);
        var runningTask = running.Success ? running.Data?.FirstOrDefault() : null;
        if (runningTask != null)
            return Ok(ApiResponse<object?>.Fail(
                $"计划下还有任务在跑队列（{runningTask.TaskNo}），请等队列完成后再关闭"));

        row.Status = "closed";
        var upd = await UpdateCore(row);
        if (!upd.Success)
            return Ok(ApiResponse<object?>.Fail(upd.Error ?? "关闭计划失败"));

        return Ok(ApiResponse<object?>.Ok(new { Code = row.Code, Status = row.Status }));
    }
}

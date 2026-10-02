using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Controllers;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Result;
using CertPlatform.Auditor.Services;
using CertPlatform.Auditor.Services.Expert;
using CertPlatform.Shared.Entities.Cert;
using CertPlatform.Shared.Entities.Expert;

namespace CertPlatform.Auditor.Controllers;

// ══════════════════════════════════════════════════════════════════════════
// 契约（PascalCase —— YZH 铁律七）
// ══════════════════════════════════════════════════════════════════════════

/// <summary>结果左树节点（企业 → 阶段 → 任务，<b>恒 3 级</b>，标准不进树）</summary>
public class ResultTreeNode
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ParentCode { get; set; }

    /// <summary>enterprise | stage | task</summary>
    public string NodeType { get; set; } = string.Empty;

    public bool IsLeaf { get; set; }

    /// <summary>任务节点才有：任务类型</summary>
    public string? TaskType { get; set; }

    /// <summary>任务节点才有：执行状态</summary>
    public string? ExecStatus { get; set; }

    /// <summary>待审批结论数（任务节点角标）</summary>
    public int PendingCount { get; set; }

    /// <summary>结论总数（任务节点角标）</summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// 子节点（★ 仅 <c>Full=true</c> 的整树模式填充）。
    /// <para>懒加载模式下恒为 null —— 前端 <c>YzhTree</c> 只在整树模式才需要它。</para>
    /// </summary>
    public List<ResultTreeNode>? Children { get; set; }
}

/// <summary>结果行（右表）</summary>
public class ResultRowDto
{
    public string Code { get; set; } = string.Empty;
    public string ItemCode { get; set; } = string.Empty;
    public int RoundNo { get; set; }

    /// <summary>★ 2026-09-30：所属任务 Code —— 前端「去补录」用它拼路由（<c>/tasks/:code?tab=gaps</c>）</summary>
    public string? TaskCode { get; set; }

    /// <summary>条款号（NC）</summary>
    public string? ClauseNumber { get; set; }
    public string? ClauseTitle { get; set; }

    /// <summary>检查项名（NC = 规则名 / 报告 = 章节名）</summary>
    public string ItemName { get; set; } = string.Empty;

    public string StandardCode { get; set; } = string.Empty;
    public string StandardName { get; set; } = string.Empty;

    /// <summary>★ 自动结果：none | ok | ng | skipped | failed | degraded</summary>
    public string AutoStatus { get; set; } = ExpertTaskConst.Auto.None;

    /// <summary>自动判定说明（原始输出）</summary>
    public string? AutoDescription { get; set; }

    public string? AutoSeverity { get; set; }
    public decimal? AutoConfidence { get; set; }

    /// <summary>★ 专家结论：conform | nonconform | observation | na（机器永不写）</summary>
    public string? Conformity { get; set; }

    /// <summary>★ 严重度：major | minor | observation</summary>
    public string? Severity { get; set; }

    /// <summary>不符合描述（NC） / 章节正文（报告）</summary>
    public string? ContentText { get; set; }

    /// <summary>客观证据引用（NC）</summary>
    public string? EvidenceRef { get; set; }

    /// <summary>跳过分类 + 原因（界面必须完整展示）</summary>
    public string? SkipCategory { get; set; }
    public string? SkipReason { get; set; }

    /// <summary>★ 复核状态（结论级，D27 第 2 层的真正落点）</summary>
    public string ReviewStatus { get; set; } = ExpertTaskConst.Review.NotStarted;

    /// <summary>★ 结论来源：是否被专家改过</summary>
    public bool IsModified { get; set; }

    public string? ReviewName { get; set; }
    public DateTime? ReviewTime { get; set; }
    public string? ReviewRemark { get; set; }

    /// <summary>★ 责任部门（D-E：列先加，数据先空，显示「—」）</summary>
    public string? ResponsibleDept { get; set; }

    /// <summary>★ 报告专用：生成方式三档（自动生成 / 引用 NC 结果 / 人工撰写）</summary>
    public string? GenerationMode { get; set; }

    /// <summary>本轮生成时间</summary>
    public DateTime CreateTime { get; set; }

    /// <summary>关联的引擎执行任务（可下钻溯源）</summary>
    public string? ExecutionTaskCode { get; set; }

    /// <summary>★ 界面直接可用的结论文本（Conformity 为空时回落到自动结果的人话）</summary>
    public string ConclusionLabel { get; set; } = string.Empty;

    /// <summary>★ 结论来源标签：自动 / 人工修改</summary>
    public string SourceLabel { get; set; } = string.Empty;
}

public class ResultTreeRequest
{
    /// <summary>nc | report</summary>
    public string Type { get; set; } = "nc";

    /// <summary>父节点 Code（空 = 取根）</summary>
    public string? ParentCode { get; set; }

    /// <summary>节点类型（enterprise | stage）</summary>
    public string? NodeType { get; set; }

    /// <summary>
    /// ★ 整树模式：一次返回「企业 → 阶段 → 任务」三层嵌套（<c>Children</c> 填充）。
    /// <para><b>为什么需要它</b>：本工作区的企业/阶段/任务数量是<b>个位数量级</b>，
    /// 一次取回比懒加载少 N×M 次往返；且前端 <c>YzhTree</c> 的懒加载依赖
    /// el-tree 的 <c>load</c> 契约，整树模式让左树渲染与「点企业/阶段不发表格请求」
    /// 这条业务规则完全解耦（无网络竞态）。</para>
    /// </summary>
    public bool Full { get; set; }
}

public class ResultListRequest
{
    public string Type { get; set; } = "nc";
    public string? TaskCode { get; set; }
    public string? StandardCode { get; set; }
    public string? ReviewStatus { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class ResultAckRequest
{
    public string Type { get; set; } = "nc";

    /// <summary>结果行 Code 列表</summary>
    public List<string> Codes { get; set; } = new();

    public string? Remark { get; set; }
}

public class ResultModifyRequest
{
    public string Type { get; set; } = "nc";
    public string Code { get; set; } = string.Empty;

    public string? Conformity { get; set; }
    public string? Severity { get; set; }
    public string? ContentText { get; set; }
    public string? EvidenceRef { get; set; }
    public string? ReviewRemark { get; set; }
}

public class ResultHistoryRequest
{
    public string Type { get; set; } = "nc";

    /// <summary>实体层行 Code</summary>
    public string ItemCode { get; set; } = string.Empty;
}

/// <summary>
/// 结果控制器（专家端 —— 结果菜单：NC 检查结果 / 报告结论）
///
/// <para><b>★ 与任务系统的分工（D31）</b>：任务系统负责「建 → 找 → 跑队列」，
/// 本控制器负责「<b>审批 + 导出</b>」。两边的数据都是同一批结果行，
/// 但<b>动作完全不同</b>，所以拆成两个控制器。</para>
///
/// <para><b>形态</b>：左树右表。左树 <b>企业 → 阶段 → 任务</b>（恒 3 级，标准不进树）；
/// 右表是结论列表，支持<b>勾选批量审批</b> / <b>单行直接修改</b> / <b>整体导出</b>。</para>
///
/// <para><b>⛔ 点「企业」或「阶段」节点 → 右表留空，不发请求</b>（用户 2026-09-30 裁决）。</para>
///
/// <para><b>★ 为什么继承 <see cref="WebControllerBase"/> 而不是裸 <c>ControllerBase</c></b>：
/// 本控制器<b>没有单一实体</b>（同时读任务 / 队列项 / NC 结果 / 报告结果 4 张表），
/// 用不了 <c>YzhControllerBase&lt;V&gt;</c>；裸 <c>ControllerBase</c> 则不带
/// <c>[YZHAuthorize]</c>（守卫 R-B 也会拦）。<c>WebControllerBase</c> = <c>ControllerBase</c>
/// + <c>[ApiController]</c> + <c>[YZHAuthorize]</c>，是框架给「无实体业务控制器」的正解
/// （先例：<c>EnterpriseFileController</c>）。</para>
/// </summary>
[ApiController]
[Route("api/Auditor/[controller]")]
public class ExpertResultController : WebControllerBase
{
    private readonly IDbOrm _db;
    private readonly WorkspaceContextService _workspace;
    private readonly IUserContext _user;
    private readonly ExpertTaskService _taskSvc;

    public ExpertResultController(
        IDbOrm db, WorkspaceContextService workspace, IUserContext user, ExpertTaskService taskSvc)
    {
        _db = db;
        _workspace = workspace;
        _user = user;
        _taskSvc = taskSvc;
    }

    // ========================================================
    // 一、左树（企业 → 阶段 → 任务）
    // ========================================================

    /// <summary>
    /// 左树节点。POST <c>api/Auditor/ExpertResult/tree</c>
    /// <para>根（无 ParentCode）= 本工作区有任务的企业；展开企业 = 阶段；展开阶段 = 任务。</para>
    /// </summary>
    [HttpPost("tree")]
    public async Task<ActionResult<ApiResponse<List<ResultTreeNode>>>> Tree([FromBody] ResultTreeRequest req)
    {
        try
        {
            var ws = ResolveWorkspace();
            if (ws.Error != null) return Ok(ApiResponse<List<ResultTreeNode>>.Fail(ws.Error));

            var taskType = NormalizeType(req.Type);
            var nodes = new List<ResultTreeNode>();

            // ★ 整树模式：企业 → 阶段 → 任务（3 次查询搞定全树，不做逐任务统计）
            if (req.Full)
                return Ok(ApiResponse<List<ResultTreeNode>>.Ok(
                    await BuildFullTreeAsync(ws.Code!, taskType)));

            if (string.IsNullOrWhiteSpace(req.ParentCode))
            {
                // ── 根：有任务的企业 ──
                var tasks = (await _db.GetListAsync<CertExpertTask>(x =>
                    x.OrgCode == ws.Code! && x.TaskType == taskType && !x.IsDeleted)).Data
                    ?? new List<CertExpertTask>();

                foreach (var g in tasks.GroupBy(x => x.EnterpriseCode))
                {
                    nodes.Add(new ResultTreeNode
                    {
                        Code = g.Key,
                        Name = g.First().EnterpriseName ?? g.Key,
                        ParentCode = null,
                        NodeType = "enterprise",
                        IsLeaf = false,
                        TotalCount = g.Sum(x => x.TotalItemCount)
                    });
                }
            }
            else if (req.NodeType == "enterprise")
            {
                // ── 第二级：阶段 ──
                var tasks = (await _db.GetListAsync<CertExpertTask>(x =>
                    x.OrgCode == ws.Code! && x.TaskType == taskType
                    && x.EnterpriseCode == req.ParentCode && !x.IsDeleted)).Data
                    ?? new List<CertExpertTask>();

                foreach (var g in tasks.GroupBy(x => x.StageCode))
                {
                    nodes.Add(new ResultTreeNode
                    {
                        Code = g.Key,
                        Name = g.First().StageName ?? g.Key,
                        ParentCode = req.ParentCode,
                        NodeType = "stage",
                        IsLeaf = false,
                        TotalCount = g.Sum(x => x.TotalItemCount)
                    });
                }
            }
            else if (req.NodeType == "stage")
            {
                // ── 第三级：任务（叶子） ──
                var tasks = (await _db.GetListAsync<CertExpertTask>(x =>
                    x.OrgCode == ws.Code! && x.TaskType == taskType
                    && x.StageCode == req.ParentCode && !x.IsDeleted)).Data
                    ?? new List<CertExpertTask>();

                // 待审批数（角标）
                foreach (var t in tasks.OrderByDescending(x => x.CreateTime))
                {
                    var (pending, total) = await CountPendingAsync(ws.Code!, t);
                    nodes.Add(new ResultTreeNode
                    {
                        Code = t.Code ?? "",
                        Name = $"{t.TaskNumber} {t.TaskName}",
                        ParentCode = req.ParentCode,
                        NodeType = "task",
                        IsLeaf = true,
                        TaskType = t.TaskType,
                        ExecStatus = t.ExecStatus,
                        PendingCount = pending,
                        TotalCount = total
                    });
                }
            }

            return Ok(ApiResponse<List<ResultTreeNode>>.Ok(nodes));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<List<ResultTreeNode>>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// 整树构建（企业 → 阶段 → 任务，恒 3 级）。
    ///
    /// <para><b>★ 只发 3 次查询</b>：任务 / 队列项 / 结论 各一次，在内存里 group ——
    /// 避免「逐任务 CountPendingAsync」的 N×2 次往返。数据量是专家工作区级
    /// （企业×阶段×任务 = 个位到十位数），完全可接受。</para>
    /// </summary>
    private async Task<List<ResultTreeNode>> BuildFullTreeAsync(string orgCode, string taskType)
    {
        var tasks = (await _db.GetListAsync<CertExpertTask>(x =>
            x.OrgCode == orgCode && x.TaskType == taskType && !x.IsDeleted)).Data
            ?? new List<CertExpertTask>();
        if (tasks.Count == 0) return new List<ResultTreeNode>();

        var taskCodes = tasks.Select(x => x.Code ?? "").Where(x => x.Length > 0).ToList();

        var items = (await _db.GetListAsync<CertExpertTaskQueueItem>(x =>
            taskCodes.Contains(x.TaskCode ?? "") && !x.IsDeleted)).Data
            ?? new List<CertExpertTaskQueueItem>();

        var itemCodes = items.Select(x => x.TaskItemCode).Distinct().ToList();

        // 结论统计：{ (任务Code, 实体层行Code) → (待审批, 总数) }
        //
        // ★★ 键必须**带上 TaskCode**：检查项是长期实体，同一 `ItemCode` 会被多轮任务反复执行，
        //    结果表里同一 ItemCode 有多行（按 RoundNo 区分）。
        //    ⛔ 2026-09-30 实测踩坑：只按 ItemCode 建键时，**每个任务算出来的都是全量合计**
        //    ⇒ 左树每个任务节点显示的「待审批/总数」全都一样，且都比真实值大。
        var stat = new Dictionary<(string TaskCode, string ItemCode), (int Pending, int Total)>();
        if (itemCodes.Count > 0)
        {
            if (taskType == ExpertTaskConst.TaskTypeNcCheck)
            {
                var rs = (await _db.GetListAsync<CertExpertNcResult>(x =>
                    x.OrgCode == orgCode && itemCodes.Contains(x.ItemCode) && !x.IsDeleted)).Data
                    ?? new List<CertExpertNcResult>();
                foreach (var g in rs.GroupBy(x => (x.TaskCode ?? "", x.ItemCode)))
                    stat[g.Key] = (g.Count(x => x.ReviewStatus == ExpertTaskConst.Review.PendingReview), g.Count());
            }
            else
            {
                var rs = (await _db.GetListAsync<CertExpertReportResult>(x =>
                    x.OrgCode == orgCode && itemCodes.Contains(x.ItemCode) && !x.IsDeleted)).Data
                    ?? new List<CertExpertReportResult>();
                foreach (var g in rs.GroupBy(x => (x.TaskCode ?? "", x.ItemCode)))
                    stat[g.Key] = (g.Count(x => x.ReviewStatus == ExpertTaskConst.Review.PendingReview), g.Count());
            }
        }

        // 任务 Code → (待审批, 总数)
        var taskStat = items
            .GroupBy(x => x.TaskCode ?? "")
            .ToDictionary(g => g.Key, g =>
            {
                var codes = g.Select(x => x.TaskItemCode).Distinct();
                var p = 0; var t = 0;
                foreach (var c in codes)
                {
                    if (stat.TryGetValue((g.Key, c), out var s)) { p += s.Pending; t += s.Total; }
                }
                return (Pending: p, Total: t);
            });

        var result = new List<ResultTreeNode>();
        foreach (var ent in tasks.GroupBy(x => x.EnterpriseCode))
        {
            var entNode = new ResultTreeNode
            {
                Code = ent.Key,
                Name = ent.First().EnterpriseName ?? ent.Key,
                ParentCode = null,
                NodeType = "enterprise",
                IsLeaf = false,
                Children = new List<ResultTreeNode>()
            };

            foreach (var st in ent.GroupBy(x => x.StageCode))
            {
                var stNode = new ResultTreeNode
                {
                    Code = st.Key,
                    Name = st.First().StageName ?? st.Key,
                    ParentCode = ent.Key,
                    NodeType = "stage",
                    IsLeaf = false,
                    Children = new List<ResultTreeNode>()
                };

                foreach (var t in st.OrderByDescending(x => x.CreateTime))
                {
                    var (p, total) = taskStat.GetValueOrDefault(t.Code ?? "", (0, 0));
                    stNode.Children.Add(new ResultTreeNode
                    {
                        Code = t.Code ?? "",
                        Name = $"{t.TaskNumber} {t.TaskName}",
                        ParentCode = st.Key,
                        NodeType = "task",
                        IsLeaf = true,
                        TaskType = t.TaskType,
                        ExecStatus = t.ExecStatus,
                        PendingCount = p,
                        TotalCount = total
                    });
                }

                // 阶段角标 = 该阶段下所有任务之和
                stNode.TotalCount = stNode.Children.Sum(x => x.TotalCount);
                stNode.PendingCount = stNode.Children.Sum(x => x.PendingCount);
                entNode.Children.Add(stNode);
            }

            entNode.TotalCount = entNode.Children.Sum(x => x.TotalCount);
            entNode.PendingCount = entNode.Children.Sum(x => x.PendingCount);
            result.Add(entNode);
        }

        return result;
    }

    /// <summary>统计某任务的结论总数与待审批数</summary>
    private async Task<(int Pending, int Total)> CountPendingAsync(string orgCode, CertExpertTask task)
    {
        var itemCodes = (await _db.GetListAsync<CertExpertTaskQueueItem>(x =>
            x.TaskCode == task.Code && !x.IsDeleted)).Data?
            .Select(x => x.TaskItemCode).Distinct().ToList() ?? new List<string>();

        if (itemCodes.Count == 0) return (0, 0);

        // ★ 必须带 TaskCode（检查项是长期实体，同一 ItemCode 有多轮结果）
        if (task.TaskType == ExpertTaskConst.TaskTypeNcCheck)
        {
            var rs = (await _db.GetListAsync<CertExpertNcResult>(x =>
                x.OrgCode == orgCode && x.TaskCode == task.Code
                && itemCodes.Contains(x.ItemCode) && !x.IsDeleted)).Data
                ?? new List<CertExpertNcResult>();
            return (rs.Count(x => x.ReviewStatus == ExpertTaskConst.Review.PendingReview), rs.Count);
        }
        else
        {
            var rs = (await _db.GetListAsync<CertExpertReportResult>(x =>
                x.OrgCode == orgCode && x.TaskCode == task.Code
                && itemCodes.Contains(x.ItemCode) && !x.IsDeleted)).Data
                ?? new List<CertExpertReportResult>();
            return (rs.Count(x => x.ReviewStatus == ExpertTaskConst.Review.PendingReview), rs.Count);
        }
    }

    // ========================================================
    // 二、右表（结论列表）
    // ========================================================

    /// <summary>
    /// 结论列表。POST <c>api/Auditor/ExpertResult/list</c>
    /// <para>⚠️ <c>TaskCode</c> 为空 → 返回空列表（不发全量查询）。这是「必须选到任务」的服务端落实。</para>
    /// </summary>
    [HttpPost("list")]
    public async Task<ActionResult<ApiResponse<object>>> List([FromBody] ResultListRequest req)
    {
        try
        {
            var ws = ResolveWorkspace();
            if (ws.Error != null) return Ok(ApiResponse<object>.Fail(ws.Error));

            if (string.IsNullOrWhiteSpace(req.TaskCode))
                return Ok(ApiResponse<object>.Ok(new { Rows = new List<ResultRowDto>(), Total = 0 }));

            // 归属校验：任务必须属于本工作区
            var task = (await _db.GetOneAsync<CertExpertTask>(x =>
                x.Code == req.TaskCode && x.OrgCode == ws.Code! && !x.IsDeleted)).Data;
            if (task == null) return Ok(ApiResponse<object>.Fail("任务不存在或不属于当前工作区"));

            var rows = req.Type == "report"
                ? await BuildReportRowsAsync(ws.Code!, task)
                : await BuildNcRowsAsync(ws.Code!, task);

            if (!string.IsNullOrWhiteSpace(req.StandardCode))
                rows = rows.Where(x => x.StandardCode == req.StandardCode).ToList();
            if (!string.IsNullOrWhiteSpace(req.ReviewStatus))
                rows = rows.Where(x => x.ReviewStatus == req.ReviewStatus).ToList();

            var total = rows.Count;
            var page = req.Page <= 0 ? 1 : req.Page;
            var size = req.PageSize <= 0 ? 20 : Math.Min(req.PageSize, 200);

            var paged = rows
                .OrderBy(x => x.StandardCode).ThenBy(x => x.ClauseNumber).ThenBy(x => x.ItemName)
                .Skip((page - 1) * size).Take(size).ToList();

            return Ok(ApiResponse<object>.Ok(new { Rows = paged, Total = total, Page = page, PageSize = size }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<object>.Fail(ex.Message));
        }
    }

    private async Task<List<ResultRowDto>> BuildNcRowsAsync(string orgCode, CertExpertTask task)
    {
        var itemCodes = (await _db.GetListAsync<CertExpertTaskQueueItem>(x =>
            x.TaskCode == task.Code && !x.IsDeleted)).Data?
            .Select(x => x.TaskItemCode).Distinct().ToList() ?? new List<string>();

        var rows = new List<ResultRowDto>();
        if (itemCodes.Count == 0) return rows;

        var entities = (await _db.GetListAsync<CertExpertNcItem>(x =>
            x.OrgCode == orgCode && itemCodes.Contains(x.Code ?? "") && !x.IsDeleted)).Data
            ?? new List<CertExpertNcItem>();
        var results = (await _db.GetListAsync<CertExpertNcResult>(x =>
            x.OrgCode == orgCode && x.TaskCode == task.Code
            && itemCodes.Contains(x.ItemCode) && !x.IsDeleted)).Data
            ?? new List<CertExpertNcResult>();

        var stdNames = await StandardNameMapAsync(results.Select(x => x.StandardCode));

        foreach (var e in entities)
        {
            // ★ 只取**本任务**在该检查项上的结论。
            //   ⛔ 不能再用 `e.CurrentResultCode` —— 那是「实体当前生效结果」指针，
            //   会被**更新的一轮任务**改写 ⇒ 打开旧任务时会看到新任务的结论。
            //   结果集已按 TaskCode 过滤，故直接按 ItemCode 取即可。
            var r = results.Where(x => x.ItemCode == e.Code)
                           .OrderByDescending(x => x.RoundNo).FirstOrDefault();

            rows.Add(new ResultRowDto
            {
                Code = r?.Code ?? "",
                ItemCode = e.Code ?? "",
                RoundNo = r?.RoundNo ?? 0,
                TaskCode = r?.TaskCode,
                ClauseNumber = e.ClauseNumber,
                ClauseTitle = e.ClauseTitle,
                ItemName = e.RuleName,
                StandardCode = e.StandardCode,
                StandardName = stdNames.GetValueOrDefault(e.StandardCode, e.StandardCode),
                AutoStatus = r?.AutoStatus ?? ExpertTaskConst.Auto.None,
                AutoDescription = r?.AutoDescription,
                AutoSeverity = r?.AutoSeverity,
                AutoConfidence = r?.AutoConfidence,
                Conformity = r?.Conformity,
                Severity = r?.Severity,
                ContentText = r?.ContentText,
                EvidenceRef = r?.EvidenceRef,
                SkipCategory = r?.SkipCategory,
                SkipReason = r?.SkipReason,
                ReviewStatus = r?.ReviewStatus ?? ExpertTaskConst.Review.NotStarted,
                IsModified = r?.IsModified ?? false,
                ReviewName = r?.ReviewName,
                ReviewTime = r?.ReviewTime,
                ReviewRemark = r?.ReviewRemark,
                ResponsibleDept = null, // D-E：列先加，数据先空，界面显示「—」
                GenerationMode = null,
                CreateTime = r?.CreateTime ?? e.CreateTime,
                ExecutionTaskCode = r?.ExecutionTaskCode,
                ConclusionLabel = ConformityLabel(r?.Conformity, r?.AutoStatus),
                SourceLabel = (r?.IsModified ?? false) ? "人工修改" : "自动"
            });
        }

        return rows;
    }

    private async Task<List<ResultRowDto>> BuildReportRowsAsync(string orgCode, CertExpertTask task)
    {
        var itemCodes = (await _db.GetListAsync<CertExpertTaskQueueItem>(x =>
            x.TaskCode == task.Code && !x.IsDeleted)).Data?
            .Select(x => x.TaskItemCode).Distinct().ToList() ?? new List<string>();

        var rows = new List<ResultRowDto>();
        if (itemCodes.Count == 0) return rows;

        var entities = (await _db.GetListAsync<CertExpertReportSectionItem>(x =>
            x.OrgCode == orgCode && itemCodes.Contains(x.Code ?? "") && !x.IsDeleted)).Data
            ?? new List<CertExpertReportSectionItem>();
        var results = (await _db.GetListAsync<CertExpertReportResult>(x =>
            x.OrgCode == orgCode && x.TaskCode == task.Code
            && itemCodes.Contains(x.ItemCode) && !x.IsDeleted)).Data
            ?? new List<CertExpertReportResult>();

        var stdNames = await StandardNameMapAsync(results.Select(x => x.StandardCode));

        foreach (var e in entities)
        {
            // ★ 只取**本任务**的结论（理由同 BuildNcRowsAsync：CurrentResultCode 会被新轮次改写）
            var r = results.Where(x => x.ItemCode == e.Code)
                           .OrderByDescending(x => x.RoundNo).FirstOrDefault();

            rows.Add(new ResultRowDto
            {
                Code = r?.Code ?? "",
                ItemCode = e.Code ?? "",
                RoundNo = r?.RoundNo ?? 0,
                TaskCode = r?.TaskCode,
                ClauseNumber = e.ClauseNumber,
                ClauseTitle = e.ClauseTitle,
                ItemName = e.SectionName,
                StandardCode = e.StandardCode,
                StandardName = stdNames.GetValueOrDefault(e.StandardCode, e.StandardCode),
                AutoStatus = r?.AutoStatus ?? ExpertTaskConst.Auto.None,
                AutoDescription = null,
                AutoSeverity = null,
                AutoConfidence = r?.AutoConfidence,
                Conformity = null,
                Severity = null,
                // ⛔ 专家正文优先；为空才回落到自动内容（界面须标注来源）
                ContentText = !string.IsNullOrWhiteSpace(r?.ContentText)
                    ? r!.ContentText : r?.AutoContent,
                EvidenceRef = null,
                SkipCategory = r?.SkipCategory,
                SkipReason = r?.SkipReason,
                ReviewStatus = r?.ReviewStatus ?? ExpertTaskConst.Review.NotStarted,
                IsModified = r?.IsModified ?? false,
                ReviewName = r?.ReviewName,
                ReviewTime = r?.ReviewTime,
                ReviewRemark = r?.ReviewRemark,
                ResponsibleDept = null,
                GenerationMode = GenerationModeLabel(r),
                CreateTime = r?.CreateTime ?? e.CreateTime,
                ExecutionTaskCode = r?.ExecutionTaskCode,
                ConclusionLabel = ReportStatusLabel(r?.AutoStatus),
                SourceLabel = (r?.IsModified ?? false) ? "人工撰写"
                    : (!string.IsNullOrWhiteSpace(r?.AutoContent) ? "自动生成" : "待人工撰写")
            });
        }

        return rows;
    }

    /// <summary>NC 结论标签：专家结论优先，为空则回落自动结果的人话</summary>
    private static string ConformityLabel(string? conformity, string? autoStatus) => conformity switch
    {
        "conform" => "符合",
        "nonconform" => "不符合",
        "observation" => "观察项",
        "na" => "不适用",
        _ => autoStatus switch
        {
            ExpertTaskConst.Auto.Ok => "（自动）符合",
            ExpertTaskConst.Auto.Ng => "（自动）不符合",
            ExpertTaskConst.Auto.Skipped => "已跳过",
            ExpertTaskConst.Auto.Failed => "执行失败",
            _ => "待判定"
        }
    };

    /// <summary>报告状态标签</summary>
    private static string ReportStatusLabel(string? autoStatus) => autoStatus switch
    {
        ExpertTaskConst.Auto.Ok => "已生成",
        ExpertTaskConst.Auto.Degraded => "降级（模板示例）",
        ExpertTaskConst.Auto.Skipped => "已跳过",
        ExpertTaskConst.Auto.Failed => "生成失败",
        _ => "待生成"
    };

    /// <summary>★ 报告「生成方式」三档（D-F）—— 决定专家审核时的心理预期</summary>
    private static string? GenerationModeLabel(CertExpertReportResult? r)
    {
        if (r == null) return null;
        if (!string.IsNullOrWhiteSpace(r.ContentText)) return "人工撰写";
        if (!string.IsNullOrWhiteSpace(r.InheritFromCode)) return "引用上轮结果";
        if (!string.IsNullOrWhiteSpace(r.AutoContent)) return "自动生成";
        return null;
    }

    // ========================================================
    // 三、审批（批量认可）
    // ========================================================

    /// <summary>
    /// 批量认可。POST <c>api/Auditor/ExpertResult/acknowledge</c>
    /// <para>body <c>{ Type, Codes: [...] }</c> —— 勾选多条一次性认可。</para>
    /// </summary>
    [HttpPost("acknowledge")]
    public async Task<ActionResult<ApiResponse<object>>> Acknowledge([FromBody] ResultAckRequest req)
    {
        try
        {
            var ws = ResolveWorkspace();
            if (ws.Error != null) return Ok(ApiResponse<object>.Fail(ws.Error));

            if (req.Codes is not { Count: > 0 })
                return Ok(ApiResponse<object>.Fail("请先勾选要认可的结论"));

            var now = DateTime.Now;
            var name = DisplayName();
            var affected = 0;
            // ★ 受影响的任务（认可后需回写任务头/子任务的 AckedCount —— 否则该字段恒为 0）
            var touchedTasks = new HashSet<string>();

            if (req.Type == "report")
            {
                var rows = (await _db.GetListAsync<CertExpertReportResult>(x =>
                    x.OrgCode == ws.Code! && req.Codes.Contains(x.Code ?? "") && !x.IsDeleted)).Data
                    ?? new List<CertExpertReportResult>();

                foreach (var r in rows)
                {
                    r.ReviewStatus = ExpertTaskConst.Review.Reviewed;
                    r.ReviewBy = _user.UserCode;
                    r.ReviewName = name;
                    r.ReviewTime = now;
                    r.ReviewRemark = req.Remark;
                    r.UpdateTime = now;
                    await _db.UpdateAsync(r,
                        nameof(CertExpertReportResult.ReviewStatus),
                        nameof(CertExpertReportResult.ReviewBy),
                        nameof(CertExpertReportResult.ReviewName),
                        nameof(CertExpertReportResult.ReviewTime),
                        nameof(CertExpertReportResult.ReviewRemark),
                        nameof(CertExpertReportResult.UpdateTime));
                    if (!string.IsNullOrWhiteSpace(r.TaskCode)) touchedTasks.Add(r.TaskCode!);
                    affected++;
                }
            }
            else
            {
                var rows = (await _db.GetListAsync<CertExpertNcResult>(x =>
                    x.OrgCode == ws.Code! && req.Codes.Contains(x.Code ?? "") && !x.IsDeleted)).Data
                    ?? new List<CertExpertNcResult>();

                foreach (var r in rows)
                {
                    r.ReviewStatus = ExpertTaskConst.Review.Reviewed;
                    r.ReviewBy = _user.UserCode;
                    r.ReviewName = name;
                    r.ReviewTime = now;
                    r.ReviewRemark = req.Remark;
                    r.UpdateTime = now;
                    await _db.UpdateAsync(r,
                        nameof(CertExpertNcResult.ReviewStatus),
                        nameof(CertExpertNcResult.ReviewBy),
                        nameof(CertExpertNcResult.ReviewName),
                        nameof(CertExpertNcResult.ReviewTime),
                        nameof(CertExpertNcResult.ReviewRemark),
                        nameof(CertExpertNcResult.UpdateTime));
                    if (!string.IsNullOrWhiteSpace(r.TaskCode)) touchedTasks.Add(r.TaskCode!);
                    affected++;
                }
            }

            // ★ 回写任务头 / 标准子任务的复核计数（失败不影响认可结果，仅记录）
            foreach (var t in touchedTasks)
            {
                try { await _taskSvc.RefreshReviewCountersAsync(ws.Code!, t); }
                catch { /* 计数回写非关键路径，不能因它让认可失败 */ }
            }

            // 留痕（任务级动作，逐条记）
            if (req.Codes.Count > 0)
                await _taskSvc.InsertLogAsync(new CertExpertTaskLog
                {
                    OrgCode = ws.Code!,
                    LogAction = ExpertTaskConst.LogAction.Acknowledge,
                    LogLevel = ExpertTaskConst.LogLevel.Info,
                    Message = $"批量认可 {affected} 条结论",
                    IsAutoResult = true,
                    OperatorCode = _user.UserCode,
                    OperatorName = name,
                    ClientIp = _user.ClientIp,
                    OperateTime = now,
                    CreateTime = now
                });

            return Ok(ApiResponse<object>.Ok(new { Affected = affected },
                $"已认可 {affected} 条结论"));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<object>.Fail(ex.Message));
        }
    }

    // ========================================================
    // 四、修改（单行直接修改）
    // ========================================================

    /// <summary>
    /// 单行修改。POST <c>api/Auditor/ExpertResult/modify</c>
    /// <para>★ 改完 <c>ReviewStatus=modified</c> + <c>IsModified=true</c> ——
    /// 导出时「结论来源」列据此显示「人工修改」。</para>
    /// </summary>
    [HttpPost("modify")]
    public async Task<ActionResult<ApiResponse<object>>> Modify([FromBody] ResultModifyRequest req)
    {
        try
        {
            var ws = ResolveWorkspace();
            if (ws.Error != null) return Ok(ApiResponse<object>.Fail(ws.Error));
            if (string.IsNullOrWhiteSpace(req.Code))
                return Ok(ApiResponse<object>.Fail("缺少结论编码"));

            var now = DateTime.Now;
            var name = DisplayName();
            // ★ 受影响任务（修改后回写 ModifiedCount）
            string? touchedTask = null;

            if (req.Type == "report")
            {
                var r = (await _db.GetOneAsync<CertExpertReportResult>(x =>
                    x.Code == req.Code && x.OrgCode == ws.Code! && !x.IsDeleted)).Data;
                if (r == null) return Ok(ApiResponse<object>.Fail("结论不存在或不属于当前工作区"));

                var oldText = r.ContentText;
                r.ContentText = req.ContentText;
                r.ContentFormat = "plain";
                r.ReviewStatus = ExpertTaskConst.Review.Modified;
                r.IsModified = true;
                r.ReviewBy = _user.UserCode;
                r.ReviewName = name;
                r.ReviewTime = now;
                r.ReviewRemark = req.ReviewRemark;
                r.UpdateTime = now;
                touchedTask = r.TaskCode;

                var up = await _db.UpdateAsync(r,
                    nameof(CertExpertReportResult.ContentText),
                    nameof(CertExpertReportResult.ContentFormat),
                    nameof(CertExpertReportResult.ReviewStatus),
                    nameof(CertExpertReportResult.IsModified),
                    nameof(CertExpertReportResult.ReviewBy),
                    nameof(CertExpertReportResult.ReviewName),
                    nameof(CertExpertReportResult.ReviewTime),
                    nameof(CertExpertReportResult.ReviewRemark),
                    nameof(CertExpertReportResult.UpdateTime));
                if (!up.Success) return Ok(ApiResponse<object>.Fail(up.Error));

                await _taskSvc.InsertLogAsync(new CertExpertTaskLog
                {
                    OrgCode = ws.Code!,
                    TaskCode = r.TaskCode,
                    TaskItemCode = r.ItemCode,
                    ItemType = ExpertTaskConst.ItemType.ReportSection,
                    LogAction = ExpertTaskConst.LogAction.Modify,
                    LogLevel = ExpertTaskConst.LogLevel.Info,
                    FieldName = nameof(CertExpertReportResult.ContentText),
                    FieldLabel = "章节正文",
                    OldValueText = oldText,
                    NewValueText = r.ContentText,
                    BeforeStatus = ExpertTaskConst.Review.PendingReview,
                    AfterStatus = ExpertTaskConst.Review.Modified,
                    Message = "专家修改章节正文",
                    IsAutoResult = false,
                    OperatorCode = _user.UserCode,
                    OperatorName = name,
                    ClientIp = _user.ClientIp,
                    OperateTime = now,
                    CreateTime = now
                });
            }
            else
            {
                var r = (await _db.GetOneAsync<CertExpertNcResult>(x =>
                    x.Code == req.Code && x.OrgCode == ws.Code! && !x.IsDeleted)).Data;
                if (r == null) return Ok(ApiResponse<object>.Fail("结论不存在或不属于当前工作区"));

                var oldConformity = r.Conformity;
                var oldSeverity = r.Severity;
                var oldText = r.ContentText;

                r.Conformity = req.Conformity;
                r.Severity = req.Severity;
                r.ContentText = req.ContentText;
                r.EvidenceRef = req.EvidenceRef;
                r.ReviewStatus = ExpertTaskConst.Review.Modified;
                r.IsModified = true;
                r.ReviewBy = _user.UserCode;
                r.ReviewName = name;
                r.ReviewTime = now;
                r.ReviewRemark = req.ReviewRemark;
                r.UpdateTime = now;
                touchedTask = r.TaskCode;

                var up = await _db.UpdateAsync(r,
                    nameof(CertExpertNcResult.Conformity),
                    nameof(CertExpertNcResult.Severity),
                    nameof(CertExpertNcResult.ContentText),
                    nameof(CertExpertNcResult.EvidenceRef),
                    nameof(CertExpertNcResult.ReviewStatus),
                    nameof(CertExpertNcResult.IsModified),
                    nameof(CertExpertNcResult.ReviewBy),
                    nameof(CertExpertNcResult.ReviewName),
                    nameof(CertExpertNcResult.ReviewTime),
                    nameof(CertExpertNcResult.ReviewRemark),
                    nameof(CertExpertNcResult.UpdateTime));
                if (!up.Success) return Ok(ApiResponse<object>.Fail(up.Error));

                await _taskSvc.InsertLogAsync(new CertExpertTaskLog
                {
                    OrgCode = ws.Code!,
                    TaskCode = r.TaskCode,
                    TaskItemCode = r.ItemCode,
                    ItemType = ExpertTaskConst.ItemType.NcCheck,
                    LogAction = ExpertTaskConst.LogAction.Modify,
                    LogLevel = ExpertTaskConst.LogLevel.Info,
                    FieldName = "Conformity",
                    FieldLabel = "判定",
                    OldValue = oldConformity,
                    NewValue = r.Conformity,
                    OldValueText = oldText,
                    NewValueText = r.ContentText,
                    BeforeStatus = ExpertTaskConst.Review.PendingReview,
                    AfterStatus = ExpertTaskConst.Review.Modified,
                    Message = $"专家修改结论：{oldConformity ?? "—"} → {r.Conformity ?? "—"}"
                              + (oldSeverity != r.Severity ? $"（严重度 {oldSeverity ?? "—"} → {r.Severity ?? "—"}）" : ""),
                    IsAutoResult = false,
                    OperatorCode = _user.UserCode,
                    OperatorName = name,
                    ClientIp = _user.ClientIp,
                    OperateTime = now,
                    CreateTime = now
                });
            }

            // ★ 回写任务头 / 标准子任务的复核计数（非关键路径，失败不阻断保存）
            if (!string.IsNullOrWhiteSpace(touchedTask))
            {
                try { await _taskSvc.RefreshReviewCountersAsync(ws.Code!, touchedTask); }
                catch { /* 计数回写失败不影响修改结果 */ }
            }

            return Ok(ApiResponse<object>.Ok(new { Code = req.Code }, "已保存修改"));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<object>.Fail(ex.Message));
        }
    }

    // ========================================================
    // 五、历史轮次（下钻）
    // ========================================================

    /// <summary>某检查项/章节的历史轮次。POST <c>api/Auditor/ExpertResult/history</c></summary>
    [HttpPost("history")]
    public async Task<ActionResult<ApiResponse<object>>> History([FromBody] ResultHistoryRequest req)
    {
        try
        {
            var ws = ResolveWorkspace();
            if (ws.Error != null) return Ok(ApiResponse<object>.Fail(ws.Error));
            if (string.IsNullOrWhiteSpace(req.ItemCode))
                return Ok(ApiResponse<object>.Fail("缺少实体行编码"));

            if (req.Type == "report")
            {
                var rs = (await _db.GetListAsync<CertExpertReportResult>(x =>
                    x.OrgCode == ws.Code! && x.ItemCode == req.ItemCode && !x.IsDeleted)).Data
                    ?? new List<CertExpertReportResult>();
                var list = rs.OrderByDescending(x => x.RoundNo).Select(x => new
                {
                    x.Code, x.RoundNo, x.TaskCode, x.AutoStatus, x.AutoContent,
                    x.ContentText, x.ReviewStatus, x.IsModified, x.ReviewName,
                    x.ReviewTime, x.CreateTime, x.ExecutionTaskCode
                }).ToList();
                return Ok(ApiResponse<object>.Ok(list));
            }
            else
            {
                var rs = (await _db.GetListAsync<CertExpertNcResult>(x =>
                    x.OrgCode == ws.Code! && x.ItemCode == req.ItemCode && !x.IsDeleted)).Data
                    ?? new List<CertExpertNcResult>();
                var list = rs.OrderByDescending(x => x.RoundNo).Select(x => new
                {
                    x.Code, x.RoundNo, x.TaskCode, x.AutoStatus, x.AutoDescription,
                    x.AutoSeverity, x.Conformity, x.Severity, x.ContentText, x.EvidenceRef,
                    x.ReviewStatus, x.IsModified, x.ReviewName, x.ReviewTime,
                    x.CreateTime, x.ExecutionTaskCode
                }).ToList();
                return Ok(ApiResponse<object>.Ok(list));
            }
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<object>.Fail(ex.Message));
        }
    }

    // ========================================================
    // 六、整体导出
    // ========================================================

    /// <summary>
    /// 整体列表导出（CSV，Excel 可直接打开）。
    /// <para>POST <c>api/Auditor/ExpertResult/export</c>，返回 <c>text/csv</c> 文件流。</para>
    /// <para>★ 含「结论来源」列（自动 / 人工修改）—— 这是「辅助系统不替代正式报告」的显式证据。</para>
    /// </summary>
    [HttpPost("export")]
    public async Task<IActionResult> Export([FromBody] ResultListRequest req)
    {
        try
        {
            var ws = ResolveWorkspace();
            if (ws.Error != null) return Ok(ApiResponse.Fail(ws.Error));

            if (string.IsNullOrWhiteSpace(req.TaskCode))
                return Ok(ApiResponse.Fail("请先选择任务再导出"));

            var task = (await _db.GetOneAsync<CertExpertTask>(x =>
                x.Code == req.TaskCode && x.OrgCode == ws.Code! && !x.IsDeleted)).Data;
            if (task == null) return Ok(ApiResponse.Fail("任务不存在或不属于当前工作区"));

            var rows = req.Type == "report"
                ? await BuildReportRowsAsync(ws.Code!, task)
                : await BuildNcRowsAsync(ws.Code!, task);

            var sb = new StringBuilder();
            if (req.Type == "report")
            {
                sb.AppendLine("序号,章节号,章节名称,标准,生成方式,自动结果,复核状态,结论来源,正文摘要");
                var i = 1;
                foreach (var r in rows)
                {
                    sb.AppendLine(string.Join(',',
                        i++, Csv(r.ClauseNumber ?? r.RoundNo.ToString()), Csv(r.ItemName),
                        Csv(r.StandardName), Csv(r.GenerationMode ?? "—"),
                        Csv(r.ConclusionLabel), Csv(ReviewStatusLabel(r.ReviewStatus)),
                        Csv(r.SourceLabel), Csv(Truncate(r.ContentText, 200))));
                }
            }
            else
            {
                sb.AppendLine("序号,条款号,检查项,标准,判定,严重度,复核状态,责任部门,结论来源,不符合描述,客观证据,自动判定说明");
                var i = 1;
                foreach (var r in rows)
                {
                    sb.AppendLine(string.Join(',',
                        i++, Csv(r.ClauseNumber), Csv(r.ItemName), Csv(r.StandardName),
                        Csv(r.ConclusionLabel), Csv(SeverityLabel(r.Severity ?? r.AutoSeverity)),
                        Csv(ReviewStatusLabel(r.ReviewStatus)),
                        Csv(r.ResponsibleDept ?? "—"), Csv(r.SourceLabel),
                        Csv(Truncate(r.ContentText, 200)), Csv(Truncate(r.EvidenceRef, 200)),
                        Csv(Truncate(r.AutoDescription, 200))));
                }
            }

            // BOM：让 Excel 正确识别 UTF-8 中文
            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
            var kind = req.Type == "report" ? "报告结论" : "NC检查结果";
            var fileName = $"{kind}_{task.TaskNumber}_{DateTime.Now:yyyyMMddHHmmss}.csv";

            await _taskSvc.InsertLogAsync(new CertExpertTaskLog
            {
                OrgCode = ws.Code!,
                TaskCode = task.Code,
                LogAction = ExpertTaskConst.LogAction.Export,
                LogLevel = ExpertTaskConst.LogLevel.Info,
                Message = $"导出 {kind} {rows.Count} 条",
                IsAutoResult = true,
                OperatorCode = _user.UserCode,
                OperatorName = DisplayName(),
                ClientIp = _user.ClientIp,
                OperateTime = DateTime.Now,
                CreateTime = DateTime.Now
            });

            return File(bytes, "text/csv; charset=utf-8", fileName);
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse.Fail(ex.Message));
        }
    }

    // ========================================================
    // 七、辅助
    // ========================================================

    private (string? Code, string? Error) ResolveWorkspace()
    {
        var ws = _workspace.Resolve(_user.UserCode);
        if (!ws.Success || ws.Data == null)
            return (null, ws.Error ?? "无法定位当前工作区");
        return (ws.Data.Code, null);
    }

    private string DisplayName() =>
        string.IsNullOrWhiteSpace(_user.UserTrueName) ? _user.UserName : _user.UserTrueName!;

    /// <summary>请求 type → 任务类型</summary>
    private static string NormalizeType(string? type) =>
        string.Equals(type, "report", StringComparison.OrdinalIgnoreCase)
            ? ExpertTaskConst.TaskTypeReportGenerate
            : ExpertTaskConst.TaskTypeNcCheck;

    private async Task<Dictionary<string, string>> StandardNameMapAsync(IEnumerable<string> codes)
    {
        var list = codes.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
        if (list.Count == 0) return new Dictionary<string, string>();
        var stds = (await _db.GetListAsync<ISOStandard>(x => list.Contains(x.Code ?? ""))).Data
            ?? new List<ISOStandard>();
        return stds.GroupBy(x => x.Code ?? "")
            .ToDictionary(g => g.Key, g => g.First().StandardName ?? g.Key);
    }

    private static string ReviewStatusLabel(string? s) => s switch
    {
        ExpertTaskConst.Review.NotStarted => "未复核",
        ExpertTaskConst.Review.PendingReview => "待审批",
        ExpertTaskConst.Review.Reviewed => "已认可",
        ExpertTaskConst.Review.Modified => "已修改",
        ExpertTaskConst.Review.Skipped => "已跳过",
        ExpertTaskConst.Review.Frozen => "已冻结",
        _ => s ?? "—"
    };

    private static string SeverityLabel(string? s) => s switch
    {
        "major" => "严重不符合",
        "minor" => "一般不符合",
        "observation" => "观察项",
        _ => "—"
    };

    /// <summary>CSV 单元格转义（含逗号/引号/换行时加引号）</summary>
    private static string Csv(string? v)
    {
        if (string.IsNullOrEmpty(v)) return "";
        var s = v.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
        if (s.Contains(',') || s.Contains('"'))
            s = "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }

    private static string Truncate(string? s, int max) =>
        string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s[..max] + "…");
}

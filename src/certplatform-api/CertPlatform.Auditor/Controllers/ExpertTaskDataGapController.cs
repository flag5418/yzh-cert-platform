using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Controllers;
using YZH.Core.Api.Models;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Result;
using CertPlatform.Auditor.Services;
using CertPlatform.Auditor.Services.Expert;
using YZH.Core.DataBase.Interfaces;

namespace CertPlatform.Auditor.Controllers;

/// <summary>
/// ★ 补录清单控制器（2026-09-30 用户裁决 J1 / J2）
/// </summary>
///
/// <para><b>落库去向</b>：补录直接写 <c>cert_extraction_result</c> /
/// <c>cert_table_extraction_result</c>（<c>ValueSource='manual'</c>），
/// ⛔ 不建独立补录表（05 号 D06）。</para>
///
/// <para><b>★ 分组语义（裁决 J1「不分文档」）</b>：<c>list</c> 按
/// <see cref="GapGroupType.Field"/> / <see cref="GapGroupType.Table"/> 返回<b>两张扁平表</b>，
/// ⛔ 不按文档分组、不做文档树。<c>StandardFileCode</c> / <c>ExpectedFileName</c> 只是提示列。</para>
///
/// <para><b>★ 影响范围不落库</b>：<c>ImpactedItems</c> 由 <c>GapDetector.BuildImpactMap</c>
/// 按工作流 DAG <b>实时反查</b>（规则是业务配置、变动频繁，落库必然漂移）。</para>
///
/// <para><b>★ 提交不阻断</b>：本控制器<b>不提供</b>「校验缺口是否已处理」的硬门禁。
/// 清单是信息不是门禁，客户可以忽略；真正的门禁在执行期 —— 引用数据为空时
/// 任务项自动失败并提示「缺失必要数据」。</para>
/// <para><b>基类选择</b>：<see cref="WebControllerBase"/>（而非裸 <c>ControllerBase</c>，守卫 R-B）。
/// 缺口数据<strong>没有 EntityConfig</strong>（不是标准 CRUD 实体，字段随裁决演进），
/// 故不走 <c>YzhControllerBase&lt;V&gt;</c>；但仍需要它带的 <c>[YZHAuthorize]</c> 强认证。</para>
/// </remarks>
[Route("api/Auditor/ExpertTaskDataGap")]
public class ExpertTaskDataGapController : WebControllerBase
{
    private readonly IDbOrm _db;
    private readonly GapFillService _fill;
    private readonly GapDetector _detector;
    private readonly GapLabelResolver _labels;
    private readonly ExpertTaskService _tasks;
    private readonly WorkspaceContextService _workspace;
    private readonly IUserContext _user;

    public ExpertTaskDataGapController(
        IDbOrm db,
        GapFillService fill,
        GapDetector detector,
        GapLabelResolver labels,
        ExpertTaskService tasks,
        WorkspaceContextService workspace,
        IUserContext userContext)
    {
        _db = db;
        _fill = fill;
        _detector = detector;
        _labels = labels;
        _tasks = tasks;
        _workspace = workspace;
        _user = userContext;
    }

    private string DisplayName() =>
        string.IsNullOrWhiteSpace(_user.UserTrueName) ? _user.UserName : _user.UserTrueName!;

    private (string? Code, string? Error) ResolveWorkspace()
    {
        var ws = _workspace.Resolve(_user.UserCode);
        if (!ws.Success || ws.Data == null) return (null, ws.Error ?? "无法定位当前工作区");
        return (ws.Data.Code, null);
    }

    // ========================================================
    // 一、读取清单（两张扁平表）
    // ========================================================

    /// <summary>按任务读取补录清单。<c>POST api/Auditor/ExpertTaskDataGap/list</c></summary>
    [HttpPost("list")]
    public async Task<ActionResult<ApiResponse<object>>> List([FromBody] GapListRequest req)
    {
        try
        {
            var ws = ResolveWorkspace();
            if (ws.Error != null) return Ok(ApiResponse<object>.Fail(ws.Error));
            if (string.IsNullOrWhiteSpace(req.TaskCode))
                return Ok(ApiResponse<object>.Fail("TaskCode 不能为空"));

            var dtos = await BuildGapListAsync(ws.Code!, req.TaskCode);
            return Ok(ApiResponse<object>.Ok(ToPayload(dtos)));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<object>.Fail($"加载补录清单失败：{ex.Message}"));
        }
    }

    /// <summary>
    /// ★★ 启动前预检 —— 「关键信息补录」对话框的触发点（2026-10-07 用户裁决）
    /// <para><c>POST api/Auditor/ExpertTaskDataGap/precheck</c></para>
    /// </summary>
    ///
    /// <para><b>为什么需要它（用户逐字）</b>：</para>
    /// <code>
    /// 「我们的启动，如果发现缺失关键信息，应该是主动弹窗，将需要录入的信息，形成表单，
    ///   让客户进行填写……而且应该是补全相关信息才开始真正的任务，
    ///   而不是开启任务后，再让人去页面找，发现还有资料没有填写」
    /// </code>
    ///
    /// <para><b>与 <c>list</c> 的唯一差别</b>：本端点先<b>重算</b>缺口
    /// （<see cref="ExpertTaskService.GenerateGapsAsync"/>），再返回清单。
    /// 重算是必须的 —— 数据可能在向导创建之后被补录 / 换版 / 重新提取，缺口集合已变。</para>
    ///
    /// <para><b>★ 信封语义</b>：本端点是<b>查询</b>（回答「能不能启动、缺什么」），
    /// 查询成功即 <c>Ok</c>，⛔ 不是 <c>Fail</c>；真正的「拒绝启动」由前端依据
    /// <c>PendingCount &gt; 0</c> 决定是否弹窗，⛔ 不做硬阻断（用户裁决「运行跳过」）。</para>
    ///
    /// <para><b>返回体</b>：与 <c>list</c> 同构（<c>Fields</c> / <c>Tables</c> / 三个计数），
    /// 表格项额外带 <c>Columns</c>（中文列头）供补录表单渲染，
    /// 并带 <c>HasUnnamedItem</c> 提示「规则页没配中文名」。</para>
    [HttpPost("precheck")]
    public async Task<ActionResult<ApiResponse<object>>> Precheck([FromBody] GapListRequest req)
    {
        try
        {
            var ws = ResolveWorkspace();
            if (ws.Error != null) return Ok(ApiResponse<object>.Fail(ws.Error));
            if (string.IsNullOrWhiteSpace(req.TaskCode))
                return Ok(ApiResponse<object>.Fail("TaskCode 不能为空"));

            var task = (await _db.GetOneAsync<CertExpertTask>(x =>
                x.OrgCode == ws.Code && x.Code == req.TaskCode && !x.IsDeleted)).Data;
            if (task == null) return Ok(ApiResponse<object>.Fail("任务不存在或不属于当前工作区"));

            // ★ 重算缺口（与提交执行同一份逻辑，⛔ 不在控制器另写 R\H 差集）
            //   ⛔ 失败不静默吞：口径分叉会演变成「预检说齐了、执行说缺了」，必须让用户看见
            try
            {
                await _tasks.GenerateGapsAsync(task, ws.Code!);
            }
            catch (Exception ex)
            {
                return Ok(ApiResponse<object>.Fail($"关键信息检查失败：{ex.Message}"));
            }

            var dtos = await BuildGapListAsync(ws.Code!, req.TaskCode);
            return Ok(ApiResponse<object>.Ok(ToPayload(dtos)));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<object>.Fail($"关键信息检查失败：{ex.Message}"));
        }
    }

    // ========================================================
    // 二、补录
    // ========================================================

    /// <summary>补录单条。<c>PUT api/Auditor/ExpertTaskDataGap/{gapCode}/fill</c></summary>
    [HttpPut("{gapCode}/fill")]
    public async Task<ActionResult<ApiResponse<GapFillService.FillOutcome>>> Fill(
        string gapCode, [FromBody] GapFillRequest req)
    {
        try
        {
            var ws = ResolveWorkspace();
            if (ws.Error != null) return Ok(ApiResponse<GapFillService.FillOutcome>.Fail(ws.Error));

            var gap = (await _db.GetOneAsync<CertExpertTaskDataGap>(x =>
                x.OrgCode == ws.Code && x.Code == gapCode && !x.IsDeleted)).Data;
            if (gap == null) return Ok(ApiResponse<GapFillService.FillOutcome>.Fail("缺口不存在"));

            if (req.Value == null)
                return Ok(ApiResponse<GapFillService.FillOutcome>.Fail("补录值不能为空"));

            var r = await _fill.FillAsync(gap, req.Value, gap.TaskCode,
                _user.UserCode, DisplayName(), req.Remark);

            return r.Success
                ? Ok(ApiResponse<GapFillService.FillOutcome>.Ok(r.Data!, "补录成功"))
                : Ok(ApiResponse<GapFillService.FillOutcome>.Fail(r.Error!));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<GapFillService.FillOutcome>.Fail($"补录失败：{ex.Message}"));
        }
    }

    /// <summary>批量补录（单事务，上限 200）。<c>POST api/Auditor/ExpertTaskDataGap/batch-fill</c></summary>
    [HttpPost("batch-fill")]
    public async Task<ActionResult<ApiResponse<GapFillService.BatchFillResult>>> BatchFill(
        [FromBody] GapBatchFillRequest req)
    {
        try
        {
            var ws = ResolveWorkspace();
            if (ws.Error != null) return Ok(ApiResponse<GapFillService.BatchFillResult>.Fail(ws.Error));

            var items = (req.Items ?? new List<GapFillItem>())
                .Where(i => !string.IsNullOrWhiteSpace(i.GapCode))
                .Select(i => (i.GapCode!, i.Value ?? ""))
                .ToList();
            if (items.Count == 0)
                return Ok(ApiResponse<GapFillService.BatchFillResult>.Fail("没有要补录的项"));

            var gaps = (await _db.GetListAsync<CertExpertTaskDataGap>(x =>
                items.Select(i => i.Item1).Contains(x.Code!) && x.OrgCode == ws.Code && !x.IsDeleted)).Data
                ?? new List<CertExpertTaskDataGap>();
            var taskCode = gaps.FirstOrDefault()?.TaskCode;

            var r = await _fill.BatchFillAsync(items, taskCode, _user.UserCode, DisplayName(), req.Remark);
            return r.Success
                ? Ok(ApiResponse<GapFillService.BatchFillResult>.Ok(r.Data!, "批量补录成功"))
                : Ok(ApiResponse<GapFillService.BatchFillResult>.Fail(r.Error!));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<GapFillService.BatchFillResult>.Fail($"批量补录失败：{ex.Message}"));
        }
    }

    // ========================================================
    // 三、跳过（客户可以忽略 —— 裁决 J1）
    // ========================================================

    /// <summary>跳过单条缺口。<c>POST api/Auditor/ExpertTaskDataGap/{gapCode}/skip</c></summary>
    [HttpPost("{gapCode}/skip")]
    public async Task<ActionResult<ApiResponse<object>>> Skip(string gapCode, [FromBody] GapSkipRequest? req)
    {
        try
        {
            var ws = ResolveWorkspace();
            if (ws.Error != null) return Ok(ApiResponse<object>.Fail(ws.Error));

            var gap = (await _db.GetOneAsync<CertExpertTaskDataGap>(x =>
                x.OrgCode == ws.Code && x.Code == gapCode && !x.IsDeleted)).Data;
            if (gap == null) return Ok(ApiResponse<object>.Fail("缺口不存在"));
            if (gap.GapStatus != ExpertTaskConst.GapStatus.Pending)
                return Ok(ApiResponse<object>.Fail($"缺口已{(gap.GapStatus == ExpertTaskConst.GapStatus.Filled ? "补录" : "跳过")}，请刷新"));

            var now = DateTime.Now;
            gap.GapStatus = ExpertTaskConst.GapStatus.Skipped;
            gap.SkipBy = _user.UserCode;
            gap.SkipName = DisplayName();
            gap.SkipTime = now;
            gap.SkipReason = req?.Reason;
            gap.UpdateBy = _user.UserCode;
            gap.UpdateTime = now;

            var u = await _db.UpdateAsync(gap,
                nameof(CertExpertTaskDataGap.GapStatus),
                nameof(CertExpertTaskDataGap.SkipBy), nameof(CertExpertTaskDataGap.SkipName),
                nameof(CertExpertTaskDataGap.SkipTime), nameof(CertExpertTaskDataGap.SkipReason),
                nameof(CertExpertTaskDataGap.UpdateBy), nameof(CertExpertTaskDataGap.UpdateTime));
            if (!u.Success) return Ok(ApiResponse<object>.Fail(u.Error));

            await RefreshTaskGapCountAsync(gap.TaskCode ?? "");
            return Ok(ApiResponse<object>.Ok(new { gapCode }, "已跳过"));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<object>.Fail($"跳过失败：{ex.Message}"));
        }
    }

    /// <summary>跳过该任务的全部待处理缺口。<c>POST api/Auditor/ExpertTaskDataGap/skip-all</c></summary>
    [HttpPost("skip-all")]
    public async Task<ActionResult<ApiResponse<object>>> SkipAll([FromBody] GapListRequest req)
    {
        try
        {
            var ws = ResolveWorkspace();
            if (ws.Error != null) return Ok(ApiResponse<object>.Fail(ws.Error));

            var rows = (await _db.GetListAsync<CertExpertTaskDataGap>(x =>
                x.OrgCode == ws.Code
                && x.TaskCode == req.TaskCode
                && x.GapStatus == ExpertTaskConst.GapStatus.Pending
                && !x.IsDeleted)).Data ?? new List<CertExpertTaskDataGap>();

            if (rows.Count == 0) return Ok(ApiResponse<object>.Ok(new { skipped = 0 }, "没有待处理缺口"));

            var now = DateTime.Now;
            foreach (var g in rows)
            {
                g.GapStatus = ExpertTaskConst.GapStatus.Skipped;
                g.SkipBy = _user.UserCode;
                g.SkipName = DisplayName();
                g.SkipTime = now;
                g.SkipReason = req.Reason ?? "专家选择全部跳过";
                g.UpdateBy = _user.UserCode;
                g.UpdateTime = now;
                await _db.UpdateAsync(g,
                    nameof(CertExpertTaskDataGap.GapStatus),
                    nameof(CertExpertTaskDataGap.SkipBy), nameof(CertExpertTaskDataGap.SkipName),
                    nameof(CertExpertTaskDataGap.SkipTime), nameof(CertExpertTaskDataGap.SkipReason),
                    nameof(CertExpertTaskDataGap.UpdateBy), nameof(CertExpertTaskDataGap.UpdateTime));
            }

            await RefreshTaskGapCountAsync(req.TaskCode);
            return Ok(ApiResponse<object>.Ok(new { skipped = rows.Count },
                $"已跳过 {rows.Count} 项。依赖这些数据的检查项执行时会标记为「数据不足，未检查」"));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<object>.Fail($"批量跳过失败：{ex.Message}"));
        }
    }

    // ========================================================
    // 四、补录留痕（不可变表，只读）
    // ========================================================

    /// <summary>某任务下的补录留痕时间线。<c>POST api/Auditor/ExpertTaskDataGap/change-logs</c></summary>
    [HttpPost("change-logs")]
    public async Task<ActionResult<ApiResponse<List<object>>>> ChangeLogs([FromBody] GapListRequest req)
    {
        try
        {
            var ws = ResolveWorkspace();
            if (ws.Error != null) return Ok(ApiResponse<List<object>>.Fail(ws.Error));

            var rows = (await _db.GetListAsync<ExtractionChangeLog>(x =>
                x.OrgCode == ws.Code && x.TaskCode == req.TaskCode)).Data
                ?? new List<ExtractionChangeLog>();

            var list = rows.OrderByDescending(x => x.OperateTime ?? x.CreateTime)
                .Select(x => (object)new
                {
                    x.Code,
                    x.ResultType,
                    x.FieldLabel,
                    x.FieldCode,
                    x.TableCode,
                    x.ChangeAction,
                    OldValue = Truncate(x.OldValue),
                    NewValue = Truncate(x.NewValue),
                    x.OldValueSource,
                    x.NewValueSource,
                    x.OperatorName,
                    OperateTime = x.OperateTime ?? x.CreateTime,
                    x.Remark
                }).ToList();

            return Ok(ApiResponse<List<object>>.Ok(list));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<List<object>>.Fail($"加载补录留痕失败：{ex.Message}"));
        }
    }

    // ========================================================
    // 五、内部工具
    // ========================================================

    /// <summary>组装缺口清单 DTO（<c>list</c> / <c>precheck</c> 共用，⛔ 不写两份）</summary>
    private async Task<List<GapDto>> BuildGapListAsync(string orgCode, string taskCode)
    {
        var rows = (await _db.GetListAsync<CertExpertTaskDataGap>(x =>
            x.OrgCode == orgCode && x.TaskCode == taskCode && !x.IsDeleted)).Data
            ?? new List<CertExpertTaskDataGap>();

        // ★ 「影响 N 条规则」实时反查（⛔ 不落库）
        var impacts = await BuildImpactsAsync(orgCode, rows);

        // ★ 表格列定义（中文列头）—— 供「关键表格信息补录」Tab 渲染可编辑表格
        var columns = await _labels.ResolveTableColumnsAsync(rows
            .Where(x => x.GapType == GapGroupType.Table)
            .Select(x => x.TableCode));

        return rows
            .OrderBy(x => x.GapStatus == ExpertTaskConst.GapStatus.Pending ? 0 : 1)
            .ThenBy(x => x.GapType).ThenBy(x => x.GapLabel)
            .Select(x => ToDto(x, impacts, columns))
            .ToList();
    }

    /// <summary>清单信封载荷（两张扁平表 + 计数 + 中文名缺失提示）</summary>
    private static object ToPayload(List<GapDto> dtos) => new
    {
        // ★ 两张扁平表（裁决 J1「不分文档」）
        Fields = dtos.Where(d => d.GapType == GapGroupType.Field).ToList(),
        Tables = dtos.Where(d => d.GapType == GapGroupType.Table).ToList(),
        PendingCount = dtos.Count(d => d.GapStatus == ExpertTaskConst.GapStatus.Pending),
        FilledCount = dtos.Count(d => d.GapStatus == ExpertTaskConst.GapStatus.Filled),
        SkippedCount = dtos.Count(d => d.GapStatus == ExpertTaskConst.GapStatus.Skipped),
        // ★ 待补录项里存在「未命名」项 ⇒ 规则页没配中文名。
        //   界面须提示管理员去 /business/doc-extraction-rule 补，
        //   ⛔ 不要让审核员以为系统坏了（实测 cert_doc_field_def 可能整表只有 1 行）。
        HasUnnamedItem = dtos.Any(d =>
            d.GapStatus == ExpertTaskConst.GapStatus.Pending && d.IsUnnamed)
    };

    private async Task<Dictionary<string, List<GapDetector.GapImpact>>> BuildImpactsAsync(
        string orgCode, List<CertExpertTaskDataGap> rows)
    {
        var map = new Dictionary<string, List<GapDetector.GapImpact>>(StringComparer.Ordinal);
        if (rows.Count == 0) return map;

        // ★ 同一任务的缺口共享一份依赖列表（一次反查，避免逐条查库）
        var taskCode = rows[0].TaskCode ?? "";
        var deps = new List<GapDetector.DataDependency>();

        var itemTypes = rows.Select(x => x.SourceItemType).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct();
        foreach (var it in itemTypes)
        {
            var codes = rows.Where(x => x.SourceItemType == it)
                            .Select(x => x.SourceItemCode)
                            .Where(c => !string.IsNullOrWhiteSpace(c))
                            .Distinct().ToList();
            if (codes.Count == 0) continue;

            _detector.OrgCode = orgCode;
            deps.AddRange(await _detector.CollectDependenciesAsync(codes, it));
        }

        return GapDetector.BuildImpactMap(deps);
    }

    private GapDto ToDto(
        CertExpertTaskDataGap x,
        Dictionary<string, List<GapDetector.GapImpact>> impacts,
        Dictionary<string, List<GapLabelResolver.TableColumn>> columns)
    {
        var key = GapDetector.ImpactKey(x.GapType, x.RuleCode ?? "", x.FieldCode, x.TableCode);
        var hit = impacts.TryGetValue(key, out var list) ? list : new List<GapDetector.GapImpact>();

        // ★ 「未命名」= 规则页没登记中文名 ⇒ 界面提示管理员去补，⛔ 不是系统故障
        var isUnnamed = x.GapLabel == GapLabelResolver.UnnamedField
                        || x.GapLabel == GapLabelResolver.UnnamedTable;

        // ★ 表格列定义（中文列头）—— 让「关键表格信息补录」渲染可编辑表格，⛔ 不再手写 JSON
        var cols = x.GapType == GapGroupType.Table
                   && !string.IsNullOrWhiteSpace(x.TableCode)
                   && columns.TryGetValue(x.TableCode!, out var found)
            ? found.Select(c => new GapColumnDto
            {
                Code = c.Code,
                Name = c.Name,
                DataType = c.DataType
            }).ToList()
            : new List<GapColumnDto>();

        return new GapDto
        {
            Code = x.Code ?? "",
            GapType = x.GapType,
            GapLabel = x.GapLabel,
            // ★ J4 主键成分（前端补录时回传，服务端据此定位写入）
            RuleCode = x.RuleCode,
            FieldCode = x.FieldCode,
            TableCode = x.TableCode,
            // ★ 只读提示（裁决 J1「不分文档」：不作分组键）
            ExpectedFileName = x.ExpectedFileName,
            StandardFileCode = x.StandardFileCode,
            GapStatus = x.GapStatus,
            StandardCode = x.StandardCode,
            ClauseCode = x.ClauseCode,
            FilledValue = x.FilledValue,
            FilledName = x.FilledName,
            FilledTime = x.FilledTime,
            SkipName = x.SkipName,
            SkipTime = x.SkipTime,
            SkipReason = x.SkipReason,
            // ★ 中文名缺失（规则页未登记）⇒ 界面提示管理员
            IsUnnamed = isUnnamed,
            // ★ 表格列定义（中文列头；字段型恒空）
            Columns = cols,
            ImpactedItemCount = hit.Count,
            ImpactedItems = hit.Select(i => new GapImpactDto
            {
                ItemCode = i.ItemCode,
                ItemName = i.ItemName,
                ItemType = i.ItemType,
                ClauseCode = i.ClauseCode
            }).ToList()
        };
    }

    private async Task RefreshTaskGapCountAsync(string taskCode)
    {
        if (string.IsNullOrWhiteSpace(taskCode)) return;
        var task = (await _db.GetOneAsync<CertExpertTask>(x => x.Code == taskCode)).Data;
        if (task == null) return;

        task.GapCount = (await _db.CountAsync<CertExpertTaskDataGap>(x =>
            x.TaskCode == taskCode
            && x.GapStatus == ExpertTaskConst.GapStatus.Pending
            && !x.IsDeleted)).Data;
        task.UpdateTime = DateTime.Now;
        await _db.UpdateAsync(task, nameof(CertExpertTask.GapCount), nameof(CertExpertTask.UpdateTime));
    }

    private static string? Truncate(string? v, int n = 200)
    {
        if (string.IsNullOrEmpty(v)) return v;
        return v.Length <= n ? v : v[..n] + "…";
    }

    // ========================================================
    // 六、DTO
    // ========================================================

    public static class GapGroupType
    {
        public const string Field = "field";
        public const string Table = "table";
    }

    public class GapListRequest
    {
        public string TaskCode { get; set; } = "";
        public string? Reason { get; set; }
    }

    public class GapFillRequest
    {
        public string? Value { get; set; }
        public string? Remark { get; set; }
    }

    public class GapFillItem
    {
        public string GapCode { get; set; } = "";
        public string? Value { get; set; }
    }

    public class GapBatchFillRequest
    {
        public List<GapFillItem>? Items { get; set; }
        public string? Remark { get; set; }
    }

    public class GapSkipRequest
    {
        public string? Reason { get; set; }
    }

    public class GapImpactDto
    {
        public string ItemCode { get; set; } = "";
        public string ItemName { get; set; } = "";
        public string ItemType { get; set; } = "";
        public string? ClauseCode { get; set; }
    }

    public class GapDto
    {
        public string Code { get; set; } = "";
        public string GapType { get; set; } = "";
        public string GapLabel { get; set; } = "";
        public string? RuleCode { get; set; }
        public string? FieldCode { get; set; }
        public string? TableCode { get; set; }
        public string? ExpectedFileName { get; set; }
        public string? StandardFileCode { get; set; }
        public string GapStatus { get; set; } = "";
        public string? StandardCode { get; set; }
        public string? ClauseCode { get; set; }
        public string? FilledValue { get; set; }
        public string? FilledName { get; set; }
        public DateTime? FilledTime { get; set; }
        public string? SkipName { get; set; }
        public DateTime? SkipTime { get; set; }
        public string? SkipReason { get; set; }
        /// <summary>★ 中文名缺失（规则页未登记中文名）⇒ 界面提示管理员，⛔ 不是系统故障</summary>
        public bool IsUnnamed { get; set; }
        /// <summary>★ 表格列定义（中文列头；字段型恒空）—— 供「关键表格信息补录」渲染表单</summary>
        public List<GapColumnDto> Columns { get; set; } = new();
        /// <summary>★ 实时反查：影响几条检查项（⛔ 不落库）</summary>
        public int ImpactedItemCount { get; set; }
        public List<GapImpactDto> ImpactedItems { get; set; } = new();
    }

    /// <summary>表格列定义（中文列头 + 类型）—— 来自 <c>cert_doc_table_field_def</c></summary>
    public class GapColumnDto
    {
        /// <summary>列编码（英文驼峰，补录回写用）</summary>
        public string Code { get; set; } = "";
        /// <summary>列中文名（界面列头）</summary>
        public string Name { get; set; } = "";
        /// <summary><c>string</c> | <c>number</c> | <c>date</c></summary>
        public string DataType { get; set; } = "string";
    }
}

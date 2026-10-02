using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CertPlatform.Admin.Services.Workflow;
using CertPlatform.Admin.Services.Workflow.Models;
using CertPlatform.Shared.Entities.Cert;
using CertPlatform.Shared.Entities.Expert;
using CertPlatform.Shared.Entities.Rpt;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Models.Result;

namespace CertPlatform.Auditor.Services.Expert
{
    /// <summary>队列项执行结果</summary>
    public class ItemExecOutcome
    {
        public bool Success { get; set; }

        /// <summary>是否可重试（false = 永久错误，直接终态）</summary>
        public bool Retryable { get; set; } = true;

        public string Message { get; set; } = string.Empty;

        /// <summary>是否算「跳过」而非「失败」（跳过不计入失败数）</summary>
        public bool IsSkipped { get; set; }
    }

    /// <summary>
    /// 队列项执行器（专家任务专用）—— 按 <c>ItemType</c> 分发。
    ///
    /// <para><b>★ 为什么不实现框架的 <c>IYzhTaskExecutor</c></b>：那是 <c>yzh_queue</c> 的分发接口，
    /// 而专家任务按 D10 用<b>专用队列表</b>（跨租户泄露 H12 + ScopeKey 双口径）。
    /// 本接口与之<b>形状对齐、职责相同</b>，只是绑定的表不同。</para>
    /// </summary>
    public interface IExpertItemExecutor
    {
        /// <summary>支持的项类型（<c>nc_check</c> / <c>report_section</c>）</summary>
        string ItemType { get; }

        Task<ItemExecOutcome> ExecuteAsync(
            CertExpertTaskQueueItem item, ExpertItemPayload payload,
            IServiceProvider scoped, CancellationToken ct);
    }

    /// <summary>队列项载荷（<c>cert_expert_task_queue_item.Payload</c> 的结构）</summary>
    public class ExpertItemPayload
    {
        public string TaskCode { get; set; } = string.Empty;
        public string TaskItemCode { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public string? ItemName { get; set; }
        public string EnterpriseCode { get; set; } = string.Empty;
        public string StageCode { get; set; } = string.Empty;
        public string StandardCode { get; set; } = string.Empty;

        /// <summary>
        /// ★ 阶段<b>人读码</b>（= <c>cert_cert_stage.StageCode</c>，如 <c>03</c> / <c>AP</c>）。
        ///
        /// <para><b>用途：仅供前端展示</b>（<c>cert_expert_task_queue_item.Payload</c> 里带一份即可）。</para>
        ///
        /// <para>⛔ <b>2026-09-30 已不再是过滤键</b>：<c>wf_execution_task.PhaseCode</c> 由
        /// <c>varchar(30)</c> 扩到 <c>varchar(36)</c>（<c>fix-wf-phasecode-width-20260930.sql</c>），
        /// 现在<b>统一传 <see cref="StageCode"/>（GUID）</b>。原因：提取层
        /// <c>cert_extraction_result.StageCode</c> 存的是 GUID，
        /// <c>NodeExecutor</c> 拿短码去比 GUID ⇒ 永不命中 ⇒ docfield 节点恒抛
        /// 「缺失必要数据」（缺陷 B1）。</para>
        /// </summary>
        public string? StageNo { get; set; }

        public static ExpertItemPayload Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new ExpertItemPayload();
            try
            {
                return JsonSerializer.Deserialize<ExpertItemPayload>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? new ExpertItemPayload();
            }
            catch
            {
                return new ExpertItemPayload();
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // 一、NC 检查执行器
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// NC 检查执行器 —— 把「一条规则 + 一家企业」跑成一轮结果。
    ///
    /// <para><b>三条分支（顺序即优先级）</b>：</para>
    /// <list type="number">
    ///   <item><b><c>JudgeMode = manual</c> → 跳过（<c>manual_mode</c>）</b>：
    ///         现场观察 / 访谈类检查项，机器<b>不猜</b>，直接交人工。这是「人工/自动按审核方法划分」的落点。</item>
    ///   <item><b>无 DAG → 跳过（<c>no_rule</c>）</b>：规则没配工作流，跑不了，也<b>不算失败</b>。</item>
    ///   <item><b>有 DAG → 调工作流引擎</b>：<c>WfExecutionTaskService.CreateAndRunAsync</c>，
    ///         引擎产物（<c>wf_execution_task</c> / <c>wf_node_execution</c>）可下钻溯源。</item>
    /// </list>
    ///
    /// <para><b>★ 机器只写 <c>Auto*</c> 组字段</b>：<c>Conformity</c> / <c>Severity</c> /
    /// <c>ContentText</c> / <c>EvidenceRef</c> 是<b>专家结论</b>，机器永不写。
    /// 这保证「系统是辅助系统、不替代正式报告」在数据层可验证。</para>
    /// </summary>
    public class ExpertNcCheckExecutor : IExpertItemExecutor
    {
        public string ItemType => ExpertTaskConst.ItemType.NcCheck;

        private readonly ILogger<ExpertNcCheckExecutor> _logger;

        public ExpertNcCheckExecutor(ILogger<ExpertNcCheckExecutor> logger) => _logger = logger;

        public async Task<ItemExecOutcome> ExecuteAsync(
            CertExpertTaskQueueItem item, ExpertItemPayload p,
            IServiceProvider sp, CancellationToken ct)
        {
            var db = sp.GetRequiredService<IDbOrm>();

            var ncItem = (await db.GetOneAsync<CertExpertNcItem>(x => x.Code == p.TaskItemCode)).Data;
            if (ncItem == null)
                return new ItemExecOutcome
                {
                    Success = false,
                    Retryable = false,
                    Message = $"检查项实体不存在（TaskItemCode={p.TaskItemCode}）"
                };

            var rule = (await db.GetOneAsync<ValidationRule>(x => x.Code == p.ItemCode)).Data;
            if (rule == null)
                return new ItemExecOutcome
                {
                    Success = false,
                    Retryable = false,
                    Message = $"规则不存在（RuleCode={p.ItemCode}）"
                };

            // ── 分支 1：人工判定 ──
            if (string.Equals(rule.JudgeMode, "manual", StringComparison.OrdinalIgnoreCase))
            {
                var r1 = await WriteResultAsync(db, ncItem, item, new CertExpertNcResult
                {
                    AutoStatus = ExpertTaskConst.Auto.Skipped,
                    SkipCategory = ExpertTaskConst.SkipCategory.ManualMode,
                    SkipReason = "该检查项为人工判定（现场观察 / 访谈类），系统不自动出结论，请专家填写",
                    ReviewStatus = ExpertTaskConst.Review.PendingReview
                }, ct);

                return new ItemExecOutcome
                {
                    Success = true,
                    IsSkipped = true,
                    Message = "人工判定项，已跳过自动判定"
                };
            }

            // ── 分支 2：未配置 DAG ──
            if (string.IsNullOrWhiteSpace(rule.RuleJson))
            {
                await WriteResultAsync(db, ncItem, item, new CertExpertNcResult
                {
                    AutoStatus = ExpertTaskConst.Auto.Skipped,
                    SkipCategory = ExpertTaskConst.SkipCategory.NoRule,
                    SkipReason = "该规则未配置工作流（DAG），无法自动判定。请在后台「检查规则」中配置后重跑",
                    ReviewStatus = ExpertTaskConst.Review.PendingReview
                }, ct);

                return new ItemExecOutcome
                {
                    Success = true,
                    IsSkipped = true,
                    Message = "规则未配置工作流，已跳过"
                };
            }

            // ── 分支 3：跑工作流引擎 ──
            var wf = sp.GetRequiredService<WfExecutionTaskService>();
            TaskExecutionResponse resp;
            try
            {
                resp = await wf.CreateAndRunAsync(new TaskExecutionRequest
                {
                    TaskType = "NC_CHECK",
                    RuleCode = p.ItemCode,
                    EnterpriseCode = p.EnterpriseCode,
                    StandardCode = p.StandardCode,
                    // ★ 阶段口径（2026-09-30 修正 B1）：引擎层与提取层【统一传 GUID】。
                    //   wf_execution_task.PhaseCode 已由 varchar(30) 扩到 varchar(36)，装得下 32 位 GUID。
                    //   原先传人读短码 '03'，而 cert_extraction_result.StageCode 存 GUID
                    //   ⇒ NodeExecutor 的 StageCode 过滤永不命中 ⇒ docfield 恒 not_found。
                    //   人读短码仍由 StageNo 带在 Payload 里供前端展示。
                    PhaseCode = p.StageCode,
                    ConfigJson = rule.RuleJson
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ExpertNc] 工作流执行异常 item={Item}", p.TaskItemCode);
                return new ItemExecOutcome
                {
                    Success = false,
                    Retryable = true,
                    Message = $"工作流执行异常：{ex.Message}"
                };
            }

            // ══════════════════════════════════════════════════════════════
            // ★★★ 分支 3.5：缺失必要数据（2026-09-30 裁决 J1 · 执行期门禁）
            //
            //   语义：引用的字段/表格没有【可用值】⇒ 这不是「不符合」，而是【无法判定】。
            //   ⛔ 绝不能产出一个看起来正常的结论（旧实现返回 ""，AI 拿着空值随机给结论）。
            //
            //   处理：① 任务项标为 skipped + data_gap（不算失败、不计入"符合"数）
            //        ② 实时补登一条 cert_expert_task_data_gap（兜底开启任务时的检测）
            //        ③ Retryable=false —— 重试没用，数据还是空的，补录后走「重跑」
            // ══════════════════════════════════════════════════════════════
            if (resp.ErrorCode == WorkflowErrorCodes.DataMissing && resp.MissingData != null)
            {
                await HandleDataMissingAsync(db, ncItem, item, p, resp);
                return new ItemExecOutcome
                {
                    Success = true,
                    IsSkipped = true,
                    Retryable = false,
                    Message = resp.Error ?? "缺失必要数据，已标记为「数据不足，未检查」"
                };
            }

            // ── 映射引擎结果 → 结果层（只写 Auto* 组）──
            var (autoStatus, description, severity) = MapNcResult(resp, rule);

            var result = new CertExpertNcResult
            {
                AutoStatus = autoStatus,
                AutoResult = SafeJson(resp.NcResult),
                AutoSeverity = severity,
                AutoDescription = description,
                AutoConfidence = null,
                ExecutionTaskCode = resp.TaskCode,
                AutoEvaluatedAt = DateTime.Now,
                ReviewStatus = ExpertTaskConst.Review.PendingReview,
                SkipCategory = autoStatus == ExpertTaskConst.Auto.Failed
                    ? ExpertTaskConst.SkipCategory.ExecFailed : null,
                SkipReason = autoStatus == ExpertTaskConst.Auto.Failed
                    ? Truncate(description, 1000) : null
            };

            await WriteResultAsync(db, ncItem, item, result, ct);

            return new ItemExecOutcome
            {
                Success = true,
                Message = autoStatus == ExpertTaskConst.Auto.Failed
                    ? "工作流执行失败（已记录原因，可重试）"
                    : $"自动判定完成：{Truncate(description, 80)}"
            };
        }

        /// <summary>
        /// ★ 缺失必要数据的处置（裁决 J1 的执行期门禁）。
        /// </summary>
        /// <para>① 任务项结果行标 <c>skipped</c> + <c>SkipCategory='data_gap'</c>
        /// （⛔ 不标 failed —— 缺数据不是专家的错，标 failed 会让失败数虚高、误导排查方向）；</para>
        /// <para>② 实时补登一条 <c>cert_expert_task_data_gap</c>。这是<b>兜底</b>：
        /// 开启任务时的 <c>GapDetector</c> 理论上已生成过，但规则可能在中途被改过、
        /// 或补录后数据又被重新提取冲掉，这里再兜一层；靠
        /// <c>uk_gap(TaskCode, GapKey)</c> 幂等，不会重复。</para>
        private async Task HandleDataMissingAsync(
            IDbOrm db, CertExpertNcItem ncItem, CertExpertTaskQueueItem item,
            ExpertItemPayload p, TaskExecutionResponse resp)
        {
            var md = resp.MissingData!;
            var now = DateTime.Now;
            var reason = Truncate(resp.Error, 1000);

            await WriteResultAsync(db, ncItem, item, new CertExpertNcResult
            {
                AutoStatus = ExpertTaskConst.Auto.Skipped,
                SkipCategory = ExpertTaskConst.SkipCategory.DataGap,
                SkipReason = reason,
                ReviewStatus = ExpertTaskConst.Review.PendingReview,
                ExecutionTaskCode = resp.TaskCode
            }, default);

            // 实时补登缺口
            try
            {
                var task = (await db.GetOneAsync<CertExpertTask>(x => x.Code == p.TaskCode)).Data;
                if (task == null) return;

                var gapKey = $"{md.DataKind}|{md.RuleCode ?? "-"}|"
                             + $"{(md.DataKind == "field" ? md.DataCode : "-")}|"
                             + $"{(md.DataKind == "table" ? md.DataCode : "-")}";

                var existed = (await db.GetListAsync<CertExpertTaskDataGap>(x =>
                    x.TaskCode == p.TaskCode && x.GapKey == gapKey && !x.IsDeleted)).Data
                    ?? new List<CertExpertTaskDataGap>();
                if (existed.Count > 0) return;   // 幂等

                var gap = new CertExpertTaskDataGap
                {
                    Code = Guid.NewGuid().ToString("N"),
                    OrgCode = task.OrgCode,
                    TaskCode = p.TaskCode,
                    SubTaskCode = "",
                    RuleCode = string.IsNullOrWhiteSpace(md.RuleCode) ? null : md.RuleCode,
                    EnterpriseCode = string.IsNullOrEmpty(md.EnterpriseCode) ? p.EnterpriseCode : md.EnterpriseCode!,
                    StandardCode = string.IsNullOrEmpty(md.StandardCode) ? p.StandardCode : md.StandardCode!,
                    StageCode = string.IsNullOrEmpty(md.StageCode) ? p.StageCode : md.StageCode!,
                    GapType = md.DataKind,
                    GapLabel = md.DataCode,
                    FieldCode = md.DataKind == "field" ? md.DataCode : null,
                    TableCode = md.DataKind == "table" ? md.DataCode : null,
                    StandardFileCode = md.StandardFileCode,
                    SourceItemType = ExpertTaskConst.ItemType.NcCheck,
                    SourceItemCode = p.ItemCode,
                    SourceItemName = p.ItemName,
                    GapStatus = ExpertTaskConst.GapStatus.Pending,
                    Sort = 0,
                    Status = "active",
                    CreateTime = now,
                    UpdateTime = now,
                    IsValid = 1,
                    IsDeleted = false
                };
                await db.InsertAsync(gap);

                await db.UpdateAsync(task, nameof(CertExpertTask.GapCount), nameof(CertExpertTask.UpdateTime));
                _logger.LogWarning(
                    "[ExpertNc] 缺失必要数据已补登缺口 task={Task} item={Item} kind={Kind} code={Code} reason={Reason}",
                    p.TaskCode, p.ItemCode, md.DataKind, md.DataCode, md.Reason);
            }
            catch (Exception ex)
            {
                // ⛔ 不让留痕失败影响主流程（缺口稍后由提交前重算补上）
                _logger.LogError(ex, "[ExpertNc] 缺口实时补登失败 task={Task}", p.TaskCode);
            }
        }

        /// <summary>
        /// 引擎结果 → (<c>AutoStatus</c>, 说明, 严重度)。
        /// <para>⚠️ 只做「执行是否成功」的判定 + 轻量关键词识别；
        /// <b>最终是否符合由专家确认</b>（<c>Conformity</c> 机器不写）。</para>
        /// </summary>
        private static (string AutoStatus, string Description, string? Severity) MapNcResult(
            TaskExecutionResponse resp, ValidationRule rule)
        {
            var nc = resp.NcResult ?? new Dictionary<string, object>();

            object? raw = null;
            if (nc.TryGetValue("result", out var rv)) raw = rv;
            var text = raw == null ? "" : (raw as string ?? JsonSerializer.Serialize(raw));

            var ok = resp.IsSuccess;
            if (!ok)
            {
                string err = nc.TryGetValue("error", out var ev) && ev != null
                    ? ev.ToString() ?? "" : (resp.Status == "failed" ? "工作流执行失败" : "未知错误");
                return (ExpertTaskConst.Auto.Failed, err, null);
            }

            // 轻量识别：结果串里出现「不符合 / 不满足 / NG」⇒ 记为 ng（仅作提示，非结论）
            var isNg = text.Contains("不符合") || text.Contains("不满足")
                       || text.Contains("nonconform", StringComparison.OrdinalIgnoreCase)
                       || text.Contains("\"ng\"", StringComparison.OrdinalIgnoreCase);

            return (
                isNg ? ExpertTaskConst.Auto.Ng : ExpertTaskConst.Auto.Ok,
                string.IsNullOrWhiteSpace(text) ? "工作流执行成功（无文本输出）" : text,
                isNg ? rule.SeverityIfViolated : null);
        }

        /// <summary>
        /// 写结果层一行 + 推进实体层跨轮次状态。
        /// <para><b>★ 轮次分配在事务内完成</b>：<c>RoundNo = RoundCount + 1</c>，
        /// 由唯一键 <c>uk_item_round</c> 兜底防并发重复。</para>
        /// </summary>
        private async Task<Result<bool>> WriteResultAsync(
            IDbOrm db, CertExpertNcItem ncItem, CertExpertTaskQueueItem item,
            CertExpertNcResult result, CancellationToken ct)
        {
            var now = DateTime.Now;

            using var tx = db.BeginTransaction();
            try
            {
                result.Code = Guid.NewGuid().ToString("N");
                result.OrgCode = ncItem.OrgCode;
                result.ItemCode = ncItem.Code ?? "";
                result.EnterpriseCode = ncItem.EnterpriseCode;
                result.StageCode = ncItem.StageCode;
                result.StandardCode = ncItem.StandardCode;
                result.RoundNo = ncItem.RoundCount + 1;
                result.TaskCode = item.TaskCode;
                result.Sort = 0;
                result.CreateTime = now;
                result.CreateBy = "system";
                result.CreateName = "系统自动判定";
                result.IsModified = false;
                result.IsValid = 1;
                result.IsDeleted = false;

                var ins = await db.InsertAsync(result);
                if (!ins.Success) { tx.Rollback(); return Result<bool>.Fail(ins.Error); }

                ncItem.CurrentResultCode = result.Code;
                ncItem.RoundCount = result.RoundNo;
                // ★ D30：只有「真的检查了」才更新上次检查时间
                ncItem.LastAuditedTime = now;
                ncItem.LastAuditedTaskCode = item.TaskCode;
                ncItem.UpdateTime = now;

                var up = await db.UpdateAsync(ncItem,
                    nameof(CertExpertNcItem.CurrentResultCode),
                    nameof(CertExpertNcItem.RoundCount),
                    nameof(CertExpertNcItem.LastAuditedTime),
                    nameof(CertExpertNcItem.LastAuditedTaskCode),
                    nameof(CertExpertNcItem.UpdateTime));
                if (!up.Success) { tx.Rollback(); return Result<bool>.Fail(up.Error); }

                tx.Commit();
                return Result<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                tx.Rollback();
                _logger.LogError(ex, "[ExpertNc] 写结果失败 item={Item}", item.Code);
                return Result<bool>.Fail(ex.Message);
            }
        }

        internal static string SafeJson(object? o)
        {
            try { return JsonSerializer.Serialize(o); }
            catch { return "{}"; }
        }

        internal static string Truncate(string? s, int max) =>
            string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s[..max] + "…");
    }

    // ══════════════════════════════════════════════════════════════════════
    // 二、报告章节执行器
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 报告章节执行器。
    ///
    /// <para><b>降级分支（<c>degraded</c>）</b>：章节未配 DAG 时，用配置层的
    /// <c>SectionContent</c>（模板示例正文）作为「参照」写入结果，
    /// <c>AutoStatus = degraded</c>。<b>⛔ 它不是 AI 生成的初稿</b> —— 界面必须明确标注，
    /// 否则专家会误以为系统已产出内容。</para>
    /// </summary>
    public class ExpertReportExecutor : IExpertItemExecutor
    {
        public string ItemType => ExpertTaskConst.ItemType.ReportSection;

        private readonly ILogger<ExpertReportExecutor> _logger;

        public ExpertReportExecutor(ILogger<ExpertReportExecutor> logger) => _logger = logger;

        public async Task<ItemExecOutcome> ExecuteAsync(
            CertExpertTaskQueueItem item, ExpertItemPayload p,
            IServiceProvider sp, CancellationToken ct)
        {
            var db = sp.GetRequiredService<IDbOrm>();

            var secItem = (await db.GetOneAsync<CertExpertReportSectionItem>(x =>
                x.Code == p.TaskItemCode)).Data;
            if (secItem == null)
                return new ItemExecOutcome
                {
                    Success = false,
                    Retryable = false,
                    Message = $"章节实体不存在（TaskItemCode={p.TaskItemCode}）"
                };

            var section = (await db.GetOneAsync<ReportSection>(x => x.Code == p.ItemCode)).Data;
            if (section == null)
                return new ItemExecOutcome
                {
                    Success = false,
                    Retryable = false,
                    Message = $"报告章节配置不存在（SectionCode={p.ItemCode}）"
                };

            string autoStatus;
            string? autoContent = null;
            string? skipCategory = null;
            string? skipReason = null;
            string? execTaskCode = null;
            string message;

            if (string.IsNullOrWhiteSpace(section.WorkflowConfig))
            {
                // 降级：用模板示例正文
                autoStatus = ExpertTaskConst.Auto.Degraded;
                autoContent = section.Content;
                skipCategory = ExpertTaskConst.SkipCategory.NoRule;
                skipReason = "该章节未配置工作流（DAG），已用「模板示例正文」降级填充；"
                             + "示例正文仅作参照，须由专家撰写正式内容";
                message = string.IsNullOrWhiteSpace(section.Content)
                    ? "章节未配工作流且无模板示例正文，已标记待人工撰写"
                    : "章节未配工作流，已用模板示例正文降级";
            }
            else
            {
                var wf = sp.GetRequiredService<WfExecutionTaskService>();
                TaskExecutionResponse resp;
                try
                {
                    resp = await wf.CreateAndRunAsync(new TaskExecutionRequest
                    {
                        TaskType = "REPORT_GENERATE",
                        RuleCode = p.ItemCode,
                        EnterpriseCode = p.EnterpriseCode,
                        StandardCode = p.StandardCode,
                        // ★ 统一传 GUID（同 NC 执行器；2026-09-30 修正 B1，PhaseCode 已扩到 varchar(36)）
                        PhaseCode = p.StageCode,
                        ConfigJson = section.WorkflowConfig
                    }, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[ExpertReport] 工作流执行异常 item={Item}", p.TaskItemCode);
                    return new ItemExecOutcome
                    {
                        Success = false,
                        Retryable = true,
                        Message = $"工作流执行异常：{ex.Message}"
                    };
                }

                execTaskCode = resp.TaskCode;
                if (resp.IsSuccess)
                {
                    autoStatus = ExpertTaskConst.Auto.Ok;
                    autoContent = ExtractText(resp);
                    message = "章节自动生成完成";
                }
                else
                {
                    autoStatus = ExpertTaskConst.Auto.Failed;
                    skipCategory = ExpertTaskConst.SkipCategory.ExecFailed;
                    skipReason = ExtractError(resp);
                    message = "章节生成失败（已记录原因，可重试）";
                }
            }

            var now = DateTime.Now;
            using var tx = db.BeginTransaction();
            try
            {
                var result = new CertExpertReportResult
                {
                    Code = Guid.NewGuid().ToString("N"),
                    OrgCode = secItem.OrgCode,
                    ItemCode = secItem.Code ?? "",
                    EnterpriseCode = secItem.EnterpriseCode,
                    StageCode = secItem.StageCode,
                    StandardCode = secItem.StandardCode,
                    RoundNo = secItem.RoundCount + 1,
                    TaskCode = item.TaskCode,
                    AutoStatus = autoStatus,
                    AutoContent = autoContent,
                    AutoResult = SafeJson(new { status = autoStatus }),
                    ExecutionTaskCode = execTaskCode,
                    AutoEvaluatedAt = now,
                    SkipCategory = skipCategory,
                    SkipReason = skipReason,
                    // ⛔ 专家结论字段留空 —— 机器永不写 ContentText
                    ContentFormat = "plain",
                    ReviewStatus = ExpertTaskConst.Review.PendingReview,
                    IsModified = false,
                    Sort = secItem.Sort,
                    CreateBy = "system",
                    CreateName = "系统自动生成",
                    CreateTime = now,
                    IsValid = 1,
                    IsDeleted = false
                };

                var ins = await db.InsertAsync(result);
                if (!ins.Success)
                {
                    tx.Rollback();
                    return new ItemExecOutcome { Success = false, Retryable = true, Message = ins.Error };
                }

                secItem.CurrentResultCode = result.Code;
                secItem.RoundCount = result.RoundNo;
                secItem.LastAuditedTime = now;
                secItem.LastAuditedTaskCode = item.TaskCode;
                secItem.UpdateTime = now;

                var up = await db.UpdateAsync(secItem,
                    nameof(CertExpertReportSectionItem.CurrentResultCode),
                    nameof(CertExpertReportSectionItem.RoundCount),
                    nameof(CertExpertReportSectionItem.LastAuditedTime),
                    nameof(CertExpertReportSectionItem.LastAuditedTaskCode),
                    nameof(CertExpertReportSectionItem.UpdateTime));
                if (!up.Success)
                {
                    tx.Rollback();
                    return new ItemExecOutcome { Success = false, Retryable = true, Message = up.Error };
                }

                tx.Commit();
            }
            catch (Exception ex)
            {
                tx.Rollback();
                _logger.LogError(ex, "[ExpertReport] 写结果失败 item={Item}", item.Code);
                return new ItemExecOutcome
                {
                    Success = false,
                    Retryable = true,
                    Message = ex.Message
                };
            }

            return new ItemExecOutcome
            {
                Success = true,
                IsSkipped = autoStatus is ExpertTaskConst.Auto.Degraded or ExpertTaskConst.Auto.Skipped,
                Message = message
            };
        }

        private static string? ExtractText(TaskExecutionResponse resp)
        {
            var nc = resp.NcResult;
            if (nc != null && nc.TryGetValue("result", out var rv) && rv != null)
                return rv as string ?? JsonSerializer.Serialize(rv);
            return null;
        }

        private static string ExtractError(TaskExecutionResponse resp)
        {
            var nc = resp.NcResult;
            if (nc != null && nc.TryGetValue("error", out var ev) && ev != null)
                return ev.ToString() ?? "工作流执行失败";
            return "工作流执行失败";
        }

        private static string SafeJson(object? o)
        {
            try { return JsonSerializer.Serialize(o); }
            catch { return "{}"; }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CertPlatform.Admin.Entities.Cert;
using CertPlatform.Shared.Entities.Rpt;
using Microsoft.Extensions.Logging;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Models.Result;

namespace CertPlatform.Auditor.Services.Expert
{
    // ══════════════════════════════════════════════════════════════════════
    // 请求 / 响应契约（PascalCase —— YZH 铁律七：C# 属性名 = JSON 字段名 = TS 字段名）
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>业务锁判定请求（向导第 1 步实时判定）</summary>
    public class LockCheckRequest
    {
        public string EnterpriseCode { get; set; } = string.Empty;
        public string StageCode { get; set; } = string.Empty;
        public string TaskType { get; set; } = string.Empty;

        /// <summary>排除自身（重跑/编辑场景）</summary>
        public string? ExcludeTaskCode { get; set; }
    }

    /// <summary>业务锁判定结果</summary>
    public class LockCheckResult
    {
        /// <summary>是否可以创建</summary>
        public bool CanCreate { get; set; }

        /// <summary>人话结论（直接显示给专家）</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>阻塞中的任务 Code（可空）</summary>
        public string? BlockingTaskCode { get; set; }

        public string? BlockingTaskNumber { get; set; }

        public string? BlockingTaskName { get; set; }

        /// <summary>阻塞任务的执行状态</summary>
        public string? BlockingExecStatus { get; set; }

        /// <summary>阻塞任务的创建时间</summary>
        public DateTime? BlockingCreateTime { get; set; }

        /// <summary>已存在的任务数（同企业+阶段+类型，含已结束）</summary>
        public int ExistingTaskCount { get; set; }

        /// <summary>生效检查项数（候选来源条数）</summary>
        public int CandidateCount { get; set; }

        /// <summary>历史平均轮次（沿用判定的参考）</summary>
        public decimal AvgRoundCount { get; set; }
    }

    /// <summary>创建任务请求（向导第 4 步提交）</summary>
    public class TaskCreateRequest
    {
        public string TaskName { get; set; } = string.Empty;
        public string TaskType { get; set; } = string.Empty;
        public string EnterpriseCode { get; set; } = string.Empty;
        public string StageCode { get; set; } = string.Empty;

        /// <summary>FULL | PARTIAL</summary>
        public string ScopeType { get; set; } = ExpertTaskConst.Scope.Full;

        /// <summary>NEW | REDO | PATCH</summary>
        public string TaskSource { get; set; } = ExpertTaskConst.TaskSource.New;

        /// <summary>选中的标准 Code 列表</summary>
        public List<string> StandardCodes { get; set; } = new();

        /// <summary>PARTIAL 时勾选的规则/章节 Code 列表（FULL 时忽略）</summary>
        public List<string>? ItemCodes { get; set; }

        /// <summary>备注</summary>
        public string? Remark { get; set; }
    }

    /// <summary>创建任务结果</summary>
    public class TaskCreateResult
    {
        public string TaskCode { get; set; } = string.Empty;
        public string TaskNumber { get; set; } = string.Empty;
        public string TaskName { get; set; } = string.Empty;

        /// <summary>创建的标准子任务数</summary>
        public int StandardCount { get; set; }

        /// <summary>创建/复活的检查项数</summary>
        public int ItemCount { get; set; }

        /// <summary>即将产生的队列数（= 标准数）</summary>
        public int QueueCount { get; set; }

        /// <summary>★ 2026-09-30：本次生成的补录清单条数（<c>R \ H</c> 差集，裁决 J1）
        /// <para>⛔ <b>不阻断</b>：清单是信息不是门禁，客户可以忽略（25 号 §6.2）。
        /// 真正的门禁在执行期 —— 引用数据为空时任务项自动失败并提示「缺失必要数据」。</para>
        /// </summary>
        public int GapCount { get; set; }

        /// <summary>人话摘要（界面直接显示）</summary>
        public string Summary { get; set; } = string.Empty;
    }

    /// <summary>候选检查项（向导第 3 步勾选表）</summary>
    public class TaskCandidateDto
    {
        public string StandardCode { get; set; } = string.Empty;
        public string StandardName { get; set; } = string.Empty;

        /// <summary>规则/章节业务键（= <c>cert_validation_rule.Code</c> / <c>cert_report_section.Code</c>）</summary>
        public string ItemCode { get; set; } = string.Empty;

        /// <summary>规则编号 / 章节号（展示用）</summary>
        public string ItemNumber { get; set; } = string.Empty;

        public string ItemName { get; set; } = string.Empty;

        public string? ClauseNumber { get; set; }
        public string? ClauseTitle { get; set; }

        /// <summary>auto | semi | manual（仅 NC）</summary>
        public string? JudgeMode { get; set; }

        public string? SeverityDefault { get; set; }

        /// <summary>★ 上次检查时间（D30：沿用项保留，界面须显示）</summary>
        public DateTime? LastAuditedTime { get; set; }

        /// <summary>★ 上次结论（复用上一轮，供专家判断是否需要重跑）</summary>
        public string? LastConclusion { get; set; }

        /// <summary>是否已配置工作流 DAG（未配置 ⇒ 执行时必跳过，界面须预警）</summary>
        public bool HasWorkflow { get; set; }

        /// <summary>系统建议是否勾选</summary>
        public bool Suggested { get; set; }
    }

    /// <summary>任务详情（详情页 4 Tab 的一次性载荷）</summary>
    public class TaskDetailDto
    {
        public CertExpertTask Task { get; set; } = new();

        /// <summary>企业 / 阶段 展示名补齐</summary>
        public string EnterpriseName { get; set; } = string.Empty;
        public string StageName { get; set; } = string.Empty;

        public List<TaskStandardDto> Standards { get; set; } = new();
        public List<TaskQueueDto> Queues { get; set; } = new();

        /// <summary>待审批结论数 / 结论总数</summary>
        public int PendingReviewCount { get; set; }
        public int TotalResultCount { get; set; }
    }

    /// <summary>标准子任务（详情 Tab 1）</summary>
    public class TaskStandardDto
    {
        public string Code { get; set; } = string.Empty;
        public string StandardCode { get; set; } = string.Empty;
        public string StandardName { get; set; } = string.Empty;
        public int ItemCount { get; set; }
        public int DoneCount { get; set; }
        public int FailedCount { get; set; }
        public int SkippedCount { get; set; }
        public string ExecStatus { get; set; } = string.Empty;
        public string LifecycleStatus { get; set; } = string.Empty;
        public string? QueueCode { get; set; }
        public string? QueueStatus { get; set; }
        public decimal Progress { get; set; }
    }

    /// <summary>执行队列（详情 Tab 2）</summary>
    public class TaskQueueDto
    {
        public string Code { get; set; } = string.Empty;
        public string StandardCode { get; set; } = string.Empty;
        public string StandardName { get; set; } = string.Empty;
        public string QueueType { get; set; } = string.Empty;
        public string QueueStatus { get; set; } = string.Empty;
        public int TotalCount { get; set; }
        public int DoneCount { get; set; }
        public int FailedCount { get; set; }
        public int SkippedCount { get; set; }
        public decimal Progress { get; set; }
        public int RetryCount { get; set; }
        public int MaxRetryCount { get; set; }
        public string? LockCode { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? FinishTime { get; set; }
        public string? LastError { get; set; }
        public bool CanStart { get; set; }
        public bool CanPause { get; set; }
    }

    /// <summary>运行日志行（详情 Tab 3）</summary>
    public class TaskLogDto
    {
        public string Code { get; set; } = string.Empty;
        public string LogAction { get; set; } = string.Empty;
        public string LogLevel { get; set; } = string.Empty;
        public string? Message { get; set; }
        public string? QueueCode { get; set; }
        public string? TaskItemCode { get; set; }
        public string? ItemType { get; set; }
        public string? ItemName { get; set; }
        public string? StandardCode { get; set; }
        public string? OperatorName { get; set; }
        public int? DurationMs { get; set; }
        public DateTime OperateTime { get; set; }
        public string? Payload { get; set; }
    }

    /// <summary>数据缺口行（详情 Tab 4）</summary>
    public class TaskGapDto
    {
        public string Code { get; set; } = string.Empty;
        public string GapType { get; set; } = string.Empty;
        public string GapLabel { get; set; } = string.Empty;
        public string? ExpectedFileName { get; set; }
        public string SourceItemName { get; set; } = string.Empty;
        public string SourceItemType { get; set; } = string.Empty;
        public string GapStatus { get; set; } = string.Empty;
        public string? StandardCode { get; set; }
        public string? ClauseCode { get; set; }
        public string? FilledValue { get; set; }
        public DateTime? FilledTime { get; set; }
        public DateTime? SkipTime { get; set; }
        public string? SkipReason { get; set; }
    }

    /// <summary>
    /// ★★ 未执行清单一行（2026-10-07 用户裁决 · 裁 2「运行跳过」）
    /// <para>用户逐字：「队列如果因为某些问题不能启动，不是任务暂停，而是任务失败，而且应该是
    /// 补全相关信息才开始真正的任务」+「再队列完成后，详细记录，哪些规则或条款未执行成功，什么原因」。</para>
    /// <para>本 DTO 就是那句「详细记录」的载体：<b>按规则/条款聚合</b>，
    /// 而不是按缺口逐条罗列 —— 审核员要看的是「哪条没跑、为什么」。</para>
    /// </summary>
    public class TaskUnexecutedDto
    {
        /// <summary>规则/章节业务键（<c>cert_expert_nc_item.Code</c> / <c>…report_section_item.Code</c>）</summary>
        public string ItemCode { get; set; } = string.Empty;

        /// <summary>★ 规则名 / 章节名（中文）</summary>
        public string ItemName { get; set; } = string.Empty;

        /// <summary><c>nc_check</c> | <c>report_section</c></summary>
        public string ItemType { get; set; } = string.Empty;

        /// <summary>关联条款编码</summary>
        public string? ClauseCode { get; set; }

        /// <summary>条款标题（中文，可为空）</summary>
        public string? ClauseTitle { get; set; }

        /// <summary>★ 未执行分类（英文枚举，前端用 <c>SkipCategoryLabel</c> 映射）</summary>
        public string SkipCategory { get; set; } = string.Empty;

        /// <summary>★ 未执行分类的中文名</summary>
        public string SkipCategoryLabel { get; set; } = string.Empty;

        /// <summary>★ 原因（人话，恒非空）</summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>★ 具体缺什么（字段/表格中文名 + 应来自哪个文件）</summary>
        public List<string> MissingItems { get; set; } = new();

        public string? StandardCode { get; set; }
    }

    /// <summary>未执行清单信封</summary>
    public class TaskUnexecutedResult
    {
        /// <summary>★ 按规则/条款聚合后的清单</summary>
        public List<TaskUnexecutedDto> Items { get; set; } = new();

        /// <summary>未执行成功的规则/条款条数</summary>
        public int TotalCount { get; set; }

        /// <summary>其中因「缺企业数据」导致的条数</summary>
        public int DataGapCount { get; set; }

        /// <summary>★ 队列是否已全部结束（未结束 ⇒ 清单还会变，界面须标注「执行中」）</summary>
        public bool IsQueueFinished { get; set; }

        /// <summary>任务执行状态（供界面判断是否已可查看最终清单）</summary>
        public string ExecStatus { get; set; } = string.Empty;
    }

    // ══════════════════════════════════════════════════════════════════════
    // 核心服务
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 专家任务核心服务 —— 「建 → 找 → 跑队列」三件事的唯一实现。
    ///
    /// <para><b>★ 职责边界（D31）</b>：<b>不含</b>认可 / 修改 / 批准 / 导出 ——
    /// 那是结果菜单（<c>ExpertResultController</c>）的事。任务系统只管执行。</para>
    ///
    /// <para><b>★ 两条结构性约束</b>：</para>
    /// <list type="number">
    ///   <item><b>D36 业务锁</b>：同 <c>OrgCode|EnterpriseCode|StageCode|TaskType</c> 同时只允许一个未结束任务。
    ///         应用层先查（给友好提示），DB 生成列唯一键兜底（防并发）。</item>
    ///   <item><b>实体层与结果层分离</b>：创建任务只写<b>实体层</b>（<c>nc_item</c> / <c>report_section_item</c>，
    ///         长期实体）；<b>结果层</b>由队列执行器追加轮次。</item>
    /// </list>
    /// </summary>
    public class ExpertTaskService
    {
        private readonly IDbOrm _db;
        private readonly ILogger<ExpertTaskService> _logger;
        /// <summary>★ 2026-09-30 补录清单生成器（R \ H 差集 · 裁决 J1）</summary>
        private readonly GapDetector _gapDetector;

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public ExpertTaskService(IDbOrm db, ILogger<ExpertTaskService> logger, GapDetector gapDetector)
        {
            _db = db;
            _logger = logger;
            _gapDetector = gapDetector;
        }

        // ================================================================
        // 一、★ D36 业务锁判定
        // ================================================================

        /// <summary>
        /// 判定「该企业 + 阶段 + 类型」是否被未结束任务占用。
        ///
        /// <para><b>为什么应用层要查一次</b>：DB 的唯一键只能告诉我们「撞了」，
        /// 不能告诉我们「撞的是谁」。专家需要看到<b>是哪一条任务</b>挡住了，才知道该去处理它。</para>
        /// </summary>
        public async Task<Result<LockCheckResult>> CheckLockAsync(
            string orgCode, LockCheckRequest req)
        {
            var result = new LockCheckResult();

            if (string.IsNullOrWhiteSpace(req.EnterpriseCode)
                || string.IsNullOrWhiteSpace(req.StageCode)
                || string.IsNullOrWhiteSpace(req.TaskType))
                return Result<LockCheckResult>.Fail("企业 / 阶段 / 任务类型 均不能为空");

            // 同作用域全部任务（含已结束、含禁用 —— 这里要的是「事实」不是「可见性」）
            var all = (await _db.GetListAsync<CertExpertTask>(x =>
                x.OrgCode == orgCode
                && x.EnterpriseCode == req.EnterpriseCode
                && x.StageCode == req.StageCode
                && x.TaskType == req.TaskType
                && !x.IsDeleted, includeDisabled: true)).Data ?? new List<CertExpertTask>();

            if (!string.IsNullOrWhiteSpace(req.ExcludeTaskCode))
                all = all.Where(x => x.Code != req.ExcludeTaskCode).ToList();

            result.ExistingTaskCount = all.Count;

            var blocking = all.FirstOrDefault(x =>
                ExpertTaskConst.IsBlocking(x.LifecycleStatus, x.ExecStatus));

            if (blocking != null)
            {
                result.CanCreate = false;
                result.BlockingTaskCode = blocking.Code;
                result.BlockingTaskNumber = blocking.TaskNumber;
                result.BlockingTaskName = blocking.TaskName;
                result.BlockingExecStatus = blocking.ExecStatus;
                result.BlockingCreateTime = blocking.CreateTime;
                result.Message =
                    $"【{blocking.TaskName}】({blocking.TaskNumber}) 尚未结束（{ExecStatusLabel(blocking.ExecStatus)}），"
                    + "同一企业 + 阶段 + 分类下不允许并行开启新任务。请先完成或取消它。";
            }
            else
            {
                result.CanCreate = true;
                result.Message = "可以创建";
            }

            // 生效检查项数 + 历史平均轮次（向导第 1 步的「系统告知」卡片）
            result.CandidateCount = await CountCandidatesAsync(
                orgCode, req.EnterpriseCode, req.StageCode, req.TaskType, null);

            result.AvgRoundCount = await AvgRoundCountAsync(
                orgCode, req.EnterpriseCode, req.StageCode, req.TaskType);

            return Result<LockCheckResult>.Ok(result);
        }

        /// <summary>执行状态的中文标签（界面/提示统一口径）</summary>
        public static string ExecStatusLabel(string? execStatus) => execStatus switch
        {
            ExpertTaskConst.Exec.Draft => "草稿",
            ExpertTaskConst.Exec.PendingRun => "待启动",
            ExpertTaskConst.Exec.Running => "执行中",
            ExpertTaskConst.Exec.Completed => "已完成",
            ExpertTaskConst.Exec.Failed => "有失败项",
            _ => execStatus ?? "未知"
        };

        /// <summary>任务类型的中文标签</summary>
        public static string TaskTypeLabel(string? taskType) => taskType switch
        {
            ExpertTaskConst.TaskTypeNcCheck => "NC 检查",
            ExpertTaskConst.TaskTypeReportGenerate => "报告生成",
            _ => taskType ?? "未知"
        };

        // ================================================================
        // 二、候选解析（向导第 3 步 + 创建时复用）
        // ================================================================

        /// <summary>
        /// 解析候选检查项。NC 走 <c>cert_validation_rule</c>，报告走 <c>cert_report_section</c>。
        ///
        /// <para><b>★ 阶段口径</b>：两张配置表都用 <c>PhaseCode</c> 存 <c>cert_cert_stage.Code</c>（GUID），
        /// 与企业层 / 任务层的 <c>StageCode</c> <b>同一口径</b>（2026-09-30 已统一，见 22 号 §1.1）。</para>
        /// </summary>
        public async Task<Result<List<TaskCandidateDto>>> GetCandidatesAsync(
            string orgCode, string enterpriseCode, string stageCode,
            string taskType, List<string>? standardCodes)
        {
            var result = new List<TaskCandidateDto>();

            var standards = await LoadStandardsAsync(standardCodes);
            var stdNameMap = standards.ToDictionary(x => x.Code ?? "", x => x.StandardName ?? "");

            if (taskType == ExpertTaskConst.TaskTypeNcCheck)
            {
                // ★ 2026-10-08：ValidationRule 已实现 IIsValid（铁律九），启用判据 = IsValid（不再是 IsActive）
                var rules = (await _db.GetListAsync<ValidationRule>(x =>
                    x.PhaseCode == stageCode && x.IsValid == 1)).Data
                    ?? new List<ValidationRule>();

                if (standardCodes is { Count: > 0 })
                    rules = rules.Where(x => standardCodes.Contains(x.StandardCode)).ToList();

                // 已有实体行（拿「上次检查时间 / 上次结论」）
                var items = (await _db.GetListAsync<CertExpertNcItem>(x =>
                    x.OrgCode == orgCode && x.EnterpriseCode == enterpriseCode
                    && x.StageCode == stageCode && !x.IsDeleted)).Data
                    ?? new List<CertExpertNcItem>();
                var itemMap = items
                    .GroupBy(x => $"{x.StandardCode}|{x.RuleCode}")
                    .ToDictionary(g => g.Key, g => g.First());

                foreach (var r in rules.OrderBy(x => x.Sort).ThenBy(x => x.RuleCode))
                {
                    var key = $"{r.StandardCode}|{r.RuleCode}";
                    itemMap.TryGetValue(key, out var existing);

                    result.Add(new TaskCandidateDto
                    {
                        StandardCode = r.StandardCode,
                        StandardName = stdNameMap.GetValueOrDefault(r.StandardCode, r.StandardCode),
                        ItemCode = r.Code ?? "",
                        ItemNumber = r.RuleCode,
                        ItemName = r.RuleName,
                        ClauseNumber = r.ClauseNumber,
                        ClauseTitle = r.ClauseTitle,
                        JudgeMode = r.JudgeMode,
                        SeverityDefault = r.SeverityIfViolated,
                        LastAuditedTime = existing?.LastAuditedTime,
                        LastConclusion = null, // 由前端按需下钻历史轮次
                        // ★ 唯一判据（NULL = 未配 DAG）见 ValidationRuleRules.HasWorkflow
                        HasWorkflow = r.HasWorkflow(),
                        // 系统建议：从未检查过 / 上次不符 / 人工判定 → 建议勾选
                        Suggested = existing == null || existing.RoundCount == 0
                    });
                }
            }
            else
            {
                var sections = (await _db.GetListAsync<ReportSection>(x =>
                    x.PhaseCode == stageCode && x.IsValid == 1 && !x.IsDeleted)).Data
                    ?? new List<ReportSection>();

                if (standardCodes is { Count: > 0 })
                    sections = sections.Where(x => standardCodes.Contains(x.StandardCode)).ToList();

                var items = (await _db.GetListAsync<CertExpertReportSectionItem>(x =>
                    x.OrgCode == orgCode && x.EnterpriseCode == enterpriseCode
                    && x.StageCode == stageCode && !x.IsDeleted)).Data
                    ?? new List<CertExpertReportSectionItem>();
                var itemMap = items
                    .GroupBy(x => $"{x.StandardCode}|{x.SectionCode}")
                    .ToDictionary(g => g.Key, g => g.First());

                foreach (var s in sections.OrderBy(x => x.SortOrder).ThenBy(x => x.Sort))
                {
                    var key = $"{s.StandardCode}|{s.Code}";
                    itemMap.TryGetValue(key, out var existing);

                    result.Add(new TaskCandidateDto
                    {
                        StandardCode = s.StandardCode,
                        StandardName = stdNameMap.GetValueOrDefault(s.StandardCode, s.StandardCode),
                        ItemCode = s.Code ?? "",
                        ItemNumber = s.SortOrder.ToString(),
                        ItemName = s.SectionName,
                        ClauseNumber = null,
                        ClauseTitle = null,
                        JudgeMode = null,
                        SeverityDefault = null,
                        LastAuditedTime = existing?.LastAuditedTime,
                        HasWorkflow = !string.IsNullOrWhiteSpace(s.WorkflowConfig),
                        Suggested = existing == null || existing.RoundCount == 0
                    });
                }
            }

            return Result<List<TaskCandidateDto>>.Ok(result);
        }

        private async Task<int> CountCandidatesAsync(
            string orgCode, string enterpriseCode, string stageCode, string taskType,
            List<string>? standardCodes)
        {
            var r = await GetCandidatesAsync(orgCode, enterpriseCode, stageCode, taskType, standardCodes);
            return r.Success ? r.Data!.Count : 0;
        }

        private async Task<decimal> AvgRoundCountAsync(
            string orgCode, string enterpriseCode, string stageCode, string taskType)
        {
            if (taskType == ExpertTaskConst.TaskTypeNcCheck)
            {
                var items = (await _db.GetListAsync<CertExpertNcItem>(x =>
                    x.OrgCode == orgCode && x.EnterpriseCode == enterpriseCode
                    && x.StageCode == stageCode && !x.IsDeleted)).Data;
                if (items is not { Count: > 0 }) return 0m;
                return Math.Round(items.Average(x => (decimal)x.RoundCount), 2);
            }
            else
            {
                var items = (await _db.GetListAsync<CertExpertReportSectionItem>(x =>
                    x.OrgCode == orgCode && x.EnterpriseCode == enterpriseCode
                    && x.StageCode == stageCode && !x.IsDeleted)).Data;
                if (items is not { Count: > 0 }) return 0m;
                return Math.Round(items.Average(x => (decimal)x.RoundCount), 2);
            }
        }

        // ================================================================
        // 三、★ 创建任务（事务：6 张表）
        // ================================================================

        /// <summary>
        /// 创建任务（向导提交）。
        ///
        /// <para><b>事务内落 6 张表</b>：<c>cert_expert_task</c>（任务头）→
        /// <c>cert_expert_task_standard</c>（N 标准子任务）→
        /// <c>cert_expert_nc_item</c> / <c>cert_expert_report_section_item</c>（实体层，长期）→
        /// <c>cert_expert_task_log</c>（审计）。<b>队列不在此步生成</b> —— 队列在「提交执行」时生成
        /// （D-C：提交与启动拆开，允许分批跑）。</para>
        ///
        /// <para><b>⛔ 为什么必须自开事务</b>：基类 <c>AddCore</c> <b>不是事务</b>（YZH 架构事实）。
        /// 跨表写入若中途失败会留下「有任务头、无检查项」的半截任务，
        /// 而它<b>照样占着 D36 业务锁</b> ⇒ 该企业+阶段再也开不了任务。</para>
        /// </summary>
        public async Task<Result<TaskCreateResult>> CreateTaskAsync(
            string orgCode, string userCode, string? userName, string? clientIp, TaskCreateRequest req)
        {
            // ── 1. 参数校验 ──
            if (string.IsNullOrWhiteSpace(req.TaskName))
                return Result<TaskCreateResult>.Fail("任务名称不能为空");

            if (req.TaskType != ExpertTaskConst.TaskTypeNcCheck
                && req.TaskType != ExpertTaskConst.TaskTypeReportGenerate)
                return Result<TaskCreateResult>.Fail("任务类型不合法");

            if (req.StandardCodes is not { Count: > 0 })
                return Result<TaskCreateResult>.Fail("请至少选择一个标准");

            // ── 2. 企业 / 阶段 / 标准 归属校验（⛔ 不静默放行，防越权）──
            var enterprise = (await _db.GetOneAsync<Enterprise>(x =>
                x.Code == req.EnterpriseCode && x.OrgCode == orgCode && !x.IsDeleted)).Data;
            if (enterprise == null)
                return Result<TaskCreateResult>.Fail("企业不存在或不属于当前工作区");

            var stage = (await _db.GetOneAsync<CertStage>(x =>
                x.Code == req.StageCode && x.IsValid == 1 && !x.IsDeleted)).Data;
            if (stage == null)
                return Result<TaskCreateResult>.Fail("阶段不存在或已停用");

            // 标准必须是该企业在该阶段下已关联的
            var linked = (await _db.GetListAsync<CertEnterpriseStage>(x =>
                x.EnterpriseCode == req.EnterpriseCode
                && x.StageCode == req.StageCode
                && x.IsValid == 1 && !x.IsDeleted)).Data ?? new List<CertEnterpriseStage>();
            var linkedStd = linked.Select(x => x.StandardCode).ToHashSet();
            var badStd = req.StandardCodes.Where(x => !linkedStd.Contains(x)).ToList();
            if (badStd.Count > 0)
                return Result<TaskCreateResult>.Fail(
                    "以下标准未与该企业在本阶段建立关联：" + string.Join("、", badStd));

            // ── 3. ★ D36 业务锁（应用层先查，给友好提示）──
            var lockCheck = await CheckLockAsync(orgCode, new LockCheckRequest
            {
                EnterpriseCode = req.EnterpriseCode,
                StageCode = req.StageCode,
                TaskType = req.TaskType
            });
            if (!lockCheck.Success) return Result<TaskCreateResult>.Fail(lockCheck.Error);
            if (!lockCheck.Data!.CanCreate)
                return Result<TaskCreateResult>.Fail(lockCheck.Data.Message);

            // ── 4. 候选解析（FULL 全取；PARTIAL 只取勾选的）──
            var cand = await GetCandidatesAsync(
                orgCode, req.EnterpriseCode, req.StageCode, req.TaskType, req.StandardCodes);
            if (!cand.Success) return Result<TaskCreateResult>.Fail(cand.Error);
            var candidates = cand.Data!;

            if (req.ScopeType == ExpertTaskConst.Scope.Partial
                && req.ItemCodes is { Count: > 0 })
            {
                var pick = req.ItemCodes.ToHashSet();
                candidates = candidates.Where(x => pick.Contains(x.ItemCode)).ToList();
            }

            if (candidates.Count == 0)
                return Result<TaskCreateResult>.Fail(
                    "该企业在本阶段下没有可执行的检查项/章节（请先在后台配置规则或报告章节）");

            // ── 5. 事务写入 ──
            var standards = await LoadStandardsAsync(req.StandardCodes);
            var stdNameMap = standards.ToDictionary(x => x.Code ?? "", x => x.StandardName ?? "");

            var byStandard = candidates.GroupBy(x => x.StandardCode).ToList();
            var taskCode = Guid.NewGuid().ToString("N");
            var now = DateTime.Now;

            using var tx = _db.BeginTransaction();
            try
            {
                // 5.1 任务头
                var task = new CertExpertTask
                {
                    Code = taskCode,
                    OrgCode = orgCode,
                    TaskName = req.TaskName.Trim(),
                    TaskNumber = await NextTaskNumberAsync(orgCode, now),
                    TaskType = req.TaskType,
                    ScopeType = req.ScopeType,
                    TaskSource = req.TaskSource,
                    ScopeSnapshot = JsonSerializer.Serialize(new ScopeSnapshotDto
                    {
                        Standards = req.StandardCodes,
                        Items = byStandard.ToDictionary(
                            g => g.Key, g => g.Select(x => x.ItemCode).ToList())
                    }),
                    EnterpriseCode = req.EnterpriseCode,
                    EnterpriseName = enterprise.Name,
                    StageCode = req.StageCode,
                    StageName = stage.StageName,
                    StandardCodes = JsonSerializer.Serialize(req.StandardCodes),
                    StandardNames = JsonSerializer.Serialize(
                        req.StandardCodes.Select(x => stdNameMap.GetValueOrDefault(x, x)).ToList()),
                    StandardCount = byStandard.Count,
                    ExecStatus = ExpertTaskConst.Exec.Draft,
                    ReviewStatus = ExpertTaskConst.Review.NotStarted,
                    LifecycleStatus = ExpertTaskConst.Lifecycle.Active,
                    TotalItemCount = candidates.Count,
                    PendingCount = candidates.Count,
                    Remark = req.Remark,
                    CreateBy = userCode,
                    CreateName = userName,
                    CreateTime = now,
                    IsValid = 1,
                    IsDeleted = false
                };

                var ins = await _db.InsertAsync(task);
                if (!ins.Success) { tx.Rollback(); return Result<TaskCreateResult>.Fail(ins.Error); }

                var itemCount = 0;

                foreach (var g in byStandard)
                {
                    var subCode = Guid.NewGuid().ToString("N");

                    // 5.2 标准子任务
                    var sub = new CertExpertTaskStandard
                    {
                        Code = subCode,
                        OrgCode = orgCode,
                        TaskCode = taskCode,
                        TaskNumber = task.TaskNumber,
                        StandardCode = g.Key,
                        StandardName = stdNameMap.GetValueOrDefault(g.Key, g.Key),
                        ExecStatus = ExpertTaskConst.Exec.Draft,
                        LifecycleStatus = ExpertTaskConst.Lifecycle.Active,
                        ItemCount = g.Count(),
                        PendingCount = g.Count(),
                        Sort = byStandard.IndexOf(g),
                        CreateBy = userCode,
                        CreateName = userName,
                        CreateTime = now,
                        IsValid = 1,
                        IsDeleted = false
                    };
                    var subIns = await _db.InsertAsync(sub);
                    if (!subIns.Success) { tx.Rollback(); return Result<TaskCreateResult>.Fail(subIns.Error); }

                    // 5.3 实体层（长期实体，幂等：存在则复活 + 刷新快照，⛔ 不重复插入）
                    foreach (var c in g)
                    {
                        var ok = req.TaskType == ExpertTaskConst.TaskTypeNcCheck
                            ? await UpsertNcItemAsync(orgCode, req, c, userCode, userName, now)
                            : await UpsertSectionItemAsync(orgCode, req, c, userCode, userName, now);
                        if (!ok.Success) { tx.Rollback(); return Result<TaskCreateResult>.Fail(ok.Error); }
                        itemCount++;
                    }
                }

                // 5.4 审计日志
                await InsertLogAsync(new CertExpertTaskLog
                {
                    TaskCode = taskCode,
                    LogAction = ExpertTaskConst.LogAction.TaskCreated,
                    LogLevel = ExpertTaskConst.LogLevel.Info,
                    Message = $"创建任务【{task.TaskName}】，{byStandard.Count} 个标准、{itemCount} 个检查项",
                    OperatorCode = userCode,
                    OperatorName = userName,
                    ClientIp = clientIp,
                    IsAutoResult = false,
                    OrgCode = orgCode,
                    OperateTime = now,
                    CreateTime = now,
                    Payload = JsonSerializer.Serialize(new
                    {
                        TaskType = req.TaskType,
                        ScopeType = req.ScopeType,
                        TaskSource = req.TaskSource,
                        req.StandardCodes,
                        ItemCount = itemCount
                    })
                });

                tx.Commit();

                _logger.LogInformation(
                    "[ExpertTask] 任务创建成功 {TaskCode} {TaskNumber} type={Type} std={Std} items={Items}",
                    taskCode, task.TaskNumber, req.TaskType, byStandard.Count, itemCount);

                // ★★ 2026-09-30 裁决 J1：创建后立刻做完备性检查，产出补录清单（R \ H 差集）。
                //   ⛔ 失败不阻断任务创建 —— 清单是【信息】不是【门禁】（客户可以忽略，见 25 号 §6.2）。
                var gapCount = 0;
                try
                {
                    gapCount = await GenerateGapsAsync(task, orgCode);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[ExpertTask] 补录清单生成失败（不影响任务创建）task={TaskCode}", taskCode);
                }

                return Result<TaskCreateResult>.Ok(new TaskCreateResult
                {
                    TaskCode = taskCode,
                    TaskNumber = task.TaskNumber,
                    TaskName = task.TaskName,
                    StandardCount = byStandard.Count,
                    ItemCount = itemCount,
                    QueueCount = byStandard.Count,
                    GapCount = gapCount,
                    Summary = gapCount > 0
                        ? $"已创建任务头 1 个 · 标准子任务 {byStandard.Count} 个 · 检查项 {itemCount} 个 · 待生成队列 {byStandard.Count} 个 · ⚠ 待补录数据 {gapCount} 项"
                        : $"已创建任务头 1 个 · 标准子任务 {byStandard.Count} 个 · 检查项 {itemCount} 个 · 待生成队列 {byStandard.Count} 个"
                });
            }
            catch (Exception ex)
            {
                tx.Rollback();
                _logger.LogError(ex, "[ExpertTask] 创建任务失败");
                return Result<TaskCreateResult>.Fail($"创建任务失败：{ex.Message}");
            }
        }

        /// <summary>
        /// ★ 2026-09-30 裁决 J1：按各标准子任务的项目集合生成补录清单（<c>R \ H</c> 差集）。
        /// </summary>
        /// <param name="task">任务头（提供 EnterpriseCode / StageCode / OrgCode）</param>
        /// <param name="byStandard">标准 Code → 该标准下的检查项/章节 Code 列表</param>
        /// <param name="snapshot">范围快照（取各标准的 itemCode）</param>
        /// <param name="orgCode">专家工作区（租户）Code ⇒ 写 <c>cert_expert_task_data_gap.OrgCode</c></param>
        /// <returns>本次新增的缺口条数</returns>
        /// <remarks>
        /// ★ 2026-10-07 改为 <c>public</c>：供「启动前预检」端点
        /// （<c>ExpertTaskDataGapController.Precheck</c>）复用同一份重算逻辑。
        /// ⛔ 不要在控制器里另写一份 R\H 差集 —— 两处口径必然分叉，
        /// 出现「预检说齐了、执行说缺了」。
        /// </remarks>
        public async Task<int> GenerateGapsAsync(CertExpertTask task, string orgCode)
        {
            var itemType = task.TaskType == ExpertTaskConst.TaskTypeNcCheck
                ? ExpertTaskConst.ItemType.NcCheck
                : ExpertTaskConst.ItemType.ReportSection;

            _gapDetector.OrgCode = orgCode;

            var total = 0;
            var subs = (await _db.GetListAsync<CertExpertTaskStandard>(x =>
                x.TaskCode == task.Code && !x.IsDeleted)).Data ?? new List<CertExpertTaskStandard>();

            foreach (var sub in subs)
            {
                // ★ R 必须按【本标准的 itemCode】局部收集（J1 可用性前提）
                //   数据源就是任务自己的范围快照 —— 即专家在向导里【实际勾选】的那些项，
                //   ⛔ 不是该标准的全部规则（否则局部 NC 也会拉出全量清单）。
                if (!ParseSnapshot(task.ScopeSnapshot).Items.TryGetValue(sub.StandardCode, out var itemCodes)
                    || itemCodes == null || itemCodes.Count == 0)
                    continue;

                var deps = await _gapDetector.CollectDependenciesAsync(itemCodes, itemType);
                if (deps.Count == 0) continue;

                var r = await _gapDetector.DetectAndPersistAsync(
                    deps, task.Code ?? "", sub.Code ?? "",
                    task.EnterpriseCode, sub.StandardCode, task.StageCode);

                total += r.Inserted.Count;

                if (r.Inserted.Count > 0)
                    _logger.LogInformation(
                        "[ExpertTask] 补录清单 task={TaskCode} std={Std} 新增缺口={New} " +
                        "需求集={Demand} 已满足={Ok} 未知节点={Unknown}",
                        task.Code, sub.StandardCode, r.Inserted.Count, r.DemandCount, r.SatisfiedCount, r.UnknownNodes.Count);
            }

            // 回写 GapCount（列表页角标）
            if (total > 0)
            {
                var pending = (await _db.CountAsync<CertExpertTaskDataGap>(x =>
                    x.TaskCode == task.Code && x.GapStatus == ExpertTaskConst.GapStatus.Pending && !x.IsDeleted)).Data;
                task.GapCount = pending;
                task.UpdateTime = DateTime.Now;
                await _db.UpdateAsync(task, nameof(CertExpertTask.GapCount), nameof(CertExpertTask.UpdateTime));
            }

            return total;
        }

        /// <summary>范围快照结构（存 <c>cert_expert_task.ScopeSnapshot</c>）</summary>
        private class ScopeSnapshotDto
        {
            public List<string> Standards { get; set; } = new();

            /// <summary>standardCode → itemCode 列表</summary>
            public Dictionary<string, List<string>> Items { get; set; } = new();
        }

        /// <summary>生成任务编号 <c>TSK-yyyyMMdd-NNNN</c>（同工作区当日递增；唯一键兜底防并发）</summary>
        private async Task<string> NextTaskNumberAsync(string orgCode, DateTime now)
        {
            var prefix = $"{ExpertTaskConst.TaskNumberPrefix}-{now:yyyyMMdd}-";
            var seq = await _db.SqlScalarAsync<int>(
                @"SELECT IFNULL(MAX(CAST(SUBSTRING(TaskNumber, @len) AS UNSIGNED)), 0) + 1
                  FROM cert_expert_task
                  WHERE OrgCode = @org AND TaskNumber LIKE @like",
                new { len = prefix.Length + 1, org = orgCode, like = prefix + "%" });
            var n = seq.Success && seq.Data > 0 ? seq.Data : 1;
            return prefix + n.ToString("D4");
        }

        /// <summary>NC 实体层 upsert（唯一键 <c>uk_scope_rule</c>；⛔ 必须含已禁用行查重，否则复活失败）</summary>
        private async Task<Result<bool>> UpsertNcItemAsync(
            string orgCode, TaskCreateRequest req, TaskCandidateDto c,
            string userCode, string? userName, DateTime now)
        {
            // ★ 用 includeDisabled: true 取规则（2026-10-08）：实体实现 IIsValid 后 GetOneAsync 会套
            //   IsValid=1 过滤；本处为规则快照 upsert（含「复活」已禁用实体行的分支），
            //   过滤会导致停用规则的任务落不到快照、条款号/严重度全空。
            var rule = (await _db.GetListAsync<ValidationRule>(
                x => x.Code == c.ItemCode, includeDisabled: true)).Data?.FirstOrDefault();

            // ★ 条款号 / 条款标题必须**显式 JOIN** `cert_iso_clause` 取。
            //   `ValidationRule.ClauseNumber` / `ClauseTitle` 是 `[SugarColumn(IsIgnore = true)]`
            //   的**视图字段**（由 Admin 端 ValidationRule 控制器的 OnQueried 填），
            //   直接查实体恒为 null ⇒ NC 结果的「条款号」列永远是空的（静默缺数据）。
            var clause = string.IsNullOrWhiteSpace(rule?.ClauseCode)
                ? null
                : (await _db.GetOneAsync<ISOClause>(x => x.Code == rule!.ClauseCode)).Data;

            var existing = (await _db.GetListAsync<CertExpertNcItem>(x =>
                x.OrgCode == orgCode
                && x.EnterpriseCode == req.EnterpriseCode
                && x.StageCode == req.StageCode
                && x.StandardCode == c.StandardCode
                && x.RuleCode == c.ItemCode, includeDisabled: true)).Data?.FirstOrDefault();

            if (existing != null)
            {
                // 复活 + 刷新规则快照（规则可能已改版）
                existing.IsValid = 1;
                existing.IsDeleted = false;
                existing.RuleName = c.ItemName;
                existing.RuleNameEn = rule?.RuleNameEn;
                existing.RuleNumber = rule?.RuleCode;
                existing.ClauseCode = rule?.ClauseCode;
                existing.ClauseNumber = clause?.ClauseNumber;
                existing.ClauseTitle = clause?.Title;
                existing.JudgeMode = rule?.JudgeMode;
                existing.SeverityDefault = rule?.SeverityIfViolated;
                // ★ 不再快照 WorkflowCode（2026-10-08）：DAG 已由 cert_validation_rule.RuleJson 承载，
                //   该列在规则表已删；CertExpertNcItem.WorkflowCode 列保留但不再写入（历史 NULL）
                existing.UpdateBy = userCode;
                existing.UpdateTime = now;

                var up = await _db.UpdateAsync(existing,
                    nameof(CertExpertNcItem.IsValid), nameof(CertExpertNcItem.IsDeleted),
                    nameof(CertExpertNcItem.RuleName), nameof(CertExpertNcItem.RuleNameEn),
                    nameof(CertExpertNcItem.RuleNumber), nameof(CertExpertNcItem.ClauseCode),
                    nameof(CertExpertNcItem.ClauseNumber), nameof(CertExpertNcItem.ClauseTitle),
                    nameof(CertExpertNcItem.JudgeMode), nameof(CertExpertNcItem.SeverityDefault),
                    nameof(CertExpertNcItem.UpdateBy), nameof(CertExpertNcItem.UpdateTime));
                return up.Success ? Result<bool>.Ok(true) : Result<bool>.Fail(up.Error);
            }

            var item = new CertExpertNcItem
            {
                Code = Guid.NewGuid().ToString("N"),
                OrgCode = orgCode,
                EnterpriseCode = req.EnterpriseCode,
                StageCode = req.StageCode,
                StandardCode = c.StandardCode,
                RuleCode = c.ItemCode,
                RuleNumber = rule?.RuleCode,
                RuleName = c.ItemName,
                RuleNameEn = rule?.RuleNameEn,
                ClauseCode = rule?.ClauseCode,
                ClauseNumber = clause?.ClauseNumber,
                ClauseTitle = clause?.Title,
                JudgeMode = rule?.JudgeMode,
                SeverityDefault = rule?.SeverityIfViolated,
                RuleVersion = 1,
                RoundCount = 0,
                CreateBy = userCode,
                CreateName = userName,
                CreateTime = now,
                IsValid = 1,
                IsDeleted = false
            };

            var ins = await _db.InsertAsync(item);
            return ins.Success ? Result<bool>.Ok(true) : Result<bool>.Fail(ins.Error);
        }

        /// <summary>报告章节实体层 upsert（唯一键 <c>uk_scope_sec</c>）</summary>
        private async Task<Result<bool>> UpsertSectionItemAsync(
            string orgCode, TaskCreateRequest req, TaskCandidateDto c,
            string userCode, string? userName, DateTime now)
        {
            var sec = (await _db.GetOneAsync<ReportSection>(x => x.Code == c.ItemCode)).Data;

            // ★ 同 NC：章节实体自身不带条款号，必须 JOIN `cert_iso_clause`（ClauseCode 可空 = 概述类章节）
            var secClause = string.IsNullOrWhiteSpace(sec?.ClauseCode)
                ? null
                : (await _db.GetOneAsync<ISOClause>(x => x.Code == sec!.ClauseCode)).Data;

            var existing = (await _db.GetListAsync<CertExpertReportSectionItem>(x =>
                x.OrgCode == orgCode
                && x.EnterpriseCode == req.EnterpriseCode
                && x.StageCode == req.StageCode
                && x.StandardCode == c.StandardCode
                && x.SectionCode == c.ItemCode, includeDisabled: true)).Data?.FirstOrDefault();

            if (existing != null)
            {
                existing.IsValid = 1;
                existing.IsDeleted = false;
                existing.SectionName = c.ItemName;
                existing.SectionNameEn = sec?.SectionNameEn;
                existing.SectionTemplateContent = sec?.Content;
                existing.ClauseCode = sec?.ClauseCode;
                existing.ClauseNumber = secClause?.ClauseNumber;
                existing.ClauseTitle = secClause?.Title;
                existing.WorkflowCode = sec?.WorkflowCode;
                existing.Sort = sec?.SortOrder ?? 0;
                existing.UpdateBy = userCode;
                existing.UpdateTime = now;

                var up = await _db.UpdateAsync(existing,
                    nameof(CertExpertReportSectionItem.IsValid), nameof(CertExpertReportSectionItem.IsDeleted),
                    nameof(CertExpertReportSectionItem.SectionName), nameof(CertExpertReportSectionItem.SectionNameEn),
                    nameof(CertExpertReportSectionItem.SectionTemplateContent),
                    nameof(CertExpertReportSectionItem.ClauseCode),
                    nameof(CertExpertReportSectionItem.ClauseNumber),
                    nameof(CertExpertReportSectionItem.ClauseTitle),
                    nameof(CertExpertReportSectionItem.WorkflowCode),
                    nameof(CertExpertReportSectionItem.Sort),
                    nameof(CertExpertReportSectionItem.UpdateBy), nameof(CertExpertReportSectionItem.UpdateTime));
                return up.Success ? Result<bool>.Ok(true) : Result<bool>.Fail(up.Error);
            }

            var item = new CertExpertReportSectionItem
            {
                Code = Guid.NewGuid().ToString("N"),
                OrgCode = orgCode,
                EnterpriseCode = req.EnterpriseCode,
                StageCode = req.StageCode,
                StandardCode = c.StandardCode,
                SectionCode = c.ItemCode,
                SectionName = c.ItemName,
                SectionNameEn = sec?.SectionNameEn,
                SectionTemplateContent = sec?.Content,
                ClauseCode = sec?.ClauseCode,
                ClauseNumber = secClause?.ClauseNumber,
                ClauseTitle = secClause?.Title,
                WorkflowCode = sec?.WorkflowCode,
                RuleVersion = 1,
                Sort = sec?.SortOrder ?? 0,
                RoundCount = 0,
                CreateBy = userCode,
                CreateName = userName,
                CreateTime = now,
                IsValid = 1,
                IsDeleted = false
            };

            var ins = await _db.InsertAsync(item);
            return ins.Success ? Result<bool>.Ok(true) : Result<bool>.Fail(ins.Error);
        }

        // ================================================================
        // 四、★ 提交执行（生成队列 + 队列项）
        // ================================================================

        /// <summary>
        /// 提交执行 —— 把任务的实体层范围「物化」成队列与队列项。
        ///
        /// <para><b>D02：1 任务 → N 标准子任务 → N 队列</b>；每个标准一个队列，
        /// <b>允许分批跑</b>（D-C：先跑 ISO 9001 确认没问题再跑 ISO 14001）。</para>
        ///
        /// <para><b>幂等</b>：唯一键 <c>uk_subtask(SubTaskCode)</c> 保证一个子任务只有一个队列；
        /// 重复提交时跳过已有队列。</para>
        /// </summary>
        public async Task<Result<object>> SubmitAsync(
            string orgCode, string taskCode, string userCode, string? userName, string? clientIp)
        {
            var task = (await _db.GetOneAsync<CertExpertTask>(x =>
                x.Code == taskCode && x.OrgCode == orgCode && !x.IsDeleted)).Data;
            if (task == null) return Result<object>.Fail("任务不存在或不属于当前工作区");

            if (task.LifecycleStatus != ExpertTaskConst.Lifecycle.Active)
                return Result<object>.Fail($"任务当前为「{task.LifecycleStatus}」，不可提交");

            if (task.ExecStatus != ExpertTaskConst.Exec.Draft
                && task.ExecStatus != ExpertTaskConst.Exec.Failed)
                return Result<object>.Fail(
                    $"任务当前为「{ExecStatusLabel(task.ExecStatus)}」，只有草稿或有失败项的任务可提交");

            var subs = (await _db.GetListAsync<CertExpertTaskStandard>(x =>
                x.TaskCode == taskCode && !x.IsDeleted)).Data ?? new List<CertExpertTaskStandard>();
            if (subs.Count == 0) return Result<object>.Fail("任务没有标准子任务，无法提交");

            var snapshot = ParseSnapshot(task.ScopeSnapshot);
            var now = DateTime.Now;
            var createdQueues = 0;
            var createdItems = 0;

            // ★ 阶段人读码（如 `03`）—— 引擎层 `wf_execution_task.PhaseCode` 是 varchar(30)，
            //   装不下任务层的 32 位 GUID；两个口径不能混用。见 ExpertItemPayload.StageNo 注释。
            var stageNo = (await _db.GetOneAsync<CertStage>(x => x.Code == task.StageCode)).Data?.StageCode;

            // ★ 2026-09-30 裁决 J1：提交时【重跑一次完备性检查】。
            //   为什么重跑：数据可能在上次检测之后被补录/替换/重新提取，缺口集合已变。
            //   ⛔ 只统计、不阻断（见返回体的 Warning 字段）。
            try
            {
                await GenerateGapsAsync(task, orgCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ExpertTask] 提交前补录清单重算失败（不影响提交）task={TaskCode}", taskCode);
            }
            var pendingGapCount = (await _db.CountAsync<CertExpertTaskDataGap>(x =>
                x.TaskCode == taskCode
                && x.GapStatus == ExpertTaskConst.GapStatus.Pending
                && !x.IsDeleted)).Data;

            using var tx = _db.BeginTransaction();
            try
            {
                foreach (var sub in subs)
                {
                    // 幂等：已有队列则跳过（可反复提交以补生成）
                    var existed = (await _db.GetListAsync<CertExpertTaskQueue>(x =>
                        x.SubTaskCode == sub.Code && !x.IsDeleted, includeDisabled: true)).Data;
                    if (existed is { Count: > 0 }) continue;

                    var itemCodes = snapshot.Items.TryGetValue(sub.StandardCode, out var list)
                        ? list
                        : new List<string>();

                    // 实体层行（真正入队的对象）
                    var rows = task.TaskType == ExpertTaskConst.TaskTypeNcCheck
                        ? await LoadNcRowsAsync(orgCode, task, sub.StandardCode, itemCodes)
                        : await LoadSectionRowsAsync(orgCode, task, sub.StandardCode, itemCodes);

                    if (rows.Count == 0) continue;

                    var queueCode = Guid.NewGuid().ToString("N");
                    var queue = new CertExpertTaskQueue
                    {
                        Code = queueCode,
                        OrgCode = orgCode,
                        TaskCode = taskCode,
                        SubTaskCode = sub.Code,
                        StandardCode = sub.StandardCode,
                        QueueType = task.TaskType == ExpertTaskConst.TaskTypeNcCheck
                            ? ExpertTaskConst.ItemType.NcCheck
                            : ExpertTaskConst.ItemType.ReportSection,
                        // ★ 单一口径 = SubTaskCode（不复用 yzh_queue 的双口径）
                        ScopeKey = sub.Code,
                        QueueStatus = ExpertTaskConst.Queue.Pending,
                        TotalCount = rows.Count,
                        Priority = 0,
                        MaxRetryCount = 3,
                        CreateBy = userCode,
                        CreateName = userName,
                        CreateTime = now,
                        IsValid = 1,
                        IsDeleted = false
                    };
                    var qIns = await _db.InsertAsync(queue);
                    if (!qIns.Success) { tx.Rollback(); return Result<object>.Fail(qIns.Error); }
                    createdQueues++;

                    var seq = 1;
                    foreach (var (rowCode, itemCode, itemName) in rows)
                    {
                        var qi = new CertExpertTaskQueueItem
                        {
                            Code = Guid.NewGuid().ToString("N"),
                            OrgCode = orgCode,
                            QueueCode = queueCode,
                            TaskCode = taskCode,
                            // ★ 实体层行 Code（⛔ 不是结果行 Code）
                            TaskItemCode = rowCode,
                            ItemType = queue.QueueType,
                            ItemCode = itemCode,
                            Seq = seq++,
                            Payload = JsonSerializer.Serialize(new
                            {
                                TaskCode = taskCode,
                                TaskItemCode = rowCode,
                                ItemCode = itemCode,
                                ItemName = itemName,
                                EnterpriseCode = task.EnterpriseCode,
                                StageCode = task.StageCode,
                                StageNo = stageNo,
                                StandardCode = sub.StandardCode
                            }),
                            ItemStatus = ExpertTaskConst.Item.Pending,
                            MaxRetryCount = 3,
                            CreateBy = userCode,
                            CreateTime = now,
                            IsValid = 1,
                            IsDeleted = false
                        };
                        var qiIns = await _db.InsertAsync(qi);
                        if (!qiIns.Success) { tx.Rollback(); return Result<object>.Fail(qiIns.Error); }
                        createdItems++;
                    }

                    // 子任务状态推进 + 回填队列 Code
                    sub.QueueCode = queueCode;
                    sub.ExecStatus = ExpertTaskConst.Exec.PendingRun;
                    sub.ItemCount = rows.Count;
                    sub.PendingCount = rows.Count;
                    sub.UpdateBy = userCode;
                    sub.UpdateTime = now;
                    var subUp = await _db.UpdateAsync(sub,
                        nameof(CertExpertTaskStandard.QueueCode),
                        nameof(CertExpertTaskStandard.ExecStatus),
                        nameof(CertExpertTaskStandard.ItemCount),
                        nameof(CertExpertTaskStandard.PendingCount),
                        nameof(CertExpertTaskStandard.UpdateBy),
                        nameof(CertExpertTaskStandard.UpdateTime));
                    if (!subUp.Success) { tx.Rollback(); return Result<object>.Fail(subUp.Error); }
                }

                // 任务头推进
                task.ExecStatus = ExpertTaskConst.Exec.PendingRun;
                task.SubmitTime = now;
                task.UpdateBy = userCode;
                task.UpdateTime = now;
                var tUp = await _db.UpdateAsync(task,
                    nameof(CertExpertTask.ExecStatus),
                    nameof(CertExpertTask.SubmitTime),
                    nameof(CertExpertTask.UpdateBy),
                    nameof(CertExpertTask.UpdateTime));
                if (!tUp.Success) { tx.Rollback(); return Result<object>.Fail(tUp.Error); }

                await InsertLogAsync(new CertExpertTaskLog
                {
                    TaskCode = taskCode,
                    LogAction = ExpertTaskConst.LogAction.TaskSubmitted,
                    LogLevel = ExpertTaskConst.LogLevel.Info,
                    Message = $"提交执行：新建队列 {createdQueues} 个、队列项 {createdItems} 个（待启动）",
                    OperatorCode = userCode,
                    OperatorName = userName,
                    ClientIp = clientIp,
                    OrgCode = orgCode,
                    OperateTime = now,
                    CreateTime = now,
                    Payload = JsonSerializer.Serialize(new { createdQueues, createdItems })
                });

                tx.Commit();

                return Result<object>.Ok(new
                {
                    TaskCode = taskCode,
                    QueueCount = createdQueues,
                    ItemCount = createdItems,
                    // ★ 2026-09-30 裁决 J1：⛔ 取消 05 号 §2.3 的 409 DATA_GAP_UNRESOLVED 硬阻断。
                    //   补录清单是【信息】不是【门禁】—— 客户可以忽略（用户原话）。
                    //   真正的门禁在执行期：引用数据为空 ⇒ 任务项自动失败 + 提示「缺失必要数据」。
                    PendingGapCount = pendingGapCount,
                    Warning = pendingGapCount > 0
                        ? $"有 {pendingGapCount} 项数据未补录，依赖它们的检查项执行时会自动失败（可去「数据缺口」页补录或跳过）"
                        : null,
                    Message = createdQueues == 0
                        ? "队列已存在，无需重复生成"
                        : $"已生成 {createdQueues} 个队列、{createdItems} 个队列项，请在详情页「执行队列」中启动"
                });
            }
            catch (Exception ex)
            {
                tx.Rollback();
                _logger.LogError(ex, "[ExpertTask] 提交执行失败 {TaskCode}", taskCode);
                return Result<object>.Fail($"提交执行失败：{ex.Message}");
            }
        }

        private class SnapshotShape
        {
            public List<string> Standards { get; set; } = new();
            public Dictionary<string, List<string>> Items { get; set; } = new();
        }

        private static SnapshotShape ParseSnapshot(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new SnapshotShape();
            try
            {
                return JsonSerializer.Deserialize<SnapshotShape>(json, JsonOpts) ?? new SnapshotShape();
            }
            catch
            {
                return new SnapshotShape();
            }
        }

        /// <summary>取要入队的 NC 实体行 → (实体行 Code, 规则 Code, 规则名)</summary>
        private async Task<List<(string RowCode, string ItemCode, string ItemName)>> LoadNcRowsAsync(
            string orgCode, CertExpertTask task, string standardCode, List<string> ruleCodes)
        {
            var rows = (await _db.GetListAsync<CertExpertNcItem>(x =>
                x.OrgCode == orgCode
                && x.EnterpriseCode == task.EnterpriseCode
                && x.StageCode == task.StageCode
                && x.StandardCode == standardCode
                && x.IsValid == 1 && !x.IsDeleted)).Data ?? new List<CertExpertNcItem>();

            if (ruleCodes is { Count: > 0 })
                rows = rows.Where(x => ruleCodes.Contains(x.RuleCode)).ToList();

            return rows.OrderBy(x => x.Sort).ThenBy(x => x.RuleNumber)
                .Select(x => (x.Code ?? "", x.RuleCode, x.RuleName)).ToList();
        }

        /// <summary>取要入队的报告章节实体行</summary>
        private async Task<List<(string RowCode, string ItemCode, string ItemName)>> LoadSectionRowsAsync(
            string orgCode, CertExpertTask task, string standardCode, List<string> sectionCodes)
        {
            var rows = (await _db.GetListAsync<CertExpertReportSectionItem>(x =>
                x.OrgCode == orgCode
                && x.EnterpriseCode == task.EnterpriseCode
                && x.StageCode == task.StageCode
                && x.StandardCode == standardCode
                && x.IsValid == 1 && !x.IsDeleted)).Data ?? new List<CertExpertReportSectionItem>();

            if (sectionCodes is { Count: > 0 })
                rows = rows.Where(x => sectionCodes.Contains(x.SectionCode)).ToList();

            return rows.OrderBy(x => x.Sort)
                .Select(x => (x.Code ?? "", x.SectionCode, x.SectionName)).ToList();
        }

        // ================================================================
        // 五、队列启停 / 重试
        // ================================================================

        /// <summary>启动队列（pending / failed → running）</summary>
        public async Task<Result<object>> StartQueueAsync(
            string orgCode, string queueCode, string userCode, string? userName, string? clientIp)
        {
            var q = (await _db.GetOneAsync<CertExpertTaskQueue>(x =>
                x.Code == queueCode && x.OrgCode == orgCode && !x.IsDeleted)).Data;
            if (q == null) return Result<object>.Fail("队列不存在或不属于当前工作区");

            if (q.QueueStatus == ExpertTaskConst.Queue.Running)
                return Result<object>.Ok(new { Message = "队列已在运行中" });
            if (q.QueueStatus == ExpertTaskConst.Queue.Completed)
                return Result<object>.Fail("队列已完成，如需重跑请使用「重试失败项」或新建任务");
            if (q.QueueStatus == ExpertTaskConst.Queue.Cancelled)
                return Result<object>.Fail("队列已取消，不可启动");

            var now = DateTime.Now;
            q.QueueStatus = ExpertTaskConst.Queue.Running;
            q.StartTime ??= now;
            q.LockCode = null;
            q.LockedUntil = null;
            q.UpdateBy = userCode;
            q.UpdateTime = now;
            var up = await _db.UpdateAsync(q,
                nameof(CertExpertTaskQueue.QueueStatus), nameof(CertExpertTaskQueue.StartTime),
                nameof(CertExpertTaskQueue.LockCode), nameof(CertExpertTaskQueue.LockedUntil),
                nameof(CertExpertTaskQueue.UpdateBy), nameof(CertExpertTaskQueue.UpdateTime));
            if (!up.Success) return Result<object>.Fail(up.Error);

            // 任务头 → running
            var task = (await _db.GetOneAsync<CertExpertTask>(x => x.Code == q.TaskCode)).Data;
            if (task != null && task.ExecStatus != ExpertTaskConst.Exec.Running)
            {
                task.ExecStatus = ExpertTaskConst.Exec.Running;
                task.UpdateBy = userCode;
                task.UpdateTime = now;
                await _db.UpdateAsync(task,
                    nameof(CertExpertTask.ExecStatus),
                    nameof(CertExpertTask.UpdateBy), nameof(CertExpertTask.UpdateTime));
            }

            await InsertLogAsync(new CertExpertTaskLog
            {
                TaskCode = q.TaskCode,
                SubTaskCode = q.SubTaskCode,
                QueueCode = queueCode,
                LogAction = ExpertTaskConst.LogAction.QueueStarted,
                LogLevel = ExpertTaskConst.LogLevel.Info,
                Message = $"启动队列（{q.TotalCount} 个队列项）",
                OperatorCode = userCode,
                OperatorName = userName,
                ClientIp = clientIp,
                OrgCode = orgCode,
                OperateTime = now,
                CreateTime = now
            });

            return Result<object>.Ok(new { Message = "队列已启动" });
        }

        /// <summary>暂停队列（running → pending；已在跑的项不中断，跑完后不再领新项）</summary>
        public async Task<Result<object>> PauseQueueAsync(
            string orgCode, string queueCode, string userCode, string? userName, string? clientIp)
        {
            var q = (await _db.GetOneAsync<CertExpertTaskQueue>(x =>
                x.Code == queueCode && x.OrgCode == orgCode && !x.IsDeleted)).Data;
            if (q == null) return Result<object>.Fail("队列不存在或不属于当前工作区");
            if (q.QueueStatus != ExpertTaskConst.Queue.Running)
                return Result<object>.Fail("只有运行中的队列可暂停");

            var now = DateTime.Now;
            q.QueueStatus = ExpertTaskConst.Queue.Pending;
            q.UpdateBy = userCode;
            q.UpdateTime = now;
            var up = await _db.UpdateAsync(q,
                nameof(CertExpertTaskQueue.QueueStatus),
                nameof(CertExpertTaskQueue.UpdateBy), nameof(CertExpertTaskQueue.UpdateTime));
            if (!up.Success) return Result<object>.Fail(up.Error);

            await InsertLogAsync(new CertExpertTaskLog
            {
                TaskCode = q.TaskCode,
                SubTaskCode = q.SubTaskCode,
                QueueCode = queueCode,
                LogAction = ExpertTaskConst.LogAction.QueuePaused,
                LogLevel = ExpertTaskConst.LogLevel.Warn,
                Message = "暂停队列（已在执行的项会跑完，之后不再领取新项）",
                OperatorCode = userCode,
                OperatorName = userName,
                ClientIp = clientIp,
                OrgCode = orgCode,
                OperateTime = now,
                CreateTime = now
            });

            return Result<object>.Ok(new { Message = "队列已暂停" });
        }

        /// <summary>重试失败项（failed → pending，重试次数归零）</summary>
        public async Task<Result<object>> RetryFailedAsync(
            string orgCode, string taskCode, string userCode, string? userName, string? clientIp)
        {
            var task = (await _db.GetOneAsync<CertExpertTask>(x =>
                x.Code == taskCode && x.OrgCode == orgCode && !x.IsDeleted)).Data;
            if (task == null) return Result<object>.Fail("任务不存在或不属于当前工作区");

            var queues = (await _db.GetListAsync<CertExpertTaskQueue>(x =>
                x.TaskCode == taskCode && !x.IsDeleted)).Data ?? new List<CertExpertTaskQueue>();
            var queueCodes = queues.Select(x => x.Code ?? "").ToList();

            var items = (await _db.GetListAsync<CertExpertTaskQueueItem>(x =>
                queueCodes.Contains(x.QueueCode)
                && x.ItemStatus == ExpertTaskConst.Item.Failed
                && !x.IsDeleted)).Data ?? new List<CertExpertTaskQueueItem>();

            if (items.Count == 0) return Result<object>.Ok(new { Message = "没有失败项可重试", Count = 0 });

            var now = DateTime.Now;
            foreach (var it in items)
            {
                it.ItemStatus = ExpertTaskConst.Item.Pending;
                it.RetryCount = 0;
                it.ErrorType = null;
                it.ErrorMessage = null;
                it.NextRetryAt = null;
                it.LockCode = null;
                it.LockedUntil = null;
                it.UpdateTime = now;
                await _db.UpdateAsync(it,
                    nameof(CertExpertTaskQueueItem.ItemStatus),
                    nameof(CertExpertTaskQueueItem.RetryCount),
                    nameof(CertExpertTaskQueueItem.ErrorType),
                    nameof(CertExpertTaskQueueItem.ErrorMessage),
                    nameof(CertExpertTaskQueueItem.NextRetryAt),
                    nameof(CertExpertTaskQueueItem.LockCode),
                    nameof(CertExpertTaskQueueItem.LockedUntil),
                    nameof(CertExpertTaskQueueItem.UpdateTime));
            }

            // 队列回到 running（若原本 failed / completed）
            foreach (var q in queues.Where(x =>
                x.QueueStatus == ExpertTaskConst.Queue.Failed
                || x.QueueStatus == ExpertTaskConst.Queue.Completed))
            {
                q.QueueStatus = ExpertTaskConst.Queue.Running;
                q.FinishTime = null;
                q.UpdateTime = now;
                await _db.UpdateAsync(q,
                    nameof(CertExpertTaskQueue.QueueStatus),
                    nameof(CertExpertTaskQueue.FinishTime),
                    nameof(CertExpertTaskQueue.UpdateTime));
            }

            task.ExecStatus = ExpertTaskConst.Exec.Running;
            task.UpdateTime = now;
            await _db.UpdateAsync(task,
                nameof(CertExpertTask.ExecStatus), nameof(CertExpertTask.UpdateTime));

            await InsertLogAsync(new CertExpertTaskLog
            {
                TaskCode = taskCode,
                LogAction = ExpertTaskConst.LogAction.QueueStarted,
                LogLevel = ExpertTaskConst.LogLevel.Info,
                Message = $"重试失败项 {items.Count} 个",
                OperatorCode = userCode,
                OperatorName = userName,
                ClientIp = clientIp,
                OrgCode = orgCode,
                OperateTime = now,
                CreateTime = now
            });

            return Result<object>.Ok(new { Message = $"已重置 {items.Count} 个失败项", Count = items.Count });
        }

        // ================================================================
        // 六、详情 / 日志 / 缺口
        // ================================================================

        /// <summary>任务详情（详情页 4 Tab 的一次性载荷）</summary>
        public async Task<Result<TaskDetailDto>> GetDetailAsync(string orgCode, string taskCode)
        {
            var task = (await _db.GetOneAsync<CertExpertTask>(x =>
                x.Code == taskCode && x.OrgCode == orgCode && !x.IsDeleted)).Data;
            if (task == null) return Result<TaskDetailDto>.Fail("任务不存在或不属于当前工作区");

            var subs = (await _db.GetListAsync<CertExpertTaskStandard>(x =>
                x.TaskCode == taskCode && !x.IsDeleted)).Data ?? new List<CertExpertTaskStandard>();
            var queues = (await _db.GetListAsync<CertExpertTaskQueue>(x =>
                x.TaskCode == taskCode && !x.IsDeleted)).Data ?? new List<CertExpertTaskQueue>();
            var qItems = (await _db.GetListAsync<CertExpertTaskQueueItem>(x =>
                x.TaskCode == taskCode && !x.IsDeleted)).Data ?? new List<CertExpertTaskQueueItem>();

            var qByCode = queues.ToDictionary(x => x.Code ?? "", x => x);

            // ★ 视图字段补齐 —— `TaskTypeLabel` / `ExecStatusLabel` / `CanSubmit` 等**只在
            //   `OnQueried`（列表路径）填**，详情走的是本方法，若不补这里，详情页
            //   「分类」列会显示空白（列表页却正常，属最难查的一类不一致）。
            task.TaskTypeLabel = TaskTypeLabel(task.TaskType);
            task.ExecStatusLabel = ExecStatusLabel(task.ExecStatus);
            task.CanSubmit = task.ExecStatus is ExpertTaskConst.Exec.Draft or ExpertTaskConst.Exec.Failed
                             && task.LifecycleStatus == ExpertTaskConst.Lifecycle.Active;
            task.CanRetry = task.ExecStatus == ExpertTaskConst.Exec.Failed;
            task.CanViewResult = task.ExecStatus is ExpertTaskConst.Exec.Completed
                or ExpertTaskConst.Exec.Running or ExpertTaskConst.Exec.Failed;
            task.IsBlocking = ExpertTaskConst.IsBlocking(task.LifecycleStatus, task.ExecStatus);

            var dto = new TaskDetailDto
            {
                Task = task,
                EnterpriseName = task.EnterpriseName ?? "",
                StageName = task.StageName ?? ""
            };

            dto.Standards = subs.OrderBy(x => x.Sort).Select(s =>
            {
                qByCode.TryGetValue(s.QueueCode ?? "", out var q);
                var mine = qItems.Where(i => i.QueueCode == s.QueueCode).ToList();
                return new TaskStandardDto
                {
                    Code = s.Code ?? "",
                    StandardCode = s.StandardCode,
                    StandardName = s.StandardName ?? s.StandardCode,
                    // ★ 检查项数口径：**队列未生成时用子任务上的「计划数」**。
                    //   `s.ItemCount` 在创建时由 `ItemCount = g.Count()` 写入（= 本次该标准要跑的检查项数），
                    //   `SubmitAsync` 生成队列后 `RefreshSubTaskAsync` 会把它同步成队列项数。
                    //   ⛔ 此前直接用 `mine.Count`（队列项数）⇒ **创建完还没点「提交执行」时，
                    //   详情 Tab1「检查项」恒显示 0，而头部「检查项」显示 2** ——
                    //   同一屏两个数字互相矛盾（DB 里 `ItemCount=2` 是对的）。
                    ItemCount = mine.Count > 0 ? mine.Count : s.ItemCount,
                    DoneCount = mine.Count(i => i.ItemStatus is ExpertTaskConst.Item.Completed),
                    FailedCount = mine.Count(i => i.ItemStatus == ExpertTaskConst.Item.Failed),
                    SkippedCount = mine.Count(i => i.ItemStatus == ExpertTaskConst.Item.Skipped),
                    ExecStatus = s.ExecStatus,
                    LifecycleStatus = s.LifecycleStatus,
                    QueueCode = s.QueueCode,
                    QueueStatus = q?.QueueStatus,
                    Progress = mine.Count > 0
                        ? Math.Round((decimal)mine.Count(i => i.ItemStatus is
                            ExpertTaskConst.Item.Completed or ExpertTaskConst.Item.Failed
                            or ExpertTaskConst.Item.Skipped) / mine.Count * 100, 2)
                        : 0m
                };
            }).ToList();

            dto.Queues = queues.OrderBy(x => x.CreateTime).Select(q =>
            {
                var mine = qItems.Where(i => i.QueueCode == q.Code).ToList();
                var total = mine.Count;
                var done = mine.Count(i => i.ItemStatus is
                    ExpertTaskConst.Item.Completed or ExpertTaskConst.Item.Failed
                    or ExpertTaskConst.Item.Skipped);
                return new TaskQueueDto
                {
                    Code = q.Code ?? "",
                    StandardCode = q.StandardCode,
                    StandardName = subs.FirstOrDefault(s => s.Code == q.SubTaskCode)?.StandardName ?? q.StandardCode,
                    QueueType = q.QueueType,
                    QueueStatus = q.QueueStatus,
                    TotalCount = total,
                    DoneCount = mine.Count(i => i.ItemStatus == ExpertTaskConst.Item.Completed),
                    FailedCount = mine.Count(i => i.ItemStatus == ExpertTaskConst.Item.Failed),
                    SkippedCount = mine.Count(i => i.ItemStatus == ExpertTaskConst.Item.Skipped),
                    Progress = total > 0 ? Math.Round((decimal)done / total * 100, 2) : 0m,
                    RetryCount = q.RetryCount,
                    MaxRetryCount = q.MaxRetryCount,
                    LockCode = q.LockCode,
                    StartTime = q.StartTime,
                    FinishTime = q.FinishTime,
                    LastError = q.LastError,
                    CanStart = q.QueueStatus is ExpertTaskConst.Queue.Pending or ExpertTaskConst.Queue.Failed,
                    CanPause = q.QueueStatus == ExpertTaskConst.Queue.Running
                };
            }).ToList();

            // 待审批结论数（结论级复核状态，D27 第 2 层下沉点）
            //
            // ★★ 必须同时按 **TaskCode** 过滤，不能只按 ItemCode！
            //    `cert_expert_nc_item` 是**长期实体**（一个企业+阶段+标准+规则只有一条），
            //    同一批检查项会被多轮任务反复执行 ⇒ 结果表里同一 `ItemCode` 有多行（按 `RoundNo` 区分）。
            //    ⛔ 2026-09-30 实测踩坑：只按 `itemCodes.Contains(x.ItemCode)` 过滤时，
            //    第 2 轮任务的详情页把**第 1 轮**的结论也统计进来 ⇒「待审批结论」显示 `2 / 4`
            //    （本任务实际只有 2 条）。历史轮次由「历史轮次」抽屉单独看，本页只算本任务。
            if (task.TaskType == ExpertTaskConst.TaskTypeNcCheck)
            {
                var itemCodes = qItems.Select(x => x.TaskItemCode).Distinct().ToList();
                var results = (await _db.GetListAsync<CertExpertNcResult>(x =>
                    x.OrgCode == orgCode && x.TaskCode == taskCode
                    && itemCodes.Contains(x.ItemCode) && !x.IsDeleted)).Data
                    ?? new List<CertExpertNcResult>();
                dto.TotalResultCount = results.Count;
                dto.PendingReviewCount = results.Count(x =>
                    x.ReviewStatus == ExpertTaskConst.Review.PendingReview);
            }
            else
            {
                var itemCodes = qItems.Select(x => x.TaskItemCode).Distinct().ToList();
                var results = (await _db.GetListAsync<CertExpertReportResult>(x =>
                    x.OrgCode == orgCode && x.TaskCode == taskCode
                    && itemCodes.Contains(x.ItemCode) && !x.IsDeleted)).Data
                    ?? new List<CertExpertReportResult>();
                dto.TotalResultCount = results.Count;
                dto.PendingReviewCount = results.Count(x =>
                    x.ReviewStatus == ExpertTaskConst.Review.PendingReview);
            }

            return Result<TaskDetailDto>.Ok(dto);
        }

        /// <summary>
        /// 重算任务头 / 标准子任务的「复核计数」（<c>AckedCount</c> / <c>ModifiedCount</c>）。
        ///
        /// <para><b>为什么需要它</b>：这两个字段是<b>结果侧动作</b>（认可 / 修改）产生的，
        /// 而结果动作在 <c>ExpertResultController</c>，它只写结果表 —— 若不回写，
        /// 任务头/子任务的这两个字段会<b>永远是 0</b>（字段存在但恒为 0 = 静默不一致）。
        /// 每次认可 / 修改后调用一次，保证「任务头计数 == 结果表实际」。</para>
        ///
        /// <para>计数口径：<c>reviewed</c> 计认可；<c>modified</c> 计修改（<b>互斥</b>，不叠加）。</para>
        /// </summary>
        public async Task RefreshReviewCountersAsync(string orgCode, string? taskCode)
        {
            if (string.IsNullOrWhiteSpace(taskCode)) return;

            var task = (await _db.GetOneAsync<CertExpertTask>(x =>
                x.Code == taskCode && x.OrgCode == orgCode && !x.IsDeleted)).Data;
            if (task == null) return;

            var items = (await _db.GetListAsync<CertExpertTaskQueueItem>(x =>
                x.TaskCode == taskCode && !x.IsDeleted)).Data
                ?? new List<CertExpertTaskQueueItem>();
            var itemCodes = items.Select(x => x.TaskItemCode).Distinct().ToList();
            if (itemCodes.Count == 0) return;

            // ★★ 计数必须带 TaskCode —— 检查项是长期实体，同一 ItemCode 有多轮结果，
            //    只按 ItemCode 统计会把**历史轮次**的认可/修改也算进本任务。
            int acked, modified;
            if (task.TaskType == ExpertTaskConst.TaskTypeNcCheck)
            {
                var rs = (await _db.GetListAsync<CertExpertNcResult>(x =>
                    x.OrgCode == orgCode && x.TaskCode == taskCode
                    && itemCodes.Contains(x.ItemCode) && !x.IsDeleted)).Data
                    ?? new List<CertExpertNcResult>();
                acked = rs.Count(x => x.ReviewStatus == ExpertTaskConst.Review.Reviewed);
                modified = rs.Count(x => x.ReviewStatus == ExpertTaskConst.Review.Modified);
            }
            else
            {
                var rs = (await _db.GetListAsync<CertExpertReportResult>(x =>
                    x.OrgCode == orgCode && x.TaskCode == taskCode
                    && itemCodes.Contains(x.ItemCode) && !x.IsDeleted)).Data
                    ?? new List<CertExpertReportResult>();
                acked = rs.Count(x => x.ReviewStatus == ExpertTaskConst.Review.Reviewed);
                modified = rs.Count(x => x.ReviewStatus == ExpertTaskConst.Review.Modified);
            }

            var now = DateTime.Now;

            task.AckedCount = acked;
            task.ModifiedCount = modified;
            task.UpdateTime = now;
            await _db.UpdateAsync(task,
                nameof(CertExpertTask.AckedCount),
                nameof(CertExpertTask.ModifiedCount),
                nameof(CertExpertTask.UpdateTime));

            // 标准子任务（按各自队列项分段统计）
            var subs = (await _db.GetListAsync<CertExpertTaskStandard>(x =>
                x.TaskCode == taskCode && !x.IsDeleted)).Data
                ?? new List<CertExpertTaskStandard>();
            foreach (var sub in subs)
            {
                var mine = items.Where(x => x.QueueCode == sub.QueueCode)
                                .Select(x => x.TaskItemCode).Distinct().ToList();
                if (mine.Count == 0) continue;

                if (task.TaskType == ExpertTaskConst.TaskTypeNcCheck)
                {
                    var rs = (await _db.GetListAsync<CertExpertNcResult>(x =>
                        x.OrgCode == orgCode && x.TaskCode == taskCode
                        && mine.Contains(x.ItemCode) && !x.IsDeleted)).Data
                        ?? new List<CertExpertNcResult>();
                    sub.AckedCount = rs.Count(x => x.ReviewStatus == ExpertTaskConst.Review.Reviewed);
                    sub.ModifiedCount = rs.Count(x => x.ReviewStatus == ExpertTaskConst.Review.Modified);
                }
                else
                {
                    var rs = (await _db.GetListAsync<CertExpertReportResult>(x =>
                        x.OrgCode == orgCode && x.TaskCode == taskCode
                        && mine.Contains(x.ItemCode) && !x.IsDeleted)).Data
                        ?? new List<CertExpertReportResult>();
                    sub.AckedCount = rs.Count(x => x.ReviewStatus == ExpertTaskConst.Review.Reviewed);
                    sub.ModifiedCount = rs.Count(x => x.ReviewStatus == ExpertTaskConst.Review.Modified);
                }

                sub.UpdateTime = now;
                await _db.UpdateAsync(sub,
                    nameof(CertExpertTaskStandard.AckedCount),
                    nameof(CertExpertTaskStandard.ModifiedCount),
                    nameof(CertExpertTaskStandard.UpdateTime));
            }
        }

        /// <summary>运行日志（详情 Tab 3；按时间倒序）</summary>
        public async Task<Result<List<TaskLogDto>>> GetLogsAsync(
            string orgCode, string taskCode, string? queueCode, int take = 200)
        {
            if (take <= 0 || take > 1000) take = 200;

            var rows = (await _db.GetListAsync<CertExpertTaskLog>(x =>
                x.OrgCode == orgCode
                && x.TaskCode == taskCode
                && (queueCode == null || x.QueueCode == queueCode)
                && !x.IsDeleted)).Data ?? new List<CertExpertTaskLog>();

            var dto = rows
                .OrderByDescending(x => x.OperateTime)
                .ThenByDescending(x => x.Id)
                .Take(take)
                .Select(x => new TaskLogDto
                {
                    Code = x.Code ?? "",
                    LogAction = x.LogAction,
                    LogLevel = x.LogLevel,
                    Message = x.Message,
                    QueueCode = x.QueueCode,
                    TaskItemCode = x.TaskItemCode,
                    ItemType = x.ItemType,
                    ItemName = x.ItemName,
                    StandardCode = x.StandardCode,
                    OperatorName = x.OperatorName,
                    DurationMs = x.DurationMs,
                    OperateTime = x.OperateTime,
                    Payload = x.Payload
                }).ToList();

            return Result<List<TaskLogDto>>.Ok(dto);
        }

        /// <summary>数据缺口（详情 Tab 4）</summary>
        public async Task<Result<List<TaskGapDto>>> GetGapsAsync(string orgCode, string taskCode)
        {
            var rows = (await _db.GetListAsync<CertExpertTaskDataGap>(x =>
                x.OrgCode == orgCode && x.TaskCode == taskCode && !x.IsDeleted)).Data
                ?? new List<CertExpertTaskDataGap>();

            var dto = rows.OrderBy(x => x.GapStatus).ThenBy(x => x.Sort)
                .Select(x => new TaskGapDto
                {
                    Code = x.Code ?? "",
                    GapType = x.GapType,
                    GapLabel = x.GapLabel,
                    ExpectedFileName = x.ExpectedFileName,
                    SourceItemName = x.SourceItemName ?? "",
                    SourceItemType = x.SourceItemType,
                    GapStatus = x.GapStatus,
                    StandardCode = x.StandardCode,
                    ClauseCode = x.ClauseCode,
                    FilledValue = x.FilledValue,
                    FilledTime = x.FilledTime,
                    SkipTime = x.SkipTime,
                    SkipReason = x.SkipReason
                }).ToList();

            return Result<List<TaskGapDto>>.Ok(dto);
        }

        /// <summary>
        /// ★★ 未执行清单（2026-10-07 用户裁决 · 裁 2「运行跳过」）
        /// </summary>
        ///
        /// <para><b>用户逐字</b>：「队列如果因为某些问题不能启动，不是任务暂停，而是任务失败，
        /// 而且应该是补全相关信息才开始真正的任务」+「再队列完成后，<b>详细记录，哪些规则或条款
        /// 未执行成功，什么原因</b>」。</para>
        ///
        /// <para><b>两个来源合并</b>：</para>
        /// <list type="number">
        ///   <item><b>缺口来源</b>（<c>cert_expert_task_data_gap</c>）—— 「缺企业数据」这一类，
        ///         含具体缺哪个字段/表格（<b>中文名</b>）+ 应来自哪个文件；</item>
        ///   <item><b>结果来源</b>（<c>cert_expert_nc_result</c> / <c>cert_expert_report_result</c>
        ///         中 <c>AutoStatus=skipped</c>）—— 补上非缺口类原因（规则未配工作流 / 规则停用 /
        ///         人工判定项 / 执行失败）。</item>
        /// </list>
        ///
        /// <para><b>★ 为什么这条记录是必需的</b>：<c>ExpertTaskQueueRunner.RefreshQueueAsync</c>
        /// 在「全部项 skipped」时会把队列置 <c>completed</c> ——
        /// <b>报告看起来跑完了，实际有检查项根本没做</b>，而且完全静默。
        /// 本清单就是把这个静默缺口<b>显式记录下来</b>。</para>
        ///
        /// <para><b>★ 按规则/条款聚合</b>，⛔ 不按缺口逐条罗列 —— 审核员要看的是
        /// 「哪条规则没跑、为什么」，不是「缺了 8 个字段」。</para>
        /// </summary>
        public async Task<Result<TaskUnexecutedResult>> GetUnexecutedAsync(string orgCode, string taskCode)
        {
            var task = (await _db.GetOneAsync<CertExpertTask>(x =>
                x.Code == taskCode && x.OrgCode == orgCode && !x.IsDeleted)).Data;
            if (task == null) return Result<TaskUnexecutedResult>.Fail("任务不存在或不属于当前工作区");

            var isNc = task.TaskType == ExpertTaskConst.TaskTypeNcCheck;
            var itemType = isNc ? ExpertTaskConst.ItemType.NcCheck : ExpertTaskConst.ItemType.ReportSection;

            var items = new Dictionary<string, TaskUnexecutedDto>(StringComparer.Ordinal);

            // 本地函数：按 (规则, 条款) 取槽位（★ 可先使用后声明）
            TaskUnexecutedDto Slot(string itemCode, string? clauseCode)
            {
                var key = $"{itemCode}|{clauseCode ?? "-"}";
                if (!items.TryGetValue(key, out var dto))
                {
                    dto = new TaskUnexecutedDto
                    {
                        ItemCode = itemCode,
                        ItemType = itemType,
                        ClauseCode = clauseCode
                    };
                    items[key] = dto;
                }
                return dto;
            }

            // ── ① 缺口来源（缺企业数据 —— 原因最具体）────────────────
            var gaps = (await _db.GetListAsync<CertExpertTaskDataGap>(x =>
                x.TaskCode == taskCode && !x.IsDeleted)).Data ?? new List<CertExpertTaskDataGap>();

            foreach (var g in gaps)
            {
                var dto = Slot(g.SourceItemCode, g.ClauseCode);
                if (string.IsNullOrWhiteSpace(dto.ItemName) && !string.IsNullOrWhiteSpace(g.SourceItemName))
                    dto.ItemName = g.SourceItemName!;
                if (string.IsNullOrWhiteSpace(dto.StandardCode)) dto.StandardCode = g.StandardCode;

                var label = string.IsNullOrWhiteSpace(g.GapLabel) ? "未命名数据" : g.GapLabel;
                var what = g.GapType == "table" ? $"表格「{label}」" : $"字段「{label}」";
                var state = g.GapStatus switch
                {
                    ExpertTaskConst.GapStatus.Filled => "已补录",
                    ExpertTaskConst.GapStatus.Skipped => "已跳过",
                    _ => "待补录"
                };
                var src = string.IsNullOrWhiteSpace(g.ExpectedFileName)
                    ? ""
                    : $"（应来自 {g.ExpectedFileName}）";
                dto.MissingItems.Add($"{state}{what}{src}");

                // 缺口类原因由 ① 统一承担（比结果行的原因更具体）
                if (string.IsNullOrWhiteSpace(dto.SkipCategory))
                {
                    dto.SkipCategory = g.GapStatus == ExpertTaskConst.GapStatus.Skipped
                        ? ExpertTaskConst.SkipCategory.DataGapSkipped
                        : ExpertTaskConst.SkipCategory.DataGap;
                    dto.SkipCategoryLabel = ExpertTaskConst.SkipCategoryLabel(dto.SkipCategory);
                }
            }

            // ── ② 结果来源（实际被跳过的结果行，补非缺口类原因）──────
            var skipped = new List<(string ItemCode, string? Category, string? Reason)>();
            if (isNc)
            {
                var rows = (await _db.GetListAsync<CertExpertNcResult>(x =>
                    x.TaskCode == taskCode && !x.IsDeleted
                    && x.AutoStatus == ExpertTaskConst.Auto.Skipped)).Data ?? new List<CertExpertNcResult>();
                skipped.AddRange(rows.Select(x => (x.ItemCode, x.SkipCategory, x.SkipReason)));
            }
            else
            {
                var rows = (await _db.GetListAsync<CertExpertReportResult>(x =>
                    x.TaskCode == taskCode && !x.IsDeleted
                    && x.AutoStatus == ExpertTaskConst.Auto.Skipped)).Data ?? new List<CertExpertReportResult>();
                skipped.AddRange(rows.Select(x => (x.ItemCode, x.SkipCategory, x.SkipReason)));
            }

            foreach (var s in skipped)
            {
                if (string.IsNullOrWhiteSpace(s.ItemCode)) continue;
                var dto = Slot(s.ItemCode, null);

                // ⛔ 缺口类原因已被 ① 更精确地描述（含「缺什么 + 应来自哪」），不覆盖
                var already = dto.SkipCategory == ExpertTaskConst.SkipCategory.DataGap
                              || dto.SkipCategory == ExpertTaskConst.SkipCategory.DataGapSkipped;

                var cat = string.IsNullOrWhiteSpace(s.Category)
                    ? ExpertTaskConst.SkipCategory.ExecFailed
                    : s.Category!;

                if (!already)
                {
                    dto.SkipCategory = cat;
                    dto.SkipCategoryLabel = ExpertTaskConst.SkipCategoryLabel(cat);
                    if (!string.IsNullOrWhiteSpace(s.Reason)) dto.Reason = s.Reason!;
                }
            }

            // ── ③ 回填规则/章节名 + 条款（结果来源本身不带名字）──────
            var needNames = items.Values
                .Where(x => string.IsNullOrWhiteSpace(x.ItemName))
                .Select(x => x.ItemCode)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct()
                .ToList();

            if (needNames.Count > 0)
            {
                if (isNc)
                {
                    var ncItems = (await _db.GetListAsync<CertExpertNcItem>(x =>
                        needNames.Contains(x.Code!) && !x.IsDeleted)).Data ?? new List<CertExpertNcItem>();
                    foreach (var it in ncItems)
                        FillNames(it.Code ?? "", it.RuleName, it.ClauseCode, it.ClauseTitle);
                }
                else
                {
                    var secItems = (await _db.GetListAsync<CertExpertReportSectionItem>(x =>
                        needNames.Contains(x.Code!) && !x.IsDeleted)).Data
                        ?? new List<CertExpertReportSectionItem>();
                    foreach (var it in secItems)
                        FillNames(it.Code ?? "", it.SectionName, it.ClauseCode, it.ClauseTitle);
                }
            }

            void FillNames(string code, string? name, string? clauseCode, string? clauseTitle)
            {
                foreach (var dto in items.Values.Where(x => x.ItemCode == code))
                {
                    if (string.IsNullOrWhiteSpace(dto.ItemName) && !string.IsNullOrWhiteSpace(name))
                        dto.ItemName = name!;
                    dto.ClauseCode ??= clauseCode;
                    dto.ClauseTitle ??= clauseTitle;
                }
            }

            // ── ④ 兜底文案（⛔ 界面不得出现空白的规则名 / 空原因）────
            foreach (var dto in items.Values)
            {
                if (string.IsNullOrWhiteSpace(dto.ItemName)) dto.ItemName = "（未知规则或章节）";
                if (string.IsNullOrWhiteSpace(dto.SkipCategory))
                {
                    dto.SkipCategory = ExpertTaskConst.SkipCategory.DataGap;
                    dto.SkipCategoryLabel = ExpertTaskConst.SkipCategoryLabel(dto.SkipCategory);
                }
                if (string.IsNullOrWhiteSpace(dto.Reason))
                {
                    dto.Reason = dto.MissingItems.Count > 0
                        ? $"{dto.SkipCategoryLabel}：{string.Join("；", dto.MissingItems)}"
                        : dto.SkipCategoryLabel;
                }
            }

            // ── ⑤ 队列是否已全部结束（未结束 ⇒ 清单还会变）──────────
            var queues = (await _db.GetListAsync<CertExpertTaskQueue>(x =>
                x.TaskCode == taskCode && !x.IsDeleted)).Data ?? new List<CertExpertTaskQueue>();
            var finished = queues.Count == 0 || queues.All(q =>
                q.QueueStatus == ExpertTaskConst.Queue.Completed
                || q.QueueStatus == ExpertTaskConst.Queue.Failed
                || q.QueueStatus == ExpertTaskConst.Queue.Cancelled);

            var list = items.Values
                .OrderByDescending(x => x.SkipCategory == ExpertTaskConst.SkipCategory.DataGap
                                        || x.SkipCategory == ExpertTaskConst.SkipCategory.DataGapSkipped)
                .ThenBy(x => x.ItemName, StringComparer.Ordinal)
                .ToList();

            return Result<TaskUnexecutedResult>.Ok(new TaskUnexecutedResult
            {
                Items = list,
                TotalCount = list.Count,
                DataGapCount = list.Count(x =>
                    x.SkipCategory == ExpertTaskConst.SkipCategory.DataGap
                    || x.SkipCategory == ExpertTaskConst.SkipCategory.DataGapSkipped),
                IsQueueFinished = finished,
                ExecStatus = task.ExecStatus
            });
        }

        // ================================================================
        // 七、辅助
        // ================================================================

        private async Task<List<ISOStandard>> LoadStandardsAsync(List<string>? codes)
        {
            var all = (await _db.GetListAsync<ISOStandard>(x => x.IsValid == 1 && !x.IsDeleted)).Data
                ?? new List<ISOStandard>();
            if (codes is { Count: > 0 })
                all = all.Where(x => codes.Contains(x.Code ?? "")).ToList();
            return all;
        }

        /// <summary>写审计日志（⛔ 日志表只 INSERT，不 UPDATE/DELETE）</summary>
        public async Task InsertLogAsync(CertExpertTaskLog log)
        {
            try
            {
                log.Code ??= Guid.NewGuid().ToString("N");
                log.IsValid = 1;
                log.IsDeleted = false;
                if (log.OperateTime == default) log.OperateTime = DateTime.Now;
                if (log.CreateTime == default) log.CreateTime = log.OperateTime;
                log.Sort = 0;
                await _db.InsertAsync(log);
            }
            catch (Exception ex)
            {
                // 日志失败绝不影响主流程
                _logger.LogWarning(ex, "[ExpertTask] 写日志失败 action={Action}", log.LogAction);
            }
        }
    }
}

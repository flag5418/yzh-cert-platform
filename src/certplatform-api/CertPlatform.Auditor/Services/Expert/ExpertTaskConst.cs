namespace CertPlatform.Auditor.Services.Expert
{
    /// <summary>
    /// 专家任务系统 —— 常量与状态机定义（唯一权威源）
    ///
    /// <para><b>为什么集中定义</b>：本系统最大的认知风险是「<b>三个状态混用</b>」。
    /// 把它们放在一个文件里、每个都写清「谁管 / 决定什么」，是唯一能防住这件事的办法。</para>
    ///
    /// <para>权威依据：<c>docs/40-实施/专家任务设计/12-业务全景图-任务平台-V1.md</c>（D01/D27/D36）
    /// 与 <c>23-任务系统界面设计-V1.md</c> §2.6。</para>
    /// </summary>
    public static class ExpertTaskConst
    {
        // ══════════════════════════════════════════════════════════════
        // 一、任务类型（D01：单一任务表 + TaskType 区分）
        // ══════════════════════════════════════════════════════════════

        /// <summary>NC 检查（候选来源 = <c>cert_validation_rule</c>）</summary>
        public const string TaskTypeNcCheck = "NC_CHECK";

        /// <summary>报告生成（候选来源 = <c>cert_report_section</c>）</summary>
        public const string TaskTypeReportGenerate = "REPORT_GENERATE";

        // ══════════════════════════════════════════════════════════════
        // 二、★ 状态三层分离（D27）—— 混用是本系统最容易犯的错
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// <b>第 1 层 · 执行状态（机器管）</b> —— 「跑完没有」。
        /// <para>★ <b>唯一决定 D36 业务锁</b>：<see cref="ExecCompleted"/> 即解锁。</para>
        /// </summary>
        public static class Exec
        {
            /// <summary>草稿（已建任务，未提交执行）</summary>
            public const string Draft = "draft";

            /// <summary>已提交、队列已生成、等待启动</summary>
            public const string PendingRun = "pending_run";

            /// <summary>队列执行中</summary>
            public const string Running = "running";

            /// <summary>★ 全部队列结束（<b>解锁</b>）</summary>
            public const string Completed = "completed";

            /// <summary>存在失败项且不再重试</summary>
            public const string Failed = "failed";
        }

        /// <summary>
        /// <b>第 2 层 · 结果状态（专家管）</b> —— 「看完没有」。
        /// <para>⚠️ <b>不影响任何逻辑</b>。任务头上的该字段仅保留（避免改表），<b>不进界面</b>；
        /// 真正的复核状态是<b>结论级</b>的，在结果表上。</para>
        /// </summary>
        public static class Review
        {
            public const string NotStarted = "not_started";
            public const string PendingReview = "pending_review";
            public const string Reviewed = "reviewed";
            public const string Modified = "modified";
            public const string Skipped = "skipped";
            public const string Frozen = "frozen";
        }

        /// <summary>
        /// <b>第 3 层 · 存续状态（管理控制）</b> —— 「这个任务还算不算数」。
        /// <para>★ 也是 D36 锁的组成条件：只有 <see cref="Active"/> 才占锁。</para>
        /// </summary>
        public static class Lifecycle
        {
            public const string Active = "active";
            public const string Archived = "archived";
            public const string Cancelled = "cancelled";
            public const string Voided = "voided";
        }

        /// <summary>队列状态</summary>
        public static class Queue
        {
            public const string Pending = "pending";
            public const string Running = "running";
            public const string Completed = "completed";
            public const string Failed = "failed";
            public const string Cancelled = "cancelled";
        }

        /// <summary>队列项状态</summary>
        public static class Item
        {
            public const string Pending = "pending";
            public const string Running = "running";
            public const string Completed = "completed";
            public const string Failed = "failed";
            public const string Skipped = "skipped";
            public const string Cancelled = "cancelled";
        }

        /// <summary>队列项类型（= <c>cert_expert_task_queue_item.ItemType</c>）</summary>
        public static class ItemType
        {
            public const string NcCheck = "nc_check";
            public const string ReportSection = "report_section";
        }

        /// <summary>自动结果状态（结果表 <c>AutoStatus</c>）</summary>
        public static class Auto
        {
            public const string None = "none";
            public const string Ok = "ok";
            public const string Ng = "ng";
            public const string Skipped = "skipped";
            public const string Failed = "failed";

            /// <summary>章节未配 DAG，用模板示例正文降级</summary>
            public const string Degraded = "degraded";
        }

        /// <summary>★ 缺口状态（<c>cert_expert_task_data_gap.GapStatus</c>）</summary>
        public static class GapStatus
        {
            /// <summary>待补录 / 待跳过</summary>
            public const string Pending = "pending";

            /// <summary>已补录（值写进 <c>cert_extraction_result</c>，<c>ValueSource='manual'</c>）</summary>
            public const string Filled = "filled";

            /// <summary>已跳过（客户可以忽略 ⇒ 依赖它的任务项执行时标 <c>skipped</c>）</summary>
            public const string Skipped = "skipped";
        }

        /// <summary>★ 跳过分类 —— 界面必须<b>完整展示原因</b>，否则专家会以为系统漏检</summary>
        public static class SkipCategory
        {
            /// <summary>依赖的企业数据缺失（已生成缺口，待补录）</summary>
            public const string DataGap = "data_gap";

            /// <summary>缺口被专家跳过</summary>
            public const string DataGapSkipped = "data_gap_skipped";

            /// <summary>规则/章节未配置工作流 DAG</summary>
            public const string NoRule = "no_rule";

            /// <summary>规则已停用</summary>
            public const string RuleDisabled = "rule_disabled";

            /// <summary>人工判定项（<c>JudgeMode=manual</c>）—— 机器不猜，交人工</summary>
            public const string ManualMode = "manual_mode";

            /// <summary>执行失败（已记录失败原因）</summary>
            public const string ExecFailed = "exec_failed";
        }

        /// <summary>任务来源（D25）</summary>
        public static class TaskSource
        {
            /// <summary>全新</summary>
            public const string New = "NEW";

            /// <summary>整体重执行</summary>
            public const string Redo = "REDO";

            /// <summary>局部更新</summary>
            public const string Patch = "PATCH";
        }

        /// <summary>范围类型</summary>
        public static class Scope
        {
            public const string Full = "FULL";
            public const string Partial = "PARTIAL";
        }

        // ══════════════════════════════════════════════════════════════
        // 三、日志动作（23 号 §四 埋点清单）
        // ══════════════════════════════════════════════════════════════

        public static class LogAction
        {
            public const string TaskCreated = "task.created";
            public const string TaskLockAcquired = "task.lock.acquired";
            public const string TaskSubmitted = "task.submitted";
            public const string TaskProgress = "task.progress";
            public const string TaskPaused = "task.paused";
            public const string TaskResumed = "task.resumed";
            public const string TaskFinished = "task.finished";
            public const string TaskCancelled = "task.cancelled";
            public const string TaskArchived = "task.archived";
            public const string ScopeResolved = "scope.resolved";
            public const string ItemDerived = "item.derived";
            public const string QueueCreated = "queue.created";
            public const string QueueStarted = "queue.started";
            public const string QueuePaused = "queue.paused";
            public const string QueueProgress = "queue.progress";
            public const string QueueFinished = "queue.finished";
            public const string ItemExtractOk = "item.extract.ok";
            public const string ItemExtractRetry = "item.extract.retry";
            public const string ItemExtractFail = "item.extract.fail";
            public const string ItemJudgeOk = "item.judge.ok";
            public const string ItemJudgeNc = "item.judge.nc";
            public const string ItemSkip = "item.skip";
            public const string GapCreated = "gap.created";

            public const string Acknowledge = "ACKNOWLEDGE";
            public const string Modify = "MODIFY";
            public const string Skip = "SKIP";
            public const string Export = "EXPORT";
            public const string SkipAllGaps = "SKIP_ALL_GAPS";
        }

        /// <summary>日志级别</summary>
        public static class LogLevel
        {
            public const string Info = "info";
            public const string Warn = "warn";
            public const string Error = "error";
        }

        // ══════════════════════════════════════════════════════════════
        // 四、D36 业务锁
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// 「未结束」判据 —— <b>唯一权威定义</b>。
        /// <para>与 <c>cert_expert_task.ActiveLockKey</c> 生成列的表达式<b>逐字一致</b>：
        /// <c>LifecycleStatus='active' AND ExecStatus &lt;&gt; 'completed'</c>。</para>
        /// <para>⛔ 任何地方判「任务是否占锁」都必须走这里，不得各自写一套。</para>
        /// </summary>
        public static bool IsBlocking(string? lifecycleStatus, string? execStatus) =>
            string.Equals(lifecycleStatus, Lifecycle.Active, System.StringComparison.Ordinal)
            && !string.Equals(execStatus, Exec.Completed, System.StringComparison.Ordinal);

        /// <summary>任务编号前缀</summary>
        public const string TaskNumberPrefix = "TSK";
    }
}

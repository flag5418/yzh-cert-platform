using System;
using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Expert
{
    /// <summary>
    /// 专家任务头（专家任务系统 A 组 · 任务执行域）
    /// <para>表名：<c>cert_expert_task</c>（DDL 权威 → <c>scripts/db/20260930_expert_task_tables_V1.sql</c>）</para>
    ///
    /// <para><b>D01 单一任务表 + TaskType</b>：NC 检查与报告生成<b>共用一张表</b>，
    /// 靠 <see cref="TaskType"/> 区分。理由：任务的生命周期（建立 → 提交 → 队列执行 → 结果）
    /// 完全同构，分表会让「任务列表」「导航树」「队列调度」三处都出现 if/else 分支。</para>
    ///
    /// <para><b>D27 状态三层分离</b>（⚠️ 混用是本系统最容易犯的错）：</para>
    /// <list type="number">
    ///   <item><see cref="ExecStatus"/> —— <b>机器管</b>：draft / pending_run / running / completed / failed。
    ///         决定业务锁是否释放（D36）。</item>
    ///   <item><see cref="ReviewStatus"/> —— <b>专家管</b>：not_started / pending_review / reviewed / modified / skipped / frozen。
    ///         ⚠️ <b>仅保留字段，界面不展示</b> —— 真正的复核状态是<b>结论级</b>的，
    ///         落在 <c>cert_expert_nc_result.ReviewStatus</c> / <c>cert_expert_report_result.ReviewStatus</c>。</item>
    ///   <item><see cref="LifecycleStatus"/> —— <b>管理控制</b>：active / archived / cancelled / voided。</item>
    /// </list>
    ///
    /// <para><b>★★ D36 业务锁（分类型串行锁）</b>：锁键 = <c>OrgCode|EnterpriseCode|StageCode|TaskType</c>；
    /// 「未结束」判据 = <c>LifecycleStatus='active' AND ExecStatus&lt;&gt;'completed'</c>。
    /// 由 MySQL <b>生成列</b> <see cref="ActiveLockKey"/> + 唯一索引 <c>uk_active_lock</c> 兜底：
    /// 唯一索引允许多个 NULL ⇒ 已结束任务不冲突（<b>锁自动释放，无需显式解锁</b>）。
    /// 语义 = 「同一企业 + 同一阶段 + 同一类型，同时只允许一个未结束任务」，
    /// 不同 TaskType（NC_CHECK / REPORT_GENERATE）各锁各的。</para>
    ///
    /// <para><b>⛔ 生成列不得赋值</b>：<see cref="ActiveLockKey"/> 是 <c>STORED</c> 生成列，
    /// 应用层写入会触发 MySQL <c>ERROR 3105</c>，故标注
    /// <c>IsOnlyIgnoreInsert/IsOnlyIgnoreUpdate</c> 从 INSERT/UPDATE 列集合中排除。</para>
    ///
    /// <para>命名规范（YZH 铁律七）：DB 列名 = C# 属性名 = TS 字段名 = PascalCase</para>
    /// </summary>
    [SugarTable("cert_expert_task")]
    public class CertExpertTask : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / CreateTime / CreateBy / UpdateTime / UpdateBy 由 BaseEntity 提供 ────

        /// <summary>★ 专家工作区编码（租户隔离键；★ 非认证机构；★ 禁止为 NULL）</summary>
        [SugarColumn(Length = 50)]
        public string OrgCode { get; set; } = string.Empty;

        /// <summary>创建人姓名（冗余，防改名后追溯断裂）</summary>
        [SugarColumn(Length = 100, IsNullable = true)]
        public string? CreateName { get; set; }

        /// <summary>业务状态（保留字段，本期不参与逻辑）</summary>
        [SugarColumn(Length = 50, IsNullable = true)]
        public string? Status { get; set; }

        /// <summary>排序号</summary>
        public int Sort { get; set; }

        /// <summary>备注</summary>
        [SugarColumn(Length = 500, IsNullable = true)]
        public string? Remark { get; set; }

        // ──── 任务标识 ────

        /// <summary>★任务名称（D29 专家自定义必填）</summary>
        [SugarColumn(Length = 200)]
        public string TaskName { get; set; } = string.Empty;

        /// <summary>任务编号 TSK-yyyyMMdd-NNNN（系统生成，<b>仅展示，不作关联键</b>；关联一律用 Code，准则 A）</summary>
        [SugarColumn(Length = 50)]
        public string TaskNumber { get; set; } = string.Empty;

        /// <summary>任务类型：<c>NC_CHECK</c> | <c>REPORT_GENERATE</c>（D01；同时是 D36 业务锁的组成段）</summary>
        [SugarColumn(Length = 20)]
        public string TaskType { get; set; } = string.Empty;

        /// <summary>范围类型：<c>FULL</c>=全局 | <c>PARTIAL</c>=局部</summary>
        [SugarColumn(Length = 20)]
        public string ScopeType { get; set; } = "FULL";

        /// <summary>★任务来源（D25）：<c>NEW</c>=全新 | <c>REDO</c>=整体重执行 | <c>PATCH</c>=局部更新</summary>
        [SugarColumn(Length = 20)]
        public string TaskSource { get; set; } = "NEW";

        /// <summary>范围快照（勾选/沿用/排除的 Code 数组 JSON），FULL 时为 NULL</summary>
        [SugarColumn(ColumnDataType = "json", IsNullable = true)]
        public string? ScopeSnapshot { get; set; }

        // ──── 业务主体（冗余快照，列表页免 JOIN） ────

        /// <summary>企业编码（<c>cert_enterprise.Code</c>）</summary>
        [SugarColumn(Length = 36)]
        public string EnterpriseCode { get; set; } = string.Empty;

        /// <summary>企业名称（冗余快照）</summary>
        [SugarColumn(Length = 200, IsNullable = true)]
        public string? EnterpriseName { get; set; }

        /// <summary>阶段编码（★<c>cert_cert_stage.Code</c>，GUID，<b>不是业务码 jd01/03</b>）</summary>
        [SugarColumn(Length = 36)]
        public string StageCode { get; set; } = string.Empty;

        /// <summary>阶段名称（冗余快照）</summary>
        [SugarColumn(Length = 100, IsNullable = true)]
        public string? StageName { get; set; }

        /// <summary>涉及标准 Code 数组（JSON 冗余，列表页免 JOIN）</summary>
        [SugarColumn(ColumnDataType = "json", IsNullable = true)]
        public string? StandardCodes { get; set; }

        /// <summary>涉及标准名称数组（JSON 冗余）</summary>
        [SugarColumn(ColumnDataType = "json", IsNullable = true)]
        public string? StandardNames { get; set; }

        /// <summary>涉及标准数</summary>
        public int StandardCount { get; set; }

        // ──── ★ D27 状态三层 ────

        /// <summary>★第 1 层 执行状态（机器管）：draft | pending_run | running | completed | failed</summary>
        [SugarColumn(Length = 20)]
        public string ExecStatus { get; set; } = "draft";

        /// <summary>★第 2 层 结果状态（专家管）。⚠️ 仅保留字段，界面不展示（结论级状态在结果表）</summary>
        [SugarColumn(Length = 20)]
        public string ReviewStatus { get; set; } = "not_started";

        /// <summary>★第 3 层 存续状态（管理控制）：active | archived | cancelled | voided</summary>
        [SugarColumn(Length = 20)]
        public string LifecycleStatus { get; set; } = "active";

        /// <summary>预留：认证周期审核事件（D26 本期不建表，仅留字段）</summary>
        [SugarColumn(Length = 36, IsNullable = true)]
        public string? AuditEventCode { get; set; }

        /// <summary>预留：事件名（如 SV-1 第一次监督审核）</summary>
        [SugarColumn(Length = 100, IsNullable = true)]
        public string? AuditEventName { get; set; }

        // ──── 计数与进度 ────

        /// <summary>本轮涉及的任务项总数</summary>
        public int TotalItemCount { get; set; }

        /// <summary>已认可数（reviewed）</summary>
        public int AckedCount { get; set; }

        /// <summary>已修改数（modified）</summary>
        public int ModifiedCount { get; set; }

        /// <summary>已跳过数（skipped）</summary>
        public int SkippedCount { get; set; }

        /// <summary>待认可数（pending_review）</summary>
        public int PendingCount { get; set; }

        /// <summary>执行失败数</summary>
        public int FailedCount { get; set; }

        /// <summary>未处理数据缺口数</summary>
        public int GapCount { get; set; }

        /// <summary>执行进度 %（队列执行进度，<b>非认可进度</b>）</summary>
        [SugarColumn(DecimalDigits = 2, ColumnDataType = "decimal(5,2)")]
        public decimal Progress { get; set; }

        // ──── 数据缺口 ────

        /// <summary>专家是否点了「跳过全部补录」</summary>
        public bool SkipAllGaps { get; set; }

        /// <summary>「跳过全部补录」时间</summary>
        public DateTime? SkipAllGapsTime { get; set; }

        // ──── 时间戳 ────

        /// <summary>提交执行时间</summary>
        public DateTime? SubmitTime { get; set; }

        /// <summary>全部队列结束时间</summary>
        public DateTime? FinishTime { get; set; }

        // ──── ★★ 生成列（D36 业务锁） ────

        /// <summary>
        /// ★业务锁键（D36）：<c>OrgCode|EnterpriseCode|StageCode|TaskType</c>；已结束（非 active 或 completed）= NULL。
        /// <para><b>MySQL 生成列（STORED）</b> —— ⛔ 应用层不得赋值，否则 <c>ERROR 3105</c>。</para>
        /// </summary>
        [SugarColumn(Length = 200, IsNullable = true,
            IsOnlyIgnoreInsert = true, IsOnlyIgnoreUpdate = true)]
        public string? ActiveLockKey { get; set; }

        // ──── ISoftDelete / IIsValid 接口字段 ────

        /// <summary>软删除标记（false=正常，true=已删除）</summary>
        public bool IsDeleted { get; set; }

        /// <summary>删除人 Code</summary>
        public string? DeleteBy { get; set; }

        /// <summary>删除时间</summary>
        public DateTime? DeleteTime { get; set; }

        /// <summary>有效标志（1=有效，0=无效）—— 铁律九：唯一启用字段</summary>
        public int IsValid { get; set; } = 1;

        // ══════════════════════════════════════════════════════════
        // ★ 视图字段（服务端 OnQueried 填充，⛔ 不入库）
        //
        // 为什么放服务端算：列表要显示中文状态与按钮可用性，而按钮可用性由
        // 「三层状态」推导（draft 才可提交 / failed 才可重试）。推导规则是业务逻辑，
        // 放前端 = 前端硬编码枚举，枚举一变就错。
        // ══════════════════════════════════════════════════════════

        /// <summary>视图：执行状态中文标签（如「执行中」）</summary>
        [SugarColumn(IsIgnore = true)]
        public string? ExecStatusLabel { get; set; }

        /// <summary>视图：任务类型中文标签（如「NC 检查」）</summary>
        [SugarColumn(IsIgnore = true)]
        public string? TaskTypeLabel { get; set; }

        /// <summary>视图：是否可提交执行（draft / failed 且 active）</summary>
        [SugarColumn(IsIgnore = true)]
        public bool CanSubmit { get; set; }

        /// <summary>视图：是否可重试失败项（failed）</summary>
        [SugarColumn(IsIgnore = true)]
        public bool CanRetry { get; set; }

        /// <summary>视图：是否可查看结果（completed / running / failed）</summary>
        [SugarColumn(IsIgnore = true)]
        public bool CanViewResult { get; set; }

        /// <summary>视图：是否正在占用 D36 业务锁（= 未结束）</summary>
        [SugarColumn(IsIgnore = true)]
        public bool IsBlocking { get; set; }
    }
}

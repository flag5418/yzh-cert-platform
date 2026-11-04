using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CertPlatform.Auditor.Entities.Expert;
using CertPlatform.Shared.Entities.Cert;
using Microsoft.Extensions.Logging;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Models.Result;

namespace CertPlatform.Auditor.Services.Expert
{
    // ══════════════════════════════════════════════════════════════════════
    // 请求 / 响应契约
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>创建认证任务请求</summary>
    public class CertTaskCreateRequest
    {
        /// <summary>计划 Code</summary>
        public string PlanCode { get; set; } = string.Empty;

        /// <summary>方向：prep / onsite（默认 onsite）</summary>
        public string TaskKind { get; set; } = "onsite";

        /// <summary>提示词模板 Code</summary>
        public string? PromptCode { get; set; }

        /// <summary>重跑模式（仅 onsite：full / incremental / selective）</summary>
        public string RunMode { get; set; } = "incremental";
    }

    /// <summary>创建认证任务响应</summary>
    public class CertTaskCreateResponse
    {
        public string TaskCode { get; set; } = string.Empty;
        public string TaskNo { get; set; } = string.Empty;
        public int ItemTotal { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>任务启动响应</summary>
    public class CertTaskStartResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>证据 MD5 变更检测响应</summary>
    public class EvidenceChangeResult
    {
        public string EvidenceCode { get; set; } = string.Empty;
        public string CheckItemCode { get; set; } = string.Empty;
        public string? OldMD5 { get; set; }
        public string? NewMD5 { get; set; }
        public bool HasChanged { get; set; }
    }

    /// <summary>影响分析结果</summary>
    public class ImpactAnalysisResult
    {
        public string ImpactLogCode { get; set; } = string.Empty;
        public int AffectedCheckItemCount { get; set; }
        public int AffectedTaskItemCount { get; set; }
        public int SkippedLockedCount { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>分页查询请求</summary>
    public class CertTaskPageRequest
    {
        public string? Keyword { get; set; }
        public string? Status { get; set; }
        public string? TaskKind { get; set; }
        public int PageIndex { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    /// <summary>分页查询响应行</summary>
    public class CertTaskRow
    {
        public string Code { get; set; } = string.Empty;
        public string TaskNo { get; set; } = string.Empty;
        public string TaskKind { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int ItemTotal { get; set; }
        public int ItemDone { get; set; }
        public int ItemFailed { get; set; }
        public int Progress { get; set; }
        public string? CreateBy { get; set; }
        public DateTime CreateTime { get; set; }
    }

    /// <summary>分页查询响应</summary>
    public class CertTaskPageResponse
    {
        public int Total { get; set; }
        public List<CertTaskRow> Rows { get; set; } = new();
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>任务项行</summary>
    public class CertTaskItemRow
    {
        public string Code { get; set; } = string.Empty;
        public string TaskCode { get; set; } = string.Empty;
        public string CheckItemCode { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string EvidenceState { get; set; } = string.Empty;
        public string AnalysisState { get; set; } = string.Empty;
        public string RunMode { get; set; } = string.Empty;
        public bool SkipLocked { get; set; }
        public string RerunState { get; set; } = string.Empty;
        public string? ClauseNumbers { get; set; }
        public string? DeptNames { get; set; }
        public int Sort { get; set; }
    }

    /// <summary>检查结果行（对齐 cert_check_result 实体字段）</summary>
    public class CertCheckResultRow
    {
        public string Code { get; set; } = string.Empty;

        /// <summary>所属分析（一次分析一行，见 cert_check_analysis）</summary>
        public string AnalysisCode { get; set; } = string.Empty;

        /// <summary>分支：prep / onsite</summary>
        public string? StageKind { get; set; }

        public string CheckItemCode { get; set; } = string.Empty;
        public string? ClauseCode { get; set; }
        public string? StandardCode { get; set; }
        public string? ClauseNumber { get; set; }

        /// <summary>准备分支结论：found / partial / missing / unknown / na（onsite 分支恒为 null）</summary>
        public string? PrepState { get; set; }

        /// <summary>检查分支结论：pass / ofi / nc / na（prep 分支恒为 null）</summary>
        public string? Conclusion { get; set; }

        public bool? NcSuggested { get; set; }
        public string? Severity { get; set; }
        public string? Reason { get; set; }

        /// <summary>是否锁定 —— 来自 cert_check_lock（检查项级），不是结果行字段</summary>
        public bool IsLocked { get; set; }

        public string? LockedBy { get; set; }
        public DateTime? LockedAt { get; set; }
        public bool? IsMandatory { get; set; }
        public string? VerifyLevel { get; set; }
    }

    /// <summary>任务项列表响应</summary>
    public class CertTaskItemsResponse
    {
        public List<CertTaskItemRow> Rows { get; set; } = new();
        public List<CertCheckResultRow> Results { get; set; } = new();
        public string Message { get; set; } = string.Empty;
    }

    // ══════════════════════════════════════════════════════════════════════
    // V2 认证准备/检查统一视图 —— 计划维度聚合
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>计划检查项主表行（统一视图 —— 一行 = plan_detail × dept/evidence/result 聚合）</summary>
    public class CertPlanCheckItemRow
    {
        public string DetailCode { get; set; } = string.Empty;
        public string? ClauseNumbers { get; set; }
        public string? DeptName { get; set; }
        public string? DeptCode { get; set; }
        public string? ContactsJson { get; set; }
        public string? DeptNames { get; set; }
        public string? TasksJson { get; set; }
        public string? PrepState { get; set; }
        /// <summary>审核员判定的最终结论（pass / ofi / nc / na；未判定为 null）</summary>
        public string? Conclusion { get; set; }
        /// <summary>系统建议结论（AI 产出，仅供参考，⛔ 不是最终结论）</summary>
        public string? AiConclusion { get; set; }
        /// <summary>系统建议不符合程度（AI 产出，仅供参考）</summary>
        public string? AiSeverity { get; set; }
        /// <summary>不符合程度（审核员判定）</summary>
        public string? Severity { get; set; }
        /// <summary>是否已由审核员判定（ConclusionBy 非空即已判定）</summary>
        public bool ConclusionConfirmed { get; set; }
        public bool? NcSuggested { get; set; }
        /// <summary>负责审核员（审核人信息）</summary>
        public string? OwnerCode { get; set; }
        public string? OwnerName { get; set; }
        /// <summary>结论是否已锁定（列表行用于黄底标记与锁徽章）—— 来自 cert_check_lock（检查项级）</summary>
        public bool IsLocked { get; set; }
        public string? LockedBy { get; set; }
        public DateTime? LockedAt { get; set; }
        public int RawEvidenceCount { get; set; }
        public int CheckEvidenceCount { get; set; }
        public int DoubtCount { get; set; }
        public int SupplementCount { get; set; }
        public int Sort { get; set; }
        /// <summary>自动分析状态：none（未分析）/ done（已完成）/ failed（失败）</summary>
        public string AnalysisState { get; set; } = "none";
        /// <summary>是否建议重新分析：none / suggested（证据变化后由系统标记）</summary>
        public string RerunState { get; set; } = "none";
        public string? TaskItemCode { get; set; }
        public DateTime? LastAnalyzedAt { get; set; }
    }

    /// <summary>计划检查项侧面详情</summary>
    public class CertPlanCheckItemDetail
    {
        public string DetailCode { get; set; } = string.Empty;
        /// <summary>对应 cert_check_result.Code，前端保存/锁定/解锁的唯一标识；无结论时为 null</summary>
        public string? ResultCode { get; set; }
        /// <summary>所属分析（cert_check_analysis.Code）—— 证据链/疑点按它查</summary>
        public string? AnalysisCode { get; set; }
        /// <summary>ResultCode 所属分支：prep / onsite</summary>
        public string? StageKind { get; set; }
        public string? ClauseNumbers { get; set; }
        public string? DeptNames { get; set; }
        public string? DeptContactsJson { get; set; }
        /// <summary>准备分支：found / partial / missing / unknown / na</summary>
        public string? PrepState { get; set; }
        /// <summary>检查分支：审核员判定的最终结论 pass / ofi / nc / na（未判定为 null）</summary>
        public string? Conclusion { get; set; }
        /// <summary>系统建议结论 / 建议程度（AI 产出，仅供参考）</summary>
        public string? AiConclusion { get; set; }
        public string? AiSeverity { get; set; }
        public string? Severity { get; set; }
        public string? Reason { get; set; }
        /// <summary>判定人（Code / 姓名 / 时间）</summary>
        public string? ConclusionBy { get; set; }
        public string? ConclusionByName { get; set; }
        public DateTime? ConclusionAt { get; set; }
        /// <summary>是否已由审核员判定</summary>
        public bool ConclusionConfirmed { get; set; }
        /// <summary>负责审核员（审核人信息）</summary>
        public string? OwnerCode { get; set; }
        public string? OwnerName { get; set; }
        public bool? NcSuggested { get; set; }
        public bool IsLocked { get; set; }
        public string? LockedBy { get; set; }
        public DateTime? LockedAt { get; set; }
        public List<CertEvidenceRow> RawEvidences { get; set; } = new();
        public List<CertEvidenceRow> CheckEvidences { get; set; } = new();
        public string? DoubtsJson { get; set; }
        /// <summary>自动分析状态：none（未分析）/ done（已完成）/ failed（失败）</summary>
        public string AnalysisState { get; set; } = "none";
        /// <summary>是否建议重新分析：none / suggested</summary>
        public string RerunState { get; set; } = "none";
        public string? TaskItemCode { get; set; }
        public int AnalyzeCount { get; set; }
        public DateTime? LastAnalyzedAt { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>计划统一视图聚合响应</summary>
    public class CertPlanChecklistResponse
    {
        public string PlanCode { get; set; } = string.Empty;
        public string? PlanName { get; set; }

        /// <summary>计划状态：active 进行中 / closed 已关闭（关闭后准备/检查只读）</summary>
        public string? PlanStatus { get; set; }

        /// <summary>计划已关闭 —— 前端据此把编辑入口全部置灰，只保留查看</summary>
        public bool PlanClosed { get; set; }
        public string? EnterpriseName { get; set; }
        public string? StageName { get; set; }
        public string? TasksJson { get; set; }
        public List<CertPlanCheckItemRow> Rows { get; set; } = new();

        /// <summary>
        /// 该计划当前是否有任务在跑队列（★ 页面锁开关 · 46 号 §5.5 / F12）。
        /// <para>前端据此把「自动分析 / 保存结论 / 锁定 / 增删证据」全部置灰 ——
        /// 队列正在写库时人再写，两边会互相覆盖。</para>
        /// </summary>
        public bool TaskRunning { get; set; }

        public string? RunningTaskCode { get; set; }
        public string? RunningTaskNo { get; set; }

        /// <summary>prep / onsite —— 前端据此只锁对应分支</summary>
        public string? RunningTaskKind { get; set; }

        public int RunningItemTotal { get; set; }
        public int RunningItemDone { get; set; }
        public int RunningItemSkipped { get; set; }

        public string Message { get; set; } = string.Empty;
    }

    // ══════════════════════════════════════════════════════════════════════
    // 跨计划检查项清单（认证准备 / 认证检查 两个独立菜单的列表页数据源）
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 跨计划检查项清单请求。
    /// <para>★ 设计依据（用户 2026-11-01）：认证准备和认证检查要拆成<b>两个独立菜单</b>，
    /// 各自一个列表页。所以清单接口<b>不挂计划</b>——按 <see cref="StageKind"/> 分分支，
    /// 把该分支下所有计划的检查项平铺成一张表。</para>
    /// </summary>
    public class CertWorklistRequest
    {
        /// <summary>分支：prep（认证准备）/ onsite（认证检查）—— 决定状态列用哪套词表</summary>
        public string StageKind { get; set; } = "prep";

        /// <summary>关键词：企业名 / 计划名 / 条款号 / 部门名 模糊匹配</summary>
        public string? Keyword { get; set; }

        /// <summary>按计划过滤（可选）</summary>
        public string? PlanCode { get; set; }

        /// <summary>按企业过滤（可选）</summary>
        public string? EnterpriseCode { get; set; }

        /// <summary>按审核员过滤（可选）—— 「我的检查项」</summary>
        public string? OwnerCode { get; set; }

        /// <summary>
        /// 状态过滤（可选）：all / waiting（未分析）/ analyzed（已分析）/ locked（已锁定）/
        /// nc（有 NC）/ doubt（有疑点）
        /// </summary>
        public string? Status { get; set; }

        public int PageIndex { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    /// <summary>清单行（一行 = 一个检查项 = 一条 cert_audit_plan_detail）</summary>
    public class CertWorklistRow
    {
        // ── 计划上下文 ──
        public string PlanCode { get; set; } = string.Empty;
        public string? PlanName { get; set; }
        public string? EnterpriseCode { get; set; }
        public string? EnterpriseName { get; set; }
        public string? StageName { get; set; }

        // ── 检查项本体 ──
        public string DetailCode { get; set; } = string.Empty;
        public string? ClauseNumbers { get; set; }
        /// <summary>条款标题（按条款号拼接，如「5.5.1 职责与权限」）—— 列表里比裸编号可读</summary>
        public string? ClauseTitles { get; set; }
        public string? DeptNames { get; set; }
        public string? DeptCode { get; set; }
        public string? DeptName { get; set; }
        public string? ContactsJson { get; set; }

        /// <summary>负责审核员（准备阶段可改，检查阶段只读）</summary>
        public string? OwnerCode { get; set; }
        public string? OwnerName { get; set; }
        /// <summary>
        /// 该检查项是否已经进入认证检查阶段（存在 onsite 结论行）。
        /// <para>★ 用于前端提前禁用「设置审核员」：审核员分工由审核组长在准备期定死，
        /// 一旦进场执行就不能再改（后端同样拦截）。让用户点了才报错体验很差。</para>
        /// </summary>
        public bool OnsiteStarted { get; set; }

        // ── 结论（按分支取不同字段）──
        public string StageKind { get; set; } = string.Empty;
        /// <summary>准备分支：found / partial / missing / unknown / na</summary>
        public string? PrepState { get; set; }
        /// <summary>检查分支：审核员判定的最终结论 pass / ofi / nc / na（未判定为 null）</summary>
        public string? Conclusion { get; set; }
        /// <summary>系统建议结论 / 建议程度（AI 产出，仅供参考）</summary>
        public string? AiConclusion { get; set; }
        public string? AiSeverity { get; set; }
        public string? Severity { get; set; }
        /// <summary>是否已由审核员判定（ConclusionBy 非空）</summary>
        public bool ConclusionConfirmed { get; set; }
        public bool? NcSuggested { get; set; }
        /// <summary>判定依据（截断到 200 字，详情页看全文）</summary>
        public string? ReasonBrief { get; set; }

        /// <summary>结论行 Code —— 前端保存/锁定/解锁/重跑的唯一标识；未分析时为 null</summary>
        public string? ResultCode { get; set; }
        public string? AnalysisCode { get; set; }

        /// <summary>
        /// 任务项 Code（<c>cert_task_item.Code</c>）—— <c>analyze-item</c> 端点的入参。
        /// <para>为 null 说明该分支还没建过任务，前端要提示「先启动一次执行」。</para>
        /// </summary>
        public string? TaskItemCode { get; set; }

        /// <summary>综合状态：waiting / analyzed / locked（前端状态列直接用它上色）</summary>
        public string State { get; set; } = "waiting";

        // ── 锁 ──
        public bool IsLocked { get; set; }
        public string? LockedBy { get; set; }
        public DateTime? LockedAt { get; set; }

        // ── 计数 ──
        public int RawEvidenceCount { get; set; }
        public int CheckEvidenceCount { get; set; }
        public int DoubtCount { get; set; }
        public int AnalyzeCount { get; set; }
        public DateTime? LastAnalyzedAt { get; set; }

        /// <summary>自动分析状态：none（未分析）/ done（已完成）/ failed（失败）</summary>
        public string AnalysisState { get; set; } = "none";
        /// <summary>是否建议重新分析：none / suggested（证据变化后由系统标记）</summary>
        public string RerunState { get; set; } = "none";

        public int Sort { get; set; }
    }

    /// <summary>清单汇总（★ 统计的是<b>过滤后全量</b>，不受分页影响）</summary>
    public class CertWorklistSummary
    {
        public int ItemTotal { get; set; }
        public int Waiting { get; set; }
        public int Analyzed { get; set; }
        public int Locked { get; set; }
        /// <summary>自动分析失败</summary>
        public int Failed { get; set; }
        /// <summary>已完成分析但证据发生变化，建议重新分析</summary>
        public int RerunSuggested { get; set; }
        public int NcCount { get; set; }
        /// <summary>准备分支：资料缺失/部分缺失的检查项数（判断该不该进场的关键指标）</summary>
        public int MissingCount { get; set; }
        public int DoubtCount { get; set; }
        /// <summary>当前分支下正在跑队列的计划数（页面锁）</summary>
        public int RunningPlans { get; set; }
    }

    /// <summary>清单响应</summary>
    public class CertWorklistResponse
    {
        public List<CertWorklistRow> Rows { get; set; } = new();
        public int Total { get; set; }
        public int PageIndex { get; set; }
        public int PageSize { get; set; }
        public CertWorklistSummary Summary { get; set; } = new();
        /// <summary>计划下拉选项（企业名·阶段·计划名）</summary>
        public List<CertWorklistPlanOption> Plans { get; set; } = new();
        /// <summary>正在跑队列的计划 Code → 任务号（前端置灰用）</summary>
        public Dictionary<string, string> RunningPlanTasks { get; set; } = new();
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>清单页顶部「按计划筛选」的下拉项</summary>
    public class CertWorklistPlanOption
    {
        public string PlanCode { get; set; } = string.Empty;
        public string? PlanName { get; set; }
        public string? EnterpriseName { get; set; }
        public string? StageName { get; set; }
        public int ItemCount { get; set; }
    }

    // ══════════════════════════════════════════════════════════════════════
    // 分支实例列表（认证准备 / 认证检查 两个菜单的「列表页」）
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 分支实例列表请求。
    /// <para>一行 = 一个「计划 × 分支」的执行实例 —— 这正是原型列表页的口径：
    /// 企业 / 计划 / 阶段 / 标准 / 检查项数 / 进度 / 状态 / 开启时间 / 操作。</para>
    /// </summary>
    public class CertStageListRequest
    {
        /// <summary>分支：prep / onsite</summary>
        public string StageKind { get; set; } = "prep";

        /// <summary>关键词：企业名 / 计划名</summary>
        public string? Keyword { get; set; }

        /// <summary>状态：all / waiting / running / paused / done / partial</summary>
        public string? Status { get; set; }

        public int PageIndex { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    /// <summary>分支实例行</summary>
    public class CertStageInstanceRow
    {
        // ── 计划上下文 ──
        public string PlanCode { get; set; } = string.Empty;
        public string? PlanName { get; set; }
        public string? EnterpriseCode { get; set; }
        public string? EnterpriseName { get; set; }
        public string? StageName { get; set; }
        /// <summary>认证标准名（逗号分隔，如「ISO 9001:2015,ISO 14001:2015」）</summary>
        public string? Standards { get; set; }
        public string StageKind { get; set; } = string.Empty;

        // ── 进度 ──
        public int ItemTotal { get; set; }
        /// <summary>已完成分析的检查项数（有当前分支结论行）</summary>
        public int ItemDone { get; set; }
        public int ItemLocked { get; set; }
        /// <summary>已签发 NC 数（仅检查分支）</summary>
        public int ItemNc { get; set; }
        /// <summary>资料缺失/部分缺失（仅准备分支）</summary>
        public int ItemMissing { get; set; }
        public int ItemDoubt { get; set; }

        // ── 状态 ──
        /// <summary>waiting / running / paused / done / partial</summary>
        public string State { get; set; } = "waiting";
        public string? TaskCode { get; set; }
        public string? TaskNo { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? FinishedAt { get; set; }
        /// <summary>第几轮执行（该分支下历史任务数）</summary>
        public int RunRound { get; set; }

        /// <summary>负责审核员姓名（去重拼接，准备期可定义）</summary>
        public string? OwnerNames { get; set; }

        /// <summary>队列正在跑 ⇒ 页面锁（前端整行只读）</summary>
        public bool TaskRunning { get; set; }
    }

    /// <summary>分支实例汇总（顶部四格统计）</summary>
    public class CertStageListSummary
    {
        public int InstanceTotal { get; set; }
        public int Running { get; set; }
        public int Done { get; set; }
        public int Paused { get; set; }
        public int ItemTotal { get; set; }
        public int NcTotal { get; set; }
        public int LockedTotal { get; set; }
    }

    /// <summary>分支实例列表响应</summary>
    public class CertStageListResponse
    {
        public List<CertStageInstanceRow> Rows { get; set; } = new();
        public int Total { get; set; }
        public int PageIndex { get; set; }
        public int PageSize { get; set; }
        public CertStageListSummary Summary { get; set; } = new();
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>专家组成员选项（设置检查项负责审核员的下拉数据源）</summary>
    public class CertMemberOption
    {
        /// <summary>成员 Code —— 落到 <c>cert_audit_plan_detail.OwnerCode</c></summary>
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        /// <summary>职称（组长 / 组员 / 技术专家…）</summary>
        public string? Title { get; set; }
        /// <summary>专业</summary>
        public string? Profession { get; set; }
    }

    // ══════════════════════════════════════════════════════════════════════
    // 核心服务
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 认证任务服务（V2 — 任务生命周期 + 证据 MD5 变更检测 + 影响分析）
    /// <para>职责：</para>
    /// <list type="bullet">
    ///   <item>任务创建：从计划明细生成 cert_task + cert_task_item</item>
    ///   <item>任务启动：状态流转 + 进度初始化</item>
    ///   <item>证据变更检测：MD5 比对 → 影响分析日志 → 任务项 RerunState 更新</item>
    /// </list>
    /// </summary>
    public class CertTaskService
    {
        private readonly IDbOrm _db;
        private readonly CertCheckLockService _lock;
        private readonly YZH.Core.DataBase.Services.QueueManager _queueMgr;
        private readonly ILogger<CertTaskService> _logger;

        /// <summary>
        /// 核心资源（企业 / 标准 / 条款 / 阶段）的<b>唯一读入口</b>。
        /// <para>⛔ 不要为了「就查个企业名」直接 <c>_db.GetListAsync&lt;Enterprise&gt;</c> ——
        /// 那违反铁律 L5（端内只允许投影，核心资源取数收口在 <c>ICoreResourceQuery</c>），
        /// 守卫 A-R6 会拦，且 `npm run build` 过不去。</para>
        /// </summary>
        private readonly CertPlatform.Shared.Domain.Scope.ICoreResourceQuery _core;
        private readonly CertPlatform.Shared.Services.Sys.UserDisplayNameResolver _userNames;

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public CertTaskService(
            IDbOrm db,
            CertCheckLockService lockService,
            YZH.Core.DataBase.Services.QueueManager queueManager,
            CertPlatform.Shared.Domain.Scope.ICoreResourceQuery core,
            CertPlatform.Shared.Services.Sys.UserDisplayNameResolver userNames,
            ILogger<CertTaskService> logger)
        {
            _db = db;
            _lock = lockService;
            _queueMgr = queueManager;
            _core = core;
            _userNames = userNames;
            _logger = logger;
        }

        // ================================================================
        // 一、任务创建
        // ================================================================

        /// <summary>
        /// 从计划创建认证任务（cert_task + cert_task_item 批量生成）。
        /// <para>检查项来源 = 该计划下 IsValid=1 / IsDeleted=0 的 cert_audit_plan_detail 行。</para>
        /// </summary>
        public async Task<Result<CertTaskCreateResponse>> CreateTaskAsync(
            string userCode, CertTaskCreateRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.PlanCode))
                return Result<CertTaskCreateResponse>.Fail("计划 Code 不能为空");

            // 取计划头
            var planResult = await _db.GetOneAsync<CertAuditPlan>(p =>
                p.Code == req.PlanCode && !p.IsDeleted);
            if (planResult.Data == null)
                return Result<CertTaskCreateResponse>.Fail("计划不存在或已删除");

            var plan = planResult.Data;

            // 已关闭的计划不能再发起新的认证准备 / 认证检查任务
            if (plan.Status == "closed")
                return Result<CertTaskCreateResponse>.Fail("认证计划已关闭，不能继续执行认证准备/认证检查");

            // 取计划明细（检查项）
            var detailsResult = await _db.GetListAsync<CertAuditPlanDetail>(d =>
                d.PlanCode == req.PlanCode && d.IsValid == 1 && !d.IsDeleted);
            var details = detailsResult.Data ?? new List<CertAuditPlanDetail>();

            if (details.Count == 0)
                return Result<CertTaskCreateResponse>.Fail("计划下无有效检查项");

            // 生成任务
            var taskCode = Guid.NewGuid().ToString("N");
            var prefix = req.TaskKind == "prep" ? "P" : "C";
            var taskNo = $"{prefix}-{DateTime.Now:yyyyMMdd}-{taskCode[..6]}";

            var task = new CertTask
            {
                Code = taskCode,
                TaskNo = taskNo,
                TaskKind = req.TaskKind,
                PlanCode = req.PlanCode,
                EnterpriseCode = plan.EnterpriseCode,
                StageCode = plan.StageCode,
                AuditType = req.TaskKind == "onsite" ? "initial" : null,
                Status = "pending",
                ItemTotal = details.Count,
                ItemDone = 0,
                ItemFailed = 0,
                PromptCode = req.PromptCode,
                Sort = 0,
                CreateBy = userCode,
                CreateTime = DateTime.Now
            };

            // 生成任务项
            var items = details.Select((d, idx) => new CertTaskItem
            {
                Code = Guid.NewGuid().ToString("N"),
                TaskCode = taskCode,
                CheckItemCode = d.Code,
                Status = "waiting",
                EvidenceState = "none",
                AnalysisState = "none",
                RunMode = req.RunMode,
                SkipLocked = true,
                RerunState = "none",
                Sort = idx,
                CreateBy = userCode,
                CreateTime = DateTime.Now
            }).ToList();

            using var tran = _db.BeginTransaction();
            try
            {
                await _db.InsertAsync(task);
                foreach (var item in items)
                    await _db.InsertAsync(item);

                tran.Commit();
            }
            catch (Exception ex)
            {
                tran.Rollback();
                _logger.LogError(ex, "创建任务失败 PlanCode={PlanCode}", req.PlanCode);
                return Result<CertTaskCreateResponse>.Fail($"创建任务失败：{ex.Message}");
            }

            return Result<CertTaskCreateResponse>.Ok(new CertTaskCreateResponse
            {
                TaskCode = taskCode,
                TaskNo = taskNo,
                ItemTotal = details.Count,
                Message = $"任务创建成功，共 {details.Count} 个检查项"
            });
        }

        // ================================================================
        // 二、任务启动 / 暂停
        // ================================================================

        /// <summary>
        /// 启动任务：pending/paused → running，并把未锁定的检查项投进统一队列。
        ///
        /// <para><b>★ 2026-11-01 重写（46 号 F1）</b>。旧实现只改 <c>Status='running'</c> ——
        /// 既不遍历 <c>cert_task_item</c> 也不投队列，点「开始」等于什么都没发生。</para>
        ///
        /// <para><b>现在做五件事（顺序不能变）</b>：</para>
        /// <list type="number">
        ///   <item><b>闸门① 筛锁</b>：<see cref="CertCheckLockService.GetLockedCodesAsync"/> 一次查询，
        ///         锁定的项仍建/留 <c>cert_task_item</c> 但置 <c>skipped</c>，⛔ <b>不投队列</b>
        ///         （防白占并发位、白打 LLM 配额）；</item>
        ///   <item>把未锁定的项重置为 <c>waiting</c>（重跑场景清掉上一轮终态）；</item>
        ///   <item>投 <c>yzh_queue</c>（一个任务 = 一条队列，一个检查项 = 一条队列项，同队列串行）；</item>
        ///   <item><b>全锁不空转</b>：一项都没得跑 ⇒ 不建队列，直接 <c>done</c> + 明确文案（§10.3）；</item>
        ///   <item>回写 <c>QueueCode</c> / <c>QueueAttempt</c> / 进度初值。</item>
        /// </list>
        /// </summary>
        public async Task<Result<CertTaskStartResponse>> StartTaskAsync(string userCode, string taskCode)
        {
            var taskResult = await _db.GetOneAsync<CertTask>(t =>
                t.Code == taskCode && !t.IsDeleted);
            if (taskResult.Data == null)
                return Result<CertTaskStartResponse>.Fail("任务不存在");

            var task = taskResult.Data;
            if (task.Status != "pending" && task.Status != "paused")
                return Result<CertTaskStartResponse>.Fail($"当前状态 {task.Status} 不允许启动");

            // 取任务项（检查项）
            var items = (await _db.GetListAsync<CertTaskItem>(i =>
                i.TaskCode == taskCode && !i.IsDeleted)).Data ?? new List<CertTaskItem>();
            if (items.Count == 0)
                return Result<CertTaskStartResponse>.Fail("任务下没有检查项，无法启动");

            var stageKind = string.IsNullOrWhiteSpace(task.TaskKind)
                ? CertCheckEngineService.StageOnsite
                : task.TaskKind;

            // ── ① 闸门①：批量筛锁（⛔ 不循环调 IsLockedAsync，N 项 = N 次往返）──
            var lockedCodes = await _lock.GetLockedCodesAsync(
                items.Select(i => i.CheckItemCode), stageKind);

            var now = DateTime.Now;
            var toEnqueue = new List<CertTaskItem>();

            foreach (var item in items)
            {
                var isLocked = lockedCodes.Contains(item.CheckItemCode);

                // 锁定的项：留痕 skipped（不是删行 —— 要能回答"为什么这项没跑"）
                item.Status = isLocked ? "skipped" : "waiting";
                item.AnalysisState = "none";
                item.RerunState = "none";
                item.Message = isLocked ? "已锁定（入队前被锁），跳过" : null;
                item.UpdateBy = userCode;
                item.UpdateTime = now;
                await _db.UpdateAsync(item);

                if (!isLocked) toEnqueue.Add(item);
            }

            // ── ② 进度初值 ──
            task.ItemTotal = items.Count;
            task.ItemDone = 0;
            task.ItemFailed = 0;
            task.ItemSkipped = lockedCodes.Count;
            task.QueueAttempt += 1;
            task.StartedAt ??= now;
            task.FinishedAt = null;

            // ── ③ 全锁：不建队列、不空转（§10.3）──
            //    ⛔ 不报错（勾到已锁定的项是正常误操作），但⛔ 也不静默（必须说清为什么没跑）
            if (toEnqueue.Count == 0)
            {
                task.Status = "done";
                task.QueueCode = null;
                task.FinishedAt = now;
                task.Message = $"选中的 {items.Count} 个检查项均已锁定，本次未执行任何分析";
                task.UpdateBy = userCode;
                task.UpdateTime = now;
                await _db.UpdateAsync(task);

                _logger.LogInformation(
                    "[CertTask] 全锁跳过 task={Task} items={Count}", taskCode, items.Count);

                return Result<CertTaskStartResponse>.Ok(new CertTaskStartResponse
                {
                    Success = true,
                    Message = task.Message
                });
            }

            // ── ④ 投统一队列（yzh_queue）──
            var isPrep = stageKind == CertCheckEngineService.StagePrep;
            var seq = 1;

            // ★ SourceId 必须带重跑轮次：yzh_queue 上有 uk_source(SourceType, SourceId) 唯一键，
            //   框架契约是「一个来源 = 一条队列」（⛔ 没有 AddTasksAsync 可以往旧队列追加）。
            //   而 cert_task 是可重跑的 —— 若恒用 taskCode，第二次点「开始」必撞唯一约束
            //   （实测报错：投递队列失败：队列主表插入失败：数据已存在）。
            //   反向查找不靠 SourceId，靠 cert_task.QueueCode（每轮覆盖成本轮的）。
            var sourceId = task.QueueAttempt <= 1
                ? taskCode
                : $"{taskCode}#{task.QueueAttempt}";

            var queueTasks = toEnqueue.Select(i => new YZH.Core.DataBase.Services.QueueManager.TaskItem
            {
                TaskType = isPrep ? CertCheckItemTaskTypes.Prep : CertCheckItemTaskTypes.Onsite,
                Payload = JsonSerializer.Serialize(new CertCheckItemPayload
                {
                    TaskCode = taskCode,
                    TaskItemCode = i.Code,
                    CheckItemCode = i.CheckItemCode,
                    StageKind = stageKind,
                    PlanCode = task.PlanCode,
                    EnterpriseCode = task.EnterpriseCode,
                    UserCode = userCode
                }),
                TaskId = sourceId,
                ItemCode = i.CheckItemCode,
                Seq = seq++
            }).ToList();

            var (ok, error, queueCode, count) = await _queueMgr.CreateQueueAsync(
                new YZH.Core.DataBase.Services.QueueManager.CreateQueueRequest
                {
                    // QueueType 只用于监控页分组展示，与 TaskType（执行器分派键）是两回事
                    QueueType = isPrep ? "cert_prep" : "cert_onsite",
                    QueueName = $"认证{(isPrep ? "准备" : "检查")}-{task.TaskNo}",
                    // ScopeKey = 业务幂等键：一个任务一条队列（重跑换 QueueCode，ScopeKey 不变）
                    ScopeKey = $"cert_check|{taskCode}",
                    SourceType = "cert_task",
                    SourceId = sourceId,
                    UserCode = userCode,
                    UserName = "",
                    OrgCode = "",
                    // 不占资源锁：认证分析锁的是检查项（cert_check_lock），不是文件/目录
                    ResourceLocks = new List<YZH.Core.DataBase.Services.QueueManager.ResourceLockItem>(),
                    Tasks = queueTasks
                });

            if (!ok)
            {
                // 投队列失败 ⇒ 任务必须退回可重试态，⛔ 不能留 running（否则页面永久锁死）
                task.Status = "pending";
                task.Message = $"投递队列失败：{error}";
                task.UpdateBy = userCode;
                task.UpdateTime = now;
                await _db.UpdateAsync(task);

                _logger.LogError("[CertTask] 投递队列失败 task={Task} err={Err}", taskCode, error);
                return Result<CertTaskStartResponse>.Fail($"投递队列失败：{error}");
            }

            // ── ⑤ 回写队列指针 + 进入 running ──
            task.Status = "running";
            task.QueueCode = queueCode;
            task.Message = lockedCodes.Count > 0
                ? $"已入队 {count} 项，跳过 {lockedCodes.Count} 项（已锁定）"
                : $"已入队 {count} 项";
            task.UpdateBy = userCode;
            task.UpdateTime = now;
            await _db.UpdateAsync(task);

            _logger.LogInformation(
                "[CertTask] 已启动 task={Task} queue={Queue} enqueued={Count} locked={Locked}",
                taskCode, queueCode, count, lockedCodes.Count);

            return Result<CertTaskStartResponse>.Ok(new CertTaskStartResponse
            {
                Success = true,
                Message = task.Message
            });
        }

        /// <summary>暂停任务：running → paused，并取消队列里还没跑的项</summary>
        public async Task<Result<CertTaskStartResponse>> PauseTaskAsync(string userCode, string taskCode)
        {
            var taskResult = await _db.GetOneAsync<CertTask>(t =>
                t.Code == taskCode && !t.IsDeleted);
            if (taskResult.Data == null)
                return Result<CertTaskStartResponse>.Fail("任务不存在");

            var task = taskResult.Data;
            if (task.Status != "running")
                return Result<CertTaskStartResponse>.Fail($"当前状态 {task.Status} 不允许暂停");

            // ★ 先取消队列再改状态：队列里 pending 的项会被 QueueManager 置 cancelled 并跳过执行。
            //   顺序反了会有一瞬间"任务已暂停但队列还在跑"（那一项的结论会写到暂停之后）。
            if (!string.IsNullOrWhiteSpace(task.QueueCode))
            {
                var (ok, err) = await _queueMgr.CancelQueueAsync(task.QueueCode!);
                if (!ok)
                    _logger.LogWarning("[CertTask] 取消队列失败 task={Task} queue={Queue} err={Err}",
                        taskCode, task.QueueCode, err);
            }

            task.Status = "paused";
            task.UpdateBy = userCode;
            task.UpdateTime = DateTime.Now;

            await _db.UpdateAsync(task);

            return Result<CertTaskStartResponse>.Ok(new CertTaskStartResponse
            {
                Success = true,
                Message = "任务已暂停"
            });
        }

        // ================================================================
        // 二·五、页面锁（F12 —— 队列执行期间人不能动）
        // ================================================================

        /// <summary>
        /// 页面锁：该检查项所属分支有任务正在跑队列 ⇒ 禁止编辑。
        ///
        /// <para><b>与检查项锁的分工</b>（46 号 §5.5，两把锁方向相反）：</para>
        /// <list type="bullet">
        ///   <item><b>页面锁</b>（本方法）管「人别动」：队列写库时人不许写，<b>队列跑完自动解</b>；</item>
        ///   <item><b>检查项锁</b>（<see cref="CertCheckLockService"/>）管「机器别动」：人确认过，队列不许覆盖，<b>手动解</b>。</item>
        /// </list>
        ///
        /// <para>⚠️ 判定口径是「<b>同计划</b>有 running 任务且该任务含这个检查项」——
        /// 不是「整个计划有 running 任务」：两个分支（准备/检查）是分开执行的，
        /// 准备在跑不该把检查页也锁死。</para>
        /// </summary>
        /// <returns>可编辑 ⇒ <c>Ok</c>；锁定 ⇒ <c>Fail(409)</c></returns>
        public async Task<Result> AssertPageEditableAsync(string checkItemCode)
        {
            if (string.IsNullOrWhiteSpace(checkItemCode)) return Result.Ok();

            var detail = (await _db.GetOneAsync<CertAuditPlanDetail>(d =>
                d.Code == checkItemCode && !d.IsDeleted)).Data;
            if (detail == null) return Result.Ok();

            // 计划已关闭 ⇒ 认证准备 / 认证检查一并作废，只允许查看
            var plan = (await _db.GetOneAsync<CertAuditPlan>(p =>
                p.Code == detail.PlanCode && !p.IsDeleted)).Data;
            if (plan != null && plan.Status == "closed")
                return Result.Fail("认证计划已关闭：对应的认证准备与认证检查已作废，只能查看", 409);

            var runningTasks = (await _db.GetListAsync<CertTask>(t =>
                t.PlanCode == detail.PlanCode
                && t.Status == "running"
                && !t.IsDeleted)).Data ?? new List<CertTask>();
            if (runningTasks.Count == 0) return Result.Ok();

            var runningCodes = runningTasks.Select(t => t.Code).ToList();
            var hit = (await _db.ExistsAsync<CertTaskItem>(i =>
                runningCodes.Contains(i.TaskCode)
                && i.CheckItemCode == checkItemCode
                && !i.IsDeleted)).Data;

            if (!hit) return Result.Ok();

            var t0 = runningTasks.First();
            var kindLabel = t0.TaskKind == CertCheckEngineService.StagePrep ? "认证准备" : "认证检查";
            return Result.Fail(
                $"页面锁定中：{kindLabel}任务 {t0.TaskNo} 正在执行队列，请等队列完成后再编辑", 409);
        }

        /// <summary>页面锁（按任务项入口，供 <c>analyze-item</c> 这类只拿到 TaskItemCode 的端点用）</summary>
        public async Task<Result> AssertPageEditableByTaskItemAsync(string taskItemCode)
        {
            if (string.IsNullOrWhiteSpace(taskItemCode)) return Result.Ok();

            var item = (await _db.GetOneAsync<CertTaskItem>(i =>
                i.Code == taskItemCode && !i.IsDeleted)).Data;
            if (item == null) return Result.Ok();

            return await AssertPageEditableAsync(item.CheckItemCode);
        }

        /// <summary>页面锁（按结果行入口，供 <c>save-result</c> / <c>lock-result</c> 这类只拿到 ResultCode 的端点用）</summary>
        public async Task<Result> AssertPageEditableByResultAsync(string resultCode)
        {
            if (string.IsNullOrWhiteSpace(resultCode)) return Result.Ok();

            var r = (await _db.GetOneAsync<CertCheckResult>(x =>
                x.Code == resultCode && !x.IsDeleted)).Data;
            if (r == null) return Result.Ok();

            return await AssertPageEditableAsync(r.CheckItemCode);
        }

        /// <summary>页面锁（按证据入口，供 <c>CertEvidence/update</c> 这类只拿到证据 Code 的端点用）</summary>
        public async Task<Result> AssertPageEditableByEvidenceAsync(string evidenceCode)
        {
            if (string.IsNullOrWhiteSpace(evidenceCode)) return Result.Ok();

            var e = (await _db.GetOneAsync<CertEvidence>(x =>
                x.Code == evidenceCode && !x.IsDeleted)).Data;
            if (e == null) return Result.Ok();

            return await AssertPageEditableAsync(e.CheckItemCode);
        }

        /// <summary>按任务项反查所属任务 Code（手动单项重跑后刷新汇总要用）；查不到返回 null</summary>
        public async Task<string?> FindTaskCodeByItemAsync(string taskItemCode)
        {
            if (string.IsNullOrWhiteSpace(taskItemCode)) return null;

            var item = (await _db.GetOneAsync<CertTaskItem>(i =>
                i.Code == taskItemCode && !i.IsDeleted)).Data;
            return item?.TaskCode;
        }

        // ================================================================
        // 三、结论锁定
        // ================================================================

        /// <summary>
        /// 锁定检查项（审核员确认后调用，防止任何队列执行覆盖）。
        ///
        /// <para><b>★ 2026-11-01 语义变更（46 号 §10.1）</b>：锁从「结果行」迁到「检查项」。</para>
        /// <para>入参仍是 <c>resultCode</c>（前端只持有结果行 Code），本方法先反查该结果行所属的
        /// <c>CheckItemCode</c>，再锁<b>检查项</b> —— 因为用户口径是「一个检查项被锁定了」，
        /// 不是「这一行被锁了」。一个检查项 3 条款只锁 1 次，不是锁 3 行。</para>
        /// <para>默认锁粒度 = <c>all</c>（准备和检查两个分支都不跑）。</para>
        /// </summary>
        public async Task<Result> LockConclusionAsync(
            string userCode, string resultCode, string? lockReason = null,
            string stageKind = CertCheckLockService.StageKinds.All)
        {
            var result = await _db.GetOneAsync<CertCheckResult>(r =>
                r.Code == resultCode && !r.IsDeleted);
            if (result.Data == null)
                return Result.Fail("结论记录不存在");

            return await _lock.LockAsync(
                userCode, result.Data.CheckItemCode, stageKind,
                lockReason ?? "manual_confirm", "manual");
        }

        /// <summary>
        /// 解锁检查项（允许重新分析）。<b>锁的唯一出口</b>。
        /// <para>留痕不删行：<c>IsActive=0</c> + <c>UnlockedBy/At</c>。</para>
        /// </summary>
        public async Task<Result> UnlockConclusionAsync(
            string userCode, string resultCode,
            string stageKind = CertCheckLockService.StageKinds.All)
        {
            var result = await _db.GetOneAsync<CertCheckResult>(r =>
                r.Code == resultCode && !r.IsDeleted);
            if (result.Data == null)
                return Result.Fail("结论记录不存在");

            return await _lock.UnlockAsync(userCode, result.Data.CheckItemCode, stageKind);
        }

        /// <summary>
        /// 保存审核员判定（结论 + 不符合程度 + 判定依据）。
        /// <para>★ 2026-11-03：系统只给建议（<c>AiConclusion</c>），最终结论只能由审核员写这里。</para>
        /// <para>已锁定的检查项禁止修改，需先解锁 —— 锁是硬约束，⛔ 不是提示（§10.1）。</para>
        /// </summary>
        public async Task<Result> SaveResultAsync(
            string userCode, string resultCode, string? conclusion, string? reason, string? severity = null)
        {
            var result = await _db.GetOneAsync<CertCheckResult>(r =>
                r.Code == resultCode && !r.IsDeleted);
            if (result.Data == null)
                return Result.Fail("结论记录不存在");

            var r = result.Data;

            // 锁判定：按该结果行所属分支查（StageKind 为空时按 all 兜底判定）
            var stage = string.IsNullOrWhiteSpace(r.StageKind)
                ? CertCheckLockService.StageKinds.Onsite
                : r.StageKind!;
            if (await _lock.IsLockedAsync(r.CheckItemCode, stage))
                return Result.Fail("检查项已锁定，请先解锁再修改");

            // 认证准备分支不产生结论（只改判定依据），检查分支才写「认证结论」
            var isPrepStage = string.Equals(
                stage, CertCheckEngineService.StagePrep, StringComparison.OrdinalIgnoreCase);

            if (!isPrepStage)
            {
                if (string.IsNullOrWhiteSpace(conclusion))
                    return Result.Fail("请选择认证结论（是否符合）");

                // ★ 落库前必须过 CertVerdictMapper（唯一出口）：前端历史上传过
                //   conformant / minor_nc / major_nc 这类 LLM 原始值，直接写进
                //   cert_check_result.Conclusion 会让「按结论筛选」和 NC 统计静默失效。
                var normalized = CertVerdictMapper.ToConclusion(conclusion);
                if (normalized == null)
                    return Result.Fail("结论值不合法：只接受 pass / ofi / nc / na");

                // 不符合必须选程度（一般 / 严重）；非不符合时清空程度
                if (normalized == CertVerdictMapper.ConclusionNc)
                {
                    var sev = CertVerdictMapper.NormalizeSeverity(severity);
                    if (sev == null)
                        return Result.Fail("不符合必须选择程度（一般 / 严重）");
                    r.Severity = sev;
                    r.SeveritySource = "manual";
                }
                else
                {
                    r.Severity = null;
                    r.SeveritySource = null;
                }

                r.Conclusion = normalized;
                r.ConclusionBy = userCode;
                r.ConclusionAt = DateTime.Now;
            }

            r.Reason = reason;
            r.UpdateBy = userCode;
            r.UpdateTime = DateTime.Now;

            await _db.UpdateAsync(r);
            return Result.Ok();
        }

        // ================================================================
        // 四、证据 MD5 变更检测 + 影响分析
        // ================================================================

        /// <summary>
        /// 检测证据内容是否发生变更（MD5 比对）。
        /// <para>调用方在保存证据后传入新的 LlmSummary/Snippet，本方法计算 PrevContentMD5 与当前 ContentMD5 比对。</para>
        /// </summary>
        public async Task<Result> DetectEvidenceChangeAsync(
            string userCode, string evidenceCode, string newContent)
        {
            var eviResult = await _db.GetOneAsync<CertEvidence>(e =>
                e.Code == evidenceCode && !e.IsDeleted);
            if (eviResult.Data == null)
                return Result.Fail("证据不存在");

            var evi = eviResult.Data;
            var newMd5 = ComputeMd5(newContent);

            evi.PrevContentMD5 = evi.ContentMD5;
            evi.ContentMD5 = newMd5;
            evi.UpdateBy = userCode;
            evi.UpdateTime = DateTime.Now;

            await _db.UpdateAsync(evi);

            return Result.Ok();
        }

        /// <summary>
        /// 证据 MD5 变更后触发影响分析：找到所有引用该证据的检查项 → 更新任务项 RerunState → 写 cert_impact_log。
        /// </summary>
        public async Task<Result<ImpactAnalysisResult>> AnalyzeImpactAsync(
            string userCode, string triggerSource, EvidenceChangeResult change)
        {
            var items = (await _db.GetListAsync<CertTaskItem>(ti =>
                ti.CheckItemCode == change.CheckItemCode && !ti.IsDeleted)).Data
                ?? new List<CertTaskItem>();

            var affectedTaskItemCount = 0;
            var skippedLockedCount = 0;
            var affectedItemsJsonArr = new List<object>();

            foreach (var item in items)
            {
                // 检查该检查项是否被锁（★ 锁在 cert_check_lock，检查项级；
                //   两个分支任一被锁都算"锁住了"——all 会同时命中两边）
                var hasLocked =
                    await _lock.IsLockedAsync(change.CheckItemCode, CertCheckLockService.StageKinds.Prep)
                    || await _lock.IsLockedAsync(change.CheckItemCode, CertCheckLockService.StageKinds.Onsite);

                if (hasLocked && item.SkipLocked)
                {
                    skippedLockedCount++;
                    affectedItemsJsonArr.Add(new
                    {
                        CheckItemCode = change.CheckItemCode,
                        TaskItemCode = item.Code,
                        Action = "skipped",
                        LockedState = "locked"
                    });
                    continue;
                }

                // 需要重跑
                item.RerunState = "suggested";
                item.RerunReason = JsonSerializer.Serialize(new[]
                {
                    new
                    {
                        evidence_code = change.EvidenceCode,
                        kind = "md5_change",
                        old_md5 = change.OldMD5,
                        new_md5 = change.NewMD5,
                        snippet = change.HasChanged ? "内容已变更" : "首次挂接"
                    }
                });
                item.RerunTriggeredAt = DateTime.Now;
                item.RerunTriggeredBy = change.OldMD5 == null ? "evidence_change" : "doc_reupload";
                item.UpdateBy = userCode;
                item.UpdateTime = DateTime.Now;

                await _db.UpdateAsync(item);
                affectedTaskItemCount++;

                affectedItemsJsonArr.Add(new
                {
                    CheckItemCode = change.CheckItemCode,
                    TaskItemCode = item.Code,
                    Action = "rerun_suggested",
                    LockedState = "none"
                });
            }

            // 写影响分析日志
            var log = new CertImpactLog
            {
                Code = Guid.NewGuid().ToString("N"),
                TriggerKind = change.OldMD5 == null ? "evidence_change" : "doc_reupload",
                TriggerSource = triggerSource,
                OldMD5 = change.OldMD5,
                NewMD5 = change.NewMD5,
                AffectedCheckItemCount = 1,
                AffectedTaskItemCount = affectedTaskItemCount,
                SkippedLockedCount = skippedLockedCount,
                AffectedItemsJson = JsonSerializer.Serialize(affectedItemsJsonArr),
                CreatedBy = userCode,
                CreatedAt = DateTime.Now
            };

            await _db.InsertAsync(log);

            return Result<ImpactAnalysisResult>.Ok(new ImpactAnalysisResult
            {
                ImpactLogCode = log.Code,
                AffectedCheckItemCount = 1,
                AffectedTaskItemCount = affectedTaskItemCount,
                SkippedLockedCount = skippedLockedCount,
                Message = $"影响分析完成：影响 {affectedTaskItemCount} 个任务项，跳过 {skippedLockedCount} 个已锁定项"
            });
        }

        // ================================================================
        // 五、分页查询 + 任务项列表
        // ================================================================

        /// <summary>分页查询认证任务列表</summary>
        public async Task<Result<CertTaskPageResponse>> GetPageAsync(
            string userCode, CertTaskPageRequest req)
        {
            var query = _db.Client.Queryable<CertTask>()
                .Where(t => !t.IsDeleted);

            if (!string.IsNullOrWhiteSpace(req.Keyword))
                query = query.Where(t => t.TaskNo.Contains(req.Keyword) || t.Code.Contains(req.Keyword));
            if (!string.IsNullOrWhiteSpace(req.Status))
                query = query.Where(t => t.Status == req.Status);
            if (!string.IsNullOrWhiteSpace(req.TaskKind))
                query = query.Where(t => t.TaskKind == req.TaskKind);

            var total = query.Count();

            var items = query
                .OrderBy(t => t.CreateTime, SqlSugar.OrderByType.Desc)
                .Skip((req.PageIndex - 1) * req.PageSize)
                .Take(req.PageSize)
                .ToList();

            var rows = items.Select(t => new CertTaskRow
            {
                Code = t.Code,
                TaskNo = t.TaskNo,
                TaskKind = t.TaskKind,
                Status = t.Status,
                ItemTotal = t.ItemTotal,
                ItemDone = t.ItemDone,
                ItemFailed = t.ItemFailed,
                Progress = t.ItemTotal > 0 ? (int)Math.Round((double)t.ItemDone / t.ItemTotal * 100) : 0,
                CreateBy = t.CreateBy,
                CreateTime = t.CreateTime
            }).ToList();

            return Result<CertTaskPageResponse>.Ok(new CertTaskPageResponse
            {
                Total = total,
                Rows = rows,
                Message = $"查询完成，共 {total} 个任务"
            });
        }

        /// <summary>查询任务项列表（含检查结果）</summary>
        public async Task<Result<CertTaskItemsResponse>> GetItemsWithResultsAsync(
            string userCode, string taskCode)
        {
            var taskResult = await _db.GetOneAsync<CertTask>(t => t.Code == taskCode && !t.IsDeleted);
            if (taskResult.Data == null)
                return Result<CertTaskItemsResponse>.Fail("任务不存在");

            // 查任务项
            var itemsResult = await _db.GetListAsync<CertTaskItem>(i =>
                i.TaskCode == taskCode && !i.IsDeleted);
            var itemList = (itemsResult.Data ?? new List<CertTaskItem>())
                .OrderBy(i => i.Sort)
                .ToList();

            // 查检查结果
            // ★ 2026-11-01：一个检查项现在可能有多组结果行（每次分析一组、每条款一行）。
            //   界面只展示「当前分析」的那组 —— 历史分析留给"历史对比"视图（§4.5.7）。
            var checkItemCodes = itemList.Select(i => i.CheckItemCode).Distinct().ToList();
            var resultList = new List<CertCheckResult>();
            if (checkItemCodes.Count > 0)
            {
                var currentAnalyses = (await _db.GetListAsync<CertCheckAnalysis>(a =>
                    checkItemCodes.Contains(a.CheckItemCode)
                    && a.IsCurrent == 1
                    && !a.IsDeleted)).Data ?? new List<CertCheckAnalysis>();

                var currentAnalysisCodes = currentAnalyses.Select(a => a.Code).ToList();
                if (currentAnalysisCodes.Count > 0)
                {
                    var resultsResult = await _db.GetListAsync<CertCheckResult>(r =>
                        currentAnalysisCodes.Contains(r.AnalysisCode) && !r.IsDeleted);
                    resultList = resultsResult.Data ?? new List<CertCheckResult>();
                }
            }

            // ★ 锁在 cert_check_lock（检查项级），不在结果行上
            var lockRows = checkItemCodes.Count == 0
                ? new List<CertCheckLock>()
                : (await _db.GetListAsync<CertCheckLock>(l =>
                    checkItemCodes.Contains(l.CheckItemCode)
                    && l.IsActive && !l.IsDeleted)).Data ?? new List<CertCheckLock>();

            var lockMap = lockRows
                .GroupBy(l => l.CheckItemCode)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.LockedAt).First());

            // 拼接 code 字段（ClauseNumbers / DeptNames 从 plan_detail 冗余）
            var detailsMap = new Dictionary<string, CertAuditPlanDetail>();
            if (checkItemCodes.Count > 0)
            {
                var detailsResult = await _db.GetListAsync<CertAuditPlanDetail>(d =>
                    checkItemCodes.Contains(d.Code) && !d.IsDeleted);
                foreach (var d in (detailsResult.Data ?? new List<CertAuditPlanDetail>()))
                    detailsMap[d.Code] = d;
            }

            var rows = itemList.Select(i =>
            {
                detailsMap.TryGetValue(i.CheckItemCode, out var detail);
                return new CertTaskItemRow
                {
                    Code = i.Code,
                    TaskCode = i.TaskCode,
                    CheckItemCode = i.CheckItemCode,
                    Status = i.Status,
                    EvidenceState = i.EvidenceState,
                    AnalysisState = i.AnalysisState,
                    RunMode = i.RunMode,
                    SkipLocked = i.SkipLocked,
                    RerunState = i.RerunState,
                    ClauseNumbers = detail?.ClauseNumbers,
                    DeptNames = detail?.DeptNames,
                    Sort = i.Sort
                };
            }).ToList();

            var resultRows = resultList.Select(r =>
            {
                lockMap.TryGetValue(r.CheckItemCode, out var lk);
                return new CertCheckResultRow
                {
                    Code = r.Code,
                    CheckItemCode = r.CheckItemCode,
                    AnalysisCode = r.AnalysisCode,
                    StageKind = r.StageKind,
                    ClauseCode = r.ClauseCode,
                    StandardCode = r.StandardCode,
                    ClauseNumber = r.ClauseNumber,
                    PrepState = r.PrepState,
                    Conclusion = r.Conclusion,
                    NcSuggested = r.NcSuggested,
                    Severity = r.Severity,
                    Reason = r.Reason,
                    // 锁来自 cert_check_lock（检查项级），不是结果行
                    IsLocked = lk != null,
                    LockedBy = lk?.LockedBy,
                    LockedAt = lk?.LockedAt,
                    IsMandatory = r.IsMandatory,
                    VerifyLevel = r.VerifyLevel
                };
            }).ToList();

            return Result<CertTaskItemsResponse>.Ok(new CertTaskItemsResponse
            {
                Rows = rows,
                Results = resultRows,
                Message = $"{rows.Count} 个检查项，{resultRows.Count} 个结论"
            });
        }

        // ================================================================
        // 六、计划统一视图聚合（V2 准备+检查一体化）
        // ================================================================

        /// <summary>
        /// 按计划查询检查项聚合列表（含部门、联系人、证据计数、结论状态）。
        /// <para>覆盖场景：认证准备（prep）+ 认证检查（onsite）共用同一张大表。</para>
        /// </summary>
        public async Task<Result<CertPlanChecklistResponse>> GetPlanChecklistAsync(
            string userCode, string planCode)
        {
            var planResult = await _db.GetOneAsync<CertAuditPlan>(p =>
                p.Code == planCode && !p.IsDeleted);
            if (planResult.Data == null)
                return Result<CertPlanChecklistResponse>.Fail("计划不存在");
            var plan = planResult.Data;

            // 所有 plan_detail
            var detailsResult = await _db.GetListAsync<CertAuditPlanDetail>(d =>
                d.PlanCode == planCode && !d.IsDeleted);
            var details = (detailsResult.Data ?? new List<CertAuditPlanDetail>())
                .OrderBy(d => d.Sort)
                .ToList();

            // 所有 cert_task（该计划下的所有任务）
            var tasksResult = await _db.GetListAsync<CertTask>(t =>
                t.PlanCode == planCode && !t.IsDeleted);
            var taskList = tasksResult.Data ?? new List<CertTask>();

            // 所有 task_item + check_result（批量）
            var taskCodes = taskList.Select(t => t.Code).ToList();
            var itemList = new List<CertTaskItem>();
            var resultList = new List<CertCheckResult>();
            if (taskCodes.Count > 0)
            {
                var itemsResult = await _db.GetListAsync<CertTaskItem>(i =>
                    taskCodes.Contains(i.TaskCode) && !i.IsDeleted);
                itemList = itemsResult.Data ?? new List<CertTaskItem>();

                var checkItemCodes = details.Select(d => d.Code).Distinct().ToList();
                if (checkItemCodes.Count > 0)
                {
                    // ★ 2026-11-01：只取「当前分析」的结果行 ——
                    //   一个检查项每个分支（prep / onsite）各有一组当前结果，历史分析不进列表。
                    var currentAnalyses = (await _db.GetListAsync<CertCheckAnalysis>(a =>
                        checkItemCodes.Contains(a.CheckItemCode)
                        && a.IsCurrent == 1
                        && !a.IsDeleted)).Data ?? new List<CertCheckAnalysis>();

                    var currentCodes = currentAnalyses.Select(a => a.Code).ToList();
                    if (currentCodes.Count > 0)
                    {
                        var resultsResult = await _db.GetListAsync<CertCheckResult>(r =>
                            currentCodes.Contains(r.AnalysisCode) && !r.IsDeleted);
                        resultList = resultsResult.Data ?? new List<CertCheckResult>();
                    }
                }
            }

            // 所有证据（按 detail.Code = CheckItemCode 关联）
            var detailCodes = details.Select(d => d.Code).ToList();
            var evidenceList = new List<CertEvidence>();
            if (detailCodes.Count > 0)
            {
                var evidenceResult = await _db.GetListAsync<CertEvidence>(e =>
                    detailCodes.Contains(e.CheckItemCode) && !e.IsDeleted && e.Status != "dropped");
                evidenceList = evidenceResult.Data ?? new List<CertEvidence>();
            }

            // 所有用于部门-联系人查询：取 plan_detail 中所有部门编码
            var deptCodes = new HashSet<string>();
            foreach (var d in details)
            {
                if (!string.IsNullOrEmpty(d.Departments))
                {
                    foreach (var c in d.Departments.Split(',', StringSplitOptions.RemoveEmptyEntries))
                        deptCodes.Add(c.Trim());
                }
            }
            var deptList = new List<CertEnterpriseDept>();
            var memberList = new List<CertEnterpriseDeptMember>();
            if (deptCodes.Count > 0)
            {
                var deptResult = await _db.GetListAsync<CertEnterpriseDept>(d =>
                    deptCodes.Contains(d.Code) && !d.IsDeleted);
                deptList = deptResult.Data ?? new List<CertEnterpriseDept>();

                var memberResult = await _db.GetListAsync<CertEnterpriseDeptMember>(m =>
                    deptCodes.Contains(m.DeptCode) && !m.IsDeleted && m.IsValid == 1);
                memberList = memberResult.Data ?? new List<CertEnterpriseDeptMember>();
            }

            // 组装响应
            // ★ 分支视图（§4.5.7）：准备分支看 PrepState、检查分支看 Conclusion ——
            //   同一张表 + StageKind 区分，一次查询就出两个分支，不用 UNION。
            var prepMap = resultList
                .Where(r => r.StageKind == CertCheckEngineService.StagePrep)
                .GroupBy(r => r.CheckItemCode)
                .ToDictionary(g => g.Key, g => g.OrderBy(r => r.ClauseNumber).First());

            var onsiteMap = resultList
                .Where(r => r.StageKind == CertCheckEngineService.StageOnsite)
                .GroupBy(r => r.CheckItemCode)
                .ToDictionary(g => g.Key, g => g.OrderBy(r => r.ClauseNumber).First());

            // 锁：检查项级（cert_check_lock），不是结果行
            var lockRows = detailCodes.Count == 0
                ? new List<CertCheckLock>()
                : (await _db.GetListAsync<CertCheckLock>(l =>
                    detailCodes.Contains(l.CheckItemCode)
                    && l.IsActive && !l.IsDeleted)).Data ?? new List<CertCheckLock>();

            var lockMap = lockRows
                .GroupBy(l => l.CheckItemCode)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.LockedAt).First());

            var evidenceByCheckItem = evidenceList
                .GroupBy(e => e.CheckItemCode)
                .ToDictionary(g => g.Key, g => g.ToList());
            var membersByDept = memberList
                .GroupBy(m => m.DeptCode)
                .ToDictionary(g => g.Key, g => g.ToList());

            var rows = details.Select(d =>
            {
                var evis = evidenceByCheckItem.GetValueOrDefault(d.Code.ToString()!) ?? new List<CertEvidence>();
                // DetailCode 实际类型是 string（Code 字段）
                return BuildPlanCheckItemRow(d, evis, prepMap, onsiteMap, lockMap,
                    deptList, membersByDept, taskList, itemList);
            }).ToList();

            // ★ 页面锁的「唯一真相」出口（F12）：前端不自己维护「我点了开始所以只读」，
            //   而是每轮轮询都问后端「现在有没有 running 任务」—— 刷新页面 / 换个人打开
            //   / 队列在后台跑完，前端都能立刻跟上（本地状态做不到）。
            var runningTask = taskList
                .Where(t => t.Status == "running")
                .OrderByDescending(t => t.StartedAt ?? DateTime.MinValue)
                .FirstOrDefault();

            var resp = new CertPlanChecklistResponse
            {
                PlanCode = planCode,
                PlanName = plan.Name,
                PlanStatus = plan.Status,
                PlanClosed = plan.Status == "closed",
                TasksJson = JsonSerializer.Serialize(taskList.Select(t => new
                {
                    t.Code,
                    t.TaskNo,
                    t.TaskKind,
                    t.Status
                })),
                Rows = rows,
                TaskRunning = runningTask != null,
                RunningTaskCode = runningTask?.Code,
                RunningTaskNo = runningTask?.TaskNo,
                RunningTaskKind = runningTask?.TaskKind,
                RunningItemTotal = runningTask?.ItemTotal ?? 0,
                RunningItemDone = runningTask?.ItemDone ?? 0,
                RunningItemSkipped = runningTask?.ItemSkipped ?? 0,
                Message = $"{rows.Count} 个检查项"
            };

            return Result<CertPlanChecklistResponse>.Ok(resp);
        }

        private CertPlanCheckItemRow BuildPlanCheckItemRow(
            CertAuditPlanDetail d,
            List<CertEvidence> evidences,
            Dictionary<string, CertCheckResult> prepMap,
            Dictionary<string, CertCheckResult> onsiteMap,
            Dictionary<string, CertCheckLock> lockMap,
            List<CertEnterpriseDept> depts,
            Dictionary<string, List<CertEnterpriseDeptMember>> membersByDept,
            List<CertTask> tasks,
            List<CertTaskItem> items)
        {
            // 部门信息取第一个部门
            var firstDeptCode = (d.Departments ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim()).FirstOrDefault();
            var firstDept = !string.IsNullOrEmpty(firstDeptCode)
                ? depts.FirstOrDefault(dep => dep.Code == firstDeptCode)
                : null;

            var contactsArr = new List<object>();
            if (firstDept != null)
            {
                var leader = new List<object>();
                if (!string.IsNullOrEmpty(firstDept.LeaderName))
                {
                    leader.Add(new
                    {
                        name = firstDept.LeaderName,
                        position = firstDept.LeaderTitle ?? "负责人",
                        phone = firstDept.LeaderPhone ?? ""
                    });
                }
                if (membersByDept.TryGetValue(firstDeptCode, out var members))
                {
                    foreach (var m in members.OrderBy(x => x.Sort).Take(3))
                    {
                        leader.Add(new { name = m.Name, position = m.RoleType ?? m.Position ?? "", phone = m.Phone ?? "" });
                    }
                }
                contactsArr = leader;
            }

            // 该 detail 关联的 tasks
            var detailTaskItems = items.Where(i => i.CheckItemCode == d.Code.ToString()).ToList();
            var detailTaskCodes = detailTaskItems.Select(i => i.TaskCode).Distinct().ToList();
            var detailTasks = tasks.Where(t => detailTaskCodes.Contains(t.Code))
                .Select(t => new { t.Code, t.TaskNo, t.TaskKind, t.Status }).ToList();

            // 最新结论：★ 两个分支各取各的（§4.5.7）
            prepMap.TryGetValue(d.Code ?? "", out var rPrep);
            onsiteMap.TryGetValue(d.Code ?? "", out var rOnsite);

            // 严重程度 / NC 建议只看检查分支（准备分支没有这两个概念）
            var r = rOnsite;

            // 证据分组：原始(doc_slice/pq_result) vs 检查(manual/onsite_media/llm)
            var rawCount = evidences.Count(e =>
                e.EvidenceKind == "doc_slice" || e.EvidenceKind == "pq_result" ||
                e.CapturedIn == "prep" || e.CapturedIn == "doc" || e.CapturedIn == "pq");
            var checkCount = evidences.Count(e =>
                e.EvidenceKind == "manual" || e.EvidenceKind == "onsite_media" ||
                e.CapturedIn == "onsite" || e.CapturedIn == "manual");

            // 疑点数 = 准备期 NC 提示 + 检查期疑点分析（AI 产出的多条疑点；旧数据回退 NC 建议）
            var onsiteDoubts = ParseDoubtList(rOnsite?.DoubtsJson);
            var doubtCount = 0;
            if (rPrep != null && !string.IsNullOrEmpty(rPrep.PrepNcHint)) doubtCount++;
            if (onsiteDoubts.Count > 0) doubtCount += onsiteDoubts.Count;
            else if (rOnsite != null && rOnsite.NcSuggested == true) doubtCount++;

            lockMap.TryGetValue(d.Code ?? "", out var lk);

            // ★ 自动分析状态（计划总览用）：检查分支优先，准备分支兜底
            var prepTaskCodes = tasks
                .Where(t => t.TaskKind == CertCheckEngineService.StagePrep)
                .Select(t => t.Code).ToHashSet();
            var onsiteTaskCodes = tasks
                .Where(t => t.TaskKind == CertCheckEngineService.StageOnsite)
                .Select(t => t.Code).ToHashSet();
            var tiOnsite = detailTaskItems.FirstOrDefault(i => onsiteTaskCodes.Contains(i.TaskCode));
            var tiPrep = detailTaskItems.FirstOrDefault(i => prepTaskCodes.Contains(i.TaskCode));
            var ti = tiOnsite ?? tiPrep;
            var analysisState = ti?.AnalysisState;
            if (string.IsNullOrWhiteSpace(analysisState))
                analysisState = rOnsite != null || rPrep != null ? "done" : "none";

            return new CertPlanCheckItemRow
            {
                DetailCode = d.Code,
                ClauseNumbers = d.ClauseNumbers,
                DeptName = firstDept?.DeptName,
                DeptCode = firstDeptCode,
                ContactsJson = contactsArr.Count > 0 ? JsonSerializer.Serialize(contactsArr) : null,
                DeptNames = d.DeptNames,
                TasksJson = JsonSerializer.Serialize(detailTasks),
                PrepState = rPrep?.PrepState,           // ← 准备分支
                // ★ 2026-11-03：Conclusion 只认审核员判定（引擎不再写它）
                Conclusion = CertVerdictMapper.ToConclusion(rOnsite?.Conclusion),
                AiConclusion = CertVerdictMapper.ToConclusion(rOnsite?.AiConclusion),
                AiSeverity = CertVerdictMapper.NormalizeSeverity(rOnsite?.AiSeverity),
                ConclusionConfirmed = !string.IsNullOrWhiteSpace(rOnsite?.ConclusionBy),
                Severity = r?.Severity,
                NcSuggested = r?.NcSuggested,
                OwnerCode = d.OwnerCode,
                OwnerName = d.OwnerName,
                IsLocked = lk != null,                  // ← 检查项级锁
                LockedBy = lk?.LockedBy,
                LockedAt = lk?.LockedAt,
                RawEvidenceCount = rawCount,
                CheckEvidenceCount = checkCount,
                DoubtCount = doubtCount,
                SupplementCount = checkCount,
                Sort = d.Sort,
                AnalysisState = analysisState,
                RerunState = string.IsNullOrWhiteSpace(ti?.RerunState) ? "none" : ti!.RerunState,
                TaskItemCode = ti?.Code,
                LastAnalyzedAt = ti?.LastAnalyzedAt
            };
        }

        /// <summary>查询单个计划检查项的侧面详情（含双层证据、部门联系人、锁定信息）</summary>
        public async Task<Result<CertPlanCheckItemDetail>> GetPlanCheckItemDetailAsync(
            string userCode, string detailCode)
        {
            var detailResult = await _db.GetOneAsync<CertAuditPlanDetail>(d =>
                d.Code == detailCode && !d.IsDeleted);
            if (detailResult.Data == null)
                return Result<CertPlanCheckItemDetail>.Fail("计划明细不存在");
            var detail = detailResult.Data;

            // 证据（全部）
            var evidenceResult = await _db.GetListAsync<CertEvidence>(e =>
                e.CheckItemCode == detail.Code && !e.IsDeleted && e.Status != "dropped");
            var allEvis = evidenceResult.Data ?? new List<CertEvidence>();

            var rawEvis = allEvis.Where(e =>
                e.EvidenceKind == "doc_slice" || e.EvidenceKind == "pq_result" ||
                e.CapturedIn == "prep" || e.CapturedIn == "doc" || e.CapturedIn == "pq")
                .OrderBy(e => e.Sort).ToList();
            var checkEvis = allEvis.Where(e =>
                e.EvidenceKind == "manual" || e.EvidenceKind == "onsite_media" ||
                e.CapturedIn == "onsite" || e.CapturedIn == "manual")
                .OrderBy(e => e.Sort).ToList();

            // 最新结论：★ 两个分支各取各的当前分析（§4.5.7）
            var currentAnalyses = (await _db.GetListAsync<CertCheckAnalysis>(a =>
                a.CheckItemCode == detail.Code && a.IsCurrent == 1 && !a.IsDeleted)).Data
                ?? new List<CertCheckAnalysis>();

            var currentCodes = currentAnalyses.Select(a => a.Code).ToList();
            var currentResults = currentCodes.Count == 0
                ? new List<CertCheckResult>()
                : (await _db.GetListAsync<CertCheckResult>(r =>
                    currentCodes.Contains(r.AnalysisCode) && !r.IsDeleted)).Data
                  ?? new List<CertCheckResult>();

            var rPrep = currentResults.FirstOrDefault(r => r.StageKind == CertCheckEngineService.StagePrep);
            var rOnsite = currentResults.FirstOrDefault(r => r.StageKind == CertCheckEngineService.StageOnsite);
            var r = rOnsite ?? rPrep;

            // 锁：检查项级
            var lockRows = (await _db.GetListAsync<CertCheckLock>(l =>
                l.CheckItemCode == detail.Code && l.IsActive && !l.IsDeleted)).Data
                ?? new List<CertCheckLock>();
            var lk = lockRows.OrderByDescending(x => x.LockedAt).FirstOrDefault();

            // 部门 + 联系人
            var deptCodes = (detail.Departments ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList();

            var deptContacts = new List<object>();
            if (deptCodes.Count > 0)
            {
                var deptResult = await _db.GetListAsync<CertEnterpriseDept>(d =>
                    deptCodes.Contains(d.Code) && !d.IsDeleted);
                foreach (var dept in deptResult.Data ?? new List<CertEnterpriseDept>())
                {
                    var memResult = await _db.GetListAsync<CertEnterpriseDeptMember>(m =>
                        m.DeptCode == dept.Code && !m.IsDeleted && m.IsValid == 1);
                    deptContacts.Add(new
                    {
                        deptCode = dept.Code,
                        deptName = dept.DeptName,
                        leaderName = dept.LeaderName,
                        leaderPhone = dept.LeaderPhone,
                        contacts = (memResult.Data ?? new List<CertEnterpriseDeptMember>())
                            .OrderBy(m => m.Sort)
                            .Select(m => new { m.Name, m.Position, m.Phone, m.RoleType })
                    });
                }
            }

            // 疑点（准备分支：PrepNcHint + ConflictsJson；检查分支：AI 疑点分析，可多条）
            var doubts = new List<object>();
            if (rPrep != null && !string.IsNullOrEmpty(rPrep.PrepNcHint))
                doubts.Add(new { kind = "prep_nc_hint", point = "准备期不符合提示", detail = rPrep.PrepNcHint, text = rPrep.PrepNcHint });
            // ★ 空数组 `[]` 不算疑点：直接透出会让抽屉里出现一条内容为「[]」的假疑点
            if (rPrep != null && !IsEmptyJsonArray(rPrep.ConflictsJson))
                doubts.Add(new { kind = "conflict", point = "资料冲突", detail = rPrep.ConflictsJson, text = rPrep.ConflictsJson });
            foreach (var dt in ParseDoubtList(rOnsite?.DoubtsJson))
            {
                doubts.Add(new
                {
                    kind = "onsite_doubt",
                    point = dt.Point,
                    detail = dt.Detail,
                    text = string.IsNullOrWhiteSpace(dt.Detail) ? dt.Point : $"{dt.Point}：{dt.Detail}"
                });
            }
            // 旧数据兜底：没有多条疑点时，把 NC 建议当一条疑点
            if (doubts.Count == 0 && rOnsite?.NcSuggested == true)
                doubts.Add(new { kind = "onsite_nc_suggested", point = "系统建议开不符合项", detail = rOnsite.Reason, text = rOnsite.Reason });

            // 判定人姓名（ConclusionBy 存的是用户 Code）
            var nameMap = await _userNames.ResolveManyAsync(new[] { rOnsite?.ConclusionBy });

            // ★ 自动分析状态：优先检查分支的任务项，回退准备分支
            var taskList = (await _db.GetListAsync<CertTask>(t =>
                t.PlanCode == detail.PlanCode && !t.IsDeleted)).Data ?? new List<CertTask>();
            var detailItems = (await _db.GetListAsync<CertTaskItem>(i =>
                i.CheckItemCode == detail.Code && !i.IsDeleted)).Data ?? new List<CertTaskItem>();
            var taskKindMap = taskList.ToDictionary(t => t.Code, t => t.TaskKind);
            var tiOnsite = detailItems
                .Where(i => taskKindMap.TryGetValue(i.TaskCode, out var k)
                            && k == CertCheckEngineService.StageOnsite)
                .OrderByDescending(i => i.AnalyzeCount)
                .FirstOrDefault();
            var tiPrep = detailItems
                .Where(i => taskKindMap.TryGetValue(i.TaskCode, out var k)
                            && k == CertCheckEngineService.StagePrep)
                .OrderByDescending(i => i.AnalyzeCount)
                .FirstOrDefault();
            var ti = rOnsite != null ? tiOnsite ?? tiPrep : tiPrep ?? tiOnsite;
            var analysisState = ti?.AnalysisState;
            if (string.IsNullOrWhiteSpace(analysisState))
                analysisState = r != null ? "done" : "none";

            return Result<CertPlanCheckItemDetail>.Ok(new CertPlanCheckItemDetail
            {
                AnalysisState = analysisState,
                RerunState = string.IsNullOrWhiteSpace(ti?.RerunState) ? "none" : ti!.RerunState,
                TaskItemCode = ti?.Code,
                AnalyzeCount = ti?.AnalyzeCount ?? 0,
                LastAnalyzedAt = ti?.LastAnalyzedAt,
                DetailCode = detail.Code,
                // 前端保存/锁定/解锁的入口 Code —— 优先检查分支（现场判定才是最终结论），回退准备分支
                ResultCode = r?.Code,
                AnalysisCode = r?.AnalysisCode,
                StageKind = r?.StageKind,
                ClauseNumbers = detail.ClauseNumbers,
                DeptNames = detail.DeptNames,
                DeptContactsJson = deptContacts.Count > 0 ? JsonSerializer.Serialize(deptContacts) : null,
                PrepState = rPrep?.PrepState,
                // ★ 归一化（CertVerdictMapper 是唯一出口）：历史结论行可能存的是 LLM
                //   原始词表（major_nc），直接透出会让抽屉里显示「major_nc」这种内部值。
                Conclusion = CertVerdictMapper.ToConclusion(rOnsite?.Conclusion),
                // ★ 2026-11-03：系统建议与审核员判定分开（建议仅供参考，最终由审核员判定）
                AiConclusion = CertVerdictMapper.ToConclusion(rOnsite?.AiConclusion),
                AiSeverity = CertVerdictMapper.NormalizeSeverity(rOnsite?.AiSeverity),
                Severity = CertVerdictMapper.ToSeverity(rOnsite?.Conclusion, rOnsite?.Severity),
                Reason = r?.Reason,
                ConclusionBy = rOnsite?.ConclusionBy,
                ConclusionByName = rOnsite?.ConclusionBy == null
                    ? null
                    : nameMap.TryGetValue(rOnsite.ConclusionBy, out var nm) ? nm : rOnsite.ConclusionBy,
                ConclusionAt = rOnsite?.ConclusionAt,
                ConclusionConfirmed = !string.IsNullOrWhiteSpace(rOnsite?.ConclusionBy),
                OwnerCode = detail.OwnerCode,
                OwnerName = detail.OwnerName,
                NcSuggested = rOnsite?.NcSuggested,
                IsLocked = lk != null,
                LockedBy = lk?.LockedBy,
                LockedAt = lk?.LockedAt,
                RawEvidences = rawEvis.Select(e => new CertEvidenceRow
                {
                    Code = e.Code,
                    CheckItemCode = e.CheckItemCode,
                    CapturedIn = e.CapturedIn,
                    EvidenceKind = e.EvidenceKind,
                    SourceName = e.SourceName,
                    SourceCode = e.SourceCode,
                    Anchor = e.Anchor,
                    Snippet = e.Snippet,
                    LlmSummary = e.LlmSummary,
                    Status = e.Status,
                    Sort = e.Sort
                }).ToList(),
                CheckEvidences = checkEvis.Select(e => new CertEvidenceRow
                {
                    Code = e.Code,
                    CheckItemCode = e.CheckItemCode,
                    CapturedIn = e.CapturedIn,
                    EvidenceKind = e.EvidenceKind,
                    SourceName = e.SourceName,
                    SourceCode = e.SourceCode,
                    Anchor = e.Anchor,
                    Snippet = e.Snippet,
                    LlmSummary = e.LlmSummary,
                    Status = e.Status,
                    Sort = e.Sort
                }).ToList(),
                DoubtsJson = doubts.Count > 0 ? JsonSerializer.Serialize(doubts) : null,
                Message = $"详情已加载"
            });
        }

        /// <summary>
        /// 判断一段 JSON 是否是「空数组 / 空值」。
        /// <para>用途：<c>ConflictsJson</c> 之类由模型输出的集合字段，空的时候是 <c>[]</c>，
        /// 不是 <c>null</c> —— 只判 <c>IsNullOrEmpty</c> 会把 <c>[]</c> 当成一条疑点渲染出去。</para>
        /// </summary>
        private static bool IsEmptyJsonArray(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return true;
            var s = json.Trim();
            return s is "[]" or "{}" or "null";
        }

        /// <summary>
        /// 解析疑点分析 JSON（<c>[{point, detail}]</c> 或 <c>["..."]</c>）。
        /// <para>空 / 非法 JSON ⇒ 空列表（不抛异常：疑点解析失败不能把整个详情拖垮）。</para>
        /// </summary>
        private static List<CertDoubtDto> ParseDoubtList(string? json)
        {
            if (string.IsNullOrWhiteSpace(json) || IsEmptyJsonArray(json))
                return new List<CertDoubtDto>();

            try
            {
                var arr = JsonSerializer.Deserialize<JsonElement>(json!, JsonOpts);
                if (arr.ValueKind != JsonValueKind.Array) return new List<CertDoubtDto>();

                var list = new List<CertDoubtDto>();
                foreach (var item in arr.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        var text = item.GetString();
                        if (!string.IsNullOrWhiteSpace(text))
                            list.Add(new CertDoubtDto { Point = text!.Trim() });
                        continue;
                    }
                    if (item.ValueKind != JsonValueKind.Object) continue;

                    var point = item.TryGetProperty("point", out var p) && p.ValueKind == JsonValueKind.String
                        ? p.GetString() ?? "" : "";
                    var detail = item.TryGetProperty("detail", out var d) && d.ValueKind == JsonValueKind.String
                        ? d.GetString() ?? "" : "";
                    if (string.IsNullOrWhiteSpace(point) && !string.IsNullOrWhiteSpace(detail))
                        point = detail.Length > 40 ? detail[..40] : detail;
                    if (string.IsNullOrWhiteSpace(point) && string.IsNullOrWhiteSpace(detail)) continue;
                    list.Add(new CertDoubtDto { Point = point.Trim(), Detail = detail.Trim() });
                }
                return list;
            }
            catch
            {
                return new List<CertDoubtDto>();
            }
        }

        // ================================================================
        // 七、跨计划检查项清单（认证准备 / 认证检查 两个独立菜单）
        // ================================================================

        /// <summary>
        /// 跨计划检查项清单 —— 认证准备 / 认证检查两个独立列表页的唯一数据源。
        ///
        /// <para><b>为什么不用 <see cref="GetPlanChecklistAsync"/></b>：那个方法是「一个计划 → 它自己的检查项」，
        /// 是从计划详情页进去看的视角。但用户要的是<b>两个独立菜单</b>：
        /// 审核员打开「认证准备」，看到的是「所有计划里我需要做的准备项」，
        /// 不关心它属于哪个计划。所以这里换成「按分支平铺全部计划」的视角。</para>
        ///
        /// <para><b>★ 统计口径</b>：<see cref="CertWorklistResponse.Summary"/> 基于
        /// 「除状态外」的全部过滤条件统计（关键词 / 计划 / 企业 / 审核员），
        /// <b>不受 Status 与分页影响</b> —— 否则用户点「已锁定」页签时，
        /// 其它页签的数字全变 0，就没法用数字导航了。</para>
        ///
        /// <para><b>★ 条款号取权威值</b>：<c>cert_audit_plan_detail.ClauseNumbers</c> 是冗余串，
        /// 实测有 <c>"m 5.5"</c> 这种脏前缀；列表展示统一回查 <c>cert_iso_clause</c>
        /// 拿 <c>ClauseNumber</c> + <c>Title</c>，脏串只作兜底。</para>
        /// </summary>
        public async Task<Result<CertWorklistResponse>> GetWorklistAsync(
            string userCode, CertWorklistRequest req)
        {
            var stageKind = (req.StageKind ?? CertCheckEngineService.StagePrep)
                .Trim().ToLowerInvariant();
            if (stageKind != CertCheckEngineService.StagePrep
                && stageKind != CertCheckEngineService.StageOnsite)
                stageKind = CertCheckEngineService.StagePrep;

            var pageIndex = req.PageIndex <= 0 ? 1 : req.PageIndex;
            var pageSize = req.PageSize <= 0 ? 20 : Math.Min(req.PageSize, 200);

            // ── 1. 计划（数据量级小，全量取后内存过滤；避免动态 Expression 拼装）──
            var allPlans = (await _db.GetListAsync<CertAuditPlan>(p => !p.IsDeleted)).Data
                ?? new List<CertAuditPlan>();

            var plans = allPlans.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(req.PlanCode))
                plans = plans.Where(p => p.Code == req.PlanCode);
            if (!string.IsNullOrWhiteSpace(req.EnterpriseCode))
                plans = plans.Where(p => p.EnterpriseCode == req.EnterpriseCode);
            var planList = plans.ToList();

            if (planList.Count == 0)
            {
                return Result<CertWorklistResponse>.Ok(new CertWorklistResponse
                {
                    PageIndex = pageIndex,
                    PageSize = pageSize,
                    Message = "没有符合条件的计划"
                });
            }

            var planCodes = planList.Select(p => p.Code).ToList();

            // ── 2. 计划明细（检查项本体）──
            var details = (await _db.GetListAsync<CertAuditPlanDetail>(d =>
                planCodes.Contains(d.PlanCode) && !d.IsDeleted && d.IsValid == 1)).Data
                ?? new List<CertAuditPlanDetail>();
            details = details.OrderBy(d => d.PlanCode).ThenBy(d => d.Sort).ToList();

            if (details.Count == 0)
            {
                return Result<CertWorklistResponse>.Ok(new CertWorklistResponse
                {
                    PageIndex = pageIndex,
                    PageSize = pageSize,
                    Plans = BuildPlanOptions(planList, new List<CertAuditPlanDetail>()),
                    Message = "该分支下没有检查项"
                });
            }

            var detailCodes = details.Select(d => d.Code).ToList();

            // ── 3. 任务（判断页面锁 + 拿分析次数）──
            var stageTasks = (await _db.GetListAsync<CertTask>(t =>
                planCodes.Contains(t.PlanCode) && !t.IsDeleted)).Data
                ?? new List<CertTask>();

            var runningTasks = stageTasks
                .Where(t => t.Status == "running" && t.TaskKind == stageKind)
                .ToList();
            var runningPlanTasks = runningTasks
                .GroupBy(t => t.PlanCode)
                .ToDictionary(g => g.Key, g => g.First().TaskNo);

            var taskItems = new List<CertTaskItem>();
            var taskCodes = stageTasks.Where(t => t.TaskKind == stageKind).Select(t => t.Code).ToList();
            if (taskCodes.Count > 0)
            {
                taskItems = (await _db.GetListAsync<CertTaskItem>(i =>
                    taskCodes.Contains(i.TaskCode) && !i.IsDeleted)).Data
                    ?? new List<CertTaskItem>();
            }
            var taskItemByCheckItem = taskItems
                .GroupBy(i => i.CheckItemCode)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.AnalyzeCount).First());

            // ── 4. 当前分析 + 结论（★ 只看 IsCurrent=1，历史分析不进列表）──
            var currentAnalyses = (await _db.GetListAsync<CertCheckAnalysis>(a =>
                detailCodes.Contains(a.CheckItemCode) && a.IsCurrent == 1 && !a.IsDeleted)).Data
                ?? new List<CertCheckAnalysis>();

            var analysisCodes = currentAnalyses.Select(a => a.Code).ToList();
            var allResults = analysisCodes.Count == 0
                ? new List<CertCheckResult>()
                : (await _db.GetListAsync<CertCheckResult>(r =>
                    analysisCodes.Contains(r.AnalysisCode) && !r.IsDeleted)).Data
                  ?? new List<CertCheckResult>();

            // 一个检查项在同一分支下可能有多条款结果行 → 取第一条作为列表代表行
            var stageResultMap = allResults
                .Where(r => r.StageKind == stageKind)
                .GroupBy(r => r.CheckItemCode)
                .ToDictionary(g => g.Key, g => g.OrderBy(r => r.ClauseNumber).First());

            // 另一分支的结论也留着 —— 列表上要显示「准备：缺失 / 检查：不符合」两个都看得到
            var otherKind = stageKind == CertCheckEngineService.StagePrep
                ? CertCheckEngineService.StageOnsite
                : CertCheckEngineService.StagePrep;
            var otherResultMap = allResults
                .Where(r => r.StageKind == otherKind)
                .GroupBy(r => r.CheckItemCode)
                .ToDictionary(g => g.Key, g => g.OrderBy(r => r.ClauseNumber).First());

            // ── 5. 锁（检查项级）──
            var lockRows = (await _db.GetListAsync<CertCheckLock>(l =>
                detailCodes.Contains(l.CheckItemCode) && l.IsActive && !l.IsDeleted)).Data
                ?? new List<CertCheckLock>();
            var lockMap = lockRows
                .GroupBy(l => l.CheckItemCode)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.LockedAt).First());

            // ── 6. 证据计数 ──
            var evidences = (await _db.GetListAsync<CertEvidence>(e =>
                detailCodes.Contains(e.CheckItemCode) && !e.IsDeleted && e.Status != "dropped")).Data
                ?? new List<CertEvidence>();
            var evidenceByItem = evidences
                .GroupBy(e => e.CheckItemCode)
                .ToDictionary(g => g.Key, g => g.ToList());

            // ── 7. 企业 / 阶段（展示名）── 走 ICoreResourceQuery（铁律 L5）
            var entCodes = planList.Select(p => p.EnterpriseCode).Distinct().ToList();
            var enterpriseMap = (await _core.GetEnterprisesByCodesAsync(entCodes))
                .GroupBy(e => e.Code).ToDictionary(g => g.Key, g => g.First());

            var stgCodes = planList.Select(p => p.StageCode)
                .Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
            var stageMap = (await _core.GetStagesByCodesAsync(stgCodes!))
                .GroupBy(s => s.Code).ToDictionary(g => g.Key, g => g.First());

            // ── 8. 条款（权威编号 + 标题）──
            var clauseGuids = details
                .SelectMany(d => (d.Clauses ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct().ToList();
            var clauseMap = (await _core.GetClausesByCodesAsync(clauseGuids))
                .GroupBy(c => c.Code).ToDictionary(g => g.Key, g => g.First());

            // ── 9. 部门联系人（详情抽屉用，列表只取首个部门）──
            var deptCodes = details
                .SelectMany(d => (d.Departments ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct().ToList();
            var deptMap = new Dictionary<string, CertEnterpriseDept>();
            var membersByDept = new Dictionary<string, List<CertEnterpriseDeptMember>>();
            if (deptCodes.Count > 0)
            {
                deptMap = (await _db.GetListAsync<CertEnterpriseDept>(d =>
                    deptCodes.Contains(d.Code) && !d.IsDeleted)).Data?
                    .GroupBy(d => d.Code).ToDictionary(g => g.Key, g => g.First())
                    ?? new Dictionary<string, CertEnterpriseDept>();

                var members = (await _db.GetListAsync<CertEnterpriseDeptMember>(m =>
                    deptCodes.Contains(m.DeptCode) && !m.IsDeleted && m.IsValid == 1)).Data
                    ?? new List<CertEnterpriseDeptMember>();
                membersByDept = members
                    .GroupBy(m => m.DeptCode)
                    .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Sort).ToList());
            }

            // ── 10. 组装行 ──
            var planById = planList.GroupBy(p => p.Code).ToDictionary(g => g.Key, g => g.First());
            var rows = new List<CertWorklistRow>(details.Count);

            foreach (var d in details)
            {
                planById.TryGetValue(d.PlanCode, out var plan);
                var entName = plan != null && enterpriseMap.TryGetValue(plan.EnterpriseCode, out var ent)
                    ? ent.Name : null;
                var stageName = plan != null && !string.IsNullOrEmpty(plan.StageCode)
                                && stageMap.TryGetValue(plan.StageCode, out var stg)
                    ? stg.StageName : null;

                // 条款：GUID → 权威编号 + 标题
                var numbers = new List<string>();
                var titles = new List<string>();
                foreach (var g in (d.Clauses ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (clauseMap.TryGetValue(g.Trim(), out var cl))
                    {
                        numbers.Add(cl.ClauseNumber);
                        titles.Add($"{cl.ClauseNumber} {cl.Title}".Trim());
                    }
                }
                // 兜底：GUID 全部查不到时才用冗余串（实测有 "m 5.5" 脏前缀，只作最后手段）
                var clauseNumbers = numbers.Count > 0
                    ? string.Join(",", numbers)
                    : d.ClauseNumbers;

                var firstDeptCode = (d.Departments ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim()).FirstOrDefault();
                deptMap.TryGetValue(firstDeptCode ?? "", out var firstDept);

                var contacts = new List<object>();
                if (firstDept != null)
                {
                    if (!string.IsNullOrEmpty(firstDept.LeaderName))
                        contacts.Add(new
                        {
                            name = firstDept.LeaderName,
                            position = firstDept.LeaderTitle ?? "负责人",
                            phone = firstDept.LeaderPhone ?? ""
                        });
                    if (membersByDept.TryGetValue(firstDeptCode!, out var mems))
                        foreach (var m in mems.Take(3))
                            contacts.Add(new
                            {
                                name = m.Name,
                                position = m.RoleType ?? m.Position ?? "",
                                phone = m.Phone ?? ""
                            });
                }

                stageResultMap.TryGetValue(d.Code, out var rStage);
                otherResultMap.TryGetValue(d.Code, out var rOther);
                lockMap.TryGetValue(d.Code, out var lk);
                var evis = evidenceByItem.GetValueOrDefault(d.Code) ?? new List<CertEvidence>();
                taskItemByCheckItem.TryGetValue(d.Code, out var ti);

                var rawCount = evis.Count(e =>
                    e.EvidenceKind == "doc_slice" || e.EvidenceKind == "pq_result" ||
                    e.CapturedIn == "prep" || e.CapturedIn == "doc" || e.CapturedIn == "pq");
                var checkCount = evis.Count(e =>
                    e.EvidenceKind == "manual" || e.EvidenceKind == "onsite_media" ||
                    e.CapturedIn == "onsite" || e.CapturedIn == "manual");

                // 疑点：优先用 AI 的多条疑点分析；旧数据回退「NC 建议 / 结论」口径
                var stageDoubts = ParseDoubtList(rStage?.DoubtsJson);
                var doubtCount = 0;
                if (!string.IsNullOrEmpty(rOther?.PrepNcHint)) doubtCount++;
                if (stageDoubts.Count > 0) doubtCount += stageDoubts.Count;
                else if (rOther?.NcSuggested == true || rStage?.NcSuggested == true
                         || CertVerdictMapper.ToConclusion(rStage?.Conclusion) == CertVerdictMapper.ConclusionNc
                         || CertVerdictMapper.ToConclusion(rOther?.Conclusion) == CertVerdictMapper.ConclusionNc)
                    doubtCount++;
                if (!string.IsNullOrEmpty(rStage?.ConflictsJson)) doubtCount++;

                var isLocked = lk != null;
                // ★ 2026-11-03：新增「自动分析状态」——未分析 / 分析中 / 已完成 / 失败
                //   不能只看结果行是否存在：分析失败同样没有结果行，但状态必须区分出来，
                //   否则审核员看到「未分析」会以为系统不会自动分析。
                var analysisState = ti?.AnalysisState;
                if (string.IsNullOrWhiteSpace(analysisState))
                    analysisState = rStage != null ? "done" : "none";
                var rerunState = string.IsNullOrWhiteSpace(ti?.RerunState) ? "none" : ti!.RerunState;

                var state = isLocked
                    ? "locked"
                    : analysisState == "failed" || ti?.Status == "failed"
                        ? "failed"
                        : rStage != null ? "analyzed" : "waiting";

                var reason = stageKind == CertCheckEngineService.StagePrep
                    ? rStage?.PrepNcHint ?? rStage?.Reason
                    : rStage?.Reason;

                rows.Add(new CertWorklistRow
                {
                    PlanCode = d.PlanCode,
                    PlanName = plan?.Name,
                    EnterpriseCode = plan?.EnterpriseCode,
                    EnterpriseName = entName,
                    StageName = stageName,

                    DetailCode = d.Code,
                    ClauseNumbers = clauseNumbers,
                    ClauseTitles = titles.Count > 0 ? string.Join("；", titles) : null,
                    DeptNames = d.DeptNames ?? firstDept?.DeptName,
                    DeptCode = firstDeptCode,
                    DeptName = firstDept?.DeptName,
                    ContactsJson = contacts.Count > 0 ? JsonSerializer.Serialize(contacts) : null,

                    OwnerCode = d.OwnerCode,
                    OwnerName = d.OwnerName,
                    // 准备分支看「另一支」有没有 onsite 结论；检查分支本身就是在 onsite
                    OnsiteStarted = stageKind == CertCheckEngineService.StagePrep
                        ? rOther != null
                        : rStage != null,

                    StageKind = stageKind,
                    // ★ 分支字段裁剪（用户 2026-11-01：「准备不签发 NC，只有检查才能签发」）：
                    //   准备列表 —— 只看 PrepState（资料齐不齐），不带 Conclusion / Severity / NcSuggested，
                    //              否则审核员在准备页看到「nc」会误以为已经开了不符合项；
                    //   检查列表 —— 看 Conclusion，同时把 PrepState 带出去作参考
                    //              （让审核员知道这个检查项在准备期是什么底子）。
                    PrepState = stageKind == CertCheckEngineService.StagePrep
                        ? rStage?.PrepState
                        : rOther?.PrepState,
                    Conclusion = stageKind == CertCheckEngineService.StageOnsite
                        // ★ 归一化兜底：映射器上线前跑出来的历史结论行里可能还是 LLM 原始词表
                        //   （major_nc / conformant），直接展示会让前端拿到五套词表混在一起。
                        //   ToConclusion 是幂等的，已映射过的值原样返回。
                        ? CertVerdictMapper.ToConclusion(rStage?.Conclusion)
                        : null,
                    // ★ 2026-11-03：AI 建议独立成列，最终结论仍由审核员判定
                    AiConclusion = stageKind == CertCheckEngineService.StageOnsite
                        ? CertVerdictMapper.ToConclusion(rStage?.AiConclusion)
                        : null,
                    AiSeverity = stageKind == CertCheckEngineService.StageOnsite
                        ? CertVerdictMapper.NormalizeSeverity(rStage?.AiSeverity)
                        : null,
                    ConclusionConfirmed = stageKind == CertCheckEngineService.StageOnsite
                          && !string.IsNullOrWhiteSpace(rStage?.ConclusionBy),
                    Severity = stageKind == CertCheckEngineService.StageOnsite
                        ? CertVerdictMapper.NormalizeSeverity(rStage?.Severity)
                          ?? CertVerdictMapper.ToSeverity(rStage?.Conclusion, rStage?.Severity)
                        : null,
                    NcSuggested = stageKind == CertCheckEngineService.StageOnsite
                        ? rStage?.NcSuggested
                        : null,
                    ReasonBrief = Truncate(reason, 200),

                    ResultCode = rStage?.Code,
                    AnalysisCode = rStage?.AnalysisCode,
                    TaskItemCode = ti?.Code,
                    State = state,

                    IsLocked = isLocked,
                    LockedBy = lk?.LockedBy,
                    LockedAt = lk?.LockedAt,

                    RawEvidenceCount = rawCount,
                    CheckEvidenceCount = checkCount,
                    DoubtCount = doubtCount,
                    AnalyzeCount = ti?.AnalyzeCount ?? 0,
                    LastAnalyzedAt = ti?.LastAnalyzedAt,
                    AnalysisState = analysisState,
                    RerunState = rerunState,

                    Sort = d.Sort
                });
            }

            // ── 11. 关键词 / 审核员过滤（作用于汇总与分页，Status 除外）──
            var baseRows = rows.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(req.OwnerCode))
                baseRows = baseRows.Where(r => r.OwnerCode == req.OwnerCode);

            if (!string.IsNullOrWhiteSpace(req.Keyword))
            {
                var kw = req.Keyword.Trim();
                baseRows = baseRows.Where(r =>
                    Contains(r.EnterpriseName, kw) ||
                    Contains(r.PlanName, kw) ||
                    Contains(r.ClauseNumbers, kw) ||
                    Contains(r.ClauseTitles, kw) ||
                    Contains(r.DeptNames, kw) ||
                    Contains(r.OwnerName, kw));
            }

            var filtered = baseRows.ToList();

            // ── 12. 汇总（基于过滤后全量，不受 Status / 分页影响）──
            var isOnsite = stageKind == CertCheckEngineService.StageOnsite;
            var summary = new CertWorklistSummary
            {
                ItemTotal = filtered.Count,
                Waiting = filtered.Count(r => r.State == "waiting"),
                // ★ 已锁定 = 已分析 + 审核员确认，必须计入「已分析」：
                //   否则锁一个检查项，进度条反而从 100% 掉到 50%，看着像把工作作废了。
                Analyzed = filtered.Count(r => r.State == "analyzed" || r.IsLocked),
                Locked = filtered.Count(r => r.IsLocked),
                Failed = filtered.Count(r => r.State == "failed"),
                RerunSuggested = filtered.Count(r => r.RerunState == "suggested"),
                // 准备分支不签发 NC ⇒ 这个数字恒为 0，前端在准备页隐藏该统计
                NcCount = isOnsite ? filtered.Count(r => r.Conclusion == "nc") : 0,
                MissingCount = filtered.Count(r =>
                    r.PrepState == "missing" || r.PrepState == "partial"),
                DoubtCount = filtered.Count(r => r.DoubtCount > 0),
                RunningPlans = runningPlanTasks.Count
            };

            // ── 13. 状态过滤 ──
            var status = (req.Status ?? "all").Trim().ToLowerInvariant();
            IEnumerable<CertWorklistRow> afterStatus = status switch
            {
                "waiting" => filtered.Where(r => r.State == "waiting"),
                "analyzed" => filtered.Where(r => r.State == "analyzed"),
                "locked" => filtered.Where(r => r.IsLocked),
                "failed" => filtered.Where(r => r.State == "failed"),
                "rerun" => filtered.Where(r => r.RerunState == "suggested"),
                "nc" => filtered.Where(r => r.Conclusion == "nc"),
                "doubt" => filtered.Where(r => r.DoubtCount > 0),
                _ => filtered
            };

            var total = afterStatus.Count();
            var pagedRows = afterStatus
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Result<CertWorklistResponse>.Ok(new CertWorklistResponse
            {
                Rows = pagedRows,
                Total = total,
                PageIndex = pageIndex,
                PageSize = pageSize,
                Summary = summary,
                Plans = BuildPlanOptions(planList, details),
                RunningPlanTasks = runningPlanTasks,
                Message = $"{total} 个检查项"
            });
        }

        /// <summary>
        /// 分支实例列表 —— 认证准备 / 认证检查两个菜单的<b>列表页</b>数据源。
        ///
        /// <para><b>一行 = 一个「计划 × 分支」的执行实例</b>，对应原型的列：
        /// 企业 / 计划 / 阶段 / 认证标准 / 检查项 / 进度 / 状态 / 开启时间 / 操作。</para>
        ///
        /// <para><b>为什么不是「一行一个检查项」</b>：用户口径是「认证准备和认证检查从认证计划派生」，
        /// 派生出来的是一份<b>执行实例</b>，审核员先看到「这家企业这个阶段准备到哪一步了」，
        /// 点进去才是检查项清单。检查项级的表在详情页（<see cref="GetWorklistAsync"/> 按 planCode 过滤）。</para>
        ///
        /// <para><b>★ 状态从哪来</b>：<c>cert_task</c> 里该计划该分支<b>最新的一条</b>任务的 Status。
        /// 没有任务 ⇒ <c>waiting</c>（还没启动过）。这比在计划上再存一个状态字段可靠 ——
        /// 任务状态是队列实际写的，不会和现实脱节。</para>
        /// </summary>
        public async Task<Result<CertStageListResponse>> GetStageListAsync(
            string userCode, CertStageListRequest req)
        {
            var stageKind = (req.StageKind ?? CertCheckEngineService.StagePrep)
                .Trim().ToLowerInvariant();
            if (stageKind != CertCheckEngineService.StagePrep
                && stageKind != CertCheckEngineService.StageOnsite)
                stageKind = CertCheckEngineService.StagePrep;

            var pageIndex = req.PageIndex <= 0 ? 1 : req.PageIndex;
            var pageSize = req.PageSize <= 0 ? 20 : Math.Min(req.PageSize, 200);

            // ── 1. 计划全量 ──
            var plans = (await _db.GetListAsync<CertAuditPlan>(p => !p.IsDeleted && p.IsValid == 1)).Data
                ?? new List<CertAuditPlan>();
            if (plans.Count == 0)
                return Result<CertStageListResponse>.Ok(new CertStageListResponse
                {
                    PageIndex = pageIndex, PageSize = pageSize, Message = "还没有认证计划"
                });

            var planCodes = plans.Select(p => p.Code).ToList();

            // ── 2. 检查项（每个计划多少个）──
            var details = (await _db.GetListAsync<CertAuditPlanDetail>(d =>
                planCodes.Contains(d.PlanCode) && !d.IsDeleted && d.IsValid == 1)).Data
                ?? new List<CertAuditPlanDetail>();
            var detailCodes = details.Select(d => d.Code).ToList();

            // ── 3. 任务（最新一条定状态 + 轮次）──
            var tasks = (await _db.GetListAsync<CertTask>(t =>
                planCodes.Contains(t.PlanCode) && !t.IsDeleted && t.TaskKind == stageKind)).Data
                ?? new List<CertTask>();

            var latestTaskByPlan = tasks
                .GroupBy(t => t.PlanCode)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(t => t.CreateTime).First());
            var runRoundByPlan = tasks
                .GroupBy(t => t.PlanCode)
                .ToDictionary(g => g.Key, g => g.Count());

            // ── 4. 当前结论（按分支）──
            var currentAnalyses = detailCodes.Count == 0
                ? new List<CertCheckAnalysis>()
                : (await _db.GetListAsync<CertCheckAnalysis>(a =>
                    detailCodes.Contains(a.CheckItemCode) && a.IsCurrent == 1 && !a.IsDeleted)).Data
                  ?? new List<CertCheckAnalysis>();

            var analysisCodes = currentAnalyses.Select(a => a.Code).ToList();
            var results = analysisCodes.Count == 0
                ? new List<CertCheckResult>()
                : (await _db.GetListAsync<CertCheckResult>(r =>
                    analysisCodes.Contains(r.AnalysisCode) && !r.IsDeleted)).Data
                  ?? new List<CertCheckResult>();

            // 只保留本分支，且一个检查项只算一次
            var stageResults = results
                .Where(r => r.StageKind == stageKind)
                .GroupBy(r => r.CheckItemCode)
                .ToDictionary(g => g.Key, g => g.OrderBy(r => r.ClauseNumber).First());

            // ── 5. 锁 ──
            var lockRows = detailCodes.Count == 0
                ? new List<CertCheckLock>()
                : (await _db.GetListAsync<CertCheckLock>(l =>
                    detailCodes.Contains(l.CheckItemCode) && l.IsActive && !l.IsDeleted)).Data
                  ?? new List<CertCheckLock>();
            var lockedItems = lockRows.Select(l => l.CheckItemCode).Distinct()
                .ToHashSet(StringComparer.Ordinal);

            // ── 6. 企业 / 阶段 / 标准（走 ICoreResourceQuery，铁律 L5）──
            var entCodes = plans.Select(p => p.EnterpriseCode).Distinct().ToList();
            var enterpriseMap = (await _core.GetEnterprisesByCodesAsync(entCodes))
                .GroupBy(e => e.Code).ToDictionary(g => g.Key, g => g.First());

            var stgCodes = plans.Select(p => p.StageCode)
                .Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
            var stageMap = (await _core.GetStagesByCodesAsync(stgCodes!))
                .GroupBy(s => s.Code).ToDictionary(g => g.Key, g => g.First());

            var stdCodes = plans
                .SelectMany(p => (p.Standards ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
                .Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s))
                .Distinct().ToList();
            var stdMap = (await _core.GetStandardsByCodesAsync(stdCodes))
                .GroupBy(s => s.Code).ToDictionary(g => g.Key, g => g.First());

            // ── 7. 组装实例行 ──
            var detailsByPlan = details.GroupBy(d => d.PlanCode)
                .ToDictionary(g => g.Key, g => g.ToList());

            var rows = new List<CertStageInstanceRow>(plans.Count);

            foreach (var plan in plans)
            {
                var myDetails = detailsByPlan.GetValueOrDefault(plan.Code) ?? new List<CertAuditPlanDetail>();
                if (myDetails.Count == 0) continue;   // 空计划不进列表（没检查项就没法执行）

                latestTaskByPlan.TryGetValue(plan.Code, out var task);

                var done = 0; var ncCount = 0; var missing = 0; var locked = 0;
                foreach (var d in myDetails)
                {
                    if (stageResults.TryGetValue(d.Code, out var r))
                    {
                        done++;
                        var concl = CertVerdictMapper.ToConclusion(r.Conclusion);
                        if (concl == CertVerdictMapper.ConclusionNc) ncCount++;
                        if (r.PrepState == "missing" || r.PrepState == "partial") missing++;
                    }
                    if (lockedItems.Contains(d.Code)) locked++;
                }

                var state = task?.Status ?? "waiting";
                // ★ 状态语义修正（2026-11-02）：
                //   ① pending 有两种含义 —— 从没跑过 / 上一轮跑完了又新建了一轮待启动。
                //      后者 ItemDone 已经 > 0，列表上显示「待启动」会让人以为白跑了，
                //      所以按完成度归到 done / partial。
                //   ② done 但没跑满 ⇒ partial，让审核员一眼看出「还差几项」。
                //   ③ failed / cancelled 保持原样，让异常能被看见。
                if (state == "pending")
                    state = done == 0 ? "waiting"
                          : done >= myDetails.Count ? "done" : "partial";
                else if (state == "done" && done < myDetails.Count)
                    state = "partial";

                var owners = myDetails
                    .Select(d => d.OwnerName)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct()
                    .ToList();

                var stdNames = (plan.Standards ?? "")
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => stdMap.TryGetValue(s.Trim(), out var std) ? std.StandardName : null)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .ToList();

                rows.Add(new CertStageInstanceRow
                {
                    PlanCode = plan.Code,
                    PlanName = plan.Name,
                    EnterpriseCode = plan.EnterpriseCode,
                    EnterpriseName = enterpriseMap.TryGetValue(plan.EnterpriseCode, out var ent)
                        ? ent.Name : null,
                    StageName = stageMap.TryGetValue(plan.StageCode ?? "", out var stg)
                        ? stg.StageName : null,
                    Standards = stdNames.Count > 0 ? string.Join(",", stdNames) : null,
                    StageKind = stageKind,

                    ItemTotal = myDetails.Count,
                    ItemDone = done,
                    ItemLocked = locked,
                    ItemNc = stageKind == CertCheckEngineService.StageOnsite ? ncCount : 0,
                    ItemMissing = stageKind == CertCheckEngineService.StagePrep ? missing : 0,
                    ItemDoubt = ncCount + missing,

                    State = state,
                    TaskCode = task?.Code,
                    TaskNo = task?.TaskNo,
                    // ★ StartedAt 兜底 CreateTime：早期任务行没写 StartedAt（历史数据），
                    //   直接给 null 会让列表「开启时间」整列空白，看不出这轮是什么时候开的。
                    StartedAt = task?.StartedAt ?? task?.CreateTime,
                    FinishedAt = task?.FinishedAt,
                    RunRound = runRoundByPlan.GetValueOrDefault(plan.Code),

                    OwnerNames = owners.Count > 0 ? string.Join("、", owners) : null,
                    TaskRunning = task?.Status == "running"
                });
            }

            // ── 8. 关键词 / 状态过滤 ──
            var filtered = rows.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(req.Keyword))
            {
                var kw = req.Keyword.Trim();
                filtered = filtered.Where(r =>
                    Contains(r.EnterpriseName, kw)
                    || Contains(r.PlanName, kw)
                    || Contains(r.Standards, kw)
                    || Contains(r.StageName, kw));
            }
            var baseList = filtered.ToList();

            var summary = new CertStageListSummary
            {
                InstanceTotal = baseList.Count,
                Running = baseList.Count(r => r.State == "running"),
                Done = baseList.Count(r => r.State == "done"),
                Paused = baseList.Count(r => r.State == "paused"),
                ItemTotal = baseList.Sum(r => r.ItemTotal),
                NcTotal = baseList.Sum(r => r.ItemNc),
                LockedTotal = baseList.Sum(r => r.ItemLocked)
            };

            var status = (req.Status ?? "all").Trim().ToLowerInvariant();
            IEnumerable<CertStageInstanceRow> afterStatus = status switch
            {
                "waiting" => baseList.Where(r => r.State == "waiting"),
                "running" => baseList.Where(r => r.State == "running"),
                "paused" => baseList.Where(r => r.State == "paused"),
                "done" => baseList.Where(r => r.State == "done"),
                "partial" => baseList.Where(r => r.State == "partial"),
                "failed" => baseList.Where(r => r.State == "failed"),
                _ => baseList
            };

            var ordered = afterStatus.OrderByDescending(r => r.StartedAt ?? DateTime.MinValue).ToList();
            var total = ordered.Count;
            var paged = ordered.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToList();

            return Result<CertStageListResponse>.Ok(new CertStageListResponse
            {
                Rows = paged,
                Total = total,
                PageIndex = pageIndex,
                PageSize = pageSize,
                Summary = summary,
                Message = $"{total} 份执行实例"
            });
        }

        /// <summary>
        /// 设置检查项负责审核员。
        /// <para><b>★ 只有准备阶段可改</b>（用户 2026-11-01）：审核员分工是入场计划的核心信息，
        /// 由审核组长在准备期定死；一旦该检查项已经有<b>检查分支（onsite）结论</b>，
        /// 说明已经进场执行过，此时改分工会让入场计划与实际执行对不上 —— 直接拒绝。</para>
        /// </summary>
        public async Task<Result> SetCheckItemOwnerAsync(
            string userCode, string detailCode, string? ownerCode, string? ownerName)
        {
            if (string.IsNullOrWhiteSpace(detailCode))
                return Result.Fail("检查项 Code 不能为空");

            var detailResult = await _db.GetOneAsync<CertAuditPlanDetail>(d =>
                d.Code == detailCode && !d.IsDeleted);
            if (detailResult.Data == null)
                return Result.Fail("检查项不存在");
            var detail = detailResult.Data;

            // 是否已进入检查阶段：存在该检查项的 onsite 结论行 ⇒ 已执行过，锁定分工
            var onsiteResults = (await _db.GetListAsync<CertCheckResult>(r =>
                r.CheckItemCode == detailCode
                && r.StageKind == CertCheckEngineService.StageOnsite
                && !r.IsDeleted)).Data ?? new List<CertCheckResult>();

            if (onsiteResults.Count > 0)
                return Result.Fail("该检查项已进入认证检查阶段，审核员不可再变更");

            detail.OwnerCode = string.IsNullOrWhiteSpace(ownerCode) ? null : ownerCode.Trim();
            detail.OwnerName = string.IsNullOrWhiteSpace(ownerName) ? null : ownerName.Trim();

            // ★ 审核员必须来自专家组成员池（用户 2026-11-01：「入场计划指派审核员时从本池子挑选」）。
            //   前端下拉已经限定，这里做二次校验 —— 防止直接调接口塞进来一个不存在的姓名，
            //   那样入场计划按审核员分组时就查不到人。
            if (detail.OwnerCode != null)
            {
                var member = (await _db.GetListAsync<CertAuditMember>(m =>
                    m.Code == detail.OwnerCode && !m.IsDeleted)).Data?.FirstOrDefault();
                if (member == null)
                    return Result.Fail("审核员不在专家组成员池中，请先在「专家组设置」里维护");
                // 姓名以成员池为准，不采信前端传值
                detail.OwnerName = member.Name;
            }

            detail.UpdateBy = userCode;
            detail.UpdateTime = DateTime.Now;

            // ★ 列级更新，⛔ 不用单参 UpdateAsync(detail) ——
            //   全列写回会把并发下别人刚改的字段一起冲掉（陷阱 ㉑ / 守卫 R-A）。
            await _db.UpdateAsync(detail,
                nameof(CertAuditPlanDetail.OwnerCode),
                nameof(CertAuditPlanDetail.OwnerName),
                nameof(CertAuditPlanDetail.UpdateBy),
                nameof(CertAuditPlanDetail.UpdateTime));

            return Result.Ok();
        }

        /// <summary>
        /// 专家组成员池（供「设置负责审核员」下拉使用）。
        ///
        /// <para><b>为什么单独开一个接口</b>：<c>/api/Auditor/AuditMember/filter</c> 走
        /// <c>YzhControllerBase</c> 的工作区隔离，要求登录账号挂靠机构；而检查项归属的是
        /// <b>计划</b>，不是当前登录人，直接用会报「当前账号未挂靠任何机构」。</para>
        ///
        /// <para><b>★ 本阶段不做工作区收敛</b>（用户 2026-11-01 总原则：权限控制是下一步，
        /// 先让业务链路跑通）。等 <c>cert_audit_plan</c> 落 <c>OrgCode</c> 后，这里改成
        /// 按计划所属工作区过滤即可，调用方无需变动。</para>
        /// </summary>
        public async Task<Result<List<CertMemberOption>>> GetMemberOptionsAsync(string? keyword)
        {
            var rows = (await _db.GetListAsync<CertAuditMember>(m =>
                !m.IsDeleted && m.IsValid == 1)).Data ?? new List<CertAuditMember>();

            var kw = keyword?.Trim();
            var list = rows
                .Where(m => string.IsNullOrEmpty(kw)
                    || Contains(m.Name, kw)
                    || Contains(m.Title, kw)
                    || Contains(m.Profession, kw))
                .OrderBy(m => m.Sort)
                .ThenBy(m => m.Name)
                .Select(m => new CertMemberOption
                {
                    Code = m.Code ?? string.Empty,
                    Name = m.Name,
                    Title = m.Title,
                    Profession = m.Profession
                })
                .ToList();

            return Result<List<CertMemberOption>>.Ok(list);
        }

        /// <summary>按计划汇总检查项数，供清单页顶部的计划下拉使用</summary>
        private static List<CertWorklistPlanOption> BuildPlanOptions(
            List<CertAuditPlan> plans, List<CertAuditPlanDetail> details)
        {
            var countByPlan = details
                .GroupBy(d => d.PlanCode)
                .ToDictionary(g => g.Key, g => g.Count());

            return plans
                .OrderBy(p => p.Sort)
                .Select(p => new CertWorklistPlanOption
                {
                    PlanCode = p.Code,
                    PlanName = p.Name,
                    ItemCount = countByPlan.GetValueOrDefault(p.Code)
                })
                .ToList();
        }

        private static bool Contains(string? src, string kw)
            => !string.IsNullOrEmpty(src)
               && src.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0;

        private static string? Truncate(string? s, int max)
            => string.IsNullOrEmpty(s) || s.Length <= max ? s : s.Substring(0, max) + "…";

        // ================================================================
        // 辅助方法
        // ================================================================

        /// <summary>计算字符串的 MD5（32 位小写）</summary>
        public static string ComputeMd5(string content)
        {
            if (string.IsNullOrEmpty(content))
                return string.Empty;

            using var md5 = MD5.Create();
            var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(content));
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>生成证据指纹（所有证据行 MD5 拼接后的 MD5）</summary>
        public static string ComputeEvidenceFingerprint(List<(string code, string? md5, string status)> evidences)
        {
            var sb = new StringBuilder();
            foreach (var (code, md5, status) in
                     evidences.OrderBy(e => e.code))
            {
                sb.Append(code).Append(':').Append(md5 ?? "").Append(':').Append(status).Append('|');
            }
            return ComputeMd5(sb.ToString());
        }
    }
}

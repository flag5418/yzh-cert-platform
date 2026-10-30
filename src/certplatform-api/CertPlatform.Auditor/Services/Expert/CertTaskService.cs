using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CertPlatform.Auditor.Entities.Expert;
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
        public string CheckItemCode { get; set; } = string.Empty;
        public string? ClauseCode { get; set; }
        public string? StandardCode { get; set; }
        public string? ClauseNumber { get; set; }
        public string? Conclusion { get; set; }
        public bool? NcSuggested { get; set; }
        public string? Severity { get; set; }
        public string? Reason { get; set; }
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
        public string? Conclusion { get; set; }
        public string? Severity { get; set; }
        public bool? NcSuggested { get; set; }
        public int RawEvidenceCount { get; set; }
        public int CheckEvidenceCount { get; set; }
        public int DoubtCount { get; set; }
        public int SupplementCount { get; set; }
        public int Sort { get; set; }
    }

    /// <summary>计划检查项侧面详情</summary>
    public class CertPlanCheckItemDetail
    {
        public string DetailCode { get; set; } = string.Empty;
        public string? ClauseNumbers { get; set; }
        public string? DeptNames { get; set; }
        public string? DeptContactsJson { get; set; }
        public string? PrepState { get; set; }
        public string? Conclusion { get; set; }
        public string? Severity { get; set; }
        public string? Reason { get; set; }
        public bool? NcSuggested { get; set; }
        public bool IsLocked { get; set; }
        public string? LockedBy { get; set; }
        public DateTime? LockedAt { get; set; }
        public List<CertEvidenceRow> RawEvidences { get; set; } = new();
        public List<CertEvidenceRow> CheckEvidences { get; set; } = new();
        public string? DoubtsJson { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>计划统一视图聚合响应</summary>
    public class CertPlanChecklistResponse
    {
        public string PlanCode { get; set; } = string.Empty;
        public string? PlanName { get; set; }
        public string? EnterpriseName { get; set; }
        public string? StageName { get; set; }
        public string? TasksJson { get; set; }
        public List<CertPlanCheckItemRow> Rows { get; set; } = new();
        public string Message { get; set; } = string.Empty;
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
        private readonly ILogger<CertTaskService> _logger;

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public CertTaskService(IDbOrm db, ILogger<CertTaskService> logger)
        {
            _db = db;
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

        /// <summary>启动任务：pending → running，填充进度字段</summary>
        public async Task<Result<CertTaskStartResponse>> StartTaskAsync(string userCode, string taskCode)
        {
            var taskResult = await _db.GetOneAsync<CertTask>(t =>
                t.Code == taskCode && !t.IsDeleted);
            if (taskResult.Data == null)
                return Result<CertTaskStartResponse>.Fail("任务不存在");

            var task = taskResult.Data;
            if (task.Status != "pending" && task.Status != "paused")
                return Result<CertTaskStartResponse>.Fail($"当前状态 {task.Status} 不允许启动");

            task.Status = "running";
            task.StartedAt = DateTime.Now;
            task.UpdateBy = userCode;
            task.UpdateTime = DateTime.Now;

            await _db.UpdateAsync(task);

            return Result<CertTaskStartResponse>.Ok(new CertTaskStartResponse
            {
                Success = true,
                Message = "任务已启动"
            });
        }

        /// <summary>暂停任务：running → paused</summary>
        public async Task<Result<CertTaskStartResponse>> PauseTaskAsync(string userCode, string taskCode)
        {
            var taskResult = await _db.GetOneAsync<CertTask>(t =>
                t.Code == taskCode && !t.IsDeleted);
            if (taskResult.Data == null)
                return Result<CertTaskStartResponse>.Fail("任务不存在");

            var task = taskResult.Data;
            if (task.Status != "running")
                return Result<CertTaskStartResponse>.Fail($"当前状态 {task.Status} 不允许暂停");

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
        // 三、结论锁定
        // ================================================================

        /// <summary>锁定结论（审核员确认后调用，防止重跑覆盖）</summary>
        public async Task<Result> LockConclusionAsync(
            string userCode, string resultCode, string? lockReason = null)
        {
            var result = await _db.GetOneAsync<CertCheckResult>(r =>
                r.Code == resultCode && !r.IsDeleted);
            if (result.Data == null)
                return Result.Fail("结论记录不存在");

            var r = result.Data;
            r.IsLocked = true;
            r.LockedBy = userCode;
            r.LockedAt = DateTime.Now;
            r.LockReason = lockReason ?? "manual_confirm";
            r.UpdateBy = userCode;
            r.UpdateTime = DateTime.Now;

            await _db.UpdateAsync(r);
            return Result.Ok();
        }

        /// <summary>解锁结论（允许重新分析）</summary>
        public async Task<Result> UnlockConclusionAsync(string userCode, string resultCode)
        {
            var result = await _db.GetOneAsync<CertCheckResult>(r =>
                r.Code == resultCode && !r.IsDeleted);
            if (result.Data == null)
                return Result.Fail("结论记录不存在");

            var r = result.Data;
            r.IsLocked = false;
            r.LockedBy = null;
            r.LockedAt = null;
            r.LockReason = null;
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
                // 检查该任务项是否有锁定的结论
                var hasLocked = (await _db.GetListAsync<CertCheckResult>(r =>
                    r.CheckItemCode == change.CheckItemCode
                    && r.IsLocked == true
                    && !r.IsDeleted)).Data?.Count > 0;

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
            var checkItemCodes = itemList.Select(i => i.CheckItemCode).ToList();
            var resultList = new List<CertCheckResult>();
            if (checkItemCodes.Count > 0)
            {
                var resultsResult = await _db.GetListAsync<CertCheckResult>(r =>
                    checkItemCodes.Contains(r.CheckItemCode) && !r.IsDeleted);
                resultList = resultsResult.Data ?? new List<CertCheckResult>();
            }

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

            var resultRows = resultList.Select(r => new CertCheckResultRow
            {
                Code = r.Code,
                CheckItemCode = r.CheckItemCode,
                ClauseCode = r.ClauseCode,
                StandardCode = r.StandardCode,
                ClauseNumber = r.ClauseNumber,
                Conclusion = r.Conclusion,
                NcSuggested = r.NcSuggested,
                Severity = r.Severity,
                Reason = r.Reason,
                IsLocked = r.IsLocked,
                LockedBy = r.LockedBy,
                LockedAt = r.LockedAt,
                IsMandatory = r.IsMandatory,
                VerifyLevel = r.VerifyLevel
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
                    var resultsResult = await _db.GetListAsync<CertCheckResult>(r =>
                        checkItemCodes.Contains(r.CheckItemCode) && !r.IsDeleted);
                    resultList = resultsResult.Data ?? new List<CertCheckResult>();
                    // 同一 checkItemCode 取最近一条
                    resultList = resultList
                        .GroupBy(r => r.CheckItemCode)
                        .Select(g => g.OrderByDescending(r => r.CreateTime).First())
                        .ToList();
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
            var resultMap = resultList.ToDictionary(r => r.CheckItemCode);
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
                return BuildPlanCheckItemRow(d, evis, resultMap, deptList, membersByDept, taskList, itemList);
            }).ToList();

            var resp = new CertPlanChecklistResponse
            {
                PlanCode = planCode,
                PlanName = plan.Name,
                TasksJson = JsonSerializer.Serialize(taskList.Select(t => new
                {
                    t.Code,
                    t.TaskNo,
                    t.TaskKind,
                    t.Status
                })),
                Rows = rows,
                Message = $"{rows.Count} 个检查项"
            };

            return Result<CertPlanChecklistResponse>.Ok(resp);
        }

        private CertPlanCheckItemRow BuildPlanCheckItemRow(
            CertAuditPlanDetail d,
            List<CertEvidence> evidences,
            Dictionary<string, CertCheckResult> resultMap,
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

            // 最新结论
            resultMap.TryGetValue(d.Code ?? "", out var r);

            // 证据分组：原始(doc_slice/pq_result) vs 检查(manual/onsite_media/llm)
            var rawCount = evidences.Count(e =>
                e.EvidenceKind == "doc_slice" || e.EvidenceKind == "pq_result" ||
                e.CapturedIn == "prep" || e.CapturedIn == "doc" || e.CapturedIn == "pq");
            var checkCount = evidences.Count(e =>
                e.EvidenceKind == "manual" || e.EvidenceKind == "onsite_media" ||
                e.CapturedIn == "onsite" || e.CapturedIn == "manual");

            // 疑点数 = PrepNcHint 非空 + 结论中 Reason 中提及 NC
            var doubtCount = 0;
            if (r != null && !string.IsNullOrEmpty(r.PrepNcHint)) doubtCount++;
            if (r != null && r.NcSuggested == true) doubtCount++;

            return new CertPlanCheckItemRow
            {
                DetailCode = d.Code,
                ClauseNumbers = d.ClauseNumbers,
                DeptName = firstDept?.DeptName,
                DeptCode = firstDeptCode,
                ContactsJson = contactsArr.Count > 0 ? JsonSerializer.Serialize(contactsArr) : null,
                DeptNames = d.DeptNames,
                TasksJson = JsonSerializer.Serialize(detailTasks),
                PrepState = r?.PrepState,
                Conclusion = r?.Conclusion,
                Severity = r?.Severity,
                NcSuggested = r?.NcSuggested,
                RawEvidenceCount = rawCount,
                CheckEvidenceCount = checkCount,
                DoubtCount = doubtCount,
                SupplementCount = checkCount,
                Sort = d.Sort
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

            // 最新结论
            var resultResult = await _db.GetOneAsync<CertCheckResult>(r =>
                r.CheckItemCode == detail.Code && !r.IsDeleted);
            var r = resultResult.Data;

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

            // 疑点（来自 PrepNcHint + ConflictsJson）
            var doubts = new List<object>();
            if (r != null && !string.IsNullOrEmpty(r.PrepNcHint))
                doubts.Add(new { kind = "prep_nc_hint", text = r.PrepNcHint });
            if (r != null && !string.IsNullOrEmpty(r.ConflictsJson))
                doubts.Add(new { kind = "conflict", text = r.ConflictsJson });

            return Result<CertPlanCheckItemDetail>.Ok(new CertPlanCheckItemDetail
            {
                DetailCode = detail.Code,
                ClauseNumbers = detail.ClauseNumbers,
                DeptNames = detail.DeptNames,
                DeptContactsJson = deptContacts.Count > 0 ? JsonSerializer.Serialize(deptContacts) : null,
                PrepState = r?.PrepState,
                Conclusion = r?.Conclusion,
                Severity = r?.Severity,
                Reason = r?.Reason,
                NcSuggested = r?.NcSuggested,
                IsLocked = r?.IsLocked ?? false,
                LockedBy = r?.LockedBy,
                LockedAt = r?.LockedAt,
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

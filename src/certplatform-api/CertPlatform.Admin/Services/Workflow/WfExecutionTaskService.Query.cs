using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SqlSugar;
using CertPlatform.Admin.Services.Workflow.Models;
using CertPlatform.Shared.Entities.Wf;

namespace CertPlatform.Admin.Services.Workflow
{
    /// <summary>
    /// 工作流执行任务服务 —— 只读查询段（阶段四，2026-09-22）
    ///
    /// <para><b>为什么单独拆文件</b>：本类已有三个写入入口（整流 / 单节点 / AI 节点测试），
    /// 查询是另一类职责，且四层聚合的投影代码较长。按项目已有惯例用 partial 拆分，
    /// 避免 <c>WfExecutionTaskService.cs</c> 继续膨胀。</para>
    ///
    /// <para><b>只读保证</b>：本文件内不出现任何 <c>Insertable</c> / <c>Updateable</c> / <c>Deleteable</c>，
    /// 全部为 <c>Queryable</c>。测试历史是不可变事实，不提供编辑能力。</para>
    /// </summary>
    public partial class WfExecutionTaskService
    {
        /// <summary>
        /// 分页查询执行任务历史（四层模型的第一层）
        /// <para>对应端点 <c>POST /api/Workflow/test/history</c>。</para>
        /// <para>命名虽在 test 域下，但查询的是通用 <c>wf_execution_task</c>，
        /// 将来 NC_CHECK / REPORT_GENERATE 产生的任务同样可查（靠 <c>TaskType</c> 区分）。</para>
        /// </summary>
        public async Task<TaskHistoryPage> QueryHistoryAsync(TaskHistoryRequest request)
        {
            request ??= new TaskHistoryRequest();

            var page = request.Page > 0 ? request.Page : 1;
            var pageSize = request.PageSize > 0
                ? Math.Min(request.PageSize, TaskHistoryPage.MaxPageSize)
                : 20;

            var query = _db.Client.Queryable<WfExecutionTask>().Where(x => !x.IsDeleted);

            if (!string.IsNullOrWhiteSpace(request.RuleCode))
                query = query.Where(x => x.RuleCode == request.RuleCode);
            if (!string.IsNullOrWhiteSpace(request.TaskType))
                query = query.Where(x => x.TaskType == request.TaskType);
            if (!string.IsNullOrWhiteSpace(request.TestScope))
                query = query.Where(x => x.TestScope == request.TestScope);
            if (!string.IsNullOrWhiteSpace(request.TaskStatus))
                query = query.Where(x => x.TaskStatus == request.TaskStatus);
            if (request.StartTime.HasValue)
                query = query.Where(x => x.CreateTime >= request.StartTime.Value);
            if (request.EndTime.HasValue)
                query = query.Where(x => x.CreateTime <= request.EndTime.Value);

            var total = await query.CountAsync();

            var tasks = await query
                .OrderByDescending(x => x.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = tasks.Select(ToHistoryItem).ToList();

            // 补路径/节点条数：只为当前页的 TaskCode 各发一次分组统计，避免 N+1
            var codes = tasks
                .Select(t => t.Code)
                .Where(c => !string.IsNullOrEmpty(c))
                .Select(c => c!)
                .ToList();

            if (codes.Count > 0)
            {
                var pathCounts = await _db.Client.Queryable<WfPathExecution>()
                    .Where(p => codes.Contains(p.TaskCode) && !p.IsDeleted)
                    .GroupBy(p => p.TaskCode)
                    .Select(p => new TaskCountRow
                    {
                        TaskCode = p.TaskCode,
                        Cnt = SqlFunc.AggregateCount(p.Id)
                    })
                    .ToListAsync();

                var nodeCounts = await _db.Client.Queryable<WfNodeExecution>()
                    .Where(n => codes.Contains(n.TaskCode) && !n.IsDeleted)
                    .GroupBy(n => n.TaskCode)
                    .Select(n => new TaskCountRow
                    {
                        TaskCode = n.TaskCode,
                        Cnt = SqlFunc.AggregateCount(n.Id)
                    })
                    .ToListAsync();

                var pathMap = pathCounts.ToDictionary(r => r.TaskCode, r => r.Cnt);
                var nodeMap = nodeCounts.ToDictionary(r => r.TaskCode, r => r.Cnt);

                foreach (var item in items)
                {
                    item.PathCount = pathMap.GetValueOrDefault(item.TaskCode);
                    item.NodeCount = nodeMap.GetValueOrDefault(item.TaskCode);
                }
            }

            _logger.LogInformation(
                "[WorkflowEngine] 测试历史查询: page={Page}, pageSize={PageSize}, total={Total}, ruleCode={RuleCode}",
                page, pageSize, total, request.RuleCode ?? "(all)");

            return new TaskHistoryPage { Items = items, TotalCount = total };
        }

        /// <summary>
        /// 按 TaskCode 聚合四层详情（task + item + path + node）
        /// <para>对应端点 <c>GET /api/Workflow/test/detail/{taskCode}</c>。
        /// 一次请求拿齐，供前端「测试历史」抽屉直接渲染，避免前端串行发 4 次请求。</para>
        /// </summary>
        /// <returns>找不到任务时返回 <c>null</c>（调用方转 404）</returns>
        public async Task<TaskExecutionDetail?> GetExecutionDetailAsync(string taskCode)
        {
            if (string.IsNullOrWhiteSpace(taskCode)) return null;

            var task = await _db.Client.Queryable<WfExecutionTask>()
                .Where(x => x.Code == taskCode && !x.IsDeleted)
                .FirstAsync();

            if (task == null) return null;

            var detail = new TaskExecutionDetail { Task = ToHistoryItem(task) };

            var items = await _db.Client.Queryable<WfExecutionTaskItem>()
                .Where(x => x.TaskCode == taskCode && !x.IsDeleted)
                .OrderBy(x => x.Id)
                .ToListAsync();

            detail.Items = items.Select(i => new TaskDetailItem
            {
                ItemCode = i.Code ?? "",
                RuleCode = i.RuleCode,
                ItemType = i.ItemType,
                ItemStatus = i.ItemStatus,
                IsSuccess = i.IsSuccess,
                ErrorMessage = i.ErrorMessage,
                DurationMs = i.DurationMs,
                StartedAt = i.StartedAt,
                CompletedAt = i.CompletedAt
            }).ToList();

            // 路径按 PathIndex 升序 —— 与执行顺序一致
            var paths = await _db.Client.Queryable<WfPathExecution>()
                .Where(p => p.TaskCode == taskCode && !p.IsDeleted)
                .OrderBy(p => p.PathIndex)
                .ToListAsync();

            detail.Paths = paths.Select(p => new TaskPathDetail
            {
                PathIndex = p.PathIndex,
                Status = p.Status,
                ReusedCount = p.ReusedCount,
                NodeIds = DeserializeStringList(p.NodeIds),
                FailedAtNodeId = p.FailedAtNodeId,
                ErrorMessage = p.ErrorMessage,
                Output = DeserializeJson(p.OutputJson),
                DurationMs = p.DurationMs,
                StartedAt = p.StartedAt,
                CompletedAt = p.CompletedAt
            }).ToList();

            // 节点按 (StartedAt, Id) 升序 —— 秒级精度下同秒内靠 Id 兜底还原顺序
            var nodes = await _db.Client.Queryable<WfNodeExecution>()
                .Where(n => n.TaskCode == taskCode && !n.IsDeleted)
                .OrderBy(n => n.StartedAt)
                .OrderBy(n => n.Id)
                .ToListAsync();

            detail.Nodes = nodes.Select(n => new TaskNodeDetail
            {
                NodeId = n.NodeId,
                NodeType = n.NodeType,
                NodeTitle = n.NodeTitle,
                SkillCode = n.SkillCode,
                ExecStatus = n.ExecStatus,
                Output = DeserializeJson(n.OutputJson),
                ErrorMessage = n.ErrorMessage,
                StartedAt = n.StartedAt,
                CompletedAt = n.CompletedAt,
                ExecutionTimeMs = n.ExecutionTimeMs,
                PromptTokens = n.PromptTokens,
                CompletionTokens = n.CompletionTokens,
                LlmDurationMs = n.LlmDurationMs,
                IsReused = n.IsReused
            }).ToList();

            detail.Task.PathCount = detail.Paths.Count;
            detail.Task.NodeCount = detail.Nodes.Count;

            _logger.LogInformation(
                "[WorkflowEngine] 测试详情查询: taskCode={TaskCode}, items={Items}, paths={Paths}, nodes={Nodes}",
                taskCode, detail.Items.Count, detail.Paths.Count, detail.Nodes.Count);

            return detail;
        }

        // ════════════════════════════════════════════════════════════
        // 内部辅助
        // ════════════════════════════════════════════════════════════

        /// <summary>分组统计的行载体（SqlSugar 分组投影用，非对外契约）</summary>
        private class TaskCountRow
        {
            public string TaskCode { get; set; } = "";
            public int Cnt { get; set; }
        }

        /// <summary>任务实体 → 历史列表行</summary>
        private static TaskHistoryItem ToHistoryItem(WfExecutionTask t) => new()
        {
            TaskCode = t.Code ?? "",
            TaskType = t.TaskType,
            TestScope = t.TestScope,
            TaskStatus = t.TaskStatus,
            RuleCode = t.RuleCode,
            EnterpriseCode = t.EnterpriseCode,
            PhaseCode = t.PhaseCode,
            DurationMs = t.DurationMs,
            StartedAt = t.StartedAt,
            CompletedAt = t.CompletedAt,
            ErrorMessage = t.ErrorMessage,
            CreateTime = t.CreateTime
        };

        /// <summary>JSON 数组字符串 → List&lt;string&gt;（非法/空值返回空列表，不抛）</summary>
        private static List<string> DeserializeStringList(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<string>();
            try
            {
                return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            }
            catch
            {
                // 历史脏数据不应让详情接口整体失败
                return new List<string>();
            }
        }

        /// <summary>JSON 字符串 → JsonElement（非法/空值返回 null，不抛）</summary>
        private static JsonElement? DeserializeJson(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                using var doc = JsonDocument.Parse(json);
                // 必须 Clone：JsonDocument 释放后其 RootElement 不可用
                return doc.RootElement.Clone();
            }
            catch
            {
                return null;
            }
        }
    }
}

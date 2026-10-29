using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CertPlatform.Auditor.Entities.Dir;
using CertPlatform.Auditor.Entities.Expert;
using CertPlatform.Shared.Domain.Organization;
using CertPlatform.Shared.Domain.Scope;
using CertPlatform.Shared.Entities.Cert;
using CertPlatform.Shared.Storage;
using Microsoft.Extensions.Logging;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.DataBase.Services;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Queue;
using YZH.Core.Stand.Models.Result;

namespace CertPlatform.Auditor.Services.Ent
{
    /// <summary>
    /// 企业文档服务（专家端 <c>/resources</c> 重写版，36 号 §四）。
    ///
    /// <para><b>★★★ 一句话定位</b>：<b>按企业管理企业交上来的原始文件</b>（唯一一层），
    /// <b>按标准提取条款信息</b>（文档 × 标准）。</para>
    ///
    /// <para><b>与老 <see cref="EnterpriseOriginalService"/> 的四个本质差异</b>：</para>
    /// <list type="number">
    ///   <item><b>去阶段</b>：一阶段与二阶段的企业资料<b>是同一批文件</b>，按阶段管理必然错位
    ///         （实测：老表 237 份文件全挂 S2，唯一计划挂 AP）。⇒ 本服务<b>任何入参都不含 StageCode</b>。</item>
    ///   <item><b>去标准（上传期）</b>：上传时<b>不问</b>「这份文件属于哪个标准」—— 标准由
    ///         <c>cert_audit_plan.Standards</c> 给出，只在提取阶段出现。同一份《质量手册》对
    ///         iso9001 与 iso13485 各提取一次，<b>字节只存一份</b>。</item>
    ///   <item><b>去槽位/去分析</b>：不再有「槽位草稿」「标签画像」；<c>doc_group</c>/<c>doc_content</c>
    ///         的角色由下游 <c>cert_document_extraction</c>（按标准提取条款）取代。</item>
    ///   <item><b>标准来源唯一</b>：<see cref="ResolvePlanStandardCodesAsync"/> 读
    ///         <c>cert_audit_plan</c>（一企业一有效计划，见 <c>AuditPlanController</c>）。
    ///         ⛔ 不再从「审核员注册的机构」反推标准 —— 那是「机构注册过什么」，
    ///         不是「这家企业要认证什么」。</item>
    /// </list>
    ///
    /// <para><b>三条硬约束（改代码前先读）</b>：</para>
    /// <list type="number">
    ///   <item><b>工作区守卫</b>：每个公开方法第一行调 <see cref="OwnershipErrorAsync"/>（一处收口，避免漏网）。</item>
    ///   <item><b>Id 零语义</b>：⛔ 定位·删除·更新·传参<b>只用 <c>Code</c></b>，永不用 <c>Id</c>（铁律四）。</item>
    ///   <item><b>判重 + 版本</b>：变更判定靠 <c>Sha256</c>；<c>(Ent,RelFolderPath,FileName)</c>
    ///         <b>不建联合唯一索引</b>（3 列超 InnoDB 3072 B 上限 ⇒ <c>ERROR 1071</c>）⇒ 判重是应用层两段式。</item>
    /// </list>
    /// </summary>
    public class EnterpriseDocService
    {
        private readonly IDbOrm _db;
        private readonly IObjectStorage _storage;
        private readonly QueueManager _queueManager;
        private readonly WorkspaceContextService _workspace;
        private readonly IUserContext _user;
        private readonly ILogger<EnterpriseDocService> _logger;

        /// <summary>核心资源检索唯一入口（铁律 L5）—— 企业/标准取数只走这里。</summary>
        private readonly ICoreResourceQuery _core;

        /// <summary>批次有效期（分钟）：init 后超时未 confirm 即作废，防悬空草稿行永久占位</summary>
        private const int TaskExpireMinutes = 30;

        /// <summary>单文件大小上限（字节）= 200 MB，与既有 upload/file 的 RequestSizeLimit 一致</summary>
        public const long MaxFileSizeBytes = 200L * 1024 * 1024;

        public EnterpriseDocService(
            IDbOrm db,
            IObjectStorage storage,
            QueueManager queueManager,
            WorkspaceContextService workspace,
            IUserContext user,
            ILogger<EnterpriseDocService> logger,
            ICoreResourceQuery core)
        {
            _db = db;
            _storage = storage;
            _queueManager = queueManager;
            _workspace = workspace;
            _user = user;
            _logger = logger;
            _core = core;
        }

        // ========================================================
        // 零、状态取值（前后端逐字一致；⛔ 转换状态只能取 convertStatus.ts 的 6 个值）
        // ========================================================

        /// <summary>转换 / Markdown 状态（对齐 <c>cert-share/src/utils/convertStatus.ts</c>）</summary>
        public static class ConvertStatus
        {
            public const string None = "none";
            public const string Pending = "pending";
            public const string Converting = "converting";
            public const string Completed = "completed";
            public const string Failed = "failed";
            public const string Unsupported = "unsupported";
        }

        /// <summary>
        /// 提取聚合状态（本文件对「计划内所有标准」的汇总）。
        /// <para>⚠️ 与 <c>cert_document_extraction.ExtractStatus</c>（单标准）是两层，不要混。</para>
        /// </summary>
        public static class ExtractAgg
        {
            /// <summary>没有可提取的 Markdown</summary>
            public const string None = "none";
            /// <summary>Markdown 就绪，等待提取</summary>
            public const string Pending = "pending";
            /// <summary>至少一个标准在跑</summary>
            public const string Running = "running";
            /// <summary>部分标准完成、部分未跑（新增标准后必现）</summary>
            public const string Partial = "partial";
            /// <summary>全部标准完成</summary>
            public const string Done = "done";
            /// <summary>有标准失败</summary>
            public const string Failed = "failed";
            /// <summary>无标准可跑（计划没标准）</summary>
            public const string Skipped = "skipped";
        }

        /// <summary>上传批次状态</summary>
        public static class TaskStatus
        {
            public const string Draft = "draft";
            public const string Uploading = "uploading";
            public const string Confirmed = "confirmed";
            public const string Cancelled = "cancelled";
            public const string Failed = "failed";
        }

        // ========================================================
        // 一、左树（企业一层，无阶段）
        // ========================================================

        /// <summary>
        /// ★ 左树：本工作区<b>企业</b>（<b>一层，无阶段</b>），每家企业带文件计数 + 当前有效计划的标准。
        ///
        /// <para><b>★ 为什么没有阶段节点</b>：一阶段与二阶段的资料是同一批文件（2026-10-29 用户裁定）。
        /// 老实现「企业 → 阶段」两层，导致同一份文件在两个阶段下重复上传、重复转换、提取结果分叉。</para>
        ///
        /// <para><b>★ 标准从计划取</b>：每家企业挂 <c>cert_audit_plan</c>（一企业一有效计划）里的
        /// <c>Standards</c> ⇒ 前端据此渲染标准 Tab。<b>没有有效计划</b>的企业如实标注
        /// <c>HasPlan=false</c>，前端提示「请先创建认证计划」，⛔ 不静默当成「没有标准」。</para>
        /// </summary>
        public async Task<object> EnterpriseTreeAsync()
        {
            var ws = _workspace.Resolve(_user.UserCode);
            if (!ws.Success || ws.Data == null)
                return new { Configured = false, Message = ws.Error ?? "无法定位当前工作区", Nodes = new object[0] };

            // 排序口径收口 OrgStandardTreeAssembler（Sort → EnterpriseNo，⛔ 不用中文 Name 字符序）
            var enterprises = OrgStandardTreeAssembler.OrderEnterprises(
                await _core.GetEnterprisesAsync(ws.Data.Code),
                e => e.Sort, e => e.EnterpriseNo);

            // 一次取回全部计划 + 全部文件计数（避免逐企业 N+1）
            var allPlans = await _db.Client.Queryable<CertAuditPlan>()
                .Where(x => x.IsValid == 1 && !x.IsDeleted)
                .ToListAsync() ?? new List<CertAuditPlan>();
            var planByEnt = allPlans
                .GroupBy(p => p.EnterpriseCode ?? "", StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.OrderBy(p => p.Sort).ThenBy(p => p.CreateTime).First(),
                    StringComparer.Ordinal);

            var entCodes = new HashSet<string>(
                enterprises.Select(e => e.Code ?? "").Where(c => c.Length > 0),
                StringComparer.Ordinal);
            var allFiles = entCodes.Count == 0
                ? new List<EnterpriseDoc>()
                : (await _db.Client.Queryable<EnterpriseDoc>()
                    .Where(x => !x.IsDeleted && x.IsValid == 1)
                    .ToListAsync() ?? new List<EnterpriseDoc>())
                  .Where(f => entCodes.Contains(f.EnterpriseCode ?? ""))
                  .ToList();

            // 计划里出现过的标准一次性取回（名称 / 版本年）
            var allStdCodes = allPlans
                .SelectMany(p => SplitCodes(p.Standards))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            var stdRows = OrgStandardTreeAssembler.OrderStandards(
                await _core.GetStandardsByCodesAsync(allStdCodes),
                x => x.Sort, x => x.VersionYear, x => x.StandardCode);
            var stdByCode = stdRows
                .Where(s => !string.IsNullOrWhiteSpace(s.Code))
                .GroupBy(s => s.Code!, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

            var nodes = new List<object>();
            foreach (var ent in enterprises)
            {
                var code = ent.Code ?? "";
                var files = allFiles.Where(f => f.EnterpriseCode == code).ToList();
                planByEnt.TryGetValue(code, out var plan);

                var stdCodes = SplitCodes(plan?.Standards);
                var standards = stdCodes
                    .Select(c => stdByCode.TryGetValue(c, out var s)
                        ? new { s.Code, s.StandardCode, s.StandardName, s.VersionYear }
                        : new { Code = c, StandardCode = c, StandardName = c, VersionYear = 0 })
                    .ToList();

                nodes.Add(new
                {
                    // ★ 双写 Code / Name：YzhTree 默认 labelField 是 'Name'，
                    //   双写后即使前端忘传 label-field 也不会「只有图标没有文字」。
                    Code = code,
                    Label = ent.Name,
                    Name = ent.Name,
                    EnterpriseCode = code,
                    IsEnterpriseNode = true,
                    FileCount = files.Count,
                    ConvertedCount = files.Count(f => f.MarkdownStatus == ConvertStatus.Completed),
                    ConvertingCount = files.Count(f => f.ConvertStatus == ConvertStatus.Converting
                                                     || f.MarkdownStatus == ConvertStatus.Converting),
                    FailedCount = files.Count(f => f.ConvertStatus == ConvertStatus.Failed
                                                || f.MarkdownStatus == ConvertStatus.Failed),
                    ExtractDoneCount = files.Count(f => f.ExtractStatus == ExtractAgg.Done),
                    // ★ 计划信息（标准来源）：没有有效计划的企业如实标注，⛔ 不静默当「无标准」
                    HasPlan = plan != null,
                    PlanCode = plan?.Code,
                    PlanName = plan?.Name,
                    Standards = standards,
                    // 子节点留空数组而不是 null：前端 el-tree 对 null 与 [] 处理不同
                    Children = Array.Empty<object>(),
                });
            }

            return new { Configured = true, Message = (string?)null, Nodes = nodes };
        }

        /// <summary>
        /// 该企业的文档列表（<b>无阶段</b>），含提取聚合状态 + 按标准过滤。
        /// </summary>
        /// <param name="standardCode">
        /// 空 = 返回全部文件（不过滤）。
        /// <para>非空 = 只返回<b>该标准下有提取产物</b>的文件（用于「按标准看资料覆盖」视图）。
        /// ⚠️ 注意这与「文件属于哪个标准」不同 —— 文件本身不属任何标准，
        /// 这里只是「该标准下有没有提取到东西」的筛子。</para>
        /// </param>
        public async Task<object> ListAsync(string enterpriseCode, string? standardCode = null,
            string? keyword = null, bool onlyFailed = false)
        {
            var err = await OwnershipErrorAsync(enterpriseCode);
            if (err != null) return new { Success = false, Message = err, Rows = new object[0] };

            var rows = await _db.Client.Queryable<EnterpriseDoc>()
                .Where(x => x.EnterpriseCode == enterpriseCode && !x.IsDeleted && x.IsValid == 1)
                .ToListAsync() ?? new List<EnterpriseDoc>();

            // 提取产物（按文件聚合；指定标准时只取该标准那几行）
            var std = (standardCode ?? "").Trim();
            var exts = await _db.Client.Queryable<DocumentExtraction>()
                .Where(x => x.EnterpriseCode == enterpriseCode && !x.IsDeleted && x.IsValid == 1)
                .ToListAsync() ?? new List<DocumentExtraction>();
            if (std.Length > 0)
                exts = exts.Where(x => x.StandardCode == std).ToList();

            var extByFile = exts
                .GroupBy(x => x.FileCode ?? "", StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

            var plan = await GetActivePlanAsync(enterpriseCode);
            var planStdCodes = SplitCodes(plan?.Standards);

            IEnumerable<EnterpriseDoc> query = rows;
            if (std.Length > 0)
                query = query.Where(r => extByFile.ContainsKey(r.Code ?? ""));
            if (onlyFailed)
                query = query.Where(r => r.ConvertStatus == ConvertStatus.Failed
                                      || r.MarkdownStatus == ConvertStatus.Failed
                                      || r.ExtractStatus == ExtractAgg.Failed);
            var kw = (keyword ?? "").Trim();
            if (kw.Length > 0)
                query = query.Where(r => (r.FileName ?? "").Contains(kw, StringComparison.OrdinalIgnoreCase)
                                      || (r.RelFolderPath ?? "").Contains(kw, StringComparison.OrdinalIgnoreCase));

            var list = query
                .OrderBy(r => r.RelFolderPath, StringComparer.Ordinal)
                .ThenBy(r => r.FileName, StringComparer.Ordinal)
                .Select(r =>
                {
                    var mine = extByFile.TryGetValue(r.Code ?? "", out var l) ? l : new List<DocumentExtraction>();
                    return (object)new
                    {
                        r.Code,
                        r.EnterpriseCode,
                        r.RelFolderPath,
                        r.FileName,
                        r.FileType,
                        r.FileSize,
                        r.Sha256,
                        r.StoragePath,
                        r.VersionNumber,
                        r.ConvertStatus,
                        r.ConvertMessage,
                        r.MarkdownStatus,
                        r.MarkdownMessage,
                        r.PreviewPdfPath,
                        r.MarkdownPath,
                        r.ConvertDate,
                        r.ExtractStatus,
                        r.ExtractMessage,
                        r.ExtractTime,
                        // ★ 提取明细（每个标准一行）：前端抽屉直接渲染，⛔ 不另开一次请求
                        //   ⚠️ 标准名称由前端按树里的标准列表映射（后端不重复查一次标准表）
                        Extractions = mine.Select(x => new
                        {
                            x.Code,
                            x.StandardCode,
                            x.ClauseNumbers,
                            x.ClauseCount,
                            x.ExtractStatus,
                            x.ExtractMessage,
                            x.ExtractedAt,
                            x.IsStale,
                            x.PromptCode,
                            x.PromptVersion,
                        }).ToList(),
                        // 计划内标准中「还没有提取产物」的那几个 ⇒ 前端显示「待提取」
                        MissingStandardCodes = planStdCodes
                            .Where(c => mine.All(x => x.StandardCode != c))
                            .ToList(),
                        // ★ 可提取 = Markdown 就绪（提取的唯一输入）；⛔ 不看 ConvertStatus
                        CanExtract = r.MarkdownStatus == ConvertStatus.Completed
                                     && !string.IsNullOrWhiteSpace(r.MarkdownPath),
                        CanPreview = !string.IsNullOrWhiteSpace(r.PreviewPdfPath),
                    };
                })
                .ToList();

            return new
            {
                Success = true,
                Message = (string?)null,
                Total = rows.Count,
                Rows = list,
                // 计划信息随列表下发：前端不必再单独请求一次
                Plan = plan == null ? null : new { plan.Code, plan.Name, plan.Standards, StandardCodes = planStdCodes },
            };
        }

        /// <summary>状态条：转换 / 提取 计数 + 该企业是否有运行中队列</summary>
        public async Task<object> StatusBarAsync(string enterpriseCode)
        {
            var err = await OwnershipErrorAsync(enterpriseCode);
            if (err != null) return new { Success = false, Message = err };

            var rows = await _db.Client.Queryable<EnterpriseDoc>()
                .Where(x => x.EnterpriseCode == enterpriseCode && !x.IsDeleted && x.IsValid == 1)
                .ToListAsync() ?? new List<EnterpriseDoc>();

            var scopeKey = ScopeKeyOf(enterpriseCode);

            // 忙碌判据 = 该 scope 下非终态队列
            var active = await _db.Client.Queryable<YzhQueue>()
                .Where(q => q.ScopeKey == scopeKey
                            && q.Status != "completed" && q.Status != "failed" && q.Status != "cancelled")
                .OrderByDescending(q => q.CreateTime)
                .ToListAsync() ?? new List<YzhQueue>();
            var running = active.FirstOrDefault();

            int queueTotal = 0, queueCompleted = 0;
            if (active.Count > 0)
            {
                var activeCodes = active.Select(q => q.QueueCode)
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .ToHashSet(StringComparer.Ordinal);

                // ⚠️ 全取 + 内存过滤（SqlSugar 对闭包集合 Contains 翻译不可靠，会静默返回空集）
                var taskScope = activeCodes.Count == 0
                    ? new List<YzhQueueTask>()
                    : await _db.Client.Queryable<YzhQueueTask>()
                        .OrderByDescending(t => t.CreateTime)
                        .Take(500)
                        .ToListAsync() ?? new List<YzhQueueTask>();
                var activeTasks = taskScope
                    .Where(t => t.QueueCode != null && activeCodes.Contains(t.QueueCode))
                    .ToList();

                var fileCodes = activeTasks
                    .Select(t => ExtractPayloadCode(t.Payload))
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Select(c => c!)
                    .ToHashSet(StringComparer.Ordinal);

                var inFlight = fileCodes.Count == 0
                    ? new List<EnterpriseDoc>()
                    : rows.Where(r => fileCodes.Contains(r.Code ?? "")).ToList();
                if (inFlight.Count > 0)
                {
                    queueTotal = inFlight.Count;
                    queueCompleted = inFlight.Count(IsFileFinished);
                }
                else
                {
                    queueTotal = active.Sum(a => a.TotalCount);
                    queueCompleted = active.Sum(a => a.CompletedCount);
                }
            }

            var plan = await GetActivePlanAsync(enterpriseCode);
            var planStdCount = SplitCodes(plan?.Standards).Count;

            return new
            {
                Success = true,
                Message = (string?)null,
                Total = rows.Count,
                ConvertingCount = rows.Count(r => r.ConvertStatus == ConvertStatus.Converting
                                               || r.MarkdownStatus == ConvertStatus.Converting),
                FailedCount = rows.Count(r => r.ConvertStatus == ConvertStatus.Failed
                                           || r.MarkdownStatus == ConvertStatus.Failed),
                UnsupportedCount = rows.Count(r => r.ConvertStatus == ConvertStatus.Unsupported
                                                || r.MarkdownStatus == ConvertStatus.Unsupported),
                // ★ Markdown 就绪 = 可提取（这是提取链的唯一入口条件）
                ExtractableCount = rows.Count(r => r.MarkdownStatus == ConvertStatus.Completed
                                                && !string.IsNullOrWhiteSpace(r.MarkdownPath)),
                ExtractDoneCount = rows.Count(r => r.ExtractStatus == ExtractAgg.Done),
                ExtractFailedCount = rows.Count(r => r.ExtractStatus == ExtractAgg.Failed),
                HasPlan = plan != null,
                PlanStandardCount = planStdCount,
                QueueCode = running?.QueueCode,
                QueueStatus = running?.Status,
                QueueType = running?.QueueType,
                QueueTotal = queueTotal,
                QueueCompleted = queueCompleted,
                QueueProgress = queueTotal > 0 ? (int)Math.Round(queueCompleted * 100d / queueTotal) : 0,
                IsBusy = active.Count > 0,
            };
        }

        /// <summary>文件是否已到终态（转换段收尾）——进度聚合的「完成」判据</summary>
        private static bool IsFileFinished(EnterpriseDoc f)
            => f.ConvertStatus is ConvertStatus.Completed or ConvertStatus.Failed or ConvertStatus.Unsupported
               && f.MarkdownStatus is ConvertStatus.Completed or ConvertStatus.Failed or ConvertStatus.Unsupported;

        // ========================================================
        // 二、五段式上传（无阶段、无标准）
        // ========================================================

        /// <summary>
        /// Step0 <b>plan</b>：预检（类型白名单 / 大小上限 / <c>Sha256</c> 幂等预估），纯计算不落库。
        ///
        /// <para><b>★ 入参没有 StageCode，也没有 StandardCode</b>（2026-10-29 用户裁定）：
        /// 上传时只回答「传什么文件」，不回答「属于哪个阶段/标准」。</para>
        /// </summary>
        public async Task<object> PlanUploadAsync(string enterpriseCode, IList<PlanItemDto> files)
        {
            var err = await OwnershipErrorAsync(enterpriseCode);
            if (err != null) return new { Success = false, Message = err, Rows = new object[0] };

            var existing = (await _db.GetListAsync<EnterpriseDoc>(
                x => x.EnterpriseCode == enterpriseCode && x.IsValid == 1)).Data
                ?? new List<EnterpriseDoc>();

            var rows = new List<object>();
            var filtered = 0;
            foreach (var f in files ?? new List<PlanItemDto>())
            {
                var name = SanitizeName(f.FileName);
                if (name.Length == 0) continue;

                // ★ 系统噪声（.DS_Store / Thumbs.db / ~$xx.doc / ._xx）⇒ 静默剔除，
                //   不进 Rows、不算 Blocked，只报计数（报「格式不支持」是错的：用户根本没打算传）
                if (UploadFilePolicy.IsSystemNoise(name)) { filtered++; continue; }

                var verdict = UploadFilePolicy.Check(name);
                var ext = verdict.Extension.Length > 0 ? verdict.Extension : Path.GetExtension(name).ToLowerInvariant();
                var folder = NormalizeFolder(f.RelFolderPath);
                var blocked = new List<string>();

                if (!verdict.Ok) blocked.Add(verdict.Message);
                if (f.FileSize > MaxFileSizeBytes)
                    blocked.Add($"超过单文件上限 {MaxFileSizeBytes / 1024 / 1024} MB");
                if (f.FileSize <= 0)
                    blocked.Add("文件内容为空");

                // ★ 判重：同路径已有行 ⇒ 比 hash。相同 = 同一文件 = 幂等跳过；不同 = 替换（版本 +1）
                var same = existing.FirstOrDefault(e =>
                    e.RelFolderPath == folder && e.FileName == name && !e.IsDeleted);

                var action = "create";
                if (same != null)
                {
                    if (!string.IsNullOrWhiteSpace(f.Sha256)
                        && string.Equals(same.Sha256, f.Sha256, StringComparison.OrdinalIgnoreCase))
                        action = "skip";      // 幂等：不产生新版本、不重跑转换与提取
                    else
                        action = "replace";   // 异 hash：归档旧版 + VersionNumber+1
                }

                rows.Add(new
                {
                    FileName = name,
                    RelFolderPath = folder,
                    FileType = ext,
                    FileSize = f.FileSize,
                    Sha256 = f.Sha256 ?? "",
                    Action = action,
                    ExistingCode = same?.Code,
                    ExistingVersion = same?.VersionNumber,
                    Blocked = blocked.Count > 0,
                    BlockReason = blocked.Count > 0 ? string.Join("；", blocked) : (string?)null,
                });
            }

            return new
            {
                Success = true,
                Message = (string?)null,
                Rows = rows,
                Summary = new
                {
                    Total = rows.Count,
                    CreateCount = rows.Count(r => ((dynamic)r).Action as string == "create"),
                    ReplaceCount = rows.Count(r => ((dynamic)r).Action as string == "replace"),
                    SkipCount = rows.Count(r => ((dynamic)r).Action as string == "skip"),
                    BlockedCount = rows.Count(r => ((dynamic)r).Blocked as bool? == true),
                    FilteredCount = filtered,
                    FilteredNames = filtered > 0
                        ? (files ?? new List<PlanItemDto>())
                            .Where(f => UploadFilePolicy.IsSystemNoise(SanitizeName(f.FileName)))
                            .Select(f => SanitizeName(f.FileName)).Distinct().Take(5).ToList()
                        : null,
                },
            };
        }

        /// <summary>
        /// Step1 <b>init</b>：建批次行 + 按判重两段式建/命中文件行，下发 <c>TaskId</c> 与建议存储路径。
        /// <para>⚠️ <b>不建草稿行</b>：行在 init 就建（<c>ConvertStatus='none'</c>），
        /// 字节到位后由 confirm 激活并入队。这样 cancel 只需删对象 + 撤未完成行。</para>
        /// </summary>
        public async Task<(string? Error, object? Data)> UploadInitAsync(string enterpriseCode, IList<PlanItemDto> files)
        {
            var err = await OwnershipErrorAsync(enterpriseCode);
            if (err != null) return (err, null);

            var now = DateTime.Now;
            var task = new EnterpriseDocUploadTask
            {
                Code = Guid.NewGuid().ToString("N"),
                CreateTime = now,
                CreateBy = _user.UserCode,
                EnterpriseCode = enterpriseCode,
                Status = TaskStatus.Draft,
                TotalFiles = files?.Count ?? 0,
                ExpireTime = now.AddMinutes(TaskExpireMinutes),
            };
            var insTask = await _db.InsertAsync(task);
            if (!insTask.Success)
                return ($"创建上传批次失败：{insTask.Error}", null);

            var orgCode = await ResolveOrgCodeAsync(enterpriseCode);
            var items = new List<object>();
            foreach (var f in files ?? new List<PlanItemDto>())
            {
                var name = SanitizeName(f.FileName);
                if (name.Length == 0) continue;

                // ★ 绕过 plan 直调 API 的兜底：系统噪声与白名单外一律不写库
                var verdict = UploadFilePolicy.Check(name);
                if (!verdict.Ok) continue;
                var ext = verdict.Extension;
                var folder = NormalizeFolder(f.RelFolderPath);

                var sha = (f.Sha256 ?? "").Trim().ToLowerInvariant();
                var existing = (await _db.Client.Queryable<EnterpriseDoc>()
                    .Where(x => x.EnterpriseCode == enterpriseCode
                                && x.RelFolderPath == folder && x.FileName == name)
                    .ToListAsync() ?? new List<EnterpriseDoc>())
                    .FirstOrDefault(e => !e.IsDeleted);

                if (existing != null && !string.IsNullOrEmpty(sha)
                    && !string.IsNullOrEmpty(existing.Sha256)
                    && string.Equals(existing.Sha256, sha, StringComparison.OrdinalIgnoreCase))
                {
                    // ★ 幂等：同 hash ⇒ 完全跳过，不建新行、不入队（省一次转换 + N 次提取）
                    task.SkipCount++;
                    items.Add(new { FileCode = existing.Code, FileName = name, Action = "skip", StoragePath = existing.StoragePath });
                    continue;
                }

                var storagePath = PathBuilder.EnterpriseDoc(enterpriseCode, folder, name);
                string code;

                if (existing != null)
                {
                    // ★ 替换：这里**只登记「待写入」**，真正归档 + 版本递增在 upload/file 收到字节时做。
                    // ⛔ 绝不能把前端声称的 sha 写进 Sha256 —— 它的语义是「**已落库字节**的指纹」，
                    //   而字节此刻还没到。写进去会让 upload/file 的「同 hash ⇒ 幂等跳过」在首传时命中，
                    //   结果文件永远传不上去（2026-10-03 实测踩到）。
                    existing.UploadTaskCode = task.Code;
                    existing.UpdateTime = now;
                    existing.UpdateBy = _user.UserCode;
                    await _db.UpdateAsync(existing,
                        nameof(EnterpriseDoc.UploadTaskCode),
                        nameof(EnterpriseDoc.UpdateTime),
                        nameof(EnterpriseDoc.UpdateBy));
                    code = existing.Code!;
                    var predict = sha.Length == 64 && string.Equals(existing.Sha256, sha, StringComparison.OrdinalIgnoreCase)
                        ? "skip" : "replace";
                    items.Add(new { FileCode = code, FileName = name, Action = predict, StoragePath = storagePath });
                }
                else
                {
                    var row = new EnterpriseDoc
                    {
                        Code = Guid.NewGuid().ToString("N"),
                        CreateTime = now,
                        CreateBy = _user.UserCode,
                        EnterpriseCode = enterpriseCode,
                        OrgCode = orgCode,
                        UploadTaskCode = task.Code,
                        RelFolderPath = folder,
                        FileName = name,
                        FileType = ext,
                        FileSize = f.FileSize,
                        // ⛔ Sha256 留空：字节未落库，指纹未知。upload/file 会按实际字节补上。
                        Sha256 = "",
                        StoragePath = storagePath,
                        VersionNumber = 1,
                        ConvertStatus = ConvertStatus.None,
                        MarkdownStatus = ConvertStatus.None,
                        ExtractStatus = ExtractAgg.None,
                        IsValid = 1,
                    };
                    var insRow = await _db.InsertAsync(row);
                    if (!insRow.Success)
                        return ($"保存文件「{name}」失败：{insRow.Error}", null);
                    code = row.Code!;
                    items.Add(new { FileCode = code, FileName = name, Action = "create", StoragePath = storagePath });
                }
            }

            task.TotalFiles = items.Count;
            task.Status = TaskStatus.Uploading;
            task.UpdateTime = now;
            task.UpdateBy = _user.UserCode;
            await _db.UpdateAsync(task,
                nameof(EnterpriseDocUploadTask.TotalFiles),
                nameof(EnterpriseDocUploadTask.SkipCount),
                nameof(EnterpriseDocUploadTask.Status),
                nameof(EnterpriseDocUploadTask.UpdateTime),
                nameof(EnterpriseDocUploadTask.UpdateBy));

            return (null, new
            {
                TaskId = task.Code,
                EnterpriseCode = enterpriseCode,
                Items = items,
                SkipCount = task.SkipCount,
            });
        }

        /// <summary>
        /// Step2 <b>file</b>：逐文件传字节。<b>按 DB 行重算路径</b>（前端不传路径，防伪造）。
        ///
        /// <para>★ 替换时序：<c>Rename(旧 → _archive/{名}.v{n})</c> → <c>Upload(原路径, 新字节)</c>。
        /// 这样 <c>StoragePath</c> 恒定，<b>预览 / 下载 / 提取的路径永不失效</b>。</para>
        /// </summary>
        public async Task<(bool Ok, string? Error, object? Data)> UploadFileStepAsync(
            string fileCode, string taskId, Stream stream, long size, string uploadFileName, string sha256)
        {
            var row = (await _db.GetOneIgnoreValidAsync<EnterpriseDoc>(x => x.Code == fileCode)).Data;
            if (row == null) return (false, "上传项不存在或已失效，请重新发起上传", null);

            var tenantErr = await OwnershipErrorAsync(row.EnterpriseCode);
            if (tenantErr != null) return (false, tenantErr, null);

            if (!string.IsNullOrEmpty(taskId)
                && !string.IsNullOrEmpty(row.UploadTaskCode)
                && !string.Equals(row.UploadTaskCode, taskId, StringComparison.Ordinal))
                return (false, "上传项不属于本批次，请重新发起上传", null);

            if (size > MaxFileSizeBytes)
                return (false, $"「{row.FileName}」超过单文件上限 {MaxFileSizeBytes / 1024 / 1024} MB", null);

            var name = row.FileName;
            if (!string.IsNullOrWhiteSpace(uploadFileName))
            {
                // ⚠️ multipart 的 filename 可能是**客户端本地全路径**（curl / 某些 SDK 会带），
                //   浏览器 FormData 只会给 file.name。先取末段再比对，否则会误判「改名」。
                var incoming = SanitizeName(Path.GetFileName((uploadFileName ?? "").Replace('\\', '/')));
                // ⛔ 换文件名 = 换逻辑文件（存储路径不同）⇒ 不允许在本阶段改名，避免行与对象脱节
                if (incoming.Length > 0 && !string.Equals(incoming, name, StringComparison.Ordinal))
                    return (false, $"上传文件名「{incoming}」与登记名「{name}」不一致，请重新发起上传", null);
            }

            // ★ 读进内存以便算 hash（判定的权威依据）
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            var bytes = ms.ToArray();
            if (bytes.Length == 0) return (false, "文件内容为空", null);

            var actualSha = sha256;
            if (actualSha.Length != 64) actualSha = Sha256Hex(bytes);

            // ★ 幂等复核：字节 hash 与**已落库版本**一致 ⇒ 不重复写、不产生新版本。
            // ⛔ 必须同时要求「已存 Sha256 非空」—— 空 = 首传还没写字节，此刻 hash 必然「相同」，
            //    判成 skip 就等于文件永远传不上去（2026-10-03 实测踩到）。
            if (!string.IsNullOrEmpty(row.Sha256)
                && string.Equals(row.Sha256, actualSha, StringComparison.OrdinalIgnoreCase))
            {
                return (true, null, new { FileCode = fileCode, FileName = name, Action = "skip", VersionNumber = row.VersionNumber });
            }

            var storagePath = row.StoragePath;
            if (string.IsNullOrEmpty(storagePath))
                storagePath = PathBuilder.EnterpriseDoc(row.EnterpriseCode, row.RelFolderPath, name);

            // ★ 异 hash ⇒ 先把当前活跃版归档，再向同一路径写新字节
            var archivedVersion = 0;
            var isReplace = row.VersionNumber >= 1 && !string.IsNullOrEmpty(row.Sha256);
            if (isReplace)
            {
                archivedVersion = await MaxArchivedVersionAsync(row.Code!);
                if (archivedVersion <= 0) archivedVersion = 1;   // 首版归档记 v1

                try
                {
                    if (await _storage.ExistsAsync(storagePath.TrimStart('/')))
                    {
                        var archivePath = PathBuilder.Archive(storagePath, archivedVersion);
                        await _storage.RenameAsync(storagePath.TrimStart('/'), archivePath.TrimStart('/'));

                        // 旧版产物随源归档（best-effort，失败不阻断）
                        foreach (var prod in new[] { row.PreviewPdfPath, row.MarkdownPath }
                                 .Where(p => !string.IsNullOrEmpty(p)))
                        {
                            try
                            {
                                if (await _storage.ExistsAsync(prod!.TrimStart('/')))
                                    await _storage.RenameAsync(prod.TrimStart('/'),
                                        PathBuilder.Archive(prod, archivedVersion).TrimStart('/'));
                            }
                            catch (Exception ex) { _logger.LogWarning(ex, "[Upload] 产物归档失败（不阻断）: {Path}", prod); }
                        }

                        var insVer = await _db.InsertAsync(new EnterpriseDocVersion
                        {
                            Code = Guid.NewGuid().ToString("N"),
                            CreateTime = DateTime.Now,
                            CreateBy = _user.UserCode,
                            FileCode = row.Code!,
                            EnterpriseCode = row.EnterpriseCode,
                            VersionNumber = archivedVersion,
                            FileName = row.FileName,
                            FileType = row.FileType,
                            FileSize = row.FileSize,
                            Sha256 = row.Sha256,
                            StoragePath = archivePath,
                            Reason = "同名异 hash 重传（版本归档）",
                            IsValid = 1,
                        });
                        if (!insVer.Success)
                            _logger.LogError("[Upload] 版本归档记录写入失败（不影响主流程）: {Code} {Err}",
                                row.Code, insVer.Error);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Upload] 归档失败，替换中止: {FileCode}", fileCode);
                    return (false, $"归档失败，上传中止：{ex.Message}", null);
                }
            }

            try
            {
                await _storage.UploadAsync(storagePath.TrimStart('/'), new MemoryStream(bytes), bytes.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Upload] 对象上传失败: {FileCode}", fileCode);
                return (false, $"上传失败：{ex.Message}", null);
            }

            row.StoragePath = storagePath;
            row.FileSize = bytes.Length;
            row.Sha256 = actualSha;
            if (isReplace) row.VersionNumber = archivedVersion + 1;
            row.ConvertStatus = ConvertStatus.Pending;
            row.MarkdownStatus = ConvertStatus.Pending;
            row.ConvertMessage = null;
            row.MarkdownMessage = null;
            row.ConvertDate = null;
            row.PreviewPdfPath = null;      // 旧产物已归档 ⇒ 清空，等重转
            row.MarkdownPath = null;
            row.ExtractStatus = ExtractAgg.None;
            row.ExtractMessage = null;
            row.UpdateTime = DateTime.Now;
            row.UpdateBy = _user.UserCode;
            await _db.UpdateAsync(row,
                nameof(EnterpriseDoc.StoragePath),
                nameof(EnterpriseDoc.FileSize),
                nameof(EnterpriseDoc.Sha256),
                nameof(EnterpriseDoc.VersionNumber),
                nameof(EnterpriseDoc.ConvertStatus),
                nameof(EnterpriseDoc.MarkdownStatus),
                nameof(EnterpriseDoc.ConvertMessage),
                nameof(EnterpriseDoc.MarkdownMessage),
                nameof(EnterpriseDoc.ConvertDate),
                nameof(EnterpriseDoc.PreviewPdfPath),
                nameof(EnterpriseDoc.MarkdownPath),
                nameof(EnterpriseDoc.ExtractStatus),
                nameof(EnterpriseDoc.ExtractMessage),
                nameof(EnterpriseDoc.UpdateTime),
                nameof(EnterpriseDoc.UpdateBy));

            // ★ 替换 ⇒ 旧提取产物全部标记过期（文档字节变了，提取结果不再可信）
            if (isReplace) await MarkExtractionStaleAsync(row.Code!);

            if (!string.IsNullOrEmpty(taskId))
            {
                var task = (await _db.GetOneIgnoreValidAsync<EnterpriseDocUploadTask>(x => x.Code == taskId)).Data;
                if (task != null)
                {
                    task.SuccessCount++;
                    if (isReplace) task.ReplaceCount++;
                    task.UpdateTime = DateTime.Now;
                    await _db.UpdateAsync(task,
                        nameof(EnterpriseDocUploadTask.SuccessCount),
                        nameof(EnterpriseDocUploadTask.ReplaceCount),
                        nameof(EnterpriseDocUploadTask.UpdateTime));
                }
            }

            return (true, null, new
            {
                FileCode = fileCode,
                FileName = name,
                Action = isReplace ? "replace" : "create",
                VersionNumber = row.VersionNumber,
                Sha256 = actualSha,
            });
        }

        /// <summary>
        /// Step3 <b>confirm</b>：激活 + 入 <c>enterprise_doc_convert</c> 队列。
        /// <para>入队失败<b>不阻断激活</b>（返回 <c>QueueError</c> 文案，前端可提示重试）。</para>
        /// </summary>
        public async Task<(string? Error, object? Data)> UploadConfirmAsync(string taskId, string enterpriseCode)
        {
            var err = await OwnershipErrorAsync(enterpriseCode);
            if (err != null) return (err, null);

            var task = (await _db.GetOneIgnoreValidAsync<EnterpriseDocUploadTask>(x => x.Code == taskId)).Data;
            if (task == null) return ("上传批次不存在或已失效", null);
            if (task.Status != TaskStatus.Uploading) return ($"上传批次状态为 {task.Status}，无法确认", null);
            if (task.ExpireTime.HasValue && task.ExpireTime.Value < DateTime.Now)
                return ("上传批次已过期，请重新发起上传", null);

            var rows = await _db.Client.Queryable<EnterpriseDoc>()
                .Where(x => x.UploadTaskCode == taskId && x.EnterpriseCode == enterpriseCode)
                .ToListAsync() ?? new List<EnterpriseDoc>();

            // 字节缺失的行（init 建了但 file 步没成功）不入队
            var activatable = rows
                .Where(r => !string.IsNullOrEmpty(r.StoragePath) && NeedsConversion(r.FileName))
                .ToList();

            var now = DateTime.Now;
            foreach (var row in rows)
            {
                row.IsValid = 1;
                // ⚠️ 不清空 UploadTaskCode：它是**上传批次的追溯列**（init/file/cancel/replace 都按它反查）
                row.UpdateTime = now;
                row.UpdateBy = _user.UserCode;
                await _db.UpdateAsync(row,
                    nameof(EnterpriseDoc.IsValid),
                    nameof(EnterpriseDoc.UpdateTime),
                    nameof(EnterpriseDoc.UpdateBy));
            }

            string? queueCode = null;
            string? queueError = null;
            if (activatable.Count > 0)
            {
                (queueCode, queueError) = await EnqueueConvertAsync(enterpriseCode, activatable, task.Code!);
            }

            task.Status = TaskStatus.Confirmed;
            task.UpdateTime = now;
            task.UpdateBy = _user.UserCode;
            await _db.UpdateAsync(task,
                nameof(EnterpriseDocUploadTask.Status),
                nameof(EnterpriseDocUploadTask.UpdateTime),
                nameof(EnterpriseDocUploadTask.UpdateBy));

            return (null, new
            {
                TaskId = taskId,
                ActivatedCount = rows.Count,
                ConvertCount = activatable.Count,
                SkipCount = task.SkipCount,
                ReplaceCount = task.ReplaceCount,
                QueueCode = queueCode,
                QueueError = queueError,
            });
        }

        /// <summary>Step4 <b>cancel</b>：取消队列 + 删对象 + 撤本批次的行</summary>
        public async Task<(string? Error, object? Data)> UploadCancelAsync(string taskId, string enterpriseCode)
        {
            var err = await OwnershipErrorAsync(enterpriseCode);
            if (err != null) return (err, null);

            var task = (await _db.GetOneIgnoreValidAsync<EnterpriseDocUploadTask>(x => x.Code == taskId)).Data;
            if (task == null) return ("上传批次不存在或已失效", null);
            if (task.Status == TaskStatus.Confirmed) return ($"上传批次已确认（{TaskStatus.Confirmed}），无法取消", null);

            await _queueManager.CancelBatchAsync(taskId);

            var rows = await _db.Client.Queryable<EnterpriseDoc>()
                .Where(x => x.UploadTaskCode == taskId && x.EnterpriseCode == enterpriseCode)
                .ToListAsync() ?? new List<EnterpriseDoc>();

            var deleted = 0;
            foreach (var row in rows)
            {
                try
                {
                    if (!string.IsNullOrEmpty(row.StoragePath))
                        await _storage.DeleteAsync(row.StoragePath.TrimStart('/'));
                }
                catch (Exception ex) { _logger.LogWarning(ex, "[Cancel] 对象删除失败: {Path}", row.StoragePath); }

                // ⛔ 已被替换过的行（VersionNumber > 1）不撤 —— 它的旧字节已归档，撤了会丢证据链
                if (row.VersionNumber <= 1)
                {
                    await _db.DeleteByCodeAsync<EnterpriseDoc>(row.Code!);
                    deleted++;
                }
                else
                {
                    row.UploadTaskCode = "";
                    await _db.UpdateAsync(row, nameof(EnterpriseDoc.UploadTaskCode));
                }
            }

            task.Status = TaskStatus.Cancelled;
            task.UpdateTime = DateTime.Now;
            task.UpdateBy = _user.UserCode;
            await _db.UpdateAsync(task,
                nameof(EnterpriseDocUploadTask.Status),
                nameof(EnterpriseDocUploadTask.UpdateTime),
                nameof(EnterpriseDocUploadTask.UpdateBy));

            return (null, new { TaskId = taskId, CancelledCount = deleted, SkippedKept = rows.Count - deleted });
        }

        // ========================================================
        // 三、删除 / 版本 / 重新转换
        // ========================================================

        /// <summary>
        /// 删除：软删行 + 删对象（源 + 产物 + 全部 <c>_archive/</c>）+ 软删提取产物。
        /// </summary>
        public async Task<Result<object?>> DeleteAsync(string fileCode, string enterpriseCode, string? reason)
        {
            if (string.IsNullOrWhiteSpace(fileCode)) return Result<object?>.Fail("文件业务键 Code 不能为空");

            var row = (await _db.GetOneIgnoreValidAsync<EnterpriseDoc>(x => x.Code == fileCode)).Data;
            if (row == null) return Result<object?>.Fail($"记录不存在：{fileCode}");
            if (!string.Equals(row.EnterpriseCode, enterpriseCode, StringComparison.Ordinal))
                return Result<object?>.Fail("记录不存在或不属于当前工作区");

            var tenantErr = await OwnershipErrorAsync(enterpriseCode);
            if (tenantErr != null) return Result<object?>.Fail(tenantErr);

            var segs = PathBuilder.Segments(row.StoragePath);
            if (segs.Length >= 2) await SafeDeleteAsync(row.StoragePath);
            if (!string.IsNullOrEmpty(row.PreviewPdfPath)) await SafeDeleteAsync(row.PreviewPdfPath);
            if (!string.IsNullOrEmpty(row.MarkdownPath)) await SafeDeleteAsync(row.MarkdownPath);

            // 历史版本：归档路径可由历史表确定性取回（MinIO 侧不可枚举）
            if (segs.Length >= 2)
            {
                var versions = await _db.Client.Queryable<EnterpriseDocVersion>()
                    .Where(x => x.FileCode == fileCode)
                    .ToListAsync() ?? new List<EnterpriseDocVersion>();
                foreach (var v in versions) await SafeDeleteAsync(v.StoragePath);
                foreach (var v in versions)
                {
                    v.IsDeleted = true;
                    v.DeleteBy = _user.UserCode;
                    v.DeleteTime = DateTime.Now;
                    await _db.UpdateAsync(v,
                        nameof(EnterpriseDocVersion.IsDeleted),
                        nameof(EnterpriseDocVersion.DeleteBy),
                        nameof(EnterpriseDocVersion.DeleteTime));
                }
            }

            // 提取产物：随文件一起软删（文件没了，提取结果无宿主）
            var exts = await _db.Client.Queryable<DocumentExtraction>()
                .Where(x => x.FileCode == fileCode && !x.IsDeleted)
                .ToListAsync() ?? new List<DocumentExtraction>();
            foreach (var e in exts)
            {
                e.IsDeleted = true;
                e.DeleteBy = _user.UserCode;
                e.DeleteTime = DateTime.Now;
                await _db.UpdateAsync(e,
                    nameof(DocumentExtraction.IsDeleted),
                    nameof(DocumentExtraction.DeleteBy),
                    nameof(DocumentExtraction.DeleteTime));
            }

            row.IsDeleted = true;
            row.DeleteBy = _user.UserCode;
            row.DeleteTime = DateTime.Now;
            row.Remark = Truncate(reason, 500);
            await _db.UpdateAsync(row,
                nameof(EnterpriseDoc.IsDeleted),
                nameof(EnterpriseDoc.DeleteBy),
                nameof(EnterpriseDoc.DeleteTime),
                nameof(EnterpriseDoc.Remark));

            return Result<object?>.Ok(new { FileCode = fileCode, DeletedExtractions = exts.Count });
        }

        /// <summary>批量删除：语义与 <see cref="DeleteAsync"/> 一致，逐个执行并汇总成败，⛔ 一份失败不中断其余</summary>
        public async Task<Result<object?>> BatchDeleteAsync(IList<string> fileCodes, string enterpriseCode, string? reason)
        {
            if (fileCodes == null || fileCodes.Count == 0) return Result<object?>.Fail("未指定要删除的文件");
            var tenantErr = await OwnershipErrorAsync(enterpriseCode);
            if (tenantErr != null) return Result<object?>.Fail(tenantErr);

            var ok = 0;
            var failed = new List<object>();
            foreach (var code in fileCodes.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.Ordinal))
            {
                var r = await DeleteAsync(code, enterpriseCode, reason);
                if (r.Success) ok++;
                else failed.Add(new { FileCode = code, Message = r.Error });
            }
            return Result<object?>.Ok(new { Total = fileCodes.Count, Deleted = ok, Failed = failed });
        }

        /// <summary>版本历史（新 → 旧）</summary>
        public async Task<object> GetVersionsAsync(string fileCode, string enterpriseCode)
        {
            var err = await OwnershipErrorAsync(enterpriseCode);
            if (err != null) return new { Success = false, Message = err, Rows = new object[0] };

            var row = (await _db.GetOneIgnoreValidAsync<EnterpriseDoc>(x => x.Code == fileCode)).Data;
            if (row == null || !string.Equals(row.EnterpriseCode, enterpriseCode, StringComparison.Ordinal))
                return new { Success = false, Message = "记录不存在或不属于当前工作区", Rows = new object[0] };

            var versions = (await _db.Client.Queryable<EnterpriseDocVersion>()
                    .Where(x => x.FileCode == fileCode && !x.IsDeleted && x.IsValid == 1)
                    .ToListAsync() ?? new List<EnterpriseDocVersion>())
                .OrderByDescending(v => v.VersionNumber)
                .Select(v => new
                {
                    v.Code,
                    v.VersionNumber,
                    v.FileName,
                    v.FileType,
                    v.FileSize,
                    v.Sha256,
                    v.StoragePath,
                    v.Reason,
                    v.CreateTime,
                    IsCurrent = v.VersionNumber == row.VersionNumber,
                })
                .ToList();

            return new
            {
                Success = true,
                Message = (string?)null,
                FileCode = fileCode,
                FileName = row.FileName,
                CurrentVersion = row.VersionNumber,
                Rows = versions,
            };
        }

        /// <summary>
        /// 回滚到历史版本：把归档对象<b>复制回</b>当前路径（归档对象保留，证据链不丢），
        /// 再重算 hash、清空产物、入队重转。
        /// <para>⚠️ 版本号<b>不回退</b> —— 回滚是「又改了一次」，版本继续 +1（与 git revert 同语义）。</para>
        /// </summary>
        public async Task<Result<object?>> RestoreVersionAsync(
            string fileCode, string enterpriseCode, int versionNumber, string? reason)
        {
            if (string.IsNullOrWhiteSpace(fileCode)) return Result<object?>.Fail("文件业务键 Code 不能为空");
            if (versionNumber <= 0) return Result<object?>.Fail("请指定要回滚的版本号");

            var tenantErr = await OwnershipErrorAsync(enterpriseCode);
            if (tenantErr != null) return Result<object?>.Fail(tenantErr);

            var row = (await _db.GetOneIgnoreValidAsync<EnterpriseDoc>(x => x.Code == fileCode)).Data;
            if (row == null) return Result<object?>.Fail($"记录不存在：{fileCode}");
            if (!string.Equals(row.EnterpriseCode, enterpriseCode, StringComparison.Ordinal))
                return Result<object?>.Fail("记录不存在或不属于当前工作区");

            var ver = (await _db.Client.Queryable<EnterpriseDocVersion>()
                .Where(x => x.FileCode == fileCode && x.VersionNumber == versionNumber && !x.IsDeleted)
                .ToListAsync() ?? new List<EnterpriseDocVersion>()).FirstOrDefault();
            if (ver == null) return Result<object?>.Fail($"版本 v{versionNumber} 不存在");
            if (string.IsNullOrWhiteSpace(ver.StoragePath)) return Result<object?>.Fail($"版本 v{versionNumber} 缺少存储路径");

            // 归档对象复制回当前路径（⛔ 不 Rename：归档对象必须留着，否则回滚一次就丢一版证据）
            byte[] content;
            try
            {
                var (stream, _) = await _storage.DownloadAsync(ver.StoragePath.TrimStart('/'));
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                content = ms.ToArray();
            }
            catch (Exception ex)
            {
                return Result<object?>.Fail($"读取历史版本失败：{ex.Message}");
            }
            if (content.Length == 0) return Result<object?>.Fail("历史版本内容为空");

            var storagePath = string.IsNullOrEmpty(row.StoragePath)
                ? PathBuilder.EnterpriseDoc(row.EnterpriseCode, row.RelFolderPath, row.FileName)
                : row.StoragePath;

            try
            {
                await _storage.UploadAsync(storagePath.TrimStart('/'), new MemoryStream(content), content.Length);
            }
            catch (Exception ex)
            {
                return Result<object?>.Fail($"写回失败：{ex.Message}");
            }

            var archivedVersion = await MaxArchivedVersionAsync(fileCode);
            row.StoragePath = storagePath;
            row.FileSize = content.Length;
            row.Sha256 = Sha256Hex(content);
            row.VersionNumber = Math.Max(row.VersionNumber, archivedVersion) + 1;
            row.ConvertStatus = ConvertStatus.Pending;
            row.MarkdownStatus = ConvertStatus.Pending;
            row.ConvertMessage = null;
            row.MarkdownMessage = null;
            row.ConvertDate = null;
            row.PreviewPdfPath = null;
            row.MarkdownPath = null;
            row.ExtractStatus = ExtractAgg.None;
            row.ExtractMessage = null;
            row.UpdateTime = DateTime.Now;
            row.UpdateBy = _user.UserCode;
            await _db.UpdateAsync(row,
                nameof(EnterpriseDoc.StoragePath),
                nameof(EnterpriseDoc.FileSize),
                nameof(EnterpriseDoc.Sha256),
                nameof(EnterpriseDoc.VersionNumber),
                nameof(EnterpriseDoc.ConvertStatus),
                nameof(EnterpriseDoc.MarkdownStatus),
                nameof(EnterpriseDoc.ConvertMessage),
                nameof(EnterpriseDoc.MarkdownMessage),
                nameof(EnterpriseDoc.ConvertDate),
                nameof(EnterpriseDoc.PreviewPdfPath),
                nameof(EnterpriseDoc.MarkdownPath),
                nameof(EnterpriseDoc.ExtractStatus),
                nameof(EnterpriseDoc.ExtractMessage),
                nameof(EnterpriseDoc.UpdateTime),
                nameof(EnterpriseDoc.UpdateBy));

            await MarkExtractionStaleAsync(fileCode);
            var (queueCode, queueError) = await EnqueueConvertAsync(
                enterpriseCode, new List<EnterpriseDoc> { row }, $"restore:v{versionNumber}");

            return Result<object?>.Ok(new
            {
                FileCode = fileCode,
                RestoredFrom = versionNumber,
                NewVersion = row.VersionNumber,
                Sha256 = row.Sha256,
                Reason = Truncate(reason, 500),
                QueueCode = queueCode,
                QueueError = queueError,
            });
        }

        /// <summary>
        /// ★ 重新转换（重跑 PDF + Markdown 两链），不动源文件、不改版本号。
        /// <para>Markdown 是提取的唯一输入 ⇒ 重转成功后由上层决定是否重跑提取
        /// （<paramref name="reExtract"/>=true 时本次一并入提取队列）。</para>
        /// </summary>
        public async Task<Result<object?>> RegenerateAsync(string fileCode, string enterpriseCode, bool reExtract = true)
        {
            if (string.IsNullOrWhiteSpace(fileCode)) return Result<object?>.Fail("文件业务键 Code 不能为空");

            var tenantErr = await OwnershipErrorAsync(enterpriseCode);
            if (tenantErr != null) return Result<object?>.Fail(tenantErr);

            var row = (await _db.GetOneIgnoreValidAsync<EnterpriseDoc>(x => x.Code == fileCode)).Data;
            if (row == null) return Result<object?>.Fail($"记录不存在：{fileCode}");
            if (!string.Equals(row.EnterpriseCode, enterpriseCode, StringComparison.Ordinal))
                return Result<object?>.Fail("记录不存在或不属于当前工作区");
            if (string.IsNullOrWhiteSpace(row.StoragePath))
                return Result<object?>.Fail("该文件还没有源字节（上传未完成），无法重新生成");

            row.ConvertStatus = ConvertStatus.Pending;
            row.MarkdownStatus = ConvertStatus.Pending;
            row.ConvertMessage = null;
            row.MarkdownMessage = null;
            row.UpdateTime = DateTime.Now;
            row.UpdateBy = _user.UserCode;
            await _db.UpdateAsync(row,
                nameof(EnterpriseDoc.ConvertStatus),
                nameof(EnterpriseDoc.MarkdownStatus),
                nameof(EnterpriseDoc.ConvertMessage),
                nameof(EnterpriseDoc.MarkdownMessage),
                nameof(EnterpriseDoc.UpdateTime),
                nameof(EnterpriseDoc.UpdateBy));

            var (queueCode, queueError) = await EnqueueConvertAsync(
                enterpriseCode, new List<EnterpriseDoc> { row }, "regenerate");

            return Result<object?>.Ok(new
            {
                FileCode = fileCode,
                QueueCode = queueCode,
                QueueError = queueError,
                // ⚠️ 重转完成前不重跑提取：Markdown 会被覆盖，此刻提取读到的还是旧文本
                ReExtractHint = reExtract
                    ? "转换完成后请再点「按标准提取」；本次不自动重跑，避免读到旧 Markdown"
                    : null,
            });
        }

        // ========================================================
        // 四、读取 Markdown / 队列明细 / 下载
        // ========================================================

        /// <summary>取已生成的 Markdown <b>文本</b>（页面内核对用；下载走 file-markdown）</summary>
        public async Task<object> GetMarkdownAsync(string fileCode, string enterpriseCode)
        {
            var err = await OwnershipErrorAsync(enterpriseCode);
            if (err != null) return new { Success = false, Message = err };

            var row = (await _db.GetOneIgnoreValidAsync<EnterpriseDoc>(x => x.Code == fileCode)).Data;
            if (row == null || !string.Equals(row.EnterpriseCode, enterpriseCode, StringComparison.Ordinal))
                return new { Success = false, Message = "记录不存在或不属于当前工作区" };
            if (string.IsNullOrWhiteSpace(row.MarkdownPath))
                return new
                {
                    Success = false,
                    Message = row.MarkdownStatus == ConvertStatus.Unsupported
                        ? "该格式无法自动提取原文（能力边界），请人工填写"
                        : "Markdown 尚未生成，请等待转换完成或点「重新生成」",
                    MarkdownStatus = row.MarkdownStatus,
                    MarkdownMessage = row.MarkdownMessage,
                };

            try
            {
                var (stream, _) = await _storage.DownloadAsync(row.MarkdownPath.TrimStart('/'));
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);

                // ★ 必须显式 UTF-8：Markdown 产物是 UTF-8，用平台默认编码读会出乱码
                var markdown = Encoding.UTF8.GetString(ms.ToArray());

                return new
                {
                    Success = true,
                    Message = (string?)null,
                    FileCode = fileCode,
                    FileName = row.FileName,
                    MarkdownStatus = row.MarkdownStatus,
                    MarkdownMessage = row.MarkdownMessage,
                    ConvertStatus = row.ConvertStatus,
                    MarkdownPath = row.MarkdownPath,
                    Markdown = markdown,
                    Length = markdown.Length,
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[企业文档] 读取 Markdown 失败: {Code}", fileCode);
                return new { Success = false, Message = $"读取内容失败：{ex.Message}" };
            }
        }

        /// <summary>★ 队列明细：排队位置 + 进度 + 每份文件的任务状态与失败原因</summary>
        public async Task<object> QueueDetailAsync(string enterpriseCode)
        {
            var err = await OwnershipErrorAsync(enterpriseCode);
            if (err != null) return new { Success = false, Message = err, Rows = new object[0] };

            var scopeKey = ScopeKeyOf(enterpriseCode);
            var rows = await _db.Client.Queryable<YzhQueue>()
                .Where(q => q.ScopeKey == scopeKey
                            && (q.QueueType == EnterpriseDocQueue.QueueTypeConvert
                                || q.QueueType == "enterprise_clause_extract"))
                .OrderByDescending(q => q.CreateTime)
                .Take(20)
                .ToListAsync() ?? new List<YzhQueue>();

            // ★ `YzhQueue` 有**两个**码：`Code`（业务键 GUID）与 `QueueCode`（`Q-2026...` 展示号）。
            //   `yzh_queue_task.QueueCode` 关联的是**后者** —— 用 q.Code 匹配必然 0 条。
            var queueCodes = rows.Select(q => q.QueueCode)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .ToHashSet(StringComparer.Ordinal);

            // ⚠️ 用内存过滤而**不是** Where(t => queueCodes.Contains(...))：
            //   SqlSugar 对「闭包捕获的集合 Contains」翻译不可靠，会静默返回空集
            var taskScope = queueCodes.Count == 0
                ? new List<YzhQueueTask>()
                : await _db.Client.Queryable<YzhQueueTask>()
                    .OrderByDescending(t => t.CreateTime)
                    .Take(500)
                    .ToListAsync() ?? new List<YzhQueueTask>();

            var tasks = taskScope.Where(t => t.QueueCode != null && queueCodes.Contains(t.QueueCode)).ToList();

            var fileCodes = tasks
                .Select(t => ExtractPayloadCode(t.Payload))
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c!)
                .ToHashSet(StringComparer.Ordinal);

            var allFiles = fileCodes.Count == 0
                ? new List<EnterpriseDoc>()
                : await _db.Client.Queryable<EnterpriseDoc>()
                    .Where(f => f.EnterpriseCode == enterpriseCode)
                    .ToListAsync() ?? new List<EnterpriseDoc>();
            var nameByCode = allFiles
                .Where(f => fileCodes.Contains(f.Code ?? ""))
                .GroupBy(f => f.Code!)
                .ToDictionary(g => g.Key, g => g.First().FileName ?? g.Key, StringComparer.Ordinal);

            var detail = rows.Select(q => new
            {
                Code = q.QueueCode,
                BizCode = q.Code,
                q.QueueName,
                QueueType = q.QueueType,
                q.Status,
                q.CreateTime,
                q.StartTime,
                q.EndTime,
                q.TotalCount, q.PendingCount, q.ProcessingCount,
                q.CompletedCount, q.FailedCount, q.CancelledCount, q.Progress,
                QueuePosition = rows.Count(x => x.CreateTime <= q.CreateTime),
                Tasks = tasks.Where(t => t.QueueCode == q.QueueCode).Select(t => new
                {
                    t.Code,
                    TaskType = t.TaskType,
                    t.Status,
                    t.ProcessTime,
                    t.RetryCount,
                    t.MaxRetryCount,
                    t.NextRetryAt,
                    t.ErrorType,
                    Message = Truncate(t.ErrorMessage, 300),
                    FileCode = ExtractPayloadCode(t.Payload),
                    FileName = ExtractPayloadCode(t.Payload) is { } fc && nameByCode.TryGetValue(fc, out var nm) ? nm : null,
                }).ToList(),
            }).ToList();

            return new
            {
                Success = true,
                Message = (string?)null,
                RunningCount = rows.Count(q => q.Status is "running" or "processing"),
                PendingCount = rows.Count(q => q.Status == "pending"),
                FailedCount = rows.Count(q => q.Status == "failed"),
                Rows = detail,
            };
        }

        /// <summary>下载：按存储路径取流（路径必须位于已知文档库之下，防穿越）</summary>
        public async Task<(string? Error, Stream? Stream, string? ContentType, string? FileName)> DownloadAsync(
            string storagePath, string? enterpriseCode)
        {
            if (string.IsNullOrWhiteSpace(storagePath)) return ("缺少文件路径", null, null, null);

            // ★ 白名单：必须是已知文档库之下的路径（防 ../ 穿越与任意 key 读取）
            if (!DocumentLibraryPath.IsAllowedStoragePath(storagePath))
                return ("文件路径不合法", null, null, null);

            if (!string.IsNullOrWhiteSpace(enterpriseCode))
            {
                var err = await OwnershipErrorAsync(enterpriseCode);
                if (err != null) return (err, null, null, null);
            }

            try
            {
                var key = storagePath.TrimStart('/');
                if (!await _storage.ExistsAsync(key)) return ("文件不存在", null, null, null);
                var (stream, contentType) = await _storage.DownloadAsync(key);
                var name = PathBuilder.SegmentAt(storagePath, PathBuilder.Segments(storagePath).Length - 1) ?? "download";
                return (null, stream, contentType ?? "application/octet-stream", name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[企业文档] 下载失败: {Path}", storagePath);
                return ($"下载失败：{ex.Message}", null, null, null);
            }
        }

        /// <summary>取预览路径（<paramref name="markdown"/>=true 取 Markdown，否则取 PDF）</summary>
        public async Task<string?> GetPreviewPathAsync(string fileCode, string enterpriseCode, bool markdown)
        {
            var err = await OwnershipErrorAsync(enterpriseCode);
            if (err != null) return null;

            var row = (await _db.GetOneIgnoreValidAsync<EnterpriseDoc>(x => x.Code == fileCode)).Data;
            if (row == null || !string.Equals(row.EnterpriseCode, enterpriseCode, StringComparison.Ordinal)) return null;

            return markdown ? row.MarkdownPath : row.PreviewPdfPath;
        }

        // ========================================================
        // 五、标准来源（唯一入口）+ 提取状态聚合
        // ========================================================

        /// <summary>
        /// 取某企业的<b>当前有效计划</b>（<c>cert_audit_plan</c>，一企业一有效计划）。
        /// <para>⚠️ 理论上最多 1 条；若历史脏数据有多条，取 <c>Sort → CreateTime</c> 最小者（确定性）。
        /// ⛔ 不静默挑一条了事 —— 调用方可用 <see cref="ResolvePlanStandardCodesAsync"/> 的告警得知。</para>
        /// </summary>
        public async Task<CertAuditPlan?> GetActivePlanAsync(string enterpriseCode)
        {
            if (string.IsNullOrWhiteSpace(enterpriseCode)) return null;
            var list = await _db.Client.Queryable<CertAuditPlan>()
                .Where(x => x.EnterpriseCode == enterpriseCode && x.IsValid == 1 && !x.IsDeleted)
                .ToListAsync() ?? new List<CertAuditPlan>();
            return list.OrderBy(p => p.Sort).ThenBy(p => p.CreateTime).FirstOrDefault();
        }

        /// <summary>
        /// ★★ <b>「标准从哪来」的唯一答案</b>：从 <c>cert_audit_plan.Standards</c> 取。
        ///
        /// <para>⛔ <b>不再从机构反推</b>：老实现走 <c>cert_enterprise_stage</c>
        /// （= 审核员注册的机构关联过什么），那是「机构注册过什么」而不是「这家企业要认证什么」。</para>
        ///
        /// <para><b>没有有效计划</b> ⇒ 返回空列表 + 一句人话原因（⛔ 不静默当「无标准」）：
        /// 提取阶段会据此把 <c>ExtractStatus</c> 置 <c>skipped</c> 并在 UI 上提示先建计划。</para>
        /// </summary>
        public async Task<(List<string> Codes, string? Reason)> ResolvePlanStandardCodesAsync(string enterpriseCode)
        {
            var plan = await GetActivePlanAsync(enterpriseCode);
            if (plan == null)
                return (new List<string>(), "该企业还没有有效的认证计划，请先在「认证计划」中创建");

            var codes = SplitCodes(plan.Standards);
            if (codes.Count == 0)
                return (new List<string>(), "当前认证计划没有选择认证标准，请先在计划中补充标准");

            return (codes, null);
        }

        /// <summary>
        /// 重算某个文档的<b>提取聚合状态</b>（按计划内标准逐个数产物）。
        /// <para>调用时机：提取任务完成/失败后、计划标准变更后、文档重转后。</para>
        /// </summary>
        public async Task<string> RecomputeExtractStatusAsync(string fileCode)
        {
            var row = (await _db.GetOneIgnoreValidAsync<EnterpriseDoc>(x => x.Code == fileCode)).Data;
            if (row == null) return ExtractAgg.None;

            // 没 Markdown ⇒ 没东西可提取
            if (row.MarkdownStatus != ConvertStatus.Completed || string.IsNullOrWhiteSpace(row.MarkdownPath))
            {
                await SetExtractAggAsync(row, ExtractAgg.None, null);
                return ExtractAgg.None;
            }

            var (planCodes, reason) = await ResolvePlanStandardCodesAsync(row.EnterpriseCode);
            if (planCodes.Count == 0)
            {
                await SetExtractAggAsync(row, ExtractAgg.Skipped, reason);
                return ExtractAgg.Skipped;
            }

            var exts = await _db.Client.Queryable<DocumentExtraction>()
                .Where(x => x.FileCode == fileCode && !x.IsDeleted && x.IsValid == 1)
                .ToListAsync() ?? new List<DocumentExtraction>();

            var mine = exts.Where(x => planCodes.Contains(x.StandardCode ?? "")).ToList();
            string agg;
            string? msg = null;

            if (mine.Any(x => x.ExtractStatus == "running"))
            {
                agg = ExtractAgg.Running;
            }
            else
            {
                var done = mine.Count(x => x.ExtractStatus == "done");
                var failed = mine.Count(x => x.ExtractStatus == "failed");
                var skipped = mine.Count(x => x.ExtractStatus == "skipped");

                if (mine.Count == 0) agg = ExtractAgg.Pending;
                else if (done == planCodes.Count) agg = ExtractAgg.Done;
                else if (failed > 0 && done + failed + skipped == planCodes.Count) agg = ExtractAgg.Failed;
                else if (done + failed + skipped >= planCodes.Count) agg = failed > 0 ? ExtractAgg.Failed : ExtractAgg.Done;
                else agg = ExtractAgg.Partial;

                if (failed > 0)
                    msg = $"{failed}/{planCodes.Count} 个标准提取失败：" +
                          string.Join("；", mine.Where(x => x.ExtractStatus == "failed")
                              .Select(x => Truncate(x.ExtractMessage, 120)).Where(m => !string.IsNullOrEmpty(m)).Take(3));
            }

            await SetExtractAggAsync(row, agg, msg);
            return agg;
        }

        private async Task SetExtractAggAsync(EnterpriseDoc row, string status, string? message)
        {
            row.ExtractStatus = status;
            row.ExtractMessage = Truncate(message, 1024);
            row.ExtractTime = status is ExtractAgg.Done or ExtractAgg.Failed or ExtractAgg.Partial
                ? DateTime.Now : row.ExtractTime;
            row.UpdateTime = DateTime.Now;
            await _db.UpdateAsync(row,
                nameof(EnterpriseDoc.ExtractStatus),
                nameof(EnterpriseDoc.ExtractMessage),
                nameof(EnterpriseDoc.ExtractTime),
                nameof(EnterpriseDoc.UpdateTime));
        }

        /// <summary>文档被替换/回滚 ⇒ 已有提取产物全部置 <c>IsStale=1</c>（字节变了，结果不可信）</summary>
        private async Task MarkExtractionStaleAsync(string fileCode)
        {
            var exts = await _db.Client.Queryable<DocumentExtraction>()
                .Where(x => x.FileCode == fileCode && !x.IsDeleted)
                .ToListAsync() ?? new List<DocumentExtraction>();
            foreach (var e in exts.Where(x => !x.IsStale))
            {
                e.IsStale = true;
                e.UpdateTime = DateTime.Now;
                await _db.UpdateAsync(e, nameof(DocumentExtraction.IsStale), nameof(DocumentExtraction.UpdateTime));
            }
        }

        // ========================================================
        // 六、入队
        // ========================================================

        /// <summary>
        /// 一次提交 = 一个队列（N 文件 = N 任务，同队列串行）。
        /// <para>⚠️ <b>不做阶段级互斥闸门</b>：文件级队列互不冲突（老模块的阶段级拒绝实测误伤，已废除）。</para>
        /// </summary>
        private async Task<(string? QueueCode, string? Error)> EnqueueConvertAsync(
            string enterpriseCode, List<EnterpriseDoc> files, string sourceTag)
        {
            if (files.Count == 0) return (null, null);

            var scopeKey = ScopeKeyOf(enterpriseCode);

            // ── 同文件重入分流：单文件旧队列取消重建；混合批次队列不误伤，本文件跳过 ──
            var ready = new List<EnterpriseDoc>();
            var blocked = new List<(string FileName, string QueueCode)>();
            foreach (var f in files)
            {
                var conflictQueue = await FindActiveQueueCodeOfFileAsync(f.Code!);
                if (conflictQueue == null) { ready.Add(f); continue; }

                var queueFiles = await GetQueueFileCodesAsync(conflictQueue);
                if (queueFiles.Count == 1 && queueFiles.Contains(f.Code!))
                {
                    var (ok, cerr) = await _queueManager.CancelQueueAsync(conflictQueue);
                    _logger.LogInformation("[EnqueueConvert] 单文件旧队列 {Queue} 取消={Ok}（文件 {File}） {Reason}",
                        conflictQueue, ok, f.FileName, cerr);
                    if (ok) { ready.Add(f); continue; }
                }
                blocked.Add((f.FileName, conflictQueue));
            }

            if (ready.Count == 0)
            {
                var why = blocked.Count > 0
                    ? $"文件「{blocked[0].FileName}」正在批次队列 {blocked[0].QueueCode} 中处理，请等待完成后再提交"
                    : "队列创建失败";
                return (null, why);
            }

            var tasks = new List<QueueManager.TaskItem>();
            var locks = new List<QueueManager.ResourceLockItem>();
            for (var i = 0; i < ready.Count; i++)
            {
                var f = ready[i];
                tasks.Add(new QueueManager.TaskItem
                {
                    TaskType = EnterpriseDocQueue.TaskTypeConvert,
                    TaskId = sourceTag,
                    Seq = i + 1,
                    Payload = JsonSerializer.Serialize(new EnterpriseDocConvertPayload
                    {
                        Code = f.Code!,
                        EnterpriseCode = f.EnterpriseCode,
                        BatchCode = sourceTag,
                    }),
                });
                locks.Add(new QueueManager.ResourceLockItem
                {
                    ResourceTable = EnterpriseDocQueue.ResourceTable,
                    ResourceCode = f.Code!,
                    ResourceName = f.FileName,
                    TaskNo = i + 1,
                });
            }

            var (ok2, err2, code, _) = await _queueManager.CreateQueueAsync(new QueueManager.CreateQueueRequest
            {
                QueueType = EnterpriseDocQueue.QueueTypeConvert,
                QueueName = ready.Count == 1
                    ? $"企业文档转换 - {Truncate(ready[0].FileName, 80)}"
                    : $"企业文档转换 - {ready.Count} 个文件",
                ScopeKey = scopeKey,
                SourceType = "enterprise_doc",
                // 批次级唯一键：sourceTag + 毫秒时间戳 ⇒ 同批次多轮不撞
                SourceId = $"batch:{sourceTag}@{DateTime.Now:yyyyMMddHHmmssfff}",
                // ★ CreateBy 存用户 Code（契约）—— 终态通知（站内信 + SignalR）按它寻址收件人
                UserCode = _user.UserCode,
                UserName = _user.UserName,
                ResourceLocks = locks,
                Tasks = tasks,
            });

            if (!ok2 || string.IsNullOrEmpty(code))
            {
                if (IsDuplicateKeyError(err2))
                    _logger.LogInformation("[EnqueueConvert] 并发下已有队列（唯一约束拦截）: {Tag}", sourceTag);
                else
                    _logger.LogWarning("[EnqueueConvert] 队列创建失败: {Tag} {Reason}", sourceTag, err2);
                return (null, err2 ?? "队列创建失败");
            }

            string? partial = null;
            if (blocked.Count > 0)
                partial = $"已入队 {ready.Count} 个文件；{blocked.Count} 个文件正在其他批次处理中，未入队："
                    + string.Join("、", blocked.Select(b => $"「{b.FileName}」(队列 {b.QueueCode})"));

            _logger.LogInformation("[EnqueueConvert] {Scope} 一次提交入队 {Ready}/{Total} 个文件 → 队列 {Queue}（{Tag}）",
                scopeKey, ready.Count, files.Count, code, sourceTag);
            return (code, partial);
        }

        // ========================================================
        // 六之二、入队：按标准提取条款（P2）
        // ========================================================

        /// <summary>
        /// <b>按标准提取条款入队</b>（文档 × 标准 = 一个任务）。
        ///
        /// <para><b>★ 标准从哪来</b>：<see cref="ResolvePlanStandardCodesAsync"/>（计划主表）。
        /// 没有有效计划 ⇒ 返回 <c>Queued=0</c> + 一句人话原因，⛔ 不静默当成功。</para>
        ///
        /// <para><b>★ 只入队「真需要跑」的</b>：Markdown 就绪 + 该标准还没跑过（或 <c>IsStale</c> /
        /// hash 变了 / 上次失败）。已经 <c>done</c> 且 hash 未变的直接跳过 —— 否则用户每点一次
        /// 「开始提取」都全量重跑一遍模型，费用与时间都不可接受。</para>
        ///
        /// <para>⚠️ 与转换队列<b>互不阻塞</b>：资源锁表用
        /// <see cref="EnterpriseDocQueue.ResourceTableExtract"/>，锁键 = <c>{fileCode}:{standardCode}</c>
        /// ⇒ 同一份文档的 N 个标准可并行，且不会把排队中的转换挡住。</para>
        /// </summary>
        public async Task<(int Queued, int Skipped, string? Error)> EnqueueExtractAsync(
            string enterpriseCode, IReadOnlyList<EnterpriseDoc> files, string sourceTag)
        {
            if (files == null || files.Count == 0) return (0, 0, null);

            var (planCodes, reason) = await ResolvePlanStandardCodesAsync(enterpriseCode);
            if (planCodes.Count == 0) return (0, 0, reason ?? "当前计划没有认证标准");

            var scopeKey = ScopeKeyOf(enterpriseCode);
            var tasks = new List<QueueManager.TaskItem>();
            var locks = new List<QueueManager.ResourceLockItem>();
            var skipped = 0;

            foreach (var f in files)
            {
                // 没 Markdown 就没东西可提取（⛔ 不退回 PDF：文本层常是乱码，静默出错更贵）
                if (f.MarkdownStatus != ConvertStatus.Completed || string.IsNullOrWhiteSpace(f.MarkdownPath))
                {
                    skipped += planCodes.Count;
                    continue;
                }

                var exts = await _db.Client.Queryable<DocumentExtraction>()
                    .Where(x => x.FileCode == f.Code && !x.IsDeleted && x.IsValid == 1)
                    .ToListAsync() ?? new List<DocumentExtraction>();

                foreach (var std in planCodes)
                {
                    var prev = exts.FirstOrDefault(x =>
                        string.Equals(x.StandardCode, std, StringComparison.Ordinal));

                    var upToDate = prev != null
                        && !prev.IsStale
                        && prev.ExtractStatus == ExtractRowStatus.Done
                        && !string.IsNullOrEmpty(f.Sha256)
                        && string.Equals(prev.SourceSha256, f.Sha256, StringComparison.OrdinalIgnoreCase);

                    if (upToDate) { skipped++; continue; }

                    var seq = tasks.Count + 1;
                    tasks.Add(new QueueManager.TaskItem
                    {
                        TaskType = EnterpriseDocQueue.TaskTypeExtract,
                        TaskId = sourceTag,
                        Seq = seq,
                        Payload = JsonSerializer.Serialize(new DocumentExtractPayload
                        {
                            FileCode = f.Code!,
                            StandardCode = std,
                            EnterpriseCode = enterpriseCode,
                            BatchCode = sourceTag,
                        }),
                    });
                    locks.Add(new QueueManager.ResourceLockItem
                    {
                        ResourceTable = EnterpriseDocQueue.ResourceTableExtract,
                        ResourceCode = $"{f.Code}:{std}",
                        ResourceName = $"{f.FileName} × {std}",
                        TaskNo = seq,
                    });
                }
            }

            if (tasks.Count == 0) return (0, skipped, null);

            var (ok, err, code, _) = await _queueManager.CreateQueueAsync(new QueueManager.CreateQueueRequest
            {
                QueueType = EnterpriseDocQueue.QueueTypeExtract,
                QueueName = tasks.Count == 1
                    ? $"条款提取 - {Truncate(files[0].FileName, 60)}"
                    : $"条款提取 - {files.Count} 个文件 × {planCodes.Count} 个标准",
                ScopeKey = scopeKey,
                SourceType = "enterprise_doc",
                SourceId = $"extract:{sourceTag}@{DateTime.Now:yyyyMMddHHmmssfff}",
                UserCode = _user.UserCode,
                UserName = _user.UserName,
                ResourceLocks = locks,
                Tasks = tasks,
            });

            if (!ok || string.IsNullOrEmpty(code))
            {
                if (IsDuplicateKeyError(err))
                    _logger.LogInformation("[EnqueueExtract] 并发下已有队列（唯一约束拦截）: {Tag}", sourceTag);
                else
                    _logger.LogWarning("[EnqueueExtract] 队列创建失败: {Tag} {Reason}", sourceTag, err);
                return (0, skipped, err ?? "队列创建失败");
            }

            _logger.LogInformation("[EnqueueExtract] {Scope} 入队 {N} 个任务（跳过 {Skip}）→ 队列 {Queue}（{Tag}）",
                scopeKey, tasks.Count, skipped, code, sourceTag);

            // 入队后立刻刷一次聚合状态（前端靠它显示「排队中」）
            foreach (var f in files)
                await RecomputeExtractStatusAsync(f.Code!);

            return (tasks.Count, skipped, null);
        }

        /// <summary>
        /// 单文件重新提取（转换完成后的自动接力 / 用户点「重新提取」）。
        /// <para>⚠️ 失败<b>不抛</b>：自动接力是<b>尽力而为</b> —— 提取队列建不出来不能让
        /// 「转换成功」变成「转换失败」（用户会以为文件坏了）。原因写日志 + 返回给调用方。</para>
        /// </summary>
        public async Task<(int Queued, string? Error)> EnqueueExtractForFileAsync(string fileCode, string sourceTag)
        {
            var row = (await _db.GetOneIgnoreValidAsync<EnterpriseDoc>(x => x.Code == fileCode)).Data;
            if (row == null || row.IsDeleted) return (0, "文件行不存在或已删除");
            var (queued, _, err) = await EnqueueExtractAsync(row.EnterpriseCode, new[] { row }, sourceTag);
            return (queued, err);
        }

        /// <summary>
        /// 整家企业重新提取（用户在列表页点「开始提取」/ 计划标准变更后）。
        /// </summary>
        public async Task<(int Queued, int Skipped, string? Error)> EnqueueExtractForEnterpriseAsync(
            string enterpriseCode, string sourceTag, IReadOnlyList<string>? fileCodes = null)
        {
            var err = await OwnershipErrorAsync(enterpriseCode);
            if (err != null) return (0, 0, err);

            var all = await _db.Client.Queryable<EnterpriseDoc>()
                .Where(x => x.EnterpriseCode == enterpriseCode && !x.IsDeleted && x.IsValid == 1)
                .ToListAsync() ?? new List<EnterpriseDoc>();

            var files = all;
            if (fileCodes is { Count: > 0 })
            {
                var wanted = new HashSet<string>(fileCodes.Where(x => !string.IsNullOrWhiteSpace(x)), StringComparer.Ordinal);
                files = all.Where(x => wanted.Contains(x.Code ?? string.Empty)).ToList();
            }

            if (files.Count == 0) return (0, 0, "该企业还没有可提取的文档");

            return await EnqueueExtractAsync(enterpriseCode, files, sourceTag);
        }

        /// <summary>查该文件当前活跃（非终态）的队列 Code（资源锁优先，SourceId 前缀兜底）</summary>
        private async Task<string?> FindActiveQueueCodeOfFileAsync(string fileCode)
        {
            var hit = await _queueManager.FindResourceLockAsync(EnterpriseDocQueue.ResourceTable, new List<string> { fileCode });
            if (hit != null && !string.IsNullOrEmpty(hit.QueueCode))
                return hit.QueueCode;

            var q = await _db.Client.Queryable<YzhQueue>()
                .Where(x => x.QueueType == EnterpriseDocQueue.QueueTypeConvert
                            && x.SourceId != null
                            && x.SourceId.StartsWith($"file:{fileCode}@")
                            && x.Status != "completed" && x.Status != "failed" && x.Status != "cancelled")
                .OrderByDescending(x => x.CreateTime)
                .FirstAsync();
            return q?.QueueCode;
        }

        /// <summary>队列任务载荷里涉及的文件 Code 集合 —— 判「单文件队列 vs 混合批次队列」的依据</summary>
        private async Task<HashSet<string>> GetQueueFileCodesAsync(string queueCode)
        {
            var tasks = await _db.Client.Queryable<YzhQueueTask>()
                .Where(t => t.QueueCode == queueCode)
                .ToListAsync() ?? new List<YzhQueueTask>();
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var t in tasks)
            {
                var one = ExtractPayloadCode(t.Payload);
                if (!string.IsNullOrWhiteSpace(one)) set.Add(one!);
            }
            return set;
        }

        // ========================================================
        // 七、工具
        // ========================================================

        /// <summary>队列 ScopeKey：<b>按企业</b>（⛔ 不再拼阶段 —— 阶段维度已废除）</summary>
        public static string ScopeKeyOf(string enterpriseCode) => $"ent:{enterpriseCode}";

        /// <summary>
        /// 工作区守卫：企业必须属于当前登录用户所在机构（<b>一处收口</b>，避免每个方法各写一遍漏网）。
        /// </summary>
        private async Task<string?> OwnershipErrorAsync(string enterpriseCode)
        {
            if (string.IsNullOrWhiteSpace(enterpriseCode)) return "企业编码不能为空";

            var ws = _workspace.Resolve(_user.UserCode);
            if (!ws.Success || ws.Data == null) return ws.Error ?? "无法定位当前工作区";

            var ent = await _core.GetEnterpriseByCodeAsync(enterpriseCode);
            if (ent == null) return "企业不存在或不属于当前工作区";
            if (!string.Equals(ent.OrgCode, ws.Data.Code, StringComparison.Ordinal))
                return "企业不存在或不属于当前工作区";

            return null;
        }

        /// <summary>取企业所属机构 Code（冗余列，只作留痕，⛔ 不作筛选条件）</summary>
        private async Task<string?> ResolveOrgCodeAsync(string enterpriseCode)
        {
            var ent = await _core.GetEnterpriseByCodeAsync(enterpriseCode);
            return ent?.OrgCode;
        }

        private async Task<int> MaxArchivedVersionAsync(string fileCode)
        {
            var list = await _db.Client.Queryable<EnterpriseDocVersion>()
                .Where(x => x.FileCode == fileCode && !x.IsDeleted)
                .ToListAsync() ?? new List<EnterpriseDocVersion>();
            return list.Count == 0 ? 0 : list.Max(v => v.VersionNumber);
        }

        private async Task SafeDeleteAsync(string? path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                if (await _storage.ExistsAsync(path.TrimStart('/')))
                    await _storage.DeleteAsync(path.TrimStart('/'));
            }
            catch (Exception ex) { _logger.LogWarning(ex, "[Delete] 对象删除失败（不阻断）: {Path}", path); }
        }

        /// <summary>是否需要走转换（排除 .tmp 之类无意义后缀）</summary>
        private static bool NeedsConversion(string? fileName)
        {
            var ext = Path.GetExtension(fileName ?? "").ToLowerInvariant();
            if (string.IsNullOrEmpty(ext)) return false;
            return !new[] { "ds_store", "tmp", "temp" }.Contains(ext.TrimStart('.'));
        }

        /// <summary>文件名字段清洗（与 <c>PathBuilder.Sanitize</c> 同口径：只去路径分隔符）</summary>
        internal static string SanitizeName(string? name)
            => (name ?? "").Replace("/", "").Replace("\\", "").Trim();

        /// <summary>相对文件夹路径归一（去首尾 /、去空段、去穿越片段）；⛔ 不得含保留段名</summary>
        internal static string NormalizeFolder(string? relFolderPath)
        {
            var segs = PathBuilder.Segments(relFolderPath);
            if (segs.Length == 0) return "";
            var reserved = PathBuilder.ReservedSegments
                .SelectMany(r => PathBuilder.Segments(r))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            return string.Join("/", segs.Where(s => !reserved.Contains(s)));
        }

        internal static string Sha256Hex(byte[] bytes)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(bytes);
            var sb = new StringBuilder(hash.Length * 2);
            foreach (var b in hash) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        private static string Truncate(string? s, int max)
            => string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s[..max]);

        /// <summary>拆分逗号分隔的 Code 串（去空、去重、保持顺序）</summary>
        internal static List<string> SplitCodes(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<string>();
            foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                if (seen.Add(part)) result.Add(part);
            return result;
        }

        /// <summary>错误信息里是否含「唯一约束 / Duplicate entry」——并发建队列时的正常竞态</summary>
        private static bool IsDuplicateKeyError(string? err)
        {
            if (string.IsNullOrWhiteSpace(err)) return false;
            return err.Contains("Duplicate entry", StringComparison.OrdinalIgnoreCase)
                || err.Contains("唯一约束", StringComparison.OrdinalIgnoreCase)
                || err.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>从队列 payload 里取文件 Code（各 payload 形态都带 <c>Code</c>）</summary>
        private static string? ExtractPayloadCode(string? payload)
        {
            if (string.IsNullOrWhiteSpace(payload)) return null;
            try
            {
                using var doc = JsonDocument.Parse(payload);
                return doc.RootElement.TryGetProperty("Code", out var v) ? v.GetString() : null;
            }
            catch (JsonException) { return null; }
        }
    }
}

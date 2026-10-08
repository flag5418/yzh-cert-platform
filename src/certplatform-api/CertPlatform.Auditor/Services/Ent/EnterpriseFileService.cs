using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SqlSugar;
using YZH.Core.Api.Models.Organization;
using CertPlatform.Shared.DocExtraction;
using CertPlatform.Shared.Constants;
using CertPlatform.Admin.Entities.Cert;
using CertPlatform.Admin.Entities.Dir;
using CertPlatform.Shared.Storage;
using YZH.Core.DataBase.Services;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Result;

namespace CertPlatform.Auditor.Services.Ent;

/// <summary>
/// 企业资料管理服务（专家端 <c>/resources</c>，04/05 分册）。
///
/// <para><b>三条硬约束</b>（写在本类顶部，改代码前先读）：</para>
/// <list type="number">
///   <item><b>工作区守卫</b>：每个公开方法第一行调 <see cref="OwnershipErrorAsync"/>，
///         确认目标企业属于当前登录人的工作区。缺这一步 = 任何工作区都能读写别家企业的资料（P0-2 越权）。</item>
///   <item><b>状态机唯一权威</b>：文件就位状态只由 <see cref="StatusOf"/> 判（01 分册 §5.1），
///         别处禁止另写判定 —— 历史上「两处判据漂移」是本模块主要 bug 源。</item>
///   <item><b>入队形态</b>：每可转换文件 <b>1 个</b> <c>file_convert</c> 任务（<c>ConvertType=""</c>，
///         执行器自动双产物且失败隔离），<c>ScopeKey = 企业 config.Code</c>（同标准目录一次只允许一个运行队列）。</item>
/// </list>
/// </summary>
public class EnterpriseFileService
{
    private readonly IDbOrm _db;
    private readonly IObjectStorage _storage;
    private readonly QueueManager _queueManager;
    private readonly WorkspaceContextService _workspace;
    private readonly IUserContext _user;
    private readonly ILogger<EnterpriseFileService> _logger;
    /// <summary>S2③ 结果版本店（归档/回活/读取唯一口）</summary>
    private readonly CertPlatform.Admin.Services.DocExtraction.EnterpriseExtractionResultStore _resultStore;
    /// <summary>提取定位链（P2：槽位页四态判定与执行器共用同一结论）</summary>
    private readonly CertPlatform.Admin.Services.DocExtraction.ExtractionScopeResolver _scopeResolver;

    /// <summary>上传任务有效期（分钟）：init 后超时未 confirm 即作废，防悬空 draft 行永久占位</summary>
    private const int TaskExpireMinutes = 30;

    /// <summary>就位状态取值（01 分册 §5.1，前后端逐字一致）</summary>
    private static class Status
    {
        public const string Missing = "missing";
        public const string Uploading = "uploading";
        public const string Uploaded = "uploaded";
        public const string Converting = "converting";
        public const string Ready = "ready";
        public const string ConvertFailed = "convertFailed";
        /// <summary>PDF 成功但 Markdown 产物失败（提取/NC 链路拿不到正文）——不能算「已就绪」</summary>
        public const string MarkdownFailed = "markdownFailed";
        public const string Removed = "removed";
    }

    public EnterpriseFileService(
        IDbOrm db,
        IObjectStorage storage,
        QueueManager queueManager,
        WorkspaceContextService workspace,
        IUserContext user,
        ILogger<EnterpriseFileService> logger,
        CertPlatform.Admin.Services.DocExtraction.EnterpriseExtractionResultStore resultStore,
        CertPlatform.Admin.Services.DocExtraction.ExtractionScopeResolver scopeResolver)
    {
        _db = db;
        _storage = storage;
        _queueManager = queueManager;
        _workspace = workspace;
        _user = user;
        _logger = logger;
        _resultStore = resultStore;
        _scopeResolver = scopeResolver;
    }

    #region 一、左树与阶段汇总

    /// <summary>
    /// 左树：本工作区企业 → 该企业关联的认证阶段（每阶段带标准数）。
    /// <para>数据源：<c>cert_enterprise</c>（工作区隔离）+ <c>cert_enterprise_stage</c>（三元关联）
    /// + <c>cert_cert_stage</c>（阶段名）。</para>
    /// </summary>
    public async Task<object> StageTreeAsync()
    {
        var ws = _workspace.Resolve(_user.UserCode);
        if (!ws.Success || ws.Data == null)
            return new { Configured = false, Message = ws.Error ?? "无法定位当前工作区", Enterprises = new object[0] };

        var enterprises = (await _db.GetListAsync<Enterprise>(
            x => x.OrgCode == ws.Data.Code, includeDisabled: false)).Data ?? new List<Enterprise>();
        enterprises = enterprises.Where(e => e.IsValid == 1).OrderBy(e => e.Sort).ThenBy(e => e.EnterpriseNo).ToList();

        if (enterprises.Count == 0)
            return new { Configured = true, Message = (string?)null, Enterprises = new object[0] };

        var entCodes = enterprises.Select(e => e.Code).ToList();
        var links = (await _db.GetListAsync<CertEnterpriseStage>(
            x => entCodes.Contains(x.EnterpriseCode))).Data ?? new List<CertEnterpriseStage>();
        links = links.Where(l => l.IsValid == 1).ToList();

        var stageRows = (await _db.GetListAsync<CertStage>(x => x.IsValid == 1)).Data ?? new List<CertStage>();
        var stageDict = stageRows
            .Where(s => !string.IsNullOrEmpty(s.Code))
            .GroupBy(s => s.Code)
            .ToDictionary(g => g.Key, g => g.First());

        var nodes = enterprises.Select(e =>
        {
            var mine = links.Where(l => l.EnterpriseCode == e.Code).ToList();
            var stages = mine
                .GroupBy(l => l.StageCode)
                .Select(g =>
                {
                    var stage = stageDict.TryGetValue(g.Key, out var s) ? s : null;
                    return new
                    {
                        StageCode = g.Key,
                        StageName = stage?.StageName ?? g.Key,
                        StageCodeBiz = stage?.StageCode,
                        SortOrder = stage?.SortOrder ?? 0,
                        StandardCount = g.Select(x => x.StandardCode).Distinct().Count()
                    };
                })
                .OrderBy(s => s.SortOrder)
                .ThenBy(s => s.StageCode)
                .ToList();

            return new
            {
                Code = e.Code,
                Name = e.Name,
                EnterpriseNo = e.EnterpriseNo,
                Stages = stages
            };
        }).ToList();

        return new { Configured = true, Message = (string?)null, Enterprises = nodes };
    }

    /// <summary>
    /// 阶段汇总：该企业该阶段下<b>每个关联标准</b>的应上传/已就位/转换中/缺失。
    /// <para>每个标准进入前先 ensure 企业目录（幂等），ensure 失败不阻断其它标准 —— 只在该标准行回 <c>Message</c>。</para>
    /// </summary>
    public async Task<object> StageOverviewAsync(string enterpriseCode, string stageCode)
    {
        var err = await OwnershipErrorAsync(enterpriseCode);
        if (err != null) return new { Configured = false, Message = err, Standards = new object[0] };

        if (string.IsNullOrWhiteSpace(stageCode))
            return new { Configured = false, Message = "请先选择认证阶段", Standards = new object[0] };

        var links = (await _db.GetListAsync<CertEnterpriseStage>(
            x => x.EnterpriseCode == enterpriseCode && x.StageCode == stageCode)).Data
            ?? new List<CertEnterpriseStage>();
        var stdCodes = links.Where(l => l.IsValid == 1).Select(l => l.StandardCode).Distinct().ToList();
        if (stdCodes.Count == 0)
            return new
            {
                Configured = false,
                Message = "该企业在此阶段尚未关联任何标准，请先在「阶段标准关联」页配置",
                Standards = new object[0]
            };

        var stageName = (await _db.GetOneAsync<CertStage>(x => x.Code == stageCode)).Data?.StageName;
        var stdRows = (await _db.GetListAsync<ISOStandard>(x => stdCodes.Contains(x.Code!))).Data
                      ?? new List<ISOStandard>();
        var stdDict = stdRows.Where(s => !string.IsNullOrEmpty(s.Code))
            .GroupBy(s => s.Code!).ToDictionary(g => g.Key, g => g.First());

        var list = new List<object>();
        var totalRequired = 0;
        var totalLive = 0;
        var totalConverting = 0;
        var totalMissing = 0;

        foreach (var stdCode in stdCodes)
        {
            var ensure = await EnsureDirectoryAsync(enterpriseCode, null, stdCode, stageCode);
            var std = stdDict.TryGetValue(stdCode, out var s) ? s : null;

            if (ensure.Error != null)
            {
                list.Add(new
                {
                    StandardCode = stdCode,
                    StandardName = std?.StandardName ?? stdCode,
                    StandardNo = std?.StandardCode,
                    Configured = false,
                    Message = ensure.Error,
                    Required = 0, Live = 0, Ready = 0, Converting = 0, Missing = 0
                });
                continue;
            }

            var slots = (await _db.GetListAsync<StandardDirectoryFile>(
                x => x.ConfigCode == ensure.Config!.Code, includeDisabled: true)).Data
                ?? new List<StandardDirectoryFile>();

            var required = slots.Where(f => f.IsRequired).ToList();
            var live = required.Count(IsLive);
            var ready = required.Count(f => StatusOf(f) == Status.Ready);
            var converting = required.Count(f => StatusOf(f) == Status.Converting);
            var missing = required.Count - live;

            totalRequired += required.Count;
            totalLive += live;
            totalConverting += converting;
            totalMissing += missing;

            list.Add(new
            {
                StandardCode = stdCode,
                StandardName = std?.StandardName ?? stdCode,
                StandardNo = std?.StandardCode,
                StandardVersionYear = std?.VersionYear,
                Configured = true,
                Message = (string?)null,
                EnterpriseConfigCode = ensure.Config!.Code,
                TemplateConfigCode = ensure.TemplateConfigCode,
                Required = required.Count,
                Live = live,
                Ready = ready,
                Converting = converting,
                Missing = missing
            });
        }

        return new
        {
            EnterpriseCode = enterpriseCode,
            StageCode = stageCode,
            StageName = stageName,
            Configured = true,
            Message = (string?)null,
            Standards = list,
            Summary = new
            {
                StandardCount = stdCodes.Count,
                TotalRequired = totalRequired,
                TotalLive = totalLive,
                TotalConverting = totalConverting,
                TotalMissing = totalMissing
            }
        };
    }

    #endregion

    #region 二、标准卡片主数据

    /// <summary>
    /// 页面主数据：某标准的<b>文件夹树 + 槽位全集（带就位状态）</b>。
    /// <para>先 ensure 企业行/槽位（幂等）；失败不抛异常，回 <c>Configured=false + Message</c>。</para>
    /// </summary>
    public async Task<object> StandardDirectoryAsync(string enterpriseCode, string stageCode, string standardCode)
    {
        var err = await OwnershipErrorAsync(enterpriseCode);
        if (err != null) return new { Configured = false, Message = err, Files = new object[0], Folders = new object[0] };

        if (string.IsNullOrWhiteSpace(standardCode) || string.IsNullOrWhiteSpace(stageCode))
            return new { Configured = false, Message = "缺少标准或阶段编码", Files = new object[0], Folders = new object[0] };

        var ensure = await EnsureDirectoryAsync(enterpriseCode, null, standardCode, stageCode);
        if (ensure.Error != null)
            return new { Configured = false, Message = ensure.Error, Files = new object[0], Folders = new object[0] };

        var config = ensure.Config!;
        var std = (await _db.GetOneAsync<ISOStandard>(x => x.Code == standardCode)).Data;

        // 文件夹树：模板 folder 行（槽位沿用的是模板 FolderCode，故模板即权威结构）
        var tplFolders = (await _db.GetListAsync<StandardDirectoryFolder>(
            x => x.ConfigCode == ensure.TemplateConfigCode)).Data ?? new List<StandardDirectoryFolder>();
        // 路径口径：对外一律「相对配置根」（03 §3.2 / 05 §8.1），故剥掉模板根段
        var rootSegment = RootSegmentOf(tplFolders);
        var folders = tplFolders
            .OrderBy(f => f.Depth).ThenBy(f => f.SortOrder).ThenBy(f => f.Sort)
            .Select(f => new StandardDirectoryFolderView
            {
                Code = f.Code,
                ParentCode = string.IsNullOrEmpty(f.ParentCode) ? null : f.ParentCode,
                FolderName = f.FolderName ?? "",
                Depth = f.Depth,
                SortOrder = f.SortOrder,
                FullPath = RelativePath(f.FullPath, rootSegment)
            })
            .ToList();

        var folderByCode = folders.Where(f => !string.IsNullOrEmpty(f.Code))
            .GroupBy(f => f.Code).ToDictionary(g => g.Key, g => g.First());

        // 槽位全集：IsValid=1（在清单）∪ 有 StoragePath（已上传/已移除，证据保留）
        var allSlots = (await _db.GetListAsync<StandardDirectoryFile>(
            x => x.ConfigCode == config.Code, includeDisabled: true)).Data ?? new List<StandardDirectoryFile>();

        // ★ P2 提取状态四态（2026-09-29 用户裁决）：
        //   not_configured 未配置（四元组无可用规则，resolver 结论）> queued 提取中（文件资源锁）
        //   > extracted 已提取 > pending 未提取。resolver 与执行器共用同一口径，不在此重复判定规则。
        var ruleSlots = await _scopeResolver.ResolveAsync(enterpriseCode, stageCode, standardCode);
        var ruleMap = ruleSlots
            .Where(x => !string.IsNullOrEmpty(x.FileCode))
            .GroupBy(x => x.FileCode!)
            .ToDictionary(g => g.Key, g => g.First());

        var slotCodes = allSlots
            .Where(f => !string.IsNullOrEmpty(f.Code))
            .Select(f => f.Code!).Distinct().ToList();
        var lockedFiles = new HashSet<string>();
        if (slotCodes.Count > 0)
        {
            var lockRows = (await _db.GetListAsync<YZH.Core.Stand.Models.Queue.YzhQueueResourceLock>(
                r => r.Status == "locked" && r.ResourceTable == QueueManager.RESOURCE_FILE)).Data
                ?? new List<YZH.Core.Stand.Models.Queue.YzhQueueResourceLock>();
            foreach (var r in lockRows)
                if (slotCodes.Contains(r.ResourceCode)) lockedFiles.Add(r.ResourceCode);
        }

        var files = allSlots
            .Where(f => f.IsValid == 1 || !string.IsNullOrEmpty(f.StoragePath))
            .Select(f =>
            {
                var folderPath = folderByCode.TryGetValue(f.FolderCode ?? "", out var fo) ? fo.FullPath : "";
                return new StandardDirectoryFileView
                {
                    Code = f.Code,
                    StandardCode = f.StandardCode,
                    StageCode = f.StageCode,
                    FolderCode = f.FolderCode ?? "",
                    FolderName = folderByCode.TryGetValue(f.FolderCode ?? "", out var fo2) ? fo2.FolderName : "根目录",
                    FolderPath = folderPath,
                    FileName = f.FileName,
                    FileType = f.FileType,
                    FullPath = f.FullPath,
                    IsRequired = f.IsRequired,
                    VersionNumber = f.VersionNumber,
                    IsValid = f.IsValid,
                    StandardFileCode = f.StandardFileCode,
                    ExtractionEnabled = f.ExtractionEnabled,
                    FileSize = f.FileSize,
                    StoragePath = f.StoragePath,
                    PreviewPdfPath = f.PreviewPdfPath,
                    MarkdownPath = f.MarkdownPath,
                    UploadStatus = f.UploadStatus,
                    ConvertStatus = f.ConvertStatus,
                    ConvertMessage = f.ConvertMessage,
                    MarkdownStatus = f.MarkdownStatus,
                    MarkdownMessage = f.MarkdownMessage,
                    ExtractStatus = EnterpriseExtractStatus.ForDisplay(f.ExtractStatus),
                    ExtractState = ExtractStateRule.Of(f.Code, f.ExtractStatus, ruleMap, lockedFiles),
                    ExtractMessage = f.ExtractMessage,
                    Status = StatusOf(f),
                    UpdateTime = f.UpdateTime,
                    CreateTime = f.CreateTime
                };
            })
            .OrderBy(f => folderByCode.TryGetValue(f.FolderCode, out var fo) ? fo.SortOrder : 0)
            .ThenBy(f => f.FileName)
            .ToList();

        var required = files.Where(f => f.IsRequired).ToList();
        return new StandardDirectoryView
        {
            Configured = true,
            Message = null,
            EnterpriseCode = enterpriseCode,
            StageCode = stageCode,
            StandardCode = standardCode,
            StandardNo = std?.StandardCode,
            StandardName = std?.StandardName ?? standardCode,
            EnterpriseConfigCode = config.Code,
            TemplateConfigCode = ensure.TemplateConfigCode,
            Folders = folders,
            Files = files,
            Summary = new StandardDirectorySummary
            {
                TotalRequired = required.Count,
                // ★ Live 口径 = IsLive（在清单且有对象），与 stage-overview 严格一致；
                //   转换失败/Markdown 失败都仍算「文件在位」，只是状态标签不同。
                //   ⛔ 若按 Status 白名单枚举，markdownFailed 会既不算 Live 也不算 Missing ⇒ 三个数加不起来。
                Live = required.Count(f => f.Status is Status.Ready or Status.Converting or Status.Uploaded
                                              or Status.ConvertFailed or Status.MarkdownFailed),
                Ready = required.Count(f => f.Status == Status.Ready),
                Converting = required.Count(f => f.Status == Status.Converting),
                Missing = required.Count(f => f.Status is Status.Missing or Status.Removed)
            }
        };
    }

    #endregion

    #region 三、就位检查（保留：纯读对账，无写副作用）

    /// <summary>
    /// 文件检查报告（纯读）：只读企业槽位做对账，⛔ 不建任何行。
    /// <para>企业目录的唯一创建入口 = <see cref="InitEnterpriseDirectoryAsync"/>（关联钩子）/ ensure。</para>
    /// <para>★ 字段 PascalCase（AGENTS.md ③）：本端点曾是唯一的 camelCase 出口（configured/standardCode…），
    /// 与同控制器其余 20 个端点相反，现已统一。</para>
    /// </summary>
    public async Task<object> FileCheckAsync(string enterpriseCode, string stageCode)
    {
        var err = await OwnershipErrorAsync(enterpriseCode);
        if (err != null)
            return new { Configured = false, Message = err, Standards = new object[0], Summary = new { TotalRequired = 0, TotalUploaded = 0, TotalExtracted = 0, TotalMissing = 0 } };

        var configs = (await _db.GetListAsync<StandardDirectoryConfig>(
            x => x.EnterpriseCode == enterpriseCode && x.StageCode == stageCode && x.IsValid == 1)).Data ?? new();

        if (configs.Count == 0)
            return new
            {
                Configured = false,
                Message = "该机构未配置标准目录，请先在管理端维护（企业目录在关联标准时自动初始化）",
                Standards = new object[0],
                Summary = new { TotalRequired = 0, TotalUploaded = 0, TotalExtracted = 0, TotalMissing = 0 }
            };

        var configCodes = configs.Select(c => c.Code).ToList();
        // ★ includeDisabled=true：`IsValid=0` 且已有对象 = 「已移除」槽位，**仍属应上传清单**（缺失态），
        //   滤掉会让需求凭空消失且零提示。
        var allFiles = (await _db.GetListAsync<StandardDirectoryFile>(
            x => configCodes.Contains(x.ConfigCode), includeDisabled: true)).Data ?? new();

        var folderCodes = allFiles.Select(f => f.FolderCode).Where(c => !string.IsNullOrEmpty(c)).Distinct().ToList();
        var folderRows = folderCodes.Count == 0
            ? new List<StandardDirectoryFolder>()
            : (await _db.GetListAsync<StandardDirectoryFolder>(x => folderCodes.Contains(x.Code!))).Data
              ?? new List<StandardDirectoryFolder>();
        var folderNames = folderRows
            .Where(f => !string.IsNullOrEmpty(f.Code))
            .GroupBy(f => f.Code!)
            .ToDictionary(g => g.Key, g => g.First().FolderName ?? g.Key);

        var stdCodes = configs.Select(c => c.StandardCode).Distinct().ToList();
        var stdRows = (await _db.GetListAsync<ISOStandard>(x => stdCodes.Contains(x.Code!))).Data
                      ?? new List<ISOStandard>();
        var stdNames = stdRows
            .Where(s => !string.IsNullOrEmpty(s.Code))
            .GroupBy(s => s.Code!)
            .ToDictionary(g => g.Key, g => g.First().StandardName);

        object Slot(StandardDirectoryFile f) => new
        {
            f.Code, f.FileName, f.FolderCode,
            FolderName = folderNames.TryGetValue(f.FolderCode, out var fn) ? fn : "根目录",
            f.StandardFileCode, f.IsRequired, f.ExtractionEnabled, f.VersionNumber,
            f.IsValid, f.ConvertStatus, f.ExtractStatus, f.ExtractMessage,
            f.MaxConfidence, f.FileSize, f.StoragePath, f.PreviewPdfPath, f.MarkdownPath,
            Status = StatusOf(f)
        };

        var standards = configs.Select(cfg =>
        {
            // 只统计与页面同口径的可见行（草稿行由上传流程自己管，不进对账）
            var defs = allFiles
                .Where(f => f.ConfigCode == cfg.Code && (f.IsValid == 1 || !string.IsNullOrEmpty(f.StoragePath)))
                .ToList();
            var required = defs.Where(f => f.IsRequired).ToList();
            var uploaded = required.Where(IsLive).ToList();
            var missing = required.Where(f => !IsLive(f)).ToList();
            var extracted = required.Where(f => IsLive(f) && f.ExtractStatus == "completed").ToList();

            return new
            {
                StandardCode = cfg.StandardCode,
                StandardName = stdNames.TryGetValue(cfg.StandardCode, out var sn) ? sn : cfg.StandardCode,
                RequiredCount = required.Count,
                UploadedCount = uploaded.Count,
                ExtractedCount = extracted.Count,
                MissingCount = missing.Count,
                UploadedFiles = uploaded.Select(Slot).ToList(),
                MissingFiles = missing.Select(Slot).ToList(),
                Folders = defs
                    .GroupBy(f => folderNames.TryGetValue(f.FolderCode, out var fn2) ? fn2 : "根目录")
                    .Select(g => new
                    {
                        FolderName = g.Key,
                        Required = g.Count(f => f.IsRequired),
                        Uploaded = g.Count(f => f.IsRequired && IsLive(f)),
                        Extracted = g.Count(f => IsLive(f) && f.ExtractStatus == "completed"),
                        Files = g.Select(Slot).ToList()
                    })
                    .OrderBy(x => x.FolderName)
                    .ToList()
            };
        }).ToList();

        return new
        {
            Configured = true,
            Message = (string?)null,
            Standards = standards,
            Summary = new
            {
                TotalRequired = standards.Sum(s => s.RequiredCount),
                TotalUploaded = standards.Sum(s => s.UploadedCount),
                TotalExtracted = standards.Sum(s => s.ExtractedCount),
                TotalMissing = standards.Sum(s => s.MissingCount)
            }
        };
    }

    #endregion

    #region 四、多标准分发预览（需求 3 核心）

    /// <summary>
    /// 分发预览（纯计算，不落库不写对象）：把一次选择的上传项按<b>每个关联标准</b>独立匹配。
    /// <para>逐标准独立判定 —— 未命中该标准的文件<b>绝不进入</b>该标准（需求 3 硬约束）。</para>
    /// </summary>
    public async Task<object> PlanDispatchAsync(
        string enterpriseCode, string stageCode,
        IList<DispatchMatcher.IncomingFile> files)
    {
        var err = await OwnershipErrorAsync(enterpriseCode);
        if (err != null) return new { Configured = false, Message = err };

        if (string.IsNullOrWhiteSpace(stageCode))
            return new { Configured = false, Message = "请先选择认证阶段" };

        var links = (await _db.GetListAsync<CertEnterpriseStage>(
            x => x.EnterpriseCode == enterpriseCode && x.StageCode == stageCode)).Data
            ?? new List<CertEnterpriseStage>();
        var stdCodes = links.Where(l => l.IsValid == 1).Select(l => l.StandardCode).Distinct().ToList();

        var scopes = new List<DispatchMatcher.StandardScope>();
        var messages = new List<object>();

        foreach (var stdCode in stdCodes)
        {
            var ensure = await EnsureDirectoryAsync(enterpriseCode, null, stdCode, stageCode);
            var std = (await _db.GetOneAsync<ISOStandard>(x => x.Code == stdCode)).Data;
            if (ensure.Error != null)
            {
                messages.Add(new { StandardCode = stdCode, Message = ensure.Error });
                continue;
            }

            var tplFolders = (await _db.GetListAsync<StandardDirectoryFolder>(
                x => x.ConfigCode == ensure.TemplateConfigCode)).Data ?? new List<StandardDirectoryFolder>();
            var tplRoot = RootSegmentOf(tplFolders);
            var folderPathByCode = tplFolders
                .Where(f => !string.IsNullOrEmpty(f.Code))
                .GroupBy(f => f.Code!)
                .ToDictionary(g => g.Key, g => RelativePath(g.First().FullPath, tplRoot));

            var slots = (await _db.GetListAsync<StandardDirectoryFile>(
                x => x.ConfigCode == ensure.Config!.Code, includeDisabled: true)).Data
                ?? new List<StandardDirectoryFile>();

            scopes.Add(new DispatchMatcher.StandardScope
            {
                StandardCode = stdCode,
                StandardName = std?.StandardName ?? stdCode,
                Slots = slots
                    .Where(f => f.IsValid == 1)
                    .Select(f => new DispatchMatcher.TemplateSlot
                    {
                        SlotCode = f.Code,
                        FileName = f.FileName,
                        FolderCode = f.FolderCode ?? "",
                        FolderPath = folderPathByCode.TryGetValue(f.FolderCode ?? "", out var p) ? p : "",
                        IsRequired = f.IsRequired,
                        MaxSizeMB = f.MaxFileSizeMB,
                        Occupied = IsLive(f)
                    })
                    .ToList()
            });
        }

        var plan = DispatchMatcher.BuildPlan(files?.ToList() ?? new List<DispatchMatcher.IncomingFile>(), scopes);

        var nodeByCode = scopes.ToDictionary(s => s.StandardCode, s => s.StandardName);

        return new
        {
            Configured = true,
            Message = (string?)null,
            EnterpriseCode = enterpriseCode,
            StageCode = stageCode,
            Standards = scopes.Select(s => new
            {
                s.StandardCode,
                s.StandardName,
                Rows = plan.Rows.Where(r => r.StandardCode == s.StandardCode).Select(ProjectRow).ToList()
            }).ToList(),
            Unmatched = plan.Unmatched.Select(u => new
            {
                u.FileName, u.RelativePath, u.FileSize, u.Reason
            }).ToList(),
            Conflicts = plan.Conflicts.Select(c => new
            {
                c.StandardCode, c.StandardName, c.SlotCode, c.SlotFileName, c.FileNames
            }).ToList(),
            CrossStandard = plan.CrossStandard.Select(kv =>
            {
                // 聚合键是相对路径（同名不同目录是不同文件），对外只展示文件名
                var display = kv.Key.Replace('\\', '/');
                var slash = display.LastIndexOf('/');
                return new
                {
                    FileName = slash >= 0 ? display[(slash + 1)..] : display,
                    RelativePath = kv.Key,
                    Standards = kv.Value,
                    StandardCodes = scopes.Where(s => kv.Value.Contains(s.StandardName))
                                          .Select(s => s.StandardCode).ToList()
                };
            }).ToList(),
            Messages = messages,
            Summary = new
            {
                RowCount = plan.Rows.Count,
                ExecutableCount = plan.Rows.Count(r => r.BlockReason == null),
                BlockedCount = plan.Rows.Count(r => r.BlockReason != null),
                UnmatchedCount = plan.Unmatched.Count,
                ConflictCount = plan.Conflicts.Count,
                CrossStandardCount = plan.CrossStandard.Count,
                StandardCount = scopes.Count
            }
        };
    }

    private static object ProjectRow(DispatchMatcher.DispatchRow r) => new
    {
        r.FileName,
        r.RelativePath,
        r.FileSize,
        r.StandardCode,
        r.StandardName,
        r.SlotCode,
        r.SlotFileName,
        r.FolderCode,
        r.FolderPath,
        Level = r.Level.ToString(),
        r.NeedsConfirm,
        r.BlockReason
    };

    #endregion

    #region 五、四段式上传（槽位模式 / 指派模式）

    /// <summary>
    /// Step1：建上传任务 + 预建/重置目标行，下发 <c>TaskId</c> 与建议存储路径。
    ///
    /// <para><b>两种 item</b>：带 <c>SlotCode</c> = 槽位模式（落到标准模板已定义的文件位）；
    /// 不带 = 指派模式（D8 模板外文件，人工指派文件夹后落库，<c>StandardFileCode</c> 留空）。</para>
    ///
    /// <para><b>StoragePath 不在本步落库</b>（偏差，见 README §五）：01 §5.1 规定「StoragePath 空 = 缺失」，
    /// 本步只传计划路径给前端显示；真实写入在 Step2（<see cref="UploadFileStepAsync"/>）——
    /// 否则未传字节的槽位会被对账误判为「已就位」。</para>
    ///
    /// <para>★ <b>先全量校验、后统一写入</b>：旧实现是「校验一项写一项」，第 N 项失败时前 N-1 项
    /// 已经把槽位行改成 <c>TaskId/pending</c>，而 <c>UploadTask</c> 行在循环后才插 ⇒
    /// 半成品行既没有任务可 confirm 也没有对象可 cancel，只能等人工点「替换」覆盖。
    /// 现在校验阶段不落任何库，任一项不合法就整批拒绝（零副作用）。</para>
    /// </summary>
    public async Task<(string? Error, object? Data)> UploadInitAsync(
        string enterpriseCode, string stageCode, string standardCode,
        IList<UploadInitItem> items, bool? autoExtract = null)
    {
        var err = await OwnershipErrorAsync(enterpriseCode);
        if (err != null) return (err, null);
        if (items == null || items.Count == 0) return ("未指定待上传文件", null);
        if (string.IsNullOrWhiteSpace(standardCode) || string.IsNullOrWhiteSpace(stageCode))
            return ("缺少标准或阶段编码", null);

        var ensure = await EnsureDirectoryAsync(enterpriseCode, null, standardCode, stageCode);
        if (ensure.Error != null) return (ensure.Error, null);
        var config = ensure.Config!;

        // 队列互斥：同标准目录已有运行中队列 ⇒ 拒绝（04 §一 Step1-2）
        var running = await _queueManager.FindRunningQueueByScopeKeyAsync(config.Code);
        if (running != null)
            return ($"该标准目录正在执行转换队列（{running.QueueCode}），请等待完成后再上传", null);

        var tplFolders = (await _db.GetListAsync<StandardDirectoryFolder>(
            x => x.ConfigCode == ensure.TemplateConfigCode)).Data ?? new List<StandardDirectoryFolder>();
        var rootSegment = RootSegmentOf(tplFolders);
        var folderByCode = tplFolders.Where(f => !string.IsNullOrEmpty(f.Code))
            .GroupBy(f => f.Code!).ToDictionary(g => g.Key, g => g.First());

        string RelFolderPathOf(string? folderCode) =>
            folderCode != null && folderByCode.TryGetValue(folderCode, out var fr)
                ? RelativePath(fr.FullPath, rootSegment)
                : "";

        var allSlots = (await _db.GetListAsync<StandardDirectoryFile>(
            x => x.ConfigCode == config.Code, includeDisabled: true)).Data ?? new List<StandardDirectoryFile>();

        // ── 阶段 1：只读校验 + 生成计划（不落库） ──
        var plan = new List<InitPlanItem>();
        var reservedFullPaths = new HashSet<string>(StringComparer.Ordinal);
        var totalSize = 0L;

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.FileName)) return ("文件名不能为空", null);
            totalSize += item.FileSize;

            if (!string.IsNullOrWhiteSpace(item.SlotCode))
            {
                // ── 槽位模式 ──
                var slot = allSlots.FirstOrDefault(f => f.Code == item.SlotCode);
                if (slot == null) return ($"目标槽位不存在：{item.SlotCode}", null);
                if (slot.IsValid == 1 && !string.IsNullOrEmpty(slot.StoragePath))
                    return ($"槽位「{slot.FileName}」已有就位文件，请使用「替换」", null);
                if (slot.IsValid != 1 && !string.IsNullOrEmpty(slot.StoragePath))
                    return ($"槽位「{slot.FileName}」已被移除，请先在版本面板恢复", null);
                // ★ 模板单文件上限（plan 已出 BlockReason，这里是绕过 plan 直调 init 时的兜底）
                if (slot.MaxFileSizeMB > 0 && item.FileSize > (long)slot.MaxFileSizeMB * 1024 * 1024)
                    return ($"「{slot.FileName}」超过模板单文件上限 {slot.MaxFileSizeMB}MB", null);
                // 同一批次里重复指向同一槽位 ⇒ 后者必失败，直接拒
                if (plan.Any(p => p.Mode == "slot" && p.Slot?.Code == slot.Code))
                    return ($"槽位「{slot.FileName}」在本次上传中被重复指定", null);
                // ★ 扩展名族兼容兜底（04 §4.1 A 方案）：plan 已把异族文件挡在未归属（ext_mismatch），
                //   这里是绕过 plan 直调 init 的最后一道（同 MaxFileSizeMB）。任一侧无扩展名放行。
                if (!DispatchMatcher.ExtCompatible(item.FileName, slot.FileName))
                {
                    var want = Path.GetExtension(slot.FileName);
                    return ($"「{item.FileName}」扩展名与槽位「{slot.FileName}」不兼容（模板要求 {want}），请上传 {want} 或同族格式", null);
                }

                plan.Add(new InitPlanItem
                {
                    Mode = "slot",
                    Slot = slot,
                    FolderPath = RelFolderPathOf(slot.FolderCode),
                    FileName = item.FileName
                });
            }
            else
            {
                // ── 指派模式（D8：模板外文件，FolderCode 须为模板 folder）──
                if (string.IsNullOrWhiteSpace(item.FolderCode) || !folderByCode.ContainsKey(item.FolderCode))
                    return ("指派文件夹不存在（必须选择该标准模板内的文件夹）", null);

                var folder = folderByCode[item.FolderCode];
                var relFolder = RelativePath(folder.FullPath, rootSegment);
                var fullPath = string.IsNullOrEmpty(relFolder) ? item.FileName : $"{relFolder}/{item.FileName}";
                if (allSlots.Any(f => f.FullPath == fullPath) || !reservedFullPaths.Add(fullPath))
                    return ($"该文件夹下已存在同名文件「{item.FileName}」，请改用替换", null);

                plan.Add(new InitPlanItem
                {
                    Mode = "assign",
                    FolderCode = item.FolderCode,
                    FolderPath = relFolder,
                    FileName = item.FileName,
                    FileType = System.IO.Path.GetExtension(item.FileName).TrimStart('.').ToLowerInvariant(),
                    FullPath = fullPath
                });
            }
        }

        // ── 阶段 2：全部校验通过，统一写入 ──
        var taskId = Guid.NewGuid().ToString("N");
        var results = new List<object>();

        foreach (var p in plan)
        {
            if (p.Mode == "slot")
            {
                var slot = p.Slot!;
                slot.TaskId = taskId;
                slot.UploadStatus = "pending";
                slot.IsValid = 1;
                slot.IsDeleted = false;
                slot.UpdateBy = _user.UserCode;
                slot.UpdateTime = System.DateTime.Now;
                await _db.UpdateAsync(slot,
                    nameof(StandardDirectoryFile.TaskId), nameof(StandardDirectoryFile.UploadStatus),
                    nameof(StandardDirectoryFile.IsValid), nameof(StandardDirectoryFile.IsDeleted),
                    nameof(StandardDirectoryFile.UpdateBy), nameof(StandardDirectoryFile.UpdateTime));

                results.Add(new
                {
                    FileCode = slot.Code,
                    Mode = "slot",
                    slot.FileName,
                    slot.FolderCode,
                    FolderPath = p.FolderPath,
                    UploadFileName = p.FileName,
                    StoragePath = PathBuilder.EnterpriseFile(enterpriseCode, standardCode, stageCode, p.FolderPath, p.FileName)
                });
            }
            else
            {
                var row = new StandardDirectoryFile
                {
                    Code = Guid.NewGuid().ToString("N"),
                    ConfigCode = config.Code,
                    EnterpriseCode = enterpriseCode,
                    StandardCode = standardCode,
                    StageCode = stageCode,
                    FolderCode = p.FolderCode,
                    StandardFileCode = null,
                    FileName = p.FileName,
                    FileType = p.FileType,
                    FullPath = p.FullPath,
                    IsRequired = false,
                    IsValid = 0,
                    IsDeleted = false,
                    UploadStatus = "pending",
                    ExtractStatus = "none",
                    VersionNumber = 1,
                    TaskId = taskId,
                    CreateBy = _user.UserCode,
                    CreateTime = System.DateTime.Now
                };
                await _db.InsertAsync(row);
                allSlots.Add(row);

                results.Add(new
                {
                    FileCode = row.Code,
                    Mode = "assign",
                    row.FileName,
                    row.FolderCode,
                    FolderPath = p.FolderPath,
                    UploadFileName = p.FileName,
                    StoragePath = PathBuilder.EnterpriseFile(enterpriseCode, standardCode, stageCode, p.FolderPath, p.FileName)
                });
            }
        }

        var task = new UploadTask
        {
            Code = Guid.NewGuid().ToString("N"),
            TaskId = taskId,
            ConfigCode = config.Code,
            TotalFiles = results.Count,
            TotalSize = totalSize,
            Status = "initialized",
            // D4：缺省自动提取
            AutoExtract = autoExtract == false ? 0 : 1,
            ExpireTime = System.DateTime.Now.AddMinutes(TaskExpireMinutes),
            IsValid = 1,
            IsDeleted = false,
            CreateBy = _user.UserCode,
            CreateTime = System.DateTime.Now
        };
        await _db.InsertAsync(task);

        return (null, new
        {
            TaskId = taskId,
            ConfigCode = config.Code,
            EnterpriseCode = enterpriseCode,
            StageCode = stageCode,
            StandardCode = standardCode,
            TotalFiles = results.Count,
            TotalSize = totalSize,
            ExpireTime = task.ExpireTime,
            Items = results
        });
    }

    /// <summary>Step1 的中间计划项（阶段 1 产出，阶段 2 才落库）</summary>
    private sealed class InitPlanItem
    {
        /// <summary>slot = 落到模板槽位；assign = 模板外文件（<c>StandardFileCode</c> 留空）</summary>
        public string Mode { get; init; } = "";
        /// <summary>槽位模式下命中的企业槽位行</summary>
        public StandardDirectoryFile? Slot { get; init; }
        /// <summary>指派模式的目标文件夹（模板 folder Code）</summary>
        public string? FolderCode { get; init; }
        /// <summary>相对配置根的文件夹路径</summary>
        public string FolderPath { get; init; } = "";
        /// <summary>实际上传文件名（槽位模式下与槽位标准名可以不同）</summary>
        public string FileName { get; init; } = "";
        public string? FileType { get; init; }
        /// <summary>相对配置根的完整路径（同文件夹判重用）</summary>
        public string? FullPath { get; init; }
    }

    /// <summary>
    /// Step2：逐文件传字节。按 DB 记录（而非前端传参）重算存储路径，防伪造。
    /// <para>单文件失败不整体中断；返回 <c>(ok, error)</c> 供前端收集重试。</para>
    /// </summary>
    public async Task<(bool Ok, string? Error)> UploadFileStepAsync(
        string fileCode, string taskId, Stream stream, long size, string uploadFileName)
    {
        var row = await GetDraftRowAsync(fileCode, taskId);
        if (row == null) return (false, "上传项不存在或已失效，请重新发起上传");

        var tenantErr = await OwnershipErrorAsync(row.EnterpriseCode);
        if (tenantErr != null) return (false, tenantErr);

        // ★ 模板单文件上限兜底：plan 阶段出 BlockReason、init 阶段也校验，这里是最后一道
        //   （客户端可绕过前端直调 upload/file；不然 MaxFileSizeMB 只是摆设）
        if (row.MaxFileSizeMB > 0 && size > (long)row.MaxFileSizeMB * 1024 * 1024)
            return (false, $"「{row.FileName}」超过模板单文件上限 {row.MaxFileSizeMB}MB，请压缩后重试");

        var folderPath = FolderPathOf(row);
        var name = string.IsNullOrWhiteSpace(uploadFileName) ? row.FileName : Path.GetFileName(uploadFileName);
        var storagePath = PathBuilder.EnterpriseFile(
            row.EnterpriseCode, row.StandardCode, row.StageCode, folderPath, name);

        try
        {
            await _storage.UploadAsync(storagePath.TrimStart('/'), stream, size);
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "[UploadFile] 对象上传失败: {FileCode}", fileCode);
            return (false, $"上传失败：{ex.Message}");
        }

        row.StoragePath = storagePath;
        row.FileSize = size;
        row.UploadStatus = "uploading";
        row.UpdateBy = _user.UserCode;
        row.UpdateTime = System.DateTime.Now;
        await _db.UpdateAsync(row,
            nameof(StandardDirectoryFile.StoragePath), nameof(StandardDirectoryFile.FileSize),
            nameof(StandardDirectoryFile.UploadStatus), nameof(StandardDirectoryFile.UpdateBy),
            nameof(StandardDirectoryFile.UpdateTime));

        var task = await GetTaskAsync(taskId);
        if (task != null)
        {
            task.SuccessCount++;
            await _db.UpdateAsync(task, nameof(UploadTask.SuccessCount));
        }

        return (true, null);
    }

    /// <summary>
    /// Step3：激活（<c>IsValid 0→1</c>、<c>UploadStatus=uploaded</c>）+ 入 <c>file_convert</c> 队列。
    /// <para>入队失败<b>不阻断激活</b>（R5）：返回 <c>ok=true</c> + <c>QueueError</c> 文案。</para>
    /// </summary>
    public async Task<(string? Error, object? Data)> UploadConfirmAsync(
        string taskId, string enterpriseCode, bool? autoExtract = null)
    {
        var err = await OwnershipErrorAsync(enterpriseCode);
        if (err != null) return (err, null);

        var task = await GetTaskAsync(taskId);
        if (task == null) return ("上传任务不存在或已失效", null);
        if (task.Status != "initialized") return ($"上传任务状态为 {task.Status}，无法确认", null);
        if (task.ExpireTime.HasValue && task.ExpireTime.Value < System.DateTime.Now)
            return ("上传任务已过期，请重新发起上传", null);

        var rows = (await _db.GetListAsync<StandardDirectoryFile>(
            x => x.TaskId == taskId, includeDisabled: true)).Data ?? new List<StandardDirectoryFile>();
        if (rows.Count == 0) return ("上传任务下没有待激活的文件", null);

        var missingBytes = rows.Where(r => string.IsNullOrEmpty(r.StoragePath)).ToList();
        if (missingBytes.Count > 0)
            return ($"有 {missingBytes.Count} 个文件尚未上传完成：{string.Join("、", missingBytes.Take(3).Select(r => r.FileName))}", null);

        var activatable = new List<StandardDirectoryFile>();
        foreach (var row in rows)
        {
            row.IsValid = 1;
            row.IsDeleted = false;
            row.UploadStatus = "uploaded";
            row.TaskId = null;
            if (NeedsConversion(row.FileName))
            {
                row.ConvertStatus = "pending";
                row.MarkdownStatus = "pending";
                activatable.Add(row);
            }
            row.UpdateBy = _user.UserCode;
            row.UpdateTime = System.DateTime.Now;
            await _db.UpdateAsync(row,
                nameof(StandardDirectoryFile.IsValid), nameof(StandardDirectoryFile.IsDeleted),
                nameof(StandardDirectoryFile.UploadStatus), nameof(StandardDirectoryFile.TaskId),
                nameof(StandardDirectoryFile.ConvertStatus), nameof(StandardDirectoryFile.MarkdownStatus),
                nameof(StandardDirectoryFile.UpdateBy), nameof(StandardDirectoryFile.UpdateTime));
        }

        // 草稿文件夹行激活（槽位模式无草稿文件夹；指派模式沿用模板文件夹，故通常为空 —— 保留以防将来扩展）
        var draftFolders = (await _db.GetListAsync<StandardDirectoryFolder>(
            x => x.TaskId == taskId, includeDisabled: true)).Data ?? new List<StandardDirectoryFolder>();
        foreach (var folder in draftFolders)
        {
            folder.IsValid = 1;
            folder.IsDeleted = false;
            folder.TaskId = null;
            folder.UpdateBy = _user.UserCode;
            folder.UpdateTime = System.DateTime.Now;
            await _db.UpdateAsync(folder,
                nameof(StandardDirectoryFolder.IsValid), nameof(StandardDirectoryFolder.IsDeleted),
                nameof(StandardDirectoryFolder.TaskId), nameof(StandardDirectoryFolder.UpdateBy),
                nameof(StandardDirectoryFolder.UpdateTime));
        }

        // D4：确认阶段允许覆盖 init 的开关；未传则沿用落库值
        if (autoExtract.HasValue) task.AutoExtract = autoExtract.Value ? 1 : 0;

        string? queueCode = null;
        string? queueError = null;
        if (activatable.Count > 0)
            (queueCode, queueError) = await EnqueueConvertQueueAsync(
                task.ConfigCode, activatable, "upload_task", taskId, task.AutoExtract == 1);

        task.Status = "completed";
        task.UpdateBy = _user.UserCode;
        task.UpdateTime = System.DateTime.Now;
        await _db.UpdateAsync(task,
            nameof(UploadTask.Status), nameof(UploadTask.AutoExtract),
            nameof(UploadTask.UpdateBy), nameof(UploadTask.UpdateTime));

        foreach (var row in rows)
            await WriteOpLogAsync(row.EnterpriseCode, row.StageCode, row.Code, "upload", row.VersionNumber,
                new { row.FileName, row.FileSize, StoragePath = row.StoragePath, TaskId = taskId });

        return (null, new
        {
            TaskId = taskId,
            ActivatedCount = rows.Count,
            ConvertCount = activatable.Count,
            QueueCode = queueCode,
            QueueError = queueError
        });
    }

    /// <summary>
    /// Step4：回滚 —— 取消队列、删已传对象、草稿行硬删、槽位行复位。
    /// </summary>
    public async Task<(string? Error, object? Data)> UploadCancelAsync(string taskId, string enterpriseCode)
    {
        var err = await OwnershipErrorAsync(enterpriseCode);
        if (err != null) return (err, null);

        var task = await GetTaskAsync(taskId);
        if (task == null) return ("上传任务不存在或已失效", null);
        if (task.Status != "initialized") return ($"上传任务状态为 {task.Status}，无需取消", null);

        // 取消关联队列（TaskId 即队列任务的 TaskId）
        await _queueManager.CancelBatchAsync(taskId);

        var rows = (await _db.GetListAsync<StandardDirectoryFile>(
            x => x.TaskId == taskId, includeDisabled: true)).Data ?? new List<StandardDirectoryFile>();

        int removedObjects = 0, deletedRows = 0, resetRows = 0;
        foreach (var row in rows)
        {
            if (!string.IsNullOrEmpty(row.StoragePath))
            {
                try
                {
                    await _storage.DeleteAsync(row.StoragePath.TrimStart('/'));
                    removedObjects++;
                }
                catch (System.Exception ex)
                {
                    _logger.LogWarning(ex, "[UploadCancel] 对象删除失败（不阻断）: {Path}", row.StoragePath);
                }
            }

            var isAssignDraft = row.IsValid != 1 && string.IsNullOrEmpty(row.StandardFileCode);
            if (isAssignDraft)
            {
                await _db.Client.Deleteable<StandardDirectoryFile>().Where(x => x.Code == row.Code).ExecuteCommandAsync();
                deletedRows++;
            }
            else
            {
                row.StoragePath = null;
                row.FileSize = null;
                row.UploadStatus = null;
                row.TaskId = null;
                row.IsValid = 1;
                row.UpdateBy = _user.UserCode;
                row.UpdateTime = System.DateTime.Now;
                await _db.UpdateAsync(row,
                    nameof(StandardDirectoryFile.StoragePath), nameof(StandardDirectoryFile.FileSize),
                    nameof(StandardDirectoryFile.UploadStatus), nameof(StandardDirectoryFile.TaskId),
                    nameof(StandardDirectoryFile.IsValid), nameof(StandardDirectoryFile.UpdateBy),
                    nameof(StandardDirectoryFile.UpdateTime));
                resetRows++;
            }
        }

        task.Status = "cancelled";
        task.UpdateBy = _user.UserCode;
        task.UpdateTime = System.DateTime.Now;
        await _db.UpdateAsync(task,
            nameof(UploadTask.Status), nameof(UploadTask.UpdateBy), nameof(UploadTask.UpdateTime));

        return (null, new
        {
            TaskId = taskId,
            DeletedRows = deletedRows,
            ResetRows = resetRows,
            RemovedObjects = removedObjects
        });
    }

    #endregion

    #region 六、替换 / 移除 / 恢复 / 版本 / 历史

    /// <summary>槽位「有效已上传」判据：有存储对象 <b>且</b> 未被移除。</summary>
    private static bool IsLive(StandardDirectoryFile f) =>
        f.IsValid == 1 && !string.IsNullOrEmpty(f.StoragePath);

    /// <summary>
    /// 文件就位状态（01 分册 §5.1 唯一权威）。
    /// <para>判定顺序刻意如此：<c>StoragePath</c> 空 = 缺失（最高优先，草稿行也归此）；
    /// 上传中次之（草稿行传完字节但未 confirm 时不得误判为「已移除」）；再判移除；最后判转换态。</para>
    /// <para>★ <c>markdownFailed</c> 单独成态：实测 <c>.doc</c> 文件 PDF 产物成功、Markdown 产物恒失败
    /// （旧版 6 个已就位行里 3 个如此）。旧逻辑只判 <c>ConvertStatus</c>，这类行仍显示绿色「已就绪」，
    /// 而「提取」按钮只看 <c>ConvertStatus==='completed'</c> 可点、后端却因缺 <c>MarkdownPath</c> 直接拒收。</para>
    /// </summary>
    private static string StatusOf(StandardDirectoryFile f)
    {
        if (string.IsNullOrEmpty(f.StoragePath)) return Status.Missing;
        var up = f.UploadStatus ?? "";
        if (up is "pending" or "uploading") return Status.Uploading;
        if (f.IsValid != 1) return Status.Removed;
        var conv = f.ConvertStatus ?? "";
        var md = f.MarkdownStatus ?? "";
        if (conv is "pending" or "converting" || md is "pending" or "converting") return Status.Converting;
        if (conv == "failed") return Status.ConvertFailed;
        if (conv == "completed")
        {
            // PDF 有了但 Markdown 没有（失败或压根没产出）⇒ 提取/NC 链路不可用，不报「已就绪」
            return string.IsNullOrEmpty(f.MarkdownPath) ? Status.MarkdownFailed : Status.Ready;
        }
        return Status.Uploaded;
    }

    /// <summary>写路径不变量守卫：企业行写操作必须携带真实企业 Code，禁写模板域（虚拟企业 Code）。</summary>
    private static string? ValidateWritableEnterprise(string enterpriseCode)
        => string.IsNullOrWhiteSpace(enterpriseCode)
            ? "更新失败：缺少业务键 EnterpriseCode"
            : enterpriseCode == YzhVirtualEnterprise.Code
                ? "更新失败：禁止写入标准模板域（虚拟企业 Code）"
                : null;

    /// <summary>
    /// G-3a 替换：归档旧件 → 覆盖上传新件 → 作废产物 → 重新入队。
    /// <para>锁定方案（README §五 偏差 D-替换）：沿用原路径覆盖 + 归档旧件到 <c>_archive/</c>，
    /// 保持外部引用（预览/提取/NC）路径稳定。</para>
    /// </summary>
    /// <remarks>★ 2026-09-30：返回 <c>Result&lt;object?&gt;</c> 而非 <c>Result</c> ——
    /// 携带「人工补录值被归档」的提示载荷（见 <c>ArchiveManualNoticeAsync</c>），
    /// 控制器把它塞进 <c>ApiResponse.data</c>，前端据此弹窗。</remarks>
    public async Task<Result<object?>> ReplaceFileAsync(
        string fileCode, string enterpriseCode, Stream fileStream, long fileSize, string fileName,
        string? reason = null, System.DateTime? expectedModifyTime = null, bool? autoExtract = null)
    {
        var guard = ValidateWritableEnterprise(enterpriseCode);
        if (guard != null) return Result<object?>.Fail(guard);
        var tenantErr = await OwnershipErrorAsync(enterpriseCode);
        if (tenantErr != null) return Result<object?>.Fail(tenantErr);
        if (string.IsNullOrWhiteSpace(fileName)) return Result<object?>.Fail("替换失败：文件名为空");

        var fr = await _db.GetOneIgnoreValidAsync<StandardDirectoryFile>(
            x => x.Code == fileCode && x.EnterpriseCode == enterpriseCode);
        var file = fr.Data;
        if (file == null) return Result<object?>.Fail("文件不存在");
        if (file.IsValid != 1) return Result<object?>.Fail("文件已移除，请先在版本面板恢复后再替换");
        if (string.IsNullOrEmpty(file.StoragePath)) return Result<object?>.Fail("替换失败：文件未上传，请先在「上传文件」入口补齐");

        var conflict = CheckModifyConflict(file, expectedModifyTime);
        if (conflict != null) return Result<object?>.Fail(conflict);

        // 队列锁检查（04 §5.2-2）：该文件正在转换 ⇒ 拒绝
        var lockHit = await _queueManager.FindResourceLockAsync(
            QueueManager.RESOURCE_FILE, new List<string> { fileCode });
        if (lockHit != null)
            return Result<object?>.Fail($"该文件正在转换队列中（{lockHit.QueueCode}），请等待完成后再替换");
        var dirErr = await ConfigLockErrorAsync(file.ConfigCode);
        if (dirErr != null) return Result<object?>.Fail(dirErr);

        // 扩展名族兼容校验（04 §5.2-3）
        var newExt = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
        var oldExt = (file.FileType ?? "").TrimStart('.').ToLowerInvariant();
        if (oldExt.Length > 0 && newExt.Length > 0 && newExt != oldExt && !IsSameFamily(newExt, oldExt))
            return Result<object?>.Fail($"新文件扩展名 .{newExt} 与槽位定义 .{oldExt} 不兼容，请确认后重试");

        var archivedVersion = await GetArchivedVersionNumberAsync(fileCode);
        // 路径策略（04 §5.1 锁定）：沿用原路径覆盖 ⇒ 外部引用（预览/提取/NC）路径稳定
        var newStoragePath = file.StoragePath!;
        var archivePath = PathBuilder.Archive(file.StoragePath!, archivedVersion);
        var oldSource = file.StoragePath!.TrimStart('/');
        var samePath = string.Equals(newStoragePath, file.StoragePath, StringComparison.Ordinal);

        if (samePath)
        {
            try { await _storage.RenameAsync(oldSource, archivePath.TrimStart('/')); }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "[ReplaceFile] 归档失败（同名路径，源未动）: {FileCode}", fileCode);
                return Result<object?>.Fail($"归档失败，替换中止：{ex.Message}");
            }
            try { await _storage.UploadAsync(newStoragePath.TrimStart('/'), fileStream, fileSize); }
            catch (System.Exception ex)
            {
                try { await _storage.RenameAsync(archivePath.TrimStart('/'), oldSource); }
                catch (System.Exception rex) { _logger.LogError(rex, "[ReplaceFile] 补偿回滚失败: {Archive}", archivePath); }
                return Result<object?>.Fail($"上传失败（已回滚归档）：{ex.Message}");
            }
        }
        else
        {
            try { await _storage.UploadAsync(newStoragePath.TrimStart('/'), fileStream, fileSize); }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "[ReplaceFile] 上传失败: {FileCode}", fileCode);
                return Result<object?>.Fail($"上传失败：{ex.Message}");
            }
            try { await _storage.RenameAsync(oldSource, archivePath.TrimStart('/')); }
            catch (System.Exception ex)
            {
                try { await _storage.DeleteAsync(newStoragePath.TrimStart('/')); }
                catch (System.Exception dex) { _logger.LogError(dex, "[ReplaceFile] 补偿删除新对象失败: {Path}", newStoragePath); }
                _logger.LogError(ex, "[ReplaceFile] 归档失败（新对象已删除，状态未变）: {FileCode}", fileCode);
                return Result<object?>.Fail($"归档失败，替换中止：{ex.Message}");
            }
        }

        // 产物（PDF/MD）随源归档，best-effort；R1：PreviewPdfPath==StoragePath 的透传产物跳过
        foreach (var prod in new[] { file.PreviewPdfPath, file.MarkdownPath }
                     .Where(p => !string.IsNullOrEmpty(p) && !string.Equals(p, file.StoragePath, StringComparison.Ordinal)))
        {
            try
            {
                var prodArchive = PathBuilder.Archive(prod!, archivedVersion);
                if (await _storage.ExistsAsync(prod!.TrimStart('/')))
                    await _storage.RenameAsync(prod!.TrimStart('/'), prodArchive.TrimStart('/'));
            }
            catch (System.Exception ex) { _logger.LogWarning(ex, "[ReplaceFile] 产物归档失败（不阻断）: {Prod}", prod); }
        }

        await _db.InsertAsync(new EnterpriseFileVersion
        {
            Code = Guid.NewGuid().ToString("N"),
            FileCode = fileCode, EnterpriseCode = enterpriseCode,
            VersionNumber = archivedVersion, FileName = file.FileName,
            StoragePath = archivePath, FileSize = file.FileSize ?? 0,
            Reason = string.IsNullOrWhiteSpace(reason) ? "用户上传新文件覆盖" : reason
        });

        file.StoragePath = newStoragePath;
        file.FileSize = fileSize;
        file.FileName = fileName;
        file.FileType = newExt;
        file.VersionNumber = archivedVersion + 1;
        file.UploadStatus = "uploaded";
        file.ConvertStatus = "pending";
        file.MarkdownStatus = "pending";
        file.ConvertedStoragePath = null;
        file.ExtractStatus = "none";
        file.MaxConfidence = null;
        file.ConvertMessage = null;
        file.MarkdownMessage = null;
        file.PreviewPdfPath = null;
        file.MarkdownPath = null;
        file.UpdateBy = _user.UserCode;
        file.UpdateTime = System.DateTime.Now;
        await _db.UpdateAsync(file,
            nameof(StandardDirectoryFile.StoragePath), nameof(StandardDirectoryFile.FileSize),
            nameof(StandardDirectoryFile.FileName), nameof(StandardDirectoryFile.FileType),
            nameof(StandardDirectoryFile.VersionNumber), nameof(StandardDirectoryFile.UploadStatus),
            nameof(StandardDirectoryFile.ConvertStatus), nameof(StandardDirectoryFile.MarkdownStatus),
            nameof(StandardDirectoryFile.ConvertedStoragePath), nameof(StandardDirectoryFile.ExtractStatus),
            nameof(StandardDirectoryFile.MaxConfidence), nameof(StandardDirectoryFile.ConvertMessage),
            nameof(StandardDirectoryFile.MarkdownMessage), nameof(StandardDirectoryFile.PreviewPdfPath),
            nameof(StandardDirectoryFile.MarkdownPath), nameof(StandardDirectoryFile.UpdateBy),
            nameof(StandardDirectoryFile.UpdateTime));

        // ★ S2③ 图 4：替换 = 版本 n+1 ⇒ 归档 ≤新版本的全部活跃结果（只翻 IsValid，行数不减）
        //    验收③：替换后读取口径必须为空，直至新版本重新提取完成
        //
        // ★ 2026-09-30 改按 RuleCode 归档（用户裁决 J4：1 文件 = 1 规则）。
        //   原按 FileCode 归档会【漏掉人工补录行】—— 补录行的 FileCode 填的是规则声明的
        //   StandardFileCode，不是文件槽位 Code。详见 EnterpriseExtractionResultStore.InvalidateByRuleAsync。
        var replaceRuleCode = await ResolveRuleCodeAsync(file.StandardFileCode);
        List<ExtractionResult> lostFields = new();
        List<TableExtractionResult> lostTables = new();

        if (!string.IsNullOrEmpty(replaceRuleCode))
        {
            // ★ 归档【前】先捞出人工补录值 —— 裁决 J4 的连带后果：按 RuleCode 清理会把
            //   同一规则下的人工补录值一起归档。覆盖文件 = 该规则数据整体重算，
            //   专家之前补的值会消失（05 号 §5.3 要求留痕 + 提示）。
            (lostFields, lostTables) =
                await _resultStore.ListArchivedManualAsync(enterpriseCode, replaceRuleCode);

            await _resultStore.InvalidateByRuleAsync(enterpriseCode, replaceRuleCode, file.VersionNumber);
        }
        else
        {
            // 该文件没配提取规则 ⇒ 回退到按文件归档（保持旧行为，不丢结果）
            await _resultStore.InvalidateActiveAsync(enterpriseCode, fileCode, file.VersionNumber);
        }

        await WriteOpLogAsync(enterpriseCode, file.StageCode, fileCode, "replace", file.VersionNumber,
            new { fileName, fileSize, reason, archivePath });

        // ★ 人工补录值被归档 ⇒ 记 change_log（archive）+ 返回提示，前端弹窗问专家是否重新补录
        var lostNotice = await ArchiveManualNoticeAsync(
            enterpriseCode, replaceRuleCode, lostFields, lostTables, fileName);

        if (NeedsConversion(fileName))
        {
            // R10：替换默认也自动提取（D4），前端可传 AutoExtract=false 关闭
            // ★ uk_source(SourceType, SourceId) 唯一：同一文件反复替换会撞唯一键 ⇒ 入队静默失败（只 warn），
            //   表现为「替换成功但不转换/不提取」。带版本号使每次替换 SourceId 唯一。
            var (queueCode, queueError) = await EnqueueConvertQueueAsync(
                file.ConfigCode, new List<StandardDirectoryFile> { file }, "file_replace",
                $"{fileCode}:v{file.VersionNumber}", autoExtract ?? true);
            if (queueError != null)
                _logger.LogWarning("[ReplaceFile] 入队失败（不阻断替换）: {FileCode} {Reason}", fileCode, queueError);
            else
                _logger.LogInformation("[ReplaceFile] 已入队 {QueueCode}: {FileCode}", queueCode, fileCode);
        }

        return Result<object?>.Ok(lostNotice);
    }

    /// <summary>扩展名族：doc≡docx、xls≡xlsx、ppt≡pptx、jpg≡jpeg 视为兼容（DispatchMatcher 同口径）</summary>
    private static bool IsSameFamily(string a, string b)
        => DispatchMatcher.NormalizeExt("x." + a) == DispatchMatcher.NormalizeExt("x." + b);

    /// <summary>
    /// 移除文件（处置而非毁灭，04 §六 偏差）：<b>MinIO 对象原地保留</b>，只置 <c>IsValid=0</c>。
    /// <para>⛔ 不置 <c>IsDeleted</c>：本行同时是「应上传槽位」定义行，软删会被全局过滤器从所有查询里抹掉
    /// ⇒ 需求凭空消失、版本面板再也打不开。移除语义 = 撤回该文件（清单留一行「已移除」，可恢复）。</para>
    /// </summary>
    public async Task<Result> DeleteFileAsync(string fileCode, string enterpriseCode, string? reason = null, System.DateTime? expectedModifyTime = null)
    {
        var guard = ValidateWritableEnterprise(enterpriseCode);
        if (guard != null) return Result.Fail(guard);
        var tenantErr = await OwnershipErrorAsync(enterpriseCode);
        if (tenantErr != null) return Result.Fail(tenantErr);

        var fr = await _db.GetOneAsync<StandardDirectoryFile>(
            x => x.Code == fileCode && x.EnterpriseCode == enterpriseCode && x.IsValid == 1);
        var file = fr.Data;
        if (file == null) return Result.Fail("文件不存在");
        var conflict = CheckModifyConflict(file, expectedModifyTime);
        if (conflict != null) return Result.Fail(conflict);

        var lockHit = await _queueManager.FindResourceLockAsync(
            QueueManager.RESOURCE_FILE, new List<string> { fileCode });
        if (lockHit != null)
            return Result.Fail($"该文件正在转换队列中（{lockHit.QueueCode}），请等待完成后再移除");

        file.IsValid = 0;
        file.UpdateBy = _user.UserCode;
        file.UpdateTime = System.DateTime.Now;
        await _db.UpdateAsync(file,
            nameof(StandardDirectoryFile.IsValid), nameof(StandardDirectoryFile.UpdateBy),
            nameof(StandardDirectoryFile.UpdateTime));

        // ★ S2③ 图 4：移除 ⇒ 归档该文件全部活跃结果（读取口径为空；只翻 IsValid，恢复时可回活）
        // ★ 2026-09-30 改按 RuleCode 归档（用户裁决 J4），同 ReplaceFileAsync
        var removeRuleCode = await ResolveRuleCodeAsync(file.StandardFileCode);
        if (!string.IsNullOrEmpty(removeRuleCode))
            await _resultStore.InvalidateByRuleAsync(enterpriseCode, removeRuleCode, file.VersionNumber);
        else
            await _resultStore.InvalidateActiveAsync(enterpriseCode, fileCode, file.VersionNumber);

        await WriteOpLogAsync(enterpriseCode, file.StageCode, fileCode, "delete", file.VersionNumber,
            new { fileName = file.FileName, storagePath = file.StoragePath, reason });
        return Result.Ok();
    }

    /// <summary>
    /// 从归档版本恢复：归档件 Copy 回当前路径（归档原件保留），单调新版本 n+1，重入转换链。
    /// <para>⛔ 不做版本号回拨 —— 历史链只读。</para>
    /// </summary>
    public async Task<Result> RestoreFileAsync(string fileCode, string enterpriseCode, int versionNumber, string? reason = null)
    {
        var guard = ValidateWritableEnterprise(enterpriseCode);
        if (guard != null) return Result.Fail(guard);
        var tenantErr = await OwnershipErrorAsync(enterpriseCode);
        if (tenantErr != null) return Result.Fail(tenantErr);

        var fr = await _db.GetOneIgnoreValidAsync<StandardDirectoryFile>(
            x => x.Code == fileCode && x.EnterpriseCode == enterpriseCode);
        var file = fr.Data;
        if (file == null) return Result.Fail("文件不存在");

        var vr = await _db.GetOneAsync<EnterpriseFileVersion>(
            x => x.FileCode == fileCode && x.EnterpriseCode == enterpriseCode
              && x.VersionNumber == versionNumber && x.IsValid == 1);
        // 队列互斥（04 §七）：同标准目录/同文件有运行中队列 ⇒ 拒绝，避免两个队列同时回写同一行
        var dirErr = await ConfigLockErrorAsync(file.ConfigCode);
        if (dirErr != null) return Result.Fail(dirErr);
        var lockHit = await _queueManager.FindResourceLockAsync(
            QueueManager.RESOURCE_FILE, new List<string> { fileCode });
        if (lockHit != null)
            return Result.Fail($"该文件正在转换队列中（{lockHit.QueueCode}），请等待完成后再恢复");

        var target = vr.Data;
        if (target == null) return Result.Fail($"归档版本 v{versionNumber} 不存在");
        if (string.IsNullOrEmpty(target.StoragePath) || !PathBuilder.IsArchivePath(target.StoragePath))
            return Result.Fail("归档路径不合法");
        if (!await _storage.ExistsAsync(target.StoragePath.TrimStart('/')))
            return Result.Fail("归档对象在存储中不存在");

        var restoredFileName = target.FileName;
        var newStoragePath = PathBuilder.EnterpriseFile(
            enterpriseCode, file.StandardCode, file.StageCode, FolderPathOf(file), restoredFileName);
        var archivedVersion = await GetArchivedVersionNumberAsync(fileCode);

        if (!string.IsNullOrEmpty(file.StoragePath))
        {
            var archivePath = PathBuilder.Archive(file.StoragePath!, archivedVersion);
            var cur = file.StoragePath!.TrimStart('/');
            try
            {
                if (string.Equals(newStoragePath, file.StoragePath, StringComparison.Ordinal))
                    await _storage.RenameAsync(cur, archivePath.TrimStart('/'));
                else if (await _storage.ExistsAsync(cur))
                {
                    await _storage.CopyAsync(cur, archivePath.TrimStart('/'));
                    await _storage.DeleteAsync(cur);
                }
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "[Restore] 当前对象归档失败: {FileCode}", fileCode);
                return Result.Fail($"当前文件归档失败，恢复中止：{ex.Message}");
            }

            await _db.InsertAsync(new EnterpriseFileVersion
            {
                Code = Guid.NewGuid().ToString("N"),
                FileCode = fileCode, EnterpriseCode = enterpriseCode,
                VersionNumber = archivedVersion, FileName = file.FileName,
                StoragePath = archivePath,
                FileSize = file.FileSize ?? 0,
                Reason = $"恢复 v{versionNumber} 前替换"
            });
        }

        try { await _storage.CopyAsync(target.StoragePath.TrimStart('/'), newStoragePath.TrimStart('/')); }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "[Restore] 归档件复制失败: {Archive}", target.StoragePath);
            return Result.Fail($"恢复失败：{ex.Message}");
        }

        var newVersion = archivedVersion + 1;
        file.FileName = restoredFileName;
        file.StoragePath = newStoragePath;
        file.FileSize = target.FileSize;
        file.VersionNumber = newVersion;
        file.IsValid = 1;
        file.IsDeleted = false;
        file.UploadStatus = "uploaded";
        file.ConvertStatus = NeedsConversion(restoredFileName) ? "pending" : null;
        file.MarkdownStatus = NeedsConversion(restoredFileName) ? "pending" : "none";
        file.ExtractStatus = "none";
        file.MaxConfidence = null;
        file.PreviewPdfPath = null;
        file.MarkdownPath = null;
        file.ConvertedStoragePath = null;
        file.UpdateBy = _user.UserCode;
        file.UpdateTime = System.DateTime.Now;
        await _db.UpdateAsync(file,
            nameof(StandardDirectoryFile.FileName), nameof(StandardDirectoryFile.StoragePath),
            nameof(StandardDirectoryFile.FileSize), nameof(StandardDirectoryFile.VersionNumber),
            nameof(StandardDirectoryFile.IsValid), nameof(StandardDirectoryFile.IsDeleted),
            nameof(StandardDirectoryFile.UploadStatus), nameof(StandardDirectoryFile.ConvertStatus),
            nameof(StandardDirectoryFile.MarkdownStatus), nameof(StandardDirectoryFile.ExtractStatus),
            nameof(StandardDirectoryFile.MaxConfidence), nameof(StandardDirectoryFile.PreviewPdfPath),
            nameof(StandardDirectoryFile.MarkdownPath), nameof(StandardDirectoryFile.ConvertedStoragePath),
            nameof(StandardDirectoryFile.UpdateBy), nameof(StandardDirectoryFile.UpdateTime));

        // ★ S2③ 图 4：恢复到 vN ⇒ 回活该版本的字段/表格、归档其余版本（验收⑤：vN 字段原样回来）
        await _resultStore.RehydrateVersionAsync(enterpriseCode, fileCode, versionNumber);

        await WriteOpLogAsync(enterpriseCode, file.StageCode, fileCode, "restore", newVersion,
            new { restoredFrom = versionNumber, fileName = restoredFileName, reason });

        if (NeedsConversion(restoredFileName))
        {
            var (_, queueError) = await EnqueueConvertQueueAsync(
                file.ConfigCode, new List<StandardDirectoryFile> { file }, "file_replace",
                $"{fileCode}:restore:v{newVersion}");
            if (queueError != null)
                _logger.LogWarning("[Restore] 入队失败（不阻断恢复）: {FileCode} {Reason}", fileCode, queueError);
        }

        return Result.Ok();
    }

    public async Task<IList<EnterpriseFileVersion>> GetVersionsAsync(string fileCode, string enterpriseCode)
    {
        var tenantErr = await OwnershipErrorAsync(enterpriseCode);
        if (tenantErr != null) return new List<EnterpriseFileVersion>();

        var r = await _db.GetListAsync<EnterpriseFileVersion>(
            x => x.FileCode == fileCode && x.EnterpriseCode == enterpriseCode && x.IsValid == 1);
        var v = r.Data ?? new();
        return v.OrderByDescending(x => x.VersionNumber).ToList();
    }

    /// <summary>时间线 = op_log ∪ 版本表，按时间倒序</summary>
    public async Task<object?> GetHistoryAsync(string fileCode, string enterpriseCode)
    {
        var tenantErr = await OwnershipErrorAsync(enterpriseCode);
        if (tenantErr != null) return null;

        var file = await _db.Client.Queryable<StandardDirectoryFile>()
            .Where(x => x.Code == fileCode && x.EnterpriseCode == enterpriseCode)
            .FirstAsync();
        if (file == null) return null;

        var logs = (await _db.GetListAsync<EnterpriseFileOpLog>(
            x => x.FileCode == fileCode && x.EnterpriseCode == enterpriseCode, includeDisabled: true)).Data ?? new();
        var versions = (await _db.GetListAsync<EnterpriseFileVersion>(
            x => x.FileCode == fileCode && x.EnterpriseCode == enterpriseCode, includeDisabled: true)).Data ?? new();

        var timeline = logs.Select(l => (Time: l.CreateTime, Item: (object)new
        {
            Source = "op_log", l.OpType, l.VersionNumber, l.Detail,
            l.CreateBy, Time = l.CreateTime
        }))
            .Concat(versions.Select(v => (Time: (System.DateTime)v.CreateTime, Item: (object)new
            {
                Source = "version",
                OpType = "archive",
                VersionNumber = (int?)v.VersionNumber,
                Detail = JsonSerializer.Serialize(new { v.FileName, v.StoragePath, v.FileSize, v.Reason }),
                CreateBy = v.CreateBy,
                Time = v.CreateTime
            })))
            .OrderByDescending(t => t.Time)
            .Select(t => t.Item)
            .ToList();

        return new { file.Code, file.FileName, file.VersionNumber, Status = StatusOf(file), Timeline = timeline };
    }

    #endregion

    #region 七、下载 / 预览

    /// <summary>
    /// 下载（含预览产物、归档版本）。双重校验：① 路径在白名单库下；② 路径首段之后的企业段属于当前工作区。
    /// </summary>
    public async Task<(string? Error, Stream? Stream, string? ContentType, string? FileName)> DownloadAsync(string storagePath)
    {
        if (string.IsNullOrWhiteSpace(storagePath) || !DocumentLibraryPath.IsAllowedStoragePath(storagePath))
            return ("非法的文件路径", null, null, null);

        var segs = PathBuilder.Segments(storagePath);
        if (segs.Length < 2 || !segs[0].Equals(DocumentLibraryPath.EnterpriseDocumentsPrefix, System.StringComparison.OrdinalIgnoreCase))
            return ("仅支持企业资料库文件下载", null, null, null);

        var tenantErr = await OwnershipErrorAsync(segs[1]);
        if (tenantErr != null) return (tenantErr, null, null, null);

        var name = segs[^1];
        try
        {
            var (stream, contentType) = await _storage.DownloadAsync(storagePath.TrimStart('/'));
            return (null, stream, string.IsNullOrEmpty(contentType) ? "application/octet-stream" : contentType, name);
        }
        catch (System.Exception ex)
        {
            _logger.LogWarning(ex, "[Download] 对象下载失败: {Path}", storagePath);
            return ("文件在存储中不存在", null, null, null);
        }
    }

    /// <summary>取文件的预览 PDF 产物路径（PDF/图片透传时 == StoragePath）</summary>
    public async Task<string?> GetPreviewPathAsync(string fileCode, string enterpriseCode, bool markdown)
    {
        var row = await LoadOwnedFileAsync(fileCode, enterpriseCode);
        if (row == null) return null;
        var path = markdown ? row.MarkdownPath : row.PreviewPdfPath;
        if (!string.IsNullOrEmpty(path)) return path;
        // 产物缺失：PDF 预览回落到源文件（图片/PDF 透传场景），Markdown 无回落
        return markdown ? null : row.StoragePath;
    }

    #endregion

    #region 八、转换队列

    /// <summary>该标准目录是否有运行中队列（前端 5s 轮询）。</summary>
    public async Task<object> GetActiveQueueAsync(string configCode, string enterpriseCode)
    {
        var tenantErr = await OwnershipErrorAsync(enterpriseCode);
        if (tenantErr != null) return new { IsBusy = false, Message = tenantErr };

        if (string.IsNullOrWhiteSpace(configCode))
            return new { IsBusy = false, Message = "缺少目录配置编码" };

        var queue = await _queueManager.FindRunningQueueByScopeKeyAsync(configCode);
        var locks = await _queueManager.FindResourceLockAsync(
            QueueManager.RESOURCE_DIR, new List<string> { configCode });

        return new
        {
            IsBusy = queue != null || locks != null,
            QueueCode = queue?.QueueCode,
            QueueName = queue?.QueueName,
            Status = queue?.Status,
            TotalCount = queue?.TotalCount ?? 0,
            CompletedCount = queue?.CompletedCount ?? 0,
            FailedCount = queue?.FailedCount ?? 0,
            PendingCount = queue?.PendingCount ?? 0,
            Progress = queue?.Progress ?? 0,
            QueueType = queue?.QueueType,
            LockQueueCode = locks?.QueueCode,
            Message = (string?)null
        };
    }

    /// <summary>取消队列（<c>QueueManager.CancelQueueAsync</c> 内部会查不到就报错，这里先做归属校验）。</summary>
    public async Task<Result> CancelQueueAsync(string queueCode, string enterpriseCode)
    {
        var tenantErr = await OwnershipErrorAsync(enterpriseCode);
        if (tenantErr != null) return Result.Fail(tenantErr);
        if (string.IsNullOrWhiteSpace(queueCode)) return Result.Fail("队列编码不能为空");

        var queue = await _db.GetOneAsync<YZH.Core.Stand.Models.Queue.YzhQueue>(x => x.QueueCode == queueCode);
        var row = queue.Data;
        if (row == null) return Result.Fail("队列不存在");

        // 归属校验：队列 ScopeKey 必须是本企业名下某个 config
        var owned = (await _db.GetListAsync<StandardDirectoryConfig>(
            x => x.EnterpriseCode == enterpriseCode)).Data ?? new();
        if (!owned.Any(c => c.Code == row.ScopeKey))
            return Result.Fail("队列不属于当前企业");

        var (ok, error) = await _queueManager.CancelQueueAsync(queueCode);
        return ok ? Result.Ok() : Result.Fail(error ?? "取消失败");
    }

    #endregion

    #region 九、提取（真链保留，偏差 D10）

    public async Task<Result> TriggerExtractAsync(string fileCode, string enterpriseCode)
    {
        var guard = ValidateWritableEnterprise(enterpriseCode);
        if (guard != null) return Result.Fail(guard);
        var tenantErr = await OwnershipErrorAsync(enterpriseCode);
        if (tenantErr != null) return Result.Fail(tenantErr);

        var fr = await _db.GetOneAsync<StandardDirectoryFile>(
            x => x.Code == fileCode && x.EnterpriseCode == enterpriseCode && x.IsValid == 1);
        var file = fr.Data;
        if (file == null) return Result.Fail("文件不存在");
        if (file.ConvertStatus != "completed") return Result.Fail("文件尚未完成转换");
        if (string.IsNullOrEmpty(file.MarkdownPath)) return Result.Fail("Markdown 产物不存在");
        // ★ S2⑤ 3 态无 pending/processing：重复触发改用队列资源锁互斥（与 delete 同口径）
        var held = await _queueManager.FindResourceLockAsync(
            QueueManager.RESOURCE_FILE, new List<string> { fileCode });
        if (held != null) return Result.Fail($"该文件已有队列在执行（{held.QueueCode}），请等待完成");

        file.ExtractStatus = "none";
        file.ExtractMessage = "已加入提取队列";
        await _db.UpdateAsync(file,
            nameof(StandardDirectoryFile.ExtractStatus), nameof(StandardDirectoryFile.ExtractMessage));

        var req = new QueueManager.CreateQueueRequest
        {
            QueueType = "doc_extract",
            QueueName = $"企业资料提取（手动触发） - {file.FileName}",
            ScopeKey = file.ConfigCode,
            SourceType = "enterprise_manual",
            SourceId = $"{fileCode}@{System.DateTime.Now:yyyyMMddHHmmss}",
            UserName = _user.UserName,
            ResourceLocks = new List<QueueManager.ResourceLockItem>
            {
                new() { ResourceTable = QueueManager.RESOURCE_FILE, ResourceCode = fileCode, ResourceName = file.FileName ?? fileCode }
            },
            Tasks = new List<QueueManager.TaskItem>
            {
                new() { TaskType = "doc_extract", Payload = JsonSerializer.Serialize(new { code = fileCode, enterpriseCode, stageCode = file.StageCode ?? "", fileName = file.FileName ?? fileCode }) }
            }
        };
        var (qok, qerr, _, _) = await _queueManager.CreateQueueAsync(req);
        if (!qok)
        {
            file.ExtractStatus = "none";
            file.ExtractMessage = $"提取队列创建失败：{qerr}";
            await _db.UpdateAsync(file,
                nameof(StandardDirectoryFile.ExtractStatus), nameof(StandardDirectoryFile.ExtractMessage));
            return Result.Fail($"提取队列创建失败：{qerr}");
        }

        await WriteOpLogAsync(enterpriseCode, file.StageCode, fileCode, "extract_trigger", file.VersionNumber, null);
        return Result.Ok();
    }

    public async Task<object?> GetExtractionResultAsync(string fileCode, string enterpriseCode)
    {
        var row = await LoadOwnedFileAsync(fileCode, enterpriseCode);
        if (row == null) return null;

        // ★ S2③ §5.3 唯一读取口径：经版本店取「活跃行」（IsValid=1），
        //   ⛔ 不带 VersionNumber 硬过滤 —— 恢复到 vN 后活跃行即 vN（验收⑤）；
        //   替换/移除后活跃行为空（验收③④）。
        var (fieldRows, tableRows) = await _resultStore.GetActiveAsync(enterpriseCode, fileCode);

        var fields = fieldRows
            .OrderBy(x => x.FieldCode)
            .Select(x => new { x.FieldCode, x.FieldName, x.ExtractedValue, Confidence = x.Confidence, x.IsManualEdited, x.ExtractedAt })
            .ToList();

        var tables = tableRows
            .OrderBy(x => x.TableIndex)
            .Select(x => new { x.TableCode, x.TableIndex, Rows = ParseJsonRows(x.ExtractedJson), x.ExtractedAt })
            .ToList();

        return new
        {
            row.Code, row.FileName,
            ExtractStatus = EnterpriseExtractStatus.ForDisplay(row.ExtractStatus),
            row.ExtractMessage, row.MaxConfidence, Fields = fields, Tables = tables
        };
    }

    private static object? ParseJsonRows(string? extractedJson)
    {
        if (string.IsNullOrWhiteSpace(extractedJson)) return null;
        try { return JsonSerializer.Deserialize<JsonElement>(extractedJson); }
        catch { return extractedJson; }
    }

    #endregion

    #region 十、企业目录初始化 / ensure

    /// <summary>
    /// ensure 失败原因码（前端据此区分「等待态」与「真故障」，⛔ 禁前端字符串匹配 Message）。
    /// </summary>
    public static class InitReason
    {
        /// <summary>成功</summary>
        public const string Ok = "";
        /// <summary>
        /// 该机构 × 标准 × 阶段**没有目录模板**。
        /// <para>★ 这是<b>等待态</b>不是故障：模板一旦由管理端建好，下次任意读接口
        /// （stage-overview / standard-directory / upload/plan）都会自动补出目录与槽位，
        /// <b>不需要重新建立关联</b>。</para>
        /// </summary>
        public const string TemplateMissing = "template_missing";
        /// <summary>企业未绑定机构（cert_enterprise.OrgCode 为空）—— 补齐后同样自动初始化</summary>
        public const string OrgUnbound = "org_unbound";
        /// <summary>入参缺失 / 试图写模板域等调用错误（属代码缺陷，不是数据状态）</summary>
        public const string InvalidRequest = "invalid_request";
    }

    /// <summary>
    /// 企业目录初始化（13 号 E7）：按 (Org,Std,Stage) 复制模板行。
    /// <para>幂等判据 = config 表 uk 四列 <c>(OrgCode, EnterpriseCode, StandardCode, StageCode)</c>；
    /// 槽位行按 <c>(ConfigCode, StandardFileCode)</c> 对账，只补缺失、不重复插。</para>
    /// <para><b>本方法只是「预热」，不是唯一初始化入口</b>：<see cref="EnsureDirectoryAsync"/> 在
    /// 每个读接口里都会跑，所以这里失败不代表资料功能不可用，补齐数据后会自动恢复。</para>
    /// </summary>
    /// <param name="reason">
    /// 失败原因码（见 <see cref="InitReason"/>）—— <c>template_missing</c> / <c>org_unbound</c>
    /// 是<b>等待态</b>（补齐后自动恢复），前端不应按故障告警。
    /// </param>
    public async Task<(bool Success, string? Error, string Reason)> InitEnterpriseDirectoryAsync(
        string enterpriseCode, string? orgCode, string standardCode, string stageCode, string? userCode = null)
    {
        var guard = ValidateWritableEnterprise(enterpriseCode);
        if (guard != null) return (false, guard, InitReason.InvalidRequest);

        var ensure = await EnsureDirectoryAsync(enterpriseCode, orgCode, standardCode, stageCode, userCode);
        return ensure.Error == null
            ? (true, null, InitReason.Ok)
            : (false, ensure.Error, ensure.Reason);
    }

    /// <summary>
    /// ensure 企业目录（config + 槽位），幂等；返回企业 config / 模板 config Code / 失败原因与原因码。
    /// </summary>
    private async Task<(StandardDirectoryConfig? Config, string TemplateConfigCode, string? Error, string Reason)> EnsureDirectoryAsync(
        string enterpriseCode, string? orgCode, string standardCode, string stageCode, string? userCode = null)
    {
        if (string.IsNullOrWhiteSpace(enterpriseCode))
            return (null, "", "缺少企业编码", InitReason.InvalidRequest);
        if (string.IsNullOrWhiteSpace(standardCode) || string.IsNullOrWhiteSpace(stageCode))
            return (null, "", "缺少标准或阶段编码", InitReason.InvalidRequest);

        var guard = ValidateWritableEnterprise(enterpriseCode);
        if (guard != null) return (null, "", guard, InitReason.InvalidRequest);

        if (string.IsNullOrWhiteSpace(orgCode))
            orgCode = await GetEnterpriseOrgCodeAsync(enterpriseCode);
        if (string.IsNullOrWhiteSpace(orgCode))
            return (null, "", "企业未绑定机构（cert_enterprise.OrgCode 为空），补齐机构后会自动初始化，无需重新关联",
                InitReason.OrgUnbound);
        orgCode = await NormalizeOrgCodeAsync(orgCode);

        // 1. 已有企业目录？（includeDisabled：禁用行须被认出并复活，否则 INSERT 撞唯一键）
        var existCfg = await _db.GetListAsync<StandardDirectoryConfig>(
            x => x.OrgCode == orgCode && x.EnterpriseCode == enterpriseCode
                 && x.StandardCode == standardCode && x.StageCode == stageCode,
            includeDisabled: true);
        var config = existCfg.Data?.FirstOrDefault();

        // 1b. ★ 机构漂移兜底：归一后的 orgCode 查不到，但存在「同企业 + 同标准 + 同阶段、挂在别的机构下」
        //     的旧目录 ⇒ 迁移它的 OrgCode，而不是新建一个空目录。
        //     不做这一步的后果：uk_enterprise_std_stage 含 OrgCode，认证机构 CbCode 一改 / 企业改挂机构 /
        //     工作区切换 ⇒ ensure 认定「没有企业目录」→ 新建空目录，带 167 行槽位 + 归档版本 + 存储对象的
        //     旧目录变成孤儿（页面看不到、文件还在桶里），且没有任何报错。
        //     实测 G4测试企业甲 正是这个形态：cert_enterprise.OrgCode=66bbf572（CB001/VirtualOrg），
        //     目录行 OrgCode=906e8b2a（归一后的认证机构），全靠 NormalizeOrgCodeAsync 兜住。
        if (config == null)
        {
            var drifted = await _db.GetListAsync<StandardDirectoryConfig>(
                x => x.EnterpriseCode == enterpriseCode && x.StandardCode == standardCode
                     && x.StageCode == stageCode && x.OrgCode != orgCode,
                includeDisabled: true);
            var orphan = drifted.Data?
                .OrderByDescending(c => c.OrgCode == enterpriseCode)
                .ThenByDescending(c => c.CreateTime)
                .FirstOrDefault();
            if (orphan != null)
            {
                var oldOrg = orphan.OrgCode;
                orphan.OrgCode = orgCode;
                orphan.IsValid = 1;
                orphan.IsDeleted = false;
                orphan.UpdateBy = userCode ?? _user.UserCode;
                orphan.UpdateTime = System.DateTime.Now;
                await _db.UpdateAsync(orphan, nameof(StandardDirectoryConfig.OrgCode),
                    nameof(StandardDirectoryConfig.IsValid), nameof(StandardDirectoryConfig.IsDeleted),
                    nameof(StandardDirectoryConfig.UpdateBy), nameof(StandardDirectoryConfig.UpdateTime));
                _logger.LogWarning(
                    "[EnsureDirectory] 企业目录机构漂移已修正 {Code}: OrgCode {Old} → {New}（企业 {Ent}）",
                    orphan.Code, oldOrg, orgCode, enterpriseCode);
                config = orphan;
            }
        }

        // 2. 模板行（EnterpriseCode = 虚拟企业常量）。多行时取第一（Sort → CreateTime，稳定口径）
        var tplCfg = await _db.GetListAsync<StandardDirectoryConfig>(
            x => x.OrgCode == orgCode && x.EnterpriseCode == YzhVirtualEnterprise.Code
                 && x.StandardCode == standardCode && x.StageCode == stageCode);
        var template = tplCfg.Data?
            .OrderBy(c => c.Sort)
            .ThenBy(c => c.CreateTime)
            .FirstOrDefault();

        var by = userCode ?? _user.UserCode;

        if (config == null)
        {
            if (template == null)
                return (null, "", "该机构未配置标准目录：请在管理端「标准目录管理」为该机构补齐模板；"
                    + "补齐后资料目录会自动初始化，无需重新建立关联", InitReason.TemplateMissing);

            config = new StandardDirectoryConfig
            {
                Code = Guid.NewGuid().ToString("N"),
                OrgCode = orgCode,
                EnterpriseCode = enterpriseCode,
                StandardCode = standardCode,
                StageCode = stageCode,
                RootFolderName = template.RootFolderName,
                Sort = template.Sort,
                IsValid = 1,
                IsDeleted = false,
                CreateBy = by,
                CreateTime = System.DateTime.Now
            };
            await _db.InsertAsync(config);
        }
        else if (config.IsValid != 1)
        {
            config.IsValid = 1;
            config.IsDeleted = false;
            config.UpdateBy = by;
            config.UpdateTime = System.DateTime.Now;
            await _db.UpdateAsync(config, nameof(StandardDirectoryConfig.IsValid),
                nameof(StandardDirectoryConfig.IsDeleted), nameof(StandardDirectoryConfig.UpdateBy),
                nameof(StandardDirectoryConfig.UpdateTime));
        }

        if (template == null)
            return (config, "", null, InitReason.Ok);

        // 3. 槽位对账：模板文件行 − 企业已有文件行（按 StandardFileCode）
        var tplFiles = (await _db.GetListAsync<StandardDirectoryFile>(
            x => x.ConfigCode == template.Code)).Data ?? new();
        var ownFiles = (await _db.GetListAsync<StandardDirectoryFile>(
            x => x.ConfigCode == config.Code, includeDisabled: true)).Data ?? new();
        var ownByTpl = ownFiles
            .Where(f => !string.IsNullOrEmpty(f.StandardFileCode))
            .GroupBy(f => f.StandardFileCode!)
            .ToDictionary(g => g.Key, g => g.First());

        // 模板文件夹 FullPath 索引（槽位 FullPath 由它拼出，供 Step2 反推文件夹路径）。
        // ★ 口径：模板行含根段，槽位行存「相对配置根」（05 §8.1 例：1质量手册/XASL-QM 质量手册.doc）
        var tplFolders = (await _db.GetListAsync<StandardDirectoryFolder>(
            x => x.ConfigCode == template.Code)).Data ?? new List<StandardDirectoryFolder>();
        var rootSegment = RootSegmentOf(tplFolders);
        var folderPathByCode = tplFolders.Where(f => !string.IsNullOrEmpty(f.Code))
            .GroupBy(f => f.Code!).ToDictionary(g => g.Key, g => RelativePath(g.First().FullPath, rootSegment));

        foreach (var tf in tplFiles)
        {
            var relFolder = folderPathByCode.TryGetValue(tf.FolderCode ?? "", out var fp) ? fp : "";
            var expectedFullPath = string.IsNullOrEmpty(relFolder) ? tf.FileName : $"{relFolder}/{tf.FileName}";

            if (ownByTpl.TryGetValue(tf.Code, out var existingRow))
            {
                // 修复历史行的 FullPath（早期版本未写入；缺它则 Step2/恢复无法反推文件夹路径）
                var needRepair = string.IsNullOrEmpty(existingRow.FullPath)
                                 || (existingRow.FullPath != expectedFullPath && string.IsNullOrEmpty(existingRow.StoragePath));
                if (needRepair)
                {
                    existingRow.FullPath = expectedFullPath;
                    existingRow.UpdateBy = by;
                    existingRow.UpdateTime = System.DateTime.Now;
                    await _db.UpdateAsync(existingRow,
                        nameof(StandardDirectoryFile.FullPath), nameof(StandardDirectoryFile.UpdateBy),
                        nameof(StandardDirectoryFile.UpdateTime));
                }

                if (existingRow.IsValid == 1) continue;

                existingRow.IsValid = 1;
                existingRow.IsDeleted = false;
                existingRow.UpdateBy = by;
                existingRow.UpdateTime = System.DateTime.Now;
                await _db.UpdateAsync(existingRow, nameof(StandardDirectoryFile.IsValid),
                    nameof(StandardDirectoryFile.IsDeleted), nameof(StandardDirectoryFile.UpdateBy),
                    nameof(StandardDirectoryFile.UpdateTime));
                continue;
            }

            await _db.InsertAsync(new StandardDirectoryFile
            {
                Code = Guid.NewGuid().ToString("N"),
                FolderCode = tf.FolderCode ?? "",
                ConfigCode = config.Code,
                EnterpriseCode = enterpriseCode,
                StandardCode = standardCode,
                StageCode = stageCode,
                StandardFileCode = tf.Code,   // ★ 槽位→模板契约键（提取规则按此取 Prompt / 写 B-08）
                FileName = tf.FileName,
                FileType = tf.FileType,
                FilePattern = tf.FilePattern,
                IsRequired = tf.IsRequired,
                MaxFileSizeMB = tf.MaxFileSizeMB,
                Description = tf.Description,
                SortOrder = tf.SortOrder,
                ExtractionEnabled = tf.ExtractionEnabled,
                ExtractionRules = tf.ExtractionRules,
                PreCheckRequired = tf.PreCheckRequired,
                ComplianceRequired = tf.ComplianceRequired,
                VersionNumber = 1,
                FullPath = expectedFullPath,
                ExtractStatus = "none",
                ConvertStatus = null,
                IsValid = 1,
                IsDeleted = false,
                CreateBy = by,
                CreateTime = System.DateTime.Now
            });
        }

        return (config, template.Code, null, InitReason.Ok);
    }

    private async Task<string?> GetEnterpriseOrgCodeAsync(string enterpriseCode)
    {
        var er = await _db.GetOneAsync<Enterprise>(x => x.Code == enterpriseCode);
        return er.Data?.OrgCode;
    }

    /// <summary>
    /// 机构域归一：把「企业挂靠节点 Code」换成「认证机构 Code」= 标准目录模板行的建档域。
    /// <para>★ 实现已上提到 <see cref="WorkspaceContextService.ResolveCertBodyCodeAsync"/> ——
    /// 参数定义 / 目录模板 / 标准关联三处都要做同一次归一，各写一份会漂移且都不报错。
    /// 此处保留方法名作为委托，避免改动 1000+ 行文件里的 12 处调用点。</para>
    /// </summary>
    private Task<string> NormalizeOrgCodeAsync(string orgCode)
        => _workspace.ResolveCertBodyCodeAsync(orgCode);

    /// <summary>
    /// 工作区守卫：目标企业必须属于当前登录人的工作区。失败返回错误文案，成功返回 null。
    /// <para>每个公开方法第一行调用 —— 缺这一步 = 跨工作区越权读写（P0-2）。</para>
    /// </summary>
    private async Task<string?> OwnershipErrorAsync(string enterpriseCode)
    {
        if (string.IsNullOrWhiteSpace(enterpriseCode)) return "企业编码不能为空";

        var ws = _workspace.Resolve(_user.UserCode);
        if (!ws.Success || ws.Data == null) return ws.Error ?? "无法定位当前工作区";

        var ent = await _db.GetOneAsync<Enterprise>(x => x.Code == enterpriseCode);
        if (ent.Data == null) return "企业不存在或不属于当前工作区";
        if (!string.Equals(ent.Data.OrgCode, ws.Data.Code, System.StringComparison.Ordinal))
            return "企业不存在或不属于当前工作区";

        return null;
    }

    /// <summary>按 Code 取属于该企业的文件行（不带工作区守卫；调用方须自行守卫）</summary>
    /// <summary>目录级队列互斥（04 §七）：同标准目录已有运行中队列 ⇒ 返回错误文案</summary>
    private async Task<string?> ConfigLockErrorAsync(string? configCode)
    {
        if (string.IsNullOrWhiteSpace(configCode)) return null;
        var running = await _queueManager.FindRunningQueueByScopeKeyAsync(configCode);
        return running == null
            ? null
            : $"该标准目录正在执行转换队列（{running.QueueCode}），请等待完成后再操作";
    }

    private async Task<StandardDirectoryFile?> LoadOwnedFileAsync(string fileCode, string enterpriseCode)
    {
        if (string.IsNullOrWhiteSpace(fileCode)) return null;
        var tenantErr = await OwnershipErrorAsync(enterpriseCode);
        if (tenantErr != null) return null;
        var r = await _db.GetOneIgnoreValidAsync<StandardDirectoryFile>(
            x => x.Code == fileCode && x.EnterpriseCode == enterpriseCode);
        return r.Data;
    }

    #endregion

    #region 十一、私有辅助

    /// <summary>取上传任务下属于该 TaskId 的行（草稿行也要能取到）</summary>
    private async Task<StandardDirectoryFile?> GetDraftRowAsync(string fileCode, string taskId)
    {
        if (string.IsNullOrWhiteSpace(fileCode) || string.IsNullOrWhiteSpace(taskId)) return null;
        var r = await _db.GetListAsync<StandardDirectoryFile>(
            x => x.Code == fileCode && x.TaskId == taskId, includeDisabled: true);
        return r.Data?.FirstOrDefault();
    }

    private async Task<UploadTask?> GetTaskAsync(string taskId)
    {
        if (string.IsNullOrWhiteSpace(taskId)) return null;
        var r = await _db.GetOneIgnoreValidAsync<UploadTask>(x => x.TaskId == taskId);
        return r.Data;
    }

    /// <summary>
    /// 由行反推所在文件夹路径（**相对配置根**，03 §3.2）。
    /// <para>槽位的 <c>FullPath</c> 已按相对口径落库（见 <see cref="EnsureDirectoryAsync"/>），
    /// 故直接取目录段；历史行缺 <c>FullPath</c> 时回落 <c>StoragePath</c> 的中间段。</para>
    /// </summary>
    private static string FolderPathOf(StandardDirectoryFile row)
    {
        var full = (row.FullPath ?? "").Trim('/');
        var idx = full.LastIndexOf('/');
        if (idx > 0) return full[..idx];
        if (full.Length > 0) return "";   // 文件直接在配置根

        var segs = PathBuilder.Segments(row.StoragePath);
        // 企业库路径结构：{库}/{企业}/{标准}/{阶段}/{文件夹...}/{文件名}
        if (segs.Length > 5) return string.Join("/", segs.Skip(4).Take(segs.Length - 5));
        return "";
    }

    /// <summary>
    /// 模板根段名（<c>Depth=1 / ParentCode 空</c> 的根文件夹行）。
    /// <para>模板 folder 行的 <c>FullPath</c> 含根段（如 <c>CS河北雄安…13485体系材料/1质量手册</c>），
    /// 但存储路径要的是「相对配置根」（03 §3.2 / 05 §8.1 例：<c>1质量手册</c>）⇒ 统一剥掉根段。</para>
    /// </summary>
    private static string RootSegmentOf(IEnumerable<StandardDirectoryFolder> folders)
    {
        var root = folders.FirstOrDefault(f => f.Depth <= 1 && string.IsNullOrEmpty(f.ParentCode));
        return (root?.FullPath ?? root?.FolderName ?? "").Trim('/');
    }

    /// <summary>把模板全路径转成「相对配置根」的路径（等于根段本身 ⇒ 空串）</summary>
    private static string RelativePath(string? fullPath, string rootSegment)
    {
        var p = (fullPath ?? "").Trim('/');
        if (p.Length == 0 || rootSegment.Length == 0) return p;
        if (string.Equals(p, rootSegment, System.StringComparison.Ordinal)) return "";
        var prefix = rootSegment + "/";
        return p.StartsWith(prefix, System.StringComparison.Ordinal) ? p[prefix.Length..] : p;
    }

    /// <summary>
    /// 建 <c>file_convert</c> 队列（04 §3.1 单任务形态）。
    /// <para>每文件 1 个 TaskItem，<c>ConvertType=""</c> ⇒ 执行器自动双产物（PDF + Markdown）且失败隔离；
    /// 载荷字段名必须与 <c>FileConvertPayload</c> 对齐（Code/FileName/SourcePath/ConvertType）。</para>
    /// </summary>
    private async Task<(string? QueueCode, string? Error)> EnqueueConvertQueueAsync(
        string configCode, List<StandardDirectoryFile> files, string sourceType, string sourceId,
        bool autoExtract = true)
    {
        if (files.Count == 0) return (null, null);

        // 防御（04 §七「同标准目录一次只允许一个运行队列」）：调用方漏检时兜底，
        // 否则会出现两个队列并发回写同一批文件行（实测发生过：replace 与 restore 同秒入队）
        var running = await _queueManager.FindRunningQueueByScopeKeyAsync(configCode);
        if (running != null)
            return (null, $"该标准目录已有运行中转换队列（{running.QueueCode}），请等待完成");

        var tasks = new List<QueueManager.TaskItem>();
        var locks = new List<QueueManager.ResourceLockItem>
        {
            new() { ResourceTable = QueueManager.RESOURCE_DIR, ResourceCode = configCode, ResourceName = configCode }
        };

        foreach (var f in files)
        {
            tasks.Add(new QueueManager.TaskItem
            {
                TaskType = "file_convert",
                TaskId = sourceId,
                Payload = JsonSerializer.Serialize(new
                {
                    Code = f.Code,
                    FileName = f.FileName,
                    SourcePath = f.StoragePath,
                    // 空串 ⇒ 执行器自动双产物（office2pdf + anydoc2md，任一失败不阻塞另一个）
                    ConvertType = "",
                    // ★ S2：企业三件套 —— 缺 EnterpriseCode 则转换完成后永不入提取队列（实测该链曾断）
                    EnterpriseCode = f.EnterpriseCode,
                    StageCode = f.StageCode,
                    // ★ S2 图 3 分流依据：false = 只转换不提取
                    AutoExtract = autoExtract
                })
            });
            locks.Add(new QueueManager.ResourceLockItem
            {
                ResourceTable = QueueManager.RESOURCE_FILE,
                ResourceCode = f.Code,
                ResourceName = f.FileName ?? f.Code
            });
        }

        var req = new QueueManager.CreateQueueRequest
        {
            QueueType = "file_convert",
            QueueName = $"企业资料转换 - {files.Count} 个文件",
            ScopeKey = configCode,
            SourceType = sourceType,
            SourceId = sourceId,
            UserName = _user.UserName,
            ResourceLocks = locks,
            Tasks = tasks
        };

        var (ok, error, queueCode, _) = await _queueManager.CreateQueueAsync(req);
        return ok ? (queueCode, null) : (null, error ?? "队列创建失败");
    }

    /// <summary>名实修正：返回**本次将被归档的旧内容版本号**（版本表 max+1），替换后的新当前版本号 = 返回值 + 1</summary>
    private async Task<int> GetArchivedVersionNumberAsync(string fileCode)
    {
        var max = await _db.Client.Queryable<EnterpriseFileVersion>()
            .Where(x => x.FileCode == fileCode && x.IsValid == 1)
            .MaxAsync(x => (int?)x.VersionNumber);
        return (max ?? 0) + 1;
    }

    /// <summary>乐观锁：前端回传打开页面时的行时间戳（UpdateTime 缺省取 CreateTime），偏差 &gt;2s 判冲突</summary>
    private static string? CheckModifyConflict(StandardDirectoryFile file, System.DateTime? expectedModifyTime)
    {
        if (!expectedModifyTime.HasValue) return null;
        var stamp = file.UpdateTime ?? file.CreateTime;
        return System.Math.Abs((stamp - expectedModifyTime.Value).TotalSeconds) > 2
            ? "文件刚被他人更新，请刷新后重试"
            : null;
    }

    /// <summary>
    /// ★ 人工补录值被按 RuleCode 归档时的留痕 + 提示（裁决 J4 的连带后果，05 号 §5.3）。
    /// </summary>
    /// <para><b>为什么需要</b>：J4 规定「覆盖文件 ⇒ 按 RuleCode 清理该规则全部数据」，
    /// 同一 <c>RuleCode</c> 下 <c>ValueSource='manual'</c> 的行也在内 ⇒ <b>专家之前补的值会消失</b>。
    /// 这是系统行为导致的人工值丢失，<b>必须留痕并告知</b>，否则专家会以为系统出错。</para>
    /// <para>★ 只 INSERT 到 <c>cert_extraction_change_log</c>（不可变表，<c>ChangeAction='archive'</c>），
    /// <b>不物理删除、不回滚</b> —— 值仍在归档行里，专家可参考。</para>
    /// <returns>提示文案；无人工值被归档时返回 <c>null</c></returns>
    private async Task<object?> ArchiveManualNoticeAsync(
        string enterpriseCode, string? ruleCode,
        List<ExtractionResult> lostFields, List<TableExtractionResult> lostTables,
        string fileName)
    {
        if (string.IsNullOrEmpty(ruleCode)) return null;
        if (lostFields.Count == 0 && lostTables.Count == 0) return null;

        var now = System.DateTime.Now;
        var orgCode = await ResolveOrgCodeAsync(enterpriseCode);

        foreach (var f in lostFields)
        {
            await _db.InsertAsync(new ExtractionChangeLog
            {
                Code = Guid.NewGuid().ToString("N"),
                OrgCode = orgCode,
                ResultType = "field",
                ResultCode = f.Code ?? "",
                EnterpriseCode = enterpriseCode,
                RuleCode = ruleCode,
                StandardCode = f.StandardCode,
                StageCode = f.StageCode,
                StandardFileCode = f.StandardFileCode,
                FileCode = f.FileCode,
                FieldCode = f.FieldCode,
                FieldLabel = f.FieldName,
                ChangeAction = ExtractionChangeLog.ActionArchive,
                OldValue = f.ExtractedValue,
                NewValue = null,
                OldValueSource = f.ValueSource,
                NewValueSource = null,
                OperatorCode = _user.UserCode,
                OperatorName = _user.UserTrueName ?? _user.UserName,
                OperateTime = now,
                Remark = $"文件「{fileName}」被替换，按 RuleCode 归档时覆盖了人工补录值",
                CreateBy = _user.UserCode,
                CreateTime = now,
                IsValid = 1
            });
        }

        foreach (var t in lostTables)
        {
            await _db.InsertAsync(new ExtractionChangeLog
            {
                Code = Guid.NewGuid().ToString("N"),
                OrgCode = orgCode,
                ResultType = "table",
                ResultCode = t.Code ?? "",
                EnterpriseCode = enterpriseCode,
                RuleCode = ruleCode,
                StandardCode = t.StandardCode,
                StageCode = t.StageCode,
                StandardFileCode = t.StandardFileCode,
                FileCode = t.FileCode,
                TableCode = t.TableCode,
                FieldLabel = t.TableCode,
                ChangeAction = ExtractionChangeLog.ActionArchive,
                OldValue = t.ExtractedJson,
                NewValue = null,
                OldValueSource = t.ValueSource,
                NewValueSource = null,
                OperatorCode = _user.UserCode,
                OperatorName = _user.UserTrueName ?? _user.UserName,
                OperateTime = now,
                Remark = $"文件「{fileName}」被替换，按 RuleCode 归档时覆盖了人工补录值",
                CreateBy = _user.UserCode,
                CreateTime = now,
                IsValid = 1
            });
        }

        var n = lostFields.Count + lostTables.Count;
        _logger.LogWarning(
            "[ReplaceFile] 人工补录值被归档 file={File} rule={Rule} 条数={N}（已写 change_log，需提示专家重新补录）",
            fileName, ruleCode, n);

        // ★ 前端据此弹窗：「您之前对『{fileName}』的人工补录值已被新的自动提取覆盖，是否需要重新补录？」
        return new
        {
            manualValuesArchived = true,
            archivedCount = n,
            fileName,
            ruleCode,
            notice = $"您之前对「{fileName}」的 {n} 项人工补录值已被新的提取结果覆盖（按规则整体重算），是否需要重新补录？"
        };
    }

    /// <summary>租户/机构 Code：<c>cert_extraction_change_log.OrgCode</c> 是 NOT NULL</summary>
    private async Task<string> ResolveOrgCodeAsync(string enterpriseCode)
    {
        var ent = (await _db.GetOneAsync<Enterprise>(x => x.Code == enterpriseCode)).Data;
        return ent?.OrgCode ?? enterpriseCode;
    }

    /// <summary>
    /// ★ 由标准文件行 Code 反查提取规则 Code（裁决 J4「1 文件 = 1 规则」的落地关键）。
    /// </summary>
    /// <para><b>映射链</b>：<c>cert_standard_directory_file.StandardFileCode</c>（模板行 Code）
    /// → <c>cert_doc_extraction_rule.StandardFileCode</c> → <c>cert_doc_extraction_rule.Code</c>。</para>
    /// <para><b>为什么必然唯一</b>：<c>uk_rule_scope(OrgCode, StandardCode, StageCode, StandardFileCode)</c>
    /// 是唯一索引（实测 2026-09-30），故同一 <c>StandardFileCode</c> 最多一条规则。</para>
    /// <param name="standardFileCode">模板行 Code；为空或查不到规则时返回 <c>""</c>（调用方回退按文件归档）</param>
    private async Task<string> ResolveRuleCodeAsync(string? standardFileCode)
    {
        if (string.IsNullOrWhiteSpace(standardFileCode)) return "";

        var rules = (await _db.GetListAsync<DocExtractionRule>(x =>
            x.StandardFileCode == standardFileCode && !x.IsDeleted)).Data ?? new List<DocExtractionRule>();

        if (rules.Count == 0) return "";
        if (rules.Count > 1)
            _logger.LogWarning(
                "[EnterpriseFile] standardFileCode={Sfc} 命中 {N} 条提取规则（uk_rule_scope 应保证唯一），取第一条",
                standardFileCode, rules.Count);

        return rules[0].Code ?? "";
    }

    /// <summary>操作留痕（只追加，不修改不删除）。失败不阻断业务。</summary>
    private async Task WriteOpLogAsync(string enterpriseCode, string? stageCode, string fileCode,
        string opType, int? versionNumber, object? detail)
    {
        try
        {
            await _db.InsertAsync(new EnterpriseFileOpLog
            {
                Code = Guid.NewGuid().ToString("N"),
                EnterpriseCode = enterpriseCode,
                StageCode = stageCode,
                FileCode = fileCode,
                OpType = opType,
                VersionNumber = versionNumber,
                Detail = detail == null ? null : JsonSerializer.Serialize(detail),
                IsValid = 1,
                IsDeleted = false,
                CreateBy = _user.UserCode,
                CreateTime = System.DateTime.Now
            });
        }
        catch (System.Exception ex)
        {
            _logger.LogWarning(ex, "[OpLog] 写入失败（不阻断业务）: {FileCode} {OpType}", fileCode, opType);
        }
    }

    /// <summary>需转换判据：排除无扩展名与系统文件（04 §3.1 判据放宽 —— 旧实现只认 doc/xls，
    /// docx/xlsx/ppt/pptx/pdf 从不入队，正是「上传后不转换」的根因）</summary>
    private static bool NeedsConversion(string fileName)
    {
        var ext = Path.GetExtension(fileName)?.ToLowerInvariant();
        if (string.IsNullOrEmpty(ext)) return false;
        var ignored = new[] { ".ds_store", ".tmp", ".temp" };
        if (ignored.Contains(ext)) return false;
        return true;
    }

    #endregion
}

#region 请求/视图 DTO

/// <summary>Step1 上传项：带 <see cref="SlotCode"/> = 槽位模式；不带 = 指派模式（D8 模板外文件）</summary>
public class UploadInitItem
{
    public string? SlotCode { get; set; }
    public string? FolderCode { get; set; }
    public string FileName { get; set; } = "";
    public long FileSize { get; set; }
}

/// <summary>标准卡片主数据（05 分册 §8.1）</summary>
public class StandardDirectoryView
{
    public bool Configured { get; set; }
    public string? Message { get; set; }
    public string EnterpriseCode { get; set; } = "";
    public string StageCode { get; set; } = "";
    public string StandardCode { get; set; } = "";
    public string? StandardNo { get; set; }
    public string? StandardName { get; set; }
    public string EnterpriseConfigCode { get; set; } = "";
    public string TemplateConfigCode { get; set; } = "";
    public List<StandardDirectoryFolderView> Folders { get; set; } = new();
    public List<StandardDirectoryFileView> Files { get; set; } = new();
    public StandardDirectorySummary Summary { get; set; } = new();
}

public class StandardDirectoryFolderView
{
    public string Code { get; set; } = "";
    public string? ParentCode { get; set; }
    public string FolderName { get; set; } = "";
    public int Depth { get; set; }
    public int SortOrder { get; set; }
    public string FullPath { get; set; } = "";
}

public class StandardDirectoryFileView
{
    /// <summary>
    /// ★ 提取状态五态（2026-09-30 用户裁决，页面「提取状态」列唯一口径）：
    /// <c>not_configured</c> 未配置（四元组无可用规则，不算失败）· <c>queued</c> 提取中（文件资源锁被占用）·
    /// <c>extracted</c> 已提取 · <c>failed</c> 提取失败（有规则但执行失败：文档与规则不匹配 / 无法解析 / AI 返回空）·
    /// <c>pending</c> 未提取（尚未跑过）。
    /// </summary>
    public string ExtractState { get; set; } = "pending";

    public string Code { get; set; } = "";
    /// <summary>标准 Code（冗余列）——前端局部刷新按它定位标准 Tab</summary>
    public string? StandardCode { get; set; }
    /// <summary>阶段 Code（冗余列）</summary>
    public string? StageCode { get; set; }
    public string FolderCode { get; set; } = "";
    public string FolderName { get; set; } = "";
    public string FolderPath { get; set; } = "";
    public string FileName { get; set; } = "";
    public string? FileType { get; set; }
    public string? FullPath { get; set; }
    public bool IsRequired { get; set; }
    public int VersionNumber { get; set; }
    public int IsValid { get; set; }
    public string? StandardFileCode { get; set; }
    public bool ExtractionEnabled { get; set; }
    public long? FileSize { get; set; }
    public string? StoragePath { get; set; }
    public string? PreviewPdfPath { get; set; }
    public string? MarkdownPath { get; set; }
    public string? UploadStatus { get; set; }
    public string? ConvertStatus { get; set; }
    public string? ConvertMessage { get; set; }
    public string? MarkdownStatus { get; set; }
    public string? MarkdownMessage { get; set; }
    public string? ExtractStatus { get; set; }
    /// <summary>★ 提取失败/跳过原因（页面 tooltip 用；成功时是「提取字段 N 个、表格 M 张」）</summary>
    public string? ExtractMessage { get; set; }
    /// <summary>缺失/上传中/已上传/转换中/已就绪/转换失败/已移除（01 §5.1）</summary>
    public string Status { get; set; } = "";
    public System.DateTime? UpdateTime { get; set; }
    public System.DateTime? CreateTime { get; set; }
}

public static class ExtractStateRule
{
    /// <summary>五态判定（与服务端组装同一逻辑，供复用/测试）。</summary>
    public static string Of(string? code, string? rawExtractStatus,
        System.Collections.Generic.Dictionary<string, CertPlatform.Admin.Services.DocExtraction.ExtractionSlot> ruleMap,
        System.Collections.Generic.HashSet<string> lockedFiles)
    {
        var c = code ?? "";
        // ★ 排队/执行中优先：资源锁在手时 DB 里可能还是上一轮的终态（尤其重试期间）
        if (lockedFiles.Contains(c)) return "queued";

        // ★★ 2026-09-30 用户裁决：直接读后端状态，不再依赖 ruleMap 命中。
        //   旧实现只在 resolver 覆盖到该行时才判 not_configured，未覆盖的行即使后端已是
        //   skipped 也会落到 pending（页面显示「未提取」）—— 这是历史文件看起来「未提取」的真因之一。
        //   同时 failed 独立成态：「有规则但执行失败」必须与「无规则」区分开。
        var display = EnterpriseExtractStatus.ForDisplay(rawExtractStatus);
        if (display == EnterpriseExtractStatus.Failed) return "failed";
        if (display == EnterpriseExtractStatus.Skipped) return "not_configured";
        if (display == EnterpriseExtractStatus.Completed) return "extracted";

        // 兜底：resolver 有明确结论且无可用规则 ⇒ 未配置（字典缺失 = 该行未参与判定，不误报）
        if (ruleMap.TryGetValue(c, out var slot) && string.IsNullOrEmpty(slot.RuleCode)) return "not_configured";
        return "pending";
    }
}

public class StandardDirectorySummary
{
    public int TotalRequired { get; set; }
    public int Live { get; set; }
    public int Ready { get; set; }
    public int Converting { get; set; }
    public int Missing { get; set; }
}

#endregion

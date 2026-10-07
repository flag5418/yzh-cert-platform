
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.DataBase.Models;
using YZH.Core.DataBase.Services;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Queue;
using CertPlatform.Admin.Entities.Dir;
using CertPlatform.Admin.Entities.Cert;
using CertPlatform.Admin.Entities.Sys;
using CertPlatform.Admin.Entities.Wf;
using CertPlatform.Shared.Storage;

namespace CertPlatform.Admin.Services.StandardDirectory;

/// <summary>
/// 标准目录管理核心服务
/// 职责：组织树、文件夹/文件 CRUD、上传 4 步、下载
/// ORM：IDbOrm（SqlSugar）
/// 存储：IObjectStorage（MinIO/阿里云 OSS）
/// </summary>
public class StandardDirectoryService
{
    private readonly IDbOrm _db;
    private readonly IObjectStorage _storage;
    private readonly IConfiguration _configuration;
    private readonly ILogger<StandardDirectoryService> _logger;
    private readonly QueueManager _queueManager;

    private static readonly JsonSerializerOptions PayloadJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    // 服务器端文件类型白名单（跳过 .DS_Store 等）
    private static readonly HashSet<string> IgnoredFileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".DS_Store", ".Thumbs.db", "desktop.ini"
    };

    public StandardDirectoryService(
        IDbOrm db,
        IObjectStorage storage,
        IConfiguration configuration,
        ILogger<StandardDirectoryService> logger,
        QueueManager queueManager)
    {
        _db = db;
        _storage = storage;
        _configuration = configuration;
        _logger = logger;
        _queueManager = queueManager;
    }

    #region 组织树（org → standard → phase）

    /// <summary>
    /// 获取三级组织树：认证机构 → 标准 → 阶段
    /// </summary>
    public async Task<List<object>> GetOrganizationTreeAsync()
    {
        var orgs = (await _db.GetListAsync<CertificationBody>(x => x.IsValid == 1)).Data ?? new();
        var standards = (await _db.GetListAsync<ISOStandard>(x => x.IsValid == 1)).Data ?? new();
        var orgStandards = (await _db.GetListAsync<CertOrgStandard>()).Data ?? new();
        var orgStages = (await _db.GetListAsync<CertOrgStage>()).Data ?? new();

        // CertStage 是机构-阶段关联（cert_org_stage.StageCode）的权威来源
        // 与「认证阶段定义」页（/cert/cert-stage）同源
        var phaseDefs = (await _db.GetListAsync<CertStage>(x => x.IsValid == 1 && !x.IsDeleted))
                        .Data?.OrderBy(x => x.SortOrder).ThenBy(x => x.StageCode).ToList() ?? new();

        // 目录配置 Code 索引：前端阶段节点用它作为 DirectoryCode（决策 ⑳修订：按机构隔离）
        var configs = (await _db.GetListAsync<StandardDirectoryConfig>(x => x.IsValid == 1)).Data ?? new();
        var configCodeMap = configs
            .GroupBy(x => $"{x.OrgCode}|{x.StandardCode}|{x.StageCode}")
            .ToDictionary(g => g.Key, g => g.First().Code);

        var tree = new List<object>();

        foreach (var org in orgs)
        {
            var orgNode = new Dictionary<string, object>
            {
                ["id"] = org.Code,
                ["label"] = org.Name,
                ["type"] = "organization",
                ["cbCode"] = org.Code,
                ["children"] = new List<object>()
            };
            var orgChildren = (List<object>)orgNode["children"];

            // 该机构关联的标准
            var orgStdCodes = orgStandards
                .Where(x => x.OrgCode == org.Code)
                .Select(x => x.StandardCode).ToList();
            var linkedStandards = standards
                .Where(x => orgStdCodes.Contains(x.Code)).ToList();

            foreach (var std in linkedStandards)
            {
                var stdNode = new Dictionary<string, object>
                {
                    ["id"] = $"{org.Code}|{std.StandardCode}",
                    ["label"] = $"{std.StandardCode} - {std.StandardName}",
                    ["type"] = "standard",
                    ["cbCode"] = org.Code,
                    ["stdCode"] = std.Code,
                    ["standardCode"] = std.StandardCode,
                    ["standardName"] = std.StandardName,
                    ["children"] = new List<object>()
                };
                var stdChildren = (List<object>)stdNode["children"];

                // 该机构+标准关联的阶段：cert_org_stage.StageCode == CertStage.StageCode
                // StandardCode==null 表示适用于所有标准
                var orgStageCodes = orgStages
                    .Where(x => x.OrgCode == org.Code
                        && (x.StandardCode == null || x.StandardCode == std.Code))
                    .Select(x => x.StageCode).ToHashSet();

                // CertStage 是权威数据源，按 SortOrder 排序展示
                var linkedPhases = phaseDefs
                    .Where(p => orgStageCodes.Contains(p.StageCode))
                    .ToList();

                foreach (var phase in linkedPhases)
                {
                    // ★ 键必须是 org + phase.Code（cert_cert_stage.Code，GUID）——
                    //   config.StageCode 存的是该 GUID（见迁移脚本列注释），不是 'jd01' 这种 StageCode；
                    //   决策⑳修订后主键含 OrgCode ⇒ 键必须带机构，否则机构 A 的配置会被机构 B 命中。
                    configCodeMap.TryGetValue($"{org.Code}|{std.Code}|{phase.Code}", out var configCode);
                    var phaseNode = new Dictionary<string, object>
                    {
                        ["id"] = $"{org.Code}|{std.StandardCode}|{phase.StageCode}",
                        ["label"] = $"{phase.StageCode} - {phase.StageName}",
                        ["type"] = "phase",
                        ["cbCode"] = org.Code,
                        ["stdCode"] = std.Code,
                        ["standardCode"] = std.StandardCode,
                        // ★★ 2026-09-30 阶段口径统一（22 号 §1.1 / 06 号 §4.1）：
                        //   phaseCode 由【业务码 'jd01'】改为【GUID = cert_cert_stage.Code】。
                        //   原因：配置层 cert_validation_rule.PhaseCode / cert_report_section.PhaseCode
                        //        已迁移为 GUID（scripts/db/fix/fix-stage-code-align-2026-09-30.sql）。
                        //   影响链：树节点 phaseCode → useFileTree.Extra.PhaseCode
                        //          → nc-config / report-rule 的 RelateField + onPrepareAdd
                        //   ⇒ 全链路自动变 GUID，前端无需改动。
                        //   ⛔ 不要再改回 phase.StageCode —— 那会让新规则又存业务码，
                        //      与 cert_enterprise_stage.StageCode（GUID）永远 join 不上，静默 0 行。
                        ["phaseCode"] = phase.Code,
                        ["phaseName"] = phase.StageName,
                        ["phaseDefinitionCode"] = phase.Code,
                        // 目录配置 Code（null = 该「标准×阶段」尚未建配置 → 前端不加载、不上传）
                        ["configCode"] = configCode
                    };
                    stdChildren.Add(phaseNode);
                }
                orgChildren.Add(stdNode);
            }
            tree.Add(orgNode);
        }
        return tree;
    }

    #endregion

    #region 资料清单树（供「标准文档填写规则」页左树）

    /// <summary>
    /// ★★ 资料清单树 + 空白模板状态（2026-10-04 新增）。
    ///
    /// <para><b>为什么必须由资料清单驱动</b>：空白模板是<b>从标准资料清单的文件加工出来的</b>
    /// （下载 → 本地加 <c>{{标签}}</c> → 上传空白模板 → 再配规则），⛔ <b>不是凭空产生的</b>。
    /// 所以左树必须以资料清单为骨架 —— 否则「还没上传模板的文件」在页面上根本不存在，
    /// 用户无从下手。<b>实测</b>：旧实现按 <c>cert_doc_template</c> 构树，
    /// 只显示 1 个已登记模板，而资料清单有 168 份标准文档 ⇒ 用户看到的是「几乎空白的树」。</para>
    ///
    /// <para><b>与「标准资料清单」页同源（保证不漂移）</b>：
    /// 机构 → 标准 → 阶段 三级<b>直接复用 <see cref="GetOrganizationTreeAsync"/></b>；
    /// 文件夹保留段过滤复用 <see cref="CollectReservedFolderCodes"/> ⇒
    /// 两页的树**不可能出现口径差异**（⛔ 不另写一套 org/std/phase 组装逻辑）。</para>
    ///
    /// <para><b>合并进来的模板状态</b>（挂在文件叶子 <c>Extra</c>）：
    /// <c>hasTemplate</c> / <c>templateCode</c> / <c>templateStoragePath</c> /
    /// <c>scanStatus</c> / <c>publishStatus</c> / <c>anchorCount</c> / <c>docCategory</c>。
    /// 页面据此决定操作条可用性与徽标，⛔ 前端不自行推断「有没有模板」。</para>
    ///
    /// <para><b>层级</b>：机构 → 标准 → 阶段 → 文件夹（可嵌套）→ 文件。
    /// 文件夹层是**必须的** —— 资料清单实测有 11 个文件夹、最深 2 层嵌套（如「4记录文件/质量类」），
    /// 拍平后 167 个文件会挤在同一个阶段节点下。</para>
    ///
    /// <para><b>性能</b>：文件夹 / 文件 / 模板 / 锚点各**一次查询**后在内存组树。
    /// 配置数在十量级、文件在百量级（实测 10 / 668），全量取回远优于逐阶段 N+1。</para>
    /// </summary>
    public async Task<List<TemplateDirectoryNode>> GetTemplateDirectoryTreeAsync()
    {
        // ① 组织树（org → standard → phase），与资料清单页同源
        var raw = await GetOrganizationTreeAsync();

        var root = new List<TemplateDirectoryNode>();
        // (阶段节点, 目录配置Code, 机构Code, 标准GUID, 阶段GUID) —— 只有配了目录的阶段才可能有文件
        var phases = new List<(TemplateDirectoryNode Node, string ConfigCode, string OrgCode, string StdCode, string StageCode)>();

        foreach (var orgObj in raw)
        {
            if (orgObj is not Dictionary<string, object> o) continue;

            var orgNode = new TemplateDirectoryNode
            {
                Code = Str(o, "id"),
                Name = Str(o, "label"),
                Extra = new() { ["kind"] = "org", ["orgCode"] = Str(o, "cbCode") },
            };
            root.Add(orgNode);

            if (o.GetValueOrDefault("children") is not List<object> stds) continue;
            foreach (var stdObj in stds)
            {
                if (stdObj is not Dictionary<string, object> s) continue;

                var stdNode = new TemplateDirectoryNode
                {
                    Code = Str(s, "id"),
                    Name = Str(s, "label"),
                    Extra = new()
                    {
                        ["kind"] = "standard",
                        ["orgCode"] = Str(s, "cbCode"),
                        ["stdCode"] = Str(s, "stdCode"),
                        ["standardNo"] = Str(s, "standardCode"),
                    },
                };
                orgNode.Children.Add(stdNode);

                if (s.GetValueOrDefault("children") is not List<object> phs) continue;
                foreach (var phObj in phs)
                {
                    if (phObj is not Dictionary<string, object> p) continue;

                    var configCode = Str(p, "configCode");
                    var phaseNode = new TemplateDirectoryNode
                    {
                        Code = Str(p, "id"),
                        Name = Str(p, "label"),
                        Extra = new()
                        {
                            ["kind"] = "stage",
                            ["orgCode"] = Str(p, "cbCode"),
                            ["stdCode"] = Str(p, "stdCode"),
                            ["standardNo"] = Str(p, "standardCode"),
                            ["phaseCode"] = Str(p, "phaseCode"),
                            ["phaseName"] = Str(p, "phaseName"),
                            ["configCode"] = configCode,
                        },
                    };
                    stdNode.Children.Add(phaseNode);

                    if (configCode.Length > 0)
                    {
                        phases.Add((phaseNode, configCode, Str(p, "cbCode"), Str(p, "stdCode"), Str(p, "phaseCode")));
                    }
                }
            }
        }

        if (phases.Count == 0) return root;

        var configCodes = phases.Select(x => x.ConfigCode).Distinct().ToList();

        // ② 文件夹 / 文件：各一次查询（⛔ 不逐阶段查 —— 那会是 2×N 次往返）
        var allFolders = (await _db.GetListAsync<StandardDirectoryFolder>(x => x.IsValid == 1)).Data ?? new();
        var allFiles = (await _db.GetListAsync<StandardDirectoryFile>(x => x.IsValid == 1)).Data ?? new();

        var scopedFolders = allFolders
            .Where(f => !string.IsNullOrEmpty(f.ConfigCode) && configCodes.Contains(f.ConfigCode!))
            .ToList();
        var scopedFiles = allFiles
            .Where(f => !string.IsNullOrEmpty(f.ConfigCode) && configCodes.Contains(f.ConfigCode))
            .ToList();

        // 保留段过滤（pdf / markdown / editable / _template / _archive）—— 与资料清单页同一份口径
        var reservedCodes = CollectReservedFolderCodes(scopedFolders);
        if (reservedCodes.Count > 0)
        {
            scopedFolders = scopedFolders.Where(f => !reservedCodes.Contains(f.Code ?? "")).ToList();
            scopedFiles = scopedFiles.Where(f => !reservedCodes.Contains(f.FolderCode ?? "")).ToList();
        }

        // ③ 空白模板（按宿主标准文件 Code 索引）
        var fileCodes = scopedFiles.Select(f => f.Code ?? "").Where(c => c.Length > 0).Distinct().ToList();
        var templates = fileCodes.Count == 0
            ? new List<DocTemplate>()
            : (await _db.GetListAsync<DocTemplate>(t => fileCodes.Contains(t.StandardFileCode!))).Data ?? new();

        var tplByFile = templates
            .Where(t => !string.IsNullOrEmpty(t.StandardFileCode))
            .GroupBy(t => t.StandardFileCode, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        // ④ 锚点统计（一次分组查询，⛔ 不逐模板 count）
        var templateCodes = templates.Select(t => t.Code ?? "").Where(c => c.Length > 0).Distinct().ToList();
        var anchorRows = templateCodes.Count == 0
            ? new List<DocTemplateAnchor>()
            : (await _db.GetListAsync<DocTemplateAnchor>(
                a => templateCodes.Contains(a.TemplateCode) && a.IsDeleted == false && a.IsValid == 1)).Data ?? new();

        var anchorStat = anchorRows
            .GroupBy(a => a.TemplateCode ?? "", StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => (Total: g.Count(), Orphan: g.Count(x => x.IsOrphan)),
                StringComparer.Ordinal);

        // ⑤ 逐阶段组装文件夹树 + 文件叶子
        foreach (var (phaseNode, configCode, orgCode, stdCode, stageCode) in phases)
        {
            var folders = scopedFolders.Where(f => f.ConfigCode == configCode).ToList();
            var files = scopedFiles.Where(f => f.ConfigCode == configCode).ToList();

            // 先建索引再挂父子：脏数据里子文件夹排在父文件夹之前时不会漏挂
            var folderByCode = new Dictionary<string, TemplateDirectoryNode>(StringComparer.Ordinal);
            foreach (var f in folders)
            {
                var fc = f.Code ?? "";
                if (fc.Length == 0) continue;
                folderByCode[fc] = new TemplateDirectoryNode
                {
                    Code = fc,
                    Name = f.FolderName ?? "",
                    Extra = new() { ["kind"] = "folder", ["depth"] = f.Depth },
                };
            }

            foreach (var f in folders)
            {
                if (!folderByCode.TryGetValue(f.Code ?? "", out var node)) continue;
                var parentCode = f.ParentCode ?? "";
                // ParentCode 为空、或父文件夹本身被过滤/跨配置 ⇒ 挂到阶段根（⛔ 不静默丢弃）
                if (parentCode.Length > 0 && folderByCode.TryGetValue(parentCode, out var parentNode))
                    parentNode.Children.Add(node);
                else
                    phaseNode.Children.Add(node);
            }

            foreach (var f in files)
            {
                var node = BuildTemplateFileNode(f, tplByFile, anchorStat, orgCode, stdCode, stageCode);
                var folderCode = f.FolderCode ?? "";
                if (folderCode.Length > 0 && folderByCode.TryGetValue(folderCode, out var folderNode))
                    folderNode.Children.Add(node);
                else
                    phaseNode.Children.Add(node);
            }
        }

        MarkLeaf(root);
        return root;
    }

    /// <summary>资料清单文件行 → 树叶子（合并空白模板状态）</summary>
    private static TemplateDirectoryNode BuildTemplateFileNode(
        StandardDirectoryFile f,
        Dictionary<string, DocTemplate> tplByFile,
        Dictionary<string, (int Total, int Orphan)> anchorStat,
        string orgCode,
        string stdCode,
        string stageCode)
    {
        var code = f.Code ?? "";
        tplByFile.TryGetValue(code, out var tpl);
        var anchors = tpl != null && anchorStat.TryGetValue(tpl.Code ?? "", out var st)
            ? st
            : (Total: 0, Orphan: 0);

        return new TemplateDirectoryNode
        {
            Code = code,
            Name = f.FileName,
            IsLeaf = true,
            Extra = new()
            {
                ["kind"] = "file",
                // ★ 宿主标准文件 —— 下载原始文档 / 上传空白模板 / 读写文档契约都用它
                ["standardFileCode"] = code,
                ["orgCode"] = orgCode,
                ["stdCode"] = stdCode,
                ["stageCode"] = stageCode,
                ["fileType"] = f.FileType ?? "",
                // 资料清单里的原始文档（「下载原始件」用）
                ["standardStoragePath"] = f.StoragePath ?? "",
                // ★ 归一产物（.docx/.xlsx）——「下载可编辑版」的正确起点：
                //   用户要的是「下载后能加工成空白模板」的文件，而 .doc/.xls 连读都读不了
                //   （NPOI 2.7.2 无 HWPF）⇒ 只要归一产物就绪，就该下载它
                ["standardEditablePath"] = f.EditableStoragePath ?? "",
                ["standardEditableStatus"] = f.EditableStatus ?? "",
                // ★ 权威分类列（37 号 §3.6）：决定「这个文档是否不需要编辑」
                ["docCategory"] = string.IsNullOrWhiteSpace(f.DocCategory) ? "editable" : f.DocCategory,
                // ★ 空白模板（为空 = 还没上传 ⇒ 页面只放行「下载 + 上传」）
                ["hasTemplate"] = tpl != null,
                ["templateCode"] = tpl?.Code ?? "",
                ["templateStoragePath"] = tpl?.StoragePath ?? "",
                ["templateFileName"] = tpl?.FileName ?? "",
                ["scanStatus"] = tpl?.ScanStatus ?? "",
                ["publishStatus"] = tpl?.PublishStatus ?? "",
                ["fillPromptCode"] = tpl?.FillPromptCode ?? "",
                ["anchorCount"] = anchors.Total,
                ["orphanCount"] = anchors.Orphan,
            },
        };
    }

    /// <summary>自底向上标记叶子（叶子的判据 = 无子节点）</summary>
    private static void MarkLeaf(List<TemplateDirectoryNode> nodes)
    {
        foreach (var n in nodes)
        {
            n.IsLeaf = n.Children.Count == 0;
            MarkLeaf(n.Children);
        }
    }

    /// <summary>从 <c>GetOrganizationTreeAsync</c> 的字典里取字符串（缺失/非字符串/null ⇒ 空串）</summary>
    private static string Str(Dictionary<string, object> d, string key)
        => d.TryGetValue(key, out var v) && v is string s ? s : "";

    #endregion

    #region 目录配置 CRUD

    public async Task<List<StandardDirectoryConfig>> GetConfigsAsync()
    {
        return (await _db.GetListAsync<StandardDirectoryConfig>(x => x.IsValid == 1)).Data ?? new();
    }

    public async Task<StandardDirectoryConfig?> GetConfigAsync(string directoryCode)
    {
        return (await _db.GetOneAsync<StandardDirectoryConfig>(x => x.Code == directoryCode)).Data;
    }

    public async Task<(bool ok, string? error, StandardDirectoryConfig? data)> CreateConfigAsync(
        StandardDirectoryConfig config)
    {
        // 业务键必填（决策⑳修订：三键 uk = OrgCode + StandardCode + StageCode）
        if (string.IsNullOrWhiteSpace(config.OrgCode))
            return (false, "缺少业务键：请选择机构（OrgCode）", null);
        if (string.IsNullOrWhiteSpace(config.StandardCode))
            return (false, "缺少业务键：请选择标准（StandardCode）", null);
        if (string.IsNullOrWhiteSpace(config.StageCode))
            return (false, "缺少业务键：请选择阶段（StageCode）", null);

        // ★⑬/§11.1：uk_org_std_stage 不含 IsDeleted → 建前必须「含已删」查重，命中已删行就地复活；
        //   ⛔ 不能只靠下面的 1062 兜底 —— 默认过滤把已删行藏起来，用户会看到
        //   「已存在」却在列表里找不到那行（删 → 再建永远失败）。
        var existing = await _db.Client.Queryable<StandardDirectoryConfig>()
            .Where(x => x.OrgCode == config.OrgCode
                     && x.StandardCode == config.StandardCode && x.StageCode == config.StageCode)
            .FirstAsync();
        if (existing != null)
        {
            if (!existing.IsDeleted)
                return (false, "该标准与阶段的目录配置已存在", null);

            // 复活：同一行（Id/Code 不变 —— Code 被树节点、文件夹、文件引用），业务字段刷新
            existing.IsDeleted = false;
            existing.DeleteBy = null;
            existing.DeleteTime = null;
            existing.IsValid = 1;
            existing.RootFolderName = config.RootFolderName;
            existing.Status = "draft";
            existing.StatusField = config.StatusField ?? existing.StatusField;
            existing.Sort = config.Sort;
            existing.Remark = config.Remark;
            existing.UpdateTime = DateTime.Now;
            await _db.UpdateAsync(existing,
                nameof(StandardDirectoryConfig.IsDeleted), nameof(StandardDirectoryConfig.DeleteBy),
                nameof(StandardDirectoryConfig.DeleteTime), nameof(StandardDirectoryConfig.IsValid),
                nameof(StandardDirectoryConfig.RootFolderName), nameof(StandardDirectoryConfig.Status),
                nameof(StandardDirectoryConfig.StatusField), nameof(StandardDirectoryConfig.Sort),
                nameof(StandardDirectoryConfig.Remark), nameof(StandardDirectoryConfig.UpdateTime));
            return (true, null, existing);
        }

        // ⚠️ P1-9：原实现直接 `return (await InsertAsync).Data`，失败时 Data 为 null 却被控制器
        //    无条件包成 Ok → 前端收到「创建成功」+ data:null。改为三元组按 ok 分支。
        config.Code = Guid.NewGuid().ToString("N");
        config.IsValid = 1;
        config.Status = "draft";
        config.CreateTime = DateTime.Now;
        try
        {
            var result = await _db.InsertAsync(config);
            if (result.Data == null)
                return (false, string.IsNullOrWhiteSpace(result.Error) ? "创建失败" : result.Error, null);
            return (true, null, result.Data);
        }
        catch (Exception ex) when (IsDuplicateKeyError(ex))
        {
            // 唯一键 uk_org_std_stage(OrgCode, StandardCode, StageCode)：同一机构+标准+阶段只能有一个目录
            return (false, "该机构、标准与阶段的目录配置已存在", null);
        }
    }

    /// <summary>
    /// 总表无感懒建（决策㉑，2026-09-27）：幂等返回「机构 × 标准 × 阶段」的目录配置，不存在则自动创建。
    /// <para>★ 这是「目录配置不要求用户手工创建」的唯一入口 —— 前端在<b>首次进入阶段 / 上传</b>时调
    /// <c>POST configs/ensure</c>，树节点拿到 configCode 后直接可用。</para>
    /// <list type="bullet">
    ///   <item>存在且有效 → 原样返回（不触碰任何字段）；</item>
    ///   <item>存在但已软删 → 就地复活同 Code 行（子树保持已删 = 空目录，见 CreateConfigAsync）；</item>
    ///   <item>不存在 → 新建（Status=draft，RootFolderName 留空，后续在「目录配置」管理界面改）；</item>
    ///   <item>并发竞态（撞 uk 报「已存在」）→ 重读返回赢家行，保证幂等。</item>
    /// </list>
    /// </summary>
    public async Task<(bool ok, string? error, StandardDirectoryConfig? data)> EnsureConfigAsync(
        string orgCode, string standardCode, string stageCode)
    {
        if (string.IsNullOrWhiteSpace(orgCode))
            return (false, "缺少业务键：机构 OrgCode", null);
        if (string.IsNullOrWhiteSpace(standardCode))
            return (false, "缺少业务键：标准 StandardCode", null);
        if (string.IsNullOrWhiteSpace(stageCode))
            return (false, "缺少业务键：阶段 StageCode", null);

        var query = () => _db.Client.Queryable<StandardDirectoryConfig>()
            .Where(x => x.OrgCode == orgCode
                     && x.StandardCode == standardCode && x.StageCode == stageCode);

        var existing = await query().FirstAsync();
        if (existing != null && !existing.IsDeleted)
            return (true, null, existing);

        var created = await CreateConfigAsync(new StandardDirectoryConfig
        {
            OrgCode = orgCode,
            StandardCode = standardCode,
            StageCode = stageCode
        });
        if (created.ok) return created;

        // 并发：另一请求刚插入 → 「已存在」→ 重读返回赢家行
        var again = await query().FirstAsync();
        if (again != null) return (true, null, again);
        return created;
    }

    /// <summary>
    /// 更新目录配置。
    /// <para>★ P0-5 修复：禁止把客户端实体直接入库 —— 框架单参 <c>UpdateAsync</c> 按 <c>Code</c>
    /// 定位且写全部列，客户端在请求体里带上别的配置的 <c>Code</c> 即可劫持任意行（含 IsValid
    /// 远程删除）。现改为「先按路由 Code 查 existing → 白名单字段拷贝 → 更新 existing」。</para>
    /// </summary>
    public async Task<(bool ok, string? error)> UpdateConfigAsync(string directoryCode, StandardDirectoryConfig input)
    {
        var existing = (await _db.GetOneAsync<StandardDirectoryConfig>(x => x.Code == directoryCode)).Data;
        if (existing == null) return (false, "配置不存在");

        existing.RootFolderName = input.RootFolderName;
        existing.Status = input.Status;
        existing.StatusField = input.StatusField;
        existing.Sort = input.Sort;
        existing.Remark = input.Remark;
        existing.IsValid = input.IsValid;
        existing.UpdateTime = DateTime.Now;

        var result = await _db.UpdateAsync(existing,
            nameof(StandardDirectoryConfig.RootFolderName),
            nameof(StandardDirectoryConfig.Status),
            nameof(StandardDirectoryConfig.StatusField),
            nameof(StandardDirectoryConfig.Sort),
            nameof(StandardDirectoryConfig.Remark),
            nameof(StandardDirectoryConfig.IsValid),
            nameof(StandardDirectoryConfig.UpdateTime));
        // ⚠️ 必须用 Success（Error == null）判定：Result<T>.Ok() 的 Code 为 **null**（不设 200），
        //    写成 `result.Code == 200` 会恒为 false → 更新已落库却回报「更新失败」。
        return result.Success ? (true, null) : (false, result.Error ?? "更新失败");
    }

    /// <summary>
    /// 删除目录配置（软删 + 级联软删文件夹/文件 + 取消关联队列）。
    /// <para>★ P1-7/P1-8 修复：删除走 <c>IsDeleted</c>（不再借用 IsValid）；级联软删子树 ——
    /// 原实现只置配置 IsValid=0，而目录级查询只按 ConfigCode 过滤不看配置，
    /// 导致「删掉的目录仍完整可见、可上传」。</para>
    /// </summary>
    public async Task<(bool ok, string? error)> DeleteConfigAsync(string directoryCode)
    {
        var config = (await _db.GetOneAsync<StandardDirectoryConfig>(x => x.Code == directoryCode)).Data;
        if (config == null) return (false, "配置不存在");

        var lockErr = await GetQueueLockErrorAsync(directoryCode);
        if (lockErr != null) return (false, lockErr);

        var now = DateTime.Now;

        // 级联软删文件（先标状态，再删对象 —— P1-15：顺序反了 DB 失败即不可逆）
        var files = (await _db.GetListAsync<StandardDirectoryFile>(
            x => x.ConfigCode == directoryCode && !x.IsDeleted, includeDisabled: true)).Data ?? new();
        foreach (var file in files)
        {
            await DeleteFileFromStorageAsync(file);
            file.IsDeleted = true;
            file.IsValid = 0;
            file.DeleteBy = "system";
            file.DeleteTime = now;
        }
        if (files.Count > 0)
            await _db.UpdateAsync(files, nameof(StandardDirectoryFile.IsDeleted),
                nameof(StandardDirectoryFile.IsValid), nameof(StandardDirectoryFile.DeleteBy),
                nameof(StandardDirectoryFile.DeleteTime));

        // 级联软删文件夹
        var folders = (await _db.GetListAsync<StandardDirectoryFolder>(
            x => x.ConfigCode == directoryCode && !x.IsDeleted, includeDisabled: true)).Data ?? new();
        foreach (var folder in folders)
        {
            folder.IsDeleted = true;
            folder.IsValid = 0;
            folder.DeleteBy = "system";
            folder.DeleteTime = now;
        }
        if (folders.Count > 0)
            await _db.UpdateAsync(folders, nameof(StandardDirectoryFolder.IsDeleted),
                nameof(StandardDirectoryFolder.IsValid), nameof(StandardDirectoryFolder.DeleteBy),
                nameof(StandardDirectoryFolder.DeleteTime));

        config.IsDeleted = true;
        config.IsValid = 0;
        config.DeleteBy = "system";
        config.DeleteTime = now;
        var result = await _db.UpdateAsync(config,
            nameof(StandardDirectoryConfig.IsDeleted), nameof(StandardDirectoryConfig.IsValid),
            nameof(StandardDirectoryConfig.DeleteBy), nameof(StandardDirectoryConfig.DeleteTime));
        // ⚠️ 同上：`result.Code == 200` 恒 false → 软删已生效却回报「删除失败」。
        return result.Success ? (true, null) : (false, result.Error ?? "删除失败");
    }

    /// <summary>
    /// 目录级查询的前置校验：配置必须存在且有效（P1-8 后半 —— 软删/禁用的目录不再可读写）。
    /// </summary>
    private async Task<string?> GetConfigInvalidErrorAsync(string directoryCode)
    {
        if (string.IsNullOrWhiteSpace(directoryCode)) return "缺少目录标识";
        var config = (await _db.GetOneAsync<StandardDirectoryConfig>(x => x.Code == directoryCode)).Data;
        if (config == null) return "目录配置不存在或已删除";
        return null;
    }

    #endregion

    #region 文件夹 CRUD

    /// <summary>
    /// 获取文件夹树（仅有效文件夹）
    /// </summary>
    public async Task<List<StandardDirectoryFolder>> GetFolderTreeAsync(string directoryCode)
    {
        var folders = (await _db.GetListAsync<StandardDirectoryFolder>(
            x => x.ConfigCode == directoryCode && x.IsValid == 1)).Data ?? new();

        var rootFolders = folders.Where(x => string.IsNullOrEmpty(x.ParentCode))
            .OrderBy(x => x.SortOrder).ToList();

        foreach (var root in rootFolders)
            root.Children = GetChildFolders(folders, root.Code ?? "", new HashSet<string>(), 0);

        return rootFolders;
    }

    /// <summary>
    /// 获取所有文件夹（扁平列表，前端按 ParentCode 过滤实现面包屑导航）
    /// </summary>
    /// <remarks>
    /// ★ 与 <see cref="GetStageFileTreeAsync"/> 保持一致：过滤掉「产物 / 系统目录」
    /// （pdf / markdown / editable / _template / _archive，见 <see cref="PathBuilder.ReservedSegments"/>），
    /// 否则左树不显示、右侧却能列出来的两棵树会不一致。
    /// </remarks>
    public async Task<List<StandardDirectoryFolder>> GetFoldersFlatAsync(string directoryCode)
    {
        var folders = (await _db.GetListAsync<StandardDirectoryFolder>(
            x => x.ConfigCode == directoryCode && x.IsValid == 1)).Data ?? new();

        var reserved = CollectReservedFolderCodes(folders);
        if (reserved.Count == 0) return folders;
        return folders.Where(f => !reserved.Contains(f.Code ?? "")).ToList();
    }

    /// <summary>
    /// 收集「产物 / 系统目录」及其全部子孙的 Code（大小写不敏感）。
    /// <para>用于显示层过滤 <c>pdf</c> / <c>markdown</c> / <c>editable</c> / <c>_template</c> / <c>_archive</c> 保留段名文件夹。</para>
    /// <para>★ 带环检测与深度上限 —— 脏数据 ParentCode 成环时不能无限递归。</para>
    /// </summary>
    private static HashSet<string> CollectReservedFolderCodes(List<StandardDirectoryFolder> folders)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        var roots = folders.Where(f => IsReservedSegmentName(f.FolderName)).ToList();
        if (roots.Count == 0) return result;

        // 子孙展开（迭代而非递归：脏数据成环时不会栈溢出）
        var frontier = roots.ToList();
        while (frontier.Count > 0)
        {
            var next = new List<StandardDirectoryFolder>();
            foreach (var node in frontier)
            {
                var code = node.Code ?? "";
                if (string.IsNullOrEmpty(code) || !result.Add(code)) continue;
                foreach (var child in folders.Where(f => f.ParentCode == code))
                {
                    if (!result.Contains(child.Code ?? "")) next.Add(child);
                }
            }
            if (result.Count > MAX_TREE_NODES) break;
            frontier = next;
        }
        return result;
    }

    /// <summary>文件夹名是否为产物 / 系统保留段名（pdf / markdown / editable / _template / _archive）</summary>
    private static bool IsReservedSegmentName(string? name)
        => !string.IsNullOrWhiteSpace(name)
           && PathBuilder.ReservedSegments.Any(s =>
               string.Equals(s, name.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// 递归取子文件夹。
    /// <para>★ P0-6 修复：带 <paramref name="visited"/> + 深度上限 ——
    /// <c>ParentCode</c> 成环（A→B→A）时原实现无限递归直至内存耗尽。</para>
    /// </summary>
    private List<StandardDirectoryFolder> GetChildFolders(
        List<StandardDirectoryFolder> allFolders, string parentCode,
        HashSet<string> visited, int depth)
    {
        if (depth > MAX_TREE_DEPTH || visited.Count > MAX_TREE_NODES) return new();
        var children = allFolders.Where(x => x.ParentCode == parentCode && !visited.Contains(x.Code ?? ""))
            .OrderBy(x => x.SortOrder).ToList();
        foreach (var child in children)
        {
            if (child.Code != null) visited.Add(child.Code);
            child.Children = GetChildFolders(allFolders, child.Code ?? "", visited, depth + 1);
        }
        return children;
    }

    /// <summary>树遍历硬上限：深度超限视为脏数据（正常目录树 ≤ 6 层）</summary>
    private const int MAX_TREE_DEPTH = 20;
    /// <summary>树遍历节点上限：防御性兜底，避免 visited 失效时被拉爆</summary>
    private const int MAX_TREE_NODES = 5000;

    /// <summary>
    /// 创建文件夹。
    /// <para>★ P1-21/P1-22 修复：复合编码 <c>FD-…</c> 已删除 —— 业务键即 <c>Code</c>（GUID）；
    /// 序号改为按 <c>(ConfigCode, ParentCode)</c> 取 <c>MAX(SortOrder)</c>；
    /// 唯一冲突不再「静默改序号重试 100 次」，而是先查重并明确报错。</para>
    /// </summary>
    public async Task<(bool ok, string? error, StandardDirectoryFolder? folder)> CreateFolderAsync(
        StandardDirectoryFolder folder)
    {
        // 队列锁检查
        var lockErr = await GetQueueLockErrorAsync(folder.ConfigCode ?? "");
        if (lockErr != null) return (false, lockErr, null);

        folder.ParentCode ??= string.Empty; // 决策 ⑨：根 ParentCode 用 ''

        // 同层同名查重（Decision ⑬：唯一键不含 IsDeleted，已删行也参与查重 →
        // 裸查 SqlSugar（不走 GetOneAsync 的 IsValid/软删过滤），项目无全局过滤器）
        var duplicate = await _db.Client.Queryable<StandardDirectoryFolder>()
            .Where(x => x.ConfigCode == folder.ConfigCode && x.ParentCode == folder.ParentCode
                        && x.FolderName == folder.FolderName)
            .FirstAsync();
        if (duplicate != null)
        {
            // ★⑬/§11.1：命中的是**已删**行 → 就地复活（Code 不变，指向它的文件/规则链不失效）
            if (!duplicate.IsDeleted)
                return (false, $"同级已存在同名文件夹「{folder.FolderName}」", null);

            duplicate.IsDeleted = false;
            duplicate.DeleteBy = null;
            duplicate.DeleteTime = null;
            duplicate.IsValid = 1;
            duplicate.Status = "draft";
            duplicate.SortOrder = await GetMaxSortOrderAsync(folder.ConfigCode ?? "", folder.ParentCode) + 1;
            duplicate.FullPath = await BuildFolderFullPathAsync(duplicate);
            duplicate.UpdateTime = DateTime.Now;
            await _db.UpdateAsync(duplicate,
                nameof(StandardDirectoryFolder.IsDeleted), nameof(StandardDirectoryFolder.DeleteBy),
                nameof(StandardDirectoryFolder.DeleteTime), nameof(StandardDirectoryFolder.IsValid),
                nameof(StandardDirectoryFolder.Status), nameof(StandardDirectoryFolder.SortOrder),
                nameof(StandardDirectoryFolder.FullPath), nameof(StandardDirectoryFolder.UpdateTime));
            return (true, null, duplicate);
        }

        folder.Code = Guid.NewGuid().ToString("N");
        folder.SortOrder = await GetMaxSortOrderAsync(folder.ConfigCode ?? "", folder.ParentCode) + 1;
        folder.FullPath = await BuildFolderFullPathAsync(folder);
        folder.IsValid = 1;
        folder.Status = "draft";
        folder.CreateTime = DateTime.Now;

        try
        {
            var result = await _db.InsertAsync(folder);
            if (result.Data != null) return (true, null, result.Data);
            return (false, string.IsNullOrWhiteSpace(result.Error) ? "创建失败" : result.Error, null);
        }
        catch (Exception ex) when (IsDuplicateKeyError(ex))
        {
            return (false, "创建失败：同级已存在同名或冲突的文件夹", null);
        }
    }

    /// <summary>
    /// 更新文件夹（重命名）。
    /// <para>★ P0-5 修复：按路由 <c>folderCode</c> 定位 existing，白名单拷贝 ——
    /// 不再把客户端实体直接交给单参 <c>UpdateAsync</c>（那会按请求体里的 Code 定位别的行）。</para>
    /// </summary>
    public async Task<(bool ok, string? error)> UpdateFolderAsync(string folderCode, StandardDirectoryFolder input)
    {
        var existing = (await _db.GetOneAsync<StandardDirectoryFolder>(
            x => x.Code == folderCode && x.IsValid == 1)).Data;
        if (existing == null) return (false, "文件夹不存在");

        var lockErr = await GetQueueLockErrorAsync(existing.ConfigCode ?? "");
        if (lockErr != null) return (false, lockErr);

        var configErr = await GetConfigInvalidErrorAsync(existing.ConfigCode ?? "");
        if (configErr != null) return (false, configErr);

        if (string.IsNullOrWhiteSpace(input.FolderName))
            return (false, "文件夹名不能为空");
        var nameErr = ValidateFolderOrFileName(input.FolderName, "文件夹名");
        if (nameErr != null) return (false, nameErr);

        // 同层同名查重（排除自己）—— Decision ⑬：裸查含已删行
        var duplicate = await _db.Client.Queryable<StandardDirectoryFolder>()
            .Where(x => x.ConfigCode == existing.ConfigCode && x.ParentCode == existing.ParentCode
                        && x.FolderName == input.FolderName && x.Code != folderCode)
            .FirstAsync();
        if (duplicate != null)
            return (false, $"同级已存在同名文件夹「{input.FolderName}」");

        existing.FolderName = input.FolderName;
        existing.Sort = input.Sort;
        existing.Remark = input.Remark;
        existing.UpdateTime = DateTime.Now;
        await _db.UpdateAsync(existing,
            nameof(StandardDirectoryFolder.FolderName),
            nameof(StandardDirectoryFolder.Sort),
            nameof(StandardDirectoryFolder.Remark),
            nameof(StandardDirectoryFolder.UpdateTime));
        return (true, null);
    }

    /// <summary>
    /// 删除文件夹（递归软删除 + MinIO 清理）
    /// </summary>
    public async Task<(bool ok, string? error, int foldersDeleted, int filesDeleted)> DeleteFolderAsync(
        string folderCode)
    {
        var folder = (await _db.GetOneAsync<StandardDirectoryFolder>(
            x => x.Code == folderCode && x.IsValid == 1)).Data;
        if (folder == null) return (false, "文件夹不存在", 0, 0);

        var lockErr = await GetQueueLockErrorAsync(folder.ConfigCode ?? "");
        if (lockErr != null) return (false, lockErr, 0, 0);

        var configErr = await GetConfigInvalidErrorAsync(folder.ConfigCode ?? "");
        if (configErr != null) return (false, configErr, 0, 0);

        var visited = new HashSet<string>();
        var (foldersDeleted, filesDeleted) = await DeleteFolderRecursiveAsync(folderCode, visited, 0);
        return (true, null, foldersDeleted, filesDeleted);
    }

    /// <summary>
    /// 递归软删除文件夹。
    /// <para>★ P0-6 修复：带 visited + 深度上限 —— 环状 ParentCode 会让原递归 StackOverflow 崩进程。</para>
    /// </summary>
    private async Task<(int foldersDeleted, int filesDeleted)> DeleteFolderRecursiveAsync(
        string folderCode, HashSet<string> visited, int depth)
    {
        int foldersDeleted = 0, filesDeleted = 0;
        if (depth > MAX_TREE_DEPTH || !visited.Add(folderCode))
            return (foldersDeleted, filesDeleted);

        // 递归删除子文件夹
        var children = (await _db.GetListAsync<StandardDirectoryFolder>(
            x => x.ParentCode == folderCode && x.IsValid == 1)).Data ?? new();
        foreach (var child in children)
        {
            if (child.Code == null) continue;
            var (fd, fild) = await DeleteFolderRecursiveAsync(child.Code, visited, depth + 1);
            foldersDeleted += fd;
            filesDeleted += fild;
        }

        // 删除文件夹下的文件（先改 DB 状态再删对象 —— P1-15）
        var files = (await _db.GetListAsync<StandardDirectoryFile>(
            x => x.FolderCode == folderCode && x.IsValid == 1)).Data ?? new();
        foreach (var file in files)
        {
            file.IsDeleted = true;
            file.IsValid = 0;
            file.Status = "archived";
            file.DeleteBy = "system";
            file.DeleteTime = DateTime.Now;
            await _db.UpdateAsync(file,
                nameof(StandardDirectoryFile.IsDeleted), nameof(StandardDirectoryFile.IsValid),
                nameof(StandardDirectoryFile.Status), nameof(StandardDirectoryFile.DeleteBy),
                nameof(StandardDirectoryFile.DeleteTime));
            await DeleteFileFromStorageAsync(file);
            filesDeleted++;
        }

        // 软删除文件夹（P1-7：走 IsDeleted，不再只置 IsValid）
        var folderEntity = (await _db.GetOneAsync<StandardDirectoryFolder>(
            x => x.Code == folderCode)).Data;
        if (folderEntity != null)
        {
            folderEntity.IsDeleted = true;
            folderEntity.IsValid = 0;
            folderEntity.Status = "archived";
            folderEntity.DeleteBy = "system";
            folderEntity.DeleteTime = DateTime.Now;
            await _db.UpdateAsync(folderEntity,
                nameof(StandardDirectoryFolder.IsDeleted), nameof(StandardDirectoryFolder.IsValid),
                nameof(StandardDirectoryFolder.Status), nameof(StandardDirectoryFolder.DeleteBy),
                nameof(StandardDirectoryFolder.DeleteTime));
            foldersDeleted++;
        }

        return (foldersDeleted, filesDeleted);
    }

    /// <summary>
    /// 同层最大序号：按 <c>(ConfigCode, ParentCode)</c> 取 <c>MAX(SortOrder)</c>。
    /// <para>★ P1-21 修复：原实现按 <c>Depth</c> 过滤 + <c>Split('|')</c> 反解复合码 ——
    /// 复合码已删，且同层不同父共享序号空间。决策 ⑦ 下序号仅作展示排序，不再参与编码。</para>
    /// </summary>
    private async Task<int> GetMaxSortOrderAsync(string configCode, string? parentCode)
    {
        parentCode ??= string.Empty;
        var folders = (await _db.GetListAsync<StandardDirectoryFolder>(
            x => x.ConfigCode == configCode && x.ParentCode == parentCode,
            includeDisabled: true)).Data ?? new();
        return folders.Count == 0 ? 0 : folders.Max(x => x.SortOrder);
    }

    /// <summary>
    /// 校验文件夹名 / 文件名（★ P0-2：路径穿越防御的第一道闸）。
    /// <para>禁 <c>/</c>、<c>\</c>、<c>..</c>、控制字符与保留段名（pdf / markdown / _archive）。</para>
    /// </summary>
    private static string? ValidateFolderOrFileName(string name, string what)
    {
        if (string.IsNullOrWhiteSpace(name)) return $"{what}不能为空";
        if (name.Contains('/') || name.Contains('\\'))
            return $"{what}不得包含路径分隔符：{name}";
        if (name.Contains(".."))
            return $"{what}不得包含「..」：{name}";
        if (name.Any(c => char.IsControl(c)))
            return $"{what}包含非法控制字符";
        var trimmed = name.Trim();
        if (PathBuilder.ReservedSegments.Any(s =>
                string.Equals(s, trimmed, StringComparison.OrdinalIgnoreCase)))
            return $"{what}不得使用保留段名「{trimmed}」";
        return null;
    }

    #endregion

    #region 文件 CRUD

    /// <summary>
    /// 获取文件列表
    /// </summary>
    public async Task<List<StandardDirectoryFile>> GetFilesAsync(string? folderCode = null)
    {
        if (string.IsNullOrEmpty(folderCode))
        {
            return (await _db.GetListAsync<StandardDirectoryFile>(
                x => x.IsValid == 1)).Data ?? new();
        }
        return (await _db.GetListAsync<StandardDirectoryFile>(
            x => x.FolderCode == folderCode && x.IsValid == 1)).Data ?? new();
    }

    /// <summary>
    /// 获取目录根级别的文件（根 = <c>FolderCode == ""</c>，决策 ⑨）。
    /// <para>★ P1-12 修复：改用专用视图 <see cref="StandardDirectoryRootFileView"/> ——
    /// 原实现借 <c>v_upload_task_detail</c> 投影，只能捞到「有上传任务」的文件，
    /// 手动创建 / 遗留的根级文件全部不可见。</para>
    /// </summary>
    public async Task<List<StandardDirectoryFile>> GetRootFilesAsync(string directoryCode)
    {
        var viewResult = await _db.GetListAsync<StandardDirectoryRootFileView>(
            x => x.ConfigCode == directoryCode && !x.IsDeleted);
        return viewResult.Data?.Select(x => new StandardDirectoryFile
        {
            Code = x.Code,
            FileName = x.FileName,
            FileType = x.FileType,
            ConfigCode = x.ConfigCode,
            FolderCode = x.FolderCode ?? "",
            UploadStatus = x.UploadStatus,
            StoragePath = x.StoragePath,
            ConvertedStoragePath = x.ConvertedStoragePath,
            ConvertStatus = x.ConvertStatus,
            ConvertMessage = x.ConvertMessage,
            PreviewPdfPath = x.PreviewPdfPath,
            MarkdownPath = x.MarkdownPath,
            MarkdownStatus = x.MarkdownStatus,
            MarkdownMessage = x.MarkdownMessage,
            FileSize = x.FileSize,
            TaskId = x.TaskId,
            IsValid = x.IsValid
        }).ToList() ?? new();
    }

    /// <summary>
    /// 更新文件信息。
    /// <para>★ P0-5 修复：按路由 <c>fileCode</c> 定位 existing 并白名单拷贝，
    /// 不再把客户端实体直接交给单参 <c>UpdateAsync</c>。</para>
    /// </summary>
    public async Task<(bool ok, string? error)> UpdateFileAsync(string fileCode, StandardDirectoryFile input)
    {
        var existing = (await _db.GetOneAsync<StandardDirectoryFile>(
            x => x.Code == fileCode && x.IsValid == 1)).Data;
        if (existing == null) return (false, "文件不存在");

        var nameErr = ValidateFolderOrFileName(input.FileName ?? "", "文件名");
        if (nameErr != null) return (false, nameErr);

        existing.FileName = input.FileName;
        existing.Description = input.Description;
        existing.Sort = input.Sort;
        existing.ExtractionEnabled = input.ExtractionEnabled;
        existing.UpdateTime = DateTime.Now;
        await _db.UpdateAsync(existing,
            nameof(StandardDirectoryFile.FileName),
            nameof(StandardDirectoryFile.Description),
            nameof(StandardDirectoryFile.Sort),
            nameof(StandardDirectoryFile.ExtractionEnabled),
            nameof(StandardDirectoryFile.UpdateTime));
        return (true, null);
    }

    /// <summary>
    /// 删除文件（MinIO + DB 软删除）
    /// </summary>
    public async Task<(bool ok, string? error)> DeleteFileAsync(string fileCode)
    {
        var lockErr = await GetFileLockErrorAsync(fileCode);
        if (lockErr != null) return (false, lockErr);

        var file = (await _db.GetOneAsync<StandardDirectoryFile>(
            x => x.Code == fileCode && x.IsValid == 1)).Data;
        if (file == null) return (false, "文件不存在");

        // 先改 DB 状态，再删对象 —— P1-15（顺序反了 MinIO 失败即不可逆）
        file.IsDeleted = true;
        file.IsValid = 0;
        file.Status = "archived";
        file.DeleteBy = "system";
        file.DeleteTime = DateTime.Now;
        await _db.UpdateAsync(file,
            nameof(StandardDirectoryFile.IsDeleted), nameof(StandardDirectoryFile.IsValid),
            nameof(StandardDirectoryFile.Status), nameof(StandardDirectoryFile.DeleteBy),
            nameof(StandardDirectoryFile.DeleteTime));
        await DeleteFileFromStorageAsync(file);
        return (true, null);
    }

    /// <summary>
    /// 下载文件
    /// </summary>
    public async Task<(Stream stream, string contentType, string fileName)?> DownloadFileAsync(string storagePath)
    {
        try
        {
            var objectName = storagePath.TrimStart('/');
            var (stream, contentType) = await _storage.DownloadAsync(objectName);
            var fileName = Path.GetFileName(objectName.Replace('\\', '/'));
            return (stream, contentType ?? "application/octet-stream", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "下载文件失败: {StoragePath}", storagePath);
            return null;
        }
    }

    /// <summary>
    /// 删除文件在 MinIO 的全部对象（★ 2026-09-26 由 2 条路径扩到 4 条）
    ///
    /// <para>必须删除：① 原始文件 ② 预览 PDF 产物 ③ Markdown 产物 ④ 遗留中间产物。</para>
    /// <para>⚠️ 原实现只删 ① ④ → 删除/替换后 ② ③ 变**孤儿对象**。更危险的是：
    /// 若之后重新上传同名文件，提取链可能读到**已删除文件的旧产物** → 提取到不存在的内容，
    /// 且零报错。</para>
    /// </summary>
    private async Task DeleteFileFromStorageAsync(StandardDirectoryFile file)
    {
        await TryDeleteObjectAsync(file.StoragePath, "原始文件");
        await TryDeleteObjectAsync(file.PreviewPdfPath, "预览PDF产物");
        await TryDeleteObjectAsync(file.MarkdownPath, "Markdown产物");
        await TryDeleteObjectAsync(file.ConvertedStoragePath, "遗留中间产物");
    }

    /// <summary>删除单个 MinIO 对象（空路径跳过；失败只告警不中断）</summary>
    private async Task TryDeleteObjectAsync(string? path, string label)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        // 透传场景下 PreviewPdfPath == StoragePath，避免重复删除同一对象
        try { await _storage.DeleteAsync(path.TrimStart('/')); }
        catch (Exception ex) { _logger.LogWarning(ex, "MinIO 删除{Label}失败: {Path}", label, path); }
    }

    #endregion

    #region 转换入队（★ 2026-09-26 双队列：PDF 预览 + Markdown 提取）

    /// <summary>
    /// 该文件是否需要进入转换队列。
    ///
    /// <para>★ 判据放宽：原实现为 <c>FileType == "doc" || FileType == "xls"</c>
    /// → <b>docx / xlsx / ppt / pptx / pdf 从不入队</b>，这正是「上传后不转换、
    /// 预览时才动态转」的根因之一。</para>
    ///
    /// <para>现在只排除两类：① 无存储路径（未真正上传）；② 被忽略的系统文件。
    /// 「哪种文件走哪条链、要不要透传」的细分交给**执行器**（判据集中一处，避免两处规则漂移）。</para>
    /// </summary>
    private static bool NeedsConversion(StandardDirectoryFile f)
    {
        if (string.IsNullOrWhiteSpace(f.StoragePath)) return false;
        if (IgnoredFileExtensions.Contains(Path.GetExtension(f.FileName ?? ""))) return false;
        return true;
    }

    /// <summary>
    /// 构造**转换队列**任务载荷：① <c>office2pdf</c>（预览）② <c>anydoc2md</c>（提取）
    /// ③ <c>office2editable</c>（★ 归一，仅旧二进制格式）。
    ///
    /// <para>为什么拆成独立任务而不是一个多产物任务（用户第 1、2 点）：</para>
    /// <list type="bullet">
    ///   <item>失败语义隔离：扫描件 PDF 能出 PDF 预览、但转不了 Markdown —— 合成一个任务时
    ///         整体判失败，用户会以为「预览也没好」。</item>
    ///   <item>可独立重试：Markdown 失败可只重试提取链，不必重跑 LibreOffice。</item>
    ///   <item>状态列各自独立：<c>ConvertStatus</c> / <c>MarkdownStatus</c> / <c>EditableStatus</c>
    ///         互不干扰（三条链各有列白名单，见 <c>OfficeConvertService</c>）。</item>
    /// </list>
    ///
    /// <para>★ 归一链的**条件性**：只有 <c>.doc</c>/<c>.xls</c>/<c>.ppt</c> 才投 ——
    /// 判据收口在 <see cref="OfficeConvertService.EditableTargetFormat"/>，
    /// ⛔ 不在此另写一份扩展名判断（两处规则漂移的后果是「零报错地不一致」）。</para>
    ///
    /// <para>⚠️ 不在入队点算 TargetPath —— 产物路径由执行器用
    /// <see cref="CodeGeneratorService.BuildProductPath"/> 从源路径派生。
    /// 原实现在此调用 <c>GenerateConvertedStoragePath("","","","",name)</c> 空参，
    /// 导致产物全部落到 <c>/standard-directory/.converted/{文件名}</c> 而互相覆盖（实测已丢数据）。</para>
    /// </summary>
    private static List<FileConvertPayload> BuildConvertPayloads(StandardDirectoryFile f)
    {
        var payloads = new List<FileConvertPayload>
        {
            new FileConvertPayload
            {
                Code = f.Code ?? "", FileName = f.FileName,
                SourcePath = f.StoragePath, ConvertType = "office2pdf"
            },
            new FileConvertPayload
            {
                Code = f.Code ?? "", FileName = f.FileName,
                SourcePath = f.StoragePath, ConvertType = "anydoc2md"
            }
        };

        // ★ 归一链（S-1，2026-10-03）：旧二进制格式 → OOXML，供 NPOI 填写引擎读
        if (OfficeConvertService.EditableTargetFormat(f.FileName) != null)
        {
            payloads.Add(new FileConvertPayload
            {
                Code = f.Code ?? "", FileName = f.FileName,
                SourcePath = f.StoragePath, ConvertType = "office2editable"
            });
        }

        return payloads;
    }

    /// <summary>把若干文件的双队列载荷摊平成任务列表</summary>
    private static List<QueueManager.TaskItem> BuildConvertTasks(
        IEnumerable<StandardDirectoryFile> files, string taskId)
        => files.Where(NeedsConversion)
                .SelectMany(BuildConvertPayloads)
                .Select(p => new QueueManager.TaskItem
                {
                    TaskType = "file_convert",
                    Payload = JsonSerializer.Serialize(p, PayloadJsonOptions),
                    TaskId = taskId
                })
                .ToList();

    /// <summary>
    /// 替换文件时作废旧产物：删对象 + 清字段。
    ///
    /// <para>⚠️ 不能删源文件本身：PDF / 图片走「透传」时 <c>PreviewPdfPath == StoragePath</c>。</para>
    /// <para>不清理的后果：替换后提取链若命中旧 <c>MarkdownPath</c>，会读到**替换前的内容**，
    /// 且状态仍显示 completed → 静默错误。</para>
    /// </summary>
    private async Task InvalidateProductsAsync(StandardDirectoryFile file)
    {
        var sameAsSource = string.Equals(file.PreviewPdfPath, file.StoragePath, StringComparison.Ordinal);
        if (!string.IsNullOrEmpty(file.PreviewPdfPath) && !sameAsSource)
            await TryDeleteObjectAsync(file.PreviewPdfPath, "旧预览PDF产物");
        await TryDeleteObjectAsync(file.MarkdownPath, "旧Markdown产物");
        await TryDeleteObjectAsync(file.ConvertedStoragePath, "旧中间产物");
        // ★ 归一产物（S-1）：不作废则填写引擎会读**替换前的内容**且状态仍是 completed ⇒ 静默错误
        //   （与上面 Markdown 注释同构）。⚠️ 不能与源路径相同：归一产物恒在 editable/ 段下，
        //   与 StoragePath 必然不同，故无需 sameAsSource 判定。
        await TryDeleteObjectAsync(file.EditableStoragePath, "旧归一产物");

        file.PreviewPdfPath = null;
        file.MarkdownPath = null;
        file.MarkdownStatus = "pending";
        file.MarkdownMessage = null;
        file.ConvertedStoragePath = null;
        file.ConvertStatus = "pending";
        file.ConvertMessage = null;
        file.EditableStoragePath = null;
        // ⚠️ 置 null（=「不需要归一」）而非 "pending"：替换后的新文件扩展名可能已变，
        //    是否需要归一只由入队点按扩展名判定，此处不预设。
        file.EditableStatus = null;
        file.EditableMessage = null;
    }

    /// <summary>
    /// 清空该文件的「文档正文」缓存（<c>cert_doc_extraction_rule.DocContent</c>）。
    ///
    /// <para>★ 用户裁定（2026-09-26）：「替换了文件，原有的规则、保存的信息肯定要变化，
    /// 这个不要紧，我们重新分析并提取即可」→ 文件替换时清缓存，下次分析自动重取。</para>
    ///
    /// <para>⚠️ **只清正文缓存，不动规则本身**（<c>Skill</c>/<c>Prompt</c> + 字段/表格定义）：
    /// 字段表是人工一条条配出来的**昂贵资产**；<c>DocContent</c> 只是"当时那份文档的正文快照"，
    /// 是廉价缓存。连规则一起清 = 每次替换文件都要重新手工配一遍字段表。</para>
    /// </summary>
    private async Task ClearDocContentCacheAsync(string fileCode)
    {
        try
        {
            var rule = (await _db.GetOneAsync<DocExtractionRule>(
                x => x.StandardFileCode == fileCode)).Data;
            if (rule == null || string.IsNullOrEmpty(rule.DocContent)) return;

            rule.DocContent = null;
            rule.UpdateTime = DateTime.Now;
            await _db.UpdateAsync(rule,
                nameof(DocExtractionRule.DocContent), nameof(DocExtractionRule.UpdateTime));
            _logger.LogInformation("[ReplaceFile] 已清空文档正文缓存: {FileCode}", fileCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ReplaceFile] 清空文档正文缓存失败: {FileCode}", fileCode);
        }
    }

    #endregion

    #region 上传 4 步

    /// <summary>
    /// Step 1: 上传预初始化 — 生成编码、创建预记录、返回增强清单
    /// </summary>
    public async Task<(bool ok, string? error, UploadManifestResponse? response)> UploadInitAsync(
        UploadManifestRequest manifest)
    {
        // 1. 定位 StandardDirectoryConfig —— 决策 ⑦ 已删复合码 SDC-…，
        //    manifest.DirectoryCode 即 config.Code（GUID）。兼容过渡：Code 查不到时按三键回退；
        //    ★ 决策⑳修订后主键含 OrgCode —— 缺机构的回退会跨机构误命中（拿到别的机构的目录），禁用。
        var config = (await _db.GetOneAsync<StandardDirectoryConfig>(
            x => x.Code == manifest.DirectoryCode)).Data;
        if (config == null && !string.IsNullOrEmpty(manifest.OrgCode)
            && !string.IsNullOrEmpty(manifest.StandardCode) && !string.IsNullOrEmpty(manifest.PhaseCode))
        {
            config = (await _db.GetOneAsync<StandardDirectoryConfig>(
                x => x.OrgCode == manifest.OrgCode
                  && x.StandardCode == manifest.StandardCode && x.StageCode == manifest.PhaseCode)).Data;
        }
        if (config == null)
            return (false, "目录配置不存在（请从目录树选择标准与阶段后重试）", null);

        var configCode = config.Code ?? manifest.DirectoryCode;
        manifest.DirectoryCode = configCode;
        manifest.StandardCode = config.StandardCode;
        manifest.PhaseCode = config.StageCode;

        // 2. 队列互斥检查
        var queueLockErr = await GetQueueLockErrorAsync(configCode);
        if (queueLockErr != null) return (false, queueLockErr, null);

        // 3. 生成 TaskId
        var taskId = Guid.NewGuid().ToString("N");

        // 5. 处理文件夹（按深度排序，复用或创建）
        var sortedFolders = manifest.Folders
            .OrderBy(f => f.Path.Count(c => c == '/'))
            .ThenBy(f => f.Path).ToList();
        var enhancedFolders = new List<EnhancedFolderItem>();
        var folderMap = new Dictionary<string, string>(); // FullPath → FolderCode

        int depthCounter = 1;
        int seqCounter = 1;
        foreach (var folder in sortedFolders)
        {
            var depth = folder.Path.Split('/').Length;
            var folderErr = ValidateFolderOrFileName(folder.Path.Split('/').Last(), "文件夹名");
            if (folderErr != null) return (false, folderErr, null);

            var existing = (await _db.GetOneAsync<StandardDirectoryFolder>(
                x => x.ConfigCode == configCode
                    && x.FullPath == folder.Path && x.IsValid == 1)).Data;

            if (existing != null)
            {
                enhancedFolders.Add(new EnhancedFolderItem
                {
                    FolderCode = existing.Code ?? "",
                    FolderName = existing.FolderName ?? "",
                    ParentCode = existing.ParentCode ?? "",
                    Depth = existing.Depth,
                    FullPath = existing.FullPath ?? folder.Path,
                    Mode = "reuse"
                });
                folderMap[folder.Path] = existing.Code ?? "";
            }
            else
            {
                var parts = folder.Path.Split('/');
                var folderName = parts.Last();
                var parentPath = parts.Length > 1 ? string.Join("/", parts.Take(parts.Length - 1)) : null;
                var parentCode = parentPath != null && folderMap.ContainsKey(parentPath)
                    ? folderMap[parentPath] : "";

                // ★⑬/§11.1 + 静默丢行：uk_cfg_fullpath 不含 IsDeleted。同路径若存在**任何状态**的
                //   行（已删 / 上次任务残留的 IsValid=0），直接 InsertAsync 会撞 1062 被 InsertAsync
                //   吞成 Result.Fail —— 而这里原先是不判结果的 → 行不落库、确认阶段才炸。
                var reuseFolder = await _db.Client.Queryable<StandardDirectoryFolder>()
                    .Where(x => x.ConfigCode == configCode && x.FullPath == folder.Path)
                    .FirstAsync();

                StandardDirectoryFolder folderRow;
                if (reuseFolder != null)
                {
                    reuseFolder.IsDeleted = false;
                    reuseFolder.DeleteBy = null;
                    reuseFolder.DeleteTime = null;
                    reuseFolder.ParentCode = parentCode ?? string.Empty;
                    reuseFolder.IsValid = 0;
                    reuseFolder.TaskId = taskId;
                    reuseFolder.Status = "draft";
                    reuseFolder.UpdateTime = DateTime.Now;
                    await _db.UpdateAsync(reuseFolder,
                        nameof(StandardDirectoryFolder.IsDeleted), nameof(StandardDirectoryFolder.DeleteBy),
                        nameof(StandardDirectoryFolder.DeleteTime), nameof(StandardDirectoryFolder.ParentCode),
                        nameof(StandardDirectoryFolder.IsValid), nameof(StandardDirectoryFolder.TaskId),
                        nameof(StandardDirectoryFolder.Status), nameof(StandardDirectoryFolder.UpdateTime));
                    folderRow = reuseFolder;
                }
                else
                {
                    folderRow = new StandardDirectoryFolder
                    {
                        Code = Guid.NewGuid().ToString("N"),
                        ConfigCode = configCode,
                        ParentCode = parentCode ?? string.Empty, // 决策 ⑨：根 ParentCode 用 ''
                        FolderName = folderName,
                        Depth = depth,
                        SortOrder = seqCounter++,
                        IsValid = 0,
                        TaskId = taskId,
                        FullPath = folder.Path,
                        Status = "draft",
                        CreateTime = DateTime.Now
                    };
                    var ins = await _db.InsertAsync(folderRow);
                    if (ins.Data == null)
                        return (false, $"创建文件夹记录失败：{(string.IsNullOrWhiteSpace(ins.Error) ? "唯一键冲突" : ins.Error)}", null);
                }

                var newFolderCode = folderRow.Code ?? "";
                enhancedFolders.Add(new EnhancedFolderItem
                {
                    FolderCode = newFolderCode,
                    FolderName = folderName,
                    ParentCode = parentCode ?? "",
                    Depth = depth,
                    FullPath = folder.Path,
                    Mode = "create"
                });
                folderMap[folder.Path] = newFolderCode;
            }
            depthCounter = Math.Max(depthCounter, depth);
        }

        // 6. 处理文件（创建或替换）
        var enhancedFiles = new List<EnhancedFileItem>();
        long totalSize = 0;

        for (int i = 0; i < manifest.Files.Count; i++)
        {
            var fileItem = manifest.Files[i];
            var fullPath = fileItem.RelativePath;
            var fileName = fileItem.FileName;

            // 服务器端白名单过滤
            var ext = Path.GetExtension(fileName);
            if (IgnoredFileExtensions.Contains(ext)) continue;

            // P0-2：文件名校验（防 ../ 路径穿越 + 保留段撞车）
            var nameErr = ValidateFolderOrFileName(fileName, "文件名");
            if (nameErr != null) return (false, nameErr, null);

            // 解析父文件夹
            var parentDir = Path.GetDirectoryName(fullPath)?.Replace('\\', '/');
            var folderCode = parentDir != null && folderMap.ContainsKey(parentDir)
                ? folderMap[parentDir] : "";

            // 查找已有文件
            var existingFile = (await _db.GetOneAsync<StandardDirectoryFile>(
                x => x.ConfigCode == configCode
                    && x.FullPath == fullPath && x.IsValid == 1)).Data;

            // ★ 路径唯一权威：PathBuilder（决策⑳修订 —— 身份段 = OrgCode + StandardCode + StageCode 原文）
            var storagePath = PathBuilder.StandardFile(
                config.OrgCode, config.StandardCode, config.StageCode, parentDir, fileName);

            if (existingFile != null)
            {
                // 替换模式
                var oldStoragePath = existingFile.StoragePath;
                existingFile.UploadStatus = "replacing";
                existingFile.TaskId = taskId;
                existingFile.StoragePath = storagePath;
                existingFile.StandardCode = config.StandardCode;
                existingFile.StageCode = config.StageCode;
                existingFile.Remark = $"[upload-replace:{taskId}]";
                await _db.UpdateAsync(existingFile);

                enhancedFiles.Add(new EnhancedFileItem
                {
                    Index = i,
                    FileCode = existingFile.Code ?? "",
                    FileName = fileName,
                    RelativePath = fullPath,
                    FullPath = fullPath,
                    FileSize = fileItem.FileSize,
                    MimeType = fileItem.MimeType,
                    StoragePath = storagePath,
                    ParentFolderCode = folderCode,
                    Mode = "replace",
                    ExistingFileCode = existingFile.Code ?? "",
                    ExistingFileId = existingFile.Id,
                    OldStoragePath = oldStoragePath,
                    Status = "pending"
                });
            }
            else
            {
                // 创建模式（决策 ⑦：复合 FileCode 已删，业务键即 Code）
                // ★⑬/§11.1 + 静默丢行：同 FullPath 存在**任何状态**的行（已删 / 残留 pending）
                //   必须复用 —— 直接 InsertAsync 撞 uk_cfg_fullpath → Result.Fail 被吞 → 行不落库。
                var reuseFile = await _db.Client.Queryable<StandardDirectoryFile>()
                    .Where(x => x.ConfigCode == configCode && x.FullPath == fullPath)
                    .FirstAsync();

                StandardDirectoryFile newFile;
                if (reuseFile != null)
                {
                    // 复活并复用 Code（提取规则 StandardFileCode 引用的就是它）；产物链按新内容清空
                    reuseFile.IsDeleted = false;
                    reuseFile.DeleteBy = null;
                    reuseFile.DeleteTime = null;
                    reuseFile.FileName = fileName;
                    reuseFile.FolderCode = folderCode;
                    reuseFile.StandardCode = config.StandardCode;
                    reuseFile.StageCode = config.StageCode;
                    reuseFile.FileType = ext?.TrimStart('.') ?? reuseFile.FileType;
                    reuseFile.StoragePath = storagePath;
                    reuseFile.IsValid = 0;
                    reuseFile.UploadStatus = "pending";
                    reuseFile.TaskId = taskId;
                    reuseFile.Status = "draft";
                    reuseFile.Remark = null;
                    reuseFile.ConvertedStoragePath = null;
                    reuseFile.ConvertStatus = "none";
                    reuseFile.ConvertMessage = null;
                    reuseFile.ConvertDate = null;
                    reuseFile.PreviewPdfPath = null;
                    reuseFile.MarkdownPath = null;
                    reuseFile.MarkdownStatus = "none";
                    reuseFile.MarkdownMessage = null;
                    reuseFile.MarkdownDate = null;
                    reuseFile.UpdateTime = DateTime.Now;
                    var reuseUpd = await _db.UpdateAsync(reuseFile,
                        nameof(StandardDirectoryFile.IsDeleted), nameof(StandardDirectoryFile.DeleteBy),
                        nameof(StandardDirectoryFile.DeleteTime), nameof(StandardDirectoryFile.FileName),
                        nameof(StandardDirectoryFile.FolderCode), nameof(StandardDirectoryFile.StandardCode),
                        nameof(StandardDirectoryFile.StageCode), nameof(StandardDirectoryFile.FileType),
                        nameof(StandardDirectoryFile.StoragePath), nameof(StandardDirectoryFile.IsValid),
                        nameof(StandardDirectoryFile.UploadStatus), nameof(StandardDirectoryFile.TaskId),
                        nameof(StandardDirectoryFile.Status), nameof(StandardDirectoryFile.Remark),
                        nameof(StandardDirectoryFile.ConvertedStoragePath), nameof(StandardDirectoryFile.ConvertStatus),
                        nameof(StandardDirectoryFile.ConvertMessage), nameof(StandardDirectoryFile.ConvertDate),
                        nameof(StandardDirectoryFile.PreviewPdfPath), nameof(StandardDirectoryFile.MarkdownPath),
                        nameof(StandardDirectoryFile.MarkdownStatus), nameof(StandardDirectoryFile.MarkdownMessage),
                        nameof(StandardDirectoryFile.MarkdownDate), nameof(StandardDirectoryFile.UpdateTime));
                    if (!reuseUpd.Success)
                        return (false, $"复活同名文件记录失败：{reuseUpd.Error}", null);
                    newFile = reuseFile;
                }
                else
                {
                    newFile = new StandardDirectoryFile
                    {
                        Code = Guid.NewGuid().ToString("N"),
                        FolderCode = folderCode,
                        ConfigCode = configCode,
                        StandardCode = config.StandardCode,
                        StageCode = config.StageCode,
                        FileName = fileName,
                        FileType = ext?.TrimStart('.'),
                        StoragePath = storagePath,
                        FullPath = fullPath,
                        IsValid = 0,
                        UploadStatus = "pending",
                        TaskId = taskId,
                        Status = "draft",
                        CreateTime = DateTime.Now
                    };
                    var ins = await _db.InsertAsync(newFile);
                    if (ins.Data == null)
                        return (false, $"创建文件记录失败：{(string.IsNullOrWhiteSpace(ins.Error) ? "唯一键冲突" : ins.Error)}", null);
                }

                enhancedFiles.Add(new EnhancedFileItem
                {
                    Index = i,
                    FileCode = newFile.Code ?? "",
                    FileName = fileName,
                    RelativePath = fullPath,
                    FullPath = fullPath,
                    FileSize = fileItem.FileSize,
                    MimeType = fileItem.MimeType,
                    StoragePath = storagePath,
                    ParentFolderCode = folderCode,
                    Mode = "create",
                    Status = "pending"
                });
            }
            totalSize += fileItem.FileSize;
        }

        if (enhancedFiles.Count == 0)
            return (false, "没有有效的文件需要上传", null);

        // 7. 创建上传任务记录
        var uploadTask = new UploadTask
        {
            Code = taskId,
            TaskId = taskId,
            ConfigCode = configCode,
            TotalFiles = enhancedFiles.Count,
            TotalSize = totalSize,
            Status = "initialized",
            CreateTime = DateTime.Now,
            ExpireTime = DateTime.Now.AddMinutes(30)
        };
        await _db.InsertAsync(uploadTask);

        return (true, null, new UploadManifestResponse
        {
            Status = "initialized",
            TaskId = taskId,
            DirectoryCode = configCode,
            TotalFiles = enhancedFiles.Count,
            TotalSize = totalSize,
            Folders = enhancedFolders,
            Files = enhancedFiles
        });
    }    /// <summary>
    /// Step 2: 逐文件上传到 MinIO
    /// </summary>
    public async Task<(bool ok, string? error)> UploadFileAsync(
        Stream fileStream, long fileSize, string fileCode, string taskId)
    {
        // 任务校验：上传任务表本身无 IsValid 中间态问题，可安全使用 GetOneAsync
        var task = (await _db.GetOneAsync<UploadTask>(
            x => x.TaskId == taskId && x.Status == "initialized")).Data;
        if (task == null) return (false, "上传任务不存在或已过期");

        // 文件校验：pending/replacing 文件 IsValid=0，必须用 GetOneIgnoreValidAsync 才能读到
        //（历史缺陷：GetOneAsync 强制 IsValid=1，永远查不到，导致状态机卡死）
        var file = (await _db.GetOneIgnoreValidAsync<StandardDirectoryFile>(
            x => x.Code == fileCode && x.TaskId == taskId)).Data;
        if (file == null) return (false, "文件编码与任务不匹配");


        var isReplaceMode = file.UploadStatus == "replacing";
        if (!isReplaceMode && (file.IsValid == 1 || file.UploadStatus != "pending"))
            return (false, "文件状态异常");
        if (isReplaceMode && file.UploadStatus != "replacing")
            return (false, "文件状态异常");

        // 上传到 MinIO（使用 DB 中的 StoragePath）
        var objectName = (file.StoragePath ?? "").TrimStart('/');
        if (string.IsNullOrEmpty(objectName))
            return (false, "存储路径未生成");

        try
        {
            var contentType = "application/octet-stream";
            await _storage.UploadAsync(objectName, fileStream, fileSize, contentType);

            // 替换模式：如果路径变了，删除旧对象
            if (isReplaceMode && !string.IsNullOrEmpty(file.Remark))
            {
                var marker = $"[upload-replace:{taskId}]";
                if ((file.Remark ?? "").Contains(marker))
                {
                    // 从 remark 中提取旧路径（简化处理：不做旧路径删除，由 confirm 统一处理）
                }
            }

            // 更新文件状态：复用上面 IgnoreValid 查到的实体（含 Code 主键），按字段精准更新
            file.UploadStatus = "uploaded";
            file.FileSize = fileSize;
            await _db.UpdateAsync(file, nameof(StandardDirectoryFile.UploadStatus), nameof(StandardDirectoryFile.FileSize));

            // 更新任务计数
            var taskToUpdate = (await _db.GetOneAsync<UploadTask>(
                x => x.TaskId == taskId)).Data;
            if (taskToUpdate != null)
            {
                taskToUpdate.SuccessCount++;
                await _db.UpdateAsync(taskToUpdate);
            }

            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "上传文件到 MinIO 失败: {FileCode}", fileCode);
            return (false, $"上传失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 单文件替换（一步完成，不经过 UploadTask 状态机）：
    /// 校验锁 → 覆盖上传 MinIO（沿用原 StoragePath）→ 回填 FileSize → doc/xls 置入转换队列。
    /// </summary>
    public async Task<(bool ok, string? error, string? convertQueueCode)> ReplaceFileAsync(
        string fileCode, Stream fileStream, long fileSize)
    {
        // 锁检查（队列运行中/上传中禁止替换）
        var lockErr = await GetFileLockErrorAsync(fileCode);
        if (lockErr != null) return (false, lockErr, null);

        var file = (await _db.GetOneAsync<StandardDirectoryFile>(
            x => x.Code == fileCode && x.IsValid == 1)).Data;
        if (file == null) return (false, "文件不存在", null);
        if (string.IsNullOrEmpty(file.StoragePath))
            return (false, "原文件从未上传过物理内容，请删除后重新上传", null);

        var objectName = file.StoragePath.TrimStart('/');
        try
        {
            // 1. 覆盖上传（沿用原路径，预览/提取的路径引用不变）
            await _storage.UploadAsync(objectName, fileStream, fileSize, "application/octet-stream");

            // 2. 回填大小与时间
            file.FileSize = fileSize;
            file.UploadStatus = "active";
            await _db.UpdateAsync(file, nameof(StandardDirectoryFile.FileSize), nameof(StandardDirectoryFile.UploadStatus));

            // 3. 重新进入转换队列（内容已变 → 旧产物作废、正文缓存作废）
            //    ★ 2026-09-26：① 判据放宽（原只认 doc/xls → docx/xlsx/ppt/pptx/pdf 从不入队）
            //                  ② 单任务 doc2docx → 双任务 office2pdf + anydoc2md
            //                  ③ 作废范围从「仅 ConvertedStoragePath」扩到全部 3 个产物 + DocContent 缓存
            string? queueCode = null;
            if (NeedsConversion(file))
            {
                await InvalidateProductsAsync(file);
                await ClearDocContentCacheAsync(file.Code ?? "");

                var req = new QueueManager.CreateQueueRequest
                {
                    QueueType = "file_convert",
                    QueueName = $"文件替换转换 - {file.FileName}",
                    ScopeKey = file.ConfigCode,
                    SourceType = "file_replace",
                    SourceId = file.Code,
                    ResourceLocks = new List<QueueManager.ResourceLockItem>
                    {
                        new() { ResourceTable = QueueManager.RESOURCE_DIR, ResourceCode = file.ConfigCode, ResourceName = file.ConfigCode },
                        new() { ResourceTable = QueueManager.RESOURCE_FILE, ResourceCode = file.Code, ResourceName = file.FileName, TaskNo = 1 }
                    },
                    Tasks = BuildConvertTasks(new[] { file }, file.ConfigCode)
                };
                var (qok, qerr, qcode, _) = await _queueManager.CreateQueueAsync(req);
                if (qok)
                {
                    queueCode = qcode;
                    // 与 confirm 流程同一约定：树可见（IsValid=1），等待转换完成回写 completed
                    file.UploadStatus = "uploaded";
                    file.ConvertStatus = "pending";
                    file.MarkdownStatus = "pending";
                    await _db.UpdateAsync(file,
                        nameof(StandardDirectoryFile.UploadStatus),
                        nameof(StandardDirectoryFile.ConvertStatus),
                        nameof(StandardDirectoryFile.MarkdownStatus),
                        nameof(StandardDirectoryFile.PreviewPdfPath),
                        nameof(StandardDirectoryFile.MarkdownPath),
                        nameof(StandardDirectoryFile.ConvertedStoragePath));
                }
                else
                {
                    // ★ P1-10：禁止 (true, 非空 error, …) 形态 —— 调用方按 ok 分支，error 非空即语义矛盾。
                    //   替换已成功，队列失败只作告警（error 置 null，queueCode 为空表示未入队）。
                    _logger.LogWarning((Exception?)null, "[ReplaceFile] 替换成功但转换队列创建失败: {FileCode} {Reason}", fileCode, qerr);
                    return (true, null, null);
                }
            }

            return (true, null, queueCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ReplaceFile] 替换文件失败: {FileCode}", fileCode);
            return (false, $"替换失败：{ex.Message}", null);
        }
    }

    /// <summary>
    /// Step 3: 确认上传 — 激活文件、创建转换队列
    /// </summary>
    public async Task<(bool ok, string? error, string? convertQueueCode)> UploadConfirmAsync(string taskId)
    {
        var task = (await _db.GetOneAsync<UploadTask>(
            x => x.TaskId == taskId)).Data;
        if (task == null) return (false, "上传任务不存在", null);

        // 队列锁检查
        var queueLockErr = await GetQueueLockErrorAsync(task.ConfigCode ?? "");
        if (queueLockErr != null) return (false, queueLockErr, null);

        // 检查所有文件是否已上传（IgnoreValid：pending/replacing 文件 IsValid=0，GetListAsync 默认过滤会漏掉）
        var allFiles = (await _db.GetListAsync<StandardDirectoryFile>(
            x => x.TaskId == taskId, includeDisabled: true)).Data ?? new();

        var pendingCount = allFiles.Count(x => x.UploadStatus == "pending");
        if (pendingCount > 0)
            return (false, $"还有 {pendingCount} 个文件未上传完成", null);

        // 分类：需要转换的文件
        // ★ 2026-09-26：判据由 `FileType == "doc" || "xls"` 放宽为 NeedsConversion()
        //   原判据让 docx/xlsx/ppt/pptx/pdf 从不入队 → 产物列填充率恒为 0
        var convertibleFiles = allFiles
            .Where(x => x.UploadStatus == "uploaded" && NeedsConversion(x))
            .ToList();

        // 全部文件激活：IsValid=0→1, UploadStatus→active, TaskId→null（普通文件）
        foreach (var file in allFiles.Where(x => x.UploadStatus == "uploaded"))
        {
            file.IsValid = 1;
            file.UploadStatus = "active";
            file.TaskId = null;
            await _db.UpdateAsync(file, nameof(StandardDirectoryFile.IsValid), nameof(StandardDirectoryFile.UploadStatus), nameof(StandardDirectoryFile.TaskId));
        }

        // 需要转换的文件额外设置双状态=pending（等待两条链各自回写）
        foreach (var file in convertibleFiles)
        {
            file.UploadStatus = "uploaded";
            file.ConvertStatus = "pending";
            file.MarkdownStatus = "pending";
            file.TaskId = null;
            await _db.UpdateAsync(file, nameof(StandardDirectoryFile.UploadStatus), nameof(StandardDirectoryFile.ConvertStatus), nameof(StandardDirectoryFile.MarkdownStatus), nameof(StandardDirectoryFile.TaskId));
        }

        // 激活文件夹：IsValid=0→1, 清除 TaskId
        // includeDisabled：草稿文件夹 IsValid=0，缺省过滤会永远查不到 → 激活空转 → 目录文件列表全空
        var folders = (await _db.GetListAsync<StandardDirectoryFolder>(
            x => x.TaskId == taskId, includeDisabled: true)).Data ?? new();
        foreach (var folder in folders)
        {
            folder.IsValid = 1;
            folder.TaskId = null;
            await _db.UpdateAsync(folder);
        }

        // 创建转换队列（如有可转换文件）
        string? convertQueueCode = null;
        if (convertibleFiles.Count > 0)
        {
            // ★ 双队列：每个文件产出 2 个任务（office2pdf + anydoc2md）
            var tasks = BuildConvertTasks(convertibleFiles, taskId);

            var req = new QueueManager.CreateQueueRequest
            {
                QueueType = "file_convert",
                QueueName = $"文档转换 - {convertibleFiles.Count}个文件",
                ScopeKey = task.ConfigCode,
                SourceType = "upload_task",
                SourceId = taskId,
                ResourceLocks = new List<QueueManager.ResourceLockItem>
                {
                    new() { ResourceTable = QueueManager.RESOURCE_DIR, ResourceCode = task.ConfigCode, ResourceName = task.ConfigCode }
                },
                Tasks = tasks
            };

            var (ok, error, queueCode, _) = await _queueManager.CreateQueueAsync(req);
            if (ok && queueCode != null)
            {
                convertQueueCode = queueCode;
            }
        }

        // 更新任务状态
        task.Status = "completed";
        task.UpdateTime = DateTime.Now;
        await _db.UpdateAsync(task);

        return (true, null, convertQueueCode);
    }

    /// <summary>
    /// Step 4: 回滚上传 — 删除预创建记录和 MinIO 对象
    /// </summary>
    public async Task<(bool ok, string? error, int deleted, int restored)> UploadCancelAsync(string taskId)
    {
        // 取消关联的转换队列（yzh_queue 无 IsValid 过滤，GetListAsync 正常）
        var activeQueues = (await _db.GetListAsync<YzhQueue>(
            x => x.SourceType == "upload_task" && x.SourceId == taskId
                && x.Status != "completed" && x.Status != "failed" && x.Status != "cancelled")).Data ?? new();
        foreach (var q in activeQueues)
        {
            await _queueManager.CancelQueueAsync(q.QueueCode);
        }

        // 使用视图查询关联文件（包含中间状态）
        var viewFiles = (await _db.GetListAsync<UploadTaskDetailView>(
            x => x.TaskId == taskId)).Data ?? new();
        var viewFileCodes = viewFiles.Where(v => !string.IsNullOrEmpty(v.FileCode)).Select(v => v.FileCode!).ToList();
        // 用 IgnoreValid 直查实体（上传中间态文件 IsValid=0；且需要 Code/Remark/ConvertedStoragePath 等完整字段）
        var files = new List<StandardDirectoryFile>();
        foreach (var fc in viewFileCodes)
        {
            var entity = (await _db.GetOneIgnoreValidAsync<StandardDirectoryFile>(x => x.Code == fc)).Data;
            if (entity != null) files.Add(entity);
        }
        int deletedCount = 0, restoredCount = 0;
        var replaceMarker = $"[upload-replace:{taskId}]";

        foreach (var file in files)
        {
            // 删除 MinIO 对象
            if (!string.IsNullOrEmpty(file.StoragePath))
            {
                try { await _storage.DeleteAsync(file.StoragePath.TrimStart('/')); }
                catch { /* 非阻塞 */ }
            }
            if (!string.IsNullOrEmpty(file.ConvertedStoragePath))
            {
                try { await _storage.DeleteAsync(file.ConvertedStoragePath.TrimStart('/')); }
                catch { /* 非阻塞 */ }
            }

            if ((file.Remark ?? "").Contains(replaceMarker))
            {
                // 替换模式：恢复为有效状态（直接更新实体）
                var updateFile = (await _db.GetOneAsync<StandardDirectoryFile>(
                    x => x.Code == file.Code)).Data;
                if (updateFile != null)
                {
                    updateFile.IsValid = 1;
                    updateFile.UploadStatus = "active";
                    updateFile.TaskId = null;
                    updateFile.ConvertStatus = null;
                    updateFile.ConvertedStoragePath = null;
                    updateFile.ConvertMessage = null;
                    updateFile.Remark = (updateFile.Remark ?? "").Replace(replaceMarker, "");
                    await _db.UpdateAsync(updateFile);
                    restoredCount++;
                }
            }
            else
            {
                // 创建模式：物理删除（IsValid=0 记录）
                await _db.Client.Deleteable<StandardDirectoryFile>()
                    .Where(x => x.Code == file.Code)
                    .ExecuteCommandAsync();
                deletedCount++;
            }
        }

        // 删除此任务创建的空文件夹
        var taskFolders = (await _db.Client.Queryable<StandardDirectoryFolder>()
            .Where(x => x.TaskId == taskId && !x.IsDeleted)
            .ToListAsync());
        foreach (var folder in taskFolders)
        {
            var fileCount = await _db.Client.Queryable<StandardDirectoryFile>()
                .Where(x => x.FolderCode == folder.Code && x.IsValid == 1 && !x.IsDeleted)
                .CountAsync();
            if (fileCount == 0)
                await _db.Client.Deleteable<StandardDirectoryFolder>()
                    .Where(x => x.Code == folder.Code)
                    .ExecuteCommandAsync();
        }

        // 删除上传任务记录
        await _db.Client.Deleteable<UploadTask>()
            .Where(x => x.TaskId == taskId)
            .ExecuteCommandAsync();

        return (true, null, deletedCount, restoredCount);
    }

    /// <summary>
    /// 查询上传状态
    /// </summary>
    public async Task<UploadStatusResponse?> GetUploadStatusAsync(string taskId)
    {
        // 使用视图查询任务详情
        var viewResult = await _db.GetOneAsync<UploadTaskDetailView>(
            x => x.TaskId == taskId);
        if (viewResult.Data == null) return null;

        var task = viewResult.Data;
        var files = (await _db.GetListAsync<UploadTaskDetailView>(
            x => x.TaskId == taskId)).Data ?? new();

        return new UploadStatusResponse
        {
            TaskId = taskId,
            Status = task.Status,
            TotalFiles = task.TotalFiles,
            SuccessCount = task.SuccessCount,
            FailCount = files.Count(x => x.UploadStatus == "failed"),
            Files = files.Select(f => new FileStatusItem
            {
                FileCode = f.FileCode,
                FileName = f.FileName,
                Status = f.UploadStatus
            }).ToList()
        };
    }

    #endregion

    #region 队列相关

    /// <summary>
    /// 获取活跃队列
    /// </summary>
    public async Task<YzhQueue?> GetActiveQueueAsync(string directoryCode)
    {
        return (await _queueManager.FindRunningQueueByScopeKeyAsync(directoryCode));
    }

    /// <summary>
    /// 获取转换进度
    /// </summary>
    public async Task<object> GetConvertProgressAsync(string taskId)
    {
        return await _queueManager.GetBatchProgressAsync(taskId);
    }

    /// <summary>
    /// 取消转换
    /// </summary>
    public async Task<(bool ok, string? error)> CancelConvertAsync(string queueCode)
    {
        return await _queueManager.CancelQueueAsync(queueCode);
    }

    #endregion

    #region 辅助方法

    private async Task<string> BuildFolderFullPathAsync(StandardDirectoryFolder folder)
    {
        if (string.IsNullOrEmpty(folder.ParentCode))
            return folder.FolderName;

        var parent = (await _db.GetOneAsync<StandardDirectoryFolder>(
            x => x.Code == folder.ParentCode)).Data;
        if (parent == null || string.IsNullOrEmpty(parent.FullPath))
            return folder.FolderName;

        return $"{parent.FullPath}/{folder.FolderName}";
    }

    private async Task<string?> GetQueueLockErrorAsync(string directoryCode)
    {
        var activeQueue = await _queueManager.FindRunningQueueByScopeKeyAsync(directoryCode);
        if (activeQueue != null)
            return $"有正在执行的任务队列（{activeQueue.QueueCode}），请等待完成后再操作";
        return null;
    }

    private async Task<string?> GetFileLockErrorAsync(string fileCode)
    {
        var file = (await _db.GetOneAsync<StandardDirectoryFile>(
            x => x.Code == fileCode)).Data;
        if (file == null) return null;

        var dirLockErr = await GetQueueLockErrorAsync(file.ConfigCode ?? "");
        if (dirLockErr != null) return dirLockErr;

        var lockResult = await _queueManager.FindResourceLockAsync(
            QueueManager.RESOURCE_FILE, new List<string> { fileCode });
        if (lockResult != null)
            return $"文件正在被队列 {lockResult.QueueCode} 使用，无法操作";

        return null;
    }

    private static bool IsDuplicateKeyError(Exception ex)
    {
        return ex.Message.Contains("Duplicate entry") ||
               ex.Message.Contains("duplicate key") ||
               ex.Message.Contains("1062");
    }

    #endregion

    #region 阶段文件树（文档提取规则页面）

    /// <summary>
    /// 获取阶段的完整文件树（含规则属性）
    /// 用于文档提取规则管理页面，单次返回所有层级的文件夹和文件
    /// </summary>
    public async Task<StageFileTreeResponse> GetStageFileTreeAsync(string directoryCode)
    {
        // 1. 查询所有启用的文件夹
        var allFolders = (await _db.GetListAsync<StandardDirectoryFolder>(
            x => x.ConfigCode == directoryCode && x.IsValid == 1)).Data ?? new();

        // ★ 1.1 剔除「产物目录」（pdf / markdown / _archive）。
        //   这三段是 PathBuilder 的保留段名 —— 系统把转换产物写在
        //   `{StoragePath}/pdf/x.pdf`、`{StoragePath}/markdown/x.md`（企业库还有 `_archive/`），
        //   与业务文件夹物理同层。正常路径下 ValidateFolderOrFileName 已挡住新建，
        //   但历史数据 / 直改 DB / 模板导入仍可能留下同名文件夹 → 显示层统一过滤，
        //   否则管理员会在目录树里看到自己没建过的「pdf」文件夹。
        //   连同其下所有文件一并剔除（文件挂在被剔除的文件夹下已无意义）。
        var reservedFolderCodes = CollectReservedFolderCodes(allFolders);
        if (reservedFolderCodes.Count > 0)
        {
            allFolders = allFolders.Where(f => !reservedFolderCodes.Contains(f.Code ?? "")).ToList();
        }

        // 2. 查询所有启用的文件
        var allFiles = (await _db.GetListAsync<StandardDirectoryFile>(
            x => x.ConfigCode == directoryCode && x.IsValid == 1)).Data ?? new();

        if (reservedFolderCodes.Count > 0)
        {
            allFiles = allFiles
                .Where(f => !reservedFolderCodes.Contains(f.FolderCode ?? ""))
                .ToList();
        }

        // 3. 规则状态权威来源：cert_doc_extraction_rule（按 StandardFileCode 关联）
        var ruleStatusMap = new Dictionary<string, string>();
        try
        {
            var stageFileCodes = allFiles.Select(f => f.Code ?? "").ToList();
            if (stageFileCodes.Count > 0)
            {
                var rules = (await _db.GetListAsync<DocExtractionRule>(
                    x => stageFileCodes.Contains(x.StandardFileCode))).Data ?? new();
                foreach (var r in rules)
                {
                    string code = r.StandardFileCode;
                    string status = r.Status;
                    if (!string.IsNullOrEmpty(code))
                        ruleStatusMap[code] = status; // 取最后一条（最新）
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[GetStageFileTree] 规则状态查询失败，降级为 none");
        }

        // 4. 构建根级文件夹树
        var rootFolders = allFolders
            .Where(x => string.IsNullOrEmpty(x.ParentCode))
            .OrderBy(x => x.SortOrder).ToList();

        var folderNodes = new List<StageFolderNode>();
        int totalFolders = 0, totalFiles = 0, configuredFiles = 0;

        foreach (var root in rootFolders)
        {
            var node = BuildStageFolderNode(root, allFolders, allFiles, ruleStatusMap,
                ref totalFolders, ref totalFiles, ref configuredFiles, new HashSet<string>(), 0);
            folderNodes.Add(node);
        }

        // 5. 根目录级孤儿文件（FolderCode 不在任何文件夹中）
        var folderCodeSet = new HashSet<string>(allFolders.Select(f => f.Code ?? ""));
        var rootOrphanFiles = allFiles
            .Where(f => !folderCodeSet.Contains(f.FolderCode)).ToList();

        if (rootOrphanFiles.Count > 0)
        {
            var rootNode = new StageFolderNode
            {
                Code = directoryCode,
                Name = "根目录",
                ParentCode = string.Empty,
                Depth = 0,
                SortOrder = 0
            };
            foreach (var file in rootOrphanFiles)
            {
                totalFiles++;
                var fileRuleStatus = ruleStatusMap.TryGetValue(file.Code ?? "", out var rs) ? rs : "none";
                bool hasRule = fileRuleStatus == "configured" || fileRuleStatus == "failed";
                if (hasRule) configuredFiles++;

                rootNode.Files.Add(new StageFileNode
                {
                    FileCode = file.Code ?? "",
                    FileName = file.FileName,
                    FolderCode = file.FolderCode,
                    StoragePath = file.StoragePath,
                    ConvertedStoragePath = file.ConvertedStoragePath,
                    ConvertStatus = file.ConvertStatus,
                    ConvertMessage = file.ConvertMessage,
                    // ★ 双产物链字段（漏映射 = 前端静默拿不到 → 误判"转换失败"）
                    PreviewPdfPath = file.PreviewPdfPath,
                    MarkdownPath = file.MarkdownPath,
                    MarkdownStatus = file.MarkdownStatus,
                    MarkdownMessage = file.MarkdownMessage,
                    UploadStatus = file.UploadStatus ?? "",
                    FileSize = file.FileSize,
                    MimeType = file.FileType,
                    RuleStatus = fileRuleStatus,
                    ExtractFieldCount = 0,
                    TableDefCount = 0
                });
            }
            folderNodes.Insert(0, rootNode);
        }

        return new StageFileTreeResponse
        {
            DirectoryCode = directoryCode,
            Folders = folderNodes,
            Statistics = new StageFileStatistics
            {
                TotalFolders = totalFolders,
                TotalFiles = totalFiles,
                ConfiguredFiles = configuredFiles
            }
        };
    }

    /// <summary>
    /// 阶段文件树递归构建。
    /// <para>★ P0-6：带 <paramref name="visited"/> + 深度上限 —— 环状 <c>ParentCode</c>
    /// （A→B→A）会让无保护递归 StackOverflow 崩进程。</para>
    /// </summary>
    private StageFolderNode BuildStageFolderNode(
        StandardDirectoryFolder folder,
        List<StandardDirectoryFolder> allFolders,
        List<StandardDirectoryFile> allFiles,
        Dictionary<string, string> ruleStatusMap,
        ref int totalFolders, ref int totalFiles, ref int configuredFiles,
        HashSet<string> visited, int depth)
    {
        totalFolders++;
        var node = new StageFolderNode
        {
            Code = folder.Code ?? "",
            Name = folder.FolderName ?? "",
            ParentCode = folder.ParentCode ?? "",
            Depth = folder.Depth,
            SortOrder = folder.SortOrder
        };

        // ★ P0-6：环 / 超深直接停在当前层（节点已建，只是不再向下递归）
        if (depth > MAX_TREE_DEPTH || !visited.Add(folder.Code ?? "")) return node;

        // 子文件夹
        var children = allFolders
            .Where(x => x.ParentCode == folder.Code)
            .OrderBy(x => x.SortOrder).ToList();
        foreach (var child in children)
            node.Children.Add(BuildStageFolderNode(child, allFolders, allFiles, ruleStatusMap,
                ref totalFolders, ref totalFiles, ref configuredFiles, visited, depth + 1));

        // 文件
        var files = allFiles
            .Where(x => x.FolderCode == folder.Code)
            .OrderBy(x => x.SortOrder).ToList();
        foreach (var file in files)
        {
            totalFiles++;
            var fileRuleStatus = ruleStatusMap.TryGetValue(file.Code ?? "", out var rs) ? rs : "none";
            bool hasRule = fileRuleStatus == "configured" || fileRuleStatus == "failed";
            if (hasRule) configuredFiles++;

            node.Files.Add(new StageFileNode
            {
                FileCode = file.Code ?? "",
                FileName = file.FileName,
                FolderCode = file.FolderCode,
                StoragePath = file.StoragePath,
                ConvertedStoragePath = file.ConvertedStoragePath,
                ConvertStatus = file.ConvertStatus,
                ConvertMessage = file.ConvertMessage,
                // ★ 双产物链字段（漏映射 = 前端静默拿不到 → 误判"转换失败"）
                PreviewPdfPath = file.PreviewPdfPath,
                MarkdownPath = file.MarkdownPath,
                MarkdownStatus = file.MarkdownStatus,
                MarkdownMessage = file.MarkdownMessage,
                UploadStatus = file.UploadStatus ?? "",
                FileSize = file.FileSize,
                MimeType = file.FileType,
                RuleStatus = fileRuleStatus,
                ExtractFieldCount = 0,
                TableDefCount = 0
            });
        }
        return node;
    }

    #endregion

    #region 目录级文件查询

    /// <summary>
    /// 获取目录下所有文件（根级文件列表，用于前端展示）
    /// </summary>
    public async Task<List<StandardDirectoryFile>> GetFilesByDirectoryAsync(string directoryCode)
    {
        return (await _db.GetListAsync<StandardDirectoryFile>(
            x => x.ConfigCode == directoryCode && x.IsValid == 1))
            .Data ?? new();
    }

    #endregion

    #region 手动创建文件记录

    /// <summary>
    /// 手动创建文件记录（不经上传流程，直接创建）
    /// </summary>
    public async Task<(bool ok, string? error, StandardDirectoryFile? file)> CreateFileAsync(
        StandardDirectoryFile file)
    {
        // 从父文件夹回填 ConfigCode（决策 ⑦：DirectoryCode/复合 FileCode 已删）
        if (!string.IsNullOrEmpty(file.FolderCode))
        {
            var parent = (await _db.GetOneAsync<StandardDirectoryFolder>(
                x => x.Code == file.FolderCode && x.IsValid == 1)).Data;
            if (parent == null) return (false, "父文件夹不存在", null);
            file.ConfigCode = parent.ConfigCode ?? file.ConfigCode;
        }
        if (string.IsNullOrEmpty(file.ConfigCode))
            return (false, "缺少目录配置 ConfigCode", null);

        var lockErr = await GetQueueLockErrorAsync(file.ConfigCode);
        if (lockErr != null) return (false, lockErr, null);

        var nameErr = ValidateFolderOrFileName(file.FileName ?? "", "文件名");
        if (nameErr != null) return (false, nameErr, null);

        if (file.Code == null || file.Code.Length == 0)
            file.Code = Guid.NewGuid().ToString("N");
        file.IsValid = 1;
        file.Status = "draft";
        file.UploadStatus = "active";
        file.CreateTime = DateTime.Now;

        // 计算 FullPath
        if (!string.IsNullOrEmpty(file.FolderCode))
        {
            var parentFolder = (await _db.GetOneAsync<StandardDirectoryFolder>(
                x => x.Code == file.FolderCode && x.IsValid == 1)).Data;
            var parentPath = parentFolder?.FullPath?.Trim('/') ?? "";
            file.FullPath = string.IsNullOrEmpty(parentPath)
                ? file.FileName
                : $"{parentPath}/{file.FileName}";
        }
        else
        {
            file.FullPath = file.FileName;
        }

        // ★⑬/§11.1：uk_cfg_fullpath 不含 IsDeleted → 建前含已删查重，命中已删同名行就地复活
        //   （Code 不变 —— 提取规则 StandardFileCode / 队列引用的就是这个 Code）
        var dup = await _db.Client.Queryable<StandardDirectoryFile>()
            .Where(x => x.ConfigCode == file.ConfigCode && x.FullPath == file.FullPath)
            .FirstAsync();
        if (dup != null)
        {
            if (!dup.IsDeleted)
                return (false, $"同目录已存在同名文件「{file.FileName}」", null);

            dup.IsDeleted = false;
            dup.DeleteBy = null;
            dup.DeleteTime = null;
            dup.IsValid = 1;
            dup.Status = "draft";
            dup.UploadStatus = "active";
            dup.FileName = file.FileName;
            dup.FolderCode = file.FolderCode;
            dup.StoragePath = file.StoragePath;
            dup.ConvertedStoragePath = file.ConvertedStoragePath;
            dup.FileSize = file.FileSize;
            dup.FileType = file.FileType;
            dup.Remark = file.Remark;
            dup.UpdateTime = DateTime.Now;
            await _db.UpdateAsync(dup,
                nameof(StandardDirectoryFile.IsDeleted), nameof(StandardDirectoryFile.DeleteBy),
                nameof(StandardDirectoryFile.DeleteTime), nameof(StandardDirectoryFile.IsValid),
                nameof(StandardDirectoryFile.Status), nameof(StandardDirectoryFile.UploadStatus),
                nameof(StandardDirectoryFile.FileName), nameof(StandardDirectoryFile.FolderCode),
                nameof(StandardDirectoryFile.StoragePath), nameof(StandardDirectoryFile.ConvertedStoragePath),
                nameof(StandardDirectoryFile.FileSize), nameof(StandardDirectoryFile.FileType),
                nameof(StandardDirectoryFile.Remark), nameof(StandardDirectoryFile.UpdateTime));
            return (true, null, dup);
        }

        var result = await _db.InsertAsync(file);
        return result.Data != null
            ? (true, null, result.Data)
            : (false, "创建失败", null);
    }

    #endregion

    #region 文件锁定状态批量查询

    /// <summary>
    /// 批量查询文件锁定状态，返回 {fileCode: queueCode} 字典
    /// </summary>
    public async Task<Dictionary<string, string>> GetFileLockStatusAsync(List<string> fileCodes)
    {
        if (fileCodes == null || fileCodes.Count == 0) return new Dictionary<string, string>();
        var hit = await _queueManager.FindResourceLockAsync(QueueManager.RESOURCE_FILE, fileCodes);
        if (hit == null) return new Dictionary<string, string>();
        return new Dictionary<string, string> { [hit.ResourceCode] = hit.QueueCode };
    }

    #endregion

    #region 导出打包 ZIP

    /// <summary>
    /// 将选中的文件夹和文件打包成 ZIP
    /// 使用 IObjectStorage 接口（支持 MinIO / 阿里 OSS 切换）
    /// </summary>
    public async Task<Stream> ExportAsZipAsync(
        string directoryCode, List<string> selectedFolderCodes, List<string> selectedFileCodes)
    {
        var config = (await _db.GetOneAsync<StandardDirectoryConfig>(
            x => x.Code == directoryCode && x.IsValid == 1)).Data;
        if (config == null)
            throw new ArgumentException("目录配置不存在");

        var allFolders = (await _db.GetListAsync<StandardDirectoryFolder>(
            x => x.ConfigCode == directoryCode && x.IsValid == 1)).Data ?? new();

        // 展开选中的文件夹（含子文件夹）
        var expandedFolderCodes = ExpandFolderCodes(allFolders, selectedFolderCodes);
        var expandedFileCodes = new HashSet<string>(selectedFileCodes ?? new List<string>());

        if (expandedFolderCodes.Count > 0)
        {
            var folderFiles = (await _db.GetListAsync<StandardDirectoryFile>(
                x => expandedFolderCodes.Contains(x.FolderCode) && x.IsValid == 1)).Data ?? new();
            foreach (var f in folderFiles)
                expandedFileCodes.Add(f.Code ?? "");
        }

        if (expandedFileCodes.Count == 0)
            throw new ArgumentException("没有找到可导出的文件");

        var filesToExport = (await _db.GetListAsync<StandardDirectoryFile>(
            x => expandedFileCodes.Contains(x.Code) && x.IsValid == 1)).Data ?? new();

        // 创建临时目录
        var tempDir = Path.Combine(Path.GetTempPath(), $"export_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // ★ P1-18：下载失败禁止写占位文件冒充成功 —— 先收集失败项，循环结束后整体失败并报出原因
            var failed = new List<string>();
            foreach (var file in filesToExport)
            {
                var folderPath = GetFolderPath(allFolders, file.FolderCode);
                var entryPath = string.IsNullOrEmpty(folderPath)
                    ? file.FileName
                    : $"{folderPath}/{file.FileName}";

                // ★ P0-2：ZIP Slip —— 逐段校验条目名，拒绝 .. / 绝对路径 / 盘符
                foreach (var seg in entryPath.Split('/', StringSplitOptions.RemoveEmptyEntries))
                {
                    var segErr = ValidateFolderOrFileName(seg, "导出条目名");
                    if (segErr != null)
                        throw new ArgumentException(segErr);
                }

                var objectName = file.StoragePath;
                if (string.IsNullOrEmpty(objectName))
                    objectName = PathBuilder.StandardFile(
                        config.OrgCode, config.StandardCode, config.StageCode, folderPath, file.FileName);
                objectName = objectName.TrimStart('/');

                var localPath = Path.Combine(tempDir, entryPath.Replace('/', Path.DirectorySeparatorChar));
                // ★ P0-2：算完本地路径再兜一道前缀校验（防分隔符归一化后逃逸临时目录）
                var fullLocal = Path.GetFullPath(localPath);
                var fullTemp = Path.GetFullPath(tempDir);
                if (!fullLocal.StartsWith(fullTemp + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                    throw new ArgumentException($"导出路径越界：{entryPath}");
                var localDir = Path.GetDirectoryName(localPath);
                if (!string.IsNullOrEmpty(localDir))
                    Directory.CreateDirectory(localDir);

                try
                {
                    // 使用 IObjectStorage 接口下载（非直接调 MinIO SDK）
                    var (stream, _) = await _storage.DownloadAsync(objectName);
                    using var fileStream = File.Create(localPath);
                    await stream.CopyToAsync(fileStream);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[ExportAsZip] 下载对象失败: {ObjectName}", objectName);
                    failed.Add($"{file.FileName}（{ex.Message}）");
                }
            }

            if (failed.Count > 0)
            {
                var shown = string.Join("；", failed.Take(5));
                throw new InvalidOperationException(
                    $"导出失败：{failed.Count} 个文件下载失败 —— {shown}{(failed.Count > 5 ? " …" : "")}");
            }

            var ms = new MemoryStream();
            using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
            {
                AddDirectoryToZip(archive, tempDir, "");
            }
            ms.Position = 0;
            return ms;
        }
        finally
        {
            try { Directory.Delete(tempDir, true); }
            catch (Exception ex) { _logger.LogWarning(ex, "[ExportAsZip] 清理临时目录失败"); }
        }
    }

    private HashSet<string> ExpandFolderCodes(List<StandardDirectoryFolder> allFolders, List<string> folderCodes)
    {
        var result = new HashSet<string>(folderCodes ?? new List<string>());
        var visited = new HashSet<string>();
        foreach (var code in folderCodes ?? Enumerable.Empty<string>())
            AddChildFolderCodes(allFolders, code, result, visited, 0);
        return result;
    }

    /// <summary>
    /// 收集子文件夹 Code。
    /// <para>★ P0-6 修复：带 visited + 深度上限 —— 环状 ParentCode 会让原递归 StackOverflow 崩进程。</para>
    /// </summary>
    private void AddChildFolderCodes(
        List<StandardDirectoryFolder> allFolders, string parentCode,
        HashSet<string> result, HashSet<string> visited, int depth)
    {
        if (depth > MAX_TREE_DEPTH || !visited.Add(parentCode)) return;
        var children = allFolders.Where(x => x.ParentCode == parentCode);
        foreach (var child in children)
        {
            var code = child.Code ?? "";
            if (!result.Add(code)) continue;
            AddChildFolderCodes(allFolders, code, result, visited, depth + 1);
        }
    }

    /// <summary>
    /// 由文件夹 Code 反推相对路径。
    /// <para>★ P0-6：环状 ParentCode 用访问集 + 跳数上限兜底，避免死循环。</para>
    /// </summary>
    private string GetFolderPath(List<StandardDirectoryFolder> allFolders, string folderCode)
    {
        var parts = new List<string>();
        var visited = new HashSet<string>();
        string current = folderCode;
        while (!string.IsNullOrEmpty(current) && visited.Add(current) && parts.Count <= MAX_TREE_DEPTH)
        {
            var folder = allFolders.FirstOrDefault(x => x.Code == current);
            if (folder == null) break;
            parts.Insert(0, folder.FolderName ?? "");
            current = folder.ParentCode;
        }
        return string.Join("/", parts);
    }

    private void AddDirectoryToZip(ZipArchive archive, string sourceDir, string entryPrefix)
    {
        foreach (var filePath in Directory.GetFiles(sourceDir))
        {
            var fileName = Path.GetFileName(filePath);
            var entryName = string.IsNullOrEmpty(entryPrefix) ? fileName : $"{entryPrefix}/{fileName}";
            archive.CreateEntryFromFile(filePath, entryName, System.IO.Compression.CompressionLevel.Optimal);
        }
        foreach (var dirPath in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(dirPath);
            var newPrefix = string.IsNullOrEmpty(entryPrefix) ? dirName : $"{entryPrefix}/{dirName}";
            archive.CreateEntry($"{newPrefix}/");
            AddDirectoryToZip(archive, dirPath, newPrefix);
        }
    }

    #endregion

    #region 旧版单文件上传（兼容旧前端）

    /// <summary>
    /// 旧版单文件直接上传（兼容旧前端直接上传场景）
    ///
    /// 与 UploadInit/UploadFile/UploadConfirm 4 步方案不同，此方法一步完成：
    /// 接收文件流 → 写 MinIO → 写 DB 记录
    /// </summary>
    public async Task<(bool ok, string? error, StandardDirectoryFile? file)> UploadFileLegacyAsync(
        Stream fileStream, string fileName, string directoryCode, string folderCode,
        string orgCode, string? standardCode = null, string? phaseCode = null)
    {
        // 校验文件类型
        var typeErr = ValidateUploadFileType(fileName);
        if (typeErr != null) return (false, typeErr, null);

        var lockErr = await GetQueueLockErrorAsync(directoryCode);
        if (lockErr != null) return (false, lockErr, null);

        // 决策 ⑦/⑩：复合 DirectoryCode 已删 —— 目录定位改用 config.Code，阶段列名 StageCode
        var config = (await _db.GetOneAsync<StandardDirectoryConfig>(
            x => x.Code == directoryCode)).Data;
        if (config == null) return (false, "目录配置不存在", null);

        standardCode ??= config.StandardCode;
        phaseCode ??= config.StageCode;
        if (string.IsNullOrEmpty(standardCode) || string.IsNullOrEmpty(phaseCode))
            return (false, "标准编码/阶段编码缺失，无法生成存储路径", null);

        // 父文件夹相对路径（决策 ⑨：根级传空）
        string parentDir = "";
        if (!string.IsNullOrEmpty(folderCode))
        {
            var parentFolder = (await _db.GetOneAsync<StandardDirectoryFolder>(
                x => x.Code == folderCode && x.IsValid == 1)).Data;
            if (parentFolder == null) return (false, "父文件夹不存在", null);
            parentDir = parentFolder.FullPath ?? "";
        }

        var nameErr = ValidateFolderOrFileName(fileName, "文件名");
        if (nameErr != null) return (false, nameErr, null);

        // ★ 路径唯一权威：PathBuilder（决策⑳修订：身份段 = OrgCode + Code 原文）
        var storagePath = PathBuilder.StandardFile(config.OrgCode, standardCode, phaseCode, parentDir, fileName);
        var fullPath = string.IsNullOrEmpty(parentDir) ? fileName : $"{parentDir}/{fileName}";

        // 上传到 MinIO（使用 IObjectStorage 接口）
        try
        {
            var objectName = storagePath.TrimStart('/');
            var contentType = GetContentType(fileName);
            await _storage.UploadAsync(objectName, fileStream, fileStream.Length, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[UploadFileLegacy] 上传文件到存储失败: {FileName}", fileName);
            return (false, $"上传失败：{ex.Message}", null);
        }

        // 创建 DB 记录
        var file = new StandardDirectoryFile
        {
            Code = Guid.NewGuid().ToString("N"),
            FolderCode = folderCode ?? "",
            ConfigCode = config.Code ?? directoryCode,
            StandardCode = standardCode,
            StageCode = phaseCode,
            FileName = fileName,
            FileType = Path.GetExtension(fileName)?.TrimStart('.'),
            StoragePath = storagePath,
            FullPath = fullPath,
            IsValid = 1,
            UploadStatus = "active",
            Status = "draft",
            CreateTime = DateTime.Now
        };
        var result = await _db.InsertAsync(file);
        return result.Data != null
            ? (true, null, result.Data)
            : (false, "创建文件记录失败", null);
    }

    private static string GetContentType(string fileName)
    {
        var ext = Path.GetExtension(fileName)?.ToLower();
        return ext switch
        {
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".doc" => "application/msword",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".xls" => "application/vnd.ms-excel",
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".tif" or ".tiff" => "image/tiff",
            ".txt" => "text/plain",
            ".rtf" => "application/rtf",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".ppt" => "application/vnd.ms-powerpoint",
            _ => "application/octet-stream"
        };
    }

    private static string? ValidateUploadFileType(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return "文件路径不能为空";

        if (fileName.StartsWith(".")) return $"不允许上传系统文件：{fileName}";

        var ext = Path.GetExtension(fileName)?.TrimStart('.').ToLower() ?? "";
        var allowedExts = new HashSet<string>
        {
            "pdf", "doc", "docx", "xls", "xlsx", "ppt", "pptx", "txt", "rtf",
            "jpg", "jpeg", "png", "gif", "bmp", "webp", "tif", "tiff"
        };
        if (!allowedExts.Contains(ext))
            return $"不支持的文件类型：{fileName}（仅支持文档/图片）";

        return null;
    }

    #endregion

    #region 存量上传任务修复

    /// <summary>
    /// 修复卡死的存量上传任务。
    /// 历史缺陷：UploadFileAsync/UploadConfirmAsync 用 GetOneAsync（强制 IsValid=1）读 pending 文件，
    /// 永远查不到 → SuccessCount 虽然计数，但文件记录停留 pending/IsValid=0，confirm 报「未上传完成」。
    /// 修复策略：对卡死任务逐文件检查 MinIO 对象是否存在，存在则回填 FileSize 并走 confirm 同款激活流程。
    /// </summary>
    /// <param name="taskId">指定任务；空 = 修复全部 initialized 且已过期的任务</param>
    public async Task<(bool ok, string? error, int repaired, int enqueued)> RepairStuckUploadsAsync(string? taskId = null)
    {
        try
        {
            // 1. 以「仍有中间态文件」为准找卡死任务（历史缺陷会把任务提前置 completed，不能只看任务状态）
            var stuckFileScan = (await _db.GetListAsync<StandardDirectoryFile>(
                x => x.UploadStatus == "pending" || x.UploadStatus == "replacing", includeDisabled: true)).Data ?? new();
            if (!string.IsNullOrEmpty(taskId))
                stuckFileScan = stuckFileScan.Where(x => x.TaskId == taskId).ToList();

            if (stuckFileScan.Count == 0)
                return (true, null, 0, 0); // ★ P1-10：ok=true 时 error 必须为 null（无待修复项看 repaired==0）

            var stuckTaskIds = stuckFileScan.Where(x => !string.IsNullOrEmpty(x.TaskId))
                .Select(x => x.TaskId!).Distinct().ToList();
            var stuckTasks = new List<UploadTask>();
            foreach (var tid in stuckTaskIds)
            {
                var one = (await _db.GetOneIgnoreValidAsync<UploadTask>(x => x.TaskId == tid)).Data;
                if (one != null) stuckTasks.Add(one);
            }
            var orphanFiles = stuckFileScan.Where(x => string.IsNullOrEmpty(x.TaskId)).ToList();

            int totalRepaired = 0;
            int totalEnqueued = 0;

            foreach (var task in stuckTasks)
            {
                // 2. 任务下所有中间态文件（IgnoreValid 读 pending/replacing/uploaded）
                var files = (await _db.GetListAsync<StandardDirectoryFile>(
                    x => x.TaskId == task.TaskId, includeDisabled: true)).Data ?? new();
                if (files.Count == 0)
                {
                    // 无关联文件：直接关任务
                    task.Status = "completed";
                    await _db.UpdateAsync(task, nameof(UploadTask.Status));
                    continue;
                }

                // 3. 逐文件检查 MinIO：存在则回填大小
                var activated = new List<StandardDirectoryFile>();
                foreach (var f in files.Where(x => x.UploadStatus == "pending" || x.UploadStatus == "replacing"))
                {
                    var objectName = (f.StoragePath ?? "").TrimStart('/');
                    if (string.IsNullOrEmpty(objectName) || !await _storage.ExistsAsync(objectName))
                        continue; // 物理文件缺失，保留原状（用户需重传）

                    // MinIO stat 拿实际大小：用 DownloadAsync 的 stat（接口无独立 Stat，借用 ListObjects 不可行，直接下载统计太重）。
                    // 这里采用「上传时已知 manifest.FileSize」不可靠，直接用 Exists + 保留原 FileSize；
                    // 但历史缺陷下 FileSize=NULL，因此用小流下载统计真实大小（一次性修复，可接受）。
                    try
                    {
                        var (stream, _) = await _storage.DownloadAsync(objectName);
                        using (stream)
                        {
                            long size = 0;
                            var buf = new byte[81920];
                            int n;
                            while ((n = await stream.ReadAsync(buf, 0, buf.Length)) > 0) size += n;
                            f.FileSize = size;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[RepairStuck] 读取对象大小失败（跳过回填）: {Path}", objectName);
                    }

                    f.UploadStatus = "uploaded";
                    totalRepaired++;
                    activated.Add(f);
                }

                // 4. confirm 同款激活：普通文件 active+IsValid=1；需转换文件进双队列
                //    ★ 2026-09-26：判据由 doc/xls 放宽为 NeedsConversion()
                var convertible = activated.Where(NeedsConversion).ToList();
                foreach (var f in activated.Where(x => !NeedsConversion(x)))
                {
                    f.IsValid = 1;
                    f.UploadStatus = "active";
                    f.TaskId = null;
                    await _db.UpdateAsync(f, nameof(StandardDirectoryFile.IsValid), nameof(StandardDirectoryFile.UploadStatus),
                        nameof(StandardDirectoryFile.TaskId), nameof(StandardDirectoryFile.FileSize));
                }
                foreach (var f in convertible)
                {
                    f.UploadStatus = "uploaded";
                    f.ConvertStatus = "pending";
                    f.MarkdownStatus = "pending";
                    f.TaskId = null;
                    await _db.UpdateAsync(f, nameof(StandardDirectoryFile.UploadStatus), nameof(StandardDirectoryFile.ConvertStatus),
                        nameof(StandardDirectoryFile.MarkdownStatus),
                        nameof(StandardDirectoryFile.TaskId), nameof(StandardDirectoryFile.FileSize));
                }

                // 5. 激活任务内文件夹
                var folders = (await _db.GetListAsync<StandardDirectoryFolder>(
                    x => x.TaskId == task.TaskId, includeDisabled: true)).Data ?? new();
                foreach (var fd in folders)
                {
                    fd.IsValid = 1;
                    fd.TaskId = null;
                    await _db.UpdateAsync(fd, nameof(StandardDirectoryFolder.IsValid), nameof(StandardDirectoryFolder.TaskId));
                }

                // 6. 建 file_convert 双队列（与 confirm 同款结构）
                if (convertible.Count > 0)
                {
                    // ★ 锁按「文件」维度（不是按载荷维度）—— 双载荷会让同一文件出现两条锁
                    var locks = new List<QueueManager.ResourceLockItem>
                    {
                        new() { ResourceTable = QueueManager.RESOURCE_DIR, ResourceCode = task.ConfigCode, ResourceName = task.ConfigCode }
                    };
                    locks.AddRange(convertible.Select((f, i) => new QueueManager.ResourceLockItem
                    {
                        ResourceTable = QueueManager.RESOURCE_FILE,
                        ResourceCode = f.Code,
                        ResourceName = f.FileName,
                        TaskNo = i + 1
                    }));

                    var req = new QueueManager.CreateQueueRequest
                    {
                        QueueType = "file_convert",
                        QueueName = $"存量修复转换 - {convertible.Count}个文件",
                        ScopeKey = task.ConfigCode,
                        SourceType = "repair_stuck_upload",
                        SourceId = task.TaskId,
                        ResourceLocks = locks,
                        Tasks = BuildConvertTasks(convertible, task.TaskId)
                    };

                    var (qok, qerr, qcode, qcount) = await _queueManager.CreateQueueAsync(req);
                    if (qok) totalEnqueued += qcount;
                    else _logger.LogWarning("[RepairStuck] 建队列失败 {DirCode}: {Err}", task.ConfigCode, qerr);
                }

                // 7. 关任务
                task.Status = "completed";
                task.UpdateTime = DateTime.Now;
                await _db.UpdateAsync(task, nameof(UploadTask.Status), nameof(UploadTask.UpdateTime));
            }

            // 8. 无任务的孤儿中间态文件（replace 流残留）：物理文件存在则直接激活（doc/xls 转换由 replace 自身队列负责）
            foreach (var f in orphanFiles)
            {
                var objectName = (f.StoragePath ?? "").TrimStart('/');
                if (string.IsNullOrEmpty(objectName) || !await _storage.ExistsAsync(objectName))
                    continue;
                f.IsValid = 1;
                f.UploadStatus = "active";
                totalRepaired++;
                await _db.UpdateAsync(f, nameof(StandardDirectoryFile.IsValid), nameof(StandardDirectoryFile.UploadStatus));
            }

            _logger.LogInformation("[RepairStuck] 修复完成：{Tasks} 个任务，{Repaired} 个文件激活，{Enqueued} 个进入转换",
                stuckTasks.Count, totalRepaired, totalEnqueued);
            return (true, null, totalRepaired, totalEnqueued);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RepairStuck] 修复存量上传任务出错");
            return (false, $"修复出错：{ex.Message}", 0, 0);
        }
    }

    #endregion

    #region 重试失败转换

    /// <summary>
    /// 重试失败的文档转换
    /// <para>扫描**任一链**处于 failed/pending 的文件，按目录分组重建双队列。</para>
    /// <para>★ 2026-09-26：判据由「doc/xls 且 ConvertStatus failed/pending」放宽为
    /// 「ConvertStatus 或 MarkdownStatus 为 failed/pending」—— 原判据让 docx/xlsx/pdf
    /// 的失败**永远无法通过本接口修复**。</para>
    /// <para>注：<c>MarkdownStatus == "unsupported"</c>（图片/扫描件）**不纳入重试** —— 那不是故障，
    /// 是能力边界，重试必然再失败。</para>
    /// </summary>
    public async Task<(bool ok, string? error, int enqueued, int queueCount)> RetryFailedConversionsAsync()
    {
        try
        {
            // 1. 候选文件：任一链 failed 或 pending
            // includeDisabled：等转换文件按设计 IsValid=0（转换完成才置 1），默认过滤会永久漏掉它们
            var candidates = (await _db.GetListAsync<StandardDirectoryFile>(
                x => !x.IsDeleted
                    && (x.ConvertStatus == "failed" || x.ConvertStatus == "pending"
                        || x.MarkdownStatus == "failed" || x.MarkdownStatus == "pending"),
                includeDisabled: true))
                .Data ?? new();

            if (candidates.Count == 0)
                return (true, null, 0, 0);

            // 2. 排除仍在队列中（有活跃资源锁）的文件
            var allLocks = (await _db.Client.Queryable<YZH.Core.Stand.Models.Queue.YzhQueueResourceLock>()
                .Where(x => x.Status == "locked" && x.ResourceTable == QueueManager.RESOURCE_FILE)
                .Select(x => x.ResourceCode)
                .ToListAsync());
            var activeLockCodes = new HashSet<string>(allLocks);

            var toRetry = new List<StandardDirectoryFile>();
            var missingSources = new List<string>();
            foreach (var f in candidates)
            {
                if (activeLockCodes.Contains(f.Code ?? "")) continue;
                if (!await SourceExistsAsync(f.StoragePath))
                {
                    missingSources.Add(f.FileName);
                    continue;
                }
                toRetry.Add(f);
            }

            if (toRetry.Count == 0)
                return (true, null, 0, 0);

            // 3. 按目录分组建队
            var groups = toRetry.GroupBy(f => f.ConfigCode ?? "");
            var enqueued = 0;
            var queueCodes = new List<string>();
            var skipped = new List<string>();

            foreach (var group in groups)
            {
                // ★ ScopeKey 统一为 configCode（= 路由锁 ResourceCode），与 UploadConfirm/Replace 同一口径；
                //   原「org|std|phase」拼法已随决策 ⑳（路径无 OrgCode 段）失效
                var scopeKey = group.Key;

                // ★ 双队列：每文件 2 个任务；锁按「文件」维度
                var locks = new List<QueueManager.ResourceLockItem>
                {
                    new() { ResourceTable = QueueManager.RESOURCE_DIR, ResourceCode = group.Key, ResourceName = group.Key }
                };
                locks.AddRange(group.Select((f, i) => new QueueManager.ResourceLockItem
                {
                    ResourceTable = QueueManager.RESOURCE_FILE,
                    ResourceCode = f.Code,
                    ResourceName = f.FileName,
                    TaskNo = i + 1
                }));

                var req = new QueueManager.CreateQueueRequest
                {
                    QueueType = "file_convert",
                    QueueName = $"失败重试-{group.Count()}个文件",
                    ScopeKey = scopeKey,
                    SourceType = "retry_failed",
                    SourceId = $"retry_{DateTime.Now:yyyyMMddHHmmss}_{group.Key}",
                    ResourceLocks = locks,
                    Tasks = BuildConvertTasks(group, group.Key)
                };

                var (ok, queueError, queueCode, count) = await _queueManager.CreateQueueAsync(req);
                if (!ok)
                {
                    skipped.Add($"{group.Key}（{queueError}）");
                    continue;
                }

                // 文件置为隐藏 + 双链 pending
                // 注：**不清** PreviewPdfPath/MarkdownPath —— 产物路径由源路径派生，重试会写回同一路径并覆盖；
                //     清空反而会在重试失败时丢失仍可用的产物引用（源文件未变 → 旧产物内容依然正确）。
                //     可见性由 PDF 链完成时置回 IsValid=1（见 OfficeConvertService）。
                foreach (var f in group)
                {
                    f.IsValid = 0;
                    f.ConvertStatus = "pending";
                    f.ConvertMessage = null;
                    f.MarkdownStatus = "pending";
                    f.MarkdownMessage = null;
                    await _db.UpdateAsync(f);
                }
                enqueued += count;
                queueCodes.Add(queueCode);
            }

            _logger.LogInformation("[RetryFailedConversions] 已重新入队 {Enqueued} 个失败文件（{QQueueCount} 个队列）",
                enqueued, queueCodes.Count);
            return (true, null, enqueued, queueCodes.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RetryFailedConversions] 重试失败转换出错");
            return (false, $"重试失败转换出错：{ex.Message}", 0, 0);
        }
    }

    /// <summary>检查存储源文件是否存在（通过 IObjectStorage 接口）</summary>
    private async Task<bool> SourceExistsAsync(string storagePath)
    {
        if (string.IsNullOrEmpty(storagePath)) return false;
        try
        {
            return await _storage.ExistsAsync(storagePath.TrimStart('/'));
        }
        catch
        {
            return true; // 其他异常不阻塞重试
        }
    }

    #endregion

    #region 存量文件产物回填（★ 2026-09-26 双产物链上线后的一次性补齐）

    /// <summary>
    /// 为**存量文件**补齐三产物（<c>PreviewPdfPath</c> / <c>MarkdownPath</c> /
    /// <c>EditableStoragePath</c>★ S-1）。
    ///
    /// <para><b>为什么需要它</b>：实测 167 行历史数据的 <c>PreviewPdfPath</c> 与
    /// <c>MarkdownPath</c> **全部为空**（产物链从未跑通过），而 <see cref="RetryFailedConversionsAsync"/>
    /// 的候选集只含 <c>failed</c>/<c>pending</c> —— 这些文件的 <c>ConvertStatus</c> 是
    /// <c>completed</c>（旧 doc→docx 链留下的），因此**永远不会被它捞到**。
    /// 结果是：目录里所有老文件至今仍走「预览时才动态转换」，提取时才现算 Markdown。</para>
    ///
    /// <para><b>候选判据</b>：未删除 + 有存储路径 + 三产物**任一为空** + 三链**都不在途**
    /// （<c>pending</c>/<c>converting</c> 视为在途，跳过以避免重复入队）。
    /// <para>★ 归一链（S-1）的「产物缺失」只在**旧二进制格式**上成立
    /// （<c>.doc</c>/<c>.xls</c>/<c>.ppt</c>）—— 其余格式该列恒为空是<b>语义</b>不是缺口，
    /// 判据由 <see cref="OfficeConvertService.EditableTargetFormat"/> 收口。</para></para>
    ///
    /// <para><b>与重试的两个刻意差异</b>：</para>
    /// <list type="number">
    ///   <item><b>不置 <c>IsValid = 0</c></b>：重试是「已知坏掉的文件先藏起来」，回填是「把整批
    ///         存量文件补齐」——若照抄置 0，目录管理页会**整片消失**（列表默认过滤 IsValid=1），
    ///         用户会以为数据被删了。文件全程可见，状态由徽标表达。</item>
    ///   <item><b>按文件裁剪任务</b>：<c>MarkdownStatus == "unsupported"</c>（图片/扫描件）
    ///         不再重复投递 Markdown 任务 —— 那不是故障，重跑必然再失败，纯浪费容器调用。</item>
    /// </list>
    ///
    /// <para>幂等：产物已齐的文件不在候选集内；重复调用只会命中仍缺产物的那些。
    /// ⚠️ 归一链的幂等还依赖「**失败也留在候选集**」：归一失败后 <c>EditableStoragePath</c>
    /// 仍为空 ⇒ 下次调用会重投（这是**期望**行为 —— 用户修好容器环境后重跑即可，
    /// 无需手工清状态）。</para>
    /// </summary>
    /// <param name="limit">单次最多处理多少个文件（防止一次把整库投进队列）</param>
    /// <param name="directoryCode">可选：只回填指定目录</param>
    public async Task<(bool ok, string? error, int scanned, int enqueued, int queueCount)> BackfillConversionsAsync(
        int limit = 200, string? directoryCode = null)
    {
        try
        {
            if (limit <= 0) limit = 200;

            // 1. 候选（SQL 粗筛，**刻意宽松**）：未删除 + 有源文件 + 三产物任一为空
            //    注：用 Queryable 直接下推到 SQL —— GetListAsync 默认会加 IsValid=1，
            //       而转换中的文件 IsValid=0，用默认值会漏掉它们（REFERENCE §二十 ⑱）。
            //    ⚠️ 归一产物列对「不需要归一」的文件（.docx 等）**恒为空** ⇒ 粗筛必然多选。
            //       多选无害（第 2 步按精确判据裁掉），但**绝不能少选** —— 故此处不写扩展名判断，
            //       精确判据统一在 BuildBackfillTasks / OfficeConvertService.EditableTargetFormat。
            var query = _db.Client.Queryable<StandardDirectoryFile>()
                .Where(x => !x.IsDeleted)
                .Where(x => x.StoragePath != null && x.StoragePath != "")
                .Where(x => (x.PreviewPdfPath == null || x.PreviewPdfPath == "")
                         || (x.MarkdownPath == null || x.MarkdownPath == "")
                         || (x.EditableStoragePath == null || x.EditableStoragePath == ""));

            if (!string.IsNullOrWhiteSpace(directoryCode))
                query = query.Where(x => x.ConfigCode == directoryCode);

            var rows = await query.ToListAsync();

            // 2. 剔除「已在途」的文件（三链任一 pending/converting）
            //    + 剔除「无活可干」的（粗筛多选进来的：不需要归一且三产物都齐）——
            //      不剔则 Take(limit) 会被这些行占满配额，真正待办的文件永远排不上（表现为
            //      「回填一直返回 0」且不报错）。
            var candidates = rows
                .Where(f => !IsChainInFlight(f.ConvertStatus)
                         && !IsChainInFlight(f.MarkdownStatus)
                         && !IsChainInFlight(f.EditableStatus))
                .Where(f => string.IsNullOrEmpty(f.PreviewPdfPath)
                         || string.IsNullOrEmpty(f.MarkdownPath)
                         || (OfficeConvertService.EditableTargetFormat(f.FileName) != null
                             && string.IsNullOrEmpty(f.EditableStoragePath)))
                .OrderBy(f => f.ConfigCode)
                .ThenBy(f => f.FileName)
                .Take(limit)
                .ToList();

            if (candidates.Count == 0)
                return (true, null, rows.Count, 0, 0);

            // 3. 剔除有活跃资源锁的文件（正在被别的队列处理）
            var activeLockCodes = new HashSet<string>(
                await _db.Client.Queryable<YzhQueueResourceLock>()
                    .Where(x => x.Status == "locked" && x.ResourceTable == QueueManager.RESOURCE_FILE)
                    .Select(x => x.ResourceCode)
                    .ToListAsync());

            var todo = new List<(StandardDirectoryFile File, List<QueueManager.TaskItem> Tasks)>();
            var missingSources = 0;
            foreach (var f in candidates)
            {
                if (activeLockCodes.Contains(f.Code ?? "")) continue;
                if (!await SourceExistsAsync(f.StoragePath)) { missingSources++; continue; }

                var tasks = BuildBackfillTasks(f);
                if (tasks.Count > 0) todo.Add((f, tasks));
            }

            if (todo.Count == 0)
                return (true, null, rows.Count, 0, 0);

            // 4. 按目录分组建队（一个目录一个队列，与上传流程的粒度一致）
            var enqueued = 0;
            var queueCodes = new List<string>();
            var skipped = new List<string>();

            foreach (var group in todo.GroupBy(x => x.File.ConfigCode ?? ""))
            {
                // ★ ScopeKey 统一为 configCode（与 UploadConfirm/Replace/Retry 同一口径）
                var scopeKey = group.Key;

                var files = group.Select(x => x.File).ToList();

                // 锁按「文件」维度（双产物任务共用同一把文件锁，避免同一文件被两个队列同时改）
                var locks = new List<QueueManager.ResourceLockItem>
                {
                    new() { ResourceTable = QueueManager.RESOURCE_DIR, ResourceCode = group.Key, ResourceName = group.Key }
                };
                locks.AddRange(files.Select((f, i) => new QueueManager.ResourceLockItem
                {
                    ResourceTable = QueueManager.RESOURCE_FILE,
                    ResourceCode = f.Code,
                    ResourceName = f.FileName,
                    TaskNo = i + 1
                }));

                var req = new QueueManager.CreateQueueRequest
                {
                    QueueType = "file_convert",
                    QueueName = $"存量回填-{files.Count}个文件",
                    ScopeKey = scopeKey,
                    SourceType = "backfill",
                    SourceId = $"backfill_{DateTime.Now:yyyyMMddHHmmss}_{group.Key}",
                    ResourceLocks = locks,
                    Tasks = group.SelectMany(x => x.Tasks).ToList()
                };

                var (ok, queueError, queueCode, count) = await _queueManager.CreateQueueAsync(req);
                if (!ok)
                {
                    skipped.Add($"{group.Key}（{queueError}）");
                    continue;
                }

                // 只把「已投递的那条链」置 pending；未投递的链保持原值
                // （例如 MarkdownStatus=unsupported 的文件，本次只投 PDF，就不能把 Markdown 改成 pending）
                foreach (var (f, _) in group)
                {
                    var pdfTasked = string.IsNullOrEmpty(f.PreviewPdfPath);
                    var mdTasked = string.IsNullOrEmpty(f.MarkdownPath) && f.MarkdownStatus != "unsupported";
                    var edTasked = OfficeConvertService.EditableTargetFormat(f.FileName) != null
                                && string.IsNullOrEmpty(f.EditableStoragePath);

                    // ★ 列级写回（2026-10-03 修正）：原实现是 UpdateAsync(f) **全列写回**。
                    //   入队后队列可能**立即**开始执行，执行器已把 EditableStatus 改成 converting/failed，
                    //   而 f 是「投递前」的快照 ⇒ 全列写回会把执行器的写入**覆盖回旧值**且零报错
                    //   （与 OfficeConvertService 类注释记录的 2026-09-26 事故同一机理）。
                    var cols = new List<string> { nameof(StandardDirectoryFile.UpdateTime) };
                    f.UpdateTime = DateTime.Now;

                    if (pdfTasked)
                    {
                        f.ConvertStatus = "pending"; f.ConvertMessage = null;
                        cols.Add(nameof(StandardDirectoryFile.ConvertStatus));
                        cols.Add(nameof(StandardDirectoryFile.ConvertMessage));
                    }
                    if (mdTasked)
                    {
                        f.MarkdownStatus = "pending"; f.MarkdownMessage = null;
                        cols.Add(nameof(StandardDirectoryFile.MarkdownStatus));
                        cols.Add(nameof(StandardDirectoryFile.MarkdownMessage));
                    }
                    if (edTasked)
                    {
                        f.EditableStatus = "pending"; f.EditableMessage = null;
                        cols.Add(nameof(StandardDirectoryFile.EditableStatus));
                        cols.Add(nameof(StandardDirectoryFile.EditableMessage));
                    }
                    // ⚠️ 刻意不动 IsValid（见方法注释「与重试的两个刻意差异」）
                    await _db.UpdateAsync(f, cols.ToArray());
                }

                enqueued += count;
                queueCodes.Add(queueCode);
            }

            _logger.LogInformation(
                "[BackfillConversions] 扫描 {Scanned} 行，入队 {Enqueued} 个任务（{QueueCount} 个队列），源文件缺失 {Missing}，跳过 {Skipped} 个目录",
                rows.Count, enqueued, queueCodes.Count, missingSources, skipped.Count);

            return (true, null, rows.Count, enqueued, queueCodes.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BackfillConversions] 存量产物回填出错");
            return (false, $"存量产物回填出错：{ex.Message}", 0, 0, 0);
        }
    }

    /// <summary>双链状态是否「在途」（入队后未落定）——在途文件不重复入队</summary>
    private static bool IsChainInFlight(string? status)
        => status == "pending" || status == "converting";

    /// <summary>
    /// 为单个文件构造**按需裁剪**的回填任务：只投「产物缺失」的那条链。
    /// <para>与 <see cref="BuildConvertTasks"/> 的区别：那个是「上传/重试，三条链都要」，这个是「补缺，只补缺的」。</para>
    /// </summary>
    private static List<QueueManager.TaskItem> BuildBackfillTasks(StandardDirectoryFile f)
    {
        if (!NeedsConversion(f)) return new();

        var payloads = new List<FileConvertPayload>();

        // ① 预览链：产物缺失才投
        if (string.IsNullOrEmpty(f.PreviewPdfPath))
        {
            payloads.Add(new FileConvertPayload
            {
                Code = f.Code ?? "", FileName = f.FileName,
                SourcePath = f.StoragePath, ConvertType = "office2pdf"
            });
        }

        // ② 提取链：产物缺失 且 不是已知的能力边界（unsupported）才投
        if (string.IsNullOrEmpty(f.MarkdownPath) && f.MarkdownStatus != "unsupported")
        {
            payloads.Add(new FileConvertPayload
            {
                Code = f.Code ?? "", FileName = f.FileName,
                SourcePath = f.StoragePath, ConvertType = "anydoc2md"
            });
        }

        // ③ ★ 归一链（S-1）：**仅旧二进制格式** 且 产物缺失才投。
        //    ⚠️ 「需不需要归一」的判据只此一处（EditableTargetFormat）—— 不要为了少扫几行
        //       在别处再写一份扩展名判断，两处漂移会「零报错地」不一致。
        if (OfficeConvertService.EditableTargetFormat(f.FileName) != null
            && string.IsNullOrEmpty(f.EditableStoragePath))
        {
            payloads.Add(new FileConvertPayload
            {
                Code = f.Code ?? "", FileName = f.FileName,
                SourcePath = f.StoragePath, ConvertType = "office2editable"
            });
        }

        return payloads.Select(p => new QueueManager.TaskItem
        {
            TaskType = "file_convert",
            Payload = JsonSerializer.Serialize(p, PayloadJsonOptions),
            TaskId = f.Code ?? ""
        }).ToList();
    }

    #endregion
}

#region 辅助类

// FileConvertPayload 已移至 OfficeConvertService.cs（2026-09-16 扩展 ConvertType 语义）

/// <summary>
/// 阶段文件树响应
/// </summary>
public class StageFileTreeResponse
{
    public string DirectoryCode { get; set; } = "";
    public List<StageFolderNode> Folders { get; set; } = new();
    public StageFileStatistics Statistics { get; set; } = new();
}

/// <summary>
/// 阶段文件统计
/// </summary>
public class StageFileStatistics
{
    public int TotalFolders { get; set; }
    public int TotalFiles { get; set; }
    public int ConfiguredFiles { get; set; }
}

/// <summary>
/// 阶段文件夹节点
/// </summary>
public class StageFolderNode
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string ParentCode { get; set; } = "";
    public int Depth { get; set; }
    public int SortOrder { get; set; }
    public List<StageFolderNode> Children { get; set; } = new();
    public List<StageFileNode> Files { get; set; } = new();
}

/// <summary>
/// 阶段文件节点
/// <para>⚠️ 本类型与 <c>CertPlatform.Shared.Entities.Dir.StageFileNode</c> **同名不同类**（历史遗留双份定义）。
/// 本命名空间内的定义**遮蔽** using 引入的那个，因此本类是接口实际序列化出去的类型。
/// 改字段必须**两处同步**，否则前端静默拿不到字段。</para>
/// </summary>
public class StageFileNode
{
    public string FileCode { get; set; } = "";
    public string FileName { get; set; } = "";
    public string FolderCode { get; set; } = "";
    public string StoragePath { get; set; } = "";
    public string ConvertedStoragePath { get; set; } = "";
    public string ConvertStatus { get; set; } = "";
    public string ConvertMessage { get; set; } = "";

    // ★ 2026-09-26 双产物链新增：前端需要区分「预览就绪」与「提取就绪」两种状态
    /// <summary>预览 PDF 产物路径（PDF/图片透传时 == StoragePath）</summary>
    public string PreviewPdfPath { get; set; } = "";
    /// <summary>提取用 Markdown 产物路径</summary>
    public string MarkdownPath { get; set; } = "";
    /// <summary>Markdown 转换状态：none/pending/converting/completed/failed/unsupported</summary>
    public string MarkdownStatus { get; set; } = "";
    /// <summary>Markdown 失败原因 / OCR 能力边界提示</summary>
    public string MarkdownMessage { get; set; } = "";

    public string UploadStatus { get; set; } = "";
    public long? FileSize { get; set; }
    public string MimeType { get; set; } = "";
    public string RuleStatus { get; set; } = "none";
    public int ExtractFieldCount { get; set; }
    public int TableDefCount { get; set; }
}

/// <summary>
/// ★ 「标准文档填写规则」页左树节点（2026-10-04 新增）。
///
/// <para>形状刻意对齐 <c>YzhTreeTableLayout</c> 的树契约（<c>Code</c> / <c>Name</c> /
/// <c>Children</c> / <c>IsLeaf</c> / <c>Extra</c>），⛔ 与资料清单页内部的
/// <c>{id,label,type,children}</c> camelCase 形状<b>不是同一个东西</b> ——
/// 那个是 <c>CertBizTree</c> 的私有格式，只有该组件消费。</para>
///
/// <para><b>载荷 PascalCase</b>（项目铁律）：前端逐字读 <c>node.Code</c> / <c>node.Extra.kind</c>，
/// 写成 <c>node.code</c> 会渲染成空且不报错。</para>
/// </summary>
public class TemplateDirectoryNode
{
    /// <summary>业务编码：机构/标准/阶段节点 = 组织树 id；文件夹 = folder.Code；文件 = standard_directory_file.Code</summary>
    public string Code { get; set; } = "";

    /// <summary>显示名：文件夹 = FolderName；文件 = FileName；其余 = 组织树 label</summary>
    public string Name { get; set; } = "";

    /// <summary>是否叶子（由 Children 是否为空推出）</summary>
    public bool IsLeaf { get; set; }

    /// <summary>
    /// 节点附加信息。唯一判据键 = <c>kind</c>：
    /// <c>org</c> / <c>standard</c> / <c>stage</c> / <c>folder</c> / <c>file</c>。
    /// 只有 <c>file</c> 叶子带 <c>standardFileCode</c> / <c>hasTemplate</c> / <c>templateCode</c> /
    /// <c>docCategory</c> / <c>scanStatus</c> / <c>publishStatus</c> / <c>anchorCount</c>。
    /// </summary>
    public Dictionary<string, object?> Extra { get; set; } = new();

    public List<TemplateDirectoryNode> Children { get; set; } = new();
}

#endregion

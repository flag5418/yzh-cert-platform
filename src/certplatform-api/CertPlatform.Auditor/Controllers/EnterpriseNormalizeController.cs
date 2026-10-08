using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using YZH.Core.Api.Controllers;
using YZH.Core.Api.Services;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.DataBase.Services;
using YZH.Core.Stand.Helpers;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Config;
using YZH.Core.Stand.Models.Result;
using CertPlatform.Shared.Constants;
using CertPlatform.Shared.Entities.Cert;
using CertPlatform.Shared.Entities.Dir;
using CertPlatform.Shared.Entities.Doc;
using CertPlatform.Shared.DocExtraction;
using CertPlatform.Shared.Office;
using CertPlatform.Shared.Storage;
using CertPlatform.Auditor.Entities.Cert;
using CertPlatform.Auditor.Entities.Doc;
using CertPlatform.Auditor.Services;
using CertPlatform.Auditor.Services.Ent.Normalize;

namespace CertPlatform.Auditor.Controllers
{
    /// <summary>
    ///     企业资料规范化控制器（专家端）—— 前缀 <c>/api/Auditor/EnterpriseNormalize</c>
    ///
    ///     <para><b>★ 与 <see cref="DocumentFillController"/> 的关系（`60` M5-2 裁定）</b>：
    ///     后者是<b>能力演示</b>（2026-10-02 定位：「把『我们想做什么』用可运行的程序讲清楚」），
    ///     输入是<b>文本模板</b>、走文本引擎、<b>不落库</b>；
    ///     本控制器是<b>正式入口</b>，输入是<b>标准域文件</b>、走 Office 写入器、
    ///     <b>产物落 MinIO + 账本落库</b>。两者<b>不重复</b>，⛔ 不要合并。</para>
    ///
    ///     <para><b>★ 端点分组（2026-10-07 扩容）</b>：</para>
    ///     <list type="bullet">
    ///       <item><term>范围（只读）</term><description><c>tree</c> 五级范围展开（标准 › 文件夹 › 文件）· <c>list</c> 扁平清单</description></item>
    ///       <item><term>计划与执行</term><description><c>plan</c> 干跑（纯读）· <c>run</c> 入队（走 <c>QueueManager</c>）</description></item>
    ///       <item><term>单文件同步</term><description><c>fill-one</c>（试跑 / 单文件测试入口）</description></item>
    ///     </list>
    ///
    ///     <para><b>★ 范围算法只有一份</b>：<see cref="BuildScopeAsync"/> 是「可规范化范围」的
    ///     唯一实现，<c>tree</c> / <c>list</c> / <c>plan</c> / <c>run</c> 全部复用 ——
    ///     ⛔ 各处自己写会出现「预览说有 3 个、真跑只跑了 2 个」且无人发现。</para>
    ///
    ///     <para><b>⚠️ 仍是纵向切片</b>：`54` §5.2 的 22 个端点里，锁定 / 账本读取 / 固定文档 /
    ///     导出 / 单文件预览仍未实现（属 P2）。队列执行器（<see cref="EnterpriseNormalizeExecutor"/>）
    ///     与编排器均已就绪，本控制器已把「范围 → 队列」这一环接通。</para>
    /// </summary>
    [ApiController]
    [Route("api/Auditor/[controller]")]
    public class EnterpriseNormalizeController : YzhControllerBase<DocFillValue>
    {
        private readonly DocumentFillOrchestrator _orchestrator;
        private readonly WorkspaceContextService _workspace;
        private readonly QueueManager _queueManager;
        private readonly IDbOrm _db;
        private readonly IOcrProvider _ocrProvider;
        private readonly ILogger<EnterpriseNormalizeController> _logger;

        public EnterpriseNormalizeController(
            EntityService<DocFillValue> entityService,
            IUserContext userContext,
            DocumentFillOrchestrator orchestrator,
            WorkspaceContextService workspace,
            QueueManager queueManager,
            IDbOrm db,
            IOcrProvider ocrProvider,
            ILogger<EnterpriseNormalizeController> logger)
            : base(entityService, userContext)
        {
            _orchestrator = orchestrator;
            _workspace = workspace;
            _queueManager = queueManager;
            _db = db;
            _ocrProvider = ocrProvider;
            _logger = logger;
        }

        protected override bool StrictConfigLoad => false;

        protected override EntityConfig LoadConfig() => EntityConfigHelper.GetConfig<DocFillValue>();

        // ════════════════════════════════════════════════════════════════════
        //  零、范围算法（唯一实现）—— 所有端点的共同底座
        // ════════════════════════════════════════════════════════════════════

        /// <summary>「可规范化范围」一次算好的结果集</summary>
        private sealed class NormalizeScope
        {
            /// <summary>范围内<b>已发布</b>的模板（驱动源）</summary>
            public List<DocTemplate> Published { get; init; } = new();

            /// <summary>
            ///     ★ 同一筛选范围内的<b>全部</b>模板（<b>含未发布</b>）。
            ///
            ///     <para><b>为什么还要留一份</b>：干跑要区分两种「跳过」——
            ///     ① 「<b>根本没配规则</b>」② 「<b>配了但没发布</b>」。两者<b>处置人不同</b>
            ///     （前者要实施人员去配、后者只要点一下发布），合成一句
            ///     「尚未配置填写规则（或模板未发布）」等于让用户自己去猜。</para>
            /// </summary>
            public List<DocTemplate> ScopedAll { get; init; } = new();

            /// <summary>标准域行（<c>EnterpriseCode = YZH-STD-ENT</c>），按 <c>Code</c> 索引</summary>
            public Dictionary<string, StandardDirectoryFile> StdMap { get; init; } =
                new(StringComparer.Ordinal);

            /// <summary>企业侧实例行，按 <c>StandardFileCode</c> 索引（同键取版本号最大者）</summary>
            public Dictionary<string, StandardDirectoryFile> InstMap { get; init; } =
                new(StringComparer.Ordinal);
        }

        /// <summary>
        ///     ★ <b>算「可规范化范围」</b> —— 驱动源 = <c>cert_doc_template</c>（INNER JOIN 标准域行）。
        ///
        ///     <para>★ 口径（`60` §六之补三，用户裁决）：<b>以「配了填写规则的空白文档」为准</b>，
        ///     ⛔ 不是全部标准文件。门槛 = <c>PublishStatus='published'</c>。</para>
        ///
        ///     <para><b>⛔ 纯读、零副作用</b> —— 不写库 / 不产文件 / 不调 LLM / 不入队。
        ///     这是干跑（<c>plan</c>）能当「承诺」用的前提。</para>
        /// </summary>
        /// <returns>成功时 <c>Error == null</c>；前置未就绪时 <c>Error</c> 非空（业务拒绝）。</returns>
        private async Task<(string? Error, NormalizeScope? Scope)> BuildScopeAsync(
            string enterpriseCode, string? standardCode, string? stageCode)
        {
            // ⓪ ★ 前置门（2026-10-06 用户裁决）：**全库**有没有「已发布」的空白模板？
            //
            //   ⛔ 必须**不受当前筛选影响**地判断 —— 否则「筛选无结果」与「功能前置未就绪」
            //      会被混成同一件事。两者的**正确信封不同**：
            //        · 全库为 0  ⇒ **业务拒绝** ⇒ `success=false` + `err` 非空（这个功能根本无从执行）
            //        · 筛选为 0  ⇒ **正常空集** ⇒ `success=true` + `items:[]`（只是这个范围里没有）
            //
            //   ★ 用户原话（2026-10-06）：「明明是错误，又返回成功，**success flag 就是反映
            //     后端到底是否执行成功的**」。
            //   规范依据：`docs/10-YZH架构/22-接口返回规范-V1.md` §B01 ——
            //     「`return Ok(ApiResponse.Ok(result))` **无条件包成功**」= **假成功**，明令禁止；
            //     且 §B02 明确「用错误文本表达失败、`success` 仍为 true」属同类违规。
            //   ⛔ 不要把「解释性文案」塞进 `data`（那是把错误藏在成功里，前端只能靠猜）。
            var allTemplates = (await _db.GetListAsync<DocTemplate>(x => x.IsValid == 1)).Data
                               ?? new List<DocTemplate>();

            if (!allTemplates.Any(t =>
                    string.Equals(t.PublishStatus, "published", StringComparison.OrdinalIgnoreCase)))
            {
                return ("没有已发布的空白模板：规范化范围只包含「配了填写规则且已发布」的空白文档，"
                        + "当前全库为 0 份。请先到后台「标准文档标准化」页发布至少 1 份空白文档。", null);
            }

            // ① 驱动源：**当前筛选范围内**的已发布模板（门槛 = published，60 §六之补三）
            var scopedAll = allTemplates
                .Where(t => string.IsNullOrEmpty(standardCode) || t.StandardCode == standardCode)
                .Where(t => string.IsNullOrEmpty(stageCode) || t.StageCode == stageCode)
                .ToList();

            var published = scopedAll
                .Where(t => string.Equals(t.PublishStatus, "published", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (published.Count == 0)
                return (null, new NormalizeScope { ScopedAll = scopedAll });

            // ② 标准域行（拿文件名 / 文件夹路径 / 配置）
            var stdFileCodes = published.Select(t => t.StandardFileCode).Distinct().ToList();
            var stdFiles = (await _db.GetListAsync<StandardDirectoryFile>(x =>
                stdFileCodes.Contains(x.Code) && x.IsValid == 1)).Data ?? new List<StandardDirectoryFile>();

            // ③ 企业侧实例行（状态 + 产物）
            var instances = (await _db.GetListAsync<StandardDirectoryFile>(x =>
                x.EnterpriseCode == enterpriseCode &&
                stdFileCodes.Contains(x.StandardFileCode!) &&
                x.IsValid == 1)).Data ?? new List<StandardDirectoryFile>();

            return (null, new NormalizeScope
            {
                Published = published,
                ScopedAll = scopedAll,
                StdMap = stdFiles
                    .GroupBy(f => f.Code!, StringComparer.Ordinal)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal),
                InstMap = instances
                    .GroupBy(i => i.StandardFileCode ?? string.Empty, StringComparer.Ordinal)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(i => i.VersionNumber).First(),
                        StringComparer.Ordinal),
            });
        }

        /// <summary>
        ///     ★ <b>把范围算成「逐文件计划」</b> —— 干跑与入队共用的唯一判定。
        ///
        ///     <para>五态判定（`54` §3.3 计数分组 · `55` §2 分流）：</para>
        ///     <list type="number">
        ///       <item><c>skip_no_template</c> —— 该标准文件没有已发布的模板（后台还没配规则 / 未发布）</item>
        ///       <item><c>skip_locked</c> —— 企业侧实例已锁定（26 号 S-6「锁定优先」）</item>
        ///       <item><c>skip_no_anchor</c> —— 模板锚点数为 0（纯复制，无填充语义）</item>
        ///       <item><c>regenerate</c> —— 企业侧已有产物，本次是覆盖重生成</item>
        ///       <item><c>fill</c> —— 首次规范化</item>
        ///     </list>
        ///
        ///     <para><b>★ 顺序即优先级</b>：先判「有没有模板」再判「锁没锁」——
        ///     没模板时锁不锁都无从执行，报「已锁定」会误导用户去解锁。</para>
        /// </summary>
        private async Task<NormalizePlanResult> BuildPlanAsync(
            string enterpriseCode, string? stageCode, NormalizeScope scope, List<string> requestedCodes)
        {
            var result = new NormalizePlanResult();

            // 去重 + 去空（前端多选可能重复传）
            var codes = (requestedCodes ?? new List<string>())
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            // ① 模板索引（同标准文件可能有多份模板，取最近更新的一份 —— 与 BuildScopeAsync 同口径）
            var templateByFile = scope.Published
                .GroupBy(t => t.StandardFileCode, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

            // ② 锚点数（⛔ 必须查库 —— 0 锚点是「跳过」的判据，不是 0 就一定要跑）
            var templateCodes = codes
                .Select(c => templateByFile.TryGetValue(c, out var t) ? t.Code : null)
                .Where(c => !string.IsNullOrEmpty(c))
                .Select(c => c!)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var anchorCount = new Dictionary<string, int>(StringComparer.Ordinal);
            if (templateCodes.Count > 0)
            {
                var anchors = (await _db.GetListAsync<DocTemplateAnchor>(x =>
                    templateCodes.Contains(x.TemplateCode) && x.IsValid == 1)).Data
                    ?? new List<DocTemplateAnchor>();
                anchorCount = anchors
                    .GroupBy(a => a.TemplateCode, StringComparer.Ordinal)
                    .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
            }

            // ③ ★ 文件名兜底表 —— 被跳过的文件不在 scope.StdMap 里（那只收「已发布模板挂的行」），
            //    不兜底就会把裸 Code 当文件名显示给用户（实测过：`7e82dc36…`）。
            //    ⚠️ 按 `Code` 查（⛔ 不按 EnterpriseCode 过滤）—— 前端理论上只会传标准域行，
            //       但传错成企业侧实例行时也要能显示出名字，⛔ 不要变成静默的裸 Code。
            var nameMap = new Dictionary<string, string>(StringComparer.Ordinal);
            if (codes.Count > 0)
            {
                var named = (await _db.GetListAsync<StandardDirectoryFile>(x =>
                    codes.Contains(x.Code) && x.IsValid == 1)).Data ?? new List<StandardDirectoryFile>();
                nameMap = named
                    .GroupBy(r => r.Code!, StringComparer.Ordinal)
                    .ToDictionary(g => g.Key,
                        g => (g.FirstOrDefault(r => string.Equals(r.EnterpriseCode,
                                  YzhVirtualEnterprise.Code, StringComparison.Ordinal)) ?? g.First()).FileName,
                        StringComparer.Ordinal);
            }

            // ④ 「配了但未发布」的模板索引 —— 用于把「没配规则」与「配了没发布」分开报
            var unpublishedByFile = scope.ScopedAll
                .Where(t => !string.Equals(t.PublishStatus, "published", StringComparison.OrdinalIgnoreCase))
                .GroupBy(t => t.StandardFileCode, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

            foreach (var code in codes)
            {
                scope.StdMap.TryGetValue(code, out var std);
                scope.InstMap.TryGetValue(code, out var inst);

                // 文件名三级兜底：范围表 → 全表 → Code（⛔ 最后一档只是防御，不该出现在真实数据里）
                var fileName = std?.FileName
                               ?? (nameMap.TryGetValue(code, out var nm) ? nm : null)
                               ?? code;

                if (!templateByFile.TryGetValue(code, out var tpl))
                {
                    result.SkipNoTemplate++;

                    var reason = unpublishedByFile.TryGetValue(code, out var draft)
                        ? $"该文件的填写规则模板尚未发布（当前状态：{draft.PublishStatus}），发布后才会纳入规范化范围"
                        : "该文件尚未配置填写规则，不在规范化范围内";

                    result.Items.Add(new NormalizePlanItem
                    {
                        StandardFileCode = code,
                        FileName = fileName,
                        Action = "skip_no_template",
                        Reason = reason,
                        IsLocked = inst?.IsLocked ?? false,
                    });
                    continue;
                }

                var locked = inst?.IsLocked ?? false;
                var hasOutput = !string.IsNullOrEmpty(inst?.StoragePath);

                if (locked)
                {
                    result.SkipLocked++;
                    result.Items.Add(new NormalizePlanItem
                    {
                        StandardFileCode = code,
                        FileName = fileName,
                        Action = "skip_locked",
                        Reason = "该文件在企业侧已锁定，规范化不会覆盖已锁定的产物（如需重跑请先解锁）",
                        AnchorCount = anchorCount.TryGetValue(tpl.Code, out var ac0) ? ac0 : 0,
                        IsLocked = true,
                        HasOutput = hasOutput,
                    });
                    continue;
                }

                var anchors0 = anchorCount.TryGetValue(tpl.Code, out var ac1) ? ac1 : 0;
                if (anchors0 == 0)
                {
                    result.SkipNoAnchor++;
                    result.Items.Add(new NormalizePlanItem
                    {
                        StandardFileCode = code,
                        FileName = fileName,
                        Action = "skip_no_anchor",
                        Reason = "模板没有任何锚点，无需填充（纯复制无意义，已跳过）",
                        AnchorCount = 0,
                        HasOutput = hasOutput,
                    });
                    continue;
                }

                if (hasOutput)
                {
                    result.WillRegenerate++;
                    result.Items.Add(new NormalizePlanItem
                    {
                        StandardFileCode = code,
                        FileName = fileName,
                        Action = "regenerate",
                        Reason = $"将重新生成（覆盖企业侧现有产物）；模板锚点 {anchors0} 个",
                        AnchorCount = anchors0,
                        HasOutput = true,
                    });
                }
                else
                {
                    result.WillFill++;
                    result.Items.Add(new NormalizePlanItem
                    {
                        StandardFileCode = code,
                        FileName = fileName,
                        Action = "fill",
                        Reason = $"将首次生成；模板锚点 {anchors0} 个",
                        AnchorCount = anchors0,
                    });
                }
            }

            result.Total = result.Items.Count;
            result.Queued = result.WillFill + result.WillRegenerate;

            // ★ §6 缺参预检（2026-10-07）：按「将要入队的标准」算缺失的必填全局参数。
            //   纯读、零副作用；结果挂到 result.ParamGaps / MissingRequired，
            //   供 plan 展示、供 run 拦截。
            var queuedCodes = result.Items
                .Where(i => i.Action == "fill" || i.Action == "regenerate")
                .Select(i => i.StandardFileCode)
                .ToList();
            var involvedStandards = queuedCodes
                .Select(c => scope.StdMap.TryGetValue(c, out var sf) ? sf.StandardCode : "")
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            var orgCodeForGaps = scope.Published.FirstOrDefault()?.OrgCode ?? "";
            if (involvedStandards.Count > 0)
            {
                result.ParamGaps = await BuildParamGapsAsync(
                    orgCodeForGaps, enterpriseCode, stageCode ?? "", involvedStandards);
                result.MissingRequired = result.ParamGaps.Sum(g => g.Items.Count);
            }

            // ★ §9.5 视觉可达预检（2026-10-07）：入队文件若含「图片 / PDF」⇒ 需视觉模型 OCR 才能出 Markdown。
            //   算出 NeedsVision（纯读，供前端预览可见 + run 拦截依据）。
            result.NeedsVision = queuedCodes
                .Where(c => scope.StdMap.TryGetValue(c, out var vstd)
                    && UploadFilePolicy.IsImageOrPdf(vstd.FileType))
                .Any();

            return result;
        }

        /// <summary>
        /// ★ §6 缺参预检：按「入队标准」算「缺失的必填全局参数」（分标准分组，2026-10-07）。
        /// <para><b>口径</b>：对每个涉及标准 S，取「该机构 × 阶段（通配）× (S 或不限标准)」下
        /// <c>IsRequired=true</c> 的参数定义全集，减去该企业「已填且非空」的参数值 ⇒ 缺失集合。
        /// 通配（<c>StandardCode=''</c>/<c>StageCode=''</c>）定义与值都参与，与
        /// <c>FillParamValueProvider.FindDefAsync</c> 的特异性口径一致。</para>
        /// <para>⛔ 纯读零副作用（2 次 DB 查询：定义一次 + 值一次），不写库、不调 LLM。</para>
        /// </summary>
        private async Task<List<NormalizeParamGapGroup>> BuildParamGapsAsync(
            string orgCode, string enterpriseCode, string stageCode,
            List<string> standards)
        {
            var result = new List<NormalizeParamGapGroup>();
            if (string.IsNullOrEmpty(orgCode) || standards.Count == 0) return result;

            // ★ 标准名称（让缺参拦截文案可读）：ISOStandard.StandardCode 为业务键
            var stdNames = (await _db.GetListAsync<ISOStandard>(s =>
                standards.Contains(s.StandardCode) && s.IsValid == 1)).Data
                ?? new List<ISOStandard>();
            var stdNameMap = stdNames
                .ToDictionary(s => s.StandardCode, s => s.StandardName, StringComparer.Ordinal);

            // ① 必填参数定义：该机构 × (阶段==stage 或不限) × (标准∈涉及 或 不限)
            var defs = (await _db.GetListAsync<FillParamDef>(d =>
                d.OrgCode == orgCode
                && d.IsRequired
                && d.IsValid == 1
                && (d.StageCode == "" || d.StageCode == stageCode)
                && (d.StandardCode == "" || standards.Contains(d.StandardCode)))).Data
                ?? new List<FillParamDef>();
            if (defs.Count == 0) return result;

            // ② 企业已填的有效值：该企业 × (阶段==stage 或 不限) × (标准∈涉及 或 不限)
            var filled = (await _db.GetListAsync<FillParamValue>(v =>
                v.EnterpriseCode == enterpriseCode
                && v.IsValid == 1
                && (v.StageCode == "" || v.StageCode == stageCode)
                && (v.StandardCode == "" || standards.Contains(v.StandardCode)))).Data
                ?? new List<FillParamValue>();
            var filledCodes = filled
                .Where(v => !string.IsNullOrWhiteSpace(v.ParamValue))
                .GroupBy(v => v.StandardCode, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Select(v => v.ParamCode)
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .ToHashSet(StringComparer.Ordinal));

            foreach (var std in standards.Distinct(StringComparer.Ordinal))
            {
                // 该标准适用的必填参数 = def 的 StandardCode ∈ {std, ''}
                var required = defs
                    .Where(d => d.StandardCode == "" || d.StandardCode == std)
                    .Select(d => d.ParamCode)
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                if (required.Count == 0) continue;

                // 已填 = 该标准的通配值 + 该标准专属值（非空）
                var filledForStd = new HashSet<string>(StringComparer.Ordinal);
                if (filledCodes.TryGetValue("", out var wc)) filledForStd.UnionWith(wc);
                if (filledCodes.TryGetValue(std, out var sc)) filledForStd.UnionWith(sc);

                var missing = required.Except(filledForStd, StringComparer.Ordinal).ToList();
                if (missing.Count == 0) continue;

                result.Add(new NormalizeParamGapGroup
                {
                    StandardCode = std,
                    // ★ 查不到名称时回退标准 Code（⛔ 不静默吞）
                    StandardName = stdNameMap.TryGetValue(std, out var nm) ? nm : std,
                    Items = missing
                        .Select(pc => defs.First(d => d.ParamCode == pc && (d.StandardCode == "" || d.StandardCode == std)))
                        .Select(d => new NormalizeParamGapItem
                        {
                            ParamCode = d.ParamCode,
                            ParamName = d.ParamName,
                            GroupName = d.GroupName ?? string.Empty,
                            ValueType = d.ValueType,
                            IsRequired = true,
                        })
                        .ToList(),
                });
            }
            return result;
        }

        // ════════════════════════════════════════════════════════════════════
        //  一、单文件同步执行（试跑 / 单文件测试入口）
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        ///     ★ <b>同步规范化单个标准文件</b> —— 直接调编排器七步，返回产物路径 + 完成率 + 账本行数。
        ///
        ///     <para>用途：① 用户「逐个测试」的入口；② 后台「配了规则能不能用」的验证口
        ///     （`60` §一 因果链第 4 项）；③ 未来「试填预览」的只读变体（`54` §5.2 端点 22）。</para>
        ///
        ///     <para>⚠️ <b>本端点不判范围、不入队</b> —— 一个文件一个请求。范围展开与队列化
        ///     见 <see cref="Plan"/> / <see cref="Run"/>。</para>
        /// </summary>
        [HttpPost("fill-one")]
        public async Task<IActionResult> FillOne([FromBody] FillOneRequest req, CancellationToken ct)
        {
            if (req == null
                || string.IsNullOrWhiteSpace(req.EnterpriseCode)
                || string.IsNullOrWhiteSpace(req.StandardFileCode))
            {
                return Ok(ApiResponse<object>.Fail("请先选择企业与目标标准文件"));
            }

            var scope = await _workspace.ResolveScopeAsync(UserContext.UserCode);
            if (!scope.Success || scope.Data == null)
                return Ok(ApiResponse<object>.Fail(scope.Error ?? "无法定位当前工作区"));

            // ★ 参数定义归属 = 体系认证机构 Code（⛔ 不是工作区 Code）
            req.OrgCode = scope.Data.CertBodyCode;
            req.OperatorCode ??= UserContext.UserCode;

            var result = await _orchestrator.FillOneAsync(req, ct);
            return Ok(ApiResponse<object>.Ok(result));
        }

        // ════════════════════════════════════════════════════════════════════
        //  二、规范化范围列表（只读）—— 「哪些标准文件配了填写规则」
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        ///     ★ <b>列出规范化范围（扁平）</b> —— 与 <see cref="Tree"/> 同一份范围算法，
        ///     只是不做「标准 › 文件夹」两级分组。
        ///
        ///     <para>⚠️ <b>保留原因</b>：`54` §5.2 把 <c>list</c> 定为独立端点，
        ///     且「阶段级一键全部规范化」不需要分组信息。⛔ 不要因为页面改用了
        ///     <c>tree</c> 就删掉它。</para>
        /// </summary>
        [HttpGet("list")]
        public async Task<IActionResult> List([FromQuery] string enterpriseCode, [FromQuery] string? standardCode,
            [FromQuery] string? stageCode, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(enterpriseCode))
                return Ok(ApiResponse<object>.Fail("请先选择企业"));

            var (err, scope) = await BuildScopeAsync(enterpriseCode, standardCode, stageCode);
            if (err != null)
                return Ok(ApiResponse<object>.Fail(err));

            var s = scope!;
            if (s.Published.Count == 0)
            {
                // ★ 前置门已过（全库有已发布模板），只是**当前筛选**（标准 / 阶段）下没有
                //   ⇒ 正常的空结果集，⛔ 不是错误 ⇒ 仍走 `Ok`（前端渲染成「这个范围下没有」）
                return Ok(ApiResponse<object>.Ok(new
                {
                    items = Array.Empty<object>(),
                    total = 0,
                }));
            }

            var items = s.Published.Select(t =>
            {
                s.StdMap.TryGetValue(t.StandardFileCode, out var std);
                s.InstMap.TryGetValue(t.StandardFileCode, out var inst);
                return new
                {
                    StandardFileCode = t.StandardFileCode,
                    FileName = std?.FileName ?? t.FileName,
                    FolderPath = std?.FolderPath ?? string.Empty,
                    TemplateCode = t.Code,
                    // ★ 必须回传 —— 前端「一键规范化」要拿它调 fill-one，
                    //   而编排器用它过滤画像（cert_enterprise_doc_profile.StandardCode）。
                    //   ⛔ 前端拼不出来（一行范围里没有标准信息），漏传 = 画像查空 = 静默零填充。
                    StandardCode = t.StandardCode,
                    StageCode = t.StageCode,
                    TemplateStatus = t.PublishStatus,
                    InstanceState = inst?.InstanceState ?? "none",
                    IsLocked = inst?.IsLocked ?? false,
                    FillCompletion = inst?.FillCompletion ?? 0m,
                    FillConfidence = inst?.FillConfidence ?? 0m,
                    FillAnchorCount = inst?.FillAnchorCount ?? 0,
                    FillPendingCount = inst?.FillPendingCount ?? 0,
                    OutputPath = inst?.StoragePath,
                    NormalizedTime = inst?.NormalizedTime,
                };
            }).ToList();

            return Ok(ApiResponse<object>.Ok(new { items, total = items.Count }));
        }

        // ════════════════════════════════════════════════════════════════════
        //  三、范围树（只读）—— 标准 › 文件夹 › 可规范化文件
        // ════════════════════════════════════════════════════════════════════

        /// <summary>范围树 —— 文件节点</summary>
        private sealed class FileNode
        {
            public string StandardFileCode { get; set; } = string.Empty;
            public string FileName { get; set; } = string.Empty;
            public string FolderCode { get; set; } = string.Empty;
            public string FolderPath { get; set; } = string.Empty;
            public string TemplateCode { get; set; } = string.Empty;
            public string StandardCode { get; set; } = string.Empty;
            public string StageCode { get; set; } = string.Empty;
            public string TemplateStatus { get; set; } = string.Empty;
            public string InstanceState { get; set; } = "none";
            public bool IsLocked { get; set; }
            public decimal FillCompletion { get; set; }
            public decimal FillConfidence { get; set; }
            public int FillAnchorCount { get; set; }
            public int FillPendingCount { get; set; }
            public string? OutputPath { get; set; }
            public DateTime? NormalizedTime { get; set; }

            /// <summary>最近一次填充留痕状态（<c>success</c> / <c>partial</c> / <c>failed</c>；无留痕为 null）</summary>
            public string? LastFillStatus { get; set; }

            /// <summary>最近一次填充留痕时间</summary>
            public DateTime? LastFillTime { get; set; }

            /// <summary>
            ///     ★ <b>最近一次填充的「待办明细」（含可操作归因）</b> —— ⛔ 不能只给
            ///     <see cref="FillPendingCount"/> 一个数。
            ///
            ///     <para><b>为什么必须有</b>：列表视图（本树）此前只显示「待办 1 个」，
            ///     而同一个数字背后有<b>四种</b>原因，指向四个<b>完全不同</b>的修复动作：</para>
            ///     <list type="number">
            ///       <item>模板锚点<b>没配数据源</b> → 后台「填写规则」页配</item>
            ///       <item>参数<b>未在后台定义</b> → 后台「企业资料参数」页新增</item>
            ///       <item>参数曾定义但<b>已被裁决移除</b> → ⛔ 不要新增，要<b>改配来源</b></item>
            ///       <item>参数已定义但<b>企业没填值</b> → 专家端「企业资料参数」页填</item>
            ///     </list>
            ///
            ///     <para>⛔ 不区分 = <b>指错方向</b>（`DocumentFillController:237-240` 原话：
            ///     「把人引到企业端去找一个根本不存在的参数 —— <b>指错方向比不报错更耗时</b>」）。</para>
            ///
            ///     <para>★ <b>零额外查询</b>：来源 = 最近一条 <c>cert_doc_fill_log.PendingsJson</c>，
            ///     而那一行本来就要取来填 <see cref="LastFillStatus"/>（见 <c>logMap</c>）。</para>
            ///
            ///     <para>⚠️ 同一 token 在文档里出现 N 次 ⇒ 本清单有 N 条（各带 <c>Location</c>）。</para>
            /// </summary>
            public List<OfficeFillPending> LastFillPendings { get; set; } = new();
        }

        /// <summary>
        ///     ★ <b>容错解析 <c>cert_doc_fill_log.PendingsJson</c></b> —— ⛔ <b>绝不抛异常</b>。
        ///
        ///     <para>这一列是<b>历史/人工数据</b>：可能是 <c>NULL</c>、可能是空串、
        ///     可能是半截 JSON（旧版本写入的、或手工改过的）。</para>
        ///
        ///     <para>抛异常会让<b>整棵树打不开</b> —— 用户连「看到哪些文件能规范化」都做不到，
        ///     更别说修。⇒ 解析失败一律退化为<b>空清单</b>，由页面照常显示「待办 N 个」。</para>
        ///
        ///     <para>⚠️ 反序列化必须<b>大小写不敏感</b>：写入端用
        ///     <c>JsonSerializer.Serialize(report.Pendings)</c>（默认 PascalCase），
        ///     但历史数据里可能存在 camelCase 变体。</para>
        /// </summary>
        private static List<OfficeFillPending> ParsePendings(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<OfficeFillPending>();
            try
            {
                return JsonSerializer.Deserialize<List<OfficeFillPending>>(json,
                           new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                       ?? new List<OfficeFillPending>();
            }
            catch
            {
                // ⛔ 不抛、不告警刷屏 —— 明细是「锦上添花」，不能因它拖垮整棵树
                return new List<OfficeFillPending>();
            }
        }

        /// <summary>范围树 —— 文件夹节点</summary>
        private sealed class FolderNode
        {
            public string FolderCode { get; set; } = string.Empty;
            public string FolderName { get; set; } = string.Empty;
            public string ParentCode { get; set; } = string.Empty;
            public int Depth { get; set; }
            public int SortOrder { get; set; }
            public string FullPath { get; set; } = string.Empty;

            /// <summary>该文件夹（含子文件夹）下的标准域文件总数 —— ⛔ 不是可规范化数</summary>
            public int TotalFileCount { get; set; }

            /// <summary>该文件夹（含子文件夹）下<b>可规范化</b>的文件数（= 已配规则且已发布）</summary>
            public int FillableCount { get; set; }

            /// <summary>直属本文件夹的可规范化文件（子文件夹的在 <see cref="Children"/> 里）</summary>
            public List<FileNode> Files { get; set; } = new();

            public List<FolderNode> Children { get; set; } = new();
        }

        /// <summary>范围树 —— 标准节点（= 页面上的一个 Tab）</summary>
        private sealed class StandardNode
        {
            public string StandardCode { get; set; } = string.Empty;

            /// <summary>标准编号（如 <c>iso9001</c>，年份另见 <c>VersionYear</c>）</summary>
            public string StandardNo { get; set; } = string.Empty;

            /// <summary>标准名称（如 <c>9001标准</c>）</summary>
            public string StandardName { get; set; } = string.Empty;

            public int VersionYear { get; set; }

            /// <summary>排序号（<c>cert_iso_standard.Sort</c>；⚠️ 实测两标准都是 0 ⇒ 必须补兜底排序）</summary>
            public int Sort { get; set; }

            /// <summary>该企业该阶段是否挂了本标准（<c>cert_enterprise_stage</c>）</summary>
            public bool Mounted { get; set; }

            /// <summary>本标准下可规范化文件总数</summary>
            public int FillableCount { get; set; }

            /// <summary>本标准下标准域文件总数（分母，让用户看懂「为什么只有这几个」）</summary>
            public int TotalFileCount { get; set; }

            public List<FolderNode> Folders { get; set; } = new();

            /// <summary>不在任何文件夹下的可规范化文件（<c>FolderCode = ""</c>）</summary>
            public List<FileNode> RootFiles { get; set; } = new();
        }

        /// <summary>
        ///     ★ <b>范围树</b> —— 「标准 › 文件夹 › 文件」三级展开（`54` §3.2 的后三级）。
        ///
        ///     <para><b>为什么必须有它</b>：扁平清单回答不了用户真正的问题 ——
        ///     「<b>这个阶段下，哪些标准的哪些文件夹里，有可以规范化的文件</b>」。
        ///     尤其当范围驱动源是模板（全库 168 份标准文件里只有 2 份配了规则）时，
        ///     扁平清单会让用户误判成「这个阶段只有 2 个文件」。</para>
        ///
        ///     <para><b>★ 显示口径（用户 2026-10-07 裁决）</b>：</para>
        ///     <list type="bullet">
        ///       <item><b>标准</b> —— 显示<b>企业该阶段挂载的全部标准</b>（含没有可规范化文件的），
        ///       让「食品标准还没配规则」这件事<b>看得见</b>，⛔ 不是静默消失</item>
        ///       <item><b>文件夹</b> —— 显示<b>全部</b>文件夹（结构要如实），
        ///       每个文件夹带 <c>TotalFileCount</c> / <c>FillableCount</c> 两个计数</item>
        ///       <item><b>文件</b> —— ⛔ <b>只列可规范化的</b>（未配规则的文件不显示）</item>
        ///     </list>
        ///
        ///     <para><b>⛔ 纯读</b>：不写库 / 不产文件 / 不入队。</para>
        /// </summary>
        [HttpGet("tree")]
        public async Task<IActionResult> Tree([FromQuery] string enterpriseCode, [FromQuery] string? stageCode,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(enterpriseCode))
                return Ok(ApiResponse<object>.Fail("请先选择企业"));

            var (err, scope) = await BuildScopeAsync(enterpriseCode, null, stageCode);
            if (err != null)
                return Ok(ApiResponse<object>.Fail(err));

            var s = scope!;

            // ① 企业该阶段「挂载的标准」—— cert_enterprise_stage 是「企业×阶段×标准」的唯一来源
            var mounted = (await _db.GetListAsync<CertEnterpriseStage>(x =>
                x.EnterpriseCode == enterpriseCode &&
                (string.IsNullOrEmpty(stageCode) || x.StageCode == stageCode))).Data
                ?? new List<CertEnterpriseStage>();
            var mountedCodes = mounted.Select(m => m.StandardCode).Distinct(StringComparer.Ordinal).ToList();

            // ② 有可规范化文件的标准（防御：模板挂了但企业没挂载 —— 也照显，否则「配了却看不到」）
            var templateCodes = s.Published.Select(t => t.StandardCode).Distinct(StringComparer.Ordinal).ToList();

            var allCodes = mountedCodes.Union(templateCodes, StringComparer.Ordinal).ToList();
            if (allCodes.Count == 0)
            {
                return Ok(ApiResponse<object>.Ok(new
                {
                    standards = Array.Empty<object>(),
                    totalStandards = 0,
                    totalFillable = 0,
                    totalFiles = 0,
                }));
            }

            var standards = (await _db.GetListAsync<ISOStandard>(x => allCodes.Contains(x.Code))).Data
                            ?? new List<ISOStandard>();
            var stdDict = standards
                .GroupBy(x => x.Code!, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

            var nodes = new List<StandardNode>();

            foreach (var code in allCodes)
            {
                stdDict.TryGetValue(code, out var iso);
                var node = new StandardNode
                {
                    StandardCode = code,
                    StandardNo = iso?.StandardCode ?? string.Empty,
                    StandardName = iso?.StandardName ?? code,
                    VersionYear = iso?.VersionYear ?? 0,
                    Mounted = mountedCodes.Contains(code),
                };
                node.Sort = iso?.Sort ?? 0;

                var tmpls = s.Published
                    .Where(t => string.Equals(t.StandardCode, code, StringComparison.Ordinal))
                    .ToList();

                if (tmpls.Count > 0)
                {
                    await FillStandardTreeAsync(node, tmpls, s, enterpriseCode);
                }

                nodes.Add(node);
            }

            // ★ 排序（10-07 实测：两标准 Sort 都是 0 ⇒ 必须补版本年与标准 Code，否则顺序随机）
            nodes = nodes
                .OrderByDescending(n => n.Mounted)
                .ThenBy(n => n.Sort)
                .ThenByDescending(n => n.VersionYear)
                .ThenBy(n => n.StandardCode, StringComparer.Ordinal)
                .ToList();

            return Ok(ApiResponse<object>.Ok(new
            {
                standards = nodes,
                totalStandards = nodes.Count,
                totalFillable = nodes.Sum(n => n.FillableCount),
                totalFiles = nodes.Sum(n => n.TotalFileCount),
            }));
        }

        /// <summary>把一个标准的「文件夹 + 文件」装进 <paramref name="node"/>（⛔ 只读）</summary>
        private async Task FillStandardTreeAsync(StandardNode node, List<DocTemplate> tmpls,
            NormalizeScope s, string enterpriseCode)
        {
            var fileCodes = tmpls.Select(t => t.StandardFileCode).Distinct(StringComparer.Ordinal).ToList();

            // ① 标准域行（拿 FolderCode / ConfigCode）—— ⚠️ 不在 s.StdMap 里的说明标准域行已失效
            var stdRows = fileCodes
                .Select(c => s.StdMap.TryGetValue(c, out var r) ? r : null)
                .Where(r => r != null)
                .Select(r => r!)
                .ToList();

            var configCodes = stdRows
                .Select(r => r.ConfigCode)
                .Where(c => !string.IsNullOrEmpty(c))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            // ② 全部标准域文件 —— 只用于「这个文件夹一共有几个文件」的分母
            //    ⚠️ 必须按 ConfigCode 查（⛔ 不按 StandardCode）—— 同一标准可以有多个目录配置
            var allStdFiles = configCodes.Count == 0
                ? new List<StandardDirectoryFile>()
                : (await _db.GetListAsync<StandardDirectoryFile>(x =>
                      configCodes.Contains(x.ConfigCode) &&
                      x.EnterpriseCode == YzhVirtualEnterprise.Code &&
                      x.IsValid == 1)).Data ?? new List<StandardDirectoryFile>();

            // ③ 文件夹（结构要如实 —— 全显，不做「空文件夹剪枝」）
            var folders = configCodes.Count == 0
                ? new List<StandardDirectoryFolder>()
                : (await _db.GetListAsync<StandardDirectoryFolder>(x =>
                      configCodes.Contains(x.ConfigCode!) && x.IsValid == 1)).Data
                  ?? new List<StandardDirectoryFolder>();

            // ④ 最近一次填充留痕（页面要显示「上次跑成什么样」）
            var logs = fileCodes.Count == 0
                ? new List<DocFillLog>()
                : (await _db.GetListAsync<DocFillLog>(x =>
                      x.EnterpriseCode == enterpriseCode &&
                      fileCodes.Contains(x.TemplateFileCode) &&
                      x.IsValid == 1)).Data ?? new List<DocFillLog>();

            var logMap = logs
                .GroupBy(l => l.TemplateFileCode, StringComparer.Ordinal)
                .ToDictionary(g => g.Key,
                    g => g.OrderByDescending(l => l.CreateTime).ThenByDescending(l => l.Id).First(),
                    StringComparer.Ordinal);

            // ⑤ 文件节点（⛔ 只造「可规范化」的 —— 未配规则的文件不显示）
            var fileNodes = tmpls.Select(t =>
            {
                s.StdMap.TryGetValue(t.StandardFileCode, out var std);
                s.InstMap.TryGetValue(t.StandardFileCode, out var inst);
                logMap.TryGetValue(t.StandardFileCode, out var log);

                return new FileNode
                {
                    StandardFileCode = t.StandardFileCode,
                    FileName = std?.FileName ?? t.FileName,
                    FolderCode = std?.FolderCode ?? string.Empty,
                    FolderPath = std?.FolderPath ?? string.Empty,
                    TemplateCode = t.Code,
                    StandardCode = t.StandardCode,
                    StageCode = t.StageCode,
                    TemplateStatus = t.PublishStatus,
                    InstanceState = inst?.InstanceState ?? "none",
                    IsLocked = inst?.IsLocked ?? false,
                    FillCompletion = inst?.FillCompletion ?? 0m,
                    FillConfidence = inst?.FillConfidence ?? 1.00m,
                    FillAnchorCount = inst?.FillAnchorCount ?? 0,
                    FillPendingCount = inst?.FillPendingCount ?? 0,
                    OutputPath = inst?.StoragePath,
                    NormalizedTime = inst?.NormalizedTime,
                    LastFillStatus = log?.Status,
                    LastFillTime = log?.CreateTime,
                    // ★ 明细随留痕一起带出（零额外查询）—— 见 FileNode.LastFillPendings 注释
                    LastFillPendings = ParsePendings(log?.PendingsJson),
                };
            }).ToList();

            // ⑥ 组装文件夹树
            var byCode = folders
                .GroupBy(f => f.Code!, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => new FolderNode
                {
                    FolderCode = g.Key,
                    FolderName = g.First().FolderName ?? string.Empty,
                    ParentCode = g.First().ParentCode ?? string.Empty,
                    Depth = g.First().Depth,
                    SortOrder = g.First().SortOrder,
                    FullPath = g.First().FullPath ?? string.Empty,
                }, StringComparer.Ordinal);

            var roots = new List<FolderNode>();
            foreach (var n in byCode.Values)
            {
                if (!string.IsNullOrEmpty(n.ParentCode) && byCode.TryGetValue(n.ParentCode, out var parent))
                    parent.Children.Add(n);
                else
                    roots.Add(n); // ParentCode = "" ⇒ 根；父节点不在集合里也当根（⛔ 不静默丢）
            }

            // ⑦ 计数 + 直属文件
            var totalByFolder = allStdFiles
                .GroupBy(f => f.FolderCode ?? string.Empty, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
            var fillableByFolder = fileNodes
                .GroupBy(f => f.FolderCode ?? string.Empty, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

            foreach (var n in byCode.Values)
            {
                n.Files = fileNodes
                    .Where(f => string.Equals(f.FolderCode, n.FolderCode, StringComparison.Ordinal))
                    .OrderBy(f => f.FileName, StringComparer.Ordinal)
                    .ToList();
            }

            foreach (var r in roots)
                AccumulateCounts(r, totalByFolder, fillableByFolder);

            node.Folders = roots
                .OrderBy(r => r.SortOrder)
                .ThenBy(r => r.FolderName, StringComparer.Ordinal)
                .ToList();

            // ⑧ 不在任何文件夹下的可规范化文件（FolderCode = "" 或指向已失效的文件夹）
            node.RootFiles = fileNodes
                .Where(f => string.IsNullOrEmpty(f.FolderCode) || !byCode.ContainsKey(f.FolderCode))
                .OrderBy(f => f.FileName, StringComparer.Ordinal)
                .ToList();

            node.FillableCount = fileNodes.Count;
            node.TotalFileCount = allStdFiles.Count;
        }

        /// <summary>自底向上累加「文件总数 / 可规范化数」（含子文件夹）</summary>
        private static void AccumulateCounts(FolderNode n,
            Dictionary<string, int> totalByFolder, Dictionary<string, int> fillableByFolder)
        {
            var total = totalByFolder.TryGetValue(n.FolderCode, out var tv) ? tv : 0;
            var fillable = fillableByFolder.TryGetValue(n.FolderCode, out var fv) ? fv : 0;

            foreach (var child in n.Children)
            {
                AccumulateCounts(child, totalByFolder, fillableByFolder);
                total += child.TotalFileCount;
                fillable += child.FillableCount;
            }

            n.TotalFileCount = total;
            n.FillableCount = fillable;
            n.Children = n.Children
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.FolderName, StringComparer.Ordinal)
                .ToList();
        }

        // ════════════════════════════════════════════════════════════════════
        //  四、干跑（只读）—— 「真跑会做什么」
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        ///     ★ <b>干跑（dry-run）</b> —— 回答「选中这批文件，真跑会发生什么」。
        ///
        ///     <para><b>★ 为什么它是强制前置</b>（`54` §3.3）：规范化是<b>覆盖性</b>操作
        ///     （重新生成会覆盖企业侧已有产物）。没有干跑，用户点「一键规范化」就是盲签 ——
        ///     尤其「已锁定 / 未配规则 / 无锚点」这三类会被静默跳过，跑完只看到数字对不上。</para>
        ///
        ///     <para><b>⛔ 零副作用</b>：不写库 / 不产文件 / 不调 LLM / 不入队。
        ///     与 <see cref="Run"/> <b>共用同一份算法</b>（<see cref="BuildPlanAsync"/>）
        ///     ⇒ 干跑说「3 个会跑」就是承诺。</para>
        /// </summary>
        [HttpPost("plan")]
        public async Task<IActionResult> Plan([FromBody] NormalizeScopeRequest req, CancellationToken ct)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.EnterpriseCode))
                return Ok(ApiResponse<object>.Fail("请先选择企业"));

            var (err, scope) = await BuildScopeAsync(req.EnterpriseCode, null, req.StageCode);
            if (err != null)
                return Ok(ApiResponse<object>.Fail(err));

            var plan = await BuildPlanAsync(req.EnterpriseCode, req.StageCode, scope!, req.StandardFileCodes);
            return Ok(ApiResponse<object>.Ok(plan));
        }

        // ════════════════════════════════════════════════════════════════════
        //  五、整批入队 —— 「一个范围 = 一个批次，一个文件 = 一个任务」
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        ///     ★ <b>整批入队</b> —— 把选中的文件投成一条 <c>enterprise_normalize</c> 队列。
        ///
        ///     <para><b>★ 为什么入队而不是同步循环</b>（`55` §3.1）：规范化一份文档要走
        ///     Office 写入 + LLM 取值，单份可达数十秒；同步循环必然请求超时且<b>无进度、不可取消</b>。
        ///     入队后由 <see cref="QueueManager"/> 的并发 Worker 跑，页面可查进度、可取消、可重跑。</para>
        ///
        ///     <para><b>★ 服务端重新算一遍范围（⛔ 不信任前端清单）</b>：前端页面可能已停留很久，
        ///     期间模板可能被取消发布、文件可能被锁定 ⇒ 必须按<b>当前</b>事实复核，
        ///     否则会把已失效的文件投进队列，执行器只能报一堆 <c>skip_*</c>。</para>
        ///
        ///     <para><b>★ 资源锁</b>：每文件一把 <c>cert_standard_directory_file</c> 锁
        ///     （键 = 标准域行 Code）⇒ 同一文件不会被两个队列同时写；
        ///     批次级再按 <c>ScopeKey = entnorm:{企业}:{阶段}</c> 防重复入队。</para>
        /// </summary>
        [HttpPost("run")]
        public async Task<IActionResult> Run([FromBody] NormalizeScopeRequest req, CancellationToken ct)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.EnterpriseCode))
                return Ok(ApiResponse<object>.Fail("请先选择企业"));
            if (string.IsNullOrWhiteSpace(req.StageCode))
                return Ok(ApiResponse<object>.Fail("请先选择阶段"));

            var ws = await _workspace.ResolveScopeAsync(UserContext.UserCode);
            if (!ws.Success || ws.Data == null)
                return Ok(ApiResponse<object>.Fail(ws.Error ?? "无法定位当前工作区"));

            var (err, scope) = await BuildScopeAsync(req.EnterpriseCode, null, req.StageCode);
            if (err != null)
                return Ok(ApiResponse<object>.Fail(err));

            // ① 按当前事实复核（干跑与入队同一算法）
            var plan = await BuildPlanAsync(req.EnterpriseCode, req.StageCode, scope!, req.StandardFileCodes);

            if (plan.Queued == 0)
            {
                // ★ 前置未就绪 = 业务拒绝（信封铁律：`success=false` + `err` 非空 + `message` 空）
                var why = new List<string>();
                if (plan.SkipLocked > 0) why.Add($"{plan.SkipLocked} 个已锁定");
                if (plan.SkipNoTemplate > 0) why.Add($"{plan.SkipNoTemplate} 个未配填写规则");
                if (plan.SkipNoAnchor > 0) why.Add($"{plan.SkipNoAnchor} 个模板无锚点");
                return Ok(ApiResponse<object>.Fail(
                    "没有可规范化的文件：" + (why.Count > 0 ? string.Join("、", why) : "未选中任何文件")));
            }

            // ★ §6 缺参拦截（2026-10-07）：入队的标准若存在「未填的必填全局参数」⇒ 业务拒绝。
            //   缺参会导致依赖 {{param}} 的锚点取不到值 ⇒ 文档只能 partial；不如执行前拦住、
            //   让专家先去「企业资料参数」页补齐再跑。只拦必填（IsRequired），选填缺失放行
            //   （照旧 partial + 待办归因），⛔ 不把整批卡死。
            if (plan.MissingRequired > 0)
            {
                var gapLines = plan.ParamGaps
                    .Select(g =>
                        $"「{g.StandardName}」缺 {g.Items.Count} 项：" +
                        string.Join("、", g.Items.Select(i => i.ParamName)))
                    .ToList();
                _logger.LogInformation(
                    "[EntNorm] 缺必填参数，拒绝入队：Ent={Ent}, Stage={Stage}, 缺失={N}",
                    req.EnterpriseCode, req.StageCode, plan.MissingRequired);
                return Ok(ApiResponse<object>.Fail(
                    "存在尚未填写的必填全局参数，请先到「企业资料参数」补齐后再执行：" +
                    string.Join("；", gapLines)));
            }

            // ★ §9.5 视觉可达拦截（2026-10-07）：入队文件含「图片 / PDF」⇒ 必须视觉模型可达。
            //   视觉能力是文档必要条件（⛔ 不版本管理、不可降级 partial）：
            //   视觉模型未配 / 非视觉模型（IsAvailable=false）时，图片/PDF 出不了 Markdown，
            //   执行必然 partial ⇒ 不如入队前拦住，让管理员去 /system/config 配好 ai_vision_config。
            if (plan.NeedsVision && !_ocrProvider.IsAvailable)
            {
                _logger.LogWarning(
                    "[EntNorm] 视觉模型不可达，拒绝入队（含图片/PDF）：Ent={Ent}, Stage={Stage}",
                    req.EnterpriseCode, req.StageCode);
                return Ok(ApiResponse<object>.Fail(
                    "本批文件包含图片 / PDF，需视觉模型（ai_vision_config）识别后才能规范化，"
                    + "但当前视觉模型未配置或不是视觉模型。请先到「系统参数 → 视觉模型配置」"
                    + "配好可用的视觉模型（如 qwen3-vl-flash）再执行。"));
            }

            // ② 防重复入队（同企业同阶段同时只允许一条运行中的规范化队列）
            var scopeKey = $"entnorm:{req.EnterpriseCode}:{req.StageCode}";
            var running = await _queueManager.FindRunningQueueByScopeKeyAsync(scopeKey);
            if (running != null)
            {
                return Ok(ApiResponse<object>.Fail(
                    $"该企业该阶段已有运行中的规范化队列（{running.QueueCode}），请等待完成或先取消"));
            }

            // ③ 造任务 + 资源锁（一个文件 = 一个任务 + 一把锁）
            var tasks = new List<QueueManager.TaskItem>();
            var locks = new List<QueueManager.ResourceLockItem>();
            var queuedItems = new List<NormalizePlanItem>();

            foreach (var item in plan.Items.Where(i => i.Action == "fill" || i.Action == "regenerate"))
            {
                scope!.StdMap.TryGetValue(item.StandardFileCode, out var std);
                var stdCode = std?.StandardCode ?? string.Empty;

                tasks.Add(new QueueManager.TaskItem
                {
                    TaskType = EnterpriseNormalizeExecutor.TaskTypeName,
                    TaskId = item.StandardFileCode,
                    Payload = JsonSerializer.Serialize(new EnterpriseNormalizeExecutor.NormalizeTaskPayload
                    {
                        OrgCode = ws.Data.CertBodyCode,
                        EnterpriseCode = req.EnterpriseCode,
                        StandardCode = stdCode,
                        StageCode = req.StageCode,
                        StandardFileCode = item.StandardFileCode,
                        OperatorCode = UserContext.UserCode,
                    }),
                });

                locks.Add(new QueueManager.ResourceLockItem
                {
                    ResourceTable = QueueManager.RESOURCE_FILE,
                    ResourceCode = item.StandardFileCode,
                    ResourceName = item.FileName,
                });

                queuedItems.Add(item);
            }

            var queueReq = new QueueManager.CreateQueueRequest
            {
                QueueType = EnterpriseNormalizeExecutor.TaskTypeName,
                QueueName = $"企业资料规范化 - {queuedItems.Count} 个文件",
                ScopeKey = scopeKey,
                ScopeInfoJson = JsonSerializer.Serialize(new
                {
                    EnterpriseCode = req.EnterpriseCode,
                    StageCode = req.StageCode,
                    FileCount = queuedItems.Count,
                }),
                SourceType = EnterpriseNormalizeExecutor.TaskTypeName,
                // ★★★ `yzh_queue.uk_source(SourceType, SourceId)` 是**唯一索引** ⇒ `SourceId` 必须**逐次唯一**。
                //   ⛔ 曾经只放 `req.EnterpriseCode` ⇒ **同一企业第二次入队必然撞唯一约束**
                //   （真机实测 err = 「队列主表插入失败：新增失败：数据已存在（唯一约束冲突）」），
                //   而第一次能成功 ⇒ 单次验证发现不了这个缺陷。
                //   ★ 本仓惯例（EnterpriseOriginalService / EnterpriseFileService / StandardDirectoryService）
                //     = 「业务键 @ 毫秒时间戳」。
                SourceId = $"{req.EnterpriseCode}@{DateTime.Now:yyyyMMddHHmmssfff}",
                UserName = UserContext.UserName,
                OrgCode = ws.Data.CertBodyCode,
                ResourceLocks = locks,
                Tasks = tasks,
            };

            var (ok, queueError, queueCode, _) = await _queueManager.CreateQueueAsync(queueReq);
            if (!ok)
            {
                _logger.LogError("[EntNorm] 入队失败：Ent={Ent}, Stage={Stage}, Err={Err}",
                    req.EnterpriseCode, req.StageCode, queueError);
                return Ok(ApiResponse<object>.Fail(queueError ?? "队列创建失败"));
            }

            _logger.LogInformation(
                "[EntNorm] 已入队：Queue={Queue}, Ent={Ent}, Stage={Stage}, 任务={Count}, 跳过={Skip}",
                queueCode, req.EnterpriseCode, req.StageCode, queuedItems.Count, plan.Total - plan.Queued);

            return Ok(ApiResponse<object>.Ok(new NormalizeRunResult
            {
                QueueCode = queueCode ?? string.Empty,
                Queued = queuedItems.Count,
                Skipped = plan.Total - plan.Queued,
                Items = queuedItems,
            }));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using YZH.Core.Api.Controllers;
using YZH.Core.Api.Models.Users;
using YZH.Core.Api.Services;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.DataBase.Services;
using YZH.Core.Stand.Helpers;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Config;
using YZH.Core.Stand.Models.Queue;
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
    ///     <para><b>★ 端点分组（2026-10-09 扩容）</b>：</para>
    ///     <list type="bullet">
    ///       <item><term>范围（只读）</term><description><c>tree</c> 五级范围展开（标准 › 文件夹 › 文件）· <c>list</c> 扁平清单</description></item>
    ///       <item><term>计划与执行</term><description><c>plan</c> 干跑（纯读）· <c>run</c> 入队（走 <c>QueueManager</c>）</description></item>
    ///       <item><term>批次</term><description><c>batch/{queueCode}</c> 进度轮询（<b>全页唯一轮询点</b>）· <c>cancel</c> 终止未跑完的任务</description></item>
    ///       <item><term>锁定</term><description><c>lock</c> / <c>unlock</c> 企业侧实例行的覆盖保护（含 <c>cert_doc_normalize_action</c> 留痕）</description></item>
    ///       <item><term>账本读</term><description><c>values</c> 取值账本 · <c>value/{anchorCode}</c> 单锚点详情（证据链 + 候选）· <c>actions</c> 动作时间线</description></item>
    ///       <item><term>账本写</term><description><c>value/override</c> 改值·改来源·钉住（含留痕）· <c>rewrite</c> 全部重写预告（纯读）· <c>candidates</c> 候选来源</description></item>
    ///       <item><term>单文件同步</term><description><c>fill-one</c>（试跑 / 单文件测试入口）</description></item>
    ///     </list>
    ///
    ///     <para><b>★ 范围算法只有一份</b>：<see cref="BuildScopeAsync"/> 是「可规范化范围」的
    ///     唯一实现，<c>tree</c> / <c>list</c> / <c>plan</c> / <c>run</c> / <c>lock</c> / <c>rewrite</c>
    ///     全部复用 —— ⛔ 各处自己写会出现「预览说有 3 个、真跑只跑了 2 个」且无人发现。</para>
    ///
    ///     <para><b>⚠️ 仍是纵向切片</b>：`54` §5.2 的 22 个端点里，固定文档
    ///     （<c>fixed/*</c> · <c>pick</c> / <c>unpick</c> / <c>none</c>，⛔ 契约表当前 0 行、无法验收）
    ///     与导出 / 预览（<c>package</c> / <c>download</c> / <c>export-values</c> / <c>preview</c>）
    ///     仍未实现（属 P2）。队列执行器（<see cref="EnterpriseNormalizeExecutor"/>）与编排器均已就绪，
    ///     本控制器已接通「<b>范围 → 队列 → 进度 → 取消</b>」「<b>锁定 → 干跑跳过 → 解锁</b>」
    ///     「<b>产物 → 账本 → 证据 → 时间线</b>」「<b>改值/改来源 → 账本 → 留痕</b>」四条链。</para>
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
            //   ⚠️ 用 GroupBy 再取首行 —— StandardCode **不是唯一键**（唯一约束是「名称+版本年」），
            //      直接 ToDictionary 遇到同编号多版本会抛「已添加了具有相同键的项」。
            var stdNames = (await _db.GetListAsync<ISOStandard>(s =>
                standards.Contains(s.StandardCode) && s.IsValid == 1)).Data
                ?? new List<ISOStandard>();
            var stdNameMap = stdNames
                .GroupBy(s => s.StandardCode, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

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

                var iso = stdNameMap.TryGetValue(std, out var found) ? found : null;

                result.Add(new NormalizeParamGapGroup
                {
                    StandardCode = std,
                    // ★ 查不到主数据时给「未登记标准（短码）」—— ⛔ 不回退裸 GUID（见 StandardLabel）
                    StandardName = StandardLabel(iso?.StandardName, iso?.StandardCode, std),
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
        ///     ★ <b>标准 Code 短码</b> —— 用于「主数据缺失」时的人话占位（如 <c>475da4fe</c>）。
        ///
        ///     <para>取前 8 位：既能唯一指认（本仓 Code 是 GUID 或 32 位 hex），
        ///     又不会像完整 GUID 那样把版面撑爆、把人劝退。</para>
        /// </summary>
        private static string ShortCode(string? code)
            => string.IsNullOrEmpty(code) ? "?" : (code.Length > 8 ? code[..8] : code);

        /// <summary>
        ///     ★ <b>标准显示名（后端生成文案用）</b> —— ⛔ <b>绝不把裸 GUID 当名字</b>。
        ///
        ///     <para>取值顺序：<c>StandardName</c> → <c>StandardCode</c>（业务编号，如 <c>iso9001</c>）
        ///     → 「未登记标准（短码）」。</para>
        ///
        ///     <para><b>为什么不让它回退成 GUID</b>：2026-10-09 用户报障 ——
        ///     `cert_iso_standard` 里 `475da4fe-…`（食品标准）被删、但企业阶段关联仍在，
        ///     于是页面 Tab 直接显示一串 GUID。**用户看到的是「系统坏了」，不是「数据缺了」**。
        ///     ⇒ 宁可显示「未登记标准（475da4fe）」，也不能显示 GUID。</para>
        ///
        ///     <para>⚠️ 本方法只服务**后端自己拼的文案**（缺参拦截、干跑拒绝原因）；
        ///     `tree` 端点的标准节点仍回传**原始事实**（空串 + <c>StandardRegistered</c>），
        ///     由前端决定怎么显示 —— 职责边界不要混。</para>
        /// </summary>
        private static string StandardLabel(string? name, string? no, string? code)
        {
            if (!string.IsNullOrWhiteSpace(name)) return name!;
            if (!string.IsNullOrWhiteSpace(no)) return no!;
            return $"未登记标准（{ShortCode(code)}）";
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

            /// <summary>标准名称（如 <c>9001标准</c>）—— ⚠️ 主数据缺失时为**空串**，⛔ 不是 Code</summary>
            public string StandardName { get; set; } = string.Empty;

            /// <summary>
            ///     ★ <b>本标准在 <c>cert_iso_standard</c> 里是否登记在册</b>（2026-10-09 新增）。
            ///
            ///     <para><b>为什么必须有</b>：<see cref="StandardCode"/> 的来源是
            ///     <c>cert_enterprise_stage</c>（企业阶段关联）与 <c>cert_doc_template</c>（模板），
            ///     两者都只是**引用**，不保证主数据还在。主数据被删而关联没清时，
            ///     此前代码写 <c>StandardName = iso?.StandardName ?? code</c> ⇒
            ///     <b>把裸 GUID 当标准名回传</b>，页面 Tab 上直接显示
            ///     <c>475da4fe-8f50-4bf7-bf2b-b39869d5ddf7</c>（2026-10-09 用户报障）。</para>
            ///
            ///     <para><b>现在</b>：<see cref="StandardName"/> / <see cref="StandardNo"/>
            ///     缺就是缺（空串），由本标记说明「为什么缺」，前端据此显示
            ///     「未登记标准（短码）」+ 处置提示，⛔ 不再回退成 GUID。</para>
            /// </summary>
            public bool StandardRegistered { get; set; }

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
                    // ⚠️ 主数据缺失时**留空**（⛔ 不再 `?? code` 回退成 GUID —— 见 StandardRegistered 注释）
                    StandardNo = iso?.StandardCode ?? string.Empty,
                    StandardName = iso?.StandardName ?? string.Empty,
                    StandardRegistered = iso != null,
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
                    // ★★ 时间口径修正（2026-10-09）：
                    //   `cert_doc_fill_log.CreateTime` 由 `BaseEntity` 赋 `DateTime.UtcNow`（**UTC**），
                    //   而 SqlSugar 从 DB 读回时 `Kind = Unspecified` ⇒ 序列化输出
                    //   「2026-10-07T08:25:41」（**无 Z**）⇒ 前端 `new Date()` 按**本地时间**解析
                    //   ⇒ 少 8 小时（同一件事：留痕 08:25 vs 实例行 NormalizedTime 16:25，实测差 8h）。
                    //   ⇒ 这里显式标 UTC，JSON 带 Z，前端换算正确。
                    //   ⚠️ 对照：`NormalizedTime`（实例行）由 `DateTime.Now` 写入 = **本地时间**，
                    //      Kind=Unspecified 恰好被前端当本地解析 ⇒ 本来就是对的，⛔ 不要一起改。
                    LastFillTime = log?.CreateTime is { } lt
                        ? DateTime.SpecifyKind(lt, DateTimeKind.Utc)
                        : null,
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

        // ════════════════════════════════════════════════════════════════════
        //  六、批次进度与取消（`54` §5.2 端点 5 / 6）
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        ///     ★ <b>批次进度</b>（`54` §5.2 端点 5）—— 页面轮询的<b>唯一</b>入口。
        ///
        ///     <para><b>★ 为什么必须有它</b>：<see cref="Run"/> 只回批次号、<b>不阻塞执行</b>
        ///     ⇒ 没有它，用户点完「一键规范化」就<b>只能干等</b>：不知道跑没跑、跑到哪、
        ///     失败在哪一份。页面对本端点的轮询是全页唯一的轮询点（`41-03` §1.6.1）。</para>
        ///
        ///     <para><b>⚠️ 只认本域的队列</b>：<c>yzh_queue</c> 是<b>全项目共用</b>的队列表
        ///     （实测 251 条里绝大多数是文件转换链的）。⛔ 不加 <c>QueueType</c> 校验，
        ///     本端点就成了「拿到任意批次号即可读任意模块队列」的越权读口。</para>
        ///
        ///     <para><b>★ 失败必须带明细</b>：只回 <c>Failed=2</c> 等于把「哪一份、为什么」
        ///     的排查成本转嫁给用户 ⇒ 这里带出文件名 + <c>ErrorType</c> + 重试次数。</para>
        /// </summary>
        [HttpGet("batch/{queueCode}")]
        public async Task<IActionResult> Batch([FromRoute] string queueCode, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(queueCode))
                return Ok(ApiResponse<object>.Fail("缺少批次号"));

            var queue = (await _db.GetOneAsync<YzhQueue>(q => q.QueueCode == queueCode)).Data;
            if (queue == null)
                return Ok(ApiResponse<object>.Fail("批次不存在或已被清理"));

            if (!IsOwnQueue(queue))
                return Ok(ApiResponse<object>.Fail("该批次不属于「企业资料规范化」，请到对应页面查看"));

            var tasks = (await _db.GetListAsync<YzhQueueTask>(j => j.QueueCode == queueCode)).Data
                        ?? new List<YzhQueueTask>();

            var failedTasks = tasks
                .Where(t => string.Equals(t.Status, "failed", StringComparison.OrdinalIgnoreCase))
                .ToList();

            // ★ 文件名一次性批量查（⛔ 不在循环里逐条查 —— N 条失败 = N 次往返）
            var nameMap = new Dictionary<string, string>(StringComparer.Ordinal);
            var failedCodes = failedTasks
                .Select(t => t.TaskId ?? string.Empty)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (failedCodes.Count > 0)
            {
                var rows = (await _db.GetListAsync<StandardDirectoryFile>(x =>
                    failedCodes.Contains(x.Code!))).Data ?? new List<StandardDirectoryFile>();
                nameMap = rows
                    .Where(r => !string.IsNullOrEmpty(r.Code))
                    .GroupBy(r => r.Code!, StringComparer.Ordinal)
                    .ToDictionary(g => g.Key, g => g.First().FileName ?? string.Empty, StringComparer.Ordinal);
            }

            return Ok(ApiResponse<object>.Ok(new NormalizeBatchResult
            {
                QueueCode = queue.QueueCode,
                QueueName = queue.QueueName ?? string.Empty,
                Status = queue.Status,
                IsFinished = IsTerminalStatus(queue.Status),
                Total = queue.TotalCount,
                Completed = queue.CompletedCount,
                Failed = queue.FailedCount,
                Cancelled = queue.CancelledCount,
                Processing = queue.ProcessingCount,
                Pending = queue.PendingCount,
                Progress = queue.Progress,
                StartTime = queue.StartTime,
                EndTime = queue.EndTime,
                Failures = failedTasks.Select(t =>
                {
                    var code = t.TaskId ?? string.Empty;
                    return new NormalizeBatchFailure
                    {
                        StandardFileCode = code,
                        FileName = nameMap.TryGetValue(code, out var n) ? n : string.Empty,
                        ErrorType = t.ErrorType ?? string.Empty,
                        ErrorMessage = t.ErrorMessage ?? string.Empty,
                        RetryCount = t.RetryCount,
                        CompleteTime = t.CompleteTime,
                    };
                }).ToList(),
            }));
        }

        /// <summary>
        ///     ★ <b>取消批次</b>（`54` §5.2 端点 6）—— 把该批次未跑完的任务一次性终止。
        ///
        ///     <para><b>★ 为什么必须能取消</b>：一个阶段的规范化可能入队几十份文件、跑十几分钟
        ///     （每份都要 LLM 取值 + Office 写入）。期间用户发现选错了范围，若不能取消只能<b>干等</b>；
        ///     更糟的是同企业同阶段被 <c>ScopeKey</c> 幂等门锁住 ⇒ <b>连重跑都做不了</b>。
        ///     一次误操作 = 十几分钟的完全阻塞。</para>
        ///
        ///     <para><b>⛔ 已结束的批次不当作「成功」</b>：对 <c>completed</c>/<c>failed</c>/<c>cancelled</c>
        ///     的批次再取消 ⇒ 返回<b>业务拒绝</b>（<c>success=false</c> + <c>err</c>）。
        ///     ⛔ 不是「Ok + 取消 0 个」—— 那是假成功，用户会以为自己这一下点生效了。</para>
        ///
        ///     <para><b>★ 审计留痕</b>：写一条 <c>batch_cancel</c>（`54` §4.2 七种动作之一）。
        ///     留痕失败<b>不谎报</b>：取消已生效 ⇒ 仍返回 Ok，但带 <c>AuditWarning</c> 让页面提示。</para>
        /// </summary>
        [HttpPost("cancel")]
        public async Task<IActionResult> Cancel([FromBody] NormalizeCancelRequest req, CancellationToken ct)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.QueueCode))
                return Ok(ApiResponse<object>.Fail("缺少批次号"));

            var queue = (await _db.GetOneAsync<YzhQueue>(q => q.QueueCode == req.QueueCode)).Data;
            if (queue == null)
                return Ok(ApiResponse<object>.Fail("批次不存在或已被清理"));

            if (!IsOwnQueue(queue))
                return Ok(ApiResponse<object>.Fail("该批次不属于「企业资料规范化」"));

            if (IsTerminalStatus(queue.Status))
                return Ok(ApiResponse<object>.Fail(
                    $"批次已{(string.Equals(queue.Status, "cancelled", StringComparison.OrdinalIgnoreCase) ? "取消" : "结束")}，无需再取消"));

            // ★ 先算「将终止多少」—— 取消会把 pending 与 processing 一并终止（QueueManager 的语义）
            var willCancel = queue.PendingCount + queue.ProcessingCount;
            var (entCode, stageCode) = ParseScopeKey(queue.ScopeKey);

            var (ok, err) = await _queueManager.CancelQueueAsync(req.QueueCode);
            if (!ok)
            {
                _logger.LogError("[EntNorm] 取消批次失败：Queue={Queue}, Err={Err}", req.QueueCode, err);
                return Ok(ApiResponse<object>.Fail(err ?? "取消失败"));
            }

            // ★ 审计留痕（⛔ 只追加；留痕失败不吞、也不谎报主操作失败）
            var auditWarning = await WriteActionAsync(new DocNormalizeAction
            {
                OrgCode = queue.OrgCode ?? string.Empty,
                EnterpriseCode = entCode,
                StageCode = stageCode,
                ActionType = "batch_cancel",
                ScopeType = "stage",
                ScopeCode = stageCode,
                ScopeName = Truncate(queue.QueueName, 200),
                QueueCode = queue.QueueCode,
                BeforeJson = JsonSerializer.Serialize(new
                {
                    Status = queue.Status,
                    Pending = queue.PendingCount,
                    Processing = queue.ProcessingCount,
                }),
                AfterJson = JsonSerializer.Serialize(new { Status = "cancelled", Cancelled = willCancel }),
            });

            _logger.LogInformation("[EntNorm] 已取消批次：Queue={Queue}, 终止={N}", req.QueueCode, willCancel);

            return Ok(ApiResponse<object>.Ok(new NormalizeCancelResult
            {
                CancelledCount = willCancel,
                AuditWarning = auditWarning,
            }));
        }

        // ════════════════════════════════════════════════════════════════════
        //  七、锁定 / 解锁（`54` §5.2 端点 7 / 8）
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        ///     ★ <b>批量锁定</b>（`54` §5.2 端点 7）—— 把选中的标准文件在<b>企业侧</b>打上锁定标记。
        ///
        ///     <para><b>★ 锁落在哪一行（本端点的核心设计）</b>：<c>IsLocked</c> 在
        ///     <b>企业侧实例行</b>（<c>cert_standard_directory_file</c>，<c>EnterpriseCode</c> = 真实企业）
        ///     上，⛔ 不是标准域行 —— 同一个标准文件，<b>企业 A 锁了不影响企业 B</b>。
        ///     而入参 <c>StandardFileCodes</c> 是<b>标准域行</b> Code（不含企业信息）
        ///     ⇒ 必须由调用方带 <c>EnterpriseCode</c>，否则无从定位要锁的那一行
        ///     （`41-03` §1.6 的入参表没列它，是那张表的疏漏）。</para>
        ///
        ///     <para><b>★ 实例行不存在 ⇒ 新建（<see cref="NormalizeLockResult.CreatedCount"/>）</b>：
        ///     锁定一个「还没生成过产物」的文件是合理需求（先把结论定下来），
        ///     而 <c>plan</c> 的 <c>skip_locked</c> 判定读的正是实例行 ⇒ <b>不建行就等于锁了没锁</b>。
        ///     但这毕竟是「我明明只是点了个锁」⇒ 新建了几行<b>必须报出来</b>，⛔ 不静默。</para>
        ///
        ///     <para><b>★ 幂等</b>：已锁定的文件<b>不再重复写</b>（保住原 <c>LockedTime</c> 与原留痕），
        ///     但计入 <see cref="NormalizeLockResult.LockedCount"/>（终态 = 已锁定）。
        ///     ⛔ 不搬 <c>plan</c> 的 <c>skip_locked</c> 判定 —— 锁定本就该能重复点。</para>
        ///
        ///     <para><b>★ 留痕粒度 = 一文件一行</b>（<c>cert_doc_normalize_action</c>，只追加）：
        ///     审计抽屉的时间线是按<b>文件</b>查的（<c>actions?TargetCode=…</c>），
        ///     ⛔ 若整批只写一行（<c>TargetCode</c> 留空），单个文件的抽屉里就
        ///     <b>永远看不到自己被谁锁的</b>。</para>
        /// </summary>
        [HttpPost("lock")]
        public async Task<IActionResult> Lock([FromBody] NormalizeLockRequest req, CancellationToken ct)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.EnterpriseCode))
                return Ok(ApiResponse<object>.Fail("请先选择企业"));

            var codes = (req.StandardFileCodes ?? new List<string>())
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (codes.Count == 0)
                return Ok(ApiResponse<object>.Fail("请先选择要锁定的文件"));

            var ws = await _workspace.ResolveScopeAsync(UserContext.UserCode);
            if (!ws.Success || ws.Data == null)
                return Ok(ApiResponse<object>.Fail(ws.Error ?? "无法定位当前工作区"));

            // ★ 企业存在性校验（2026-10-09 加）：本端点是**会新建实例行**的写口 ——
            //   企业 Code 写错就是往库里种孤儿行，代价远高于 run / fill-one 这类只读场景，
            //   值得这一次查询。（⛔ 别处没这道校验 ≠ 这里也不该有。）
            var enterprise = (await _db.GetOneAsync<Enterprise>(x =>
                x.Code == req.EnterpriseCode && x.IsValid == 1)).Data;
            if (enterprise == null)
                return Ok(ApiResponse<object>.Fail("企业不存在或已失效，请重新选择"));

            var (err, scope) = await BuildScopeAsync(req.EnterpriseCode, null, req.StageCode);
            if (err != null)
                return Ok(ApiResponse<object>.Fail(err));

            var s = scope!;

            // ① 目标「标准域行」：优先用范围里已有的（零额外查询）
            var stdMap = new Dictionary<string, StandardDirectoryFile>(StringComparer.Ordinal);
            foreach (var c in codes)
            {
                if (s.StdMap.TryGetValue(c, out var row)) stdMap[c] = row;
            }

            // ② 其余（模板未发布 / 不在当前筛选范围）补一次批量查询
            //    ★ 允许「先锁后发布」：用户完全可能先把结论定住，再回头去配规则。
            //    ⚠️ 判「是否可规范化」用 scope.StdMap 是否含键，⛔ 不要拿 Published 再按 StageCode 过滤
            //       —— 那会误剔（与 plan 的口径不一致）。
            var missing = codes.Where(c => !stdMap.ContainsKey(c)).ToList();
            if (missing.Count > 0)
            {
                var extra = (await _db.GetListAsync<StandardDirectoryFile>(x =>
                    missing.Contains(x.Code!) &&
                    x.EnterpriseCode == YzhVirtualEnterprise.Code &&
                    x.IsValid == 1)).Data ?? new List<StandardDirectoryFile>();
                foreach (var g in extra.GroupBy(r => r.Code!, StringComparer.Ordinal))
                    stdMap[g.Key] = g.First();
            }

            var now = DateTime.Now;
            var operatorCode = UserContext.UserCode;
            var lockedCount = 0;
            var createdCount = 0;
            var skippedCount = 0;
            var auditWarnings = new List<string>();

            foreach (var code in codes)
            {
                // ③ 找不到标准域行 ⇒ 无法定位（⛔ 不猜、不静默丢，计入 Skipped）
                if (!stdMap.TryGetValue(code, out var std))
                {
                    skippedCount++;
                    continue;
                }

                s.InstMap.TryGetValue(code, out var inst);

                // ④ 幂等：已是目标状态 ⇒ 计入终态，但 ⛔ 不重复写库、不重复留痕
                if (inst != null && inst.IsLocked)
                {
                    lockedCount++;
                    continue;
                }

                if (inst == null)
                {
                    // ⑤ 新建实例行 —— 字段口径照抄编排器 DocumentFillOrchestrator 的构造段
                    //    （⛔ 不要另立一套：同一行会被两条路径构造，字段口径必须一致）
                    var newRow = new StandardDirectoryFile
                    {
                        // ⚠️ 必须手动赋 Code：_db.InsertAsync 是 DbOrm 直插、不经过 AddCore()
                        //    ⇒ 基类「新增时自动生成 Code」不生效，而 Code 是 NOT NULL
                        Code = Guid.NewGuid().ToString(),
                        ConfigCode = std.ConfigCode,
                        EnterpriseCode = req.EnterpriseCode,
                        StandardCode = std.StandardCode,
                        StageCode = string.IsNullOrEmpty(std.StageCode) ? req.StageCode : std.StageCode,
                        StandardFileCode = code,
                        FileName = std.FileName,
                        FileType = std.FileType,
                        FullPath = string.IsNullOrEmpty(std.FolderPath)
                            ? std.FileName
                            : $"{std.FolderPath}/{std.FileName}",
                        VersionNumber = 1,
                        IsValid = 1,
                        CreateBy = operatorCode,
                        IsLocked = true,
                        LockedBy = operatorCode,
                        LockedTime = now,
                    };

                    var ins = await _db.InsertAsync(newRow);
                    if (!ins.Success)
                    {
                        _logger.LogError("[EntNorm] 锁定新建实例行失败：File={File}, Err={Err}",
                            code, ins.Error);
                        skippedCount++;
                        continue;
                    }

                    createdCount++;
                    lockedCount++;
                }
                else
                {
                    // ⑥ 列级回写（⛔ 禁止全列写回 —— 陷阱 ㉕：并发上传链会把其它列清成 NULL）
                    var upd = await _db.UpdateAsync(
                        new StandardDirectoryFile
                        {
                            Code = inst.Code,
                            IsLocked = true,
                            LockedBy = operatorCode,
                            LockedTime = now,
                            UpdateTime = now,
                            UpdateBy = operatorCode,
                        },
                        nameof(StandardDirectoryFile.IsLocked),
                        nameof(StandardDirectoryFile.LockedBy),
                        nameof(StandardDirectoryFile.LockedTime),
                        nameof(StandardDirectoryFile.UpdateTime),
                        nameof(StandardDirectoryFile.UpdateBy));
                    if (!upd.Success)
                    {
                        _logger.LogError("[EntNorm] 锁定回写失败：File={File}, Err={Err}", code, upd.Error);
                        skippedCount++;
                        continue;
                    }

                    lockedCount++;
                }

                // ⑦ 留痕（一文件一行）—— 写失败不吞、也不谎报主操作失败
                var warn = await WriteActionAsync(new DocNormalizeAction
                {
                    OrgCode = ws.Data.CertBodyCode,
                    EnterpriseCode = req.EnterpriseCode,
                    StandardCode = std.StandardCode,
                    StageCode = string.IsNullOrEmpty(std.StageCode) ? req.StageCode : std.StageCode,
                    ActionType = "lock",
                    ScopeType = "file",
                    ScopeCode = code,
                    ScopeName = Truncate(std.FileName, 200),
                    TargetCode = code,
                    BeforeJson = JsonSerializer.Serialize(new { IsLocked = false }),
                    AfterJson = JsonSerializer.Serialize(new
                    {
                        IsLocked = true,
                        LockedBy = operatorCode,
                        LockedTime = now,
                    }),
                });
                if (warn != null) auditWarnings.Add(warn);
            }

            if (lockedCount == 0)
            {
                return Ok(ApiResponse<object>.Fail(
                    "没有可锁定的文件：所选文件均不属于该企业的标准目录（或已失效）"));
            }

            _logger.LogInformation(
                "[EntNorm] 锁定：Ent={Ent}, 目标={Target}, 已锁={Locked}, 新建行={Created}, 跳过={Skip}",
                req.EnterpriseCode, codes.Count, lockedCount, createdCount, skippedCount);

            return Ok(ApiResponse<object>.Ok(new NormalizeLockResult
            {
                LockedCount = lockedCount,
                CreatedCount = createdCount,
                SkippedCount = skippedCount,
                AuditWarning = CollapseWarnings(auditWarnings),
            }));
        }

        /// <summary>
        ///     ★ <b>批量解锁</b>（`54` §5.2 端点 8）—— 放开企业侧实例行的锁定保护。
        ///
        ///     <para><b>⛔ <c>Reason</c> 必填</b>：解锁是「<b>放开一道保护</b>」——
        ///     放开之后，规范化就能覆盖这份文件。不写理由，事后没人能回答
        ///     「这份已经定稿的文件为什么被改过」。理由写入 <c>cert_doc_normalize_action.Reason</c>。
        ///     （前端也会拦一道，但后端这道才是权威 —— 接口可能被直接调用。）</para>
        ///
        ///     <para><b>★ 「未锁定」不是错误</b>：计入 <c>SkippedCount</c> 照常返回 <c>Ok</c> ——
        ///     用户多选了一个本来就没锁的文件，不该让整批失败（前端会把跳过数显示出来）。</para>
        ///
        ///     <para><b>★ 只改 3 列</b>：<c>IsLocked=false</c> + <c>LockedBy=null</c> + <c>LockedTime=null</c>。
        ///     ⛔ <b>不设</b>「解锁人 / 解锁时间 / 解锁理由」三个列 —— 解锁是<b>多次</b>动作，
        ///     列只能记最后一次；这些统一落 <c>cert_doc_normalize_action</c>
        ///     （26 号 A-2「解锁也留痕」）。</para>
        /// </summary>
        [HttpPost("unlock")]
        public async Task<IActionResult> Unlock([FromBody] NormalizeUnlockRequest req, CancellationToken ct)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.EnterpriseCode))
                return Ok(ApiResponse<object>.Fail("请先选择企业"));

            var codes = (req.StandardFileCodes ?? new List<string>())
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (codes.Count == 0)
                return Ok(ApiResponse<object>.Fail("请先选择要解锁的文件"));

            // ⛔ 理由必填（`41-03` §1.6 端点 8 的显式要求）
            if (string.IsNullOrWhiteSpace(req.Reason))
                return Ok(ApiResponse<object>.Fail("请填写解锁理由（解锁会放开覆盖保护，必须留痕原因）"));

            var ws = await _workspace.ResolveScopeAsync(UserContext.UserCode);
            if (!ws.Success || ws.Data == null)
                return Ok(ApiResponse<object>.Fail(ws.Error ?? "无法定位当前工作区"));

            // ① 企业侧实例行（一次批量查；⛔ 不逐文件查 —— N 个文件 = N 次往返）
            var rows = (await _db.GetListAsync<StandardDirectoryFile>(x =>
                x.EnterpriseCode == req.EnterpriseCode &&
                codes.Contains(x.StandardFileCode!) &&
                x.IsValid == 1)).Data ?? new List<StandardDirectoryFile>();

            // ⚠️ 同键可能有多版本 ⇒ 取版本号最大者（与 BuildScopeAsync 同口径）
            var instMap = rows
                .GroupBy(r => r.StandardFileCode ?? string.Empty, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.VersionNumber).First(),
                    StringComparer.Ordinal);

            var now = DateTime.Now;
            var operatorCode = UserContext.UserCode;
            var reason = Truncate(req.Reason.Trim(), 500);
            var unlockedCount = 0;
            var skippedCount = 0;
            var auditWarnings = new List<string>();

            foreach (var code in codes)
            {
                // ② 没有实例行 ⇒ 从未生成过 ⇒ 本来就没锁（⛔ 不是错误）
                if (!instMap.TryGetValue(code, out var inst))
                {
                    skippedCount++;
                    continue;
                }

                // ③ 本来就没锁 ⇒ 跳过（不写库、不留痕 —— 无状态变化）
                if (!inst.IsLocked)
                {
                    skippedCount++;
                    continue;
                }

                // ④ 列级回写（⛔ 禁止全列写回 —— 陷阱 ㉕）
                var upd = await _db.UpdateAsync(
                    new StandardDirectoryFile
                    {
                        Code = inst.Code,
                        IsLocked = false,
                        LockedBy = null,
                        LockedTime = null,
                        UpdateTime = now,
                        UpdateBy = operatorCode,
                    },
                    nameof(StandardDirectoryFile.IsLocked),
                    nameof(StandardDirectoryFile.LockedBy),
                    nameof(StandardDirectoryFile.LockedTime),
                    nameof(StandardDirectoryFile.UpdateTime),
                    nameof(StandardDirectoryFile.UpdateBy));
                if (!upd.Success)
                {
                    _logger.LogError("[EntNorm] 解锁回写失败：File={File}, Err={Err}", code, upd.Error);
                    skippedCount++;
                    continue;
                }

                unlockedCount++;

                // ⑤ 留痕（一文件一行，含 Reason）
                var warn = await WriteActionAsync(new DocNormalizeAction
                {
                    OrgCode = ws.Data.CertBodyCode,
                    EnterpriseCode = req.EnterpriseCode,
                    StandardCode = inst.StandardCode,
                    StageCode = inst.StageCode,
                    ActionType = "unlock",
                    ScopeType = "file",
                    ScopeCode = code,
                    ScopeName = Truncate(inst.FileName, 200),
                    TargetCode = code,
                    BeforeJson = JsonSerializer.Serialize(new
                    {
                        IsLocked = true,
                        LockedBy = inst.LockedBy,
                        // ⚠️ 必须显式标 Local：本列由 `DateTime.Now` 写入（**本地时间**，与同表
                        //   `NormalizedTime` 同口径），但从 MySQL 读回时 Kind=Unspecified
                        //   ⇒ 不标的话 JSON 里**没有偏移量**（`…T11:19:39`），
                        //   而 lock 端点写的是带偏移的（`…+08:00`）⇒ 同一条时间线两种格式，
                        //   看的人会以为时区不一致。标 Local 后两处都是 `+08:00`。
                        LockedTime = inst.LockedTime is { } lt
                            ? DateTime.SpecifyKind(lt, DateTimeKind.Local)
                            : (DateTime?)null,
                    }),
                    AfterJson = JsonSerializer.Serialize(new { IsLocked = false }),
                    Reason = reason,
                });
                if (warn != null) auditWarnings.Add(warn);
            }

            if (unlockedCount == 0)
            {
                return Ok(ApiResponse<object>.Fail(
                    "没有可解锁的文件：所选文件在该企业侧均不存在或本来就未锁定"));
            }

            _logger.LogInformation(
                "[EntNorm] 解锁：Ent={Ent}, 目标={Target}, 已解={Unlocked}, 跳过={Skip}, Reason={Reason}",
                req.EnterpriseCode, codes.Count, unlockedCount, skippedCount, reason);

            return Ok(ApiResponse<object>.Ok(new NormalizeUnlockResult
            {
                UnlockedCount = unlockedCount,
                SkippedCount = skippedCount,
                AuditWarning = CollapseWarnings(auditWarnings),
            }));
        }

        // ════════════════════════════════════════════════════════════════════
        //  八、取值账本读（`54` §5.2 端点 9 / 10 / 17）—— 审计抽屉三个 Tab 的数据源
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        ///     ★ <b>读取某份文件的取值账本</b>（`54` §5.2 端点 9）—— 审计抽屉 Tab1。
        ///
        ///     <para><b>★ <c>FillLogCode</c> 是可选的关键设计</b>：一次填充 = 一批账本行
        ///     （<c>cert_doc_fill_value.FillLogCode</c>）。不传时后端替用户取<b>最近一次</b>填充，
        ///     并把「取的是哪一次」回传（见 <see cref="NormalizeLedgerResult"/> 的注释）。</para>
        ///
        ///     <para><b>★ 「没有账本」不是错误</b>：从未规范化过 ⇒ 返回 <c>Ok</c> +
        ///     <c>FillLogCode</c> 为空，页面显示「尚未规范化」。⛔ 不要报红 ——
        ///     用户只是还没跑过，不是系统坏了。</para>
        ///
        ///     <para><b>⛔ 纯读</b>：不写库 / 不产文件 / 不调 LLM。</para>
        /// </summary>
        [HttpGet("values")]
        public async Task<IActionResult> Values([FromQuery] string enterpriseCode, [FromQuery] string standardFileCode,
            [FromQuery] string? fillLogCode, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(enterpriseCode) || string.IsNullOrWhiteSpace(standardFileCode))
                return Ok(ApiResponse<object>.Fail("请先选择企业与目标文件"));

            DocFillLog? log;
            if (!string.IsNullOrWhiteSpace(fillLogCode))
            {
                // ★ 显式指定了某次留痕 ⇒ 查不到是**错误**（⛔ 不能静默退化成「最近一次」——
                //   那会让用户以为看的是指定那一次，实际看的是另一次）
                log = (await _db.GetOneAsync<DocFillLog>(x =>
                    x.Code == fillLogCode && x.IsValid == 1)).Data;
                if (log == null)
                    return Ok(ApiResponse<object>.Fail("指定的填充记录不存在或已被清理"));
            }
            else
            {
                log = await FindLatestFillLogAsync(enterpriseCode, standardFileCode);
                if (log == null)
                    return Ok(ApiResponse<object>.Ok(new NormalizeLedgerResult()));
            }

            var rows = (await _db.GetListAsync<DocFillValue>(x => x.FillLogCode == log.Code)).Data
                       ?? new List<DocFillValue>();

            var items = await BuildLedgerItemsAsync(rows);

            return Ok(ApiResponse<object>.Ok(new NormalizeLedgerResult
            {
                FillLogCode = log.Code ?? string.Empty,
                FillLogStatus = log.Status,
                FillLogMessage = DescribeFillStatus(log.Status),
                FillLogTime = ToUtc(log.CreateTime),
                FillLogTotalAnchors = log.TotalAnchors,
                FillLogPendingCount = log.PendingCount,
                OutputPath = log.OutputStoragePath,
                Total = items.Count,
                Items = items,
            }));
        }

        /// <summary>
        ///     ★ <b>单个锚点的取值详情</b>（`54` §5.2 端点 10）—— 审计抽屉 Tab2「数据来源」。
        ///
        ///     <para><b>★ 比列表多出来的四样</b>：证据原文 / 证据位置 / 证据链 5 级 JSON /
        ///     AI 候选池 / 该锚点的动作时间线。它们只在「点『查』看某一个位置」时才需要，
        ///     ⛔ 不适合塞进列表（一份文档上千行，全带 = 响应爆炸）。</para>
        ///
        ///     <para><b>⚠️ 路由参数与查询参数同名</b>：<c>41-03</c> 表里写的是
        ///     <c>value/{anchorCode}</c> + 入参 <c>{FillLogCode, AnchorCode}</c>。
        ///     以<b>路由</b>为准（更符合 REST 直觉），查询串里重复给也不报错。</para>
        /// </summary>
        [HttpGet("value/{anchorCode}")]
        public async Task<IActionResult> ValueDetail([FromRoute] string anchorCode,
            [FromQuery] string enterpriseCode, [FromQuery] string standardFileCode,
            [FromQuery] string? fillLogCode, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(anchorCode))
                return Ok(ApiResponse<object>.Fail("缺少锚点标识"));
            if (string.IsNullOrWhiteSpace(enterpriseCode) || string.IsNullOrWhiteSpace(standardFileCode))
                return Ok(ApiResponse<object>.Fail("请先选择企业与目标文件"));

            DocFillLog? log;
            if (!string.IsNullOrWhiteSpace(fillLogCode))
            {
                log = (await _db.GetOneAsync<DocFillLog>(x =>
                    x.Code == fillLogCode && x.IsValid == 1)).Data;
                if (log == null)
                    return Ok(ApiResponse<object>.Fail("指定的填充记录不存在或已被清理"));
            }
            else
            {
                log = await FindLatestFillLogAsync(enterpriseCode, standardFileCode);
                if (log == null)
                    return Ok(ApiResponse<object>.Fail("该文件尚未规范化，没有可查看的取值记录"));
            }

            // ⚠️ 同一 (FillLogCode, AnchorCode) 理论上唯一；实测若出现重复（表格区域行），
            //    取 Id 最小的一条 —— ⛔ 不随机取，否则两次刷新可能看到不同的值。
            var row = (await _db.GetListAsync<DocFillValue>(x =>
                x.FillLogCode == log.Code && x.AnchorCode == anchorCode)).Data
                ?.OrderBy(r => r.Id).FirstOrDefault();

            if (row == null)
                return Ok(ApiResponse<object>.Fail("该锚点在这份产物里没有取值记录（可能规则已变更后重新填充）"));

            var (dto, anchor) = await BuildLedgerItemAsync(row);

            // ★ 该锚点的动作时间线 —— ⚠️ 必须同时按企业过滤：AnchorCode 是**模板侧**标识，
            //   不含企业信息；只按 AnchorCode 查会读到别的企业对同一锚点的操作（越权读）。
            var actions = await QueryActionsAsync(anchorCode, null, log.EnterpriseCode, ActionTimelineLimit);
            var candidates = await BuildCandidatesAsync(log.EnterpriseCode, standardFileCode, anchorCode);

            return Ok(ApiResponse<object>.Ok(new FillValueDetailDto
            {
                // ── 基础字段（逐字段拷贝）──
                Code = dto.Code,
                AnchorCode = dto.AnchorCode,
                AnchorRef = dto.AnchorRef,
                AnchorType = dto.AnchorType,
                AnchorKind = dto.AnchorKind,
                FieldCode = dto.FieldCode,
                LocationKind = dto.LocationKind,
                LocationDesc = dto.LocationDesc,
                ValueType = dto.ValueType,
                ValueDisplay = dto.ValueDisplay,
                ValueText = dto.ValueText,
                SourceKind = dto.SourceKind,
                SourceLabel = dto.SourceLabel,
                Confidence = dto.Confidence,
                ConfidenceReason = dto.ConfidenceReason,
                FillStatus = dto.FillStatus,
                WriteMode = dto.WriteMode,
                OriginalText = dto.OriginalText,
                IsOverridden = dto.IsOverridden,
                OverrideKind = dto.OverrideKind,
                OverrideReason = dto.OverrideReason,
                OverriddenBy = dto.OverriddenBy,
                OverriddenTime = dto.OverriddenTime,
                IsPinned = dto.IsPinned,
                SampleData = dto.SampleData,
                Required = dto.Required,
                IsOrphan = dto.IsOrphan,
                AnchorExists = dto.AnchorExists,
                Sort = dto.Sort,
                // ── 详情专属 ──
                EvidenceText = row.EvidenceText,
                EvidencePageHint = row.EvidencePageHint,
                SourceDetailJson = row.SourceDetailJson,
                Candidates = candidates,
                Actions = actions,
            }));
        }

        /// <summary>
        ///     ★ <b>动作留痕时间线</b>（`54` §5.2 端点 17）—— 审计抽屉 Tab3。
        ///
        ///     <para><b>★ 两种查法</b>（<c>41-03</c> 写「TargetCode 或 ScopeCode」）：
        ///     文件抽屉用 <paramref name="targetCode"/>（= 文件 Code）；
        ///     文件夹 / 阶段级用 <paramref name="scopeCode"/>。两者都给时取<b>并集</b>去重。</para>
        ///
        ///     <para><b>⚠️ 有上限，且上限必须可见</b>：本表<b>只追加、永不删除</b>
        ///     ⇒ 时间线会无限增长。截断却不说 = 让用户以为「就这些了」
        ///     ⇒ 返回 <c>Truncated</c> 让页面显式提示「还有更早的记录未显示」。</para>
        ///
        ///     <para><b>⛔ 纯读</b>。</para>
        /// </summary>
        [HttpGet("actions")]
        public async Task<IActionResult> Actions([FromQuery] string? targetCode, [FromQuery] string? scopeCode,
            [FromQuery] int? limit, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(targetCode) && string.IsNullOrWhiteSpace(scopeCode))
                return Ok(ApiResponse<object>.Fail("请提供 targetCode（文件）或 scopeCode（范围）"));

            var take = limit is > 0 and <= ActionTimelineMaxLimit ? limit.Value : ActionTimelineLimit;

            var all = await QueryActionsAsync(targetCode, scopeCode, null, take + 1);
            var truncated = all.Count > take;
            var items = truncated ? all.Take(take).ToList() : all;

            return Ok(ApiResponse<object>.Ok(new NormalizeActionListResult
            {
                Limit = take,
                Truncated = truncated,
                Total = items.Count,
                Items = items,
            }));
        }

        // ════════════════════════════════════════════════════════════════════
        //  八之二、账本写 / 重写预告 / 候选（`54` §5.2 端点 11 / 12 / 13）
        //  —— 「单元格三交互」的后两个：改值·改来源（override）· 全部重写（rewrite）
        // ════════════════════════════════════════════════════════════════════

        /// <summary>D1 取值来源六值（`22` 号 §四；<c>sibling</c> 一期不用）</summary>
        private static readonly string[] AllowedSourceKinds =
            { "global", "self", "profile", "compute", "ai", "manual" };

        /// <summary>
        ///     确定性来源 —— 可信度恒 <c>1.00</c>（`54` §4.5）。
        ///     <para>⚠️ 与 <see cref="AllowedSourceKinds"/> 的差集 = <c>ai</c>/<c>profile</c>，
        ///     这两类的分数<b>只有重新取值才知道</b> ⇒ ⛔ 不臆造，按未确认兜底 <c>0.50</c>。</para>
        /// </summary>
        private static readonly string[] DeterministicSourceKinds = { "global", "self", "compute", "manual" };

        /// <summary>来源类别 → 人话（换来源时标签没跟着换，页面会出现「标签说 A、实际取 B」的自相矛盾）</summary>
        private static string SourceKindLabel(string kind) => kind switch
        {
            "global" => "全局参数",
            "self" => "文档自身",
            "profile" => "企业资料画像",
            "compute" => "计算得出",
            "ai" => "AI 建议",
            "manual" => "人工填写",
            _ => kind,
        };

        /// <summary>
        ///     ★ <b>账本写</b> —— 改值 / 改来源 / 钉住（`54` §5.2 端点 11；`55` §17.2 / §17.3）。
        ///
        ///     <para><b>★ 一个端点覆盖三件事，靠 <c>Kind</c> 分流</b>：
        ///     <c>value</c> 只改值 · <c>source</c> 只改来源（值不变）· <c>both</c> 两者都改。
        ///     三者写的是<b>同一行账本、同一批列、同一条痕</b> ⇒ 拆成三个端点只会
        ///     把「谁先谁后」变成新问题。</para>
        ///
        ///     <para><b>⛔ 只改账本，⛔ 不重跑、⛔ 不动产物文件</b>：这是 `55` §5.4
        ///     「编辑三缓冲」的<b>落笔</b>那一半 —— 账本改了、产物还是旧的，
        ///     必须由用户显式再走一次生成才会体现在文档里。⛔ 不要在这里顺手重跑：
        ///     改一个字就重跑一次 NPOI，慢且会把审计链淹没在琐碎动作里（§6.2）。</para>
        ///
        ///     <para><b>★ 列级白名单回写</b>（陷阱 ㉕）：<c>UpdateAsync</c> 不指定列会写全列
        ///     ⇒ 会把本操作<b>不拥有</b>的列（<c>EvidenceText</c>/<c>LocationKind</c>/
        ///     <c>SourceFileCode</c>…）一起覆盖掉，与并发的填充链互相清空。</para>
        ///
        ///     <para><b>★ 留痕粒度 = 一动作一行</b>：改值/改来源写 <c>rewrite</c>，
        ///     钉住/取消钉住写 <c>pin</c>/<c>unpin</c> —— 一次调用两者都做 ⇒ <b>两行</b>。
        ///     合成一行会让审计时间线上「谁把这条钉住了」变成一个看不见的动作。</para>
        /// </summary>
        [HttpPost("value/override")]
        public async Task<IActionResult> ValueOverride([FromBody] NormalizeOverrideRequest req, CancellationToken ct)
        {
            if (req == null)
                return Ok(ApiResponse<object>.Fail("请求体不能为空"));
            if (string.IsNullOrWhiteSpace(req.FillLogCode))
                return Ok(ApiResponse<object>.Fail("缺少填充记录标识（FillLogCode）"));
            if (string.IsNullOrWhiteSpace(req.AnchorCode))
                return Ok(ApiResponse<object>.Fail("缺少锚点标识（AnchorCode）"));

            var kind = (req.Kind ?? string.Empty).Trim().ToLowerInvariant();
            if (kind.Length > 0 && kind is not ("value" or "source" or "both"))
                return Ok(ApiResponse<object>.Fail("Kind 只能是 value（只改值）/ source（只改来源）/ both（两者都改）"));

            var touchValue = kind is "value" or "both";
            var touchSource = kind is "source" or "both";

            if (!touchValue && !touchSource && !req.IsPin.HasValue)
                return Ok(ApiResponse<object>.Fail("本次没有要修改的内容：请指定 Kind（改值 / 改来源）或 IsPin（钉住 / 取消钉住）"));

            // ★ Reason 必填（`55` §17.4）：改值 / 换来源都要留「为什么」，否则事后答不出「凭什么改」
            if (string.IsNullOrWhiteSpace(req.Reason))
                return Ok(ApiResponse<object>.Fail("请填写修改理由（改值 / 换来源都必须留下为什么，审计要求）"));

            // ⚠️ 空串 ≠ 没传：空串 = 显式清空（用户就是要抹掉）；null = 没给 ⇒ 拒绝，⛔ 不猜
            if (touchValue && req.ValueText == null)
                return Ok(ApiResponse<object>.Fail("请提供 ValueText（若要清空该值请传空字符串）"));

            if (touchSource)
            {
                if (string.IsNullOrWhiteSpace(req.NewSourceKind))
                    return Ok(ApiResponse<object>.Fail("换来源时必须提供 NewSourceKind"));
                if (!AllowedSourceKinds.Contains(req.NewSourceKind.Trim().ToLowerInvariant(), StringComparer.Ordinal))
                    return Ok(ApiResponse<object>.Fail(
                        $"来源类别不合法：{req.NewSourceKind}（只能是 {string.Join(" / ", AllowedSourceKinds)}）"));
            }

            // ① 定位留痕与账本行
            var log = (await _db.GetOneAsync<DocFillLog>(x =>
                x.Code == req.FillLogCode && x.IsValid == 1)).Data;
            if (log == null)
                return Ok(ApiResponse<object>.Fail("指定的填充记录不存在或已被清理"));

            // ⚠️ 同一 (FillLogCode, AnchorCode) 理论上唯一；出现重复时取 Id 最小（⛔ 不随机取，
            //    否则两次调用可能改到不同的行）—— 与 `value/{anchorCode}` 同口径
            var row = (await _db.GetListAsync<DocFillValue>(x =>
                x.FillLogCode == log.Code && x.AnchorCode == req.AnchorCode)).Data
                ?.OrderBy(r => r.Id).FirstOrDefault();
            if (row == null)
                return Ok(ApiResponse<object>.Fail("该锚点在这份产物里没有取值记录，无法修改（可能规则已变更后重新填充过）"));

            // ② 乐观并发校验（只在校验值被传进来时做）
            if (!string.IsNullOrWhiteSpace(req.SourceKind)
                && !string.Equals(req.SourceKind.Trim(), row.SourceKind, StringComparison.OrdinalIgnoreCase))
            {
                return Ok(ApiResponse<object>.Fail(
                    $"来源已被他人改动（你看到的是「{req.SourceKind}」，库里当前是「{row.SourceKind}」），请刷新后重试"));
            }

            // ③ 算目标值（先在内存里算完，再一次性列级回写 —— ⛔ 不做多次 UPDATE）
            var now = DateTime.Now; // ★ 本地时间：与 NormalizedTime / LockedTime 同口径（BaseEntity.CreateTime 才是 UTC）
            var operatorCode = UserContext.UserCode;

            var newValueText = row.ValueText;
            var newValueDisplay = row.ValueDisplay;
            var newValueNumber = row.ValueNumber;
            var newValueDate = row.ValueDate;
            var newFillStatus = row.FillStatus;
            var newSourceKind = row.SourceKind;
            var newSourceLabel = row.SourceLabel;
            var newSourceDetail = row.SourceDetailJson;
            var newConfidence = row.Confidence;
            var newConfidenceReason = row.ConfidenceReason;

            if (touchValue)
            {
                newValueText = req.ValueText ?? string.Empty;
                newValueDisplay = Truncate(newValueText, 500);
                newValueNumber = null;
                newValueDate = null;

                if (string.IsNullOrWhiteSpace(newValueText))
                {
                    // ★ 显式清空 ⇒ `removed`（⛔ 不计入完成率分子：该锚点确实没值了）
                    newFillStatus = "removed";
                }
                else
                {
                    newFillStatus = "filled";
                    // ★ 同步落「类型列」：只改 ValueText 的话，数值/日期列还留着旧值
                    //   ⇒ 同一个锚点在两列里有两个值，谁对没人说得清
                    switch ((row.ValueType ?? "text").Trim().ToLowerInvariant())
                    {
                        case "number" when decimal.TryParse(newValueText, NumberStyles.Any,
                            CultureInfo.InvariantCulture, out var num):
                            newValueNumber = num;
                            break;
                        case "date" when DateTime.TryParse(newValueText, CultureInfo.InvariantCulture,
                            DateTimeStyles.None, out var dt):
                            newValueDate = dt;
                            break;
                    }
                }

                // 人工值 = 确定性来源 ⇒ 可信度 1.00（`54` §4.5：manual 恒 1.00）
                newConfidence = 1.00m;
                newConfidenceReason = string.Empty;
            }

            if (touchSource)
            {
                newSourceKind = req.NewSourceKind!.Trim().ToLowerInvariant();
                newSourceLabel = string.IsNullOrWhiteSpace(req.SourceLabel)
                    ? SourceKindLabel(newSourceKind)
                    : Truncate(req.SourceLabel!, 200);

                // ★ 换原始件（方式①）带新的证据链 ⇒ 原样存（⛔ 后端不解析）
                if (req.SourceDetailJson != null) newSourceDetail = req.SourceDetailJson;

                if (touchValue && !string.IsNullOrWhiteSpace(newValueText))
                {
                    // 「both」且值是人填的 ⇒ 值确定无疑，来源类别不再影响可信度
                    newConfidence = 1.00m;
                    newConfidenceReason = string.Empty;
                }
                else if (DeterministicSourceKinds.Contains(newSourceKind, StringComparer.Ordinal))
                {
                    newConfidence = 1.00m;
                    newConfidenceReason = string.Empty;
                }
                else
                {
                    // ai / profile：真实分数只有「重新取值」才知道 ⇒ ⛔ 不臆造，按未确认兜底 0.50
                    newConfidence = 0.50m;
                    newConfidenceReason = "来源已由人工改指定，尚未重新取值，可信度暂按未确认处理";
                }
            }

            var pinChanged = req.IsPin.HasValue && req.IsPin.Value != row.IsPinned;
            var newPinned = req.IsPin ?? row.IsPinned;

            var beforeJson = JsonSerializer.Serialize(new
            {
                row.ValueText,
                row.ValueDisplay,
                row.SourceKind,
                row.SourceLabel,
                row.SourceDetailJson,
                row.IsOverridden,
                row.OverrideKind,
                row.IsPinned,
            });

            // ④ 列级白名单回写
            var cols = new List<string>
            {
                nameof(DocFillValue.ValueText), nameof(DocFillValue.ValueNumber), nameof(DocFillValue.ValueDate),
                nameof(DocFillValue.ValueDisplay), nameof(DocFillValue.FillStatus),
                nameof(DocFillValue.SourceKind), nameof(DocFillValue.SourceLabel),
                nameof(DocFillValue.SourceDetailJson),
                nameof(DocFillValue.Confidence), nameof(DocFillValue.ConfidenceReason),
                nameof(DocFillValue.UpdateTime), nameof(DocFillValue.UpdateBy),
            };

            var upd = new DocFillValue
            {
                Code = row.Code,
                ValueText = newValueText,
                ValueNumber = newValueNumber,
                ValueDate = newValueDate,
                ValueDisplay = newValueDisplay,
                FillStatus = newFillStatus,
                SourceKind = newSourceKind,
                SourceLabel = newSourceLabel,
                SourceDetailJson = newSourceDetail,
                Confidence = newConfidence,
                ConfidenceReason = newConfidenceReason,
                UpdateTime = now,
                UpdateBy = operatorCode,
                // 值/来源列（仅在真的改了时才写 —— 纯钉住不该顺手把 IsOverridden 设成 1）
                IsOverridden = row.IsOverridden,
                OverrideKind = row.OverrideKind,
                OverrideReason = row.OverrideReason,
                OverriddenBy = row.OverriddenBy,
                OverriddenTime = row.OverriddenTime,
                IsPinned = newPinned,
            };

            if (touchValue || touchSource)
            {
                upd.IsOverridden = true;
                upd.OverrideKind = kind;
                upd.OverrideReason = Truncate(req.Reason, 500);
                upd.OverriddenBy = operatorCode;
                upd.OverriddenTime = now;

                cols.AddRange(new[]
                {
                    nameof(DocFillValue.IsOverridden), nameof(DocFillValue.OverrideKind),
                    nameof(DocFillValue.OverrideReason), nameof(DocFillValue.OverriddenBy),
                    nameof(DocFillValue.OverriddenTime),
                });
            }

            if (req.IsPin.HasValue) cols.Add(nameof(DocFillValue.IsPinned));

            var write = await _db.UpdateAsync(upd, cols.ToArray());
            if (!write.Success)
            {
                _logger.LogError("[EntNorm] 账本改写失败：Row={Row}, Kind={Kind}, Err={Err}",
                    row.Code, kind, write.Error);
                // ★ 主操作没生效 ⇒ 业务拒绝（信封铁律：⛔ 不谎报成功）
                return Ok(ApiResponse<object>.Fail("改写取值账本失败：" + (write.Error ?? "未知原因")));
            }

            // ⑤ 留痕（失败 ⇒ 主操作已生效，但审计链断了 ⇒ Ok + AuditWarning，⛔ 不谎报失败）
            var warnings = new List<string>();
            var fileName = (await _db.GetOneAsync<StandardDirectoryFile>(x =>
                x.Code == row.StandardFileCode && x.IsValid == 1)).Data?.FileName ?? string.Empty;

            if (touchValue || touchSource)
            {
                var afterJson = JsonSerializer.Serialize(new
                {
                    ValueText = newValueText,
                    ValueDisplay = newValueDisplay,
                    SourceKind = newSourceKind,
                    SourceLabel = newSourceLabel,
                    SourceDetailJson = newSourceDetail,
                    IsOverridden = true,
                    OverrideKind = kind,
                    IsPinned = newPinned,
                });

                var w = await WriteActionAsync(new DocNormalizeAction
                {
                    OrgCode = log.OrgCode,
                    EnterpriseCode = log.EnterpriseCode,
                    StandardCode = log.StandardCode,
                    StageCode = log.StageCode,
                    // ★ `55` §17.3：改值 / 换来源的留痕动作 = `rewrite`（字典 normalize_action 七值之一）
                    ActionType = "rewrite",
                    ScopeType = "file",
                    ScopeCode = row.StandardFileCode,
                    ScopeName = fileName,
                    TargetCode = row.StandardFileCode,
                    AnchorCode = req.AnchorCode,
                    BeforeJson = beforeJson,
                    AfterJson = afterJson,
                    Reason = Truncate(req.Reason, 500),
                    // ⚠️ 本端点不带批次（不是队列触发）⇒ QueueCode 留空，⛔ 不拿 FillLogCode 冒充
                    QueueCode = string.Empty,
                });
                if (w != null) warnings.Add(w);
            }

            if (pinChanged)
            {
                var w = await WriteActionAsync(new DocNormalizeAction
                {
                    OrgCode = log.OrgCode,
                    EnterpriseCode = log.EnterpriseCode,
                    StandardCode = log.StandardCode,
                    StageCode = log.StageCode,
                    ActionType = req.IsPin!.Value ? "pin" : "unpin",
                    ScopeType = "file",
                    ScopeCode = row.StandardFileCode,
                    ScopeName = fileName,
                    TargetCode = row.StandardFileCode,
                    AnchorCode = req.AnchorCode,
                    BeforeJson = JsonSerializer.Serialize(new { IsPinned = row.IsPinned }),
                    AfterJson = JsonSerializer.Serialize(new { IsPinned = newPinned }),
                    Reason = Truncate(req.Reason, 500),
                    QueueCode = string.Empty,
                });
                if (w != null) warnings.Add(w);
            }

            _logger.LogInformation(
                "[EntNorm] 账本改写：Row={Row}, File={File}, Anchor={Anchor}, Kind={Kind}, Pin={Pin}",
                row.Code, row.StandardFileCode, req.AnchorCode, kind, newPinned);

            return Ok(ApiResponse<object>.Ok(new NormalizeOverrideResult
            {
                Code = row.Code ?? string.Empty,
                Kind = kind,
                ValueDisplay = newValueDisplay,
                SourceKind = newSourceKind,
                SourceLabel = newSourceLabel,
                IsPinned = newPinned,
                AuditWarning = CollapseWarnings(warnings),
            }));
        }

        /// <summary>
        ///     ★ <b>「全部重写」预告</b>（`54` §5.2 端点 12；`55` §6.3 五条不覆盖规则）。
        ///
        ///     <para><b>★ 本端点<b>只预告、⛔ 不入队</b></b>（判据在契约本身：`41-03` §1.6 把出参
        ///     写成 <c>NormalizePlan</c> —— <b>没有 <c>QueueCode</c></b>；真入队的 <c>run</c>
        ///     出参是 <c>NormalizeRunResult</c>）。它回答的是 `41-02` §5.3 弹窗要显示的问题：
        ///     「<b>如果现在全部重写，会保留什么、会覆盖什么</b>」。</para>
        ///
        ///     <para><b>★ 返回的是<b>事实</b>与<b>按开关的推算</b>，⛔ 不是「已经保留」的承诺</b>：
        ///     <c>PinnedAnchorTotal</c>/<c>ManualValueTotal</c>/<c>SampleAnchorTotal</c> 是库里
        ///     现在有多少行；<c>WillKeepAnchorTotal</c> 是<b>按 <c>KeepManual</c>/<c>KeepPinned</c>
        ///     算出来会保留多少</b>。执行期真正「保留」的落实在编排器（见 <c>RewriteFiles</c> 注释）。</para>
        ///
        ///     <para><b>★ 规则④（示例数据必须清空）⛔ 不受开关影响</b>：合规铁律，
        ///     即使该锚点被钉住也清空 ⇒ <see cref="NormalizeRewriteFilePrecheck.SampleCount"/>
        ///     的行<b>一律不计入 <c>WillKeepCount</c></b>。</para>
        ///
        ///     <para><b>⛔ 纯读、零副作用</b>：不写库 / 不产文件 / 不调 LLM / 不入队。</para>
        /// </summary>
        [HttpPost("rewrite")]
        public async Task<IActionResult> Rewrite([FromBody] NormalizeRewriteRequest req, CancellationToken ct)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.EnterpriseCode))
                return Ok(ApiResponse<object>.Fail("请先选择企业"));

            var scopeType = (req.ScopeType ?? "file").Trim().ToLowerInvariant();
            if (scopeType is not ("enterprise" or "stage" or "standard" or "folder" or "file"))
                return Ok(ApiResponse<object>.Fail(
                    "ScopeType 只能是 enterprise / stage / standard / folder / file"));

            // ★ 五级范围里「阶段」「标准」这两级能直接收窄查询 ⇒ 下推到 BuildScopeAsync
            //   （⛔ 不要先查全量再在内存里筛：那会让「范围」有两个实现）
            var stageFilter = req.StageCode;
            string? stdFilter = null;
            if (scopeType == "stage" && !string.IsNullOrWhiteSpace(req.ScopeCode)) stageFilter = req.ScopeCode!;
            if (scopeType == "standard" && !string.IsNullOrWhiteSpace(req.ScopeCode)) stdFilter = req.ScopeCode!;

            var (err, scope) = await BuildScopeAsync(req.EnterpriseCode, stdFilter, stageFilter);
            if (err != null)
                return Ok(ApiResponse<object>.Fail(err));

            var codes = ResolveRewriteScope(req, scopeType, scope!);
            if (codes.Count == 0)
                return Ok(ApiResponse<object>.Fail(
                    "选定范围内没有可重写的文件：规范化范围只包含「配了填写规则且已发布」的空白文档"));

            // ① 干跑（与 `plan` 共用同一份算法 ⇒ 「预告说 3 个」就是承诺）
            var plan = await BuildPlanAsync(req.EnterpriseCode, stageFilter, scope!, codes);

            plan.RewritePrecheck = true;
            plan.KeepManual = req.KeepManual;
            plan.KeepPinned = req.KeepPinned;

            // ② 只对「将要跑」的文件做保留/覆盖预检（跳过的项没有重写语义，算了也没人看）
            foreach (var item in plan.Items.Where(i => i.Action is "fill" or "regenerate"))
            {
                var pre = await BuildRewritePrecheckAsync(req.EnterpriseCode, item.StandardFileCode,
                    item.FileName, item.Action, item.IsLocked, req.KeepManual, req.KeepPinned);

                plan.RewriteFiles.Add(pre);
                plan.PinnedAnchorTotal += pre.PinnedCount;
                plan.ManualValueTotal += pre.ManualCount;
                plan.SampleAnchorTotal += pre.SampleCount;
                plan.WillKeepAnchorTotal += pre.WillKeepCount;
                plan.WillRecomputeAnchorTotal += pre.WillRecomputeCount;
            }

            _logger.LogInformation(
                "[EntNorm] 重写预检：Ent={Ent}, Type={Type}, Code={Code}, 将跑={Run}, 保留={Keep}, 重取={Re}",
                req.EnterpriseCode, scopeType, req.ScopeCode, plan.Queued,
                plan.WillKeepAnchorTotal, plan.WillRecomputeAnchorTotal);

            return Ok(ApiResponse<object>.Ok(plan));
        }

        /// <summary>
        ///     ★ <b>候选来源</b>（`54` §5.2 端点 13）—— 改来源（`55` §17.2）的「有哪些可选」。
        ///
        ///     <para><b>★ <c>ProfileCode</c> 有两层作用</b>（这是它存在的理由）：
        ///     ① <b>窄化</b>：只返回<b>来自这份企业原始资料</b>的建议 —— 对应方式①
        ///     「从候选清单里换一个原始件」；② <b>解析</b>：画像行里带
        ///     <c>EnterpriseCode</c> 与 <c>MatchTargetStandardFileCode</c>
        ///     ⇒ 只给 <c>ProfileCode</c> 也能定位，不必再传企业与文件。</para>
        ///
        ///     <para><b>⚠️ 比 `41-03` 的 <c>{AnchorCode, ProfileCode?}</c> 多两个<b>可选</b>入参</b>：
        ///     <c>EnterpriseCode</c> / <c>StandardFileCode</c>。理由同 <c>lock</c> 端点的
        ///     <c>EnterpriseCode</c>：候选池按「企业 × 文件 × 锚点」三键存放，
        ///     只有 <c>AnchorCode</c>（模板侧标识、不含企业）时<b>无从定位</b>。
        ///     ⛔ 但这里做成<b>可选</b>而不是必填 —— 给了 <c>ProfileCode</c> 就够。</para>
        ///
        ///     <para><b>⛔ 查不到返回空数组，不是错误</b>：确定性来源的锚点本来就没有 AI 候选。</para>
        ///
        ///     <para><b>⚠️ 已知范围边界</b>：本端点目前<b>只枚举 AI 建议池</b>
        ///     （<c>cert_doc_ai_suggestion</c>）。「可用来源枚举」的另一半 ——
        ///     该锚点的 <c>SourceSpec</c> 引用了哪些全局参数 / 画像字段、各自企业是否已填 ——
        ///     需要解析 <c>SourceSpec</c> + 查 <c>cert_fill_param_value</c>，属 P2。
        ///     ⇒ 现阶段页面「改来源」的下拉只有 AI 候选，全局参数来源要走 <c>value/override</c>
        ///     直接指定 <c>NewSourceKind='global'</c>。</para>
        /// </summary>
        [HttpGet("candidates")]
        public async Task<IActionResult> Candidates([FromQuery] string anchorCode, [FromQuery] string? profileCode,
            [FromQuery] string? enterpriseCode, [FromQuery] string? standardFileCode, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(anchorCode))
                return Ok(ApiResponse<object>.Fail("缺少锚点标识（AnchorCode）"));

            var ent = enterpriseCode?.Trim() ?? string.Empty;
            var std = standardFileCode?.Trim() ?? string.Empty;
            var onlyFromOriginalFile = string.Empty;

            if (!string.IsNullOrWhiteSpace(profileCode))
            {
                var profile = (await _db.GetOneAsync<EnterpriseDocProfile>(x =>
                    x.Code == profileCode && x.IsValid == 1)).Data;
                if (profile == null)
                    return Ok(ApiResponse<object>.Fail("指定的企业资料画像不存在或已被清理"));

                if (string.IsNullOrEmpty(ent)) ent = profile.EnterpriseCode;
                if (string.IsNullOrEmpty(std)) std = profile.MatchTargetStandardFileCode;
                onlyFromOriginalFile = profile.OriginalFileCode;
            }

            if (string.IsNullOrEmpty(ent) || string.IsNullOrEmpty(std))
            {
                return Ok(ApiResponse<object>.Fail(
                    "无法确定候选池范围：请提供 ProfileCode（从画像解析企业与目标文件），"
                    + "或直接提供 EnterpriseCode 与 StandardFileCode"));
            }

            var items = await BuildCandidatesAsync(ent, std, anchorCode,
                string.IsNullOrWhiteSpace(onlyFromOriginalFile) ? null : onlyFromOriginalFile);

            return Ok(ApiResponse<object>.Ok(items));
        }

        // ────────────────────────────────────────────────────────────────────
        //  八之附：账本 / 留痕的共用构造（第 4 批 override / rewrite / candidates 复用）
        // ────────────────────────────────────────────────────────────────────

        /// <summary>时间线默认返回条数（够看清最近发生了什么，又不至于把响应撑大）</summary>
        private const int ActionTimelineLimit = 200;

        /// <summary>时间线单次上限 —— ⛔ 再大就是「把整张只追加表一次拉走」</summary>
        private const int ActionTimelineMaxLimit = 500;

        /// <summary>
        ///     取某企业某文件<b>最近一次</b>填充留痕。
        ///     <para>⚠️ 排序键必须带 <c>Id</c> 兜底：<c>CreateTime</c> 精度到秒，
        ///     同一秒内的两次填充会并列 ⇒ 只按时间排会随机取一条。</para>
        /// </summary>
        private async Task<DocFillLog?> FindLatestFillLogAsync(string enterpriseCode, string standardFileCode)
        {
            var logs = (await _db.GetListAsync<DocFillLog>(x =>
                x.EnterpriseCode == enterpriseCode &&
                x.TemplateFileCode == standardFileCode)).Data ?? new List<DocFillLog>();

            return logs.OrderByDescending(x => x.CreateTime).ThenByDescending(x => x.Id).FirstOrDefault();
        }

        /// <summary>填充留痕状态 → 人话（⛔ 不是堆栈；未知状态如实返回原值，⛔ 不吞）</summary>
        private static string DescribeFillStatus(string? status) => status switch
        {
            "success" => "已生成（无待办）",
            "partial" => "部分完成，有待办项",
            "failed" => "执行失败",
            null or "" => string.Empty,
            _ => status,
        };

        /// <summary>
        ///     ★ 把 <c>BaseEntity.CreateTime</c>（<b>UTC</b>）标成带 Z 的时间。
        ///     <para>⚠️ SqlSugar 从 MySQL 读回时 <c>Kind = Unspecified</c> ⇒ 不标的话
        ///     JSON 里没有 <c>Z</c> ⇒ 前端 <c>new Date()</c> 按<b>本地</b>解析 ⇒ 少 8 小时。
        ///     （同 <c>tree</c> 端点 <c>LastFillTime</c> 的处置；<c>NormalizedTime</c> 是本地时间，
        ///     ⛔ 不要一起标 —— 那个本来就是对的。）</para>
        /// </summary>
        private static DateTime? ToUtc(DateTime value)
            => value == default ? null : DateTime.SpecifyKind(value, DateTimeKind.Utc);

        /// <summary>批量构造账本 DTO（含锚点 JOIN —— <c>AnchorRef</c> / <c>FieldCode</c> 只在锚点表里）</summary>
        private async Task<List<FillValueDto>> BuildLedgerItemsAsync(List<DocFillValue> rows)
        {
            if (rows.Count == 0) return new List<FillValueDto>();

            var anchorMap = await LoadAnchorMapAsync(rows.Select(r => r.AnchorCode));

            return rows
                .Select(r => MapLedger(r, anchorMap.TryGetValue(r.AnchorCode ?? string.Empty, out var a) ? a : null))
                // ★ 排序用「锚点侧实时的 Sort」优先：账本里的 Sort 是写入时的快照，
                //   规则改过之后快照就过期了（实测当前数据全是 0 ⇒ 必须再补 AnchorRef 兜底，
                //   否则顺序随机、两次刷新还可能不一样）
                .OrderBy(d => d.Sort)
                .ThenBy(d => d.AnchorRef, StringComparer.Ordinal)
                .ThenBy(d => d.Code, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>单行账本 DTO（端点 10 用）</summary>
        private async Task<(FillValueDto Dto, DocTemplateAnchor? Anchor)> BuildLedgerItemAsync(DocFillValue row)
        {
            var anchorMap = await LoadAnchorMapAsync(new[] { row.AnchorCode });
            anchorMap.TryGetValue(row.AnchorCode ?? string.Empty, out var anchor);
            return (MapLedger(row, anchor), anchor);
        }

        /// <summary>
        ///     ★ 批量取锚点行（一次查询，⛔ 不在循环里逐条查）。
        ///     <para>⚠️ <b>必须带 <c>includeDisabled: true</c></b>：换版 / 删规则后锚点会软删，
        ///     这时我们仍要拿到 <c>AnchorRef</c> 才能让用户看懂「哪条规则失效了」——
        ///     用默认过滤会查不到 ⇒ 页面只剩一个裸 Code。</para>
        /// </summary>
        private async Task<Dictionary<string, DocTemplateAnchor>> LoadAnchorMapAsync(IEnumerable<string?> anchorCodes)
        {
            var codes = anchorCodes
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c!)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (codes.Count == 0) return new Dictionary<string, DocTemplateAnchor>(StringComparer.Ordinal);

            var anchors = (await _db.GetListAsync<DocTemplateAnchor>(x => codes.Contains(x.Code!),
                includeDisabled: true)).Data ?? new List<DocTemplateAnchor>();

            return anchors
                .Where(a => !string.IsNullOrEmpty(a.Code))
                .GroupBy(a => a.Code!, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        }

        /// <summary>账本行 + 锚点行 → DTO（⛔ 纯映射，无副作用）</summary>
        private static FillValueDto MapLedger(DocFillValue row, DocTemplateAnchor? anchor)
        {
            // ⚠️ AnchorExists 的判据必须同时看 IsValid 与 IsDeleted：
            //    IsValid=0（停用）或 IsDeleted=1（软删）都算「规则已失效」
            var anchorLive = anchor != null && anchor.IsValid == 1 && !anchor.IsDeleted;

            return new FillValueDto
            {
                Code = row.Code ?? string.Empty,
                AnchorCode = row.AnchorCode,
                AnchorRef = anchor?.AnchorRef ?? string.Empty,
                AnchorType = anchor?.AnchorType ?? string.Empty,
                AnchorKind = anchor?.AnchorKind ?? string.Empty,
                FieldCode = anchor?.FieldCode ?? string.Empty,
                LocationKind = row.LocationKind,
                LocationDesc = row.LocationDesc,
                ValueType = row.ValueType,
                ValueDisplay = row.ValueDisplay,
                ValueText = row.ValueText ?? string.Empty,
                SourceKind = row.SourceKind,
                SourceLabel = row.SourceLabel,
                Confidence = row.Confidence,
                ConfidenceReason = row.ConfidenceReason,
                FillStatus = row.FillStatus,
                WriteMode = row.WriteMode,
                OriginalText = row.OriginalText,
                IsOverridden = row.IsOverridden,
                OverrideKind = row.OverrideKind,
                OverrideReason = row.OverrideReason,
                OverriddenBy = row.OverriddenBy,
                OverriddenTime = row.OverriddenTime,
                IsPinned = row.IsPinned,
                SampleData = anchor?.SampleData ?? false,
                Required = anchor?.Required ?? false,
                IsOrphan = anchor?.IsOrphan ?? false,
                AnchorExists = anchorLive,
                Sort = anchor?.Sort ?? row.Sort,
            };
        }

        /// <summary>
        ///     ★ AI 候选建议池（`cert_doc_ai_suggestion`）—— 端点 10 的 <c>Candidates[]</c>。
        ///
        ///     <para>⚠️ <b>只含 AI 建议</b>：编排器已把 AI 取值从「自动生效」改为
        ///     「<b>建议池待确认</b>」（`55` §4.6）⇒ 这些行 <c>Status='pending'</c>、
        ///     ⛔ 不进值字典。用户在这里挑一条，才由 <c>value/override</c> 落进账本。</para>
        ///
        ///     <para><b>⛔ 查不到返回空表（不是错误）</b> —— 确定性来源的锚点本来就没有 AI 候选。</para>
        ///
        ///     <para><b>★ <paramref name="sourceDocCode"/> = 「只要来自这份原始资料的建议」</b>
        ///     （端点 13 的 <c>ProfileCode</c> 场景，`55` §17.2 方式①）。
        ///     ⚠️ 过滤必须在<b>实体层</b>做 —— <c>SourceDocCode</c> 不是 DTO 上的筛选键，
        ///     映射之后再按它筛会拿不到值。</para>
        /// </summary>
        private async Task<List<SourceCandidateDto>> BuildCandidatesAsync(
            string enterpriseCode, string standardFileCode, string anchorCode, string? sourceDocCode = null)
        {
            var rows = (await _db.GetListAsync<DocAiSuggestion>(x =>
                x.EnterpriseCode == enterpriseCode &&
                x.TemplateFileCode == standardFileCode &&
                x.AnchorCode == anchorCode)).Data ?? new List<DocAiSuggestion>();

            if (!string.IsNullOrWhiteSpace(sourceDocCode))
            {
                rows = rows
                    .Where(x => string.Equals(x.SourceDocCode, sourceDocCode, StringComparison.Ordinal))
                    .ToList();
            }

            return rows
                .OrderBy(x => x.SuggestionIndex)
                .ThenBy(x => x.Id)
                .Select(x => new SourceCandidateDto
                {
                    Code = x.Code ?? string.Empty,
                    SourceKind = "ai",
                    SourceLabel = string.IsNullOrWhiteSpace(x.ModelName)
                        ? "AI 建议"
                        : $"AI 建议（{x.ModelName}）",
                    Value = string.IsNullOrWhiteSpace(x.ManualValue) ? (x.SuggestedValue ?? string.Empty) : x.ManualValue!,
                    ValueKind = x.ValueKind,
                    Confidence = x.Confidence,
                    Evidence = x.SourceSnippet,
                    Location = x.SourceLocation,
                    // ★ 换原始件（方式①）要靠它写回 SourceDetailJson.originalFileCode
                    SourceDocCode = x.SourceDocCode,
                    Reason = x.Reason,
                    IsPicked = x.IsSelected,
                    Index = x.SuggestionIndex,
                })
                .ToList();
        }

        /// <summary>
        ///     ★ 查动作留痕（倒序）。
        ///     <para>⚠️ <b>必须带 <c>take + 1</c> 调用</b>才能判「是否被截断」——
        ///     多取一条是判截断最省的写法（⛔ 不要为此再加一次 count 查询）。</para>
        /// </summary>
        private async Task<List<NormalizeActionDto>> QueryActionsAsync(
            string? targetCode, string? scopeCode, string? enterpriseCode, int take)
        {
            var rows = new List<DocNormalizeAction>();

            if (!string.IsNullOrWhiteSpace(targetCode))
            {
                var byTarget = (await _db.GetListAsync<DocNormalizeAction>(x => x.TargetCode == targetCode)).Data
                               ?? new List<DocNormalizeAction>();
                rows.AddRange(byTarget);
            }

            if (!string.IsNullOrWhiteSpace(scopeCode))
            {
                var byScope = (await _db.GetListAsync<DocNormalizeAction>(x => x.ScopeCode == scopeCode)).Data
                              ?? new List<DocNormalizeAction>();
                rows.AddRange(byScope);
            }

            var merged = rows
                .Where(r => string.IsNullOrWhiteSpace(enterpriseCode) || r.EnterpriseCode == enterpriseCode)
                .GroupBy(r => r.Code ?? string.Empty, StringComparer.Ordinal)
                .Select(g => g.First())
                .OrderByDescending(r => r.CreateTime)
                .ThenByDescending(r => r.Id)
                .Take(take)
                .ToList();

            return await BuildActionDtosAsync(merged);
        }

        /// <summary>
        ///     动作留痕行 → DTO，并批量解析「操作人姓名」。
        ///     <para>⚠️ <c>Sys_User</c> 查询用 <c>includeDisabled: true</c> ——
        ///     被停用的用户留下的历史留痕仍要显示出姓名（⛔ 否则审计链上出现一串 Code）。</para>
        /// </summary>
        private async Task<List<NormalizeActionDto>> BuildActionDtosAsync(List<DocNormalizeAction> rows)
        {
            var dtos = rows.Select(a => new NormalizeActionDto
            {
                Code = a.Code ?? string.Empty,
                ActionType = a.ActionType,
                ScopeType = a.ScopeType,
                ScopeCode = a.ScopeCode,
                ScopeName = a.ScopeName,
                TargetCode = a.TargetCode,
                AnchorCode = a.AnchorCode,
                BeforeJson = a.BeforeJson,
                AfterJson = a.AfterJson,
                Reason = a.Reason,
                CreateBy = a.CreateBy ?? string.Empty,
                // ★ 兜底先放 Code —— 查不到姓名时⛔ 也不能留空（页面会显示成「操作人：」后面什么都没有）
                OperatorName = a.CreateBy ?? string.Empty,
                CreateTime = ToUtc(a.CreateTime),
                QueueCode = a.QueueCode,
            }).ToList();

            var userCodes = dtos
                .Select(d => d.CreateBy)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (userCodes.Count == 0) return dtos;

            var users = (await _db.GetListAsync<Sys_User>(x => userCodes.Contains(x.Code!),
                includeDisabled: true)).Data ?? new List<Sys_User>();

            var nameMap = users
                .Where(u => !string.IsNullOrEmpty(u.Code))
                .GroupBy(u => u.Code!, StringComparer.Ordinal)
                .ToDictionary(g => g.Key,
                    g => string.IsNullOrWhiteSpace(g.First().UserTrueName)
                        ? g.First().UserName
                        : g.First().UserTrueName,
                    StringComparer.Ordinal);

            foreach (var d in dtos)
            {
                if (nameMap.TryGetValue(d.CreateBy, out var name) && !string.IsNullOrWhiteSpace(name))
                    d.OperatorName = name;
            }

            return dtos;
        }

        // ────────────────────────────────────────────────────────────────────
        //  六之附：本域队列的共用判定与留痕（lock/unlock 亦复用）
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        ///     ⚠️ <b>只认本域的队列</b> —— <c>yzh_queue</c> 是全项目共用的队列表。
        ///     不加这道校验，本控制器就成了「拿到批次号即可读 / 取消任意模块队列」的越权口。
        /// </summary>
        private static bool IsOwnQueue(YzhQueue queue) =>
            string.Equals(queue.QueueType, EnterpriseNormalizeExecutor.TaskTypeName,
                StringComparison.OrdinalIgnoreCase);

        /// <summary>
        ///     终态判定 —— 与 <c>QueueManager.IsTerminal</c> <b>同口径</b>。
        ///     <para>⚠️ 两处必须一起改：本处是给前端「<b>停止轮询</b>」用的，
        ///     <c>QueueManager</c> 那份是给取消 / 调度用的；只改一处 ⇒ 页面永远转圈。</para>
        /// </summary>
        private static bool IsTerminalStatus(string? status) =>
            status is not null
            && (status.Equals("completed", StringComparison.OrdinalIgnoreCase)
                || status.Equals("failed", StringComparison.OrdinalIgnoreCase)
                || status.Equals("cancelled", StringComparison.OrdinalIgnoreCase));

        /// <summary>
        ///     从 <c>ScopeKey</c>（<c>entnorm:{企业}:{阶段}</c>）解析企业与阶段。
        ///     <para>⛔ 解析不出就返回空串（<b>不猜</b>）—— 留痕里的企业与阶段宁可空，
        ///     也不能填错（填错的留痕比空的更难排查）。</para>
        /// </summary>
        private static (string EnterpriseCode, string StageCode) ParseScopeKey(string? scopeKey)
        {
            var parts = (scopeKey ?? string.Empty).Split(':');
            return parts.Length >= 3 && string.Equals(parts[0], "entnorm", StringComparison.OrdinalIgnoreCase)
                ? (parts[1], parts[2])
                : (string.Empty, string.Empty);
        }

        /// <summary>按列宽截断 —— DB 列宽是硬约束，超长在非严格模式下会<b>静默截断</b></summary>
        private static string Truncate(string? text, int max) =>
            string.IsNullOrEmpty(text) ? string.Empty : (text.Length <= max ? text : text.Substring(0, max));

        /// <summary>
        ///     把多条「留痕写入失败」原因收敛成<b>一句</b>（去重 + 限量）。
        ///
        ///     <para><b>为什么必须收敛</b>：批量锁定 20 个文件若留痕全失败，原样拼 20 条
        ///     相同文案会把页面撑爆、把真正的主操作结果挤没。去重后同一根因只留一条，
        ///     数量差异用「另有 N 类」交代 —— ⛔ 既不静默，也不刷屏。</para>
        /// </summary>
        private static string? CollapseWarnings(List<string> warnings)
        {
            if (warnings == null || warnings.Count == 0) return null;

            var distinct = warnings.Distinct(StringComparer.Ordinal).ToList();
            var head = string.Join("；", distinct.Take(3));
            return distinct.Count > 3 ? $"{head}（另有 {distinct.Count - 3} 类）" : head;
        }

        /// <summary>
        ///     ★ <b>把「重写范围」展开成标准域行 Code 清单</b>（`rewrite` 端点专用）。
        ///
        ///     <para><b>★ 优先级：显式清单 &gt; ScopeType 展开</b>。与 <c>plan</c> / <c>run</c> /
        ///     <c>lock</c> 同口径 —— 前端已持有完整树，文件夹级由<b>前端</b>展开后传入；
        ///     后端再实现一遍文件夹递归 = 两份算法必然漂移，且漂移时<b>无人发现</b>。</para>
        ///
        ///     <para><b>⚠️ <c>folder</c> 级只取「直接挂靠该文件夹」的文件，⛔ 不递归子文件夹</b>：
        ///     递归需要读文件夹树（另一张表），而这份数据前端本来就有。
        ///     需要子文件夹的文件时，请传 <see cref="NormalizeRewriteRequest.StandardFileCodes"/>。</para>
        ///
        ///     <para>⚠️ <c>enterprise</c> / <c>stage</c> / <c>standard</c> 三级的范围收窄已在
        ///     <see cref="BuildScopeAsync"/> 里完成（<c>scope.Published</c> 就是已筛过的结果）
        ///     ⇒ 这里只做投影，⛔ 不重复筛一遍（重复筛 = 两处口径）。</para>
        /// </summary>
        private static List<string> ResolveRewriteScope(
            NormalizeRewriteRequest req, string scopeType, NormalizeScope scope)
        {
            var explicitCodes = (req.StandardFileCodes ?? new List<string>())
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (explicitCodes.Count > 0) return explicitCodes;

            var scopeCode = req.ScopeCode?.Trim() ?? string.Empty;

            if (scopeType == "file")
                return string.IsNullOrEmpty(scopeCode)
                    ? new List<string>()
                    : new List<string> { scopeCode };

            if (scopeType == "folder")
            {
                if (string.IsNullOrEmpty(scopeCode)) return new List<string>();

                return scope.Published
                    .Where(t => scope.StdMap.TryGetValue(t.StandardFileCode, out var sf)
                                && string.Equals(sf.FolderCode, scopeCode, StringComparison.Ordinal))
                    .Select(t => t.StandardFileCode)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
            }

            // enterprise / stage / standard —— 收窄已在 BuildScopeAsync 完成
            return scope.Published
                .Select(t => t.StandardFileCode)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        ///     ★ <b>单份文件的「重写保留/覆盖」预检</b>（`55` §6.3 五条不覆盖规则）。
        ///
        ///     <para><b>基准 = 该文件<b>最近一次</b>填充的账本</b>（⛔ 不接受指定批次）：
        ///     「全部重写」的语义永远是「相对<b>当前最新产物</b>重算」，
        ///     拿一个更老的批次当基准 ⇒ 会把中间几次人工改动判成「不存在」而覆盖掉。</para>
        ///
        ///     <para><b>★ 规则④ 优先于规则②③</b>：示例数据必须清空（合规铁律，⛔ 不可跳过）
        ///     ⇒ 先判 <c>SampleData</c>，命中的行<b>无论是否被钉住/人工改过都不计入保留</b>。
        ///     反过来（先判保留）会让「钉住一个示例数据锚点」变成绕过合规清空的后门。</para>
        ///
        ///     <para><b>★ 保留数必须<b>按行去重</b></b>：一行可以同时「被钉住」且「被人工改过值」
        ///     （`55` §18.4：两者可同时为 1）⇒ <c>PinnedCount + ManualCount</c>
        ///     <b>不等于</b>保留数，直接相加会把同一行算两次。</para>
        /// </summary>
        private async Task<NormalizeRewriteFilePrecheck> BuildRewritePrecheckAsync(
            string enterpriseCode, string standardFileCode, string fileName, string action,
            bool isLocked, bool keepManual, bool keepPinned)
        {
            var pre = new NormalizeRewriteFilePrecheck
            {
                StandardFileCode = standardFileCode,
                FileName = fileName,
                Action = action,
                IsLocked = isLocked,
            };

            var log = await FindLatestFillLogAsync(enterpriseCode, standardFileCode);
            // 从未规范化过 ⇒ 全是 0。语义明确：没有旧产物可保留（⛔ 不是「预检失败」）
            if (log == null) return pre;

            pre.FillLogCode = log.Code;
            pre.FillLogTime = ToUtc(log.CreateTime);

            var rows = (await _db.GetListAsync<DocFillValue>(x => x.FillLogCode == log.Code)).Data
                       ?? new List<DocFillValue>();
            pre.LedgerRowCount = rows.Count;
            if (rows.Count == 0) return pre;

            var anchorMap = await LoadAnchorMapAsync(rows.Select(r => r.AnchorCode));

            foreach (var r in rows)
            {
                anchorMap.TryGetValue(r.AnchorCode ?? string.Empty, out var anchor);

                // 规则④：示例数据必须清空（⛔ 不受 KeepPinned / KeepManual 影响）
                if (anchor?.SampleData == true)
                {
                    pre.SampleCount++;
                    continue;
                }

                var isPinned = r.IsPinned;
                var isManual = r.IsOverridden
                               && (string.Equals(r.OverrideKind, "value", StringComparison.OrdinalIgnoreCase)
                                   || string.Equals(r.OverrideKind, "both", StringComparison.OrdinalIgnoreCase));

                if (isPinned) pre.PinnedCount++;
                if (isManual) pre.ManualCount++;

                // ★ 去重计行 —— 见方法注释：钉住 + 人工改值可能是同一行
                if ((keepPinned && isPinned) || (keepManual && isManual)) pre.WillKeepCount++;
            }

            pre.WillRecomputeCount = pre.LedgerRowCount - pre.WillKeepCount - pre.SampleCount;
            return pre;
        }

        /// <summary>
        ///     ★ 写一条动作留痕（<c>cert_doc_normalize_action</c>，<b>只追加</b>）。
        ///
        ///     <para><b>返回 <c>null</c> = 成功</b>；非 <c>null</c> = 失败原因（调用方<b>必须带出去</b>）。</para>
        ///
        ///     <para><b>⚠️ 为什么手动赋 <c>Code</c></b>：<c>_db.InsertAsync</c> 是 <b>DbOrm 直插</b>、
        ///     <b>不经过 <c>AddCore()</c></b> ⇒ 基类「新增时框架自动生成 Code」那条<b>不生效</b>，
        ///     而 <c>cert_doc_normalize_action.Code</c> 是 <c>NOT NULL</c> ⇒ 漏赋即插入失败。
        ///     （同族事故：<c>cert_doc_fill_value</c> 曾因此恒 0 行，却报「账本 N 行」。）</para>
        ///
        ///     <para><b>⛔ 不静默</b>：写失败既不能吞（审计链断要让人知道），
        ///     也不能让调用方谎报「主操作失败」（主操作其实已经生效）⇒ 返回原因由调用方上屏。</para>
        /// </summary>
        private async Task<string?> WriteActionAsync(DocNormalizeAction row)
        {
            row.Code = Guid.NewGuid().ToString();
            row.CreateBy = UserContext.UserCode;
            row.CreateTime = DateTime.UtcNow;

            try
            {
                var ins = await _db.InsertAsync(row);
                if (ins.Success) return null;

                _logger.LogError("[EntNorm] 动作留痕写入失败：Action={Action}, Target={Target}, Err={Err}",
                    row.ActionType, row.TargetCode, ins.Error);
                return $"动作已生效，但审计留痕写入失败（{ins.Error}）";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EntNorm] 动作留痕写入异常：Action={Action}, Target={Target}",
                    row.ActionType, row.TargetCode);
                return $"动作已生效，但审计留痕写入异常（{ex.Message}）";
            }
        }
    }
}

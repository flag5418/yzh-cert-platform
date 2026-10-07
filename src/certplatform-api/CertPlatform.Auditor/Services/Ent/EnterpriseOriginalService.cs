using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CertPlatform.Admin.Services.Workflow;
using CertPlatform.Shared.Constants;
using CertPlatform.Shared.DocExtraction;
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
    /// 企业原始资料管理服务（专家端 <c>/enterprise-original</c>，36 号 §五）。
    ///
    /// <para><b>与 <see cref="EnterpriseFileService"/> 的区别（两者不可混）</b>：</para>
    /// <list type="table">
    ///   <item><description></description><description><b>本服务</b>（原始资料）</description><description><c>EnterpriseFileService</c>（资料库）</description></item>
    ///   <item><description>组织轴</description><description>企业 → <b>阶段</b>（无标准）</description><description>企业 → 阶段 → 标准 → 槽位</description></item>
    ///   <item><description>MinIO 库</description><description><c>enterprise-original-source/…</c></description><description><c>enterprise-documents/…</c></description></item>
    ///   <item><description>主表</description><description><c>cert_enterprise_original_file</c></description><description><c>cert_standard_directory_file</c></description></item>
    ///   <item><description>输入性质</description><description>企业交上来的<strong>散乱资料</strong>（还没对标准）</description><description>企业<strong>已按标准备好</strong>的材料</description></item>
    /// </list>
    ///
    /// <para><b>三条硬约束（改代码前先读）</b>：</para>
    /// <list type="number">
    ///   <item><b>工作区守卫</b>：每个公开方法第一行调 <see cref="OwnershipErrorAsync"/>（一处收口，避免漏网）。</item>
    ///   <item><b>D9 Id 零语义</b>：⛔ 定位·删除·更新·传参<b>只用 <c>Code</c></b>，永不用 <c>Id</c>（铁律四）。</item>
    ///   <item><b>D7 判重 + D8 版本</b>：变更判定靠 <c>Sha256</c>；⛔ 表①无 <c>(Ent,Stage,Path,FileName)</c> 唯一索引
    ///         （4 列联合索引 3536 B &gt; InnoDB 上限 3072 B，实跑 ERROR 1071）⇒ 判重是应用层两段式。</item>
    /// </list>
    /// </summary>
    public class EnterpriseOriginalService
    {
        private readonly IDbOrm _db;
        private readonly IObjectStorage _storage;
        private readonly QueueManager _queueManager;
        private readonly WorkspaceContextService _workspace;
        private readonly IUserContext _user;
        private readonly ILogger<EnterpriseOriginalService> _logger;

        /// <summary>批次有效期（分钟）：init 后超时未 confirm 即作废，防悬空草稿行永久占位</summary>
        private const int TaskExpireMinutes = 30;

        /// <summary>单文件大小上限（字节）= 200 MB，与既有 upload/file 的 RequestSizeLimit 一致</summary>
        public const long MaxFileSizeBytes = 200L * 1024 * 1024;


        public EnterpriseOriginalService(
            IDbOrm db,
            IObjectStorage storage,
            QueueManager queueManager,
            WorkspaceContextService workspace,
            IUserContext user,
            ILogger<EnterpriseOriginalService> logger)
        {
            _db = db;
            _storage = storage;
            _queueManager = queueManager;
            _workspace = workspace;
            _user = user;
            _logger = logger;
        }

        // ========================================================
        // 零、状态取值（前后端逐字一致；⛔ 转换状态只能取 convertStatus.ts 的 6 个值）
        // ========================================================

        /// <summary>转换状态（对齐 <c>cert-share/src/utils/convertStatus.ts</c>）</summary>
        public static class ConvertStatus
        {
            public const string None = "none";
            public const string Pending = "pending";
            public const string Converting = "converting";
            public const string Completed = "completed";
            public const string Failed = "failed";
            public const string Unsupported = "unsupported";
        }

        /// <summary>分析状态</summary>
        public static class AnalyzeStatus
        {
            public const string Pending = "pending";
            public const string Analyzing = "analyzing";
            public const string Analyzed = "analyzed";
            public const string Failed = "failed";
            public const string Skipped = "skipped";
        }

        /// <summary>分析策略（字典 <c>ANALYZE_POLICY</c>）—— 只作用于 L2「数据来源」层</summary>
        public static class Policy
        {
            public const string Analyze = "analyze";
            public const string Skip = "skip";
            public const string Ignore = "ignore";
        }

        /// <summary>
        /// ★ <b>「填写期可用」唯一判定</b>（36 号 §六 铁律，2026-10-03 验收补入）。
        ///
        /// <para><b>为什么必须双条件而不是看分析状态</b>：<c>ConvertStatus</c> 与 <c>AnalyzeStatus</c>
        /// 是<b>两列独立状态</b>（先转 Markdown、再分析，两阶段串联）。因此存在
        /// 「<b>转成功但分析失败</b>」的半成品态 —— 此时文件有 Markdown、有预览 PDF，但没有标签/作用画像。
        /// 若下游（规范化 / 匹配 / 填充）只判「分析成功」，仍可能取到<b>连分析都没跑</b>的行；
        /// 若只判「转换成功」，会把<b>半成品</b>喂给 LLM。
        /// <b>⇒ 取资料必须「转换成功 AND 分析成功」，缺一不可。</b></para>
        ///
        /// <para>三个判定口径（不要各写一遍）：</para>
        /// <list type="bullet">
        ///   <item><b><see cref="IsUsableForFilling"/></b>：唯一权威。转换 <c>completed</c>
        ///         ∧ Markdown <c>completed</c> ∧ 分析 <c>analyzed</c>。</item>
        ///   <item><b><see cref="IsHalfProduct"/></b>：半成品（转换好了但分析没成）——
        ///         这类行<b>必须被下游排除</b>，且 UI 上要能一眼看出。</item>
        ///   <item><b><see cref="ExplainUsability"/></b>：给 UI 的一行话说明（为什么不可用）。</item>
        /// </list>
        /// </summary>
        public static bool IsUsableForFilling(EnterpriseOriginalFile row)
            => row.ConvertStatus == ConvertStatus.Completed
               && row.MarkdownStatus == ConvertStatus.Completed
               && row.AnalyzeStatus == AnalyzeStatus.Analyzed;

        /// <summary>半成品：转换成功但分析未成功（<b>下游必须排除</b>，见类注释）</summary>
        public static bool IsHalfProduct(EnterpriseOriginalFile row)
            => row.ConvertStatus == ConvertStatus.Completed
               && row.MarkdownStatus == ConvertStatus.Completed
               && row.AnalyzeStatus != AnalyzeStatus.Analyzed;

        /// <summary>
        /// ★ <b>操作系统 / 编辑器垃圾文件</b>识别（2026-10-03 用户报障）：
        /// macOS 选文件夹上传时，每个目录里都可能带 <c>.DS_Store</c>；Windows 有 <c>Thumbs.db</c>；
        /// Office 打开文档会生成锁文件 <c>~$xxx.doc</c>；macOS 打包可能带 <c>._xxx</c>（资源分叉）。
        ///
        /// <para><b>为什么必须单独识别，而不是丢给「格式白名单」判不合规</b>：
        /// 这些文件**用户根本没打算上传**，把它们报成「不支持的格式」是错的（用户看到一堆
        /// 莫名其妙的不合规项）；更糟的是前端「有一个不合规就整体中止」会让
        /// <b>整个文件夹传不上去</b>（2026-10-03 实测：含 1 个 <c>.DS_Store</c> ⇒ 上传按钮点了没反应）。
        /// ⇒ 判定为「系统噪声」⇒ <b>plan 阶段直接剔除</b>，只在 <c>Summary.FilteredCount</c> 里报个���数。</para>
        /// </summary>
        public static bool IsSystemNoise(string? fileName)
            => UploadFilePolicy.IsSystemNoise(fileName);

        /// <summary>图片类扩展名 —— <b>天然是证照照片</b>（营业执照/身份证/资质证书扫描件）</summary>
        private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tif", ".tiff"
        };

        /// <summary>
        /// ★ <b>格式层预判</b>：这份文件是否<b>天然不该</b>进标签/作用抽取。
        ///
        /// <para><b>为什么要有这层规则</b>（2026-10-03 用户指出）：营业执照、身份证、资质证书、许可证
        /// 这类<b>特定证件</b>本身没有「体系文件的作用」语义，硬打标签只会污染召回词表。
        /// 而我们<b>目前没有</b> OCR / 版面理解 / 证件分类模型
        /// （联网调研结论见 36 号 §十 L7），<b>纯靠 LLM 读 Markdown 猜「这是不是证件」本就不准</b> ——
        /// 尤其图片类根本产不出 Markdown，LLM 连看都看不到。</para>
        ///
        /// <para><b>当前口径（保守，只判最有把握的）</b>：</para>
        /// <list type="bullet">
        ///   <item><b>图片文件</b>（jpg/png/…）⇒ 直接判「不建议提取」。
        ///         理由：企业上传的图片几乎全是证照扫描件，正文无从提取。</item>
        ///   <item><b>PDF 不一刀切</b>：体系文件（手册/程序文件/记录）大量是 PDF，
        ///         仍交给 LLM 判 + 人工设策略。</item>
        /// </list>
        ///
        /// <para><b>将来接入证件识别后</b>（§十 L7 的三个候选方向）：本方法扩成
        /// 「格式预判 → 证件识别 → LLM」三级判定，证件类走<b>独立的证件字段</b>
        /// （证照类型/编号/有效期），<b>不塞进通用标签池</b>。</para>
        /// </summary>
        public static bool IsNotSuggestedByFormat(EnterpriseOriginalFile row)
        {
            var ext = (row.FileType ?? "").Trim().ToLowerInvariant();
            return ext.Length > 0 && ImageExtensions.Contains(ext);
        }

        /// <summary>
        /// ★ <b>不建议提取</b>：人工或 AI 判定这份文件<b>不参与</b>标签/作用抽取。
        ///
        /// <para><b>为什么这类文件不给「标签 / 作用」入口</b>（2026-10-03 用户裁决）：
        /// 营业执照、身份证、资质证书、许可证这类<b>特定证件</b>本身没有「体系文件的作用」语义 ——
        /// 硬给它打标签只会污染召回词表；而我们<b>还没有</b>按格式+内容识别证件的能力
        /// （无 OCR、无版面理解、无证件分类模型），靠 LLM 猜「这是不是证件」本就不准。</para>
        ///
        /// <para>⇒ 判定口径 = <b>格式预判</b>（<see cref="IsNotSuggestedByFormat"/>，图片类）
        /// <b>∪</b> <b>策略</b>（人工或 AI 建议 skip/ignore）。两者任一命中即不提供标签/作用入口。</para>
        /// 将来若引入证件识别（见 36 号 §十 遗留），再按识别结果细分「证件类」，
        /// 那时证件应该走<b>独立的证件字段</b>（证照类型/编号/有效期），而不是塞进通用标签池。</para>
        /// </summary>
        public static bool IsNotSuggested(EnterpriseOriginalFile row)
            => row.AnalyzePolicy is Policy.Skip or Policy.Ignore
               || IsNotSuggestedByFormat(row);

        /// <summary>不可用的一句话原因（UI 直接展示，避免用户猜）</summary>
        public static string ExplainUsability(EnterpriseOriginalFile row)
        {
            if (IsNotSuggested(row))
                return row.PolicySource == "ai"
                    ? "系统建议不参与提取（待人工确认），这类文件不设标签和作用"
                    : "已设置为不参与提取，这类文件不设标签和作用";
            if (row.ConvertStatus == ConvertStatus.Failed) return "转换失败";
            if (row.ConvertStatus == ConvertStatus.Converting) return "转换中";
            if (row.ConvertStatus == ConvertStatus.None) return "待转换";
            if (row.ConvertStatus == ConvertStatus.Unsupported) return "该格式需人工填写";
            if (row.MarkdownStatus == ConvertStatus.Failed) return "Markdown 提取失败";
            if (row.MarkdownStatus == ConvertStatus.Converting) return "Markdown 提取中";
            if (row.MarkdownStatus == ConvertStatus.None) return "待提取 Markdown";
            if (row.MarkdownStatus == ConvertStatus.Unsupported) return "该格式需人工填写";
            if (row.AnalyzeStatus == AnalyzeStatus.Analyzing) return "分析中";
            if (row.AnalyzeStatus == AnalyzeStatus.Pending) return "待分析";
            if (row.AnalyzeStatus == AnalyzeStatus.Skipped) return "策略为跳过/忽略（未做语义分析）";
            if (row.AnalyzeStatus == AnalyzeStatus.Failed) return "分析失败";
            return "";
        }

        /// <summary>批次状态</summary>
        public static class TaskStatus
        {
            public const string Draft = "draft";
            public const string Uploading = "uploading";
            public const string Confirmed = "confirmed";
            public const string Cancelled = "cancelled";
            public const string Failed = "failed";
        }

        // ========================================================
        // 一、左树 + 列表
        // ========================================================

        /// <summary>
        /// 左树：本工作区企业 → 该企业关联的认证阶段（每阶段带原始资料计数）。
        /// <para>与 <c>EnterpriseFileService.StageTreeAsync</c> 同构，但计数口径是<b>原始资料表</b>。</para>
        /// </summary>
        public async Task<object> StageTreeAsync()
        {
            var ws = _workspace.Resolve(_user.UserCode);
            if (!ws.Success || ws.Data == null)
                return new { Configured = false, Message = ws.Error ?? "无法定位当前工作区", Nodes = new object[0] };

            var enterprises = (await _db.GetListAsync<Enterprise>(x => x.OrgCode == ws.Data.Code)).Data
                ?? new List<Enterprise>();

            var nodes = new List<object>();
            foreach (var ent in enterprises.OrderBy(e => e.Name))
            {
                var links = await _db.Client.Queryable<CertEnterpriseStage>()
                    .Where(x => x.EnterpriseCode == ent.Code && x.IsValid == 1 && !x.IsDeleted)
                    .ToListAsync() ?? new List<CertEnterpriseStage>();

                // ★ <b>M7（2026-10-07）</b>：本企业绑定的**标准清单**，随阶段节点下发 ⇒ 前端渲染标准 tab。
                //
                // <para><b>为什么必须从这里取标准</b>：原始资料表 <c>cert_enterprise_original_file</c>
                // <b>没有标准列</b>（只有 <c>StageCode</c>）—— 标准维度只存在于
                // <c>cert_enterprise_stage</c>（一行 = 一个「企业 × 阶段 × 标准」）与画像表
                // <c>cert_enterprise_doc_profile</c>（一行 = 一个「文件 × 标准」画像）。</para>
                //
                // <para>⇒ 前端不把标准传下去时，后端只能 <c>FirstOrDefault</c> 取「某一行」画像
                // ⇒ 同一份文件在 A 标准下显示 B 标准的标签/作用（用户点名痛点：
                // 「点击分组的时候，显示的信息不正确」）。</para>
                var stdCodes = links.Select(l => (l.StandardCode ?? "").Trim())
                    .Where(c => c != "")
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                var stdRows = stdCodes.Count == 0
                    ? new List<ISOStandard>()
                    : await _db.Client.Queryable<ISOStandard>()
                        .Where(x => stdCodes.Contains(x.Code) && x.IsValid == 1 && !x.IsDeleted)
                        .ToListAsync() ?? new List<ISOStandard>();
                // ⚠️ 排序在内存里做（⛔ 不在 SqlSugar 表达式里 OrderBy）。
                //    Sort 是标准表自己的排序号，实测**两个标准都是 0**（并列）⇒ 只靠它排序结果不稳定，
                //    会让「食品标准」排在「9001标准」前面（对 9001 为主的企业很别扭）。
                //    故补两级确定性兜底：VersionYear（标准版本年，旧的在前）→ StandardCode。
                //    ⚠️ 这只是**确定性**排序，不代表业务上的主次（业务主次未定义）。
                stdRows = stdRows
                    .OrderBy(x => x.Sort)
                    .ThenBy(x => x.VersionYear)
                    .ThenBy(x => x.StandardCode, StringComparer.Ordinal)
                    .ToList();

                var stageCodes = links.Select(l => l.StageCode).Distinct().ToList();
                var stages = stageCodes.Count == 0
                    ? new List<CertStage>()
                    : await _db.Client.Queryable<CertStage>()
                        .Where(x => stageCodes.Contains(x.Code) && x.IsValid == 1 && !x.IsDeleted)
                        .ToListAsync() ?? new List<CertStage>();

                var counts = await _db.Client.Queryable<EnterpriseOriginalFile>()
                    .Where(x => x.EnterpriseCode == ent.Code && !x.IsDeleted && x.IsValid == 1)
                    .ToListAsync() ?? new List<EnterpriseOriginalFile>();

                var children = stages.OrderBy(s => s.SortOrder).Select(s =>
                {
                    // ★ 必须用 s.Code（GUID）而不是 s.StageCode（slug 如 jd01/03）：
                    //   EnterpriseOriginalFile.StageCode 存的是**阶段 Code（GUID）**
                    //   —— 与 cert_enterprise_stage.StageCode 同一口径（见下方 Code = s.Code 的注释）。
                    //   ⛔ 曾经写成 c.StageCode == s.StageCode ⇒ GUID 比 slug 永远不等
                    //      ⇒ 每个阶段的 FileCount 恒为 0（企业节点却是真实总数）
                    //      ⇒ 界面表现为「企业明明有 101 份，每个阶段都显示 0」。
                    var sc = counts.Where(c => c.StageCode == s.Code).ToList();
                    // ★ M7：该阶段绑定的标准（同一阶段可以同时做多个标准，如 9001 + 食品）
                    var stageStdCodes = links.Where(l => l.StageCode == s.Code)
                        .Select(l => (l.StandardCode ?? "").Trim())
                        .Where(c => c != "")
                        .ToHashSet(StringComparer.Ordinal);
                    return (object)new
                    {
                        // ★ 必须是 Code（GUID）而不是 StageCode（slug 如 jd01/03）：
                        //   前端拿节点 Code 作为 list 端点的 StageCode 传回，给 slug 查不到行。
                        //   ⛔ 这与「关联表 cert_enterprise_stage.StageCode 存的是 Code」是同一口径。
                        Code = s.Code,
                        Label = s.StageName,
                        // 双写 Name：YzhTree 的默认 labelField 是 'Name'，
                        // 双写后即使前端忘传 label-field 也不会「只有图标没有文字」。
                        Name = s.StageName,
                        StageCodeSlug = s.StageCode,
                        // ★ 必须带上所属企业 Code：前端点【阶段】节点时要分别拿到
                        //   EnterpriseCode 与 StageCode 去调 list/status-bar。
                        //   ⛔ 两者都取 node.Code 会把阶段 GUID 当企业 GUID 传 ⇒ 工作区校验必失败。
                        EnterpriseCode = ent.Code,
                        IsEnterpriseNode = false,
                        FileCount = sc.Count,
                        ConvertingCount = sc.Count(c => c.ConvertStatus == ConvertStatus.Converting),
                        AnalyzingCount = sc.Count(c => c.AnalyzeStatus == AnalyzeStatus.Analyzing),
                        FailedCount = sc.Count(c => c.ConvertStatus == ConvertStatus.Failed
                                                 || c.AnalyzeStatus == AnalyzeStatus.Failed),
                        // ★ M7：本阶段绑定的标准清单（已按 Sort 排好）
                        //   ⇒ 前端据此渲染标准 tab；把选中标准的 Code 传回 list/profile 端点，
                        //     标签与作用才按「该标准那一行画像」显示（根治串标准）。
                        Standards = stdRows
                            .Where(x => stageStdCodes.Contains(x.Code))
                            .Select(x => new { x.Code, x.StandardCode, x.StandardName, x.VersionYear })
                            .ToList(),
                    };
                }).ToList();

                nodes.Add(new
                {
                    Code = ent.Code,
                    Label = ent.Name,
                    Name = ent.Name,
                    EnterpriseCode = ent.Code,
                    StageCode = "",
                    IsEnterpriseNode = true,
                    FileCount = counts.Count,
                    Children = children,
                });
            }

            return new { Configured = true, Message = (string?)null, Nodes = nodes };
        }

        /// <summary>
        /// 该企业该阶段的原始资料列表（含当前活跃版 + 分析状态 + 标签 + 可用性）。
        /// </summary>
        /// <param name="tagCodes">
        /// ★ <b>语义过滤</b>（老板问题 1 的落点）：按受控标签过滤，多个标签取<b>并集</b>。
        /// <para>这是<b>分类过滤</b>的唯一入口 —— 目录（<c>RelFolderPath</c>）做不到这件事，
        /// 因为企业交上来时的物理目录 ≠ 业务分类。</para>
        /// <para>⛔ 标签存在 <c>cert_enterprise_doc_profile.TagsJson</c>（JSON 数组）里，
        /// 在 MySQL 侧做 JSON 过滤需要函数索引；本实现<b>取回后在内存里过滤</b>
        /// —— 单企业单阶段量级为几十到几百份，内存过滤足够，且不引 JSON 索引的维护成本。</para>
        /// </param>
        /// <param name="onlyUsable">
        /// true = 只返回「填写期可用」的行（<b>转换 ∧ 分析</b> 双条件，见 <see cref="IsUsableForFilling"/>）。
        /// 下游取资料时<b>应当</b>传 true，从根上排除半成品。
        /// </param>
        /// <param name="groupByTag">
        /// true = 额外返回「语义分组聚合」（每个标签多少份），供页面渲染分组视图。
        /// </param>
        /// <param name="standardCode">
        /// ★ <b>M6（2026-10-06）</b>：按<b>标准</b>取画像。
        ///
        /// <para>一个文件在 N 个标准下各有<b>一行</b>画像（选项 A 多行画像）⇒ 不指定标准时
        /// 只能拿到「该文件的某一行」（<c>FirstOrDefault</c>），<b>标签/作用可能属于另一个标准</b>。</para>
        /// <para>空 = 兼容旧前端：不过滤标准（取该文件版本号最大的一行）。</para>
        /// </param>
        public async Task<object> ListAsync(
            string enterpriseCode, string stageCode,
            IList<string>? tagCodes = null, bool onlyUsable = false, bool groupByTag = false,
            string? standardCode = null)
        {
            var err = await OwnershipErrorAsync(enterpriseCode);
            if (err != null) return new { Success = false, Message = err, Rows = new object[0] };
            if (string.IsNullOrWhiteSpace(stageCode)) return new { Success = false, Message = "请先选择认证阶段", Rows = new object[0] };

            var rows = await _db.Client.Queryable<EnterpriseOriginalFile>()
                .Where(x => x.EnterpriseCode == enterpriseCode && x.StageCode == stageCode
                            && !x.IsDeleted && x.IsValid == 1)
                .ToListAsync() ?? new List<EnterpriseOriginalFile>();

            // ★ M6：画像按**标准**过滤（不传则不限标准，取版本号最大的一行）
            var std = (standardCode ?? "").Trim();
            var profiles = (await _db.Client.Queryable<EnterpriseDocProfile>()
                .Where(x => x.EnterpriseCode == enterpriseCode && x.IsLatest && x.IsValid == 1
                            && (std == "" || x.StandardCode == std))
                .ToListAsync() ?? new List<EnterpriseDocProfile>())
                // ★ 同一文件在同一标准下只应有一行 IsLatest；万一历史数据有多行，取版本号最大者（确定性）
                .GroupBy(x => x.OriginalFileCode, StringComparer.Ordinal)
                .Select(g => g.OrderByDescending(x => x.ProfileVersion).First())
                .ToList();

            // ★ 本企业×阶段「画像里出现过的标准」——供前端渲染标准切换器（M7-③，P2 落地 UI）
            var availableStandards = (await _db.Client.Queryable<EnterpriseDocProfile>()
                    .Where(x => x.EnterpriseCode == enterpriseCode && x.StageCode == stageCode
                                && x.IsLatest && x.IsValid == 1)
                    .ToListAsync() ?? new List<EnterpriseDocProfile>())
                .Select(x => x.StandardCode)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            // ★ 标签过滤（语义分组）：先挂画像再过滤
            var tagByFile = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var p in profiles)
                tagByFile[p.OriginalFileCode] = ParseTagCodes(p.TagsJson);

            var wanted = (tagCodes ?? new List<string>())
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            IEnumerable<EnterpriseOriginalFile> query = rows;
            if (wanted.Count > 0)
                query = query.Where(r => tagByFile.TryGetValue(r.Code, out var ts)
                                        && ts.Any(t => wanted.Contains(t)));
            if (onlyUsable)
                query = query.Where(IsUsableForFilling);

            var filtered = query.ToList();

            var list = filtered
                .OrderBy(r => r.RelFolderPath)
                .ThenBy(r => r.FileName)
                .Select(r =>
                {
                    var p = profiles.FirstOrDefault(x => x.OriginalFileCode == r.Code);
                    var usable = IsUsableForFilling(r);
                    return (object)new
                    {
                        r.Code,
                        r.EnterpriseCode,
                        r.StageCode,
                        r.RelFolderPath,
                        r.FileName,
                        r.FileType,
                        r.FileSize,
                        r.Sha256,
                        r.StoragePath,
                        r.VersionNumber,
                        // ★ 语义分组：这份文件命中了哪些受控标签
                        Tags = tagByFile.TryGetValue(r.Code, out var tl) ? tl : new List<string>(),
                        // ★ 填写期可用性（转换 ∧ 分析 双条件）+ 半成品标记 + 一句话原因
                        IsUsableForFilling = usable,
                        IsHalfProduct = IsHalfProduct(r),
                        // ★ 「不建议提取」⇒ 前端不给标签/作用入口（营业执照/身份证等特定证件）
                        IsNotSuggested = IsNotSuggested(r),
                        // 供 UI 区分原因：格式预判（图片类）vs 策略设置（人工/AI 建议）
                        NotSuggestedByFormat = IsNotSuggestedByFormat(r),
                        UnusableReason = usable ? (string?)null : ExplainUsability(r),
                        r.ConvertStatus,
                        r.ConvertMessage,
                        r.PreviewPdfPath,
                        r.MarkdownPath,
                        r.MarkdownStatus,
                        r.MarkdownMessage,
                        r.ConvertDate,
                        r.AnalyzeStatus,
                        r.AnalyzeMessage,
                        r.AnalyzeTime,
                        r.AnalyzePolicy,
                        r.PolicyReason,
                        r.PolicySource,
                        r.PolicyDecidedBy,
                        r.PolicyDecidedTime,
                        CreateTime = r.CreateTime,
                        CreateBy = r.CreateBy,
                        UpdateTime = r.UpdateTime,
                        HasProfile = p != null,
                        ProfileStatus = p?.ProfileStatus,
                        DocCategory = p?.DocCategory,
                        Summary = p?.Summary,
                        DocPurpose = p?.DocPurpose,
                        InfoItemsJson = p?.InfoItemsJson,
                        ProfileVersion = p?.ProfileVersion,
                        // ★ M6：这行画像属于哪个标准（前端标准切换器据此对齐）
                        StandardCode = p?.StandardCode,
                    };
                }).ToList();

            // 语义分组聚合：每个标签命中多少份（供页面「按标签分组」视图）
            var grouped = filtered
                .SelectMany(r => tagByFile.TryGetValue(r.Code, out var ts) ? ts : new List<string>())
                .GroupBy(t => t, StringComparer.OrdinalIgnoreCase)
                .Select(g => new { TagCode = g.Key, FileCount = g.Count() })
                .OrderByDescending(g => g.FileCount)
                .ThenBy(g => g.TagCode, StringComparer.Ordinal)
                .ToList();

            // 全量可用性统计（不受标签过滤影响，供状态条用）
            var usableTotal = rows.Count(IsUsableForFilling);
            var halfTotal = rows.Count(IsHalfProduct);

            return new
            {
                Success = true,
                Message = (string?)null,
                Rows = list,
                Total = rows.Count,
                UsableCount = usableTotal,
                HalfProductCount = halfTotal,
                Groups = groupByTag ? grouped : null,
                // ★ M6：当前生效的标准过滤 + 该企业×阶段画像里出现过的全部标准（供前端标准切换器）
                StandardCode = std == "" ? null : std,
                Standards = availableStandards,
            };
        }

        /// <summary>从 <c>TagsJson</c>（JSON 数组）取 <c>tagCode</c> 列表；兼容「纯字符串数组」与「对象数组」两种形态</summary>
        internal static List<string> ParseTagCodes(string? tagsJson)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(tagsJson)) return list;
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(tagsJson);
                if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Array) return list;
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    var code = el.ValueKind switch
                    {
                        System.Text.Json.JsonValueKind.String => el.GetString(),
                        System.Text.Json.JsonValueKind.Object =>
                            el.TryGetProperty("tagCode", out var c) ? c.GetString() : null,
                        _ => null,
                    };
                    if (!string.IsNullOrWhiteSpace(code)) list.Add(code.Trim());
                }
            }
            catch (System.Text.Json.JsonException) { /* 非法 JSON 由画像校验拦，这里只做防御 */ }
            return list;
        }

        // ========================================================
        // 七·b、★ 重新生成（转换 / 识别重跑入口，36 号 §八 T4）
        // ========================================================

        /// <summary>
        /// ★ **重新生成**一份文件的 Markdown（重跑转换链，随后自动接分析链）。
        ///
        /// <para><b>为什么必须有这个接口</b>（2026-10-03 核查发现）：此前 Markdown 只有「上传时生成」这一个入口，
        /// 于是下面这些场景<b>只能重新上传整个文件夹</b>，体验极差且会打断已有批次：</para>
        /// <list type="number">
        ///   <item>anydoc 偶发失败（Docker 忙 / 超时）⇒ 想重试</item>
        ///   <item>改了 <c>ai_vision_config</c>（换模型 / 开视觉识别）⇒ 想用新配置重跑扫描件</item>
        ///   <item>专家误判「需人工填写」⇒ 想换成 AI 识别</item>
        ///   <item>僵死逃生把状态标成 failed ⇒ 需要真正的重试口子</item>
        /// </list>
        ///
        /// <para><b>语义</b>：<b>不动源文件、不改版本号</b>（不是替换文件），
        /// 只重跑「字节 → PDF/Markdown」这一段；成功后自动并入 analyze 队列（单文件批次）。</para>
        /// </summary>
        /// <param name="reanalyze">是否连语义分析一起重跑（false = 只重转 Markdown，画像不动）</param>
        public async Task<Result<object?>> RegenerateAsync(
            string fileCode, string enterpriseCode, bool reanalyze = true)
        {
            if (string.IsNullOrWhiteSpace(fileCode))
                return Result<object?>.Fail("文件业务键 Code 不能为空");

            var row = (await _db.Client.Queryable<EnterpriseOriginalFile>()
                .Where(x => x.Code == fileCode).ToListAsync() ?? new List<EnterpriseOriginalFile>())
                .FirstOrDefault();
            if (row == null) return Result<object?>.Fail($"记录不存在：{fileCode}");

            var tenantErr = await OwnershipErrorAsync(enterpriseCode);
            if (tenantErr != null) return Result<object?>.Fail(tenantErr);
            if (!string.Equals(row.EnterpriseCode, enterpriseCode, StringComparison.Ordinal))
                return Result<object?>.Fail("记录不存在或不属于当前工作区");
            if (row.IsDeleted) return Result<object?>.Fail("记录已删除");

            // ★ 文件级闸门（2026-10-06 重构）：⛔ 不再按「阶段忙」拒绝 —— 阶段里其他文件在跑
            //   与本文件无关（实测误伤）。同文件自己的旧队列由 EnqueueFileQueuesAsync
            //   入队前取消重建（用户裁决 ③），并发写同一行的风险由资源锁 + 取消解决。
            if (string.IsNullOrEmpty(row.StoragePath))
                return Result<object?>.Fail("文件尚未上传完成，无法生成内容");

            // 置回待处理（⛔ 清 Markdown 路径，防止读到旧产物）
            row.ConvertStatus = ConvertStatus.Pending;
            row.ConvertMessage = null;
            row.MarkdownStatus = ConvertStatus.Pending;
            row.MarkdownMessage = null;
            row.PreviewPdfPath = null;
            row.MarkdownPath = null;
            row.ConvertDate = null;
            row.UpdateTime = DateTime.Now;
            row.UpdateBy = _user.UserCode;
            var updateCols = new List<string>
            {
                nameof(EnterpriseOriginalFile.ConvertStatus),
                nameof(EnterpriseOriginalFile.ConvertMessage),
                nameof(EnterpriseOriginalFile.MarkdownStatus),
                nameof(EnterpriseOriginalFile.MarkdownMessage),
                nameof(EnterpriseOriginalFile.PreviewPdfPath),
                nameof(EnterpriseOriginalFile.MarkdownPath),
                nameof(EnterpriseOriginalFile.ConvertDate),
            };
            // ★ reanalyze=false = 只重转不重识别 ⇒ 识别状态**原样保留**（画像不动就不是「待识别」）。
            //   旧实现无条件置 pending + SkipAnalyze 短路 ⇒ 状态永远停在「待识别」（历史缺陷，随重构修）
            if (reanalyze)
            {
                row.AnalyzeStatus = AnalyzeStatus.Pending;
                row.AnalyzeMessage = "等待重新识别";
                updateCols.Add(nameof(EnterpriseOriginalFile.AnalyzeStatus));
                updateCols.Add(nameof(EnterpriseOriginalFile.AnalyzeMessage));
            }
            updateCols.Add(nameof(EnterpriseOriginalFile.UpdateTime));
            updateCols.Add(nameof(EnterpriseOriginalFile.UpdateBy));
            await _db.UpdateAsync(row, updateCols.ToArray());

            // ★ 单文件队列：编排器内串行跑「转换段 → 分析段」；skipAnalyze 承接 reanalyze=false
            var (qCode, qErr) = await EnqueueFileQueuesAsync(
                row.EnterpriseCode, row.StageCode,
                new List<EnterpriseOriginalFile> { row }, $"file:{fileCode}", skipAnalyze: !reanalyze);

            if (qCode == null)
            {
                // 入队失败 ⇒ 状态已置回 pending，卡在这儿会误导用户，立即回滚状态
                row.ConvertStatus = ConvertStatus.Failed;
                row.MarkdownStatus = ConvertStatus.Failed;
                row.ConvertMessage = $"重新生成入队失败：{qErr}";
                row.MarkdownMessage = row.ConvertMessage;
                await _db.UpdateAsync(row,
                    nameof(EnterpriseOriginalFile.ConvertStatus),
                    nameof(EnterpriseOriginalFile.MarkdownStatus),
                    nameof(EnterpriseOriginalFile.ConvertMessage),
                    nameof(EnterpriseOriginalFile.MarkdownMessage));
                return Result<object?>.Fail($"重新生成入队失败：{qErr}");
            }

            return Result<object?>.Ok(new
            {
                FileCode = fileCode,
                QueueCode = qCode,
                Reanalyze = reanalyze,
                Message = reanalyze ? "已重新入队，完成后会自动重新识别" : "已重新入队（仅重新生成内容，不重新识别）",
            });
        }

        /// <summary>
        /// ★ 取一份文件已生成的 Markdown **文本内容**（供前端「查看提取结果」）。
        ///
        /// <para><b>与 <c>file-markdown/{fileCode}</c> 的区别</b>（两者都要，缺一不可）：</para>
        /// <list type="bullet">
        ///   <item><c>file-markdown/{fileCode}</c> —— 返回<b>文件流</b>（<c>text/markdown</c>），
        ///         用途是<b>下载</b>。</item>
        ///   <item>本方法 —— 返回 <c>{ markdown: string, … }</c> <b>JSON</b>，
        ///         用途是<b>前端直接展示 / 编辑 / 让用户核对提取质量</b>。</item>
        /// </list>
        ///
        /// <para><b>为什么必须有</b>（2026-10-03 核查发现）：专家反馈「Markdown 提取的数据是乱码」时，
        /// 唯一的查看方式是下载文件用外部编辑器打开 —— 页面内看不到内容，就无法判断是提取错了还是显示错了，
        /// 「人工修正标签/作用」也无从核对依据。</para>
        /// </summary>
        public async Task<object> GetMarkdownAsync(string fileCode, string enterpriseCode)
        {
            var err = await OwnershipErrorAsync(enterpriseCode);
            if (err != null) return new { Success = false, Message = err };

            var row = (await _db.Client.Queryable<EnterpriseOriginalFile>()
                .Where(x => x.Code == fileCode && x.EnterpriseCode == enterpriseCode)
                .ToListAsync() ?? new List<EnterpriseOriginalFile>())
                .FirstOrDefault();
            if (row == null) return new { Success = false, Message = "记录不存在或不属于当前工作区" };

            if (string.IsNullOrEmpty(row.MarkdownPath))
                return new
                {
                    Success = false,
                    Message = string.IsNullOrEmpty(row.MarkdownMessage)
                        ? "尚未生成内容（可能还在处理中，或该格式无法自动提取）"
                        : row.MarkdownMessage!,
                    MarkdownStatus = row.MarkdownStatus,
                    MarkdownMessage = row.MarkdownMessage,
                };

            try
            {
                var (stream, _) = await _storage.DownloadAsync(row.MarkdownPath.TrimStart('/'));
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);

                // ★ 必须显式 UTF-8：Markdown 产物是 UTF-8，用平台默认编码读会出乱码
                //   （这正是 2026-10-03 专家报「Markdown 内容乱码」的一类根因；
                //     另一半是下载端点缺 charset，已在 DownloadAsync 里补）。
                var markdown = System.Text.Encoding.UTF8.GetString(ms.ToArray());

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
                _logger.LogError(ex, "[原始资料] 读取 Markdown 失败: {Code}", fileCode);
                return new { Success = false, Message = $"读取内容失败：{ex.Message}" };
            }
        }

        /// <summary>
        /// ★ 队列明细（老板问题 5「只有聚合计数、无排队明细」的补齐）。
        ///
        /// <para>状态条只回答「有多少在跑」，回答不了「<b>排队的还有多少、排在第几、哪一份卡住了</b>」。
        /// 本端点返回该企业该阶段下所有相关队列 + 每个队列的任务明细（TaskType / 状态 / 耗时 / 失败原因）。</para>
        /// </summary>
        public async Task<object> QueueDetailAsync(string enterpriseCode, string stageCode)
        {
            var err = await OwnershipErrorAsync(enterpriseCode);
            if (err != null) return new { Success = false, Message = err, Rows = new object[0] };

            var scopeKey = ScopeKeyOf(enterpriseCode, stageCode);
            var rows = await _db.Client.Queryable<YzhQueue>()
                .Where(q => q.ScopeKey == scopeKey
                            && (q.QueueType == EnterpriseOriginalQueue.QueueTypeFile
                                || q.QueueType == EnterpriseOriginalQueue.QueueTypeIngest
                                || q.QueueType == EnterpriseOriginalQueue.QueueTypeAnalyze))
                .OrderByDescending(q => q.CreateTime)
                .Take(20)
                .ToListAsync() ?? new List<YzhQueue>();

            // ★ `YzhQueue` 有**两个**码：`Code`（BaseEntity 的 GUID 业务键）与 `QueueCode`（`Q-2026...`，
            //   用户看到的那个）。`yzh_queue_task.QueueCode` 关联的是**后者** ——
            //   用 `q.Code` 去匹配必然 0 条 ⇒ 任务明细永远空（2026-10-03 实测踩到）。
            var queueCodes = rows.Select(q => q.QueueCode)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .ToHashSet(StringComparer.Ordinal);

            // ⚠️ 用内存过滤而**不是** `Where(t => queueCodes.Contains(t.QueueCode))`：
            //   SqlSugar 对「闭包捕获的集合 Contains」翻译不可靠，会**静默返回空集**（不报错、日志无痕）
            //   —— 这是本模块第 3 次踩同一个坑（stage-tree / 版本表 / 这里）。队列任务表数据量极小，
            //   全取后在内存里过滤最稳。
            var taskScope = queueCodes.Count == 0
                ? new List<YzhQueueTask>()
                : await _db.Client.Queryable<YzhQueueTask>()
                    .OrderByDescending(t => t.CreateTime)
                    .Take(500)
                    .ToListAsync() ?? new List<YzhQueueTask>();

            var tasks = taskScope.Where(t => t.QueueCode != null && queueCodes.Contains(t.QueueCode)).ToList();

            // 任务 → 文件：⚠️ 三种 payload 形态
            //   · file（当前唯一写入口，2026-10-06 起）：{ "Code": "<文件Code>", ... }
            //   · ingest（历史）：{ "Code": "<文件Code>", ... }
            //   · analyze（历史批次）：{ "BatchCode": "...", "FileCodes": ["<Code1>","<Code2>"] }
            // ⇒ 统一展开成「一个文件一行」，否则批次任务在 UI 上挂不到文件名。
            var fileCodes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var t in tasks)
            {
                var one = ExtractPayloadCode(t.Payload);
                if (!string.IsNullOrWhiteSpace(one)) fileCodes.Add(one!);
                foreach (var c in ExtractPayloadFileCodes(t.Payload)) fileCodes.Add(c);
            }

            // ⚠️ 同样走「全取 + 内存过滤」：SqlSugar 的 Contains 翻译不可靠（本模块第 3 次踩）
            var allFiles = fileCodes.Count == 0
                ? new List<EnterpriseOriginalFile>()
                : await _db.Client.Queryable<EnterpriseOriginalFile>()
                    .Where(f => f.EnterpriseCode == enterpriseCode)
                    .ToListAsync() ?? new List<EnterpriseOriginalFile>();
            var nameByCode = allFiles
                .Where(f => fileCodes.Contains(f.Code ?? ""))
                .GroupBy(f => f.Code!)
                .ToDictionary(g => g.Key, g => g.First().FileName ?? g.Key, StringComparer.Ordinal);

            var detail = rows.Select(q => new
            {
                // ★ 同时给出两个码：`Code` 是业务键，`QueueCode` 是展示号（前端按它关联任务）
                Code = q.QueueCode,
                BizCode = q.Code,
                q.QueueName,
                QueueType = q.QueueType,
                q.Status,
                q.CreateTime,
                q.StartTime,
                q.EndTime,
                // ★ YzhQueue 自带进度字段：已完成/总数 + 各状态计数（老板问题 5 要的「进度」）
                q.TotalCount, q.PendingCount, q.ProcessingCount,
                q.CompletedCount, q.FailedCount, q.CancelledCount, q.Progress,
                // 排队位置：同 ScopeKey 下按创建时间排序的序号（1 = 最先提交）
                QueuePosition = rows.Where(x => x.CreateTime <= q.CreateTime).Count(),
                // ★ 用 q.QueueCode（Q-2026... 展示号）匹配，不是 q.Code（GUID 业务键）
                Tasks = tasks.Where(t => t.QueueCode == q.QueueCode).Select(t => new
                {
                    t.Code,
                    TaskType = t.TaskType,
                    t.Status,
                    // ★ YzhQueueTask 没有 StartTime/EndTime，只有 ProcessTime + 重试字段
                    t.ProcessTime,
                    t.RetryCount,
                    t.MaxRetryCount,
                    t.NextRetryAt,
                    t.ErrorType,
                    Message = Truncate(t.ErrorMessage, 300),
                    FileCode = ExtractPayloadCode(t.Payload),
                    FileName = ExtractPayloadCode(t.Payload) is { } fc && nameByCode.TryGetValue(fc, out var nm)
                        ? nm
                        : ExtractPayloadFileCodes(t.Payload).Where(c => nameByCode.ContainsKey(c))
                            .Select(c => nameByCode[c]).ToList() as object ?? null,
                }).ToList(),
            }).ToList();

            var running = rows.Count(q => q.Status == "running" || q.Status == "processing");

            return new
            {
                Success = true,
                Message = (string?)null,
                RunningCount = running,
                PendingCount = rows.Count(q => q.Status == "pending"),
                FailedCount = rows.Count(q => q.Status == "failed"),
                Rows = detail,
            };
        }

        /// <summary>从批次型 payload（analyze）的 <c>FileCodes</c> 数组取全部文件 Code</summary>
        private static List<string> ExtractPayloadFileCodes(string? payload)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(payload)) return list;
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(payload);
                if (doc.RootElement.TryGetProperty("FileCodes", out var arr) && arr.ValueKind == System.Text.Json.JsonValueKind.Array)
                    foreach (var el in arr.EnumerateArray())
                        if (el.ValueKind == System.Text.Json.JsonValueKind.String && el.GetString() is { } s && s.Length > 0)
                            list.Add(s);
            }
            catch (System.Text.Json.JsonException) { }
            return list;
        }

        /// <summary>从队列 payload 里取文件 Code（两个 payload 形态都带 <c>Code</c>）</summary>
        private static string? ExtractPayloadCode(string? payload)
        {
            if (string.IsNullOrWhiteSpace(payload)) return null;
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(payload);
                return doc.RootElement.TryGetProperty("Code", out var v) ? v.GetString() : null;
            }
            catch (System.Text.Json.JsonException) { return null; }
        }

        /// <summary>状态条：转换中 / 分析中 / 失败 计数 + 该阶段是否有运行中队列</summary>
        public async Task<object> StatusBarAsync(string enterpriseCode, string stageCode)
        {
            var err = await OwnershipErrorAsync(enterpriseCode);
            if (err != null) return new { Success = false, Message = err };

            var rows = await _db.Client.Queryable<EnterpriseOriginalFile>()
                .Where(x => x.EnterpriseCode == enterpriseCode && x.StageCode == stageCode
                            && !x.IsDeleted && x.IsValid == 1)
                .ToListAsync() ?? new List<EnterpriseOriginalFile>();

            var scopeKey = ScopeKeyOf(enterpriseCode, stageCode);

            // ★ 忙碌判据 = 该 scope 下**非终态**队列（2026-10-06 文件级重构后可能同时有 N 个）
            var active = await _db.Client.Queryable<YzhQueue>()
                .Where(q => q.ScopeKey == scopeKey
                            && q.Status != "completed" && q.Status != "failed" && q.Status != "cancelled")
                .OrderByDescending(q => q.CreateTime)
                .ToListAsync() ?? new List<YzhQueue>();
            var running = active.FirstOrDefault();

            // ★ 进度按**文件**聚合：一个文件 = 一个队列 = 一个任务 ⇒ 旧「单队列 TotalCount」
            //   口径会显示成「0 / 1」，毫无意义。文件级口径 = 本次在跑的文件里已到终态的占比。
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
                    ? new List<EnterpriseOriginalFile>()
                    : rows.Where(r => fileCodes.Contains(r.Code ?? "")).ToList();
                if (inFlight.Count > 0)
                {
                    queueTotal = inFlight.Count;
                    queueCompleted = inFlight.Count(IsFileFinished);
                }
                else
                {
                    // 兜底（历史批次队列 / 载荷解析不出文件）：退回任务口径
                    queueTotal = active.Sum(a => a.TotalCount);
                    queueCompleted = active.Sum(a => a.CompletedCount);
                }
            }

            return new
            {
                Success = true,
                Message = (string?)null,
                Total = rows.Count,
                ConvertingCount = rows.Count(r => r.ConvertStatus == ConvertStatus.Converting),
                AnalyzingCount = rows.Count(r => r.AnalyzeStatus == AnalyzeStatus.Analyzing),
                FailedCount = rows.Count(r => r.ConvertStatus == ConvertStatus.Failed || r.AnalyzeStatus == AnalyzeStatus.Failed),
                UnsupportedCount = rows.Count(r => r.ConvertStatus == ConvertStatus.Unsupported
                                                || r.MarkdownStatus == ConvertStatus.Unsupported),
                PendingCount = rows.Count(r => r.AnalyzeStatus == AnalyzeStatus.Pending),
                AnalyzedCount = rows.Count(r => r.AnalyzeStatus == AnalyzeStatus.Analyzed),
                // ★ 填写期可用性（转换 ∧ 分析 双条件）—— 下游取资料必须用这个口径
                UsableCount = rows.Count(IsUsableForFilling),
                HalfProductCount = rows.Count(IsHalfProduct),
                QueueCode = running?.QueueCode,
                QueueStatus = running?.Status,
                // ★ 前端「正在做什么」按类型说人话（旧实现漏了这个字段 ⇒ 标签恒为 else 分支，2026-10-06 修）
                QueueType = running?.QueueType,
                QueueTotal = queueTotal,
                QueueCompleted = queueCompleted,
                QueueProgress = queueTotal > 0 ? (int)Math.Round(queueCompleted * 100d / queueTotal) : 0,
                // ★「有队列在跑」= 忙碌态 ⇒ 前端显示进度横幅 + 3s 轮询（⛔ 不再禁用上传：
                //   文件级队列互不冲突，阶段级拒绝是 2026-10-03 的旧口径，随重构废除）
                IsBusy = active.Count > 0,
            };
        }

        /// <summary>文件是否已到终态（转换段 ∧ 分析段都收尾）——进度聚合的「完成」判据</summary>
        private static bool IsFileFinished(EnterpriseOriginalFile f)
        {
            var convDone = f.ConvertStatus == ConvertStatus.Completed
                        || f.ConvertStatus == ConvertStatus.Failed
                        || f.ConvertStatus == ConvertStatus.Unsupported;
            var anaDone = f.AnalyzeStatus == AnalyzeStatus.Analyzed
                       || f.AnalyzeStatus == AnalyzeStatus.Skipped
                       || f.AnalyzeStatus == AnalyzeStatus.Failed;
            return convDone && anaDone;
        }

        // ========================================================
        // 二、五段式上传
        // ========================================================

        /// <summary>
        /// Step0 <b>plan</b>：预检（类型白名单 / 大小上限 / <b>Sha256 幂等预估</b>），纯计算不落库。
        ///
        /// <para>⚠️ 与 <c>EnterpriseFileService.upload/plan</c> 语义<b>完全不同</b>（那是多标准分发预览）——
        /// 本段是<b>重写</b>不是照抄（36 号 §6.5 勘误）。</para>
        /// </summary>
        public async Task<object> PlanUploadAsync(string enterpriseCode, string stageCode, IList<PlanItemDto> files)
        {
            var err = await OwnershipErrorAsync(enterpriseCode);
            if (err != null) return new { Success = false, Message = err, Rows = new object[0] };
            if (string.IsNullOrWhiteSpace(stageCode)) return new { Success = false, Message = "请先选择认证阶段", Rows = new object[0] };

            var existing = (await _db.GetListAsync<EnterpriseOriginalFile>(
                x => x.EnterpriseCode == enterpriseCode && x.StageCode == stageCode && x.IsValid == 1)).Data
                ?? new List<EnterpriseOriginalFile>();

            var rows = new List<object>();
            var filtered = 0;
            foreach (var f in files ?? new List<PlanItemDto>())
            {
                var name = SanitizeName(f.FileName);
                if (name.Length == 0) continue;

                // ★ 系统噪声（.DS_Store / Thumbs.db / ~$xx.doc / ._xx）⇒ 静默剔除，
                //   不进 Rows、不算 Blocked，只报计数。理由见 IsSystemNoise 注释。
                // ★ 系统噪声静默剔除（.DS_Store / Thumbs.db / ~$xx.doc / ._xx）
                if (UploadFilePolicy.IsSystemNoise(name)) { filtered++; continue; }

                var verdict = UploadFilePolicy.Check(name);
                var ext = verdict.Extension.Length > 0 ? verdict.Extension : Path.GetExtension(name).ToLowerInvariant();
                var folder = NormalizeFolder(f.RelFolderPath);
                var blocked = new List<string>();

                // 格式判定走 UploadFilePolicy 单一事实源（⛔ 不再在本类维护第二份白名单）
                if (!verdict.Ok) blocked.Add(verdict.Message);
                if (f.FileSize > MaxFileSizeBytes)
                    blocked.Add($"超过单文件上限 {MaxFileSizeBytes / 1024 / 1024} MB");
                if (f.FileSize <= 0)
                    blocked.Add("文件内容为空");

                // ★ D7：同路径已有行 ⇒ 比 hash。hash 相同 = 同一文件 = 幂等跳过；不同 = 替换（版本 +1）
                var same = existing.FirstOrDefault(e =>
                    e.RelFolderPath == folder && e.FileName == name && !e.IsDeleted);

                var action = "create";
                if (same != null)
                {
                    if (!string.IsNullOrWhiteSpace(f.Sha256)
                        && string.Equals(same.Sha256, f.Sha256, StringComparison.OrdinalIgnoreCase))
                        action = "skip";      // 幂等：不产生新版本、不重跑 LLM
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
                    // ★ 被静默剔除的系统噪声数量（.DS_Store / Thumbs.db / ~$xx.doc …）
                    FilteredCount = filtered,
                    FilteredNames = filtered > 0
                        ? (files ?? new List<PlanItemDto>())
                            .Where(f => IsSystemNoise(SanitizeName(f.FileName)))
                            .Select(f => SanitizeName(f.FileName)).Distinct().Take(5).ToList()
                        : null,
                },
            };
        }

        /// <summary>
        /// Step1 <b>init</b>：建批次行 + 按 D7 两段式建/命中文件行，下发 <c>TaskId</c> 与建议存储路径。
        /// <para>⚠️ <b>不建草稿行</b>：本模块没有「槽位草稿」概念，行在 init 就建（<c>ConvertStatus='none'</c>），
        /// 字节到位后由 confirm 激活并入队。这样 cancel 只需删对象 + 撤未完成行。</para>
        /// </summary>
        public async Task<(string? Error, object? Data)> UploadInitAsync(
            string enterpriseCode, string stageCode, IList<PlanItemDto> files)
        {
            var err = await OwnershipErrorAsync(enterpriseCode);
            if (err != null) return (err, null);
            if (string.IsNullOrWhiteSpace(stageCode)) return ("请先选择认证阶段", null);

            var now = DateTime.Now;
            var task = new EnterpriseOriginalUploadTask
            {
                Code = Guid.NewGuid().ToString("N"),
                CreateTime = now,
                CreateBy = _user.UserCode,
                EnterpriseCode = enterpriseCode,
                StageCode = stageCode,
                Status = TaskStatus.Draft,
                TotalFiles = files?.Count ?? 0,
                ExpireTime = now.AddMinutes(TaskExpireMinutes),
            };
            var insTask = await _db.InsertAsync(task);
            if (!insTask.Success)
                return ($"创建上传批次失败：{insTask.Error}", null);

            var items = new List<object>();
            foreach (var f in files ?? new List<PlanItemDto>())
            {
                var name = SanitizeName(f.FileName);
                if (name.Length == 0) continue;
                // ★ 绕过 plan 直调 API 的兜底：系统噪声与白名单外一律不写库
                var initVerdict = UploadFilePolicy.Check(name);
                if (!initVerdict.Ok) continue;
                var ext = initVerdict.Extension;
                var folder = NormalizeFolder(f.RelFolderPath);

                var sha = (f.Sha256 ?? "").Trim().ToLowerInvariant();
                var existing = (await _db.Client.Queryable<EnterpriseOriginalFile>()
                    .Where(x => x.EnterpriseCode == enterpriseCode && x.StageCode == stageCode
                                && x.RelFolderPath == folder && x.FileName == name)
                    .ToListAsync() ?? new List<EnterpriseOriginalFile>())
                    .FirstOrDefault(e => !e.IsDeleted);

                if (existing != null && !string.IsNullOrEmpty(sha)
                    && !string.IsNullOrEmpty(existing.Sha256)
                    && string.Equals(existing.Sha256, sha, StringComparison.OrdinalIgnoreCase))
                {
                    // ★ D7 幂等：同 hash ⇒ 完全跳过，不建新行、不入队（省一次 LLM）
                    task.SkipCount++;
                    items.Add(new { FileCode = existing.Code, FileName = name, Action = "skip", StoragePath = existing.StoragePath });
                    continue;
                }

                var storagePath = PathBuilder.EnterpriseOriginalSource(enterpriseCode, stageCode, folder, name);
                string code;

                if (existing != null)
                {
                    // ★ D8 替换：本阶段**只登记「待写入」**，真正归档 + 版本递增在 upload/file 收到字节时做。
                    // ⛔ 这里**绝不能**把前端声称的 sha 写进 Sha256 ——
                    //    Sha256 的语义是「**已落库字节**的指纹」，而字节此刻还没到。
                    //    写进去会让 upload/file 的「同 hash ⇒ 幂等跳过」判定在**首传**时就命中，
                    //    结果文件永远传不上去（2026-10-03 实测踩到）。
                    existing.UploadTaskCode = task.Code;
                    existing.UpdateTime = now;
                    existing.UpdateBy = _user.UserCode;
                    await _db.UpdateAsync(existing,
                        nameof(EnterpriseOriginalFile.UploadTaskCode),
                        nameof(EnterpriseOriginalFile.UpdateTime),
                        nameof(EnterpriseOriginalFile.UpdateBy));
                    code = existing.Code;
                    // 前端已带 hash 时可预判动作，但**不落库**；缺 hash 时一律报 replace 交由 file 步裁决
                    var predict = sha.Length == 64 && string.Equals(existing.Sha256, sha, StringComparison.OrdinalIgnoreCase)
                        ? "skip" : "replace";
                    items.Add(new { FileCode = code, FileName = name, Action = predict, StoragePath = storagePath });
                }
                else
                {
                    var row = new EnterpriseOriginalFile
                    {
                        Code = Guid.NewGuid().ToString("N"),
                        CreateTime = now,
                        CreateBy = _user.UserCode,
                        EnterpriseCode = enterpriseCode,
                        StageCode = stageCode,
                        OrgCode = await ResolveOrgCodeAsync(enterpriseCode),
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
                        AnalyzeStatus = AnalyzeStatus.Pending,
                        AnalyzePolicy = Policy.Analyze,
                        IsValid = 1,
                    };
                    var insRow = await _db.InsertAsync(row);
                    if (!insRow.Success)
                        return ($"保存文件「{name}」失败：{insRow.Error}", null);
                    code = row.Code;
                    items.Add(new { FileCode = code, FileName = name, Action = "create", StoragePath = storagePath });
                }
            }

            task.TotalFiles = items.Count;
            task.Status = TaskStatus.Uploading;
            task.UpdateTime = now;
            task.UpdateBy = _user.UserCode;
            await _db.UpdateAsync(task,
                nameof(EnterpriseOriginalUploadTask.TotalFiles),
                nameof(EnterpriseOriginalUploadTask.SkipCount),
                nameof(EnterpriseOriginalUploadTask.Status),
                nameof(EnterpriseOriginalUploadTask.UpdateTime),
                nameof(EnterpriseOriginalUploadTask.UpdateBy));

            return (null, new
            {
                TaskId = task.Code,
                EnterpriseCode = enterpriseCode,
                StageCode = stageCode,
                Items = items,
                SkipCount = task.SkipCount,
            });
        }

        /// <summary>
        /// Step2 <b>file</b>：逐文件传字节。<b>按 DB 行重算路径</b>（前端不传路径，防伪造）。
        ///
        /// <para>★ D8 替换时序（与 <c>EnterpriseFileService.ReplaceFileAsync</c> 同构）：
        /// <c>Rename(旧 → _archive/{名}.v{n})</c> → <c>Upload(原路径, 新字节)</c>。
        /// 这样 <c>StoragePath</c> 恒定，<b>预览 / 下载 / 语义分析的路径永不失效</b>。</para>
        /// </summary>
        public async Task<(bool Ok, string? Error, object? Data)> UploadFileStepAsync(
            string fileCode, string taskId, Stream stream, long size, string uploadFileName, string sha256)
        {
            var row = (await _db.GetOneIgnoreValidAsync<EnterpriseOriginalFile>(x => x.Code == fileCode)).Data;
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
                var incoming = Path.GetFileName((uploadFileName ?? "").Replace('\\', '/'));
                incoming = SanitizeName(incoming);
                // ⛔ 换文件名 = 换逻辑文件（存储路径不同）⇒ 不允许在本阶段改名，避免行与对象脱节
                if (incoming.Length > 0 && !string.Equals(incoming, name, StringComparison.Ordinal))
                    return (false, $"上传文件名「{incoming}」与登记名「{name}」不一致，请重新发起上传", null);
            }

            // ★ 读进内存以便算 hash（D7 判定的权威依据）
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            var bytes = ms.ToArray();
            if (bytes.Length == 0) return (false, "文件内容为空", null);

            var actualSha = sha256;
            if (actualSha.Length != 64) actualSha = Sha256Hex(bytes);

            // ★ D7 幂等复核：字节算出的 hash 与**已落库版本**一致 ⇒ 不重复写、不产生新版本。
            // ⛔ 必须同时要求「已存 Sha256 非空」—— 空 = 首传还没写字节，
            //    此刻 hash 必然「相同」，判成 skip 就等于文件永远传不上去（2026-10-03 实测踩到）。
            if (!string.IsNullOrEmpty(row.Sha256)
                && string.Equals(row.Sha256, actualSha, StringComparison.OrdinalIgnoreCase))
            {
                return (true, null, new { FileCode = fileCode, FileName = name, Action = "skip", VersionNumber = row.VersionNumber });
            }

            var storagePath = row.StoragePath;
            if (string.IsNullOrEmpty(storagePath))
                storagePath = PathBuilder.EnterpriseOriginalSource(row.EnterpriseCode, row.StageCode, row.RelFolderPath, name);

            // ★ D8：异 hash ⇒ 先把当前活跃版归档，再向同一路径写新字节
            var archivedVersion = 0;
            var isReplace = row.VersionNumber >= 1 && !string.IsNullOrEmpty(row.Sha256);
            if (isReplace)
            {
                archivedVersion = await MaxArchivedVersionAsync(row.Code);
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

                        var insVer = await _db.InsertAsync(new EnterpriseOriginalFileVersion
                        {
                            Code = Guid.NewGuid().ToString("N"),
                            CreateTime = DateTime.Now,
                            CreateBy = _user.UserCode,
                            FileCode = row.Code,
                            EnterpriseCode = row.EnterpriseCode,
                            StageCode = row.StageCode,
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
            row.AnalyzeStatus = AnalyzeStatus.Pending;
            row.AnalyzeMessage = null;
            row.UpdateTime = DateTime.Now;
            row.UpdateBy = _user.UserCode;
            await _db.UpdateAsync(row,
                nameof(EnterpriseOriginalFile.StoragePath),
                nameof(EnterpriseOriginalFile.FileSize),
                nameof(EnterpriseOriginalFile.Sha256),
                nameof(EnterpriseOriginalFile.VersionNumber),
                nameof(EnterpriseOriginalFile.ConvertStatus),
                nameof(EnterpriseOriginalFile.MarkdownStatus),
                nameof(EnterpriseOriginalFile.ConvertMessage),
                nameof(EnterpriseOriginalFile.MarkdownMessage),
                nameof(EnterpriseOriginalFile.ConvertDate),
                nameof(EnterpriseOriginalFile.PreviewPdfPath),
                nameof(EnterpriseOriginalFile.MarkdownPath),
                nameof(EnterpriseOriginalFile.AnalyzeStatus),
                nameof(EnterpriseOriginalFile.AnalyzeMessage),
                nameof(EnterpriseOriginalFile.UpdateTime),
                nameof(EnterpriseOriginalFile.UpdateBy));

            // 替换 ⇒ 旧画像全部标记为非最新（26 号 A-4：画像随文件版本失效）
            if (isReplace) await InvalidateProfileAsync(row.Code);

            if (!string.IsNullOrEmpty(taskId))
            {
                var task = (await _db.GetOneIgnoreValidAsync<EnterpriseOriginalUploadTask>(x => x.Code == taskId)).Data;
                if (task != null)
                {
                    task.SuccessCount++;
                    if (isReplace) task.ReplaceCount++;
                    task.UpdateTime = DateTime.Now;
                    await _db.UpdateAsync(task,
                        nameof(EnterpriseOriginalUploadTask.SuccessCount),
                        nameof(EnterpriseOriginalUploadTask.ReplaceCount),
                        nameof(EnterpriseOriginalUploadTask.UpdateTime));
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
        /// Step3 <b>confirm</b>：激活（<c>IsValid 0→1</c>）+ 入 <c>enterprise_original_ingest</c> 队列。
        /// <para>入队失败<b>不阻断激活</b>（返回 <c>QueueError</c> 文案，前端可提示重试）。</para>
        /// </summary>
        public async Task<(string? Error, object? Data)> UploadConfirmAsync(string taskId, string enterpriseCode)
        {
            var err = await OwnershipErrorAsync(enterpriseCode);
            if (err != null) return (err, null);

            var task = (await _db.GetOneIgnoreValidAsync<EnterpriseOriginalUploadTask>(x => x.Code == taskId)).Data;
            if (task == null) return ("上传批次不存在或已失效", null);
            if (task.Status != TaskStatus.Uploading) return ($"上传批次状态为 {task.Status}，无法确认", null);
            if (task.ExpireTime.HasValue && task.ExpireTime.Value < DateTime.Now)
                return ("上传批次已过期，请重新发起上传", null);

            var rows = await _db.Client.Queryable<EnterpriseOriginalFile>()
                .Where(x => x.UploadTaskCode == taskId && x.EnterpriseCode == enterpriseCode)
                .ToListAsync() ?? new List<EnterpriseOriginalFile>();

            // 字节缺失的行（init 建了但 file 步没成功）不入队
            var activatable = rows
                .Where(r => !string.IsNullOrEmpty(r.StoragePath) && NeedsConversion(r.FileName))
                .ToList();

            var now = DateTime.Now;
            foreach (var row in rows)
            {
                row.IsValid = 1;
                // ⚠️ 不清空 UploadTaskCode：它是**上传批次的追溯列**（init/file/cancel/replace
                //   都按它反查本批次行）。队列侧已不再依赖它 —— 批次语义随 2026-10-06
                //   文件级重构废除（入队快照写进 payload，执行时按 payload 走）。
                row.UpdateTime = now;
                row.UpdateBy = _user.UserCode;
                await _db.UpdateAsync(row,
                    nameof(EnterpriseOriginalFile.IsValid),
                    nameof(EnterpriseOriginalFile.UpdateTime),
                    nameof(EnterpriseOriginalFile.UpdateBy));
            }

            // ★ 文件级闸门（2026-10-06 重构）：⛔ 不再按「阶段忙」拒绝整批上传 —— 同阶段其他
            //   文件在跑与本批次无关（2026-10-03 阶段级拒绝「实测误伤」，随重构废除）。
            //   同文件重传的旧队列由 EnqueueFileQueuesAsync 入队前**取消重建**（用户裁决 ③）。
            string? queueCode = null;
            string? queueError = null;
            if (activatable.Count > 0)
            {
                // 一个文件 = 一个队列 = 一个任务：转换段 + 分析段由 EnterpriseOriginalFileExecutor
                // **串行**跑在同一个任务里 ⇒ 无须再等「整批转换完」补建 analyze（旧抢跑根因已消）。
                // TaskId = task.Code ⇒ UploadCancelAsync 的 CancelBatchAsync(taskId) 仍可整批取消。
                (queueCode, queueError) = await EnqueueFileQueuesAsync(
                    enterpriseCode, task.StageCode, activatable, task.Code);
            }

            task.Status = TaskStatus.Confirmed;
            task.UpdateTime = now;
            task.UpdateBy = _user.UserCode;
            await _db.UpdateAsync(task,
                nameof(EnterpriseOriginalUploadTask.Status),
                nameof(EnterpriseOriginalUploadTask.UpdateTime),
                nameof(EnterpriseOriginalUploadTask.UpdateBy));

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

            var task = (await _db.GetOneIgnoreValidAsync<EnterpriseOriginalUploadTask>(x => x.Code == taskId)).Data;
            if (task == null) return ("上传批次不存在或已失效", null);
            if (task.Status == TaskStatus.Confirmed) return ($"上传批次已确认（{TaskStatus.Confirmed}），无法取消", null);

            await _queueManager.CancelBatchAsync(taskId);

            var rows = await _db.Client.Queryable<EnterpriseOriginalFile>()
                .Where(x => x.UploadTaskCode == taskId && x.EnterpriseCode == enterpriseCode)
                .ToListAsync() ?? new List<EnterpriseOriginalFile>();

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
                    await _db.DeleteByCodeAsync<EnterpriseOriginalFile>(row.Code);
                    deleted++;
                }
                else
                {
                    row.UploadTaskCode = "";
                    await _db.UpdateAsync(row, nameof(EnterpriseOriginalFile.UploadTaskCode));
                }
            }

            task.Status = TaskStatus.Cancelled;
            task.UpdateTime = DateTime.Now;
            task.UpdateBy = _user.UserCode;
            await _db.UpdateAsync(task,
                nameof(EnterpriseOriginalUploadTask.Status),
                nameof(EnterpriseOriginalUploadTask.UpdateTime),
                nameof(EnterpriseOriginalUploadTask.UpdateBy));

            return (null, new { TaskId = taskId, CancelledCount = deleted, SkippedKept = rows.Count - deleted });
        }

        // ========================================================
        // 三、删除 / 版本 / 历史
        // ========================================================

        /// <summary>
        /// 删除（L0 存档层）：软删行 + 删对象（源 + 产物 + 全部 <c>_archive/</c>）。
        /// <para>⚠️ <b>画像保留</b>：<c>cert_enterprise_doc_profile</c> 不删（审计要能查「当时分析成什么」）。</para>
        /// </summary>
        public async Task<Result<object?>> DeleteAsync(string fileCode, string enterpriseCode, string? reason)
        {
            if (string.IsNullOrWhiteSpace(fileCode)) return Result<object?>.Fail("文件业务键 Code 不能为空");

            var row = (await _db.GetOneIgnoreValidAsync<EnterpriseOriginalFile>(x => x.Code == fileCode)).Data;
            if (row == null) return Result<object?>.Fail($"记录不存在：{fileCode}");
            if (!string.Equals(row.EnterpriseCode, enterpriseCode, StringComparison.Ordinal))
                return Result<object?>.Fail("记录不存在或不属于当前工作区");

            var tenantErr = await OwnershipErrorAsync(enterpriseCode);
            if (tenantErr != null) return Result<object?>.Fail(tenantErr);

            var segs = PathBuilder.Segments(row.StoragePath);
            if (segs.Length >= 2) await SafeDeleteAsync(row.StoragePath);
            if (!string.IsNullOrEmpty(row.PreviewPdfPath)) await SafeDeleteAsync(row.PreviewPdfPath);
            if (!string.IsNullOrEmpty(row.MarkdownPath)) await SafeDeleteAsync(row.MarkdownPath);

            // 历史版本：归档路径可由当前路径确定性推导（同一父目录 + _archive/{名}.v{n}）
            if (segs.Length >= 2)
            {
                var versions = await _db.Client.Queryable<EnterpriseOriginalFileVersion>()
                    .Where(x => x.FileCode == fileCode)
                    .ToListAsync() ?? new List<EnterpriseOriginalFileVersion>();
                foreach (var v in versions) await SafeDeleteAsync(v.StoragePath);
                foreach (var v in versions)
                {
                    v.IsDeleted = true;
                    v.DeleteBy = _user.UserCode;
                    v.DeleteTime = DateTime.Now;
                    await _db.UpdateAsync(v,
                        nameof(EnterpriseOriginalFileVersion.IsDeleted),
                        nameof(EnterpriseOriginalFileVersion.DeleteBy),
                        nameof(EnterpriseOriginalFileVersion.DeleteTime));
                }
            }

            row.IsDeleted = true;
            row.DeleteBy = _user.UserCode;
            row.DeleteTime = DateTime.Now;
            row.Remark = string.IsNullOrWhiteSpace(reason) ? row.Remark : Truncate(reason, 500);
            row.UpdateTime = DateTime.Now;
            row.UpdateBy = _user.UserCode;
            await _db.UpdateAsync(row,
                nameof(EnterpriseOriginalFile.IsDeleted),
                nameof(EnterpriseOriginalFile.DeleteBy),
                nameof(EnterpriseOriginalFile.DeleteTime),
                nameof(EnterpriseOriginalFile.Remark),
                nameof(EnterpriseOriginalFile.UpdateTime),
                nameof(EnterpriseOriginalFile.UpdateBy));

            return Result<object?>.Ok(null);
        }

        /// <summary>
        /// ★ <b>批量删除</b>（2026-10-07 新增）—— 页面上「删除整个文件夹」的落点。
        ///
        /// <para><b>为什么必须有一个端点，而不是前端循环调 <see cref="DeleteAsync"/></b>：
        /// 一个文件夹的子树里可能有几十份文件，逐个调用会产生几十次往返；中途某次失败
        /// 还会留下「删了一半」的中间态，而用户界面上看不出到底删掉了哪些。</para>
        ///
        /// <para><b>语义与单个删除完全一致</b>（软删行 + 删对象 + 归档历史版本；
        /// <c>cert_enterprise_doc_profile</c> <b>画像保留</b>以备审计）。
        /// 逐个执行并汇总成败 —— ⛔ 某一份失败<b>不中断</b>其余（与 <see cref="BatchSetPolicyAsync"/> 同款口径），
        /// 否则一个已被别处删掉的行会让整次操作白跑。</para>
        /// </summary>
        public async Task<Result<object?>> BatchDeleteAsync(
            IList<string> fileCodes, string enterpriseCode, string? reason)
        {
            if (fileCodes == null || fileCodes.Count == 0)
                return Result<object?>.Fail("请先选择要删除的文件");

            var tenantErr = await OwnershipErrorAsync(enterpriseCode);
            if (tenantErr != null) return Result<object?>.Fail(tenantErr);

            var ok = 0;
            var failed = new List<string>();
            foreach (var code in fileCodes.Distinct(StringComparer.Ordinal))
            {
                var r = await DeleteAsync(code, enterpriseCode, reason);
                if (r.Success) ok++;
                else failed.Add($"{code}：{r.Error}");
            }

            return Result<object?>.Ok(new
            {
                Total = fileCodes.Count,
                SuccessCount = ok,
                FailedCount = failed.Count,
                Failed = failed.Take(10).ToList(),
            });
        }

        /// <summary>历史版本列表（只追加，倒序）</summary>
        public async Task<object> GetVersionsAsync(string fileCode, string enterpriseCode)
        {
            var err = await OwnershipErrorAsync(enterpriseCode);
            if (err != null) return new { Success = false, Message = err, Rows = new object[0] };

            var row = (await _db.GetOneAsync<EnterpriseOriginalFile>(x => x.Code == fileCode)).Data;
            if (row == null || !string.Equals(row.EnterpriseCode, enterpriseCode, StringComparison.Ordinal))
                return new { Success = false, Message = "记录不存在或不属于当前工作区", Rows = new object[0] };

            var versions = await _db.Client.Queryable<EnterpriseOriginalFileVersion>()
                .Where(x => x.FileCode == fileCode && x.IsValid == 1)
                .ToListAsync() ?? new List<EnterpriseOriginalFileVersion>();

            var list = versions.OrderByDescending(v => v.VersionNumber).Select(v => (object)new
            {
                v.Code,
                v.FileCode,
                v.VersionNumber,
                v.FileName,
                v.FileType,
                v.FileSize,
                v.Sha256,
                v.StoragePath,
                v.Reason,
                CreateTime = v.CreateTime,
                CreateBy = v.CreateBy,
            }).ToList();

            return new
            {
                Success = true,
                Message = (string?)null,
                FileCode = fileCode,
                FileName = row.FileName,
                CurrentVersion = row.VersionNumber,
                Rows = list,
            };
        }

        /// <summary>
        /// 回滚到指定历史版本（36 号 §十 L5）。
        /// <para>动作 = <b>反向替换</b>：把归档版重新变成当前版，当前版被归档 ⇒ 版本号继续递增
        /// （<b>不倒退</b>）。这样版本链永远单调递增，审计可追「谁在什么时候换回了旧版」。</para>
        /// </summary>
        public async Task<Result<object?>> RestoreVersionAsync(
            string fileCode, string enterpriseCode, int versionNumber, string? reason)
        {
            if (versionNumber < 1) return Result<object?>.Fail("版本号从 1 开始");

            var row = (await _db.GetOneIgnoreValidAsync<EnterpriseOriginalFile>(x => x.Code == fileCode)).Data;
            if (row == null) return Result<object?>.Fail($"记录不存在：{fileCode}");

            var tenantErr = await OwnershipErrorAsync(enterpriseCode);
            if (tenantErr != null) return Result<object?>.Fail(tenantErr);
            if (!string.Equals(row.EnterpriseCode, enterpriseCode, StringComparison.Ordinal))
                return Result<object?>.Fail("记录不存在或不属于当前工作区");

            var target = (await _db.Client.Queryable<EnterpriseOriginalFileVersion>()
                .Where(x => x.FileCode == fileCode && x.VersionNumber == versionNumber && x.IsValid == 1)
                .ToListAsync() ?? new List<EnterpriseOriginalFileVersion>())
                .FirstOrDefault();
            if (target == null) return Result<object?>.Fail($"版本 v{versionNumber} 不存在或已删除");

            // 先把当前活跃版归档
            var archivedVersion = await MaxArchivedVersionAsync(fileCode);
            if (archivedVersion <= 0) archivedVersion = row.VersionNumber;

            try
            {
                if (!string.IsNullOrEmpty(row.StoragePath) && await _storage.ExistsAsync(row.StoragePath.TrimStart('/')))
                {
                    var archivePath = PathBuilder.Archive(row.StoragePath, archivedVersion);
                    await _storage.RenameAsync(row.StoragePath.TrimStart('/'), archivePath.TrimStart('/'));
                    var insArch = await _db.InsertAsync(new EnterpriseOriginalFileVersion
                    {
                        Code = Guid.NewGuid().ToString("N"),
                        CreateTime = DateTime.Now,
                        CreateBy = _user.UserCode,
                        FileCode = fileCode,
                        EnterpriseCode = row.EnterpriseCode,
                        StageCode = row.StageCode,
                        VersionNumber = archivedVersion,
                        FileName = row.FileName,
                        FileType = row.FileType,
                        FileSize = row.FileSize,
                        Sha256 = row.Sha256,
                        StoragePath = archivePath,
                        Reason = $"回滚前归档（原 v{row.VersionNumber}）",
                        IsValid = 1,
                    });
                    if (!insArch.Success)
                        _logger.LogError("[Restore] 归档记录写入失败（不影响主流程）: {Code} {Err}", fileCode, insArch.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Restore] 当前版归档失败: {FileCode}", fileCode);
                return Result<object?>.Fail($"归档失败，回滚中止：{ex.Message}");
            }

            // 归档版 → 当前路径（Copy 后保留归档副本：CopyAsync + 不删源）
            try
            {
                await _storage.CopyAsync(target.StoragePath.TrimStart('/'), row.StoragePath.TrimStart('/'));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Restore] 归档件复制失败: {Archive}", target.StoragePath);
                return Result<object?>.Fail($"恢复失败：{ex.Message}");
            }

            row.FileSize = target.FileSize;
            row.Sha256 = target.Sha256;
            row.VersionNumber = archivedVersion + 1;
            row.ConvertStatus = ConvertStatus.Pending;
            row.MarkdownStatus = ConvertStatus.Pending;
            row.ConvertMessage = null;
            row.MarkdownMessage = null;
            row.ConvertDate = null;
            row.PreviewPdfPath = null;
            row.MarkdownPath = null;
            row.AnalyzeStatus = AnalyzeStatus.Pending;
            row.AnalyzeMessage = null;
            row.UploadTaskCode = "";   // ★ 单文件重跑，见 SetPolicyAsync 同名注释
            row.Remark = Truncate($"回滚到 v{versionNumber}：{reason}", 500);
            row.UpdateTime = DateTime.Now;
            row.UpdateBy = _user.UserCode;
            await _db.UpdateAsync(row,
                nameof(EnterpriseOriginalFile.FileSize), nameof(EnterpriseOriginalFile.Sha256),
                nameof(EnterpriseOriginalFile.VersionNumber),
                nameof(EnterpriseOriginalFile.ConvertStatus), nameof(EnterpriseOriginalFile.MarkdownStatus),
                nameof(EnterpriseOriginalFile.ConvertMessage), nameof(EnterpriseOriginalFile.MarkdownMessage),
                nameof(EnterpriseOriginalFile.ConvertDate),
                nameof(EnterpriseOriginalFile.PreviewPdfPath), nameof(EnterpriseOriginalFile.MarkdownPath),
                nameof(EnterpriseOriginalFile.AnalyzeStatus), nameof(EnterpriseOriginalFile.AnalyzeMessage),
                nameof(EnterpriseOriginalFile.Remark),
                nameof(EnterpriseOriginalFile.UpdateTime), nameof(EnterpriseOriginalFile.UpdateBy));

            await InvalidateProfileAsync(fileCode);

            // ★ 单文件队列（先取消该文件旧队列再重建）
            var (_, qErr) = await EnqueueFileQueuesAsync(
                row.EnterpriseCode, row.StageCode, new List<EnterpriseOriginalFile> { row }, $"restore:v{versionNumber}");
            if (qErr != null) _logger.LogWarning("[Restore] 入队失败（不阻断恢复）: {FileCode} {Reason}", fileCode, qErr);

            return Result<object?>.Ok(new { FileCode = fileCode, NewVersionNumber = row.VersionNumber, RestoredFrom = versionNumber });
        }

        // ========================================================
        // 四、分析策略（36 号 §3.5 三层）
        // ========================================================

        /// <summary>
        /// 改分析策略 + 写审计留痕。
        /// <para><b>语义</b>：改完只影响<b>下次重跑</b>；已产出的画像不变（策略是 L2「要不要从它取数」的开关，
        /// 不是文件删除）。要立刻生效用 <see cref="ApplyPolicyAndReanalyzeAsync"/>。</para>
        /// </summary>
        public async Task<Result<object?>> SetPolicyAsync(
            string fileCode, string enterpriseCode, string policy, string? policyReason, bool reanalyze)
        {
            if (policy is not (Policy.Analyze or Policy.Skip or Policy.Ignore))
                return Result<object?>.Fail($"不支持的策略「{policy}」（字典 ANALYZE_POLICY：analyze / skip / ignore）");

            var row = (await _db.GetOneIgnoreValidAsync<EnterpriseOriginalFile>(x => x.Code == fileCode)).Data;
            if (row == null) return Result<object?>.Fail($"记录不存在：{fileCode}");

            var tenantErr = await OwnershipErrorAsync(enterpriseCode);
            if (tenantErr != null) return Result<object?>.Fail(tenantErr);
            if (!string.Equals(row.EnterpriseCode, enterpriseCode, StringComparison.Ordinal))
                return Result<object?>.Fail("记录不存在或不属于当前工作区");

            await WritePolicyAsync(row, policy, policyReason, "manual");

            if (!reanalyze) return Result<object?>.Ok(new { FileCode = fileCode, AnalyzePolicy = row.AnalyzePolicy });

            // 应用策略并重算：analyze → 入 ingest（重转）+ analyze 链；skip/ignore → 只标 skipped
            if (policy == Policy.Analyze)
            {
                row.ConvertStatus = ConvertStatus.Pending;
                row.MarkdownStatus = ConvertStatus.Pending;
                row.ConvertMessage = null;
                row.MarkdownMessage = null;
                row.AnalyzeStatus = AnalyzeStatus.Pending;
                row.AnalyzeMessage = null;
                // ★ 置空批次号：单文件重跑的追溯（队列不再按批次反查，此列仅供上传链路用）
                row.UploadTaskCode = "";
                row.UpdateTime = DateTime.Now;
                await _db.UpdateAsync(row,
                    nameof(EnterpriseOriginalFile.ConvertStatus),
                    nameof(EnterpriseOriginalFile.MarkdownStatus),
                    nameof(EnterpriseOriginalFile.ConvertMessage),
                    nameof(EnterpriseOriginalFile.MarkdownMessage),
                    nameof(EnterpriseOriginalFile.AnalyzeStatus),
                    nameof(EnterpriseOriginalFile.AnalyzeMessage),
                    nameof(EnterpriseOriginalFile.UploadTaskCode),
                    nameof(EnterpriseOriginalFile.UpdateTime));

                var (_, qErr) = await EnqueueFileQueuesAsync(
                    row.EnterpriseCode, row.StageCode, new List<EnterpriseOriginalFile> { row }, "policy:analyze");
                if (qErr != null) return Result<object?>.Fail($"策略已保存，但入队失败：{qErr}");
            }
            else
            {
                row.AnalyzeStatus = AnalyzeStatus.Skipped;
                row.AnalyzeMessage = $"人工设定策略为 {policy}";
                row.UpdateTime = DateTime.Now;
                await _db.UpdateAsync(row,
                    nameof(EnterpriseOriginalFile.AnalyzeStatus),
                    nameof(EnterpriseOriginalFile.AnalyzeMessage),
                    nameof(EnterpriseOriginalFile.UpdateTime));
            }

            return Result<object?>.Ok(new { FileCode = fileCode, AnalyzePolicy = row.AnalyzePolicy, Reanalyzed = true });
        }

        /// <summary>批量设置策略 + 可选重算（36 号 §八 T4.1）</summary>
        public async Task<Result<object?>> BatchSetPolicyAsync(
            IList<string> fileCodes, string enterpriseCode, string policy, string? policyReason, bool reanalyze)
        {
            if (fileCodes == null || fileCodes.Count == 0) return Result<object?>.Fail("请先选择文件");

            var tenantErr = await OwnershipErrorAsync(enterpriseCode);
            if (tenantErr != null) return Result<object?>.Fail(tenantErr);

            var ok = 0;
            var failed = new List<string>();
            foreach (var code in fileCodes)
            {
                var r = await SetPolicyAsync(code, enterpriseCode, policy, policyReason, reanalyze);
                if (r.Success) ok++;
                else failed.Add($"{code}：{r.Error}");
            }

            return Result<object?>.Ok(new
            {
                Total = fileCodes.Count,
                SuccessCount = ok,
                FailedCount = failed.Count,
                Failed = failed.Take(10).ToList(),
            });
        }

        // ========================================================
        // 五、画像读取与人工修正（D6）
        // ========================================================

        /// <summary>
        /// 取该文件最新画像（语义结果 + 策略）。
        /// <para>★ <b>M6</b>：<paramref name="standardCode"/> 指定「哪个标准下的画像」；
        /// 空 = 兼容旧前端（取版本号最大的一行，可能属于任意标准）。</para>
        /// </summary>
        public async Task<object> GetProfileAsync(string fileCode, string enterpriseCode, string? standardCode = null)
        {
            var err = await OwnershipErrorAsync(enterpriseCode);
            if (err != null) return new { Success = false, Message = err };

            var std = (standardCode ?? "").Trim();
            var p = (await _db.Client.Queryable<EnterpriseDocProfile>()
                .Where(x => x.OriginalFileCode == fileCode && x.IsLatest && x.IsValid == 1
                            && (std == "" || x.StandardCode == std))
                .ToListAsync() ?? new List<EnterpriseDocProfile>())
                .OrderByDescending(x => x.ProfileVersion).FirstOrDefault();

            if (p == null) return new { Success = false, Message = "该文件尚无画像（可能策略为跳过/忽略，或分析未完成）" };

            // ★ M6：一并回该文件**有画像的全部标准**，前端标准切换器据此渲染
            var standards = (await _db.Client.Queryable<EnterpriseDocProfile>()
                    .Where(x => x.OriginalFileCode == fileCode && x.IsLatest && x.IsValid == 1)
                    .ToListAsync() ?? new List<EnterpriseDocProfile>())
                .Select(x => x.StandardCode)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            return new { Success = true, Message = (string?)null, Profile = p, Standards = standards };
        }

        /// <summary>
        /// 人工修正画像（D6 全量编辑：标签 / 作用四段 / InfoItems / 策略）。
        /// <para>修正写<b>新版本</b>（<c>ProfileVersion+1</c>、旧版 <c>IsLatest=0</c>）而不是原地改 ——
        /// 「谁在什么时候把标签从 A 改成 B」必须可审计（AI 建议值本身也要留痕对比）。</para>
        /// <para>★ <b>M6</b>：<c>dto.StandardCode</c> 指定改的是<b>哪个标准</b>那一行
        /// （多行画像下不指定就只能改到版本号最大的一行 = 可能不是用户在看的那一行）。</para>
        /// </summary>
        public async Task<Result<object?>> CorrectProfileAsync(CorrectProfileDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.FileCode)) return Result<object?>.Fail("文件业务键 Code 不能为空");

            var tenantErr = await OwnershipErrorAsync(dto.EnterpriseCode ?? "");
            if (tenantErr != null) return Result<object?>.Fail(tenantErr);

            var std = (dto.StandardCode ?? "").Trim();
            var latest = (await _db.Client.Queryable<EnterpriseDocProfile>()
                .Where(x => x.OriginalFileCode == dto.FileCode && x.IsLatest && x.IsValid == 1
                            && (std == "" || x.StandardCode == std))
                .ToListAsync() ?? new List<EnterpriseDocProfile>())
                .OrderByDescending(x => x.ProfileVersion).FirstOrDefault();
            if (latest == null)
                return Result<object?>.Fail(std == ""
                    ? "该文件尚无画像，无法修正"
                    : $"该文件在标准 {std} 下尚无画像，无法修正");

            // 标签值必须 ∈ cert_tag_dict.TagCode，否则标签漂移 → 召回退化（36 号 R6）
            if (!string.IsNullOrWhiteSpace(dto.TagsJson))
            {
                var valid = await _db.Client.Queryable<TagDict>().Where(x => x.IsValid == 1).ToListAsync() ?? new List<TagDict>();
                var codes = valid.Select(t => t.TagCode).Where(c => !string.IsNullOrWhiteSpace(c))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var bad = ExtractTagCodes(dto.TagsJson).Where(c => !codes.Contains(c)).ToList();
                if (bad.Count > 0)
                    return Result<object?>.Fail($"标签值不在受控字典内：{string.Join("、", bad)}（只能从 cert_tag_dict 选）");
            }

            var next = new EnterpriseDocProfile
            {
                Code = Guid.NewGuid().ToString("N"),
                CreateTime = DateTime.Now,
                CreateBy = _user.UserCode,
                OriginalFileCode = latest.OriginalFileCode,
                EnterpriseCode = latest.EnterpriseCode,
                OrgCode = latest.OrgCode,
                StageCode = latest.StageCode,
                StandardCode = latest.StandardCode,
                FileName = latest.FileName,
                ProfileVersion = latest.ProfileVersion + 1,
                IsLatest = true,
                DetectSource = "manual",
                PromptCode = latest.PromptCode,
                PromptVersion = latest.PromptVersion,
                ModelName = latest.ModelName,
                SourceMarkdownPath = latest.SourceMarkdownPath,
                ProfileStatus = latest.ProfileStatus,
                DocCategory = latest.DocCategory,
                Summary = latest.Summary,
                Keywords = latest.Keywords,
                SuggestedStandardCodes = latest.SuggestedStandardCodes,
                Confidence = latest.Confidence,
                IsManualCorrected = true,
                CorrectedBy = _user.UserCode,
                CorrectedTime = DateTime.Now,
                IsValid = 1,
                // 只覆盖用户本次提交的字段；未提交的沿用上一版
                TagsJson = dto.TagsJson ?? latest.TagsJson,
                TagsSource = dto.TagsJson != null ? "manual" : latest.TagsSource,
                TagsReason = dto.TagsReason ?? latest.TagsReason,
                TagsConfidence = dto.TagsConfidence ?? latest.TagsConfidence,
                DocPurpose = dto.DocPurpose ?? latest.DocPurpose,
                DocPurposeSource = dto.DocPurpose != null ? "manual" : latest.DocPurposeSource,
                DocPurposeConfidence = dto.DocPurposeConfidence ?? latest.DocPurposeConfidence,
                InfoItemsJson = dto.InfoItemsJson ?? latest.InfoItemsJson,
                FieldsJson = dto.FieldsJson ?? latest.FieldsJson,
                TablesJson = dto.TablesJson ?? latest.TablesJson,
            };

            latest.IsLatest = false;
            await _db.UpdateAsync(latest, nameof(EnterpriseDocProfile.IsLatest));
            await _db.InsertAsync(next);

            // 策略同步（若本次也改了策略）
            var row = (await _db.GetOneIgnoreValidAsync<EnterpriseOriginalFile>(x => x.Code == dto.FileCode)).Data;
            if (row != null && !string.IsNullOrWhiteSpace(dto.AnalyzePolicy))
                await WritePolicyAsync(row, dto.AnalyzePolicy, dto.PolicyReason, "manual");

            return Result<object?>.Ok(new
            {
                FileCode = dto.FileCode,
                ProfileVersion = next.ProfileVersion,
                IsLatest = true,
            });
        }

        // ========================================================
        // 六、下载 / 预览
        // ========================================================

        /// <summary>
        /// 下载（含预览产物、历史版本）。<b>双重校验</b>：① <c>DocumentLibraryPath.IsAllowedStoragePath</c>
        /// ② <b>首段必须是 <c>enterprise-original-source</c></b>（36 号 §4.2 表③ 的第二道闸）。
        /// </summary>
        public async Task<(string? Error, Stream? Stream, string? ContentType, string? FileName)> DownloadAsync(
            string storagePath, string? enterpriseCode)
        {
            if (string.IsNullOrWhiteSpace(storagePath)
                || !DocumentLibraryPath.IsAllowedStoragePath(storagePath))
                return ("非法的文件路径", null, null, null);

            var segs = PathBuilder.Segments(storagePath);
            if (segs.Length < 2
                || !segs[0].Equals(DocumentLibraryPath.EnterpriseOriginalSourcePrefix, StringComparison.OrdinalIgnoreCase))
                return ("仅支持企业原始资料库文件下载", null, null, null);

            // 第 2 段 = 企业 Code
            var owner = enterpriseCode;
            if (string.IsNullOrWhiteSpace(owner)) owner = segs[1];
            var tenantErr = await OwnershipErrorAsync(owner);
            if (tenantErr != null) return (tenantErr, null, null, null);

            var name = segs[^1];
            try
            {
                var (stream, contentType) = await _storage.DownloadAsync(storagePath.TrimStart('/'));

                // ★★ 文本类 MIME 必须显式补 `charset=utf-8`（2026-10-03 实测踩到）
                //   症状：Markdown/CSV/TXT 下载下来在浏览器里是「浣撶郴璁よ瘉」这类乱码。
                //   根因：MinIO 存的 ContentType 只有 `text/markdown`、**没有 charset**，
                //         浏览器只能退回系统默认编码 —— 中文环境多为 GBK ⇒ UTF-8 字节被当 GBK 解读。
                //   ⚠️ 这是**全平台所有文本下载**的通病，不是本模块独有；
                //      修在这里是因为本模块是第一个把 Markdown 暴露给前端看的。
                if (!string.IsNullOrEmpty(contentType)
                    && contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
                    && !contentType.Contains("charset", StringComparison.OrdinalIgnoreCase))
                {
                    contentType = contentType + "; charset=utf-8";
                }

                return (null, stream, contentType, name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Download] 对象读取失败: {Path}", storagePath);
                return ($"文件读取失败：{ex.Message}", null, null, null);
            }
        }

        /// <summary>预览 PDF 产物（无产物时回落源文件：PDF/图片透传场景）</summary>
        public async Task<string?> GetPreviewPathAsync(string fileCode, string enterpriseCode, bool markdown)
        {
            var row = (await _db.GetOneAsync<EnterpriseOriginalFile>(x => x.Code == fileCode)).Data;
            if (row == null || !string.Equals(row.EnterpriseCode, enterpriseCode, StringComparison.Ordinal))
                return null;

            var path = markdown ? row.MarkdownPath : row.PreviewPdfPath;
            return string.IsNullOrEmpty(path) ? row.StoragePath : path;
        }

        // ========================================================
        // 七、内部辅助
        // ========================================================

        /// <summary>工作区守卫：一处收口，所有公开方法第一行调用</summary>
        private async Task<string?> OwnershipErrorAsync(string enterpriseCode)
        {
            if (string.IsNullOrWhiteSpace(enterpriseCode)) return "企业编码不能为空";

            var ws = _workspace.Resolve(_user.UserCode);
            if (!ws.Success || ws.Data == null) return ws.Error ?? "无法定位当前工作区";

            var ent = await _db.GetOneAsync<Enterprise>(x => x.Code == enterpriseCode);
            if (ent.Data == null) return "企业不存在或不属于当前工作区";
            if (!string.Equals(ent.Data.OrgCode, ws.Data.Code, StringComparison.Ordinal))
                return "企业不存在或不属于当前工作区";

            return null;
        }

        private async Task<string> ResolveOrgCodeAsync(string enterpriseCode)
        {
            var ent = await _db.GetOneAsync<Enterprise>(x => x.Code == enterpriseCode);
            return ent.Data?.OrgCode ?? "";
        }

        /// <summary>写策略 + 审计留痕（36 号 §八 T4.2）</summary>
        private async Task WritePolicyAsync(EnterpriseOriginalFile row, string policy, string? reason, string source)
        {
            row.AnalyzePolicy = policy;
            row.PolicyReason = Truncate(reason ?? "", 200);
            row.PolicySource = source;
            row.PolicyDecidedBy = _user.UserCode;
            row.PolicyDecidedTime = DateTime.Now;
            row.UpdateTime = DateTime.Now;
            row.UpdateBy = _user.UserCode;
            await _db.UpdateAsync(row,
                nameof(EnterpriseOriginalFile.AnalyzePolicy),
                nameof(EnterpriseOriginalFile.PolicyReason),
                nameof(EnterpriseOriginalFile.PolicySource),
                nameof(EnterpriseOriginalFile.PolicyDecidedBy),
                nameof(EnterpriseOriginalFile.PolicyDecidedTime),
                nameof(EnterpriseOriginalFile.UpdateTime),
                nameof(EnterpriseOriginalFile.UpdateBy));
        }

        /// <summary>文件换版 ⇒ 旧画像全部置为非最新（26 号 A-4）</summary>
        private async Task InvalidateProfileAsync(string fileCode)
        {
            var profiles = await _db.Client.Queryable<EnterpriseDocProfile>()
                .Where(x => x.OriginalFileCode == fileCode && x.IsLatest)
                .ToListAsync() ?? new List<EnterpriseDocProfile>();
            foreach (var p in profiles)
            {
                p.IsLatest = false;
                await _db.UpdateAsync(p, nameof(EnterpriseDocProfile.IsLatest));
            }
        }

        private async Task<int> MaxArchivedVersionAsync(string fileCode)
        {
            var r = await _db.Client.Queryable<EnterpriseOriginalFileVersion>()
                .Where(x => x.FileCode == fileCode)
                .MaxAsync(x => (int?)x.VersionNumber);
            return r ?? 0;
        }

        /// <summary>
        /// ★ <b>按文件建队列</b>（2026-10-06 队列重构的<b>唯一入队口</b>）：
        /// 一个文件 = 一个队列 = 一个任务（<c>enterprise_original_file</c>），
        /// 任务内由 <see cref="EnterpriseOriginalFileExecutor"/> <b>串行</b>跑「转换段 → 分析段」。
        ///
        /// <para><b>同文件重入 = 取消旧队列再重建</b>（用户裁决 ③）：入队前先
        /// <see cref="CancelActiveQueuesOfFileAsync"/> 收掉该文件的活跃队列（含旧批次 ingest
        /// 持有的资源锁），否则 <c>yzh_queue_resource_lock.uk_active</c>（Table|Code 唯一）直接冲突。
        /// ⛔ <b>不再做阶段级 ScopeKey 互斥</b>：一个文件在跑不该拦住同阶段的其他文件
        /// （旧批次闸门「实测误伤」，已随重构废除）。</para>
        ///
        /// <para><b>★ <c>TaskId</c> = 触发标识 <paramref name="sourceTag"/></b>：confirm 传上传批次 Code
        /// ⇒ <see cref="UploadCancelAsync"/> 的 <c>CancelBatchAsync(taskId)</c>（框架按 TaskId 精确匹配）
        /// 仍能整批取消；其余入口传 <c>file:{Code}</c> / <c>restore:v{n}</c> / <c>policy:analyze</c>。</para>
        /// </summary>
        /// <param name="skipAnalyze">true = 只重转不重识别（「重新生成」勾掉「重新识别」）</param>
        private async Task<(string? QueueCode, string? Error)> EnqueueFileQueuesAsync(
            string enterpriseCode, string stageCode, List<EnterpriseOriginalFile> files, string sourceTag,
            bool skipAnalyze = false)
        {
            if (files.Count == 0) return (null, null);

            var scopeKey = ScopeKeyOf(enterpriseCode, stageCode);
            var standardCodes = await ResolveStageStandardCodesAsync(enterpriseCode, stageCode);
            var created = new List<string>();
            string? firstErr = null;

            foreach (var f in files)
            {
                await CancelActiveQueuesOfFileAsync(f.Code, f.FileName);

                var (ok, err, code, _) = await _queueManager.CreateQueueAsync(new QueueManager.CreateQueueRequest
                {
                    QueueType = EnterpriseOriginalQueue.QueueTypeFile,
                    QueueName = $"企业原始资料处理 - {Truncate(f.FileName, 80)}",
                    ScopeKey = scopeKey,
                    SourceType = "enterprise_original",
                    // uk_source(SourceType, SourceId) 唯一：文件 Code + 毫秒时间戳 ⇒ 同文件多轮不撞
                    SourceId = $"file:{f.Code}@{DateTime.Now:yyyyMMddHHmmssfff}",
                    // 持文件锁：转换段独占该行；取消/完成时由框架统一释放
                    ResourceLocks = new List<QueueManager.ResourceLockItem>
                    {
                        new() { ResourceTable = ResourceTable, ResourceCode = f.Code, ResourceName = f.FileName },
                    },
                    Tasks = new List<QueueManager.TaskItem>
                    {
                        new()
                        {
                            TaskType = EnterpriseOriginalQueue.TaskTypeFile,
                            TaskId = sourceTag,
                            Payload = JsonSerializer.Serialize(new EnterpriseOriginalFilePayload
                            {
                                Code = f.Code,
                                EnterpriseCode = f.EnterpriseCode,
                                StageCode = f.StageCode,
                                BatchCode = sourceTag,
                                SkipAnalyze = skipAnalyze,
                                StandardCodes = standardCodes,
                            }),
                        },
                    },
                });

                if (ok && !string.IsNullOrEmpty(code))
                {
                    created.Add(code);
                }
                else
                {
                    firstErr ??= err ?? "队列创建失败";
                    // 并发下「别人刚给这个文件建好」表现为唯一约束冲突 ⇒ 已有队列在跑，不当失败
                    if (IsDuplicateKeyError(err))
                        _logger.LogInformation("[EnqueueFile] 并发下已有队列（唯一约束拦截）: {File}", f.Code);
                    else
                        _logger.LogWarning("[EnqueueFile] 队列创建失败: {File} {Reason}", f.Code, err);
                }
            }

            if (created.Count == 0) return (null, firstErr ?? "队列创建失败");
            _logger.LogInformation("[EnqueueFile] {Scope} 入队 {Ok}/{Total} 个文件队列（{Tag}）",
                scopeKey, created.Count, files.Count, sourceTag);
            return (created[0], files.Count == created.Count ? null : firstErr);
        }

        /// <summary>
        /// 取消该文件的<b>所有活跃队列</b>（同文件重传 / 重新生成 / 回滚 / 改策略 = 先取消旧的再重建）。
        /// <para>① 按<b>资源锁</b>找：覆盖旧 <c>enterprise_original_ingest</c> 批次队列（它们持文件锁）；
        /// ② 按 <c>SourceId</c> 前缀 <c>file:{Code}@</c> 兜底：文件级队列锁已释放但队列仍活跃的窗口。</para>
        /// </summary>
        private async Task CancelActiveQueuesOfFileAsync(string fileCode, string fileName)
        {
            var hit = await _queueManager.FindResourceLockAsync(ResourceTable, new List<string> { fileCode });
            if (hit != null && !string.IsNullOrEmpty(hit.QueueCode))
            {
                var (ok, err) = await _queueManager.CancelQueueAsync(hit.QueueCode);
                _logger.LogInformation("[CancelOld] 文件 {File} 持锁队列 {Queue} 取消={Ok} {Reason}",
                    fileName, hit.QueueCode, ok, err);
            }

            var qs = await _db.Client.Queryable<YzhQueue>()
                .Where(q => q.QueueType == EnterpriseOriginalQueue.QueueTypeFile
                            && q.SourceId != null
                            && q.SourceId.StartsWith($"file:{fileCode}@")
                            && q.Status != "completed" && q.Status != "failed" && q.Status != "cancelled")
                .ToListAsync() ?? new List<YzhQueue>();
            foreach (var q in qs)
            {
                var (ok, err) = await _queueManager.CancelQueueAsync(q.QueueCode);
                _logger.LogInformation("[CancelOld] 文件 {File} 前缀队列 {Queue} 取消={Ok} {Reason}",
                    fileName, q.QueueCode, ok, err);
            }
        }

        /// <summary>
        /// 解析「企业 × 阶段」关联的<b>标准快照</b>（GUID 列表），入队时固化进载荷。
        /// <para>★ M6 语义：doc_group / doc_content 提示词按标准绑定 ⇒ 必须按标准各分析一次。
        /// 入队时快照，执行时不再回查 —— 排队期间改了阶段-标准关联，按入队时的口径跑。</para>
        /// </summary>
        private async Task<List<string>> ResolveStageStandardCodesAsync(string enterpriseCode, string stageCode)
        {
            var list = (await _db.Client.Queryable<CertEnterpriseStage>()
                    .Where(x => x.EnterpriseCode == enterpriseCode && x.StageCode == stageCode
                                && x.IsValid == 1 && !x.IsDeleted)
                    .ToListAsync() ?? new List<CertEnterpriseStage>())
                .Select(x => x.StandardCode)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c!)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (list.Count == 0)
                _logger.LogWarning("[EnqueueFile] {Scope} 未关联任何标准，分析段将按执行侧自动解析",
                    ScopeKeyOf(enterpriseCode, stageCode));
            return list;
        }

        /// <summary>错误信息里是否含「唯一约束 / Duplicate entry」——并发建队列时的正常竞态</summary>
        private static bool IsDuplicateKeyError(string? err)
        {
            if (string.IsNullOrWhiteSpace(err)) return false;
            return err.Contains("Duplicate entry", StringComparison.OrdinalIgnoreCase)
                || err.Contains("唯一约束", StringComparison.OrdinalIgnoreCase)
                || err.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>队列资源锁表名（框架的 <c>RESOURCE_FILE</c> 指向标准目录表，本模块用自己的一张）</summary>
        public const string ResourceTable = "cert_enterprise_original_file";

        public static string ScopeKeyOf(string enterpriseCode, string stageCode) => $"{enterpriseCode}@{stageCode}";

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

        private static bool NeedsConversion(string? fileName)
        {
            var ext = Path.GetExtension(fileName ?? "").ToLowerInvariant();
            if (string.IsNullOrEmpty(ext)) return false;
            return !new[] { ".ds_store", ".tmp", ".temp" }.Contains(ext.TrimStart('.'));
        }

        /// <summary>文件名字段清洗（与 <c>PathBuilder.Sanitize</c> 同口径：只去路径分隔符）</summary>
        internal static string SanitizeName(string? name)
            => (name ?? "").Replace("/", "").Replace("\\", "").Trim();

        /// <summary>相对文件夹路径归一（去首尾 / 、去空段、去穿越片段）；⛔ 不得含保留段名</summary>
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

        private static string Truncate(string s, int max)
            => string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s[..max]);

        private static List<string> ExtractTagCodes(string tagsJson)
        {
            var list = new List<string>();
            try
            {
                using var doc = JsonDocument.Parse(tagsJson);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    foreach (var el in doc.RootElement.EnumerateArray())
                    {
                        if (el.ValueKind == JsonValueKind.Object
                            && el.TryGetProperty("tagCode", out var c))
                            list.Add(c.GetString() ?? "");
                    }
            }
            catch (JsonException) { /* 非法 JSON 交由后端 Schema 校验报错 */ }
            return list.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
        }
    }
}
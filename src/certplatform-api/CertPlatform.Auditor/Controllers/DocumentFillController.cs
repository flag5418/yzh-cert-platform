using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Controllers;
using YZH.Core.Api.Services;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Helpers;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Config;
using YZH.Core.Stand.Models.Result;
using CertPlatform.Auditor.Services;
using CertPlatform.Admin.Entities.Cert;
using CertPlatform.Shared.Fill;

namespace CertPlatform.Auditor.Controllers;

/// <summary>
/// 文档填充控制器（专家端 —— 「我们系统有什么能力」的直接证据）
///
/// <para><b>★ 这个端点的产品意义</b>（用户 2026-10-02 定位）：</para>
/// <para>系统当前<b>不是</b>一个完整可交付的产品，NC 与报告亦然。它的用途是
/// <b>把「我们想做什么」用可运行的程序讲清楚</b>，让体系认证机构与审核专家看懂系统能力，
/// 待真正实施时再补细节。因此本端点刻意做成<b>「输入一家真实企业，输出一份真实成文」</b> ——
/// 不是一张功能列表，而是一次可复现的演示。</para>
///
/// <para><b>★ 它演示的 4 项能力</b>（一次调用全部覆盖）：</para>
/// <list type="table">
///   <listheader><term>能力</term><description>在返回里怎么看</description></listheader>
///   <item><term>全局参数</term><description><c>report.byKind.global</c> + 正文里企业名/信用代码/地址被填入</description></item>
///   <item><term>替换</term><description><c>report.byKind.replace</c> + 正文里 <c>{{enterprise.*}}</c> 被填入</description></item>
///   <item><term>页眉页脚</term><description><c>header</c> / <c>footer</c> 字段（每页重复的编号/版本/页码）</description></item>
///   <item><term>AI 生成</term><description><c>report.byKind.ai</c> + 质量方针等段落：已生成则填入，未生成则进待办</description></item>
/// </list>
///
/// <para><b>★ 诚实边界</b>：本端点<b>不</b>直连大模型（理由见 <c>AiGenerateResolver</c> 注释）。
/// AI 项的取值来自参数表 —— 即「谁生成的不重要，重要的是生成结果可被人工复核、可被文档复用」。
/// 这是「AI 辅助、人负责」的架构前提，也是 05 册 <c>21</c> 号把本系统定位为
/// <b>辅助系统、非正式报告</b>的技术落地。</para>
///
/// <para><b>路由前缀</b>：<c>/api/Auditor/DocumentFill</c></para>
/// </summary>
[ApiController]
[Route("api/Auditor/[controller]")]
public class DocumentFillController : YzhControllerBase<FillParamValue>
{
    private readonly IDbOrm _db;
    private readonly WorkspaceContextService _workspace;
    private readonly DocumentFillEngine _engine = new();

    public DocumentFillController(
        EntityService<FillParamValue> entityService,
        IUserContext userContext,
        IDbOrm db,
        WorkspaceContextService workspace)
        : base(entityService, userContext)
    {
        _db = db;
        _workspace = workspace;
    }

    protected override bool StrictConfigLoad => false;

    protected override EntityConfig LoadConfig() => EntityConfigHelper.GetConfig<FillParamValue>();

    // ════════════════════════════════════════════════════════════════════
    // 一、能力清单 —— 「我们支持哪些填充能力」
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// ★ 填充能力清单 + 锚点语法参考 + 内置演示模板。
    /// <para>前端「能力演示」页首屏调它，把「系统能做什么」一次性讲清楚。</para>
    /// </summary>
    [HttpGet("capabilities")]
    public IActionResult Capabilities()
    {
        var caps = _engine.Capabilities.Select(c => new
        {
            kind = c.Kind,
            name = c.DisplayName,
            order = c.Order,
            syntax = SyntaxOf(c.Kind),
            meaning = MeaningOf(c.Kind),
            example = ExampleOf(c.Kind),
        }).ToList();

        return Ok(ApiResponse<object>.Ok(new
        {
            capabilities = caps,
            demoTemplate = DemoBody,
            demoHeader = DemoHeader,
            demoFooter = DemoFooter,
            anchorPattern = FillSyntax.TokenPattern,
        }));
    }

    // ════════════════════════════════════════════════════════════════════
    // 二、★ 填充预览 —— 核心端点
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// ★ <b>填充预览</b>：给一家真实企业 + 一份模板 → 输出成文 + 证据报告。
    ///
    /// <para><b>模板来源</b>：<c>template</c> 留空则用内置演示模板（质量手册封面 + 颁布令 + 方针目标章节），
    /// 该模板刻意覆盖全部 4 类锚点，用于「能力演示」；传入自定义模板则用于真实文档试跑。</para>
    ///
    /// <para><b>返回值</b>：<c>output</c>（正文）、<c>header</c> / <c>footer</c>（页眉页脚）、
    /// <c>report</c>（完成度 + 按能力分布 + 待办清单）。前端直接把 <c>output</c> 渲染成预览，
    /// 把 <c>report</c> 渲染成右侧「证据摘要」。</para>
    /// </summary>
    [HttpPost("preview")]
    public async Task<IActionResult> Preview([FromBody] DemoPreviewRequest req)
    {
        var scope = await _workspace.ResolveScopeAsync(UserContext.UserCode);
        if (!scope.Success || scope.Data == null)
            return Ok(ApiResponse<object>.Fail(scope.Error ?? "无法定位当前工作区"));

        if (req == null || string.IsNullOrWhiteSpace(req.EnterpriseCode))
            return Ok(ApiResponse<object>.Fail("请先选择企业"));

        var ent = await _db.Client.Queryable<Enterprise>()
            .Where(x => x.Code == req.EnterpriseCode && x.IsDeleted == false)
            .FirstAsync();

        if (ent == null)
            return Ok(ApiResponse<object>.Fail("企业不存在或已删除"));
        if (!string.Equals(ent.OrgCode, scope.Data.WorkspaceCode, StringComparison.Ordinal))
            return Ok(ApiResponse<object>.Fail("无权访问其他工作区的企业"));

        // orgCode = 体系认证机构 Code（仅供页脚 {{@org}} 机构名查询）；
        // ★ 参数定义全平台共享（2026-10-06 起 OrgCode 恒空串 —— 见 26 号 §3.1 裁决）
        var orgCode = scope.Data.CertBodyCode;
        var stdCode = req.StandardCode ?? string.Empty;
        var stageCode = req.StageCode ?? string.Empty;

        // ── 1. 定义（含通配，按具体度去重）──
        var defs = await _db.Client.Queryable<FillParamDef>()
            .Where(d => d.OrgCode == "" && d.IsDeleted == false && d.IsValid == 1
                        && (d.StandardCode == "" || d.StandardCode == stdCode)
                        && (d.StageCode == "" || d.StageCode == stageCode))
            .ToListAsync();

        var picked = ParamValueResolver.PickMostSpecific(defs);

        // ── 2. 装配参数 ──
        //   ★ 取值决策统一走 ParamValueResolver（与企业端「合并清单」同一实现）——
        //     否则「企业端显示的值」与「文档里填的值」会漂移，且两边都不报错。
        var codes = picked.Select(d => d.ParamCode).ToList();
        var saved = codes.Count == 0
            ? new List<FillParamValue>()
            : await _db.Client.Queryable<FillParamValue>()
                .Where(v => v.EnterpriseCode == ent.Code && v.IsDeleted == false
                            && codes.Contains(v.ParamCode))
                .ToListAsync();

        var valueMap = saved
            .GroupBy(v => ParamValueResolver.ValueKey(v.StandardCode, v.StageCode, v.ParamCode))
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var entInfo = ToEnterpriseInfo(ent);
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
        var paramNames = new Dictionary<string, string>(StringComparer.Ordinal);
        var autoCount = 0;
        var tableCount = 0;

        foreach (var def in picked)
        {
            paramNames[def.ParamCode] = def.ParamName;

            valueMap.TryGetValue(
                ParamValueResolver.ValueKey(def.StandardCode, def.StageCode, def.ParamCode), out var row);

            var d = ParamValueResolver.Resolve(def, entInfo, row?.ParamValue, row?.ValueSource,
                row?.IsManualEdited ?? false);

            if (string.IsNullOrWhiteSpace(d.Value)) continue;

            parameters[def.ParamCode] = d.Value!;
            if (string.Equals(d.ValueSource, ParamValueResolver.SourceAuto, StringComparison.Ordinal))
                autoCount++;
            else
                tableCount++;
        }

        // ── 3. 文档元信息（页眉页脚变量来源）──
        var stdNo = stdCode.Length == 0
            ? null
            : await _db.Client.Queryable<ISOStandard>()
                .Where(x => x.Code == stdCode)
                .Select(x => x.StandardCode)
                .FirstAsync();

        var stageName = stageCode.Length == 0
            ? null
            : await _db.Client.Queryable<CertStage>()
                .Where(x => x.Code == stageCode)
                .Select(x => x.StageName)
                .FirstAsync();

        var docPrefix = parameters.TryGetValue("doc_prefix", out var dp) ? dp : null;

        // 页脚「{{@org}}」应为**体系认证机构**名称（不是工作区名 —— 工作区名形如「尚龙认证 · 王卿权」，
        // 印在正式文件页脚上不合适）
        var certBodyName = await _db.Client.Queryable<CertificationBody>()
            .Where(x => x.Code == orgCode)
            .Select(x => x.Name)
            .FirstAsync();

        var ctx = new FillContext
        {
            Template = string.IsNullOrWhiteSpace(req.Template) ? DemoBody : req.Template!,
            HeaderTemplate = string.IsNullOrWhiteSpace(req.Header) ? DemoHeader : req.Header,
            FooterTemplate = string.IsNullOrWhiteSpace(req.Footer) ? DemoFooter : req.Footer,
            Params = parameters,
            ParamNames = paramNames,
            Enterprise = entInfo,
            // ⛔ Sys_Organization 的机构名属性是 OrgName（不是 Name），且无 ShortName
            //    此处用**认证机构**名称（而非工作区名），理由见上方 certBodyName 注释
            Org = new OrgInfo
            {
                Code = orgCode,
                Name = string.IsNullOrWhiteSpace(certBodyName) ? scope.Data.WorkspaceName : certBodyName,
            },
            Doc = new DocInfo
            {
                No = string.IsNullOrWhiteSpace(docPrefix) ? null : $"{docPrefix}-QM-2026-001",
                Title = "质量手册",
                Version = "A/0",
                StandardNo = stdNo,
                StageName = stageName,
                Page = "1",
            },
            // ★ AI 默认开启：未生成时进「待办」而非静默留空，用户能看到「这里还需要内容」
            AiEnabled = req.AiEnabled ?? true,
            Now = DateTime.Now,
        };

        var result = _engine.Fill(ctx);

        // ── 4. 校准待办原因：区分「未定义」与「未填写」──
        //    引擎只看到「已装配的值」，无法知道某个 paramCode 是否在后台定义过。
        //    不区分的话，模板里写错参数名会显示成「尚未完善」，把人引到企业端去找一个
        //    根本不存在的参数 —— 指错方向比不报错更耗时。
        var definedCodes = picked.Select(d => d.ParamCode).ToHashSet(StringComparer.Ordinal);
        var undefinedTokens = new List<string>();
        foreach (var p in result.Report.Pendings)
        {
            if (!string.Equals(p.Kind, "global", StringComparison.Ordinal)) continue;
            if (definedCodes.Contains(p.Key)) continue;

            p.Reason = "参数未在后台定义（请到「体系认证全局参数定义」按机构×标准×阶段新增该参数编码）";
            undefinedTokens.Add(p.Key);
        }

        return Ok(ApiResponse<object>.Ok(new
        {
            enterpriseCode = ent.Code,
            enterpriseName = ent.Name,
            standardCode = stdCode,
            stageCode = stageCode,

            output = result.Output,
            header = result.Header,
            footer = result.Footer,

            report = new
            {
                total = result.Report.Total,
                resolved = result.Report.ResolvedCount,
                pending = result.Report.PendingCount,
                completion = Math.Round(result.Report.Completion, 4),
                byKind = result.Report.ByKind.Select(kv => new
                {
                    kind = kv.Key,
                    name = DocumentFillEngine.KindLabel(kv.Key),
                    resolved = kv.Value[0],
                    pending = kv.Value[1],
                }).OrderByDescending(x => x.resolved + x.pending).ToList(),
                hits = result.Report.Hits.Select(h => new
                {
                    token = h.Token, key = h.Key, kind = h.Kind,
                    kindName = DocumentFillEngine.KindLabel(h.Kind),
                    value = h.Value, source = h.Source,
                }).ToList(),
                pendings = result.Report.Pendings.Select(p => new
                {
                    token = p.Token, key = p.Key, kind = p.Kind,
                    kindName = DocumentFillEngine.KindLabel(p.Kind),
                    reason = p.Reason,
                }).ToList(),
            },

            // 统计口径（供界面展示「这份文档的信息是从哪来的」）
            stats = new
            {
                paramCount = parameters.Count,
                autoMappedCount = autoCount,
                filledFromTableCount = tableCount,
                definedCount = definedCodes.Count,
                undefinedTokenCount = undefinedTokens.Count,
            },

            summary = DocumentFillEngine.RenderReport(result.Report),
        }));
    }

    // ════════════════════════════════════════════════════════════════════
    // 私有
    // ════════════════════════════════════════════════════════════════════

    // ⛔ 此处刻意**不**再放 Specificity / ResolveAutoValue ——
    //    已上提为 ParamValueResolver 的公开成员（与企业端「合并清单」共用同一实现）。

    private static EnterpriseInfo ToEnterpriseInfo(Enterprise e) => new()
    {
        Code = e.Code,
        Name = e.Name,
        ShortName = e.ShortName,
        CreditCode = e.CreditCode,
        LegalPerson = e.LegalPerson,
        Province = e.Province,
        City = e.City,
        Address = e.Address,
        IndustryType = e.IndustryType,
        EmployeeCount = e.EmployeeCount,
        CertScope = e.CertScope,
        ContactName = e.ContactName,
        ContactPhone = e.ContactPhone,
        ContactEmail = e.ContactEmail,
        EnterpriseNo = e.EnterpriseNo,
        ArchiveDate = e.ArchiveDate,
    };

    private static string SyntaxOf(string kind) => kind switch
    {
        "global" => "{{param_code}}",
        "replace" => "{{enterprise.Attr}} · {{org.Attr}} · {{system.date}}",
        "headerFooter" => "{{@doc_no}} · {{@company}} · {{@date_cn}} · {{@page}}",
        "ai" => "{{ai:param_code}}",
        _ => string.Empty,
    };

    private static string MeaningOf(string kind) => kind switch
    {
        "global" => "读「企业全局参数定义」里企业完善后的值；可被企业覆盖，是最主要的取值来源",
        "replace" => "直接取企业/机构/系统属性，不经参数表；永远与企业档案一致，企业不可改",
        "headerFooter" => "文档级自动变量（编号/版本/日期/页码/企业名），页眉页脚与正文共用，每页重复",
        "ai" => "无法从结构化字段推出的段落（质量方针/目标/企业概况），由 AI 生成后落参数表供复用",
        _ => string.Empty,
    };

    private static string ExampleOf(string kind) => kind switch
    {
        "global" => "{{main_products}} → 主要产品",
        "replace" => "{{enterprise.LegalPerson}} → 法定代表人",
        "headerFooter" => "{{@doc_no}} → YZH-QM-2026-001",
        "ai" => "{{ai:quality_policy}} → 一段质量方针正文",
        _ => string.Empty,
    };

    // ════════════════════════════════════════════════════════════════════
    // 内置演示模板（刻意覆盖 4 类锚点，供「能力演示」）
    // ════════════════════════════════════════════════════════════════════

    /// <summary>页眉：每页重复 —— 演示「页眉页脚」能力</summary>
    private const string DemoHeader = "{{@company}}　|　{{@doc_title}}　|　文件编号：{{@doc_no}}　|　版本：{{@version}}";

    /// <summary>页脚：每页重复 —— 页码与日期</summary>
    private const string DemoFooter = "{{@org}}　制　·　{{@date_cn}}　·　第 {{@page}} 页";

    /// <summary>
    /// 演示正文 —— 一份质量手册的封面 + 颁布令 + 方针目标章节。
    /// <para>锚点分布：全局参数 2 处（doc_prefix / main_products）、替换若干
    /// （企业档案类锚点走 {{enterprise.*}} —— 2026-10-06 起企业全称等档案字段
    /// 不再是字典参数，见 26 号 §3.1 裁决）、AI 生成 3 处、页眉页脚由 header/footer 承担。</para>
    /// </summary>
    private const string DemoBody = """
# {{enterprise.Name}}
## 质 量 手 册

| 项目 | 内容 |
|---|---|
| 文件编号 | {{@doc_no}} |
| 版本 / 修改状态 | {{@version}} |
| 受控状态 | 受控 |
| 编制单位 | {{enterprise.Name}} |
| 统一社会信用代码 | {{enterprise.CreditCode}} |
| 法定代表人 | {{enterprise.LegalPerson}} |
| 注册地址 | {{enterprise.Address}} |
| 认证范围 | {{enterprise.CertScope}} |
| 主要产品 | {{main_products}} |
| 发布日期 | {{@date_cn}} |

---

### 颁 布 令

本公司依据 {{@standard_no}} 标准要求，结合本企业实际编制了本《质量手册》。
本手册自 {{@date_cn}} 起发布实施，全体员工必须遵照执行。

本手册由 {{enterprise.Name}}（统一社会信用代码 {{enterprise.CreditCode}}）法定代表人
{{enterprise.LegalPerson}} 批准发布。

批准人：{{enterprise.LegalPerson}}　　　日期：{{system.date}}

---

### 0.1 企业概况

{{ai:company_profile}}

本公司现有员工 {{enterprise.EmployeeCount}} 人，注册地址位于
{{enterprise.Province}}{{enterprise.City}}{{enterprise.Address}}，
主要从事 {{enterprise.IndustryType}} 领域业务。

本手册的日常管理由 {{enterprise.ContactName}} 负责，联系电话
{{enterprise.ContactPhone}}。

---

### 5.2 质量方针

{{ai:quality_policy}}

### 5.4.1 质量目标

{{ai:quality_objective}}

---

### 文件编号规则

本公司体系文件编号统一采用前缀 `{{doc_prefix}}`，例如本手册编号为 `{{@doc_no}}`。
""";

    // ════════════════════════════════════════════════════════════════════
    // 请求体
    // ════════════════════════════════════════════════════════════════════

    public sealed class DemoPreviewRequest
    {
        public string EnterpriseCode { get; set; } = string.Empty;
        public string? StandardCode { get; set; }
        public string? StageCode { get; set; }

        /// <summary>留空 = 用内置演示模板</summary>
        public string? Template { get; set; }

        /// <summary>留空 = 用内置演示页眉</summary>
        public string? Header { get; set; }

        /// <summary>留空 = 用内置演示页脚</summary>
        public string? Footer { get; set; }

        /// <summary>是否启用 AI 生成（默认 true）。关闭时 <c>{{ai:*}}</c> 全部进待办。</summary>
        public bool? AiEnabled { get; set; }
    }
}

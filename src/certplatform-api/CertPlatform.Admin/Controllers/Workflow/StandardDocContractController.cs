using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using CertPlatform.Admin.Services.DocExtraction;
using CertPlatform.Admin.Services.Workflow;
using CertPlatform.Shared.DocExtraction;
using CertPlatform.Shared.Domain.Scope;
using CertPlatform.Shared.Entities.Dir;
using CertPlatform.Shared.Entities.Doc;
using CertPlatform.Shared.Fill;
using CertPlatform.Shared.Services.Ent;
using YZH.Core.Api.Controllers;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Result;
using CertPlatform.Shared.Services.Workflow;
using CertPlatform.Shared.Services.DocExtraction;

namespace CertPlatform.Admin.Controllers.Workflow;

/// <summary>
/// <b>标准文档契约</b>控制器（<c>cert_standard_doc_contract</c>）。
///
/// <para><b>路由前缀</b>：<c>/api/Admin/Workflow/StandardDocContract</c>（含 <c>Admin/</c> 端标记，
/// 与后台端其余 Controller 一致；<c>ApiCode</c> 取路由<b>末段</b>，加端标记不影响授权关联）。</para>
///
/// <para><b>基类说明（★ 有意为之，⛔ 不是漏改）</b>：继承 <see cref="WebControllerBase"/> 而非
/// <c>YzhControllerBase&lt;V&gt;</c> —— 契约表<b>没有</b> EntityConfig、也不走页面配置驱动的 CRUD，
/// 继承后者只会白带六个通用端点（且那六个端点全都用不上）。
/// 架构规范允许「<b>职能特殊控制器</b>」直接继承 <c>ControllerBase</c>，本控制器正属该类；
/// 此处显式记录理由，避免后人 review 时误判为「基类写错了」而改成 <c>YzhControllerBase&lt;V&gt;</c>。</para>
///
/// <para><b>业务定位（37 号 §3.4 Tab3 / §0.2）</b>：契约是「<b>文档语义分析</b>」的标准侧落点 ——
/// 分类（<c>DocCategory</c>）、作用（<c>DocPurpose</c>）、标签（<c>TagsJson</c>）、
/// 包含信息（<c>InfoItemsJson</c>）、指纹（<c>FingerprintJson</c>）都存这里。
/// 一文件一契约（<c>uk_standard_file_code</c>），天然幂等键。</para>
///
/// <para><b>★ 为什么新建这个控制器（实测缺口）</b>：37 号 §0.2 假定「<c>MENU_00210</c> 已有页面写契约」，
/// 但实查 —— <c>cert_standard_doc_contract</c> <b>0 行，且全项目没有任何读写它的端点</b>。
/// 所以「文档填写规则」页的 Tab3 无从取值，必须先补上这对端点。</para>
///
/// <para><b>★ <c>DocCategory</c> 的权威列是 <c>cert_standard_directory_file</c></b>（34 号 §2.3：
/// 流程在 ⑦ 裁决后按本列分叉）⇒ <c>save</c> 时<b>同批写两处</b>，保证永不不一致；
/// <c>detail</c> 返回的 <c>DocCategory</c> 一律取<b>文件行</b>的值。</para>
///
/// <para><b>⛔ 画像链与定向链分离（01 号 D3）</b>：本表只落契约，任何情况下不写 <c>cert_extraction_result</c>。</para>
///
/// <para><b>权限</b>：不标 <c>[RequirePermission]</c> ⇒ 仅需认证（当前项目口径）。</para>
/// </summary>
[ApiController]
[Route("api/Admin/Workflow/StandardDocContract")]
public class StandardDocContractController : WebControllerBase
{
    private readonly IDbOrm _db;
    /// <summary>核心资源检索唯一入口（铁律 L5）—— 目录文件按 Code 单查只走它。</summary>
    private readonly ICoreResourceQuery _core;
    private readonly DocExtractionRuleService _extraction;
    private readonly PromptWorkbenchService _prompts;
    private readonly IObjectStorage _storage;
    private readonly IUserContext _userContext;
    private readonly ILogger<StandardDocContractController> _logger;

    public StandardDocContractController(
        IDbOrm db,
        DocExtractionRuleService extraction,
        PromptWorkbenchService prompts,
        IObjectStorage storage,
        IUserContext userContext,
        ILogger<StandardDocContractController> logger,
        ICoreResourceQuery core)
    {
        _db = db;
        _core = core;
        _extraction = extraction;
        _prompts = prompts;
        _storage = storage;
        _userContext = userContext;
        _logger = logger;
    }

    // ════════════════════════════════════════════════════════════════════
    // 一、读：契约详情（不存在时返回「空壳 + 文件行默认值」，前端可直接编辑后保存）
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>读契约</b>（37 号 §五 的 <c>GET contract?fileCode=</c>）。
    ///
    /// <para>契约不存在时<b>不报错</b>，返回 <c>Exists=false</c> 的空壳 —— 这样前端选中一个
    /// 从未配过契约的标准文档时，Tab3 仍能渲染出可编辑表单（否则要额外处理 404 分支）。</para>
    /// </summary>
    [HttpGet("detail")]
    public async Task<IActionResult> Detail([FromQuery] string fileCode)
    {
        if (string.IsNullOrWhiteSpace(fileCode))
            return Ok(ApiResponse<object>.Fail("缺少标准文件编码（fileCode）"));

        var fileRow = await _core.GetDirectoryFileByCodeAsync(fileCode);
        if (fileRow == null)
            return Ok(ApiResponse<object>.Fail("标准目录文件不存在或已删除"));

        var contract = await _db.Client.Queryable<StandardDocContract>()
            .Where(c => c.StandardFileCode == fileCode)
            .FirstAsync();

        return Ok(ApiResponse<object>.Ok(new
        {
            Exists = contract != null,
            Code = contract?.Code,
            StandardFileCode = fileCode,
            FileName = fileRow.FileName,

            // ★ 分类权威 = 标准目录文件行（不是契约行）
            DocCategory = NormalizeCategory(fileRow.DocCategory, null),

            // ★ 文档约束 + 是否依赖企业文档（2026-10-16 定稿，`67` 号 §4.2）
            //   ⚠️ 权威位置 = **标准目录文件行**（不是契约行）——
            //      契约表的 DocRole / FixedDocSubtype 已**物理删除**（那是「第三对分叉列」：
            //      页面写契约列、编排器读目录列 ⇒ 配了不生效，见实体注释）。
            IsRequired = fileRow.IsRequired,
            Replaceable = fileRow.Replaceable,

            // ── ★ 标准原始文档的存储信息（37 号 §3.3 的「下载标准文档」）──
            //   页面中栏要「先看原始文档、再上传加工后的空白模板」，所以路径必须随本接口一起给。
            //   优先级与前端 DocPreview 一致：原始 → 可编辑副本 → 转换产物。
            //   ⚠️ 这里只**透出路径**，下载仍走既有的 `downloadFile(storagePath)`（⛔ 不新造下载端点）。
            StandardFileType = fileRow.FileType,
            StandardStoragePath = fileRow.StoragePath,
            StandardEditablePath = fileRow.EditableStoragePath,
            StandardConvertedPath = fileRow.ConvertedStoragePath,
            StandardEditableStatus = fileRow.EditableStatus,
            EnterpriseCode = fileRow.EnterpriseCode,

            DocName = contract?.DocName ?? fileRow.FileName,
            // ⛔ 不再返回 DocRole（该列已物理删除，2026-10-16）—— 见 `67` 号 §4.1
            DocPurpose = contract?.DocPurpose,
            TagsJson = contract?.TagsJson,
            InfoItemsJson = contract?.InfoItemsJson,
            FingerprintJson = contract?.FingerprintJson,

            // 分析元数据（只读，供前端展示「AI 还是人工」）
            AnalyzeStatus = contract?.AnalyzeStatus ?? "pending",
            AnalyzeMessage = contract?.AnalyzeMessage,
            ModelName = contract?.ModelName,
            AnalyzeTime = contract?.AnalyzeTime,
            TagsSource = contract?.TagsSource,
            DocPurposeSource = contract?.DocPurposeSource,
            TagsConfidence = contract?.TagsConfidence,
            DocPurposeConfidence = contract?.DocPurposeConfidence,
            IsManualCorrected = contract?.IsManualCorrected ?? false,
        }));
    }

    // ════════════════════════════════════════════════════════════════════
    // 二、写：保存契约（人工编辑 ⇒ 标记 manual，批量重跑不得覆盖）
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>保存契约</b>（37 号 §五 的 <c>POST contract/save</c>）。
    ///
    /// <para><b>★ 含已删查重</b>：<c>uk_standard_file_code</c> 不含 <c>IsDeleted</c> ⇒ 必须把已软删行
    /// 也查出来（陷阱 ㊶）：命中<b>存活</b>行 ⇒ 更新；命中<b>已软删</b>行 ⇒ 就地复活（沿用同 Code）；
    /// 都没有 ⇒ 新增。</para>
    ///
    /// <para><b>★ 人工编辑的语义</b>：一旦人工保存，<c>TagsSource</c>/<c>DocPurposeSource</c> 置
    /// <c>manual</c>、<c>IsManualCorrected=1</c> —— 后续 AI 批量重跑<b>不得覆盖</b>（33 号 §五）。</para>
    ///
    /// <para><b>★ 同批同步 <c>cert_standard_directory_file.DocCategory</c></b>：那是流程分叉的权威列。</para>
    /// </summary>
    [HttpPost("save")]
    public async Task<IActionResult> Save([FromBody] ContractSaveRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.StandardFileCode))
            return Ok(ApiResponse<object>.Fail("缺少标准文件编码（standardFileCode）"));

        var fileRow = await _core.GetDirectoryFileByCodeAsync(req.StandardFileCode);
        if (fileRow == null)
            return Ok(ApiResponse<object>.Fail("标准目录文件不存在或已删除"));

        // ★ 分类归一 + 白名单校验（hybrid 一期不启用，Q-10；传了也不落库）
        var category = NormalizeCategory(req.DocCategory, fileRow.DocCategory);
        if (category == null)
            return Ok(ApiResponse<object>.Fail("文档分类只能是 fixed / editable（hybrid 一期不启用）"));

        // ★ 文档约束 / 是否依赖企业文档（2026-10-16 定稿，`67` 号 §4.2）
        //   ⚠️ 二者都是 `bool?`：`null` = **本次不动**（与 DocPurpose / TagsJson 的「null 不动」口径一致），
        //      前端只改了分类、没碰这两个开关时，不该把既有值冲回默认。
        //   ⛔ 本方法**不做**「不依赖企业文档却配了企业资料类锚点来源」的校验 —— 那是锚点级校验，
        //      见 `67` 号 §6.2（配置期给警告，不阻断）。

        var scope = await _extraction.ResolveRuleScopeAsync(req.StandardFileCode);
        var now = DateTime.Now;
        var user = _userContext.UserCode;

        // ★ 含已删查重（⛔ GetOneIgnoreValidAsync 仍过滤软删 ⇒ 必须走 Client.Queryable）
        var contract = await _db.Client.Queryable<StandardDocContract>()
            .Where(c => c.StandardFileCode == req.StandardFileCode)
            .FirstAsync();

        var isNew = contract == null;
        if (contract == null)
        {
            contract = new StandardDocContract
            {
                // ★ 底层 ORM（IDbOrm.InsertAsync）**不生成业务键 Code** —— 那是 EntityService 的职责。
                //   不自己给会直接报「新增失败：必填字段为空」（实测踩过）。
                Code = Guid.NewGuid().ToString("N"),
                StandardFileCode = req.StandardFileCode,
                CreateBy = user,
                CreateTime = now,
                IsValid = 1,
                Status = "draft",
            };
        }

        // 复活（若命中已软删行）
        contract.IsDeleted = false;
        contract.DeleteBy = null;
        contract.DeleteTime = null;

        // ── 身份段（服务端权威推导，⛔ 不信前端）──
        contract.ConfigCode = fileRow.ConfigCode ?? string.Empty;
        contract.StandardCode = scope.StandardCode ?? string.Empty;
        contract.StageCode = scope.StageCode ?? string.Empty;

        // ── 业务字段 ──
        contract.DocName = string.IsNullOrWhiteSpace(req.DocName) ? fileRow.FileName : req.DocName!;
        contract.DocCategory = category;
        // ⛔ 2026-10-16：不再写 contract.DocRole / contract.FixedDocSubtype（两列已物理删除，`67` 号 §4.1）。
        //   「文档约束」「是否依赖企业文档」的权威位置 = 标准目录文件行，见下方「同批同步」。

        // 只在「显式提交」时改写 —— null = 本次不动该字段（避免前端漏传导致清空）
        if (req.DocPurpose != null)
        {
            contract.DocPurpose = req.DocPurpose;
            contract.DocPurposeSource = string.IsNullOrWhiteSpace(req.DocPurpose) ? null : "manual";
        }
        // ★ JSON 列必须归一（2026-10-04 实测缺陷）：`TagsJson` 是 MySQL `json` 列，
        //   前端「标签为空」时提交的是**空串** ⇒ MySQL 直接报
        //   `Invalid JSON text: "The document is empty." at position 0`，**整条保存失败**。
        //   ⇒ 空值一律归一成合法 JSON 字面量（数组 `[]` / 对象 `{}`），⛔ 绝不写空串。
        if (req.TagsJson != null)
        {
            contract.TagsJson = ToJsonArray(req.TagsJson);
            contract.TagsSource = string.IsNullOrWhiteSpace(req.TagsJson) ? null : "manual";
        }
        if (req.InfoItemsJson != null) contract.InfoItemsJson = ToJsonArray(req.InfoItemsJson);
        if (req.FingerprintJson != null) contract.FingerprintJson = ToJsonObject(req.FingerprintJson);

        // ★ 人工编辑留痕：批量重跑不得覆盖
        contract.IsManualCorrected = true;
        contract.UpdateBy = user;
        contract.UpdateTime = now;
        if (isNew || contract.AnalyzeStatus == "pending") contract.AnalyzeStatus = "manual";

        var saveResult = isNew
            ? await _db.InsertAsync(contract)
            : await _db.UpdateAsync(contract,
                nameof(StandardDocContract.IsDeleted), nameof(StandardDocContract.DeleteBy), nameof(StandardDocContract.DeleteTime),
                nameof(StandardDocContract.ConfigCode), nameof(StandardDocContract.StandardCode), nameof(StandardDocContract.StageCode),
                nameof(StandardDocContract.DocName), nameof(StandardDocContract.DocCategory),
                nameof(StandardDocContract.DocPurpose), nameof(StandardDocContract.DocPurposeSource),
                nameof(StandardDocContract.TagsJson), nameof(StandardDocContract.TagsSource),
                nameof(StandardDocContract.InfoItemsJson), nameof(StandardDocContract.FingerprintJson),
                nameof(StandardDocContract.IsManualCorrected), nameof(StandardDocContract.AnalyzeStatus),
                nameof(StandardDocContract.UpdateBy), nameof(StandardDocContract.UpdateTime));

        if (!saveResult.Success)
            return Ok(ApiResponse<object>.Fail(saveResult.Error ?? "契约保存失败"));

        // ════════════════════════════════════════════════════════════════════
        //  ★ 同批同步标准目录文件行（2026-10-16 扩展，`67` 号 §5.3）
        //
        //  三个「文档级」属性一次请求写到位，保证永不不一致：
        //    · DocCategory  —— 流程分叉的权威列（原本就有）
        //    · IsRequired   —— 文档约束（必要 / 非必要）
        //    · Replaceable  —— 是否依赖企业文档
        //
        //  ⚠️ 只写【标准行】（EnterpriseCode 为空，归属模型 V2）—— 企业实例行不覆盖。
        //  ⚠️ 列级写回（⛔ 禁止全列写回 —— 陷阱 ㉕：并发上传链会把这些列清成 NULL）。
        //  ⚠️ 只把**真的变了**的列列入更新集，避免无谓写库与无谓的 UpdateTime 抖动。
        // ════════════════════════════════════════════════════════════════════
        var isStandardRow = string.IsNullOrEmpty(fileRow.EnterpriseCode);
        var fileCols = new List<string>();

        if (!string.Equals(fileRow.DocCategory, category, StringComparison.Ordinal))
        {
            fileRow.DocCategory = category;
            fileCols.Add(nameof(StandardDirectoryFile.DocCategory));
        }
        if (isStandardRow && req.IsRequired != null && fileRow.IsRequired != req.IsRequired.Value)
        {
            fileRow.IsRequired = req.IsRequired.Value;
            fileCols.Add(nameof(StandardDirectoryFile.IsRequired));
        }
        if (isStandardRow && req.Replaceable != null && fileRow.Replaceable != req.Replaceable.Value)
        {
            fileRow.Replaceable = req.Replaceable.Value;
            fileCols.Add(nameof(StandardDirectoryFile.Replaceable));
        }

        if (fileCols.Count > 0)
        {
            fileRow.UpdateBy = user;
            fileRow.UpdateTime = now;
            fileCols.Add(nameof(StandardDirectoryFile.UpdateBy));
            fileCols.Add(nameof(StandardDirectoryFile.UpdateTime));

            var sync = await _db.UpdateAsync(fileRow, fileCols.ToArray());
            if (!sync.Success)
            {
                // 不阻塞主保存，但必须留日志 —— 否则两处静默分叉
                _logger.LogWarning(
                    "[Contract] 契约已保存，但同步标准目录行失败：File={File}, Cols={Cols}, Err={Err}",
                    req.StandardFileCode, string.Join(",", fileCols), sync.Error);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  ★ 配置期校验（`67` 号 §6.2，用户 2026-10-16 裁决③「需要实现」）
        //
        //  「不依赖企业文档」的文档由后台**自动生成**，企业⛔ 不用交东西。
        //  若它的锚点仍配了「需要企业资料」的来源 ⇒ 运行期必然取不到值。
        //  ⇒ 保存时**顺带扫一遍**并把结论回给前端（⛔ 不阻断保存，只提示）。
        //
        //  ⚠️ 只在 `Replaceable = false` 时才扫 —— 依赖企业文档的文档配这些来源是**正确**的。
        //  ⚠️ 扫描走 `ICoreResourceQuery`（核心资源检索唯一入口，铁律 L5）。
        // ════════════════════════════════════════════════════════════════════
        var sourceWarnings = fileRow.Replaceable
            ? new List<string>()
            : await CollectEnterpriseSourceWarningsAsync(req.StandardFileCode);

        _logger.LogInformation("[Contract] 契约已保存：File={File}, Category={Cat}, New={New}, By={User}, Warn={Warn}",
            req.StandardFileCode, category, isNew, user, sourceWarnings.Count);

        return Ok(ApiResponse<object>.Ok(new
        {
            contract.Code,
            contract.StandardFileCode,
            contract.DocName,
            contract.DocCategory,
            // ★ 文档级属性回传的是**目录行的真实值**（权威位置）——
            //   ⛔ 不是契约行（那两个列已废弃）。这样前端保存后能立刻看到「真的生效了」。
            fileRow.IsRequired,
            fileRow.Replaceable,
            contract.DocPurpose,
            contract.TagsJson,
            contract.InfoItemsJson,
            contract.FingerprintJson,
            contract.AnalyzeStatus,
            contract.IsManualCorrected,
            // ★ 配置期警告（⛔ 不阻断保存）：该文档标了「不依赖企业文档」，
            //   但锚点来源需要企业资料 ⇒ 逐条说明。空数组 = 无问题。
            Warnings = sourceWarnings,
        }, isNew ? "契约已创建" : "契约已保存"));
    }

    // ════════════════════════════════════════════════════════════════════
    //  ★ 配置期校验：「不依赖企业文档」⇒ 锚点来源不得需要企业资料
    //     （`67` 号 §6.2 落地 —— 用户 2026-10-16 裁决③「需要实现」）
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     扫描该标准文件下<b>全部锚点</b>的来源配置，挑出「<b>需要企业资料</b>」的那些，逐条成话。
    ///
    ///     <para><b>为什么必须做</b>：文档可标成「不依赖企业文档」（导航页 / 固定描述）——
    ///     这类文档由后台<b>自动生成</b>，企业⛔ 不用交东西。若它的锚点仍配了
    ///     「从企业文档抽字段」这类来源，<b>运行期必然取不到值</b>，
    ///     而配置期一切正常、不报错（本项目最坏的失败形态：静默失效）。</para>
    ///
    ///     <para><b>⛔ 为什么只警告、不阻断</b>：用户可能是「先配锚点 → 后改文档属性」，
    ///     也可能反过来 ⇒ 阻断会把人卡在中间态。判据：<b>配置期给提示，运行期有写实归因兜底</b>
    ///     （编排器的 <c>skipped_enterprise_source</c> 待办）。</para>
    ///
    ///     <para><b>★ 判据与运行期共用</b> <see cref="FillSourcePolicy"/> ——
    ///     ⛔ 不在这里另写一套 kind 判断（两处并存必然漂移）。</para>
    ///
    ///     <para>⚠️ 本方法<b>只读不写</b>，且异常一律吞掉只记日志 ——
    ///     校验是「锦上添花」，⛔ 不得让保存主流程因它失败。</para>
    /// </summary>
    private async Task<List<string>> CollectEnterpriseSourceWarningsAsync(string standardFileCode)
    {
        var warnings = new List<string>();
        try
        {
            var template = (await _core.GetDocTemplatesByStandardFileCodesAsync(new[] { standardFileCode }))
                .FirstOrDefault();
            if (template == null) return warnings;   // 没上传空白模板 ⇒ 没有锚点 ⇒ 无从校验

            var anchors = (await _db.GetListAsync<DocTemplateAnchor>(x =>
                x.TemplateCode == template.Code &&
                x.IsValid == 1 &&
                x.IsOrphan == false)).Data ?? new List<DocTemplateAnchor>();

            foreach (var anchor in anchors)
            {
                var spec = SourceSpecModel.TryParse(anchor.SourceSpec);
                if (spec?.Sources == null || spec.Sources.Count == 0) continue;

                foreach (var entry in spec.Sources)
                {
                    if (!FillSourcePolicy.DependsOnEnterpriseMaterial(entry.Kind, entry.Ref)) continue;
                    warnings.Add(FillSourcePolicy.DescribeConfigWarning(anchor.AnchorRef, entry.Kind, entry.Ref));
                }
            }
        }
        catch (Exception ex)
        {
            // ⛔ 校验失败不得影响主保存（它只是提示）
            _logger.LogWarning(ex, "[Contract] 企业资料类来源校验失败：File={File}", standardFileCode);
        }

        return warnings;
    }

    // ════════════════════════════════════════════════════════════════════
    // 三、分析：聚合触发（复用已有分析能力，⛔ 不新写 LLM 逻辑）
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>自动分析</b>（用户 2026-10-03 要求「每个文件应该有个自动分析按钮」）。
    ///
    /// <para><b>★ 一次调用聚合三个分析</b>（37 号 §0.2 的 A / B / C）：</para>
    /// <list type="bullet">
    /// <item><b>C 字段提取</b>：调 <see cref="DocExtractionRuleService.AIAnalyzeAsync"/> ——
    /// 已实现，⛔ 不重复造。</item>
    /// <item><b>B 文档语义分析</b>：调 <see cref="PromptWorkbenchService.AnalyzeForQueueAsync"/> 的
    /// <c>doc_group</c>（标签）+ <c>doc_content</c>（作用）两跳，结论落 <c>cert_standard_doc_contract</c>。
    /// 见 <see cref="RunSemanticAsync"/>。</item>
    /// <item><b>A 锚点扫描</b>：需要<b>空白模板</b>（37 号 H-2 硬约束）。未上传时本项<b>跳过并提示</b>，
    /// ⛔ 不报错 —— 用户的实际顺序是先分析原始文档、再上传空白模板。</item>
    /// </list>
    ///
    /// <para><b>★ 三项互相独立，互不阻断</b>（P2' 程序不阻断、只如实推导）：任一项失败只影响它自己的
    /// <c>Status</c> 段，另两项照跑 —— 例如提示词还没配（B 必然失败）不该让 C 字段提取也做不成。</para>
    ///
    /// <para><b>⚠️ 本端点会真调 LLM（两次，约 15~25 秒）</b>，前端超时须 ≥120s。</para>
    /// </summary>
    [HttpPost("analyze")]
    public async Task<IActionResult> Analyze([FromQuery] string fileCode)
    {
        if (string.IsNullOrWhiteSpace(fileCode))
            return Ok(ApiResponse<object>.Fail("缺少标准文件编码（fileCode）"));

        var fileRow = await _core.GetDirectoryFileByCodeAsync(fileCode);
        if (fileRow == null)
            return Ok(ApiResponse<object>.Fail("标准目录文件不存在或已删除"));

        // ── C：字段提取（已有能力，直接复用）──
        object fieldResult;
        try
        {
            var resp = await _extraction.AIAnalyzeAsync(new AIAnalyzeRequest { FileCode = fileCode });
            fieldResult = new
            {
                Status = resp.Fields.Count > 0 || resp.Tables.Count > 0 ? "ok" : "empty",
                FieldCount = resp.Fields.Count,
                TableCount = resp.Tables.Count,
                Fields = resp.Fields,
                Tables = resp.Tables,
                Message = resp.Message,
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Contract] 字段提取失败：File={File}", fileCode);
            fieldResult = new { Status = "failed", Message = ex.Message };
        }

        // ── B：文档语义分析（标签 + 作用，两跳 LLM，结论落契约表）──
        var semantic = await RunSemanticAsync(fileRow);

        // ── A：锚点扫描（需要空白模板 —— H-2 硬约束）──
        var template = await _db.Client.Queryable<DocTemplate>()
            .Where(t => t.StandardFileCode == fileCode && t.IsDeleted == false)
            .FirstAsync();

        object scanResult;
        if (template == null)
        {
            scanResult = new
            {
                Status = "blocked",
                Message = "尚未上传空白模板，无法扫描锚点（请先「下载标准文档 → 加工 → 上传空白模板」）",
            };
        }
        else
        {
            // ★ 扫描器**已接入**（`DocTemplateAnchor/scan`）。此处如实回报已有扫描结果，
            //   ⛔ 不再写「扫描器尚未接入」——那会与页面上可用的「重新扫描」按钮自相矛盾。
            var anchorRows = await _db.Client.Queryable<DocTemplateAnchor>()
                .Where(a => a.TemplateCode == template.Code && a.IsDeleted == false && a.IsValid == 1)
                .Select(a => new { a.AnchorType, a.IsOrphan })
                .ToListAsync();

            var scanned = string.Equals(template.ScanStatus, "completed", StringComparison.Ordinal);

            scanResult = new
            {
                Status = scanned ? "ok" : template.ScanStatus,
                ScanStatus = template.ScanStatus,
                ScanMessage = template.ScanMessage,
                ScanTime = template.ScanTime,
                AnchorCount = anchorRows.Count,
                OrphanCount = anchorRows.Count(a => a.IsOrphan),
                ByType = anchorRows
                    .GroupBy(a => a.AnchorType ?? string.Empty, StringComparer.Ordinal)
                    .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal),
                Message = scanned
                    ? $"已扫描：{anchorRows.Count} 个锚点（孤儿 {anchorRows.Count(a => a.IsOrphan)} 个）"
                    : "尚未扫描或扫描未完成，请点「重新扫描」",
            };
        }

        return Ok(ApiResponse<object>.Ok(new
        {
            StandardFileCode = fileCode,
            Semantic = semantic,
            Field = fieldResult,
            Scan = scanResult,
            // ★ 前端据此决定下一步引导
            NextStep = template == null ? "upload-template" : "rescan",
        }));
    }

    // ════════════════════════════════════════════════════════════════════
    // 三·B、文档语义分析（两跳 LLM → 契约表）
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>B 文档语义分析</b>：跑 <c>doc_group</c>（标签）+ <c>doc_content</c>（作用）两跳 LLM，
    /// 结论落 <c>cert_standard_doc_contract</c>。
    ///
    /// <para><b>★ 为什么复用 <c>AnalyzeForQueueAsync</c> 而不是自己写 LLM 调用</b>：
    /// 它是「提示词工作台」的唯一队列语义入口，内部已含
    /// ① 生效提示词解析（<c>ResolveActiveAsync</c>）
    /// ② 语义上下文占位符渲染（<c>BuildSemanticContextAsync</c>，含标签字典裁剪）
    /// ③ 33 号 §3.3 的<b>六条输出校验</b>（越界回退 OTHER / clamp / 截断）
    /// ④ 用量日志（<c>cert_ai_usage_log</c>）。
    /// 自己写一遍就是<b>「复制即漂移」</b> —— 校验口径一分叉，契约表与画像表就会长出不同形状。</para>
    ///
    /// <para><b>★ 提取口径也复用 <see cref="SemanticHints"/></b>（与 <c>EnterpriseOriginalAnalyzeExecutor</c>
    /// 同一套），⛔ 不在本类里另抄一份 JSON 解析。</para>
    ///
    /// <para><b>⛔ 本方法不写 <c>DocCategory</c></b>：该列的<b>权威位置是
    /// <c>cert_standard_directory_file.DocCategory</c></b>（<see cref="Detail"/> 也一律读文件行）。
    /// 只写契约行 ⇒ <c>detail</c> 读不到 ⇒ 变成<b>「写了但不生效」的静默分叉</b>。
    /// 故 AI 的分类结论只作为 <c>SuggestedCategory</c> <b>建议</b>返回，由人工在 Tab3 确认后经
    /// <see cref="Save"/> 同批写两处（P3 建议 ≠ 事实）。</para>
    ///
    /// <para><b>★ 输入是 Markdown，⛔ 不是原始二进制</b>：语义分析吃转换产物
    /// （<c>cert_standard_directory_file.MarkdownPath</c>）；未转换 ⇒ <c>blocked</c>，⛔ 不报错。</para>
    /// </summary>
    private async Task<SemanticOutcome> RunSemanticAsync(StandardDirectoryFile fileRow)
    {
        // ① 输入闸：Markdown 未就绪 ⇒ blocked（⛔ 不是 failed —— 还没轮到而已）
        if (string.IsNullOrWhiteSpace(fileRow.MarkdownPath))
            return SemanticOutcome.Blocked("原始文档尚未转换为 Markdown，无法做语义分析（请先让文件完成转换）");

        // ② 读 Markdown（MinIO）
        string markdown;
        try
        {
            // ⚠️ 路径必须 TrimStart('/')：库里存的是带前导斜杠的展示路径，MinIO 的 key 不带
            //   （与 EnterpriseOriginalAnalyzeExecutor 同一口径）
            var (stream, _) = await _storage.DownloadAsync(fileRow.MarkdownPath!.TrimStart('/'));
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            markdown = Encoding.UTF8.GetString(ms.ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Contract] Markdown 读取失败：File={File}, Path={Path}",
                fileRow.Code, fileRow.MarkdownPath);
            return SemanticOutcome.Failed($"Markdown 读取失败：{ex.Message}");
        }

        if (string.IsNullOrWhiteSpace(markdown))
            return SemanticOutcome.Failed("Markdown 内容为空，无法分析");

        // ③ 作用域（★ 必须是标准 GUID，⛔ 不接受 slug；空 = 平台级提示词）
        var scope = await _extraction.ResolveRuleScopeAsync(fileRow);
        var standardCode = string.IsNullOrWhiteSpace(scope.StandardCode) ? null : scope.StandardCode;

        var fileName = fileRow.FileName;
        var one = new List<(string, string?)> { (fileName, markdown) };

        // ④ L1 doc_group（标签）—— 批次提示词，本场景只有 1 份
        var groupRes = await _prompts.AnalyzeForQueueAsync(
            PromptTypes.Group, standardCode, one, $"standard_doc:{fileRow.Code}:group");

        // ⑤ L2 doc_content（作用）—— 单份
        var contentRes = await _prompts.AnalyzeForQueueAsync(
            PromptTypes.Content, standardCode, one, $"standard_doc:{fileRow.Code}:content");

        // ⑥ 提取（统一口径）
        var groupByFile = groupRes.Success && !string.IsNullOrWhiteSpace(groupRes.Json)
            ? SemanticHints.ParseGroupItems(groupRes.Json!)
            : new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

        var (tagsJson, tagsReason, tagsConf, typeGuess, keywords, summary, aiCategory)
            = SemanticHints.ExtractGroupHints(groupByFile, fileName, groupRes.ValidationMessages);

        // ⚠️ contentRes.Json 可能为 null ⇒ 传空串让 ExtractContentHints 内部的 JsonException 分支兜住
        var (purpose, purposeConf, infoItems, _, _, _)
            = SemanticHints.ExtractContentHints(contentRes.Json ?? string.Empty);

        // ⑦ 两跳独立判定（P2' 只如实推导：一跳挂了不该把另一跳的成果也丢掉）
        var groupOk = groupRes.Success;
        var contentOk = contentRes.Success;

        var warnings = new List<string>();
        if (!groupRes.Success) warnings.Add($"标签分析失败：{groupRes.Message}");
        if (!contentRes.Success) warnings.Add($"作用分析失败：{contentRes.Message}");
        if (groupRes.Success && string.IsNullOrWhiteSpace(tagsJson))
            warnings.Add("标签分析成功但未返回可用标签（可能提示词未声明 tags 字段）");

        var nothingProduced = string.IsNullOrWhiteSpace(tagsJson) && string.IsNullOrWhiteSpace(purpose);

        // ⑧ 落库（含已删查重 + 复活 —— 陷阱 ㊶）
        var user = _userContext.UserCode;
        var now = DateTime.Now;

        var contract = await _db.Client.Queryable<StandardDocContract>()
            .Where(c => c.StandardFileCode == fileRow.Code)
            .FirstAsync();

        // ★ B4：人工修正过的行 ⛔ 不覆盖（33 号 §五）。仍把 AI 结论原样回给前端供人工参考。
        if (contract != null && contract.IsManualCorrected)
        {
            _logger.LogInformation("[Contract] 语义分析跳过（人工已修正）：File={File}", fileRow.Code);
            return SemanticOutcome.Skipped(
                "该文档契约已被人工修正，自动分析不覆盖（如需重跑请先清除人工标记）",
                purpose, tagsJson, aiCategory, typeGuess, keywords, summary,
                tagsConf, purposeConf, groupRes, contentRes, warnings);
        }

        if (nothingProduced)
        {
            // 两跳都没产出 ⇒ 如实标 failed，并留痕（排障时能看到「当时用的哪版提示词、报了什么」）
            if (contract != null)
            {
                contract.AnalyzeStatus = "failed";
                contract.AnalyzeMessage = Truncate(string.Join("；", warnings), 1024);
                contract.AnalyzeTime = now;
                contract.UpdateBy = user;
                contract.UpdateTime = now;
                await _db.UpdateAsync(contract,
                    nameof(StandardDocContract.AnalyzeStatus),
                    nameof(StandardDocContract.AnalyzeMessage),
                    nameof(StandardDocContract.AnalyzeTime),
                    nameof(StandardDocContract.UpdateBy),
                    nameof(StandardDocContract.UpdateTime));
            }
            return SemanticOutcome.Failed(
                warnings.Count > 0 ? string.Join("；", warnings) : "语义分析未产出任何结论",
                warnings);
        }

        var isNew = contract == null;
        if (contract == null)
        {
            // ★ 底层 ORM 不生成业务键 Code（必须自己给，否则「新增失败：必填字段为空」）
            contract = new StandardDocContract
            {
                Code = Guid.NewGuid().ToString("N"),
                StandardFileCode = fileRow.Code,
                CreateBy = user,
                CreateTime = now,
                IsValid = 1,
                Status = "draft",
            };
        }

        // 复活（若命中已软删行）
        contract.IsDeleted = false;
        contract.DeleteBy = null;
        contract.DeleteTime = null;

        // 身份段（服务端权威推导，⛔ 不信前端）
        contract.ConfigCode = fileRow.ConfigCode ?? string.Empty;
        contract.StandardCode = scope.StandardCode ?? string.Empty;
        contract.StageCode = scope.StageCode ?? string.Empty;
        if (string.IsNullOrWhiteSpace(contract.DocName)) contract.DocName = fileRow.FileName;
        if (string.IsNullOrWhiteSpace(contract.DocCategory))
            contract.DocCategory = NormalizeCategory(fileRow.DocCategory, null) ?? "editable";

        // ── 语义字段：只在有值时写，⛔ 绝不用 null 清空既有内容 ──
        if (!string.IsNullOrWhiteSpace(tagsJson))
        {
            contract.TagsJson = ToJsonArray(tagsJson);
            contract.TagsSource = "ai";
            contract.TagsConfidence = tagsConf;
        }
        var reason = Truncate(tagsReason, 500);
        if (!string.IsNullOrWhiteSpace(reason)) contract.TagsReason = reason;

        if (!string.IsNullOrWhiteSpace(purpose))
        {
            contract.DocPurpose = purpose;
            contract.DocPurposeSource = "ai";
            contract.DocPurposeConfidence = purposeConf;
        }
        if (!string.IsNullOrWhiteSpace(infoItems)) contract.InfoItemsJson = ToJsonArray(infoItems);

        // 召回关键词：人工可维护 ⇒ 只在空时填（⛔ 不覆盖人工填的）
        var kw = Truncate(keywords, 500);
        if (!string.IsNullOrWhiteSpace(kw) && string.IsNullOrWhiteSpace(contract.Keywords)) contract.Keywords = kw;

        // ── 分析元数据（可观测 + 可追溯 + 可重跑）──
        //   两跳合并口径：模型取「作用分析」那次（更贴近最终结论），Token / 耗时为两跳之和。
        contract.ModelName = Truncate(contentOk ? contentRes.Model : groupRes.Model, 50);
        contract.PromptCode = Truncate(contentOk ? contentRes.PromptCode : groupRes.PromptCode, 64);
        contract.PromptVersion = contentOk ? contentRes.PromptVersion : groupRes.PromptVersion;
        contract.PromptTokens = groupRes.PromptTokens + contentRes.PromptTokens;
        contract.CompletionTokens = groupRes.CompletionTokens + contentRes.CompletionTokens;
        contract.DurationMs = (int)Math.Min(groupRes.DurationMs + contentRes.DurationMs, int.MaxValue);
        contract.AnalyzeStatus = groupOk && contentOk ? "completed" : "partial";
        contract.AnalyzeMessage = Truncate(string.Join("；", warnings), 1024);
        contract.AnalyzeTime = now;
        contract.UpdateBy = user;
        contract.UpdateTime = now;

        var save = isNew ? await _db.InsertAsync(contract) : await _db.UpdateAsync(contract,
            nameof(StandardDocContract.ModelName), nameof(StandardDocContract.PromptCode), nameof(StandardDocContract.PromptVersion),
            nameof(StandardDocContract.PromptTokens), nameof(StandardDocContract.CompletionTokens),
            nameof(StandardDocContract.DurationMs), nameof(StandardDocContract.AnalyzeStatus),
            nameof(StandardDocContract.AnalyzeMessage), nameof(StandardDocContract.AnalyzeTime),
            nameof(StandardDocContract.UpdateBy), nameof(StandardDocContract.UpdateTime));
        if (!save.Success)
        {
            _logger.LogWarning("[Contract] 语义结论落库失败：File={File}, Err={Err}", fileRow.Code, save.Error);
            return SemanticOutcome.Failed($"语义结论落库失败：{save.Error}", warnings);
        }

        _logger.LogInformation(
            "[Contract] 语义分析完成：File={File}, Group={G}, Content={C}, Tags={Tags}, {PT}+{CT}tok {Ms}ms",
            fileRow.Code, groupRes.Success, contentRes.Success, tagsJson != null,
            contract.PromptTokens, contract.CompletionTokens, contract.DurationMs);

        return new SemanticOutcome
        {
            Status = contract.AnalyzeStatus,
            Message = warnings.Count > 0 ? string.Join("；", warnings) : null,
            Saved = true,
            DocPurpose = purpose,
            TagsJson = tagsJson,
            SuggestedCategory = aiCategory,
            TypeGuess = typeGuess,
            Keywords = keywords,
            Summary = summary,
            TagsConfidence = tagsConf,
            DocPurposeConfidence = purposeConf,
            Model = contract.ModelName,
            PromptCode = contract.PromptCode,
            PromptVersion = contract.PromptVersion,
            PromptTokens = contract.PromptTokens ?? 0,
            CompletionTokens = contract.CompletionTokens ?? 0,
            DurationMs = contract.DurationMs ?? 0,
            Warnings = warnings,
        };
    }

    /// <summary>截断到 <paramref name="max"/> 字符（MySQL 严格模式下超长直接抛错 ⇒ 整条链路挂）</summary>
    private static string? Truncate(string? s, int max)
        => string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s[..max]);

    /// <summary>
    /// B 语义分析的产出（只读回执，供 <see cref="Analyze"/> 原样透出）。
    /// <para><c>Status</c> 取值：<c>completed</c> / <c>partial</c> / <c>blocked</c> / <c>skipped</c> / <c>failed</c>。</para>
    /// </summary>
    private sealed class SemanticOutcome
    {
        /// <summary>completed=两跳都成 / partial=只成一跳 / blocked=输入未就绪 / skipped=人工已修正 / failed=无产出</summary>
        public string Status { get; init; } = "failed";

        /// <summary>人读说明（失败原因 / 告警汇总）</summary>
        public string? Message { get; init; }

        /// <summary>结论是否已落 <c>cert_standard_doc_contract</c>（<c>blocked</c>/<c>skipped</c>/<c>failed</c> ⇒ false）</summary>
        public bool Saved { get; init; }

        public string? DocPurpose { get; init; }
        public string? TagsJson { get; init; }

        /// <summary>★ AI 建议的文档分类 —— <b>仅供人工确认</b>，⛔ 未落库（见 <see cref="RunSemanticAsync"/> 注释）</summary>
        public string? SuggestedCategory { get; init; }

        public string? TypeGuess { get; init; }
        public string? Keywords { get; init; }
        public string? Summary { get; init; }
        public decimal? TagsConfidence { get; init; }
        public decimal? DocPurposeConfidence { get; init; }

        public string? Model { get; init; }
        public string? PromptCode { get; init; }
        public int? PromptVersion { get; init; }
        public int PromptTokens { get; init; }
        public int CompletionTokens { get; init; }
        public long DurationMs { get; init; }

        /// <summary>逐条告警（含 33 号 §3.3 输出校验的后端自动校正明细）</summary>
        public List<string> Warnings { get; init; } = new();

        public static SemanticOutcome Blocked(string message)
            => new() { Status = "blocked", Message = message };

        public static SemanticOutcome Failed(string message, List<string>? warnings = null)
            => new() { Status = "failed", Message = message, Warnings = warnings ?? new() };

        public static SemanticOutcome Skipped(
            string message, string? purpose, string? tagsJson, string? category, string? typeGuess,
            string? keywords, string? summary, decimal? tagsConf, decimal? purposeConf,
            AnalyzeForQueueResult groupRes,
            AnalyzeForQueueResult contentRes, List<string> warnings)
            => new()
            {
                Status = "skipped",
                Message = message,
                DocPurpose = purpose,
                TagsJson = tagsJson,
                SuggestedCategory = category,
                TypeGuess = typeGuess,
                Keywords = keywords,
                Summary = summary,
                TagsConfidence = tagsConf,
                DocPurposeConfidence = purposeConf,
                Model = contentRes.Success ? contentRes.Model : groupRes.Model,
                PromptTokens = groupRes.PromptTokens + contentRes.PromptTokens,
                CompletionTokens = groupRes.CompletionTokens + contentRes.CompletionTokens,
                DurationMs = groupRes.DurationMs + contentRes.DurationMs,
                Warnings = warnings,
            };
    }

    // ════════════════════════════════════════════════════════════════════
    // 四、私有
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 分类归一 + 白名单校验。
    /// <para>只允许 <c>fixed</c> / <c>editable</c>；<c>hybrid</c> 一期不启用（37 号 Q-10）。
    /// 入参为空 ⇒ 回退 <paramref name="fallback"/>，再空 ⇒ <c>editable</c>。
    /// ⛔ 非法值返回 <c>null</c> 让调用方报错，<b>不静默改成 editable</b> —— 静默改值会让用户以为设置生效了。</para>
    /// </summary>
    private static string? NormalizeCategory(string? input, string? fallback)
    {
        var v = (input ?? string.Empty).Trim().ToLowerInvariant();
        if (v.Length == 0) v = (fallback ?? string.Empty).Trim().ToLowerInvariant();
        if (v.Length == 0) return "editable";

        return v switch
        {
            "fixed" => "fixed",
            "editable" => "editable",
            _ => null,
        };
    }

    /// <summary>
    /// JSON <b>数组</b>列的空值归一：空串 / 纯空白 ⇒ <c>"[]"</c>，否则原样（去首尾空白）。
    ///
    /// <para><b>为什么必须做</b>（2026-10-04 实测）：<c>cert_standard_doc_contract.TagsJson</c> 是
    /// MySQL <c>json</c> 列，写空串会抛 <c>Invalid JSON text: "The document is empty." at position 0</c>
    /// —— 而且是在 <c>INSERT</c> 阶段抛出，<b>整条契约保存失败</b>，前端只看到「新增失败」。
    /// 前端已经改为提交 <c>[]</c>，此处是<b>第二道闸</b>：任何调用方（脚本 / 后续 AI 批量写入）
    /// 漏传空串都不会再炸。</para>
    /// </summary>
    private static string ToJsonArray(string? raw)
        => string.IsNullOrWhiteSpace(raw) ? "[]" : raw.Trim();

    /// <summary>
    /// JSON <b>对象</b>列的空值归一：空串 / 纯空白 ⇒ <c>null</c>（该列可空，语义 = 「未配置指纹」）。
    /// <para>⛔ 不能写 <c>"{}"</c> —— 指纹规则的判空口径是 <c>IS NULL</c>，写 <c>{}</c> 会让
    /// 「没有指纹」与「指纹为空对象」变成两种状态，匹配侧要多处理一个分支。</para>
    /// </summary>
    private static string? ToJsonObject(string? raw)
        => string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();

    /// <summary>保存契约的请求体</summary>
    public sealed class ContractSaveRequest
    {
        /// <summary>标准文件 Code（<c>cert_standard_directory_file.Code</c>，必填）</summary>
        public string StandardFileCode { get; set; } = string.Empty;

        /// <summary>文档名称（空 = 取文件行的 FileName）</summary>
        public string? DocName { get; set; }

        /// <summary>★ 是否不需要编辑：<c>fixed</c>（固定格式，免填）/ <c>editable</c>（要配填写规则）</summary>
        public string? DocCategory { get; set; }

        /// <summary>
        /// ★【文档约束】必要 / 非必要（2026-10-16 定稿，`67` 号 §4.2）。
        ///
        /// <para><c>true</c> = 必要文档（未生成 ⇒ 计入缺口）/
        /// <c>false</c> = 非必要文档（生成不出来 ⇒ 可忽略，编排器记 <c>skipped_optional</c>）。</para>
        ///
        /// <para>⚠️ <c>null</c> = <b>本次不动</b>该字段（⛔ 不冲回默认），与 DocPurpose / TagsJson 同口径。</para>
        /// <para>权威位置 = <c>cert_standard_directory_file.IsRequired</c>（<b>标准行</b>）。</para>
        /// </summary>
        public bool? IsRequired { get; set; }

        /// <summary>
        /// ★【是否依赖企业文档】（2026-10-16 定稿，`67` 号 §4.2）。
        ///
        /// <para><c>true</c> = <b>依赖</b>企业文档（内容来自企业交上来的资料，要去匹配 / 取用）/
        /// <c>false</c> = <b>不依赖</b>（标准自带的内容 —— 导航页 / 固定描述 —— 企业不用交，
        /// 后台按规则自动生成并入库）。</para>
        ///
        /// <para>⚠️ <c>null</c> = <b>本次不动</b>该字段。权威位置 =
        /// <c>cert_standard_directory_file.Replaceable</c>（<b>标准行</b>）。</para>
        ///
        /// <para>⛔ 本 DTO 不再有 <c>DocRole</c> / <c>FixedDocSubtype</c> —— 那两个契约表列已**物理删除**，
        /// 见 `67` 号 §4.1。旧前端若仍传这两个字段，会被<b>静默忽略</b>（不报错）。</para>
        /// </summary>
        public bool? Replaceable { get; set; }

        /// <summary>文档作用（四段式人读文本）</summary>
        public string? DocPurpose { get; set; }

        /// <summary>标签数组 JSON（值必须 ∈ <c>cert_tag_dict.TagCode</c>）</summary>
        public string? TagsJson { get; set; }

        /// <summary>包含信息结构化清单 JSON</summary>
        public string? InfoItemsJson { get; set; }

        /// <summary>指纹规则集 JSON（<c>fixed</c> 文档专用）</summary>
        public string? FingerprintJson { get; set; }
    }
}

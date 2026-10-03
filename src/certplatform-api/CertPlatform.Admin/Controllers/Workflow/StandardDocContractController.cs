using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using CertPlatform.Admin.Entities.Doc;
using CertPlatform.Admin.Services.DocExtraction;
using CertPlatform.Shared.DocExtraction;
using CertPlatform.Shared.Entities.Dir;
using YZH.Core.Api.Controllers;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Result;

namespace CertPlatform.Admin.Controllers.Workflow;

/// <summary>
/// <b>标准文档契约</b>控制器（<c>cert_standard_doc_contract</c>）。
///
/// <para><b>路由前缀</b>：<c>/api/Admin/Workflow/StandardDocContract</c>（含 <c>Admin/</c> 端标记，
/// 与后台端其余 Controller 一致；<c>ApiCode</c> 取路由<b>末段</b>，加端标记不影响授权关联）。</para>
///
/// <para><b>基类说明</b>：继承 <see cref="WebControllerBase"/> 而非 <c>YzhControllerBase&lt;V&gt;</c> ——
/// 契约表<b>没有</b> EntityConfig 与页面配置驱动的 CRUD 需求，继承后者只会白带六个通用端点。</para>
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
    private readonly DocExtractionRuleService _extraction;
    private readonly IUserContext _userContext;
    private readonly ILogger<StandardDocContractController> _logger;

    public StandardDocContractController(
        IDbOrm db,
        DocExtractionRuleService extraction,
        IUserContext userContext,
        ILogger<StandardDocContractController> logger)
    {
        _db = db;
        _extraction = extraction;
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

        var fileRow = (await _db.GetOneAsync<StandardDirectoryFile>(f => f.Code == fileCode)).Data;
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

            DocName = contract?.DocName ?? fileRow.FileName,
            DocRole = contract?.DocRole ?? "required",
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

        var fileRow = (await _db.GetOneAsync<StandardDirectoryFile>(f => f.Code == req.StandardFileCode)).Data;
        if (fileRow == null)
            return Ok(ApiResponse<object>.Fail("标准目录文件不存在或已删除"));

        // ★ 分类归一 + 白名单校验（hybrid 一期不启用，Q-10；传了也不落库）
        var category = NormalizeCategory(req.DocCategory, fileRow.DocCategory);
        if (category == null)
            return Ok(ApiResponse<object>.Fail("文档分类只能是 fixed / editable（hybrid 一期不启用）"));

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
        if (!string.IsNullOrWhiteSpace(req.DocRole)) contract.DocRole = req.DocRole!;

        // 只在「显式提交」时改写 —— null = 本次不动该字段（避免前端漏传导致清空）
        if (req.DocPurpose != null)
        {
            contract.DocPurpose = req.DocPurpose;
            contract.DocPurposeSource = string.IsNullOrWhiteSpace(req.DocPurpose) ? null : "manual";
        }
        if (req.TagsJson != null)
        {
            contract.TagsJson = req.TagsJson;
            contract.TagsSource = string.IsNullOrWhiteSpace(req.TagsJson) ? null : "manual";
        }
        if (req.InfoItemsJson != null) contract.InfoItemsJson = req.InfoItemsJson;
        if (req.FingerprintJson != null) contract.FingerprintJson = req.FingerprintJson;

        // ★ 人工编辑留痕：批量重跑不得覆盖
        contract.IsManualCorrected = true;
        contract.UpdateBy = user;
        contract.UpdateTime = now;
        if (isNew || contract.AnalyzeStatus == "pending") contract.AnalyzeStatus = "manual";

        var saveResult = isNew
            ? await _db.InsertAsync(contract)
            : await _db.UpdateAsync(contract);

        if (!saveResult.Success)
            return Ok(ApiResponse<object>.Fail(saveResult.Error ?? "契约保存失败"));

        // ★ 同批同步权威列（DocCategory）—— 两处写同一请求，保证永不不一致
        if (!string.Equals(fileRow.DocCategory, category, StringComparison.Ordinal))
        {
            fileRow.DocCategory = category;
            fileRow.UpdateBy = user;
            fileRow.UpdateTime = now;
            var sync = await _db.UpdateAsync(
                fileRow, nameof(StandardDirectoryFile.DocCategory), nameof(StandardDirectoryFile.UpdateTime));
            if (!sync.Success)
            {
                // 不阻塞主保存，但必须留日志 —— 否则两处静默分叉
                _logger.LogWarning("[Contract] 契约已保存，但同步标准目录行 DocCategory 失败：File={File}, Err={Err}",
                    req.StandardFileCode, sync.Error);
            }
        }

        _logger.LogInformation("[Contract] 契约已保存：File={File}, Category={Cat}, New={New}, By={User}",
            req.StandardFileCode, category, isNew, user);

        return Ok(ApiResponse<object>.Ok(new
        {
            contract.Code,
            contract.StandardFileCode,
            contract.DocName,
            contract.DocCategory,
            contract.DocPurpose,
            contract.TagsJson,
            contract.InfoItemsJson,
            contract.FingerprintJson,
            contract.AnalyzeStatus,
            contract.IsManualCorrected,
        }, isNew ? "契约已创建" : "契约已保存"));
    }

    // ════════════════════════════════════════════════════════════════════
    // 三、分析：聚合触发（复用已有分析能力，⛔ 不新写 LLM 逻辑）
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>自动分析</b>（用户 2026-10-03 要求「每个文件应该有个自动分析按钮」）。
    ///
    /// <para><b>★ 一次调用聚合两个分析</b>（37 号 §0.2 的 A / B / C 三个分析中的 C 与 A）：</para>
    /// <list type="bullet">
    /// <item><b>C 字段提取</b>：调 <see cref="DocExtractionRuleService.AIAnalyzeAsync"/> ——
    /// 已实现，⛔ 不重复造。</item>
    /// <item><b>A 锚点扫描</b>：需要<b>空白模板</b>（37 号 H-2 硬约束）。未上传时本项<b>跳过并提示</b>，
    /// ⛔ 不报错 —— 用户的实际顺序是先分析原始文档、再上传空白模板。</item>
    /// </list>
    ///
    /// <para><b>⚠️ B 文档语义分析（分类/作用/标签）本轮未接</b>：它需要 LLM 提示词（<c>doc_group</c> /
    /// <c>doc_content</c>）+ 标签字典裁剪，属「企业原始资料分析」执行器
    /// （<c>EnterpriseOriginalAnalyzeExecutor</c>）的既有能力，接口尚未对本页开放。
    /// 本轮先返回 <c>Semantic.Status = "not_wired"</c>，前端据此提示「可手工在 Tab3 填写」。</para>
    /// </summary>
    [HttpPost("analyze")]
    public async Task<IActionResult> Analyze([FromQuery] string fileCode)
    {
        if (string.IsNullOrWhiteSpace(fileCode))
            return Ok(ApiResponse<object>.Fail("缺少标准文件编码（fileCode）"));

        var fileRow = (await _db.GetOneAsync<StandardDirectoryFile>(f => f.Code == fileCode)).Data;
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

        // ── A：锚点扫描（需要空白模板 —— H-2 硬约束）──
        var template = await _db.Client.Queryable<DocTemplate>()
            .Where(t => t.StandardFileCode == fileCode && t.IsDeleted == false)
            .FirstAsync();

        object scanResult = template == null
            ? new { Status = "blocked", Message = "尚未上传空白模板，无法扫描锚点（请先「下载标准文档 → 加工 → 上传空白模板」）" }
            : new { Status = "not_wired", Message = "扫描器本轮尚未接入（需先扩展 WordDocumentScanner / ExcelSheetScanner）" };

        return Ok(ApiResponse<object>.Ok(new
        {
            StandardFileCode = fileCode,
            Semantic = new { Status = "not_wired", Message = "语义分析（分类/作用/标签）本轮未接，可先在「文档契约」页手工填写" },
            Field = fieldResult,
            Scan = scanResult,
            // ★ 前端据此决定下一步引导
            NextStep = template == null ? "upload-template" : "rescan",
        }));
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

    /// <summary>保存契约的请求体</summary>
    public sealed class ContractSaveRequest
    {
        /// <summary>标准文件 Code（<c>cert_standard_directory_file.Code</c>，必填）</summary>
        public string StandardFileCode { get; set; } = string.Empty;

        /// <summary>文档名称（空 = 取文件行的 FileName）</summary>
        public string? DocName { get; set; }

        /// <summary>★ 是否不需要编辑：<c>fixed</c>（固定格式，免填）/ <c>editable</c>（要配填写规则）</summary>
        public string? DocCategory { get; set; }

        /// <summary>required / optional / reference / attachment</summary>
        public string? DocRole { get; set; }

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

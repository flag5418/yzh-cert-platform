using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using NPOI.XSSF.UserModel;
using NPOI.XWPF.UserModel;
using YZH.Core.Api.Controllers;
using YZH.Core.Api.Services;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Helpers;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Config;
using YZH.Core.Stand.Models.Result;
using CertPlatform.Admin.Entities.Doc;
using CertPlatform.Shared.Office.Excel;
using CertPlatform.Shared.Office.Word;

namespace CertPlatform.Admin.Controllers.Workflow;

/// <summary>
/// 模板锚点规则控制器（「标准文档填写规则」页面的右表 —— 左树右表样板的右侧）
///
/// <para><b>路由前缀</b>：<c>/api/Admin/Workflow/DocTemplateAnchor</c></para>
/// <para><b>数据库</b>：<c>cert_doc_template_anchor</c></para>
///
/// <para><b>业务定位</b>：一行 = 一条「模板里的哪个位置 ← 填什么值」的规则，
/// 是 <c>DocumentFillEngine</c> 的输入。</para>
///
/// <para><b>★ 唯一键 <c>uk_tpl_anchor</c></b> =
/// <c>(TemplateCode, AnchorType, AnchorKind, SheetName, SectionIndex, HeaderKind, AnchorRef)</c>
/// 共 <b>7 列</b>。三个定位列（<c>SheetName</c> / <c>SectionIndex</c> / <c>HeaderKind</c>）是
/// <b>NOT NULL</b> 且「不适用」写空串/0 —— ⛔ 不能写 NULL，因为 MySQL 唯一索引里 NULL 不互相冲突，
/// 写 NULL 会让同一模板插入任意多条重复锚点。</para>
///
/// <para><b>★ 列级写入（陷阱 ㉕）</b>：<see cref="BatchUpdatableColumns"/> 显式列清单 ——
/// 并发/多轮写同一行时若整行写回，会把别的来源刚写的列覆盖掉且日志显示成功。</para>
/// </summary>
[ApiController]
[Route("api/Admin/Workflow/DocTemplateAnchor")]
public class DocTemplateAnchorController : YzhControllerBase<DocTemplateAnchor>
{
    private readonly IDbOrm _db;
    private readonly IObjectStorage _storage;
    private readonly ILogger<DocTemplateAnchorController> _logger;

    public DocTemplateAnchorController(
        EntityService<DocTemplateAnchor> entityService,
        IUserContext userContext,
        IDbOrm db,
        IObjectStorage storage,
        ILogger<DocTemplateAnchorController> logger)
        : base(entityService, userContext)
    {
        _db = db;
        _storage = storage;
        _logger = logger;
    }

    /// <summary>★ 开发期强制暴露缺配置问题（缺 JSON 直接 throw，不静默空白）</summary>
    protected override bool StrictConfigLoad => true;

    /// <summary>加载 EntityConfig 配置</summary>
    protected override EntityConfig LoadConfig()
    {
        return EntityConfigHelper.GetConfig<DocTemplateAnchor>();
    }

    // ════════════════════════════════════════════════════════════════════
    // 一、常量 / 受控值
    // ════════════════════════════════════════════════════════════════════

    /// <summary>锚点类型受控值（L3 提交期校验；来源：DDL 注释 + 22 号补入 block）</summary>
    private static readonly HashSet<string> AnchorTypes = new(StringComparer.Ordinal)
    {
        "scalar", "block", "table", "table_total", "domain",
    };

    /// <summary>定位方式受控值</summary>
    private static readonly HashSet<string> AnchorKinds = new(StringComparer.Ordinal)
    {
        "token", "bookmark", "range",
    };

    /// <summary>写入方式受控值</summary>
    private static readonly HashSet<string> WriteModes = new(StringComparer.Ordinal)
    {
        "replace", "overwrite", "append", "remove",
    };

    /// <summary>值类型受控值</summary>
    private static readonly HashSet<string> ValueTypes = new(StringComparer.Ordinal)
    {
        "text", "number", "date", "bool", "enum",
    };

    /// <summary>
    /// ★ 批量保存时允许整行覆盖的列（<b>列级写入</b>）。
    /// <para>⛔ 不含 <c>Code</c>（定位键，改了就变成另一行）、<c>Id</c>、<c>CreateTime</c>、<c>CreateBy</c>。</para>
    /// </summary>
    private static readonly string[] BatchUpdatableColumns =
    {
        nameof(DocTemplateAnchor.AnchorType),
        nameof(DocTemplateAnchor.AnchorKind),
        nameof(DocTemplateAnchor.AnchorRef),
        nameof(DocTemplateAnchor.SheetName),
        nameof(DocTemplateAnchor.SectionIndex),
        nameof(DocTemplateAnchor.HeaderKind),
        nameof(DocTemplateAnchor.DomainKind),
        nameof(DocTemplateAnchor.FieldCode),
        nameof(DocTemplateAnchor.ColumnsJson),
        nameof(DocTemplateAnchor.TokenModifiersJson),
        nameof(DocTemplateAnchor.SourceSpec),
        nameof(DocTemplateAnchor.SourceSummary),
        nameof(DocTemplateAnchor.WriteMode),
        nameof(DocTemplateAnchor.OriginalText),
        nameof(DocTemplateAnchor.ConditionJson),
        nameof(DocTemplateAnchor.ValueType),
        nameof(DocTemplateAnchor.DefaultText),
        nameof(DocTemplateAnchor.NumberFormat),
        nameof(DocTemplateAnchor.MergeJson),
        nameof(DocTemplateAnchor.StyleJson),
        nameof(DocTemplateAnchor.MarkStyleName),
        nameof(DocTemplateAnchor.Required),
        nameof(DocTemplateAnchor.IsOrphan),
        nameof(DocTemplateAnchor.Sort),
        nameof(DocTemplateAnchor.Remark),
        nameof(DocTemplateAnchor.IsValid),
        nameof(DocTemplateAnchor.IsDeleted),
        nameof(DocTemplateAnchor.DeleteBy),
        nameof(DocTemplateAnchor.DeleteTime),
        nameof(DocTemplateAnchor.UpdateTime),
    };

    // ════════════════════════════════════════════════════════════════════
    // 二、写入覆写（含已删查重 + 就地复活）
    // ════════════════════════════════════════════════════════════════════

    /// <summary>新增：规范化 + 校验 + 含已删查重（命中已删行就地复活，⛔ 不走 INSERT 以免撞主键）</summary>
    public override async Task<Result<DocTemplateAnchor>> AddCore(DocTemplateAnchor entity)
    {
        Normalize(entity);
        var err = Validate(entity);
        if (err != null) return Result<DocTemplateAnchor>.Fail(err);

        var tplErr = await EnsureTemplateAsync(entity.TemplateCode);
        if (tplErr != null) return Result<DocTemplateAnchor>.Fail(tplErr);

        var existing = await FindByUniqueKeyAsync(entity);
        if (existing != null)
        {
            if (!existing.IsDeleted)
                return Result<DocTemplateAnchor>.Fail(
                    $"同一模板下已存在相同锚点（{entity.AnchorType}/{entity.AnchorKind}/{entity.AnchorRef}）。");

            // 复活：沿用同 Code（填充日志可能已引用它）
            entity.Code = existing.Code;
            entity.Id = existing.Id;
            entity.CreateTime = existing.CreateTime;
            entity.CreateBy = existing.CreateBy;
            entity.IsDeleted = false;
            entity.DeleteBy = null;
            entity.DeleteTime = null;
            entity.UpdateTime = DateTime.Now;

            var upd = await _db.UpdateAsync(entity, BatchUpdatableColumns);
            if (!upd.Success) return Result<DocTemplateAnchor>.Fail(upd.Error ?? "复活锚点失败");

            _logger.LogInformation("[DocTemplateAnchor] 复活已软删锚点：{Tpl}/{Ref}", entity.TemplateCode, entity.AnchorRef);
            return Result<DocTemplateAnchor>.Ok(entity);
        }

        return await base.AddCore(entity);
    }

    /// <summary>修改：规范化 + 校验 + 唯一键查重（排除自身）</summary>
    public override async Task<Result<DocTemplateAnchor>> UpdateCore(DocTemplateAnchor entity)
    {
        if (string.IsNullOrWhiteSpace(entity.Code))
            return Result<DocTemplateAnchor>.Fail("更新失败：缺少业务键 Code");

        Normalize(entity);
        var err = Validate(entity);
        if (err != null) return Result<DocTemplateAnchor>.Fail(err);

        var tplErr = await EnsureTemplateAsync(entity.TemplateCode);
        if (tplErr != null) return Result<DocTemplateAnchor>.Fail(tplErr);

        var dup = await FindByUniqueKeyAsync(entity);
        if (dup != null && !string.Equals(dup.Code, entity.Code, StringComparison.Ordinal))
        {
            return Result<DocTemplateAnchor>.Fail(
                dup.IsDeleted
                    ? $"目标唯一键下存在一条<b>已删除</b>的锚点（Code={dup.Code}）。请先删除本行，再重新保存（届时会自动复活那一行）。"
                    : "同一模板下已存在相同锚点（类型/定位方式/引用 三者相同）。");
        }

        return await base.UpdateCore(entity);
    }

    // ════════════════════════════════════════════════════════════════════
    // 三、自定义端点
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>批量保存</b>：按唯一键 upsert（命中 ⇒ 列级更新并复活；未命中 ⇒ 插入）。
    /// <para>整个批次在<b>单事务</b>内完成 —— 任一条失败即整体回滚，避免「半批锚点生效」。</para>
    /// <para>⛔ 不删除本批次未出现的锚点：删除走 <c>delete</c> 或 <c>clear</c>，显式且可审计。</para>
    ///
    /// <summary>
    /// <b>扫描空白模板 → 生成锚点清单</b>（37 号 §7.1 第 ⑤ 步 / §7.2）。
    ///
    /// <para><b>★ 零 LLM</b>（H-3）：模板自己声明了要填什么，扫描是「读声明」不是「猜意图」。
    /// 复用 <see cref="WordDocumentScanner.ScanAnchors"/> / <see cref="ExcelSheetScanner.ScanAnchors"/>
    /// —— ⛔ 不新写第二套解析（Q-6）。</para>
    ///
    /// <para><b>★ 幂等与孤儿</b>（37 号 §7.2）：按 <c>uk_tpl_anchor</c> 做 upsert；
    /// 本次<b>没扫到</b>的旧锚点标 <c>IsOrphan=1</c>，⛔ <b>不删除</b> —— 它可能已有填过的值
    /// （21 号 §4.3）。<c>IsOrphan</c> 的锚点在页面上进「黄牌」清单，由人工决定去留。</para>
    ///
    /// <para><b>★ 只更新「扫描能确定的列」</b>：<c>AnchorType</c> / <c>AnchorRef</c> /
    /// <c>FieldCode</c> / <c>DomainKind</c> / <c>Sort</c> / <c>IsOrphan</c>。
    /// ⛔ <b>绝不触碰</b> <c>SourceSpec</c> / <c>WriteMode</c> / <c>Required</c> 等<b>人工配置列</b> ——
    /// 重扫一次就把实施人员配了半天的取值来源清空，是不可接受的（陷阱 ㉕ 的同源问题）。</para>
    ///
    /// <para><b>⛔ 首版边界</b>：Word 侧只扫 <c>{{Token}}</c>（书签 / <c>YZH_Mark</c> 未覆盖）；
    /// Excel 侧只扫单元格 <c>{{Token}}</c>（不推断连续数据区行范围）。原因见两个扫描器的类注释 ——
    /// <b>当前没有任何真实空白模板可验证</b>，不写无法验证的启发式代码。</para>
    /// </summary>
    /// <param name="templateCode">模板 Code（<c>cert_doc_template.Code</c>）</param>
    /// <param name="force">true = 强制重扫（默认 false：已扫过且非 draft 状态则跳过）</param>
    [HttpPost("scan")]
    public async Task<IActionResult> Scan([FromQuery] string templateCode, [FromQuery] bool force = false)
    {
        if (string.IsNullOrWhiteSpace(templateCode))
            return Ok(ApiResponse<object>.Fail("请指定所属模板（templateCode）"));

        var tpl = (await _db.GetOneAsync<DocTemplate>(t => t.Code == templateCode)).Data;
        if (tpl == null)
            return Ok(ApiResponse<object>.Fail("模板不存在或已删除，请先上传空白模板"));
        if (string.IsNullOrWhiteSpace(tpl.StoragePath))
            return Ok(ApiResponse<object>.Fail("该模板还没有上传文件，请先「上传空白模板」"));

        var kind = (tpl.FileKind ?? string.Empty).Trim().ToLowerInvariant();
        if (kind != "docx" && kind != "xlsx")
            return Ok(ApiResponse<object>.Fail($"不支持的模板类型「{tpl.FileKind}」（只支持 docx / xlsx）"));

        // ★ 幂等：已扫过且已离开 draft ⇒ 跳过（force 可强制重扫）
        if (!force && tpl.ScanTime != null && tpl.PublishStatus != "draft")
        {
            return Ok(ApiResponse<object>.Ok(new
            {
                Skipped = true,
                tpl.ScanTime,
                tpl.PartCount,
                tpl.PublishStatus,
            }, "模板未变化，已跳过重扫（如需强制重扫请传 force=true）"));
        }

        // 标记扫描中（失败也要留痕，否则前端永远转圈）
        tpl.ScanStatus = "processing";
        tpl.ScanMessage = null;
        await _db.UpdateAsync(tpl, nameof(DocTemplate.ScanStatus), nameof(DocTemplate.ScanMessage));

        List<ScannedAnchor> scanned;
        try
        {
            var (stream, _) = await _storage.DownloadAsync(tpl.StoragePath.TrimStart('/'));
            using (stream)
            {
                scanned = kind == "docx" ? ScanWordStream(stream) : ScanExcelStream(stream);
            }
        }
        catch (Exception ex)
        {
            tpl.ScanStatus = "failed";
            tpl.ScanMessage = Truncate(ex.Message, 1000);
            await _db.UpdateAsync(tpl, nameof(DocTemplate.ScanStatus), nameof(DocTemplate.ScanMessage));
            _logger.LogError(ex, "[DocTemplateAnchor] 扫描失败 Template={Tpl}, Path={Path}",
                templateCode, tpl.StoragePath);
            return Ok(ApiResponse<object>.Fail($"扫描失败：{ex.Message}"));
        }

        try
        {
            var (inserted, updated, orphaned) = await PersistScanAsync(templateCode, scanned);

            tpl.ScanStatus = "completed";
            tpl.ScanMessage = null;
            tpl.ScanTime = DateTime.Now;
            tpl.PartCount = scanned.Count;
            tpl.BookmarkCount = scanned.Count(a => a.AnchorKind == "bookmark");
            tpl.MarkCount = 0;
            if (string.Equals(tpl.PublishStatus, "draft", StringComparison.Ordinal))
                tpl.PublishStatus = "scanned";

            await _db.UpdateAsync(tpl,
                nameof(DocTemplate.ScanStatus), nameof(DocTemplate.ScanMessage), nameof(DocTemplate.ScanTime),
                nameof(DocTemplate.PartCount), nameof(DocTemplate.BookmarkCount), nameof(DocTemplate.MarkCount),
                nameof(DocTemplate.PublishStatus));

            _logger.LogInformation(
                "[DocTemplateAnchor] 扫描完成 Template={Tpl}：新增 {Ins} / 更新 {Upd} / 孤儿 {Orp} / 共 {Total}",
                templateCode, inserted, updated, orphaned, scanned.Count);

            return Ok(ApiResponse<object>.Ok(new
            {
                TemplateCode = templateCode,
                Total = scanned.Count,
                Inserted = inserted,
                Updated = updated,
                Orphaned = orphaned,
                ScannedAt = tpl.ScanTime,
            }, $"扫描完成：识别到 {scanned.Count} 个锚点（新增 {inserted} / 更新 {updated} / 标记孤儿 {orphaned}）"));
        }
        catch (Exception ex)
        {
            tpl.ScanStatus = "failed";
            tpl.ScanMessage = Truncate(ex.Message, 1000);
            await _db.UpdateAsync(tpl, nameof(DocTemplate.ScanStatus), nameof(DocTemplate.ScanMessage));
            _logger.LogError(ex, "[DocTemplateAnchor] 锚点落库失败 Template={Tpl}", templateCode);
            return Ok(ApiResponse<object>.Fail($"锚点落库失败：{ex.Message}"));
        }
    }

    /// <summary>
    /// <b>校验（三色）</b>（37 号 §3.4 Tab4）。
    ///
    /// <list type="table">
    ///   <listheader><term>色</term><description>规则 / 是否阻断发布</description></listheader>
    ///   <item><term>🔴 红牌</term><description>非法组合（<c>domain(auto)</c>+来源 / <c>table_total</c>+manual /
    ///   <c>scalar</c>+remove）—— <b>阻断发布</b></description></item>
    ///   <item><term>🟡 黄牌</term><description>锚点未配取值来源 / <c>IsOrphan=1</c> —— 进清单，不阻断</description></item>
    ///   <item><term>🟢</term><description>通过</description></item>
    /// </list>
    ///
    /// <para><b>⚠️ 首版未覆盖的两条红牌</b>（37 号 §3.4 Tab4 的 ① ②）：
    /// 「锚点有、字段定义无」需要 <c>cert_doc_field_def</c>（当前仅 1 行，不足以支撑判定）；
    /// 「<c>{{Token}}</c> 跨 run 未归一（W9）」需要在扫描期记录 run 切分信息（首版扫描器未记录）。
    /// 两者<b>不是遗漏而是暂缓</b> —— 见 <see cref="Scan"/> 的边界说明。判定口径一旦落定会在此处补齐。</para>
    ///
    /// <para>结果写 <c>DocTemplate.ViolationJson</c>，并据红牌数把 <c>PublishStatus</c> 推进到
    /// <c>ready</c>（无红牌且有锚点）或退回 <c>scanned</c>。</para>
    /// </summary>
    [HttpPost("validate")]
    public async Task<IActionResult> ValidateTemplate([FromQuery] string templateCode)
    {
        if (string.IsNullOrWhiteSpace(templateCode))
            return Ok(ApiResponse<object>.Fail("请指定所属模板（templateCode）"));

        var tpl = (await _db.GetOneAsync<DocTemplate>(t => t.Code == templateCode)).Data;
        if (tpl == null) return Ok(ApiResponse<object>.Fail("模板不存在或已删除"));

        var violations = await BuildViolationsAsync(templateCode);
        var errors = violations.Count(v => v.Level == "error");
        var warnings = violations.Count(v => v.Level == "warning");

        var canPublish = errors == 0 && tpl.PartCount > 0;

        // ★ 阻断原因的唯一口径：前端按钮的禁用提示直接读它，⛔ 不复算
        //   （「0 红牌却仍不能发布」的真实原因是「还没有锚点」，必须说清楚，
        //    否则界面会显示「全部通过」而发布按钮又不可用 —— 自相矛盾）
        string? blockReason = errors > 0
            ? $"存在 {errors} 个红牌问题，不能发布"
            : tpl.PartCount == 0
                ? "该模板还没有锚点，请先「重新扫描」"
                : null;

        tpl.ViolationJson = violations.Count == 0 ? null : JsonSerializer.Serialize(violations);
        // 无红牌 + 有锚点 ⇒ ready（可发布）；否则退回 scanned
        // ★ 但**不降级 `published`** —— 校验是只读动作，不该把已发布的模板打回 ready
        //   （实测踩过：发布后点一次「校验」，状态徽标从「已发布」掉回「可发布」）。
        //   仅当出现红牌时才退回 scanned（此时模板确实不可再用）。
        tpl.PublishStatus = errors > 0
            ? "scanned"
            : tpl.PublishStatus == "published"
                ? "published"
                : canPublish ? "ready" : "scanned";
        await _db.UpdateAsync(tpl,
            nameof(DocTemplate.ViolationJson), nameof(DocTemplate.PublishStatus));

        return Ok(ApiResponse<object>.Ok(new
        {
            TemplateCode = templateCode,
            ErrorCount = errors,
            WarningCount = warnings,
            Violations = violations,
            AnchorCount = tpl.PartCount,
            PublishStatus = tpl.PublishStatus,
            CanPublish = canPublish,
            BlockReason = blockReason,
        }, errors == 0 ? "校验通过" : $"发现 {errors} 个红牌问题，不能发布"));
    }

    /// <summary>
    /// <b>发布</b>（37 号 §7.1 第 ⑦ 步）：<c>PublishStatus=published</c>，该标准文档此后可用此模板生成。
    ///
    /// <para><b>★ 硬前置</b>：<b>无红牌</b>且<b>有锚点</b>。发布前<b>重新校验一遍</b>
    /// （⛔ 不信任库里缓存的 <c>ViolationJson</c> —— 锚点可能在「校验」之后又被改过）。</para>
    /// </summary>
    [HttpPost("publish")]
    public async Task<IActionResult> Publish([FromQuery] string templateCode)
    {
        if (string.IsNullOrWhiteSpace(templateCode))
            return Ok(ApiResponse<object>.Fail("请指定所属模板（templateCode）"));

        var tpl = (await _db.GetOneAsync<DocTemplate>(t => t.Code == templateCode)).Data;
        if (tpl == null) return Ok(ApiResponse<object>.Fail("模板不存在或已删除"));

        if (tpl.PartCount == 0)
            return Ok(ApiResponse<object>.Fail("该模板还没有锚点，请先「重新扫描」"));

        var violations = await BuildViolationsAsync(templateCode);
        var errors = violations.Count(v => v.Level == "error");
        if (errors > 0)
            return Ok(ApiResponse<object>.Fail($"存在 {errors} 个红牌问题，不能发布（请先解决「校验结果」页的红牌）"));

        tpl.PublishStatus = "published";
        tpl.ViolationJson = violations.Count == 0 ? null : JsonSerializer.Serialize(violations);
        await _db.UpdateAsync(tpl,
            nameof(DocTemplate.PublishStatus), nameof(DocTemplate.ViolationJson));

        _logger.LogInformation("[DocTemplateAnchor] 模板已发布 Template={Tpl}, Anchors={N}",
            templateCode, tpl.PartCount);

        return Ok(ApiResponse<object>.Ok(new
        {
            tpl.Code,
            tpl.StandardFileCode,
            tpl.PublishStatus,
            AnchorCount = tpl.PartCount,
        }, "模板已发布"));
    }

    /// <summary>
    /// <para><b>★ 语义 = 整行 upsert（不是部分更新）</b>：每条 <c>items[i]</c> 视为该锚点的<b>完整状态</b>，
    /// 未在 JSON 里出现的字段会被写成其 CLR 默认值（<c>""</c> / <c>0</c> / <c>false</c>）。
    /// 实测踩过：先存 <c>required=true</c>，再发一条只带 <c>defaultText</c> 的「同键」条目，
    /// <c>required</c> 被静默改回 <c>false</c>。⇒ <b>前端编辑器必须提交完整行</b>。</para>
    /// </summary>
    [HttpPost("save-batch")]
    public async Task<IActionResult> SaveBatch([FromBody] SaveAnchorBatchRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.TemplateCode))
            return Ok(ApiResponse<object>.Fail("请先指定所属模板（templateCode）"));

        if (req.Items == null || req.Items.Count == 0)
            return Ok(ApiResponse<object>.Fail("锚点清单为空"));

        var tplErr = await EnsureTemplateAsync(req.TemplateCode);
        if (tplErr != null) return Ok(ApiResponse<object>.Fail(tplErr));

        // 批内查重：同一唯一键出现两次 ⇒ 后者会覆盖前者，属配置错误，直接拒绝
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < req.Items.Count; i++)
        {
            var item = req.Items[i];
            item.TemplateCode = req.TemplateCode;
            Normalize(item);

            var err = Validate(item);
            if (err != null)
                return Ok(ApiResponse<object>.Fail($"第 {i + 1} 条锚点校验失败：{err}"));

            if (!seen.Add(UniqueKeyOf(item)))
                return Ok(ApiResponse<object>.Fail(
                    $"第 {i + 1} 条锚点与前面的条目重复（类型/定位方式/引用 完全相同）：{item.AnchorRef}"));
        }

        using var tx = _db.BeginTransaction();
        try
        {
            var inserted = 0;
            var updated = 0;
            var now = DateTime.Now;

            for (var i = 0; i < req.Items.Count; i++)
            {
                var a = req.Items[i];

                var existing = await FindByUniqueKeyAsync(a);
                if (existing == null)
                {
                    // 仅「插入」时按批次顺序补默认排序 —— 更新路径 ⛔ 不改 Sort，
                    // 否则前端显式把 Sort 设成 0 会被静默改成批次下标
                    if (a.Sort == 0) a.Sort = i;

                    if (string.IsNullOrWhiteSpace(a.Code)) a.Code = Guid.NewGuid().ToString("N");
                    a.IsDeleted = false;
                    a.DeleteBy = null;
                    a.DeleteTime = null;
                    a.CreateTime = now;
                    a.UpdateTime = now;

                    var ins = await _db.InsertAsync(a);
                    if (!ins.Success)
                    {
                        tx.Rollback();
                        return Ok(ApiResponse<object>.Fail($"第 {i + 1} 条锚点插入失败：{ins.Error}"));
                    }
                    inserted++;
                }
                else
                {
                    a.Code = existing.Code;
                    a.Id = existing.Id;
                    a.CreateTime = existing.CreateTime;
                    a.CreateBy = existing.CreateBy;
                    a.IsDeleted = false;
                    a.DeleteBy = null;
                    a.DeleteTime = null;
                    a.UpdateTime = now;

                    var upd = await _db.UpdateAsync(a, BatchUpdatableColumns);
                    if (!upd.Success)
                    {
                        tx.Rollback();
                        return Ok(ApiResponse<object>.Fail($"第 {i + 1} 条锚点更新失败：{upd.Error}"));
                    }
                    updated++;
                }
            }

            tx.Commit();

            // ★ 重算 PartCount（锚点部件数）。
            //   为什么必须做：`PartCount` 既是「发布前置」（`CanPublish = 无红牌 && PartCount > 0`）
            //   又是左树徽标的依据，但**只有扫描器写它**。一旦锚点经本端点落库（扫描尚未跑过），
            //   就会出现「锚点表有 N 行、PartCount=0 ⇒ 校验说『还没有锚点』⇒ 永远发不出去」的死角。
            //   放在 Commit 之后：避免依赖「事务内能否读到未提交行」这一未定行为。
            var liveAnchors = await _db.Client.Queryable<DocTemplateAnchor>()
                .Where(a => a.TemplateCode == req.TemplateCode && a.IsDeleted == false && a.IsValid == 1)
                .CountAsync();
            var tplRow = (await _db.GetOneAsync<DocTemplate>(t => t.Code == req.TemplateCode)).Data;
            if (tplRow != null && tplRow.PartCount != liveAnchors)
            {
                tplRow.PartCount = liveAnchors;
                await _db.UpdateAsync(tplRow, nameof(DocTemplate.PartCount));
            }

            _logger.LogInformation("[DocTemplateAnchor] save-batch：模板 {Tpl} → 新增 {Ins} / 更新 {Upd} / 锚点总数 {N}",
                req.TemplateCode, inserted, updated, liveAnchors);

            return Ok(ApiResponse<object>.Ok(new
            {
                TemplateCode = req.TemplateCode,
                Inserted = inserted,
                Updated = updated,
                Total = req.Items.Count,
                AnchorCount = liveAnchors,
            }));
        }
        catch (Exception ex)
        {
            tx.Rollback();
            _logger.LogError(ex, "[DocTemplateAnchor] save-batch 失败 TemplateCode={Tpl}", req.TemplateCode);
            return Ok(ApiResponse<object>.Error($"批量保存失败：{ex.Message}"));
        }
    }

    /// <summary><b>清空某模板的全部锚点</b>（软删，留痕）。显式调用 —— ⛔ 不在 save-batch 里隐式触发。</summary>
    [HttpPost("clear")]
    public async Task<IActionResult> Clear([FromQuery] string templateCode)
    {
        if (string.IsNullOrWhiteSpace(templateCode))
            return Ok(ApiResponse<object>.Fail("请指定所属模板（templateCode）"));

        var codes = await _db.Client.Queryable<DocTemplateAnchor>()
            .Where(a => a.TemplateCode == templateCode && a.IsDeleted == false)
            .Select(a => a.Code)
            .ToListAsync();

        if (codes.Count == 0)
            return Ok(ApiResponse<object>.Ok(new { TemplateCode = templateCode, Deleted = 0 }));

        var del = await Entity.DeleteBatch(codes, clientIp: UserContext.ClientIp);
        if (!del.Success)
            return Ok(ApiResponse<object>.Fail(del.Error ?? "清空锚点失败"));

        _logger.LogInformation("[DocTemplateAnchor] 清空模板 {Tpl} 的 {N} 条锚点", templateCode, del.Data);

        return Ok(ApiResponse<object>.Ok(new { TemplateCode = templateCode, Deleted = del.Data }));
    }

    /// <summary>
    /// <b>唯一键清单</b>：给定模板下所有锚点的唯一键（含已软删）—— 供页面在保存前做前端预检，
    /// 提前暴露「即将撞已删行」的情况。
    /// </summary>
    [HttpGet("keys")]
    public async Task<IActionResult> Keys([FromQuery] string templateCode)
    {
        if (string.IsNullOrWhiteSpace(templateCode))
            return Ok(ApiResponse<object>.Fail("请指定所属模板（templateCode）"));

        var rows = await _db.Client.Queryable<DocTemplateAnchor>()
            .Where(a => a.TemplateCode == templateCode)
            .Select(a => new
            {
                a.Code,
                a.AnchorType,
                a.AnchorKind,
                a.AnchorRef,
                a.SheetName,
                a.SectionIndex,
                a.HeaderKind,
                a.FieldCode,
                a.ValueType,
                a.Required,
                a.Sort,
                a.IsDeleted,
                a.IsValid,
            })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(new
        {
            TemplateCode = templateCode,
            Total = rows.Count,
            LiveCount = rows.Count(r => !r.IsDeleted),
            DeletedCount = rows.Count(r => r.IsDeleted),
            Items = rows,
        }));
    }

    // ════════════════════════════════════════════════════════════════════
    // 四、私有：规范化 / 校验 / 查重
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 规范化：唯一键的三个 NOT NULL 定位列在「不适用」时必须落<b>空串/0</b>。
    /// <para>⛔ 不能留 NULL —— MySQL 唯一索引里 NULL 不互相冲突，会让重复锚点静默入库。</para>
    /// </summary>
    private static void Normalize(DocTemplateAnchor a)
    {
        a.TemplateCode = (a.TemplateCode ?? string.Empty).Trim();
        a.AnchorType = (a.AnchorType ?? string.Empty).Trim();
        a.AnchorRef = (a.AnchorRef ?? string.Empty).Trim();

        // ★ 唯一键成员：不适用 ⇒ 空串 / 0（⛔ 绝不 NULL）
        a.SheetName = (a.SheetName ?? string.Empty).Trim();
        a.HeaderKind = (a.HeaderKind ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(a.AnchorKind)) a.AnchorKind = "token";
        else a.AnchorKind = a.AnchorKind.Trim();

        if (string.IsNullOrWhiteSpace(a.WriteMode)) a.WriteMode = "overwrite";
        else a.WriteMode = a.WriteMode.Trim();

        if (string.IsNullOrWhiteSpace(a.ValueType)) a.ValueType = "text";
        else a.ValueType = a.ValueType.Trim();
    }

    /// <summary>L3 提交期校验：必填 + 受控值（没等级的规则 = 愿望）</summary>
    private static string? Validate(DocTemplateAnchor a)
    {
        if (string.IsNullOrWhiteSpace(a.TemplateCode)) return "所属模板不能为空";
        if (string.IsNullOrWhiteSpace(a.AnchorType)) return "锚点类型（AnchorType）不能为空";
        if (string.IsNullOrWhiteSpace(a.AnchorRef)) return "锚点引用（AnchorRef）不能为空";

        if (!AnchorTypes.Contains(a.AnchorType))
            return $"锚点类型「{a.AnchorType}」不合法（可选：{string.Join(" / ", AnchorTypes)}）";
        if (!AnchorKinds.Contains(a.AnchorKind))
            return $"定位方式「{a.AnchorKind}」不合法（可选：{string.Join(" / ", AnchorKinds)}）";
        if (!WriteModes.Contains(a.WriteMode))
            return $"写入方式「{a.WriteMode}」不合法（可选：{string.Join(" / ", WriteModes)}）";
        if (!ValueTypes.Contains(a.ValueType))
            return $"值类型「{a.ValueType}」不合法（可选：{string.Join(" / ", ValueTypes)}）";

        // 定位方式与定位参数的搭配（把「配了但引擎读不到」挡在提交期）
        if (a.AnchorKind == "range" && string.IsNullOrWhiteSpace(a.SheetName))
            return "定位方式为 range（Excel 区域）时，工作表名（SheetName）不能为空";
        if (a.AnchorKind == "token" && a.AnchorType == "table_total" && string.IsNullOrWhiteSpace(a.FieldCode))
            return "表格合计锚点（table_total）必须绑定语义字段（FieldCode）";

        return null;
    }

    /// <summary>确保所属模板存活（防止产生不可达的孤儿锚点）</summary>
    private async Task<string?> EnsureTemplateAsync(string templateCode)
    {
        var tpl = await _db.GetOneAsync<DocTemplate>(t => t.Code == templateCode);
        return tpl.Data == null ? "所属模板不存在或已删除，请先登记模板" : null;
    }

    /// <summary>
    /// 按 <c>uk_tpl_anchor</c> 的 7 列查一行，<b>含已软删</b>。
    /// <para>⛔ 不用 <c>GetOneIgnoreValidAsync</c>：它仍过滤软删除 ⇒ 复活分支永不触发。</para>
    /// </summary>
    private Task<DocTemplateAnchor?> FindByUniqueKeyAsync(DocTemplateAnchor a)
    {
        return _db.Client.Queryable<DocTemplateAnchor>()
            .Where(x => x.TemplateCode == a.TemplateCode
                        && x.AnchorType == a.AnchorType
                        && x.AnchorKind == a.AnchorKind
                        && x.SheetName == a.SheetName
                        && x.SectionIndex == a.SectionIndex
                        && x.HeaderKind == a.HeaderKind
                        && x.AnchorRef == a.AnchorRef)
            .FirstAsync();
    }

    /// <summary>唯一键的字符串投影（批内查重用；分隔符用不可见字符避免与业务值冲突）</summary>
    private static string UniqueKeyOf(DocTemplateAnchor a)
        => string.Join('\u0001', a.TemplateCode, a.AnchorType, a.AnchorKind,
            a.SheetName, a.SectionIndex.ToString(), a.HeaderKind, a.AnchorRef);

    // ════════════════════════════════════════════════════════════════════
    // 五、私有：扫描（下载 → 解析 → 归一 → 落库）
    // ════════════════════════════════════════════════════════════════════

    /// <summary>扫描结果的中间形态（Word / Excel 统一成一种，落库只写一遍）</summary>
    private sealed record ScannedAnchor(
        string AnchorRef, string AnchorType, string AnchorKind,
        string FieldCode, string SheetName, string? DomainKind, int Sort);

    private static List<ScannedAnchor> ScanWordStream(Stream stream)
    {
        var doc = new XWPFDocument(stream);
        return WordDocumentScanner.ScanAnchors(doc)
            .Select(a => new ScannedAnchor(
                a.AnchorRef, a.AnchorType, a.AnchorKind,
                a.FieldCode, string.Empty, a.DomainKind, a.Sort))
            .ToList();
    }

    private static List<ScannedAnchor> ScanExcelStream(Stream stream)
    {
        var wb = new XSSFWorkbook(stream);
        return ExcelSheetScanner.ScanAnchors(wb)
            .Select(a => new ScannedAnchor(
                a.AnchorRef, a.AnchorType, a.AnchorKind,
                a.FieldCode, a.SheetName, a.DomainKind, a.Sort))
            .ToList();
    }

    /// <summary>
    /// 把扫描结果落库（37 号 §7.2 的幂等 upsert）。
    ///
    /// <list type="number">
    /// <item>按 <c>uk_tpl_anchor</c> 查：没有 ⇒ 新增；有 ⇒ <b>只更新扫描列</b>。</item>
    /// <item>本次没扫到的存活锚点 ⇒ <c>IsOrphan=1</c>（⛔ 不删）。</item>
    /// </list>
    ///
    /// <para><b>⛔ 为什么更新时不整行写回</b>：锚点表上有一半列是<b>人工配置</b>的
    /// （<c>SourceSpec</c> / <c>WriteMode</c> / <c>Required</c> / <c>DefaultText</c> …）。
    /// 整行写回会把「扫描前刚配好的取值来源」清成默认值，且日志显示成功 —— 陷阱 ㉕ 的同源问题。</para>
    /// </summary>
    private async Task<(int inserted, int updated, int orphaned)> PersistScanAsync(
        string templateCode, List<ScannedAnchor> scanned)
    {
        var now = DateTime.Now;
        var inserted = 0;
        var updated = 0;

        using var tx = _db.BeginTransaction();
        try
        {
            var seenKeys = new HashSet<string>(StringComparer.Ordinal);

            foreach (var s in scanned)
            {
                var a = new DocTemplateAnchor
                {
                    TemplateCode = templateCode,
                    AnchorType = s.AnchorType,
                    AnchorKind = s.AnchorKind,
                    AnchorRef = s.AnchorRef,
                    SheetName = s.SheetName,
                    SectionIndex = 0,
                    HeaderKind = string.Empty,
                    DomainKind = s.DomainKind,
                    FieldCode = s.FieldCode,
                    Sort = s.Sort,
                    IsValid = 1,
                };
                Normalize(a);

                // 扫描结果必须自洽（受控值 + 定位搭配）；不自洽的项跳过，⛔ 不让整批失败
                var err = Validate(a);
                if (err != null)
                {
                    _logger.LogWarning("[DocTemplateAnchor] 扫描结果不合规，已跳过：{Ref} —— {Err}", s.AnchorRef, err);
                    continue;
                }

                seenKeys.Add(UniqueKeyOf(a));

                var existing = await FindByUniqueKeyAsync(a);
                if (existing == null)
                {
                    a.Code = Guid.NewGuid().ToString("N");
                    a.CreateTime = now;
                    a.UpdateTime = now;
                    var ins = await _db.InsertAsync(a);
                    if (!ins.Success) throw new InvalidOperationException($"锚点插入失败（{s.AnchorRef}）：{ins.Error}");
                    inserted++;
                }
                else
                {
                    // ★ 只动「扫描能确定的列」，⛔ 不碰人工配置列
                    existing.IsDeleted = false;
                    existing.DeleteBy = null;
                    existing.DeleteTime = null;
                    existing.IsOrphan = false;      // 重新出现 ⇒ 不再是孤儿
                    existing.AnchorType = a.AnchorType;
                    existing.AnchorRef = a.AnchorRef;
                    existing.FieldCode = a.FieldCode;
                    existing.DomainKind = a.DomainKind;
                    existing.Sort = a.Sort;
                    existing.IsValid = 1;
                    existing.UpdateTime = now;

                    var upd = await _db.UpdateAsync(existing,
                        nameof(DocTemplateAnchor.IsDeleted), nameof(DocTemplateAnchor.DeleteBy),
                        nameof(DocTemplateAnchor.DeleteTime), nameof(DocTemplateAnchor.IsOrphan),
                        nameof(DocTemplateAnchor.AnchorType), nameof(DocTemplateAnchor.AnchorRef),
                        nameof(DocTemplateAnchor.FieldCode), nameof(DocTemplateAnchor.DomainKind),
                        nameof(DocTemplateAnchor.Sort), nameof(DocTemplateAnchor.IsValid),
                        nameof(DocTemplateAnchor.UpdateTime));
                    if (!upd.Success) throw new InvalidOperationException($"锚点更新失败（{s.AnchorRef}）：{upd.Error}");
                    updated++;
                }
            }

            // ★ 本次没扫到的存活锚点 ⇒ 标孤儿（⛔ 不删：可能已有填过的值，21 号 §4.3）
            var live = await _db.Client.Queryable<DocTemplateAnchor>()
                .Where(x => x.TemplateCode == templateCode && x.IsDeleted == false && x.IsOrphan == false)
                .ToListAsync();

            var orphaned = 0;
            foreach (var o in live)
            {
                if (seenKeys.Contains(UniqueKeyOf(o))) continue;
                o.IsOrphan = true;
                o.UpdateTime = now;
                var upd = await _db.UpdateAsync(o,
                    nameof(DocTemplateAnchor.IsOrphan), nameof(DocTemplateAnchor.UpdateTime));
                if (upd.Success) orphaned++;
                else _logger.LogWarning("[DocTemplateAnchor] 标孤儿失败：{Code} —— {Err}", o.Code, upd.Error);
            }

            tx.Commit();
            return (inserted, updated, orphaned);
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    /// <summary>截断到指定长度（失败原因写库前收敛，避免超列长）</summary>
    private static string Truncate(string? text, int max)
    {
        var t = text ?? string.Empty;
        return t.Length <= max ? t : t[..max];
    }

    /// <summary>
    /// <b>三色校验的判定逻辑</b>（<see cref="ValidateTemplate"/> 与 <see cref="Publish"/> 共用）。
    /// <para>抽成一处是刻意的：发布前<b>必须重新校验</b>，若两处各写一份判定，
    /// 「校验通过但发布被拒」或反之的静默不一致迟早出现。</para>
    /// </summary>
    private async Task<List<ViolationItem>> BuildViolationsAsync(string templateCode)
    {
        var anchors = await _db.Client.Queryable<DocTemplateAnchor>()
            .Where(a => a.TemplateCode == templateCode && a.IsDeleted == false && a.IsValid == 1)
            .ToListAsync();

        var list = new List<ViolationItem>();

        foreach (var a in anchors)
        {
            var hasSource = !string.IsNullOrWhiteSpace(a.SourceSpec);

            // 🔴 domain(auto)：PAGE / NUMPAGES 交给 Word 算，配来源是逻辑错误
            if (a.AnchorType == "domain" && string.Equals(a.DomainKind, "auto", StringComparison.Ordinal) && hasSource)
                list.Add(new ViolationItem("E1", "error", a.AnchorRef,
                    "域自动值（如 @PAGE）由 Word 计算，不能配置取值来源"));

            // 🔴 table_total：合计必须能算，人工填合计 = 必然算错
            if (a.AnchorType == "table_total" && a.SourceSpec != null
                && a.SourceSpec.Contains("\"manual\"", StringComparison.OrdinalIgnoreCase))
                list.Add(new ViolationItem("E2", "error", a.AnchorRef,
                    "表格合计必须由系统计算，不能走人工录入"));

            // 🔴 scalar + remove：行内标量删除会破坏排版
            if (a.AnchorType == "scalar" && string.Equals(a.WriteMode, "remove", StringComparison.Ordinal))
                list.Add(new ViolationItem("E3", "error", a.AnchorRef,
                    "行内标量删除会破坏排版，应改用 block 锚点"));

            // 🟡 未配取值来源（domain 类不需要来源）
            if (a.AnchorType != "domain" && !hasSource)
                list.Add(new ViolationItem("W1", "warning", a.AnchorRef, "尚未配置取值来源"));

            // 🟡 孤儿锚点：最近一次扫描已消失，但保留其已填值
            if (a.IsOrphan)
                list.Add(new ViolationItem("W2", "warning", a.AnchorRef,
                    "该锚点在最近一次扫描中已消失（保留其已填值，不参与本轮）"));
        }

        return list;
    }

    /// <summary>一条校验结果（三色）</summary>
    public sealed record ViolationItem(string Code, string Level, string Anchor, string Message);

    /// <summary>批量保存请求</summary>
    public sealed class SaveAnchorBatchRequest
    {
        /// <summary>所属模板 Code（<c>cert_doc_template.Code</c>）</summary>
        public string TemplateCode { get; set; } = string.Empty;

        /// <summary>锚点清单（每条按唯一键 upsert）</summary>
        public List<DocTemplateAnchor> Items { get; set; } = new();
    }
}

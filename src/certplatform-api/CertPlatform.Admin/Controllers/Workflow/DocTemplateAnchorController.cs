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
using CertPlatform.Shared.Office;
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
    /// <para>⛔ <b>刻意不含 <c>IsLocked</c></b>（2026-10-05）：锁定与配置是<b>两个正交动作</b> ——
    /// 保存配置<b>不该</b>顺手改锁定状态。若列在这里，前端「保存锚点」时漏传 <c>isLocked</c>
    /// 会被写成 <c>false</c>，实施人员辛苦确认过的锁定被<b>静默解除</b>（且日志显示成功）。
    /// 锁定只走 <see cref="Lock"/> 端点。</para>
    /// <para>⚠️ 本清单含 <c>IsValid</c> / <c>IsDeleted</c> / <c>DeleteBy</c> / <c>DeleteTime</c> 四个
    /// <b>接口列</b>（复活分支需要写它们）。代价是：前端提交的行若漏传 <c>isValid</c>，
    /// 反序列化后是 <c>0</c> ⇒ 会把锚点<b>静默置为无效</b>（此后 <c>Validate</c> / <c>liveAnchors</c>
    /// 都查不到它）。⇒ 落库前必须显式归一（见 <see cref="NormalizeInterfaceColumns"/>）。</para>
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

    /// <summary>
    /// ★ <b>非配置列</b> —— 判定「锁定行是否真的被改了配置」时<b>排除</b>这些列
    /// （它们不是实施人员配的规则，而是接口列 / 扫描产物 / 派生值）。
    /// <para>⛔ 为什么必须排除而不是「锁定行一律拒绝」：本页「保存」是<b>整批提交</b>（含锁定行），
    /// 若锁定行内容一个字没改也拒绝，实施人员会被无意义地挡住 —— 那是假闸门。
    /// ⇒ 只拦「真的改了配置」的（见 <see cref="ConfigDiff"/>）。</para>
    /// <list type="bullet">
    /// <item><c>IsValid</c> / <c>IsDeleted</c> / <c>DeleteBy</c> / <c>DeleteTime</c> / <c>UpdateTime</c> —— 接口/审计列</item>
    /// <item><c>IsOrphan</c> —— 扫描产物（由 <c>Scan</c> 管理，人工不直接配）</item>
    /// <item><c>SourceSummary</c> —— 由 <c>SourceSpec</c> 派生（列表展示用）</item>
    /// </list>
    /// </summary>
    private static readonly HashSet<string> NonConfigColumns = new(StringComparer.Ordinal)
    {
        nameof(DocTemplateAnchor.IsValid),
        nameof(DocTemplateAnchor.IsDeleted),
        nameof(DocTemplateAnchor.DeleteBy),
        nameof(DocTemplateAnchor.DeleteTime),
        nameof(DocTemplateAnchor.UpdateTime),
        nameof(DocTemplateAnchor.IsOrphan),
        nameof(DocTemplateAnchor.SourceSummary),
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

    /// <summary>
    /// 修改：规范化 + 接口列归一 + 校验 + 唯一键查重（排除自身）+ <b>锁定拦截</b>。
    /// <para>⚠️ 本端点（基类 CRUD 路径）与 <see cref="SaveBatch"/>（页面主路径）用<b>同一套</b>
    /// 锁定判定（<see cref="ConfigDiff"/>）—— ⛔ 不各写一份，否则会出现「这条路拦得住、那条拦不住」。</para>
    /// </summary>
    public override async Task<Result<DocTemplateAnchor>> UpdateCore(DocTemplateAnchor entity)
    {
        if (string.IsNullOrWhiteSpace(entity.Code))
            return Result<DocTemplateAnchor>.Fail("更新失败：缺少业务键 Code");

        Normalize(entity);
        // ★ 接口列归一：⛔ 不允许一次「更新」把锚点静默置为无效（前端漏传 isValid ⇒ 0）
        NormalizeInterfaceColumns(entity);
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

        // ★ 锁定拦截（用户 2026-10-05 裁定「锁定的问题，就是不能再修改配置」）：
        //   命中自身且已锁定 ⇒ 配置列有实质变化就拒绝；无变化放行（避免「只改了个没变的字段」也被拒）。
        if (dup != null && string.Equals(dup.Code, entity.Code, StringComparison.Ordinal) && dup.IsLocked)
        {
            var diff = ConfigDiff(entity, dup);
            if (diff.Count > 0)
                return Result<DocTemplateAnchor>.Fail(
                    $"该锚点已锁定，不能修改其配置（{string.Join(" / ", diff)}）。如需修改请先解锁。");
        }

        return await base.UpdateCore(entity);
    }

    // ════════════════════════════════════════════════════════════════════
    // 三、自定义端点
    // ════════════════════════════════════════════════════════════════════

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
            // ★ B6：落库**之前**先拍一张「锁定锚点」快照。
            //   ⚠️ 必须**含已软删**：换版时 `DocTemplateController.InvalidateAnchorsAsync` 已把旧锚点
            //      全部软删，只查存活行会一行都查不到 ⇒ 恒返回「全部保留」的假结论。
            var lockedBefore = await _db.Client.Queryable<DocTemplateAnchor>()
                .Where(a => a.TemplateCode == templateCode && a.IsLocked)
                .Select(a => new { a.AnchorRef, a.AnchorType, a.AnchorKind, a.SheetName, a.SectionIndex, a.HeaderKind })
                .ToListAsync();

            var (inserted, updated, orphaned, persisted) = await PersistScanAsync(templateCode, scanned);

            // ★ B6：如实回报锁定锚点的去向 —— 「配置还在」与「配置丢了」必须能看出来。
            //   保留：新模板里仍有同唯一键锚点 ⇒ 软删 + 复活 ⇒ 配置自动保留（无需额外代码）。
            //   丢失：新模板里已没有该锚点 ⇒ 真的退出，⛔ 不做模糊匹配（见 AnchorScanMerge 类注释）。
            var lockedSummary = AnchorScanMerge.Summarize(
                lockedBefore.Select(a => (
                    AnchorScanMerge.AnchorKey.Of(a.AnchorType, a.AnchorKind, a.SheetName,
                        a.SectionIndex, a.HeaderKind, a.AnchorRef),
                    a.AnchorRef,
                    true)),
                persisted);

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
                "[DocTemplateAnchor] 扫描完成 Template={Tpl}：新增 {Ins} / 更新 {Upd} / 孤儿 {Orp} / 共 {Total}"
                + " / 锁定 {Locked}（保留 {Carried} / 丢失 {Lost}）",
                templateCode, inserted, updated, orphaned, scanned.Count,
                lockedSummary.Total, lockedSummary.Carried, lockedSummary.Lost);

            // ★ 消息里显式点名「锁定的锚点丢了」—— 这是唯一无法自动挽回的情况，
            //   必须让人在扫描后的第一眼就看到，而不是等发布前才发现规则不见了。
            var msg = $"扫描完成：识别到 {scanned.Count} 个锚点（新增 {inserted} / 更新 {updated} / 标记孤儿 {orphaned}）";
            if (lockedSummary.Lost > 0)
                msg += $"；⚠️ 有 {lockedSummary.Lost} 个已锁定的锚点在新模板中已不存在（{string.Join("、", lockedSummary.LostRefs)}），其配置无法保留";

            return Ok(ApiResponse<object>.Ok(new
            {
                TemplateCode = templateCode,
                Total = scanned.Count,
                Inserted = inserted,
                Updated = updated,
                Orphaned = orphaned,
                ScannedAt = tpl.ScanTime,
                // ★ B6：锁定锚点去向（保留 / 丢失）
                Locked = new
                {
                    Total = lockedSummary.Total,
                    Carried = lockedSummary.Carried,
                    Lost = lockedSummary.Lost,
                    LostRefs = lockedSummary.LostRefs,
                },
            }, msg));
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
    /// <b>批量保存</b>：按唯一键 upsert（命中 ⇒ 列级更新并复活；未命中 ⇒ 插入）。
    ///
    /// <para><b>★ 语义 = 整行 upsert（不是部分更新）</b>：每条 <c>items[i]</c> 视为该锚点的<b>完整状态</b>，
    /// 未在 JSON 里出现的字段会被写成其 CLR 默认值（<c>""</c> / <c>0</c> / <c>false</c>）。
    /// 实测踩过：先存 <c>required=true</c>，再发一条只带 <c>defaultText</c> 的「同键」条目，
    /// <c>required</c> 被静默改回 <c>false</c>。⇒ <b>前端编辑器必须提交完整行</b>。</para>
    ///
    /// <para>整个批次在<b>单事务</b>内完成 —— 任一条失败即整体回滚，避免「半批锚点生效」。</para>
    /// <para>⛔ 不删除本批次未出现的锚点：删除走 <c>delete</c> 或 <c>clear</c>，显式且可审计。</para>
    ///
    /// <para><b>★ 锁定拦截</b>（2026-10-05 用户裁定「锁定的问题，就是不能再修改配置」）：
    /// 提交项命中<b>已锁定</b>的锚点且配置列有<b>实质变化</b>时<b>整批拒绝</b>，
    /// 并点名「哪条锚点 / 哪些列」。锁定行内容没变的照常放行 —— 否则整批保存会被假闸门挡住
    /// （本页保存是整批提交，锁定行一个字没改也被拒 ⇒ 实施人员连其他行都存不了）。判定见 <see cref="ConfigDiff"/>。</para>
    ///
    /// <para><b>★ 接口列归一</b>：落库前强制「存活 + 有效」（见 <see cref="NormalizeInterfaceColumns"/>），
    /// ⛔ 不允许一次「保存配置」把锚点静默置为无效。</para>
    /// </summary>
    /// <param name="req">模板 Code + 锚点清单（每条按唯一键 upsert）</param>
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
            // ★ 接口列归一：⛔ 不允许「保存配置」把锚点静默置为无效（前端漏传 isValid ⇒ 0）
            NormalizeInterfaceColumns(item);

            var err = Validate(item);
            if (err != null)
                return Ok(ApiResponse<object>.Fail($"第 {i + 1} 条锚点校验失败：{err}"));

            if (!seen.Add(UniqueKeyOf(item)))
                return Ok(ApiResponse<object>.Fail(
                    $"第 {i + 1} 条锚点与前面的条目重复（类型/定位方式/引用 完全相同）：{item.AnchorRef}"));
        }

        // ★ 锁定拦截（用户 2026-10-05 裁定「锁定的问题，就是不能再修改配置」）。
        //   放在**开事务之前** —— 只读预检，失败时不留任何痕迹，也不会「半批生效」。
        //   ⚠️ 只拦「配置列有实质变化」的：整批提交里锁定行内容没变是常态，
        //      一律拒绝会变成假闸门（实施人员连其他行都存不了）。
        var lockedConflicts = new List<string>();
        foreach (var item in req.Items)
        {
            var locked = await FindByUniqueKeyAsync(item);
            if (locked == null || !locked.IsLocked) continue;

            var diff = ConfigDiff(item, locked);
            if (diff.Count > 0)
                lockedConflicts.Add($"{item.AnchorRef}（{string.Join(" / ", diff)}）");
        }
        if (lockedConflicts.Count > 0)
        {
            _logger.LogInformation("[DocTemplateAnchor] save-batch 被锁定拦截：Template={Tpl}, 冲突 {N} 条",
                req.TemplateCode, lockedConflicts.Count);
            return Ok(ApiResponse<object>.Fail(
                $"以下锚点已锁定，不能修改其配置：{string.Join("；", lockedConflicts)}。如需修改请先解锁。"));
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
                    // ★ 接口列归一（含 IsValid=1）—— 见 NormalizeInterfaceColumns 注释
                    NormalizeInterfaceColumns(a);
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
                    // ★ 接口列归一（含 IsValid=1）—— 前端漏传 isValid 会被反序列化成 0，
                    //   不归一就会把锚点静默置为无效（此后校验与计数都看不见它，日志却显示成功）
                    NormalizeInterfaceColumns(a);
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

    /// <summary>
    /// <b>清空某模板的全部锚点</b>（软删，留痕）。显式调用 —— ⛔ 不在 save-batch 里隐式触发。
    ///
    /// <para><b>★ 跳过已锁定的锚点</b>（2026-10-05 用户裁定「锁定的问题，就是不能再修改配置」）：
    /// 清空 = 把配置整批删掉，与「锁定后不可改配置」直接冲突 ⇒ 锁定行一律不动，
    /// 并在返回体与消息里<b>如实报告跳过了几条</b>（⛔ 不静默少删 —— 那会让人以为已经清干净了）。
    /// 要连锁定行一起清，须先解锁。</para>
    /// </summary>
    [HttpPost("clear")]
    public async Task<IActionResult> Clear([FromQuery] string templateCode)
    {
        if (string.IsNullOrWhiteSpace(templateCode))
            return Ok(ApiResponse<object>.Fail("请指定所属模板（templateCode）"));

        var rows = await _db.Client.Queryable<DocTemplateAnchor>()
            .Where(a => a.TemplateCode == templateCode && a.IsDeleted == false)
            .Select(a => new { a.Code, a.IsLocked })
            .ToListAsync();

        var codes = rows.Where(r => !r.IsLocked).Select(r => r.Code).ToList();
        var skippedLocked = rows.Count - codes.Count;

        if (codes.Count == 0)
        {
            return Ok(ApiResponse<object>.Ok(new
            {
                TemplateCode = templateCode,
                Deleted = 0,
                SkippedLocked = skippedLocked,
            }, skippedLocked > 0
                ? $"该模板下 {skippedLocked} 条锚点均已锁定，未清空任何锚点（如需清空请先解锁）"
                : "该模板下没有锚点"));
        }

        var del = await Entity.DeleteBatch(codes, clientIp: UserContext.ClientIp);
        if (!del.Success)
            return Ok(ApiResponse<object>.Fail(del.Error ?? "清空锚点失败"));

        _logger.LogInformation("[DocTemplateAnchor] 清空模板 {Tpl} 的 {N} 条锚点（跳过已锁定 {Skip} 条）",
            templateCode, del.Data, skippedLocked);

        return Ok(ApiResponse<object>.Ok(new
        {
            TemplateCode = templateCode,
            Deleted = del.Data,
            SkippedLocked = skippedLocked,
        }, skippedLocked > 0
            ? $"已清空 {del.Data} 条锚点；{skippedLocked} 条已锁定，未清空"
            : $"已清空 {del.Data} 条锚点"));
    }

    /// <summary>
    /// <b>锁定 / 解锁锚点</b>（用户第 20 轮第 2 条 + 第 24 轮 Q1 裁决 + <b>2026-10-05 口径修订</b>）。
    ///
    /// <para><b>★ 语义（2026-10-05 修订）</b>：<c>IsLocked = 1</c> = 实施人员已认可该锚点的设置规则，
    /// <b>配置就此冻结 —— 不能再修改配置</b>（用户逐字：「锁定的问题，就是不能再修改配置」）。
    /// 落地表现：<see cref="SaveBatch"/> / <see cref="UpdateCore"/> 命中锁定行且配置列有<b>实质变化</b>时
    /// <b>拒绝</b>；<see cref="Clear"/> <b>跳过</b>锁定行。解锁即本端点传 <c>locked=false</c>。</para>
    ///
    /// <para><b>⛔ 此前口径已作废</b>：旧注释写「它<b>不是权限位、不阻断任何操作</b>」（依据 P2'
    /// 「程序不阻断，只如实推导」）。那条原则约束的是「<b>能不能这么配</b>」这类业务组合
    /// （见 49-V4 §四 C1~C6：没有模板也允许标可编辑，程序只如实推导状态），
    /// ⛔ <b>不适用于「锁定之后还能不能再改」</b> —— 锁定是实施人员的<b>显式冻结动作</b>，
    /// 冻结了就不能改，两者并不冲突。别再用 P2' 给「锁定可绕过」背书。</para>
    ///
    /// <para><b>★ 换版重扫仍自动保留配置</b>：新模板里仍有同唯一键锚点 ⇒ 软删 + 复活 ⇒ 配置保留；
    /// 已消失的由 <see cref="Scan"/> 列进 <c>Locked.LostRefs</c> 如实回报（见 <see cref="AnchorScanMerge"/>）。</para>
    ///
    /// <para><b>⛔ 为什么不记「谁锁的、什么时候锁的」</b>（Q1 用户逐字：「锚点是实施人员操作的，
    /// 他认可了这个设置规则 ok 了，就加上锚点了」）：<c>BaseEntity</c> 的 <c>UpdateBy</c> / <c>UpdateTime</c>
    /// 已记录人与时间，再开两列就是同一事实存两处。</para>
    ///
    /// <para><b>★ 与 <see cref="SaveBatch"/> 正交</b>：本端点<b>只改</b> <c>IsLocked</c>；
    /// <c>save-batch</c> 的列清单里<b>刻意不含</b> <c>IsLocked</c>（见
    /// <see cref="BatchUpdatableColumns"/>）⇒ 保存配置不会顺手解除锁定。</para>
    ///
    /// <para><b>★ 锁定动作本身不设前置</b>：锁定一个「还没配取值来源」的锚点是<b>允许</b>的 ——
    /// 程序只在 <c>Warnings</c> 里如实提示，⛔ 不拒绝。是否配齐由前端（C8 闸）与人工决定。
    /// （注意：这是「锁定动作」不设闸，与「锁定后不可改配置」是两件事。）</para>
    /// </summary>
    /// <param name="req">模板 Code + 目标锚点 Code 清单（或 <c>all=true</c> 整模板）+ 目标状态</param>
    [HttpPost("lock")]
    public async Task<IActionResult> Lock([FromBody] LockAnchorRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.TemplateCode))
            return Ok(ApiResponse<object>.Fail("请指定所属模板（templateCode）"));

        var tplErr = await EnsureTemplateAsync(req.TemplateCode);
        if (tplErr != null) return Ok(ApiResponse<object>.Fail(tplErr));

        // ★ 取「该模板下全部存活锚点」后在内存里过滤，⛔ 不在 SQL 里拼 IN ——
        //   ① 单模板锚点数很小（当前 5 行，现实规模 < 500）；
        //   ② 顺带拿到 `AnchorType` / `SourceSpec`，供下面的告警判定，不必再查一次。
        var all = await _db.Client.Queryable<DocTemplateAnchor>()
            .Where(a => a.TemplateCode == req.TemplateCode && a.IsDeleted == false)
            .ToListAsync();

        List<DocTemplateAnchor> targets;
        if (req.Codes.Count > 0)
        {
            var wanted = new HashSet<string>(
                req.Codes.Where(c => !string.IsNullOrWhiteSpace(c)), StringComparer.Ordinal);
            targets = all.Where(a => wanted.Contains(a.Code)).ToList();

            // 显式点名却一个都没命中 ⇒ 报错（可能传错模板，或锚点已被删除）
            if (targets.Count == 0)
                return Ok(ApiResponse<object>.Fail("未找到要操作的锚点（可能已删除，或不属于该模板）"));
        }
        else if (req.All)
        {
            targets = all;
        }
        else
        {
            return Ok(ApiResponse<object>.Fail("请指定要操作的锚点（codes），或传 all=true 作用于整个模板"));
        }

        if (targets.Count == 0)
            return Ok(ApiResponse<object>.Ok(new
            {
                TemplateCode = req.TemplateCode,
                Locked = req.Locked,
                Affected = 0,
                Unchanged = 0,
                Failed = 0,
                LockedTotal = 0,
                Warnings = Array.Empty<string>(),
            }, "该模板下还没有锚点"));

        var now = DateTime.Now;
        var affected = 0;
        var unchanged = 0;
        var failed = 0;

        foreach (var a in targets)
        {
            if (a.IsLocked == req.Locked) { unchanged++; continue; }

            a.IsLocked = req.Locked;
            a.UpdateTime = now;

            var upd = await _db.UpdateAsync(a,
                nameof(DocTemplateAnchor.IsLocked), nameof(DocTemplateAnchor.UpdateTime));
            if (upd.Success) affected++;
            else
            {
                failed++;
                _logger.LogWarning("[DocTemplateAnchor] 锁定状态写入失败：{Code} —— {Err}", a.Code, upd.Error);
            }
        }

        // ★ 如实推导、⛔ 不阻断：锁定「还没配取值来源」的锚点是允许的，但要让人知道
        var warnings = new List<string>();
        if (req.Locked)
        {
            var noSource = targets.Count(a =>
                a.AnchorType != "domain" && string.IsNullOrWhiteSpace(a.SourceSpec));
            if (noSource > 0)
                warnings.Add($"本次操作的锚点中有 {noSource} 个尚未配置取值来源，锁定后仍不能用于自动填充");

            var orphans = targets.Count(a => a.IsOrphan);
            if (orphans > 0)
                warnings.Add($"本次操作的锚点中有 {orphans} 个是孤儿（最近一次扫描已消失），锁定它们不会让它们回到列表");
        }

        _logger.LogInformation("[DocTemplateAnchor] {Act}锚点：Template={Tpl}, 生效 {Aff} / 未变 {Un} / 失败 {Fail}",
            req.Locked ? "锁定" : "解锁", req.TemplateCode, affected, unchanged, failed);

        var lockedTotal = targets.Count(a => a.IsLocked);

        var msg = (req.Locked ? "已锁定 " : "已解锁 ") + $"{affected} 个锚点"
                  + (unchanged > 0 ? $"（{unchanged} 个状态未变）" : string.Empty)
                  + (failed > 0 ? $"；{failed} 个写入失败" : string.Empty);

        return Ok(ApiResponse<object>.Ok(new
        {
            TemplateCode = req.TemplateCode,
            Locked = req.Locked,
            Affected = affected,
            Unchanged = unchanged,
            Failed = failed,
            // ★ 本次操作范围内「操作后」仍处于锁定态的条数 —— 前端据此刷新行状态，⛔ 不用自己算
            LockedTotal = lockedTotal,
            Warnings = warnings,
        }, msg));
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
                a.IsLocked,
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
            LockedCount = rows.Count(r => !r.IsDeleted && r.IsLocked),
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

    /// <summary>
    /// ★ <b>接口列归一</b>（落库前必调）：本端点用<b>整行</b>覆盖，而
    /// <see cref="BatchUpdatableColumns"/> 含 <c>IsValid</c> / <c>IsDeleted</c> / <c>DeleteBy</c> / <c>DeleteTime</c>。
    /// <para>⚠️ 前端提交的行若<b>漏传</b> <c>isValid</c>，反序列化后是 <c>0</c>
    /// ⇒ 该锚点被<b>静默置为无效</b>（此后 <see cref="BuildViolationsAsync"/> 与 <c>liveAnchors</c>
    /// 都看不见它，而日志显示「保存成功」）。</para>
    /// <para>⇒ 保存配置<b>不该</b>改变有效性 / 删除态：一律强制「存活 + 有效」。
    /// 删除只走 <c>delete</c> / <c>clear</c>（显式且可审计）。</para>
    /// </summary>
    private static void NormalizeInterfaceColumns(DocTemplateAnchor a)
    {
        a.IsDeleted = false;
        a.DeleteBy = null;
        a.DeleteTime = null;
        a.IsValid = 1;
    }

    /// <summary>
    /// ★ <b>锁定拦截判定</b>：返回「提交行与库中行在<b>配置列</b>上不一致」的列名清单（空 = 无实质变化）。
    ///
    /// <para><b>为什么用差异比对而不是「锁定行一律拒绝」</b>：本页「保存」是<b>整批提交</b>（含锁定行），
    /// 锁定行一个字没改也被拒 ⇒ 假闸门，实施人员无法保存其他行。</para>
    ///
    /// <para><b>★ 与落库共用同一份列清单</b>：反射遍历 <see cref="BatchUpdatableColumns"/> 并跳过
    /// <see cref="NonConfigColumns"/> —— ⛔ 不另抄一份「配置列」清单，两处清单各自漂移是
    /// 「有的列拦得住、有的列拦不住」这类静默漏拦的经典成因。</para>
    ///
    /// <para>⚠️ 调用前两边都必须已 <see cref="Normalize"/>：否则提交行里空白的
    /// <c>AnchorKind</c> / <c>WriteMode</c> / <c>ValueType</c> 会被误判成「改过了」。</para>
    /// </summary>
    private static List<string> ConfigDiff(DocTemplateAnchor submitted, DocTemplateAnchor existing)
    {
        var diff = new List<string>();

        foreach (var col in BatchUpdatableColumns)
        {
            if (NonConfigColumns.Contains(col)) continue;

            var prop = typeof(DocTemplateAnchor).GetProperty(col);
            if (prop == null) continue;

            if (!Equals(prop.GetValue(submitted), prop.GetValue(existing))) diff.Add(col);
        }

        return diff;
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
    /// <para><b>★ 返回值多一项 <c>persisted</c></b>（2026-10-05）：本次<b>实际落库</b>的锚点键集合。
    /// 供 <see cref="Scan"/> 判定「锁定锚点的配置保住了没有」（见 <see cref="AnchorScanMerge"/>）。
    /// ⛔ 不能用原始 <paramref name="scanned"/> 代替：扫描识别出但被合规校验拒掉的项<b>没进库</b>，
    /// 拿它们当「已保留」会给出假结论。</para>
    /// </summary>
    private async Task<(int inserted, int updated, int orphaned, List<AnchorScanMerge.AnchorKey> persisted)>
        PersistScanAsync(string templateCode, List<ScannedAnchor> scanned)
    {
        var now = DateTime.Now;
        var inserted = 0;
        var updated = 0;
        var persisted = new List<AnchorScanMerge.AnchorKey>();

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
                persisted.Add(AnchorScanMerge.AnchorKey.Of(
                    a.AnchorType, a.AnchorKind, a.SheetName, a.SectionIndex, a.HeaderKind, a.AnchorRef));

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
            return (inserted, updated, orphaned, persisted);
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

    /// <summary>锁定 / 解锁请求（<c>POST lock</c>）</summary>
    public sealed class LockAnchorRequest
    {
        /// <summary>所属模板 Code（<c>cert_doc_template.Code</c>，必填）</summary>
        public string TemplateCode { get; set; } = string.Empty;

        /// <summary>
        /// 目标锚点 Code 清单（单行切换就传 1 个）。
        /// <para>⛔ 不能跨模板：不在 <see cref="TemplateCode"/> 下的 Code 会被静默忽略 ——
        /// 这是刻意的（避免误传 Code 改到别的模板的锚点），未命中时会整体报错而不是部分生效。</para>
        /// </summary>
        public List<string> Codes { get; set; } = new();

        /// <summary>true = 作用于该模板下<b>全部</b>存活锚点（<see cref="Codes"/> 为空时才生效）</summary>
        public bool All { get; set; }

        /// <summary>目标状态：true = 锁定，false = 解锁</summary>
        public bool Locked { get; set; } = true;
    }
}

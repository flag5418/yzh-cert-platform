using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using YZH.Core.Api.Controllers;
using YZH.Core.Api.Services;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Helpers;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Config;
using YZH.Core.Stand.Models.Result;
using CertPlatform.Admin.Services.DocExtraction;
using CertPlatform.Admin.Services.StandardDirectory;
using CertPlatform.Shared.Constants;
using CertPlatform.Shared.Storage;

namespace CertPlatform.Admin.Controllers.Workflow;

/// <summary>
/// 标准文档模板控制器（「标准文档填写规则」页面的宿主对象）
///
/// <para><b>路由前缀</b>：<c>/api/Admin/Workflow/DocTemplate</c></para>
/// <para><b>数据库</b>：<c>cert_doc_template</c></para>
///
/// <para><b>业务定位</b>：一行 = 一份已登记进系统的空白模板。锚点规则
/// （<c>DocTemplateAnchor</c>）与全文提示词（<c>DocFillPrompt</c>）都挂在模板 Code 之下。</para>
///
/// <para><b>★ 首版不做扫描</b>（用户 2026-10-03 裁定）：「我们现在的文档不是我们真正已经设置好的空白模板，
/// 现在扫描分析当前的材料没有任何意义」。因此本控制器<b>只登记</b>：
/// 人工上传空白模板 → 登记 <c>StoragePath</c> → 手工录入锚点规则。
/// 扫描列（<see cref="DocTemplate.ScanStatus"/> 等）只读不写，等真实模板到位再启用。</para>
///
/// <para><b>★ 三条铁律</b>：</para>
/// <list type="number">
/// <item><b>身份段权威推导</b>：<c>OrgCode/StandardCode/StageCode</c> ⛔ 不信前端传值，
/// 一律经 <see cref="DocExtractionRuleService.ResolveRuleScopeAsync(string)"/>（提取规则同源唯一入口）。</item>
/// <item><b>文件类型权威推导</b>：由 <c>FileName</c> 扩展名推导，⛔ 不信前端传值。</item>
/// <item><b>含已删查重</b>：唯一键 <c>uk_standard_file</c> 不含 <c>IsDeleted</c> ⇒ 建前必须把已软删的行也查出来，
/// 命中则<b>就地复活</b>（⛔ 不能走 INSERT，Id 会撞主键）。</item>
/// </list>
/// </summary>
[ApiController]
[Route("api/Admin/Workflow/DocTemplate")]
public class DocTemplateController : YzhControllerBase<DocTemplate>
{
    private readonly IDbOrm _db;
    private readonly DocExtractionRuleService _extraction;
    private readonly EntityService<DocTemplateAnchor> _anchors;
    private readonly IObjectStorage _storage;
    private readonly StandardDirectoryService _directory;
    private readonly ILogger<DocTemplateController> _logger;

    public DocTemplateController(
        EntityService<DocTemplate> entityService,
        EntityService<DocTemplateAnchor> anchors,
        IUserContext userContext,
        IDbOrm db,
        DocExtractionRuleService extraction,
        IObjectStorage storage,
        StandardDirectoryService directory,
        ILogger<DocTemplateController> logger)
        : base(entityService, userContext)
    {
        _anchors = anchors;
        _db = db;
        _extraction = extraction;
        _storage = storage;
        _directory = directory;
        _logger = logger;
    }

    /// <summary>★ 开发期强制暴露缺配置问题（缺 JSON 直接 throw，不静默空白）</summary>
    protected override bool StrictConfigLoad => true;

    /// <summary>加载 EntityConfig 配置</summary>
    protected override EntityConfig LoadConfig()
    {
        return EntityConfigHelper.GetConfig<DocTemplate>();
    }

    // ════════════════════════════════════════════════════════════════════
    // 一、写入覆写（校验 + 身份段推导 + 含已删查重）
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 新增：先<b>校验 + 权威推导</b>，再按唯一键「含已删」查重。
    /// <para>命中<b>已软删</b>行 ⇒ 就地复活（复用同 Code，锚点/日志的外键不失效）；
    /// 命中<b>存活</b>行 ⇒ 拒绝（提示改用编辑）。</para>
    /// <para>⛔ 不走 <c>OnBeforeAdd</c>：钩子只能「取消」，无法把 INSERT 改写成 UPDATE。</para>
    /// </summary>
    public override async Task<Result<DocTemplate>> AddCore(DocTemplate entity)
    {
        var (ok, msg) = await PrepareAsync(entity);
        if (!ok) return Result<DocTemplate>.Fail(msg ?? "校验失败");

        // ★ uk_standard_file 不含 IsDeleted ⇒ 必须把已删行也查出来
        var existing = await FindAnyByStandardFileAsync(entity.StandardFileCode);
        if (existing != null)
        {
            if (existing.IsDeleted)
                return await ReviveAsync(existing, entity);

            return Result<DocTemplate>.Fail(
                $"该标准文件已登记模板（{existing.FileName}）。请直接编辑该模板，或先删除它。");
        }

        return await base.AddCore(entity);
    }

    /// <summary>修改：重新权威推导身份段（模板可能被改挂到另一个标准文件）</summary>
    public override async Task<Result<DocTemplate>> UpdateCore(DocTemplate entity)
    {
        if (string.IsNullOrWhiteSpace(entity.Code))
            return Result<DocTemplate>.Fail("更新失败：缺少业务键 Code");

        var (ok, msg) = await PrepareAsync(entity);
        if (!ok) return Result<DocTemplate>.Fail(msg ?? "校验失败");

        // 换了宿主标准文件 ⇒ 不能与别的模板撞（含已删行）
        var dup = await FindAnyByStandardFileAsync(entity.StandardFileCode);
        if (dup != null && !string.Equals(dup.Code, entity.Code, StringComparison.Ordinal))
        {
            return Result<DocTemplate>.Fail(
                dup.IsDeleted
                    ? $"目标标准文件下存在一条<b>已删除</b>的模板（{dup.FileName}），Code={dup.Code}。请先把它复活或改挂，避免主键冲突。"
                    : $"该标准文件已被模板（{dup.FileName}）占用。");
        }

        return await base.UpdateCore(entity);
    }

    /// <summary>
    /// 删除：软删模板后<b>级联软删</b>其锚点规则（模板没了 ⇒ 锚点不可达）。
    /// <para><b>★ 复活不回滚锚点（有意为之，实测确认）</b>：重新登记同一标准文件时，
    /// 模板行按唯一键就地复活（Code 不变），但其锚点<b>保持已删</b> —— 因为重登记后锚点集
    /// 可能已变，静默恢复旧规则比「让用户重新确认一遍」更危险。
    /// 用户在页面重新 <c>save-batch</c> 时，锚点会按 <c>uk_tpl_anchor</c> 被<b>逐条复活</b>，
    /// 所以「Code 不变、数据不丢」，只是需要一次显式重提。</para>
    /// </summary>
    public override async Task<Result<int>> DeleteCore(params string[] codes)
    {
        var result = await base.DeleteCore(codes);
        if (!result.Success) return result;

        // 级联：锚点一并软删（⛔ 不硬删，留痕；也避免重新登记时被旧锚点污染）
        var anchorCodes = await _db.Client.Queryable<DocTemplateAnchor>()
            .Where(a => codes.Contains(a.TemplateCode) && a.IsDeleted == false)
            .Select(a => a.Code)
            .ToListAsync();

        if (anchorCodes.Count > 0)
        {
            var cascade = await _anchors.DeleteBatch(anchorCodes, clientIp: UserContext.ClientIp);
            if (!cascade.Success)
            {
                // 不阻塞主删除（模板已删），但必须留日志 —— 否则锚点会静默残留
                _logger.LogWarning("[DocTemplate] 模板 {Codes} 已删，但其 {N} 条锚点级联软删失败：{Err}",
                    string.Join(",", codes), anchorCodes.Count, cascade.Error);
            }
        }

        return result;
    }

    // ════════════════════════════════════════════════════════════════════
    // 二、自定义端点
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>可登记的宿主标准文件</b>：<c>cert_standard_directory_file</c> 中 <c>EnterpriseCode = YZH-STD-ENT</c>
    /// 的<b>模板文件定义行</b>，标记哪些已登记模板。
    ///
    /// <para><b>★ 为什么还要返回 <c>Editable*</c> 四列（实测教训）</b>：库里 168 个模板行中
    /// <b>143 个是 <c>.doc</c>、11 个是 <c>.xls</c></b>，而填写引擎只认 <c>.docx</c> / <c>.xlsx</c>。
    /// 能直接登记的只有 14 行（本来就是 docx/xlsx/txt）；其余 154 行必须走
    /// <c>EditableStoragePath</c>（归一产物）。<c>Ready</c> = 有可用的 docx/xlsx 字节。</para>
    /// </summary>
    [HttpGet("candidates")]
    public async Task<IActionResult> Candidates([FromQuery] string? keyword)
    {
        var rows = await _db.Client.Queryable<StandardDirectoryFile>()
            .Where(f => f.IsDeleted == false && f.IsValid == 1
                        && f.EnterpriseCode == YzhVirtualEnterprise.Code)
            .OrderBy(f => f.SortOrder)
            .Select(f => new
            {
                f.Code,
                f.FileName,
                f.ConfigCode,
                f.StandardCode,
                f.StageCode,
                f.FileType,
                f.StoragePath,
                f.EditableStoragePath,
                f.EditableStatus,
            })
            .ToListAsync();

        // 已登记 = 存活且有效的模板行（软删/禁用的不算已登记，可重新登记并复活）
        var registered = (await _db.GetListAsync<DocTemplate>()).Data
                             ?.Select(t => t.StandardFileCode)
                             .Where(c => !string.IsNullOrEmpty(c))
                             .ToHashSet(StringComparer.Ordinal)
                         ?? new HashSet<string>(StringComparer.Ordinal);

        var kw = keyword?.Trim() ?? string.Empty;
        var items = rows
            .Where(r => kw.Length == 0
                        || (r.FileName?.Contains(kw, StringComparison.OrdinalIgnoreCase) ?? false))
            .Select(r =>
            {
                // 可用字节：归一产物优先（它才是 docx/xlsx），否则回退原始路径
                var effectivePath = string.IsNullOrWhiteSpace(r.EditableStoragePath)
                    ? r.StoragePath
                    : r.EditableStoragePath;
                var kind = ResolveFileKind(effectivePath);

                return new
                {
                    r.Code,
                    r.FileName,
                    r.ConfigCode,
                    r.StandardCode,
                    r.StageCode,
                    r.FileType,
                    r.EditableStatus,
                    // ★ 登记时应写入 DocTemplate.StoragePath 的路径
                    EffectiveStoragePath = effectivePath,
                    // ★ 登记后的 FileKind（null = 尚不可登记，需先归一）
                    RegisterKind = kind,
                    Ready = kind != null,
                    Registered = registered.Contains(r.Code),
                };
            })
            .ToList();

        return Ok(ApiResponse<object>.Ok(new
        {
            // Total = 关键字过滤后的条数；AllCount = 全部模板行（168 实测）
            Total = items.Count,
            AllCount = rows.Count,
            ReadyCount = items.Count(x => x.Ready),
            RegisteredCount = items.Count(x => x.Registered),
            Items = items,
        }));
    }

    /// <summary>
    /// <b>从标准目录文件登记模板</b>（首版主路径 —— 人工挑一份已存在的空白模板，不做扫描）。
    ///
    /// <para><b>为什么需要这个端点</b>：前端若自己拼 <c>StoragePath</c>，就得知道「归一产物优先」
    /// 这条规则 —— 那是<b>会漂移的第二套口径</b>。此处把「选哪条路径」收敛到后端一处。</para>
    ///
    /// <para><b>★ 与 DDL 注释的偏差（有意为之，实测驱动）</b>：DDL 说 <c>StoragePath</c> 指向
    /// <c>PathBuilder.TemplateFile()</c> 的 <c>_template/</c> 段。但库里 154/168 的模板只有
    /// 「标准目录下的归一产物」这一份可用字节，没有独立上传的模板文件。首版<b>直接引用归一产物</b>，
    /// 等真实需要「模板与标准文档分离」时再补上传端点。</para>
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.StandardFileCode))
            return Ok(ApiResponse<object>.Fail("请选择宿主标准文件（standardFileCode）"));

        var fileResult = await _db.GetOneAsync<StandardDirectoryFile>(f => f.Code == req.StandardFileCode);
        var file = fileResult.Data;
        if (file == null)
            return Ok(ApiResponse<object>.Fail("标准目录文件不存在或已删除"));

        if (file.EnterpriseCode != YzhVirtualEnterprise.Code)
            return Ok(ApiResponse<object>.Fail(
                "该行不是模板文件定义行（EnterpriseCode 不是 YZH-STD-ENT），不能作为填写模板。"));

        // ★ 可用字节：归一产物优先
        var storagePath = !string.IsNullOrWhiteSpace(file.EditableStoragePath)
            ? file.EditableStoragePath!
            : file.StoragePath;

        if (string.IsNullOrWhiteSpace(storagePath))
            return Ok(ApiResponse<object>.Fail("该文件既无归一产物、也无原始路径，无法登记为模板"));

        var kind = ResolveFileKind(storagePath);
        if (kind == null)
        {
            return Ok(ApiResponse<object>.Fail(
                $"该文件的可用字节不是 .docx / .xlsx（{storagePath}），无法作为填写模板。"
                + $"请先在「标准目录」里完成归一（当前 EditableStatus={file.EditableStatus ?? "空"}）。"));
        }

        var entity = new DocTemplate
        {
            StandardFileCode = file.Code,
            FileName = string.IsNullOrWhiteSpace(req.FileName) ? file.FileName : req.FileName!,
            StoragePath = storagePath,
            FileKind = kind,
            FillPromptCode = req.FillPromptCode,
            Remark = req.Remark,
            IsValid = 1,
        };

        var result = await AddCore(entity);
        return Ok(result.Success
            ? ApiResponse<object>.Ok(new
            {
                result.Data!.Code,
                result.Data.StandardFileCode,
                result.Data.OrgCode,
                result.Data.StandardCode,
                result.Data.StageCode,
                result.Data.FileName,
                result.Data.FileKind,
                result.Data.StoragePath,
                result.Data.PublishStatus,
            }, "模板登记成功")
            : ApiResponse<object>.Fail(result.Error ?? "登记失败"));
    }

    /// <summary>
    /// <b>上传空白模板</b>（37 号 §7.1 第 ④ 步 —— 本页最关键的人工入口）。
    ///
    /// <para><b>为什么需要它</b>：标准目录里的源文件是 <c>.doc</c> / <c>.xls</c>（实测 168 份中 143 + 11），
    /// 且<b>没有锚点</b>。填写引擎只认 <c>.docx</c> / <c>.xlsx</c>，且必须人工在 Word/Excel 里加
    /// <c>{{标签}}</c> / 书签 / <c>YZH_Mark</c>。所以链路是：<b>下载源文件 → 本地加工 → 上传空白模板</b>。</para>
    ///
    /// <para><b>落点</b>：<c>PathBuilder.TemplateFile()</c> 指向的 <c>_template/</c> 段（⛔ 不手工拼段名）。</para>
    ///
    /// <para><b>★ 覆盖语义</b>：重新上传 = <b>换版</b>。已存在的模板行<b>沿用同 Code</b> 就地更新
    /// （锚点外键不失效），并把 <c>PublishStatus</c> 重置为 <c>draft</c>、<c>ScanStatus</c> 重置为 <c>pending</c>
    /// —— 字节变了，旧锚点与旧校验结果全部作废，必须重扫重校验。</para>
    ///
    /// <para><b>⛔ 不做归档</b>：首版直接覆盖。归档（<c>_template/_archive/xxx.docx.v1</c>）留给 S6 的
    /// 「换版状态机」一起做 —— 现在没有版本号来源，硬造一个会引入第二套口径。</para>
    /// </summary>
    [HttpPost("upload-template")]
    public async Task<IActionResult> UploadTemplate([FromForm] UploadTemplateDto dto)
    {
        if (dto?.File == null || dto.File.Length == 0)
            return Ok(ApiResponse<object>.Fail("请选择要上传的空白模板文件"));

        if (string.IsNullOrWhiteSpace(dto.StandardFileCode))
            return Ok(ApiResponse<object>.Fail("请先选择宿主标准文件（standardFileCode）"));

        // ★ 类型权威推导（⛔ 不信前端）：填写引擎只认 docx/xlsx
        var kind = ResolveFileKind(dto.File.FileName);
        if (kind == null)
            return Ok(ApiResponse<object>.Fail(
                "空白模板只支持 .docx / .xlsx。请先在本地把 .doc / .xls 另存为对应格式，并加上 {{标签}} / 书签 / YZH_Mark 标记。"));

        var fileRow = (await _db.GetOneAsync<StandardDirectoryFile>(f => f.Code == dto.StandardFileCode)).Data;
        if (fileRow == null)
            return Ok(ApiResponse<object>.Fail("标准目录文件不存在或已删除"));

        if (fileRow.EnterpriseCode != YzhVirtualEnterprise.Code)
            return Ok(ApiResponse<object>.Fail(
                "该行不是模板文件定义行（EnterpriseCode 不是 YZH-STD-ENT），不能上传空白模板。"));

        // ★ 身份段经唯一作用域入口推导（⛔ 不在此另写一份 —— 两套口径必然漂移）
        var scope = await _extraction.ResolveRuleScopeAsync(fileRow.Code);
        if (string.IsNullOrWhiteSpace(scope.OrgCode))
            return Ok(ApiResponse<object>.Fail(
                "无法确定模板所属机构：标准目录文件缺少 ConfigCode，或其所属配置缺少 OrgCode。请先在「标准目录」里补全。"));

        // ★ 路径复用 PathBuilder（⛔ 不手工插 _template 段）
        var folderPath = ResolveFolderPath(fileRow);
        var fileName = Path.GetFileName(dto.File.FileName);
        string templatePath;
        try
        {
            templatePath = PathBuilder.TemplateFile(
                scope.OrgCode, scope.StandardCode, scope.StageCode, folderPath, fileName);
        }
        catch (Exception ex)
        {
            // PathBuilder 身份段为空会抛异常 —— ⛔ 不吞，转成人话返回（陷阱 R6）
            return Ok(ApiResponse<object>.Fail($"模板路径生成失败：{ex.Message}"));
        }

        // 读入内存：① 算指纹 ② 上传（同一份字节，避免流被消费两次）
        byte[] bytes;
        await using (var ms = new MemoryStream())
        {
            await dto.File.CopyToAsync(ms);
            bytes = ms.ToArray();
        }

        var sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        try
        {
            await using var uploadStream = new MemoryStream(bytes);
            await _storage.UploadAsync(
                templatePath.TrimStart('/'), uploadStream, bytes.Length, "application/octet-stream");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DocTemplate] 上传空白模板失败：Path={Path}", templatePath);
            return Ok(ApiResponse<object>.Fail($"上传失败：{ex.Message}"));
        }

        // ★★ 换版必须失效预览缓存（2026-10-04）——
        //   预览 PDF 的缓存路径由**源路径**派生（`…/_template/x.docx` → `…/_template/pdf/x.docx.pdf`），
        //   而「重新上传 = 换版」时同名文件的缓存路径**完全相同** ⇒ 不删的话中栏会继续显示
        //   **上一版**的 PDF。用户刚上传完期待看到新版，却看到旧版 —— 这是「静默显示错内容」，
        //   比直接报错危险得多（用户会以为上传没生效，然后反复重传）。
        //   两个路径都失效：新路径（同名覆盖）+ 旧路径（改名换版时旧缓存会变孤儿）。
        var prevPath = (await FindAnyByStandardFileAsync(dto.StandardFileCode))?.StoragePath;
        await InvalidatePreviewCacheAsync(templatePath);
        if (!string.IsNullOrEmpty(prevPath) && !string.Equals(prevPath, templatePath, StringComparison.Ordinal))
            await InvalidatePreviewCacheAsync(prevPath);

        var result = await UpsertTemplateAsync(fileRow, templatePath, kind, fileName, sha256, dto.Remark);
        if (!result.Success)
            return Ok(ApiResponse<object>.Fail(result.Error ?? "模板登记失败"));

        var t = result.Data!;
        _logger.LogInformation(
            "[DocTemplate] 空白模板已上传：Template={Code}, Path={Path}, Sha256={Sha}, Kind={Kind}",
            t.Code, templatePath, sha256, kind);

        return Ok(ApiResponse<object>.Ok(new
        {
            t.Code,
            t.StandardFileCode,
            t.OrgCode,
            t.StandardCode,
            t.StageCode,
            t.FileName,
            t.FileKind,
            t.StoragePath,
            t.SourceSha256,
            t.PublishStatus,
            t.ScanStatus,
            // ★ 前端据此自动触发一次扫描（上传后锚点必然是空的）
            NeedScan = true,
        }, "空白模板上传成功，请点击「重新扫描」生成锚点"));
    }

    /// <summary>
    /// ★★ <b>左树（首选）</b>：<b>标准资料清单树</b> + 空白模板状态，五级展开
    /// 「机构 → 标准 → 阶段 → 文件夹 → 文件」。
    ///
    /// <para><b>为什么是资料清单而不是模板表</b>（2026-10-04 用户裁定）：
    /// 「我们所有的文档来源首先得有标准文档资料清单，我们是根据这个资料清单的文件，
    /// 下载后进行空白文档设置，上传后，再定义规则的，<b>而不是想当然的空白的</b>」。</para>
    ///
    /// <para>按模板表构树时，<b>还没上传模板的文件在页面上根本不存在</b> ⇒ 用户无从下手。
    /// 实测：模板表 1 行 vs 资料清单 168 份 ⇒ 树几乎是空的。</para>
    ///
    /// <para><b>与「标准资料清单」页同源</b>：机构/标准/阶段复用
    /// <c>StandardDirectoryService.GetOrganizationTreeAsync</c>，文件夹保留段过滤复用同一份口径
    /// ⇒ 两页的树不可能漂移。</para>
    ///
    /// <para><b>与 <see cref="Tree"/> 的关系</b>：<c>tree</c> 保留（只列已登记模板，供
    /// 「候选/登记」类场景）；页面左树已切到本端点。<b>⛔ 不要删 <c>tree</c></b> ——
    /// 删了会让 ApiCode 关联断裂。</para>
    /// </summary>
    [HttpGet("directory-tree")]
    public async Task<IActionResult> DirectoryTree()
    {
        var nodes = await _directory.GetTemplateDirectoryTreeAsync();

        var total = 0;
        var withTemplate = 0;
        void Walk(List<TemplateDirectoryNode> ns)
        {
            foreach (var n in ns)
            {
                if (n.Extra.TryGetValue("kind", out var k) && k as string == "file")
                {
                    total++;
                    if (n.Extra.TryGetValue("hasTemplate", out var h) && h is true) withTemplate++;
                }
                Walk(n.Children);
            }
        }
        Walk(nodes);

        return Ok(ApiResponse<object>.Ok(new
        {
            Nodes = nodes,
            Total = total,
            WithTemplate = withTemplate,
        }));
    }

    /// <summary>
    /// <b>挂接 / 解绑全文填写提示词</b>：写 <c>cert_doc_template.FillPromptCode</c>。
    ///
    /// <para>【为什么单开一个端点，而不是复用通用 <c>update</c>】</para>
    /// <para>通用 <c>update</c> 会把 <c>BcFlag</c> 的全部列提交一遍 —— 前端只要漏传一个业务键，
    /// 它就被清空（见「静默失败陷阱」⑳/㉑）。而本操作**只改一列**，语义清晰、无法误伤。</para>
    ///
    /// <para>【校验】非空 <c>PromptCode</c> 必须**至少存在一个未软删的版本**，
    /// 否则就是挂了个空壳 —— 运行期 <c>resolve</c> 找不到版本 ⇒ 静默不走全文规则，
    /// 而界面上却显示「已挂接」，是最难查的一类不一致。</para>
    /// </summary>
    [HttpPost("set-prompt")]
    public async Task<IActionResult> SetPrompt([FromBody] SetPromptRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.TemplateCode))
            return Ok(ApiResponse<object>.Fail("请指定模板 Code"));

        // ★ 双关键字准则 A：定位只用 Code；含已删也要能查到（软删模板不该在这里被"复活"）
        var tpl = await _db.Client.Queryable<DocTemplate>()
            .Where(t => t.Code == req.TemplateCode)
            .FirstAsync();
        if (tpl == null)
            return Ok(ApiResponse<object>.Fail("模板不存在"));
        if (tpl.IsDeleted)
            return Ok(ApiResponse<object>.Fail("该模板已删除，无法挂接提示词"));

        var promptCode = (req.PromptCode ?? string.Empty).Trim();

        if (promptCode.Length > 0)
        {
            var versions = await _db.Client.Queryable<DocFillPrompt>()
                .Where(p => p.PromptCode == promptCode)
                .ToListAsync();
            if (!versions.Any(p => !p.IsDeleted))
                return Ok(ApiResponse<object>.Fail($"提示词「{promptCode}」还没有任何版本，无法挂接"));
        }

        tpl.FillPromptCode = promptCode.Length == 0 ? null : promptCode;
        tpl.UpdateTime = DateTime.Now;

        // ⚠️ 列级更新：并发写同行时不能整行覆盖（见「静默失败陷阱」㉕/㉖）
        var upd = await _db.UpdateAsync(tpl, new[]
        {
            nameof(DocTemplate.FillPromptCode),
            nameof(DocTemplate.UpdateTime),
        });
        if (!upd.Success)
            return Ok(ApiResponse<object>.Fail(upd.Error ?? "挂接提示词失败"));

        _logger.LogInformation("[DocTemplate] 挂接提示词：Template={Tpl}, PromptCode={P}",
            tpl.Code, promptCode.Length == 0 ? "(解绑)" : promptCode);

        return Ok(ApiResponse<object>.Ok(new
        {
            TemplateCode = tpl.Code,
            FillPromptCode = tpl.FillPromptCode,
            Bound = promptCode.Length > 0,
        }));
    }

    /// <summary>
    /// <b>左树（旧）</b>：已登记模板按「机构 → 标准 · 阶段 → 模板文件」三级展开。
    /// <para>节点字段遵循 <c>YzhTreeTableLayout</c> 契约：<c>Code</c> / <c>Name</c> / <c>Children</c> /
    /// <c>IsLeaf</c> / <c>Extra</c>（<c>Extra</c> 携带 <c>kind</c> 与锚点数，供右侧按 <c>kind</c> 分派）。</para>
    /// <para>⚠️ 页面左树已改用 <see cref="DirectoryTree"/>（资料清单驱动）。本端点保留兼容。</para>
    /// </summary>
    [HttpGet("tree")]
    public async Task<IActionResult> Tree()
    {
        var templates = (await _db.GetListAsync<DocTemplate>()).Data ?? new List<DocTemplate>();
        if (templates.Count == 0)
            return Ok(ApiResponse<object>.Ok(new { Nodes = new List<object>(), Total = 0 }));

        // 锚点数：一次分组查询，⛔ 不逐模板 count（N+1）
        var templateCodes = templates.Select(t => t.Code).ToList();
        var anchorRows = await _db.Client.Queryable<DocTemplateAnchor>()
            .Where(a => templateCodes.Contains(a.TemplateCode) && a.IsDeleted == false && a.IsValid == 1)
            .Select(a => new { a.TemplateCode, a.IsOrphan })
            .ToListAsync();

        var anchorStat = anchorRows
            .GroupBy(a => a.TemplateCode, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => new { Total = g.Count(), Orphan = g.Count(x => x.IsOrphan) },
                StringComparer.Ordinal);

        // 名称字典：机构 / 标准 / 阶段（只查真正出现的 Code，避免全表扫）
        var orgCodes = templates.Select(t => t.OrgCode).Where(c => !string.IsNullOrEmpty(c)).Distinct().ToList();
        var stdCodes = templates.Select(t => t.StandardCode).Where(c => !string.IsNullOrEmpty(c)).Distinct().ToList();
        var stageCodes = templates.Select(t => t.StageCode).Where(c => !string.IsNullOrEmpty(c)).Distinct().ToList();

        var orgNames = orgCodes.Count == 0
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : (await _db.Client.Queryable<CertificationBody>()
                    .Where(x => orgCodes.Contains(x.Code))
                    .Select(x => new { x.Code, x.Name })
                    .ToListAsync())
                .ToDictionary(x => x.Code, x => x.Name ?? x.Code, StringComparer.Ordinal);

        var stdNames = stdCodes.Count == 0
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : (await _db.Client.Queryable<ISOStandard>()
                    .Where(x => stdCodes.Contains(x.Code))
                    .Select(x => new { x.Code, Name = x.StandardName })
                    .ToListAsync())
                .ToDictionary(x => x.Code, x => x.Name ?? x.Code, StringComparer.Ordinal);

        var stageNames = stageCodes.Count == 0
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : (await _db.Client.Queryable<CertStage>()
                    .Where(x => stageCodes.Contains(x.Code))
                    .Select(x => new { x.Code, Name = x.StageName })
                    .ToListAsync())
                .ToDictionary(x => x.Code, x => x.Name ?? x.Code, StringComparer.Ordinal);

        // ★ 标准目录文件行：左树要给模板叶子挂「文档分类」徽标（37 号 §3.6 组 E / 组 F 分流），
        //   而 DocCategory 的**权威列在 `cert_standard_directory_file`**（不是契约表、也不是模板表）。
        //   ⇒ 一次批量查回，⛔ 不逐模板查（N+1）。
        var stdFileCodes = templates
            .Select(t => t.StandardFileCode)
            .Where(c => !string.IsNullOrEmpty(c))
            .Distinct()
            .ToList();

        var stdFiles = stdFileCodes.Count == 0
            ? new Dictionary<string, StandardDirectoryFile>(StringComparer.Ordinal)
            : (await _db.Client.Queryable<StandardDirectoryFile>()
                    .Where(x => stdFileCodes.Contains(x.Code))
                    .ToListAsync())
                .ToDictionary(x => x.Code, x => x, StringComparer.Ordinal);

        string NameOf(Dictionary<string, string> map, string key)
            => string.IsNullOrEmpty(key) ? "未指定" : map.TryGetValue(key, out var n) ? n : key;

        // 组装：机构 → 标准 · 阶段 → 模板
        var nodes = templates
            .GroupBy(t => t.OrgCode ?? string.Empty, StringComparer.Ordinal)
            .Select(orgGroup =>
            {
                var orgChildren = orgGroup
                    .GroupBy(t => $"{t.StandardCode}|{t.StageCode}", StringComparer.Ordinal)
                    .Select(scopeGroup =>
                    {
                        var first = scopeGroup.First();
                        var stdName = NameOf(stdNames, first.StandardCode);
                        var stageName = NameOf(stageNames, first.StageCode);
                        var scopeCode = $"SCOPE:{first.StandardCode}|{first.StageCode}";

                        var leaves = scopeGroup.Select(t =>
                        {
                            var stat = anchorStat.TryGetValue(t.Code, out var s) ? s : new { Total = 0, Orphan = 0 };
                            var stdFile = stdFiles.TryGetValue(t.StandardFileCode ?? string.Empty, out var sf) ? sf : null;
                            return new TreeDto
                            {
                                Code = t.Code,
                                Name = t.FileName,
                                IsLeaf = true,
                                Extra = new Dictionary<string, object?>
                                {
                                    ["kind"] = "template",
                                    ["standardFileCode"] = t.StandardFileCode,
                                    // ★ 文档分类（权威列 = 标准目录文件行）：editable / fixed
                                    ["docCategory"] = stdFile?.DocCategory,
                                    ["standardFileName"] = stdFile?.FileName,
                                    ["fileKind"] = t.FileKind,
                                    ["storagePath"] = t.StoragePath,
                                    ["fillPromptCode"] = t.FillPromptCode,
                                    ["publishStatus"] = t.PublishStatus,
                                    ["scanStatus"] = t.ScanStatus,
                                    ["anchorCount"] = stat.Total,
                                    ["orphanCount"] = stat.Orphan,
                                },
                            };
                        }).ToList();

                        return new TreeDto
                        {
                            Code = scopeCode,
                            Name = $"{stdName} · {stageName}",
                            IsLeaf = false,
                            Children = leaves,
                            Extra = new Dictionary<string, object?>
                            {
                                ["kind"] = "scope",
                                ["standardCode"] = first.StandardCode,
                                ["stageCode"] = first.StageCode,
                                ["standardName"] = stdName,
                                ["stageName"] = stageName,
                                ["templateCount"] = leaves.Count,
                            },
                        };
                    })
                    .ToList();

                return new TreeDto
                {
                    Code = $"ORG:{orgGroup.Key}",
                    Name = NameOf(orgNames, orgGroup.Key),
                    IsLeaf = false,
                    Children = orgChildren,
                    Extra = new Dictionary<string, object?>
                    {
                        ["kind"] = "org",
                        ["orgCode"] = orgGroup.Key,
                        ["templateCount"] = orgGroup.Count(),
                    },
                };
            })
            .ToList();

        return Ok(ApiResponse<object>.Ok(new
        {
            Nodes = nodes,
            Total = templates.Count,
        }));
    }

    // ════════════════════════════════════════════════════════════════════
    // 三、私有：校验 / 推导 / 查重 / 复活
    // ════════════════════════════════════════════════════════════════════

    /// <summary>统一校验 + 权威推导（新增/修改共用，<b>无副作用</b>，可重复调用）</summary>
    private async Task<(bool ok, string? msg)> PrepareAsync(DocTemplate entity)
    {
        if (entity == null) return (false, "请求体不能为空");

        if (string.IsNullOrWhiteSpace(entity.StandardFileCode))
            return (false, "请先选择宿主标准文件（StandardFileCode）");

        if (string.IsNullOrWhiteSpace(entity.StoragePath))
            return (false, "模板路径不能为空");

        // ★ 文件类型后端权威推导（单一约束原则：不依赖前端传入）
        //   先看 StoragePath —— 它才是填写引擎真正会打开的字节；FileName 只是展示名。
        //   实测：168 个模板行里 143 个是 .doc、11 个是 .xls，直接拿原始扩展名会全部登记失败。
        var kind = ResolveFileKind(entity.StoragePath) ?? ResolveFileKind(entity.FileName);
        if (kind == null)
        {
            return (false,
                "无法推导模板类型（只支持 .docx / .xlsx）："
                + $"StoragePath={entity.StoragePath}，FileName={entity.FileName}。"
                + "若源文件是 .doc / .xls，请先完成「归一」并把归一产物路径填入 StoragePath。");
        }
        entity.FileKind = kind;

        // ★ 身份段经提取规则的唯一作用域入口推导（⛔ 不在此另写一份 —— 两套口径必然漂移）
        var scope = await _extraction.ResolveRuleScopeAsync(entity.StandardFileCode);
        if (string.IsNullOrWhiteSpace(scope.OrgCode))
        {
            return (false,
                "无法确定模板所属机构：标准目录文件缺少 ConfigCode，或其所属配置缺少 OrgCode。"
                + "请先在「标准目录」里补全该文件的机构归属。");
        }

        entity.OrgCode = scope.OrgCode;
        entity.StandardCode = scope.StandardCode;
        entity.StageCode = scope.StageCode;

        // 登记即 draft + pending（首版不扫描，扫描列由后续扫描器写）
        if (string.IsNullOrWhiteSpace(entity.PublishStatus)) entity.PublishStatus = "draft";
        entity.ScanStatus = "pending";

        return (true, null);
    }

    /// <summary>按扩展名权威推导模板类型；不支持的类型返回 null（⛔ 不猜）</summary>
    private static string? ResolveFileKind(string? fileName)
    {
        var ext = Path.GetExtension(fileName ?? string.Empty)?.TrimStart('.').ToLowerInvariant();
        return ext switch
        {
            "docx" => "docx",
            "xlsx" => "xlsx",
            _ => null,
        };
    }

    /// <summary>从标准目录文件行推导「相对配置根的文件夹路径」（<c>FullPath</c> 去掉文件名那段）</summary>
    private static string ResolveFolderPath(StandardDirectoryFile file)
    {
        var full = file.FullPath;
        if (string.IsNullOrWhiteSpace(full)) return string.Empty;
        var idx = full.LastIndexOf('/');
        return idx <= 0 ? string.Empty : full[..idx];
    }

    /// <summary>
    /// 失效某个模板路径派生的**预览 PDF 字节缓存**（best-effort，⛔ 失败不阻断换版）。
    ///
    /// <para><b>缓存路径口径必须与读侧一致</b>：同走 <c>PathBuilder.Product</c>
    /// （<c>DocExtractionRuleService.GetPreviewPdfByPathAsync</c> 第 ③ 步），
    /// ⛔ 不在这里另写一份拼接 —— 两份必然漂移，而漂移的后果是「缓存删不掉 ⇒ 预览显示上一版」。</para>
    ///
    /// <para>MinIO 的 <c>RemoveObject</c> 对不存在的对象不报错（S3 语义），
    /// 所以首次上传时这里的删除是安全的空操作。</para>
    /// </summary>
    private async Task InvalidatePreviewCacheAsync(string templatePath)
    {
        try
        {
            var cachePath = PathBuilder.Product(templatePath, PathBuilder.PdfSegment, ".pdf");
            if (string.IsNullOrEmpty(cachePath)) return;
            await _storage.DeleteAsync(cachePath.TrimStart('/'));
            _logger.LogInformation("[DocTemplate] 已失效预览缓存: {Cache}", cachePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[DocTemplate] 预览缓存失效失败（不影响换版）: {Path}", templatePath);
        }
    }

    /// <summary>
    /// <b>上传空白模板后的落库</b>（覆盖语义）：
    /// <list type="bullet">
    /// <item>命中<b>存活</b>行 ⇒ 就地更新（<b>沿用同 Code</b> ⇒ 锚点/填充日志外键不失效），
    /// <c>PublishStatus</c> 归 <c>draft</c>、<c>ScanStatus</c> 归 <c>pending</c> —— 字节变了，旧锚点作废。</item>
    /// <item>命中<b>已软删</b>行 ⇒ 就地复活（同 Code，⛔ 不能 INSERT，Id 会撞主键）。</item>
    /// <item>都没有 ⇒ 走 <see cref="AddCore"/> 正常新增。</item>
    /// </list>
    /// </summary>
    private async Task<Result<DocTemplate>> UpsertTemplateAsync(
        StandardDirectoryFile fileRow, string templatePath, string fileKind,
        string fileName, string sha256, string? remark)
    {
        var existing = await FindAnyByStandardFileAsync(fileRow.Code);
        if (existing == null)
        {
            return await AddCore(new DocTemplate
            {
                StandardFileCode = fileRow.Code,
                FileKind = fileKind,
                FileName = fileName,
                StoragePath = templatePath,
                SourceSha256 = sha256,
                Remark = remark,
                IsValid = 1,
            });
        }

        // 覆盖 / 复活：沿用同 Code
        existing.IsDeleted = false;
        existing.DeleteBy = null;
        existing.DeleteTime = null;

        // ★ 换版判定必须在覆盖 `SourceSha256` **之前**做（2026-10-04 用户实测缺陷）。
        //   判定结果决定「旧锚点是否作废」，见方法尾部。
        var versionChanged = !string.Equals(existing.SourceSha256, sha256, StringComparison.OrdinalIgnoreCase);

        existing.StandardFileCode = fileRow.Code;
        existing.FileKind = fileKind;
        existing.FileName = fileName;
        existing.StoragePath = templatePath;
        existing.SourceSha256 = sha256;
        if (!string.IsNullOrWhiteSpace(remark)) existing.Remark = remark;
        existing.IsValid = 1;

        // 字节变了 ⇒ 回到「已上传未扫描」，旧校验结果与发布状态全部作废
        existing.PublishStatus = "draft";
        existing.ScanStatus = "pending";
        existing.ScanMessage = null;
        existing.ScanTime = null;
        existing.ViolationJson = null;
        existing.PartCount = 0;   // ★ 旧锚点随换版作废 ⇒ 部件数归零（重扫时按实际重算）

        // ★★ 换版必须清空「试填预览」（2026-10-06，B7 配套）——
        //   试填预览 PDF 的路径由**模板路径**派生且是**固定 key**（`PathBuilder.PreviewFromTemplate`，
        //   不带时间戳，理由见该方法的注释：试填不需要历史版本、省空间）。
        //   ⇒ 换版后同名模板的预览路径**完全相同** ⇒ 不清空的话，中栏「填充后预览」会继续显示
        //   **上一版模板**填出来的 PDF。这不是「内容旧了」，而是「这份预览属于另一份文档」
        //   —— 用户会以为规则改了没生效，然后反复改规则、反复试填（方向完全错）。
        //   ⛔ 与「改锚点规则」区分：改规则**不清**预览（那只是快照变旧，页面用 `PreviewTime`
        //   如实显示「试填于何时」即可，属 §P3「建议 ≠ 事实」的同类口径：不阻断、不替人决定）。
        var stalePreviewPath = existing.PreviewPdfPath;
        if (versionChanged)
        {
            existing.PreviewPdfPath = null;
            existing.PreviewTime = null;
        }

        // 身份段重新推导（模板可能被改挂到另一个标准文件）
        var scope = await _extraction.ResolveRuleScopeAsync(fileRow.Code);
        existing.OrgCode = scope.OrgCode;
        existing.StandardCode = scope.StandardCode;
        existing.StageCode = scope.StageCode;
        existing.UpdateTime = DateTime.Now;

        var upd = await _db.UpdateAsync(existing);
        if (!upd.Success)
            return Result<DocTemplate>.Fail(upd.Error ?? "模板更新失败");

        _logger.LogInformation("[DocTemplate] 空白模板覆盖：StandardFileCode={Code}, Template={Tpl}",
            fileRow.Code, existing.Code);

        // ★ 换版 ⇒ 旧锚点全部作废（软删）。必须在模板更新**成功之后**做 ——
        //   更新失败就动锚点，会留下「模板还是旧字节、锚点却已被清空」的不一致状态。
        if (versionChanged) await InvalidateAnchorsAsync(existing.Code);

        // ★ 换版 ⇒ 旧试填预览对象也删掉（best-effort，⛔ 失败不阻断换版）。
        //   「同名换版」时新预览路径与旧路径**相同**（固定 key），下次试填会覆盖；
        //   但「改名换版」会让旧对象变成**孤儿**（DB 已无引用、MinIO 里却还占着空间）。
        //   两种都删一遍最省心 —— RemoveObject 对不存在的对象不报错（S3 语义）。
        if (versionChanged && !string.IsNullOrWhiteSpace(stalePreviewPath))
        {
            try
            {
                await _storage.DeleteAsync(stalePreviewPath.TrimStart('/'));
                _logger.LogInformation("[DocTemplate] 已删除随换版失效的试填预览对象: {Path}", stalePreviewPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[DocTemplate] 试填预览对象删除失败（不影响换版）: {Path}", stalePreviewPath);
            }
        }

        return Result<DocTemplate>.Ok(existing);
    }

    /// <summary>
    /// <b>换版时作废该模板的全部锚点</b>（软删，留痕）。
    ///
    /// <para><b>为什么必须做</b>（2026-10-04 用户实测缺陷）：重新上传 = 换版，模板字节已变，
    /// 旧锚点描述的是<b>上一版</b>文档 ⇒ 必须作废。修复前的行为是「只把本次没扫到的标孤儿、
    /// 不删」，结果用户重新上传后页面上<b>旧锚点与新锚点并存</b>，用户的原话就是
    /// 「重新上传并没有删除之前的锚点，好像又重复了锚点」。</para>
    ///
    /// <para><b>★ 为什么是软删而不是硬删</b>：① 留痕、可审计、可回溯；
    /// ② 随后重扫时 <c>FindByUniqueKeyAsync</c> 会按 <c>uk_tpl_anchor</c> 命中<b>已软删</b>行并
    /// 逐条复活 ⇒ 「新模板里<b>仍然存在</b>的同名 token」会<b>保留原配置</b>
    /// （<c>SourceSpec</c> / <c>Required</c> / <c>WriteMode</c> …），只有「新模板里<b>已消失</b>的
    /// token」才真正退出列表。这正是用户期望的语义 —— 换版不是「从零开始」，
    /// 而是「锚点集跟着新文档走」。</para>
    ///
    /// <para><b>⛔ 同字节重传不作废</b>：调用方只在 <c>SourceSha256</c> 变化时才调用本方法；
    /// 用户只是又点了一次上传时，文档没变，清空锚点会白丢实施人员已配好的规则。</para>
    ///
    /// <para><b>⛔ 绝不抛异常</b>：作废失败不能连累上传（上传已成功、字节已落 MinIO），
    /// 只留日志 —— 否则用户会看到「上传失败」，而文件其实已经在存储里了。</para>
    /// </summary>
    private async Task InvalidateAnchorsAsync(string templateCode)
    {
        try
        {
            var codes = await _db.Client.Queryable<DocTemplateAnchor>()
                .Where(a => a.TemplateCode == templateCode && a.IsDeleted == false)
                .Select(a => a.Code)
                .ToListAsync();
            if (codes.Count == 0) return;

            var del = await _anchors.DeleteBatch(codes, clientIp: UserContext.ClientIp);
            if (del.Success)
            {
                _logger.LogInformation("[DocTemplate] 换版作废旧锚点：Template={Tpl}, Count={N}",
                    templateCode, codes.Count);
            }
            else
            {
                _logger.LogWarning("[DocTemplate] 换版作废旧锚点失败（不影响上传）：Template={Tpl}, Err={Err}",
                    templateCode, del.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[DocTemplate] 换版作废旧锚点异常（不影响上传）：Template={Tpl}", templateCode);
        }
    }

    /// <summary>
    /// 按 <c>uk_standard_file</c>（<c>StandardFileCode</c>）查一行，<b>含已软删</b>。
    /// <para>⛔ 不能用 <c>GetOneIgnoreValidAsync</c>：它<b>仍然过滤软删除</b>
    /// （<c>SqlSugarDbOrm.cs:64</c> 带 <c>IsDeletedCondition</c>），查不到已删行 ⇒ 复活分支永不触发，
    /// 撞唯一键时只会得到一个 1062 报错。此处直接走 <c>Client.Queryable</c>（无任何隐式过滤）。</para>
    /// </summary>
    private Task<DocTemplate?> FindAnyByStandardFileAsync(string standardFileCode)
    {
        return _db.Client.Queryable<DocTemplate>()
            .Where(t => t.StandardFileCode == standardFileCode)
            .FirstAsync();
    }

    /// <summary>
    /// 就地复活已软删的模板行：<b>沿用同 Code / Id</b>（锚点、填充日志的外键不失效），
    /// 业务字段按本次登记刷新，并清空软删标记。
    /// </summary>
    private async Task<Result<DocTemplate>> ReviveAsync(DocTemplate dead, DocTemplate incoming)
    {
        dead.IsDeleted = false;
        dead.DeleteBy = null;
        dead.DeleteTime = null;

        dead.OrgCode = incoming.OrgCode;
        dead.StandardCode = incoming.StandardCode;
        dead.StageCode = incoming.StageCode;
        dead.FileKind = incoming.FileKind;
        dead.FileName = incoming.FileName;
        dead.StoragePath = incoming.StoragePath;
        dead.SourceSha256 = incoming.SourceSha256;
        dead.FillPromptCode = incoming.FillPromptCode;
        dead.Remark = incoming.Remark;
        dead.IsValid = incoming.IsValid;
        dead.UpdateTime = DateTime.Now;

        var upd = await _db.UpdateAsync(dead);
        if (!upd.Success)
            return Result<DocTemplate>.Fail(upd.Error ?? "复活已删除的模板记录失败");

        _logger.LogInformation("[DocTemplate] 复活已软删模板：StandardFileCode={Code}, TemplateCode={Tpl}",
            dead.StandardFileCode, dead.Code);

        return Result<DocTemplate>.Ok(dead);
    }

    /// <summary>从标准目录文件登记模板的请求</summary>
    public sealed class RegisterRequest
    {
        /// <summary>宿主标准文件 Code（<c>cert_standard_directory_file.Code</c>，必填）</summary>
        public string StandardFileCode { get; set; } = string.Empty;

        /// <summary>展示用文件名（空 = 取标准目录文件行的 FileName）</summary>
        public string? FileName { get; set; }

        /// <summary>全文填写规则 PromptCode（空 = 不走全文规则）</summary>
        public string? FillPromptCode { get; set; }

        /// <summary>备注</summary>
        public string? Remark { get; set; }
    }

    /// <summary>挂接全文填写提示词的请求（<c>POST set-prompt</c>）</summary>
    public sealed class SetPromptRequest
    {
        /// <summary>模板 Code（<c>cert_doc_template.Code</c>，必填）—— ★ 双关键字准则 A：定位只用 Code</summary>
        public string? TemplateCode { get; set; }

        /// <summary>提示词编码；<b>空串 = 解绑</b>（该模板不走全文规则）</summary>
        public string? PromptCode { get; set; }
    }

    /// <summary>上传空白模板的请求（<c>multipart/form-data</c>）</summary>
    public sealed class UploadTemplateDto
    {        /// <summary>空白模板文件（<c>.docx</c> / <c>.xlsx</c>）</summary>
        public IFormFile? File { get; set; }

        /// <summary>宿主标准文件 Code（<c>cert_standard_directory_file.Code</c>，必填）</summary>
        public string StandardFileCode { get; set; } = string.Empty;

        /// <summary>备注</summary>
        public string? Remark { get; set; }
    }

    /// <summary>左树节点（契约：<c>Code</c> / <c>Name</c> / <c>Children</c> / <c>IsLeaf</c> / <c>Extra</c>）</summary>
    public sealed class TreeDto
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsLeaf { get; set; }
        public List<TreeDto> Children { get; set; } = new();
        public Dictionary<string, object?> Extra { get; set; } = new();
    }
}

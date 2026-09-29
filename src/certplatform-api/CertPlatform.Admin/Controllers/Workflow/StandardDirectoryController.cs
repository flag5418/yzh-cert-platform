using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Controllers;
using YZH.Core.Api.Filters;
using YZH.Core.Api.Services;
using YZH.Core.DataBase.Services;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Config;
using YZH.Core.Stand.Models.Request;
using YZH.Core.Stand.Models.Result;
using CertPlatform.Admin.Services.StandardDirectory;
using CertPlatform.Shared.Entities.Dir;

namespace CertPlatform.Admin.Controllers.Workflow;

/// <summary>
/// 标准目录管理控制器
/// 路由前缀：/api/Workflow/StandardDirectory
///
/// <para><b>架构对齐（审计 P0-4 / 决策 ⑯，2026-09-27）</b>：
/// ① 由裸 <c>ControllerBase</c> 改为继承 <c>YzhControllerBase&lt;StandardDirectoryConfig&gt;</c>，
///    与全站控制器同构（<c>[YZHAuthorize]</c> + 实体配置 + 通用端点）；
/// ② <b>全部端点显式标注 <c>[RequirePermission]</c></b>（含继承自基类的 10 个通用端点）——
///    权限码 = <c>ApiScanner.GenerateApiCode</c> 的 SHA256 原像
///    <c>"{METHOD}|StandardDirectory|{action小写}"</c>，与 <c>sys_api.Code</c> 逐字一致，
///    因此「角色-接口」页勾选后立即生效；
/// ③ 基类通用增删改 <b>覆写 Core 方法委托业务服务</b>，否则会绕过 Code 生成 / 唯一键 /
///    级联删除 / 白名单字段更新（= 审计 P0-5「全字段覆盖」同类缺陷）。</para>
///
/// <para>⚠️ 动作名 <c>GetConfig</c> 已改名 <c>GetConfigByCode</c>：与基类 <c>GET config</c>
/// 同名会让 <c>ApiScanner</c> 撞出重复 ApiCode 而跳过登记（角色-接口页少一行）。
/// 改名使旧 ApiCode 失效，须重跑 ApiSync（启动时自动）并重新关联 —— 当前该控制器
/// 的角色-接口关联数为 0，无可断裂项。</para>
/// </summary>
[ApiController]
[Route("api/Workflow/[controller]")]
public class StandardDirectoryController : YzhControllerBase<StandardDirectoryConfig>
{
    private readonly StandardDirectoryService _service;
    private readonly DirectoryTemplateService _templateService;
    private readonly QueueManager _queueManager;

    public StandardDirectoryController(
        EntityService<StandardDirectoryConfig> entityService,
        StandardDirectoryService service,
        DirectoryTemplateService templateService,
        QueueManager queueManager,
        IUserContext userContext)
        : base(entityService, userContext)
    {
        _service = service;
        _templateService = templateService;
        _queueManager = queueManager;
    }

    #region 组织树

    [RequirePermission("d22650ef6003418f257ee0290dff845d22ed15cabaecc79dfb41402e9a7adcb7")] // GET|StandardDirectory|getorganizationtree
    [HttpGet("organization-tree")]
    public async Task<IActionResult> GetOrganizationTree()
    {
        var tree = await _service.GetOrganizationTreeAsync();
        return Ok(ApiResponse<object?>.Ok(data: tree));
    }

    #endregion

    #region 配置 CRUD

    [RequirePermission("5c28376c3dc202a74e74e3a333241fe164d67d4901f75e5872f01900a21ef9b6")] // GET|StandardDirectory|getconfigs
    [HttpGet("configs")]
    public async Task<IActionResult> GetConfigs()
    {
        var configs = await _service.GetConfigsAsync();
        return Ok(ApiResponse<object?>.Ok(data: configs));
    }

    [RequirePermission("eb507bce0d6eba1c858f1569f97ac81956f58e5ff7a11ac8c648ca631785e0d2")] // GET|StandardDirectory|getconfigbycode
    [HttpGet("configs/{directoryCode}")]
    public async Task<IActionResult> GetConfigByCode(string directoryCode)
    {
        var config = await _service.GetConfigAsync(directoryCode);
        if (config == null) return Ok(ApiResponse.Fail("配置不存在"));
        return Ok(ApiResponse<object?>.Ok(data: config));
    }

    [RequirePermission("fd78ad4ef91da8a28e9394bbe39f3682fd6a26332750b96d073a4ee994705674")] // POST|StandardDirectory|createconfig
    [HttpPost("configs/create")]
    public async Task<IActionResult> CreateConfig([FromBody] StandardDirectoryConfig config)
    {
        var (ok, error, data) = await _service.CreateConfigAsync(config);
        return Ok(ok ? ApiResponse<object?>.Ok(data: data) : ApiResponse<object?>.Fail(error));
    }

    /// <summary>总表无感懒建（决策㉑）：三键幂等 ensure，不存在自动建、已删复活，前端首次进入阶段/上传时调用</summary>
    [RequirePermission("943efed75fbbfe24f721f32fae8f7215c63d4ae09e9ec05d337982a02cf80887")] // POST|StandardDirectory|ensureconfig
    [HttpPost("configs/ensure")]
    public async Task<IActionResult> EnsureConfig([FromBody] StandardDirectoryConfig input)
    {
        var (ok, error, data) = await _service.EnsureConfigAsync(
            input.OrgCode, input.StandardCode, input.StageCode);
        return Ok(ok ? ApiResponse<object?>.Ok(data: data) : ApiResponse<object?>.Fail(error));
    }

    [RequirePermission("abe1635ca4e7fbd8c062d5bc085d44e02c7b68fa87e12577dd538c71462148e5")] // POST|StandardDirectory|updateconfig
    [HttpPost("configs/{directoryCode}")]
    public async Task<IActionResult> UpdateConfig(string directoryCode, [FromBody] StandardDirectoryConfig config)
    {
        var (ok, error) = await _service.UpdateConfigAsync(directoryCode, config);
        return Ok(ok ? ApiResponse<object?>.Ok("更新成功") : ApiResponse<object?>.Fail(error ?? "更新失败"));
    }

    [RequirePermission("3184a2ae239bb3c7a43d359b94978add29ef99af0a270775dc8a082455119214")] // POST|StandardDirectory|deleteconfig
    [HttpPost("configs/{directoryCode}/delete")]
    public async Task<IActionResult> DeleteConfig(string directoryCode)
    {
        var (ok, error) = await _service.DeleteConfigAsync(directoryCode);
        return Ok(ok ? ApiResponse<object?>.Ok("删除成功") : ApiResponse<object?>.Fail(error ?? "删除失败"));
    }

    #endregion

    #region 文件夹

    [RequirePermission("a3077ff31e264cd0974eb060cc570f5a18265998993be2645f6de398ef48e451")] // GET|StandardDirectory|getfoldertree
    [HttpGet("configs/{directoryCode}/folders")]
    public async Task<IActionResult> GetFolderTree(string directoryCode)
    {
        var tree = await _service.GetFolderTreeAsync(directoryCode);
        return Ok(ApiResponse<object?>.Ok(data: tree));
    }

    [RequirePermission("e6192325f9788e0f451e8b204ac58eb802906fe68a3541eaecf637b5e3a0a38e")] // GET|StandardDirectory|getfoldersflat
    [HttpGet("configs/{directoryCode}/folders-flat")]
    public async Task<IActionResult> GetFoldersFlat(string directoryCode)
    {
        var folders = await _service.GetFoldersFlatAsync(directoryCode);
        return Ok(ApiResponse<object?>.Ok(data: folders));
    }

    [RequirePermission("ecd35c18507d49b5fd204670a0bd8c2c02ba86c619a3bf327358690feadc0ab0")] // POST|StandardDirectory|createfolder
    [HttpPost("configs/{directoryCode}/folders/create")]
    public async Task<IActionResult> CreateFolder(string directoryCode, [FromBody] StandardDirectoryFolder folder)
    {
        folder.ConfigCode = directoryCode;
        var (ok, error, result) = await _service.CreateFolderAsync(folder);
        return Ok(ok ? ApiResponse<object?>.Ok(data: result) : ApiResponse<object?>.Fail(error));
    }

    [RequirePermission("0e62710e4b0b837921391c96512d6b457631e32f054aa60d74d6a73bbd95e437")] // POST|StandardDirectory|updatefolder
    [HttpPost("folders/{folderCode}")]
    public async Task<IActionResult> UpdateFolder(string folderCode, [FromBody] StandardDirectoryFolder folder)
    {
        var (ok, error) = await _service.UpdateFolderAsync(folderCode, folder);
        return Ok(ok ? ApiResponse<object?>.Ok() : ApiResponse<object?>.Fail(error));
    }

    [RequirePermission("573749823fb8e6dca80376c97ab9f08d0f0e1d0bd8a3e027d069ecf83f6d0e50")] // POST|StandardDirectory|deletefolder
    [HttpPost("folders/{folderCode}/delete")]
    public async Task<IActionResult> DeleteFolder(string folderCode)
    {
        var (ok, error, foldersDeleted, filesDeleted) = await _service.DeleteFolderAsync(folderCode);
        return Ok(ok ? ApiResponse<object?>.Ok(data: new { foldersDeleted, filesDeleted }) : ApiResponse<object?>.Fail(error));
    }

    #endregion

    #region 文件

    [RequirePermission("bba2ceec09c47b3ca69b12a7f0391ba9dfe3e0e7c998dd53ae1bc41adf797edc")] // GET|StandardDirectory|getfiles
    [HttpGet("folders/{folderCode}/files")]
    public async Task<IActionResult> GetFiles(string folderCode)
    {
        var files = await _service.GetFilesAsync(folderCode);
        return Ok(ApiResponse<object?>.Ok(data: files));
    }

    [RequirePermission("214107b04d609f5286cb37aa682c6b3d740af3b2d7f9daed82beab78f9afacdb")] // GET|StandardDirectory|getrootfiles
    [HttpGet("directories/{directoryCode}/root-files")]
    public async Task<IActionResult> GetRootFiles(string directoryCode)
    {
        var files = await _service.GetRootFilesAsync(directoryCode);
        return Ok(ApiResponse<object?>.Ok(data: files));
    }

    [RequirePermission("7fd61ef977214adea5271b6a54bc61dd96de41525caab10c9a4d1510896444ea")] // POST|StandardDirectory|updatefile
    [HttpPost("files/{fileCode}")]
    public async Task<IActionResult> UpdateFile(string fileCode, [FromBody] StandardDirectoryFile file)
    {
        var (ok, error) = await _service.UpdateFileAsync(fileCode, file);
        return Ok(ok ? ApiResponse<object?>.Ok() : ApiResponse<object?>.Fail(error));
    }

    [RequirePermission("124b7cf9c1a2cd9bc6539de1b7cbab812a9f6450e8536ef754bf17272fc445cf")] // POST|StandardDirectory|deletefile
    [HttpPost("files/{fileCode}/delete")]
    public async Task<IActionResult> DeleteFile(string fileCode)
    {
        var (ok, error) = await _service.DeleteFileAsync(fileCode);
        return Ok(ok ? ApiResponse<object?>.Ok() : ApiResponse<object?>.Fail(error));
    }

    [RequirePermission("10bd13dbc84484f2265b587097fbbcaa961f433944e73064626790d442f336b9")] // GET|StandardDirectory|download
    [HttpGet("download")]
    public async Task<IActionResult> Download([FromQuery] string storagePath)
    {
        // 安全校验：仅允许 standard-directory/ 前缀，禁止路径穿越（防御性双保险，
        // MinIO 对象键虽无文件系统穿越风险，但防止越权读取 bucket 内其他模块对象）
        if (string.IsNullOrWhiteSpace(storagePath) || !this.IsAllowedStoragePath(storagePath))
            return Ok(ApiResponse.Fail("非法的文件路径"));

        var result = await _service.DownloadFileAsync(storagePath);
        if (result == null)
            return Ok(ApiResponse.Fail("文件不存在"));

        var (stream, contentType, fileName) = result.Value;
        return File(stream, contentType, fileName);
    }

    #endregion

    #region 上传 4 步

    [RequirePermission("073d658225a0d62c58859cf813796ef816edbd97822dcc990eb98556d5eb7500")] // POST|StandardDirectory|uploadinit
    [HttpPost("upload-init")]
    public async Task<IActionResult> UploadInit([FromBody] UploadManifestRequest manifest)
    {
        var (ok, error, response) = await _service.UploadInitAsync(manifest);
        if (!ok) return Ok(ApiResponse<object?>.Fail(error));
        return Ok(ApiResponse<object?>.Ok(data: response));
    }

    [RequirePermission("35ebcb213960c6d03549c17fce5fc5355a156ab6d7ef16a836e45a9eef9b24b7")] // POST|StandardDirectory|uploadfilev2
    [HttpPost("upload-file-v2")]
    public async Task<IActionResult> UploadFileV2([FromForm] UploadFileV2Dto dto)
    {
        if (dto.File == null || dto.File.Length == 0)
            return Ok(ApiResponse<object?>.Fail("请选择文件"));

        using var stream = dto.File.OpenReadStream();
        var (ok, error) = await _service.UploadFileAsync(stream, dto.File.Length, dto.FileCode, dto.TaskId);
        return Ok(ok ? ApiResponse<object?>.Ok() : ApiResponse<object?>.Fail(error));
    }

    [RequirePermission("0d30ee8df3f7f3bf8045360d1d8d63f88cee00ced6c00b0d84b63d95559943fd")] // POST|StandardDirectory|uploadconfirm
    [HttpPost("upload-confirm")]
    public async Task<IActionResult> UploadConfirm([FromBody] UploadConfirmRequest req)
    {
        var (ok, error, convertQueueCode) = await _service.UploadConfirmAsync(req.TaskId);
        return Ok(ok ? ApiResponse<object?>.Ok(data: new { convertQueueCode }) : ApiResponse<object?>.Fail(error));
    }

    [RequirePermission("5cb81e9166d3df03450806bdd3dcab90b071c3cf3526fc29e881541e9500868c")] // POST|StandardDirectory|uploadcancel
    [HttpPost("upload-cancel")]
    public async Task<IActionResult> UploadCancel([FromBody] UploadConfirmRequest req)
    {
        var (ok, error, deleted, restored) = await _service.UploadCancelAsync(req.TaskId);
        return Ok(ok ? ApiResponse<object?>.Ok(data: new { deleted, restored }) : ApiResponse<object?>.Fail(error));
    }

    /// <summary>
    /// 单文件替换（一步完成：覆盖上传 + 回填大小 + doc/xls 进转换队列）
    /// </summary>
    [RequirePermission("af44a8370701dedaab48b80792bbe55a16257e4a93a0142c4a998d8f570dddd8")] // POST|StandardDirectory|replacefile
    [HttpPost("files/{fileCode}/replace")]
    public async Task<IActionResult> ReplaceFile(string fileCode, [FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
            return Ok(ApiResponse<object?>.Fail("请选择文件"));

        using var stream = file.OpenReadStream();
        var (ok, error, convertQueueCode) = await _service.ReplaceFileAsync(fileCode, stream, file.Length);
        return Ok(ok ? ApiResponse<object?>.Ok(data: new { convertQueueCode }) : ApiResponse<object?>.Fail(error));
    }

    [RequirePermission("f559e95c8302ae2cb40995c747671db25815838b9bd6f5e215c07f7a8d6270f2")] // GET|StandardDirectory|getuploadstatus
    [HttpGet("upload-status")]
    public async Task<IActionResult> GetUploadStatus([FromQuery] string taskId)
    {
        var status = await _service.GetUploadStatusAsync(taskId);
        if (status == null) return Ok(ApiResponse.Fail("任务不存在"));
        return Ok(ApiResponse<object?>.Ok(data: status));
    }

    #endregion

    #region 队列相关

    [RequirePermission("347bdd649d55f41de6a44eeea1b9a70eb87ae6a53b1cedf4acd3430221d73fea")] // GET|StandardDirectory|getactivequeue
    [HttpGet("active-queue")]
    public async Task<IActionResult> GetActiveQueue([FromQuery] string directoryCode)
    {
        var queue = await _service.GetActiveQueueAsync(directoryCode);
        return Ok(ApiResponse<object?>.Ok(data: queue));
    }

    [RequirePermission("8ae8cbf569e79e65e078509ed49ce5215439aa0dd92855c2a3d08277eb5b4989")] // POST|StandardDirectory|getconvertprogress
    [HttpPost("convert/progress")]
    public async Task<IActionResult> GetConvertProgress([FromQuery] string taskId)
    {
        var progress = await _service.GetConvertProgressAsync(taskId);
        return Ok(ApiResponse<object?>.Ok(data: progress));
    }

    [RequirePermission("86541f83478ca7d834f982982fda1c1c454b66cc80d0e7f197e59ef492ed8532")] // POST|StandardDirectory|cancelconvert
    [HttpPost("convert/cancel")]
    public async Task<IActionResult> CancelConvert([FromQuery] string queueCode)
    {
        var (ok, error) = await _service.CancelConvertAsync(queueCode);
        return Ok(ok ? ApiResponse<object?>.Ok() : ApiResponse<object?>.Fail(error));
    }

    #endregion

    #region 阶段文件树（文档提取规则页面）

    /// <summary>
    /// 获取阶段的完整文件树（含规则属性）
    /// 用于文档提取规则管理页面
    /// </summary>
    [RequirePermission("3521ed388408e8b0d6f620d529168a355cd5a052935c96a220fffa47ac546da0")] // GET|StandardDirectory|getstagefiletree
    [HttpGet("stage-files/{directoryCode}")]
    public async Task<IActionResult> GetStageFileTree(string directoryCode)
    {
        var result = await _service.GetStageFileTreeAsync(directoryCode);
        return Ok(ApiResponse<object?>.Ok(data: result));
    }

    #endregion

    #region 目录级文件查询

    /// <summary>
    /// 获取目录下所有文件（不含子文件夹中的文件）
    /// </summary>
    [RequirePermission("c6449b1d51cb6c9cb4640fff2d125d558d2187af24895d62dad66da927855724")] // GET|StandardDirectory|getdirectoryfiles
    [HttpGet("directory-files")]
    public async Task<IActionResult> GetDirectoryFiles([FromQuery] string directoryCode)
    {
        var files = await _service.GetFilesByDirectoryAsync(directoryCode);
        return Ok(ApiResponse<object?>.Ok(data: files));
    }

    #endregion

    #region 手动创建文件记录

    /// <summary>
    /// 手动创建文件记录（不经上传流程）
    /// </summary>
    [RequirePermission("32f52d61768107d44c2b77e939811a33a963d8648465e526f37a5ebb0d79614b")] // POST|StandardDirectory|createfile
    [HttpPost("folders/{folderCode}/files/create")]
    public async Task<IActionResult> CreateFile(string folderCode, [FromBody] StandardDirectoryFile file)
    {
        file.FolderCode = folderCode;
        var (ok, error, result) = await _service.CreateFileAsync(file);
        return Ok(ok ? ApiResponse<object?>.Ok(data: result) : ApiResponse<object?>.Fail(error));
    }

    #endregion

    #region 导出打包 ZIP

    /// <summary>
    /// 将选中的文件夹和文件打包成 ZIP
    /// </summary>
    [RequirePermission("fa5c43b660f0d281dfcbd190205ccac9af54ef8885bb6d8d888ad9fc361ece6a")] // POST|StandardDirectory|exportaszip
    [HttpPost("configs/{directoryCode}/export")]
    public async Task<IActionResult> ExportAsZip(string directoryCode, [FromBody] CertPlatform.Shared.Entities.Dir.ExportRequest request)
    {
        if ((request?.FolderCodes == null || request.FolderCodes.Count == 0) &&
            (request?.FileCodes == null || request.FileCodes.Count == 0))
        {
            return Ok(ApiResponse.Fail("请至少选择一个文件夹或文件"));
        }

        try
        {
            var stream = await _service.ExportAsZipAsync(directoryCode, request.FolderCodes, request.FileCodes);
            var fileName = $"StandardDirectory_{directoryCode}_{DateTime.Now:yyyyMMddHHmmss}.zip";
            return File(stream, "application/zip", fileName);
        }
        catch (Exception ex)
        {
            // ★ P1-17/P1-18：下载/导出失败必须带原因回给前端（服务层不再写占位文件）
            return Ok(ApiResponse.Fail($"导出失败：{ex.Message}"));
        }
    }

    #endregion

    #region 旧版单文件上传（兼容旧前端）

    /// <summary>
    /// 旧版单文件直接上传（兼容旧前端直接上传场景）
    /// </summary>
    [RequirePermission("73db36f70b9ae2dd493ec8d6c8e194854ca8a891e9dc0cdd785cebef38f617b2")] // POST|StandardDirectory|uploadfilelegacy
    [HttpPost("upload-file")]
    public async Task<IActionResult> UploadFileLegacy([FromForm] UploadFileLegacyDto dto)
    {
        if (dto.File == null || dto.File.Length == 0)
            return Ok(ApiResponse<object?>.Fail("请选择文件"));

        using var stream = dto.File.OpenReadStream();
        var (ok, error, file) = await _service.UploadFileLegacyAsync(
            stream, dto.File.FileName, dto.DirectoryCode, dto.FolderCode,
            dto.OrgCode, dto.StandardCode, dto.PhaseCode);
        return Ok(ok ? ApiResponse<object?>.Ok(data: file) : ApiResponse<object?>.Fail(error));
    }

    #endregion

    #region 文件锁定状态

    /// <summary>
    /// 批量查询文件在运行中队列中的锁定状态
    /// </summary>
    [RequirePermission("35d0c0926835c9769c1fb567a0c7056ee2bafa1c7302bfada53e320bc2031521")] // POST|StandardDirectory|getfilelockstatus
    [HttpPost("file-lock-status")]
    public async Task<IActionResult> GetFileLockStatus([FromBody] FileLockStatusRequest request)
    {
        var result = await _service.GetFileLockStatusAsync(request.FileCodes);
        return Ok(ApiResponse<object?>.Ok(data: result));
    }

    #endregion

    #region 重试失败转换

    /// <summary>
    /// 重试失败的文档转换
    /// </summary>
    [RequirePermission("a3c8d1b623b6beb94b04b7b8c779a4ad87a161fc058a4afeabeb79c307405755")] // POST|StandardDirectory|retryfailedconversions
    [HttpPost("retry-failed-conversions")]
    public async Task<IActionResult> RetryFailedConversions()
    {
        var (ok, error, enqueued, queueCount) = await _service.RetryFailedConversionsAsync();
        return Ok(ok ? ApiResponse<object?>.Ok(data: new { enqueued, queueCount }) : ApiResponse<object?>.Fail(error));
    }

    #endregion

    #region 存量产物回填（★ 2026-09-26 双产物链）

    /// <summary>
    /// 为存量文件补齐双产物（PreviewPdfPath / MarkdownPath）。
    /// <para>一次性运维接口：双产物链上线前上传的文件产物全为空，且 ConvertStatus 是 completed
    /// （旧 doc→docx 链留下），因此 <c>retry-failed-conversions</c> 捞不到它们。</para>
    /// <para>与重试的差异：**不隐藏文件**（不置 IsValid=0），且只补缺的那条链。</para>
    /// </summary>
    [RequirePermission("0458b3cfe21891f939c607e29a0e3d281abfae692daeb7bbcf73f474eb6ce9d7")] // POST|StandardDirectory|backfillconversions
    [HttpPost("backfill-conversions")]
    public async Task<IActionResult> BackfillConversions([FromBody] BackfillConversionsRequest? req)
    {
        var (ok, error, scanned, enqueued, queueCount) =
            await _service.BackfillConversionsAsync(req?.Limit ?? 200, req?.DirectoryCode);
        return Ok(ok
            ? ApiResponse<object?>.Ok(data: new { scanned, enqueued, queueCount })
            : ApiResponse<object?>.Fail(error));
    }

    #endregion

    #region 存量上传任务修复

    /// <summary>
    /// 修复卡死的存量上传任务（历史缺陷：GetOneAsync 的 IsValid=1 过滤导致状态机卡死）。
    /// 对指定任务（缺省 = 全部 initialized 且已过期的任务）逐文件检查 MinIO 对象：
    /// 存在则回填 FileSize 并激活（doc/xls 置入转换队列，其余直接 active）。
    /// </summary>
    [RequirePermission("11a32192b3932dca5b0bb634598fc3c78d6a51ae4244c3cea00a4372404f4ea1")] // POST|StandardDirectory|repairstuckuploads
    [HttpPost("repair-stuck-uploads")]
    public async Task<IActionResult> RepairStuckUploads([FromBody] RepairStuckUploadsRequest? req)
    {
        var (ok, error, repaired, enqueued) = await _service.RepairStuckUploadsAsync(req?.TaskId);
        return Ok(ok ? ApiResponse<object?>.Ok(data: new { repaired, enqueued }) : ApiResponse<object?>.Fail(error));
    }

    #endregion

    #region 基类通用端点 —— 业务委托（禁止绕过服务层规则）

    /// <summary>
    /// 基类通用增删改必须委托本控制器的业务服务，否则会绕过：
    /// ① Code 生成 + 唯一键 uk_std_stage ② 级联软删（文件夹/文件/MinIO 对象）
    /// ③ 白名单字段更新（审计 P0-5「全字段覆盖」的同类缺陷）。
    /// </summary>
    public override async Task<Result<StandardDirectoryConfig>> AddCore(StandardDirectoryConfig entity)
    {
        var (valid, msg) = ValidateEntity(entity);
        if (!valid) return Result<StandardDirectoryConfig>.Fail(msg ?? "校验失败");

        var (ok, error, data) = await _service.CreateConfigAsync(entity);
        return ok && data != null
            ? Result<StandardDirectoryConfig>.Ok(data)
            : Result<StandardDirectoryConfig>.Fail(error ?? "创建失败");
    }

    /// <summary>更新只认业务键 Code（双关键字准则）：缺 Code 直接失败，禁止回退 Id</summary>
    public override async Task<Result<StandardDirectoryConfig>> UpdateCore(StandardDirectoryConfig entity)
    {
        if (string.IsNullOrWhiteSpace(entity.Code))
            return Result<StandardDirectoryConfig>.Fail("更新失败：缺少业务键 Code");

        var (valid, msg) = ValidateEntity(entity);
        if (!valid) return Result<StandardDirectoryConfig>.Fail(msg ?? "校验失败");

        var (ok, error) = await _service.UpdateConfigAsync(entity.Code, entity);
        if (!ok) return Result<StandardDirectoryConfig>.Fail(error ?? "更新失败");

        var updated = await _service.GetConfigAsync(entity.Code);
        return Result<StandardDirectoryConfig>.Ok(updated ?? entity);
    }

    /// <summary>删除委托业务服务（软删 + 级联），绝不走基类 DeleteBatch 硬编码逻辑</summary>
    public override async Task<Result<int>> DeleteCore(params string[] codes)
    {
        if (codes == null || codes.Length == 0)
            return Result<int>.Fail("未指定要删除的记录");

        var deleted = 0;
        foreach (var code in codes)
        {
            if (string.IsNullOrWhiteSpace(code)) continue;
            var (ok, error) = await _service.DeleteConfigAsync(code);
            if (!ok) return Result<int>.Fail(error ?? $"删除 {code} 失败");
            deleted++;
        }
        return Result<int>.Ok(deleted);
    }

    #endregion

    #region 基类通用端点（HTTP 层显式补 [RequirePermission] —— 基类方法上的特性对子类不可见）

    [RequirePermission("f82005dd80180fc07f4cb98926c485da16eaed22c0b06f79eca825d6fbca37b5")] // GET|StandardDirectory|getconfig
    public override ActionResult<ApiResponse<EntityConfigDto>> GetConfig() => base.GetConfig();

    [RequirePermission("f8b17bafcf9eaec67443464d7ceccb2b22dcfa3754a1eb266c0a9845e7b965dc")] // POST|StandardDirectory|filter
    public override Task<ActionResult<ApiResponse<PagedResult<StandardDirectoryConfig>>>> Filter(
        [FromBody] FilterRequest request) => base.Filter(request);

    [RequirePermission("dc8761e30d0b73aef6ca9f4d7b9f7d8b4f6d542c982440908a9122c677a70c6c")] // POST|StandardDirectory|add
    public override Task<ActionResult<ApiResponse<StandardDirectoryConfig>>> Add(
        [FromBody] StandardDirectoryConfig entity) => base.Add(entity);

    [RequirePermission("f6b1394ffa263a3d97e1ba36e0849c15ebd7075654930f423148485512d07b64")] // POST|StandardDirectory|update
    public override Task<ActionResult<ApiResponse<StandardDirectoryConfig>>> Update(
        [FromBody] StandardDirectoryConfig entity) => base.Update(entity);

    [RequirePermission("644e3a4c5c593f60667a37d949906cf304efe3b04cc75581cf1a3c710a694fea")] // POST|StandardDirectory|delete
    public override Task<ActionResult<ApiResponse<object?>>> Delete([FromBody] string[] codes)
        => base.Delete(codes);

    [RequirePermission("5d33f7650e6ecf252b8004d0b3894022978f716563b4aa37c67e8ed1fc12bd06")] // POST|StandardDirectory|export
    public override Task<IActionResult> Export(
        [FromBody] YZH.Core.Stand.Models.Request.ExportRequest request) => base.Export(request);

    [RequirePermission("997cc5838bc43dd7b3b8a35538acc74b48119d58032f282933a84701836af886")] // POST|StandardDirectory|import
    public override Task<ActionResult<ApiResponse<ImportResult>>> Import(IFormFile file)
        => base.Import(file);

    [RequirePermission("717c0982bf6c0440a3760a0551ad832dd7a2b1d65575cfabd22de069b23ddae4")] // GET|StandardDirectory|downloadimporttemplate
    public override Task<IActionResult> DownloadImportTemplate() => base.DownloadImportTemplate();

    [RequirePermission("ec6d2198a431fd98a5d73dc45cd4190f295dfcaca14d148a1666a8c3525bdc80")] // POST|StandardDirectory|executeaction
    public override Task<ActionResult<ApiResponse<object?>>> ExecuteAction(
        string methodName, [FromBody] JsonElement entityData) => base.ExecuteAction(methodName, entityData);

    [RequirePermission("588ccdb1542d12970c2b439aac952c74a9cbf75baba619ce5b7af71e4c00529b")] // POST|StandardDirectory|toggleisvalid
    public override Task<ActionResult<ApiResponse<object?>>> ToggleIsValid(
        [FromBody] JsonElement entityData) => base.ToggleIsValid(entityData);

    #endregion
}

/// <summary>修复存量上传任务请求</summary>
public class RepairStuckUploadsRequest
{
    /// <summary>指定任务 ID；空 = 修复全部卡死任务</summary>
    public string? TaskId { get; set; }
}

/// <summary>存量产物回填请求</summary>
public class BackfillConversionsRequest
{
    /// <summary>单次最多处理多少个文件（缺省 200；防止一次把整库投进队列）</summary>
    public int Limit { get; set; } = 200;

    /// <summary>只回填指定目录（缺省 = 全部目录）</summary>
    public string? DirectoryCode { get; set; }
}

/// <summary>
/// 存储路径白名单校验：必须位于**已知文档库**（standard-directory / enterprise-documents）之下，
/// 且不含穿越片段。
///
/// <para>★ 2026-09-26 改造：原实现硬编码 <c>p.StartsWith("standard-directory/")</c>
/// （且声明了一个从未被使用的死字段 <c>AllowedPrefixes</c>）→ 一旦企业文档库启用，
/// 其 <c>file-preview</c>/<c>file-markdown</c>/<c>download</c> 请求会**静默被拒**：
/// 业务失败恒 HTTP 200，前端只看到「未找到文件」，**没有任何线索指向白名单**。
/// 现改为委托 <see cref="DocumentLibraryPath.IsAllowedStoragePath"/>（前缀的唯一权威源）。</para>
/// </summary>
public static partial class ControllerSafetyExtensions
{
    /// <summary>
    /// 校验存储路径是否允许访问（位于已知文档库之下 + 无穿越片段）
    /// </summary>
    public static bool IsAllowedStoragePath(this StandardDirectoryController _, string path)
        => DocumentLibraryPath.IsAllowedStoragePath(path);
}

/// <summary>
/// 旧版上传文件 DTO
/// </summary>
public class UploadFileLegacyDto
{
    public IFormFile File { get; set; }
    public string DirectoryCode { get; set; }
    public string FolderCode { get; set; }
    public string OrgCode { get; set; }
    public string StandardCode { get; set; }
    public string PhaseCode { get; set; }
}

/// <summary>
/// 文件锁定状态查询请求 DTO
/// </summary>
public class FileLockStatusRequest
{
    public List<string> FileCodes { get; set; } = new();
}

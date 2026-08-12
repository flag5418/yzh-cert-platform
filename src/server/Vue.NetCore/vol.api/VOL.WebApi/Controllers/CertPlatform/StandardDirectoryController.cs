/*
 * 标准目录管理 Controller
 */
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VOL.Core.Controllers.Basic;
using VOL.Core.Utilities;
using VOL.Core.Filters;
using VOL.Builder.IServices.CertPlatform;
using VOL.Builder.Services.CertPlatform;
using VOL.Entity.CertPlatform.Dir;
using VOL.Entity.DomainModels;
using Microsoft.Extensions.DependencyInjection;

namespace VOL.WebApi.Controllers.CertPlatform
{
    [Route("api/standard-directory")]
    [ApiController]
    [JWTAuthorize]
    public class StandardDirectoryController : ApiBaseController<object>
    {
        private readonly IStandardDirectoryService _service;
        private readonly ICodeGeneratorService _codeGenerator;

    private readonly ConvertQueueManager _queueManager;

    [ActivatorUtilitiesConstructor]
    public StandardDirectoryController(
        IStandardDirectoryService service,
        ICodeGeneratorService codeGenerator,
        ConvertQueueManager queueManager)
    : base(service)
    {
        _service = service;
        _codeGenerator = codeGenerator;
        _queueManager = queueManager;
    }

        #region 组织树

        /// <summary>
        /// 获取组织树数据
        /// 格式：机构 -> 标准 -> 阶段
        /// </summary>
        [HttpGet("organization-tree")]
        public IActionResult GetOrganizationTree()
        {
            var result = _service.GetOrganizationTree();
            return JsonNormal(result);
        }

        #endregion

        #region 标准目录配置

        /// <summary>
        /// 获取标准目录配置列表
        /// </summary>
        [HttpGet("configs")]
        public IActionResult GetConfigs()
        {
            var options = new PageDataOptions { Page = 1, Rows = 100 };
            var result = _service.GetConfigs(options);
            return JsonNormal(result);
        }

        /// <summary>
        /// 获取单个标准目录配置
        /// </summary>
        [HttpGet("configs/{directoryCode}")]
        public IActionResult GetConfig(string directoryCode)
        {
            var result = _service.GetConfig(directoryCode);
            return JsonNormal(new WebResponseContent().OK(null, result));
        }

        /// <summary>
        /// 创建标准目录配置
        /// </summary>
        [HttpPost("configs/create")]
        public IActionResult CreateConfig([FromBody] StandardDirectoryConfig config)
        {
            var result = _service.CreateConfig(config);
            return JsonNormal(result);
        }

        /// <summary>
        /// 更新标准目录配置
        /// </summary>
        [HttpPut("configs/{directoryCode}")]
        public IActionResult UpdateConfig(string directoryCode, [FromBody] StandardDirectoryConfig config)
        {
            config.DirectoryCode = directoryCode;
            var result = _service.UpdateConfig(config);
            return JsonNormal(result);
        }

        /// <summary>
        /// 删除标准目录配置
        /// </summary>
        [HttpDelete("configs/{directoryCode}")]
        public IActionResult DeleteConfig(string directoryCode)
        {
            var result = _service.DeleteConfig(directoryCode);
            return JsonNormal(result);
        }

        #endregion

        #region 标准目录文件夹

        /// <summary>
        /// 获取阶段的完整文件树（含规则属性）
        /// 用于文档提取规则管理页面，单次返回所有层级
        /// </summary>
        [HttpGet("stage-files/{directoryCode}")]
        public IActionResult GetStageFileTree(string directoryCode)
        {
            var result = _service.GetStageFileTree(directoryCode);
            return JsonNormal(new WebResponseContent().OK(null, result));
        }

        /// <summary>
        /// 获取标准目录文件夹树
        /// </summary>
        [HttpGet("configs/{directoryCode}/folders")]
        public IActionResult GetFolderTree(string directoryCode)
        {
            var result = _service.GetFolderTree(directoryCode);
            return JsonNormal(result);
        }

        /// <summary>
        /// 创建标准目录文件夹
        /// </summary>
        [HttpPost("configs/{directoryCode}/folders/create")]
        public IActionResult CreateFolder(string directoryCode, [FromBody] StandardDirectoryFolder folder)
        {
            folder.DirectoryCode = directoryCode;
            var result = _service.CreateFolder(folder);
            return JsonNormal(result);
        }

        /// <summary>
        /// 更新标准目录文件夹
        /// </summary>
        [HttpPut("folders/{folderCode}")]
        public IActionResult UpdateFolder(string folderCode, [FromBody] StandardDirectoryFolder folder)
        {
            folder.FolderCode = folderCode;
            var result = _service.UpdateFolder(folder);
            return JsonNormal(result);
        }

        /// <summary>
        /// 删除标准目录文件夹
        /// </summary>
        [HttpDelete("folders/{folderCode}")]
        public IActionResult DeleteFolder(string folderCode)
        {
            var result = _service.DeleteFolder(folderCode);
            return JsonNormal(result);
        }

        #endregion

        #region 标准目录文件

        /// <summary>
        /// 获取标准目录文件列表
        /// </summary>
        [HttpGet("folders/{folderCode}/files")]
        public IActionResult GetFiles(string folderCode)
        {
            var result = _service.GetFiles(folderCode);
            return JsonNormal(result);
        }

        /// <summary>
        /// 获取目录下所有文件（不含子文件夹中的文件）
        /// </summary>
        [HttpGet("directory-files")]
        public IActionResult GetDirectoryFiles([FromQuery] string directoryCode)
        {
            var result = _service.GetFilesByDirectory(directoryCode);
            return JsonNormal(result);
        }

        /// <summary>
        /// 创建标准目录文件
        /// </summary>
        [HttpPost("folders/{folderCode}/files/create")]
        public IActionResult CreateFile(string folderCode, [FromBody] StandardDirectoryFile file)
        {
            file.FolderCode = folderCode;
            var result = _service.CreateFile(file);
            return JsonNormal(result);
        }

        /// <summary>
        /// 更新标准目录文件
        /// </summary>
        [HttpPut("files/{fileCode}")]
        public IActionResult UpdateFile(string fileCode, [FromBody] StandardDirectoryFile file)
        {
            file.FileCode = fileCode;
            var result = _service.UpdateFile(file);
            return JsonNormal(result);
        }

        /// <summary>
        /// 删除标准目录文件
        /// </summary>
        [HttpDelete("files/{fileCode}")]
        public IActionResult DeleteFile(string fileCode)
        {
            var result = _service.DeleteFile(fileCode);
            return JsonNormal(result);
        }

        #endregion

        #region 导出打包

        /// <summary>
        /// 将选中的文件夹和文件打包成ZIP
        /// </summary>
        [HttpPost("configs/{directoryCode}/export")]
        public async Task<IActionResult> ExportAsZip(string directoryCode, [FromBody] ExportRequest request)
        {
            try
            {
                if ((request?.FolderCodes == null || request.FolderCodes.Count == 0) &&
                    (request?.FileCodes == null || request.FileCodes.Count == 0))
                {
                    return BadRequest("请至少选择一个文件夹或文件");
                }
                var stream = await _service.ExportAsZip(directoryCode, request.FolderCodes, request.FileCodes);
                var fileName = $"StandardDirectory_{directoryCode}_{System.DateTime.Now:yyyyMMddHHmmss}.zip";
                return File(stream, "application/zip", fileName);
            }
            catch (System.Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// 下载单个文件（从 MinIO 流式返回）
        /// </summary>
        [HttpGet("download")]
        public async Task<IActionResult> DownloadFile([FromQuery] string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path))
                    return BadRequest("缺少文件路径参数");

                var (stream, contentType, fileName) = await _service.DownloadFile(path);
                return File(stream, contentType, fileName);
            }
            catch (FileNotFoundException)
            {
                return NotFound("文件不存在");
            }
            catch (System.Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        #endregion

        #region 文件上传

        /// <summary>
        /// 上传文件到标准目录（旧版，保留兼容）
        /// 支持文件夹上传（通过relativePath保持目录结构）
        /// </summary>
        [HttpPost("upload-file")]
        public async Task<IActionResult> UploadFile([FromForm] UploadFileDto dto)
        {
            try
            {
                var result = await _service.UploadFile(dto.File, dto.DirectoryCode, dto.RelativePath);
                return JsonNormal(result);
            }
            catch (System.Exception ex)
            {
                return JsonNormal(new WebResponseContent().Error($"上传失败：{ex.Message}"));
            }
        }

        /// <summary>
        /// 批量上传预初始化
        /// 接收客户端清单，预创建编码和数据库记录，返回增强清单
        /// </summary>
        [HttpPost("upload-init")]
        public async Task<IActionResult> UploadInit([FromBody] UploadManifestRequest manifest)
        {
            try
            {
                var result = await _service.UploadInit(manifest);
                return JsonNormal(result);
            }
            catch (System.Exception ex)
            {
                return JsonNormal(new WebResponseContent().Error($"预处理失败：{ex.Message}"));
            }
        }

        /// <summary>
        /// 上传单个文件到MinIO（新版，基于taskId）
        /// </summary>
        [HttpPost("upload-file-v2")]
        [DisableRequestSizeLimit]
        public async Task<IActionResult> UploadFileV2([FromForm] UploadFileV2Dto dto)
        {
            try
            {
                var request = new UploadFileRequest
                {
                    FileCode = dto.FileCode,
                    StoragePath = dto.StoragePath,
                    TaskId = dto.TaskId
                };
                var result = await _service.UploadFileWithTask(dto.File, request);
                return JsonNormal(result);
            }
            catch (System.Exception ex)
            {
                return JsonNormal(new WebResponseContent().Error($"上传失败：{ex.Message}"));
            }
        }

        /// <summary>
        /// 确认上传完成
        /// </summary>
        [HttpPost("upload-confirm")]
        public async Task<IActionResult> UploadConfirm([FromQuery] string taskId)
        {
            try
            {
                var result = await _service.UploadConfirm(taskId);
                return JsonNormal(result);
            }
            catch (System.Exception ex)
            {
                return JsonNormal(new WebResponseContent().Error($"确认失败：{ex.Message}"));
            }
        }

        /// <summary>
        /// 回滚上传任务
        /// </summary>
        [HttpPost("upload-cancel")]
        public async Task<IActionResult> UploadCancel([FromQuery] string taskId)
        {
            try
            {
                var result = await _service.UploadCancel(taskId);
                return JsonNormal(result);
            }
            catch (System.Exception ex)
            {
                return JsonNormal(new WebResponseContent().Error($"回滚失败：{ex.Message}"));
            }
        }

        /// <summary>
        /// 查询上传任务状态
        /// </summary>
        [HttpGet("upload-status")]
        public IActionResult GetUploadStatus([FromQuery] string taskId)
        {
            try
            {
                var result = _service.GetUploadStatus(taskId);
                if (result == null)
                    return JsonNormal(new WebResponseContent().Error("任务不存在"));
                return JsonNormal(new WebResponseContent().OK(null, result));
            }
            catch (System.Exception ex)
            {
                return JsonNormal(new WebResponseContent().Error($"查询失败：{ex.Message}"));
            }
        }

        #endregion

        #region 编码生成工具

        /// <summary>
        /// 生成标准目录编码
        /// </summary>
        [HttpGet("codes/directory")]
        public IActionResult GenerateDirectoryCode(string standardCode, string phaseCode)
        {
            var code = _codeGenerator.GenerateDirectoryCode(standardCode, phaseCode);
            return JsonNormal(new WebResponseContent().OK(null, code));
        }

        /// <summary>
        /// 生成文件夹编码
        /// </summary>
        [HttpGet("codes/folder")]
        public IActionResult GenerateFolderCode(string directoryCode, int level, int sequence)
        {
            var code = _codeGenerator.GenerateFolderCode(directoryCode, level, sequence);
            return JsonNormal(new WebResponseContent().OK(null, code));
        }

        /// <summary>
        /// 生成文件编码（简化版）
        /// 格式：FL-{FolderCode}|{FileName}
        /// </summary>
        [HttpGet("codes/file")]
        public IActionResult GenerateFileCode(string folderCode, string fileName)
        {
            var code = _codeGenerator.GenerateFileCode(folderCode, fileName);
            return JsonNormal(new WebResponseContent().OK(null, code));
        }

        /// <summary>
        /// 生成存储路径（简化版）
        /// 格式：/{StandardCode}/{PhaseCode}/{FolderCode}/{FileName}
        /// </summary>
        [HttpGet("paths/storage")]
        public IActionResult GenerateStoragePath(
            string standardCode, 
            string phaseCode, 
            string folderCode, 
            string fileName)
        {
            var path = _codeGenerator.GenerateStoragePath(
                standardCode, phaseCode, folderCode, fileName);
            return JsonNormal(new WebResponseContent().OK(null, path));
        }

        #endregion

        #region 转换队列

[HttpPost("convert/progress")]
public async Task<IActionResult> GetConvertProgress([FromBody] dynamic param)
{
    string taskId = (string)(param?.taskId ?? "");
    var progress = await _queueManager.GetBatchProgressAsync(taskId);
            return JsonNormal(new WebResponseContent().OK(null, progress));
        }

        [HttpPost("convert/queue-status")]
        public async Task<IActionResult> GetQueueStatus()
        {
            var status = await _queueManager.GetQueueStatusAsync();
            return JsonNormal(new WebResponseContent().OK(null, status));
        }

        [HttpPost("convert/cancel")]
        public async Task<IActionResult> CancelConvert([FromQuery] string taskId)
        {
            await _queueManager.CancelBatchAsync(taskId);
            return JsonNormal(new WebResponseContent().OK("转换已取消"));
        }

        #endregion
    }
}

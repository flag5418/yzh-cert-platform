using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using VOL.Builder.IServices.CertPlatform;
using VOL.Core.Controllers.Basic;
using VOL.Entity.AttributeManager;

namespace VOL.WebApi.Controllers.CertPlatform
{
    [Route("api/DirectoryTemplate")]
    [PermissionTable(Name = "DirectoryTemplate")]
    [ApiController]
    public class DirectoryTemplateController : ApiBaseController<IDirectoryTemplateService>
    {
        public DirectoryTemplateController(IDirectoryTemplateService service) : base(service) { }

        /// <summary>
        /// 获取目录树
        /// </summary>
        [HttpGet, Route("tree")]
        public async Task<IActionResult> GetTree([FromQuery] string configCode)
        {
            var tree = await Service.GetTreeAsync(configCode);
            return Ok(new { status = true, data = tree });
        }

        /// <summary>
        /// 新增文件夹
        /// </summary>
        [HttpPost, Route("addFolder")]
        public async Task<IActionResult> AddFolder([FromBody] VOL.Entity.CertPlatform.Cert.DirectoryTemplate entity)
        {
            var result = await Service.AddFolderAsync(entity);
            return Ok(result);
        }

        /// <summary>
        /// 修改文件夹
        /// </summary>
        [HttpPost, Route("updateFolder")]
        public async Task<IActionResult> UpdateFolder([FromBody] VOL.Entity.CertPlatform.Cert.DirectoryTemplate entity)
        {
            var result = await Service.UpdateFolderAsync(entity);
            return Ok(result);
        }

        /// <summary>
        /// 删除文件夹
        /// </summary>
        [HttpPost, Route("deleteFolder")]
        public async Task<IActionResult> DeleteFolder([FromBody] FolderCodeRequest request)
        {
            var result = await Service.DeleteFolderAsync(request.FolderCode);
            return Ok(result);
        }

        /// <summary>
        /// 获取文件要求列表
        /// </summary>
        [HttpGet, Route("fileRequirements")]
        public async Task<IActionResult> GetFileRequirements([FromQuery] string folderCode)
        {
            var list = await Service.GetFileRequirementsAsync(folderCode);
            return Ok(new { status = true, data = list });
        }

        /// <summary>
        /// 保存文件要求
        /// </summary>
        [HttpPost, Route("saveFileRequirement")]
        public async Task<IActionResult> SaveFileRequirement([FromBody] VOL.Entity.CertPlatform.Cert.FileRequirement entity)
        {
            var result = await Service.SaveFileRequirementAsync(entity);
            return Ok(result);
        }

        /// <summary>
        /// 删除文件要求
        /// </summary>
        [HttpPost, Route("deleteFileRequirement")]
        public async Task<IActionResult> DeleteFileRequirement([FromBody] RequirementCodeRequest request)
        {
            var result = await Service.DeleteFileRequirementAsync(request.RequirementCode);
            return Ok(result);
        }

        // ===== 模板文件管理 API =====

        /// <summary>
        /// 上传标准目录模板文件
        /// </summary>
        [HttpPost, Route("uploadTemplateFile")]
        public async Task<IActionResult> UploadTemplateFile([FromForm] TemplateFileUploadRequest request)
        {
            if (request.File == null || request.File.Length == 0)
                return Ok(new { status = false, message = "请选择文件" });

            using var stream = request.File.OpenReadStream();
            var result = await Service.UploadTemplateFileAsync(
                request.RequirementCode, request.File.FileName, stream, request.File.Length);

            return Ok(result);
        }

        /// <summary>
        /// 下载标准目录模板文件
        /// </summary>
        [HttpGet, Route("downloadTemplateFile")]
        public async Task<IActionResult> DownloadTemplateFile([FromQuery] string requirementCode)
        {
            var (stream, fileName, contentType) = await Service.DownloadTemplateFileAsync(requirementCode);
            if (stream == null)
                return Ok(new { status = false, message = "模板文件不存在" });

            return File(stream, contentType ?? "application/octet-stream", fileName);
        }

        /// <summary>
        /// 删除标准目录模板文件
        /// </summary>
        [HttpPost, Route("deleteTemplateFile")]
        public async Task<IActionResult> DeleteTemplateFile([FromBody] RequirementCodeRequest request)
        {
            var result = await Service.DeleteTemplateFileAsync(request.RequirementCode);
            return Ok(result);
        }

        /// <summary>
        /// 重命名标准目录模板文件
        /// </summary>
        [HttpPost, Route("renameTemplateFile")]
        public async Task<IActionResult> RenameTemplateFile([FromBody] RenameTemplateFileRequest request)
        {
            var result = await Service.RenameTemplateFileAsync(request.RequirementCode, request.NewFileName);
            return Ok(result);
        }
    }

    public class FolderCodeRequest
    {
        public string FolderCode { get; set; }
    }

    public class RequirementCodeRequest
    {
        public string RequirementCode { get; set; }
    }

    public class TemplateFileUploadRequest
    {
        public string RequirementCode { get; set; }
        public IFormFile File { get; set; }
    }

    public class RenameTemplateFileRequest
    {
        public string RequirementCode { get; set; }
        public string NewFileName { get; set; }
    }
}

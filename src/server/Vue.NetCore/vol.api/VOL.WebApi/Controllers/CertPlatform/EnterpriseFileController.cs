using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using VOL.Builder.IServices.CertPlatform;
using VOL.Core.Controllers.Basic;
using VOL.Entity.AttributeManager;

namespace VOL.WebApi.Controllers.CertPlatform
{
    [Route("api/EnterpriseFile")]
    [PermissionTable(Name = "EnterpriseFile")]
    [ApiController]
    public class EnterpriseFileController : ApiBaseController<IEnterpriseFileService>
    {
        public EnterpriseFileController(IEnterpriseFileService service) : base(service) { }

        /// <summary>
        /// 上传企业文件
        /// </summary>
        [HttpPost, Route("upload")]
        public async Task<IActionResult> Upload([FromForm] FileUploadRequest request)
        {
            if (request.File == null || request.File.Length == 0)
                return Ok(new { status = false, message = "请选择文件" });

            using var stream = request.File.OpenReadStream();
            var result = await Service.UploadAsync(
                request.EnterpriseCode, request.FolderCode,
                request.StandardCode, request.PhaseCode, request.FolderPath,
                request.File.FileName, stream, request.File.Length);

            return Ok(result);
        }

        /// <summary>
        /// 获取目录树
        /// </summary>
        [HttpGet, Route("tree")]
        public async Task<IActionResult> GetTree([FromQuery] string enterpriseCode, [FromQuery] string phaseCode)
        {
            var tree = await Service.GetDocumentTreeAsync(enterpriseCode, phaseCode);
            return Ok(new { status = true, data = tree });
        }

        /// <summary>
        /// 获取文件列表
        /// </summary>
        [HttpGet, Route("list")]
        public async Task<IActionResult> GetFileList([FromQuery] string folderCode, [FromQuery] int page = 1, [FromQuery] int rows = 20)
        {
            var (items, total) = await Service.GetFileListAsync(folderCode, page, rows);
            return Ok(new { status = true, data = items, total });
        }

        /// <summary>
        /// 删除文件
        /// </summary>
        [HttpPost, Route("delete")]
        public async Task<IActionResult> Delete([FromBody] FileCodeRequest request)
        {
            var result = await Service.DeleteFileAsync(request.FileCode);
            return Ok(result);
        }

        /// <summary>
        /// 获取文件版本历史
        /// </summary>
        [HttpGet, Route("versions")]
        public async Task<IActionResult> GetVersions([FromQuery] string fileCode)
        {
            var versions = await Service.GetFileVersionsAsync(fileCode);
            return Ok(new { status = true, data = versions });
        }

        /// <summary>
        /// 触发文件转换
        /// </summary>
        [HttpPost, Route("convert")]
        public async Task<IActionResult> Convert([FromBody] FileCodeRequest request)
        {
            var result = await Service.TriggerConversionAsync(request.FileCode);
            return Ok(result);
        }
    }

    public class FileUploadRequest
    {
        public string EnterpriseCode { get; set; }
        public string FolderCode { get; set; }
        public string StandardCode { get; set; }
        public string PhaseCode { get; set; }
        public string FolderPath { get; set; }
        public IFormFile File { get; set; }
    }

    public class FileCodeRequest
    {
        public string FileCode { get; set; }
    }
}

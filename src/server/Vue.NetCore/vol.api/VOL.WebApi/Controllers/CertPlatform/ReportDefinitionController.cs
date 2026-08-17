using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VOL.Core.BaseProvider;
using VOL.Core.Extensions.AutofacManager;
using VOL.Entity.DomainModels;
using VOL.Builder.IServices.CertPlatform;
using VOL.Entity.CertPlatform.Cert;
using VOL.Entity.CertPlatform.Rpt;

namespace VOL.WebApi.Controllers.CertPlatform
{
    [Route("api/report-definition")]
    [Authorize]
    public class ReportDefinitionController : ControllerBase
    {
        private readonly IReportDefinitionService _service;

        public ReportDefinitionController(IReportDefinitionService service)
        {
            _service = service;
        }

        // ── 报告模板 ──

        [HttpPost("template/page")]
        public async Task<IActionResult> GetTemplatePage([FromBody] PageDataOptions options,
            [FromQuery] string orgCode = null,
            [FromQuery] string standardCode = null,
            [FromQuery] string phaseCode = null)
        {
            var result = await _service.GetPageDataAsync(options, orgCode, standardCode, phaseCode);
            return Ok(new { status = true, data = result });
        }

        [HttpGet("template/list")]
        public async Task<IActionResult> GetTemplateList([FromQuery] string orgCode, [FromQuery] string standardCode, [FromQuery] string phaseCode)
        {
            var list = await _service.GetByOrgStandardPhaseAsync(orgCode, standardCode, phaseCode);
            return Ok(new { status = true, data = list });
        }

        [HttpGet("template/{code}")]
        public async Task<IActionResult> GetTemplate(string code)
        {
            var entity = await _service.GetTemplateAsync(code);
            return Ok(new { status = true, data = entity });
        }

        [HttpGet("template/context")]
        public async Task<IActionResult> GetByContext([FromQuery] string orgCode, [FromQuery] string standardCode, [FromQuery] string phaseCode)
        {
            var entity = await _service.GetByContextAsync(orgCode, standardCode, phaseCode);
            return Ok(new { status = true, data = entity });
        }

        [HttpPost("template")]
        public async Task<IActionResult> SaveTemplate([FromBody] ReportTemplate entity)
        {
            var result = await _service.SaveTemplateAsync(entity);
            if (!result) return Ok(new { status = false, message = "保存失败" });
            // 返回保存后的完整实体（前端直接拿 id）
            var saved = await _service.GetByContextAsync(entity.OrgCode, entity.StandardCode, entity.PhaseCode);
            return Ok(new { status = true, message = "保存成功", data = saved });
        }

        [HttpPost("template/delete/{id}")]
        public async Task<IActionResult> DeleteTemplate(long id)
        {
            var result = await _service.DeleteTemplateAsync(id);
            return Ok(new { status = result, message = result ? "删除成功" : "删除失败" });
        }

        // ── 报告章节 ──

        [HttpGet("section/{reportCode}")]
        public async Task<IActionResult> GetSections(string reportCode)
        {
            var list = await _service.GetSectionsAsync(reportCode);
            return Ok(new { status = true, data = list });
        }

        [HttpPost("section")]
        public async Task<IActionResult> SaveSection([FromBody] ReportSection entity)
        {
            var result = await _service.SaveSectionAsync(entity);
            return Ok(new { status = result, message = result ? "保存成功" : "保存失败" });
        }

        [HttpPost("section/delete/{id}")]
        public async Task<IActionResult> DeleteSection(long id)
        {
            var result = await _service.DeleteSectionAsync(id);
            return Ok(new { status = result, message = result ? "删除成功" : "删除失败" });
        }

        [HttpPost("section/copy/{sourceId}")]
        public async Task<IActionResult> CopySection(long sourceId)
        {
            var copy = await _service.CopySectionAsync(sourceId);
            if (copy == null) return Ok(new { status = false, message = "源章节不存在" });
            return Ok(new { status = true, data = copy });
        }

        // ── 报告模板文件上传 ──

        [HttpPost("template/upload")]
        [RequestSizeLimit(100_000_000)]
        public async Task<IActionResult> UploadTemplateFile(
            [FromForm] IFormFile file,
            [FromForm] string orgCode,
            [FromForm] string standardCode,
            [FromForm] string phaseCode)
        {
            if (file == null || file.Length == 0)
                return Ok(new { status = false, message = "请选择文件" });
            if (string.IsNullOrWhiteSpace(orgCode) || string.IsNullOrWhiteSpace(standardCode) || string.IsNullOrWhiteSpace(phaseCode))
                return Ok(new { status = false, message = "机构、标准、阶段编码不能为空" });

            try
            {
                var minio = AutofacContainerModule.GetService<IMinIOHelper>();
                if (minio == null)
                    return Ok(new { status = false, message = "MinIO 服务未注册" });

                // 构建路径：report/机构code/标准code/阶段code/report/文件名
                var safeFileName = Path.GetFileName(file.FileName).Replace(" ", "_");
                var objectName = $"report/{orgCode}/{standardCode}/{phaseCode}/report/{safeFileName}";

                using var stream = file.OpenReadStream();
                await minio.UploadAsync(objectName, stream, file.Length, file.ContentType);

                return Ok(new { status = true, data = new { path = objectName, fileName = safeFileName, size = file.Length } });
            }
            catch (System.Exception ex)
            {
                return Ok(new { status = false, message = $"上传失败：{ex.Message}" });
            }
        }

        // ── 树形结构：机构→标准→阶段→检查项 ──

        [HttpGet("tree")]
        public async Task<IActionResult> GetTree([FromQuery] string orgCode)
        {
            var rulesSvc = AutofacContainerModule.GetService<IValidationRuleService>();
            List<ValidationRule> rules = new();
            if (!string.IsNullOrWhiteSpace(orgCode) && rulesSvc != null)
                rules = await rulesSvc.GetByOrgStandardPhaseAsync(orgCode, null, null);

            var ruleDict = new Dictionary<string, List<ValidationRule>>();
            foreach (var r in rules)
            {
                var key = $"{r.StandardCode}|{r.PhaseCode}";
                if (!ruleDict.TryGetValue(key, out var list))
                    ruleDict[key] = new List<ValidationRule>();
                list.Add(r);
            }

            return Ok(new { status = true, data = new { rules = ruleDict } });
        }
    }
}

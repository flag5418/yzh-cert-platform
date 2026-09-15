extern alias SharedEntities;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Api.Services;
using SharedEntities::CertPlatform.Shared.Entities.Cert;
using SharedEntities::CertPlatform.Shared.Entities.Rpt;

namespace CertPlatform.Admin.Controllers.Workflow
{
    /// <summary>
    /// 报告定义控制器 — 模板 + 章节 CRUD + 文件上传
    /// <para>路由前缀：/api/ReportDefinition</para>
    /// </summary>
    [Route("api/[controller]")]
    public class ReportDefinitionController : ControllerBase
    {
        private readonly EntityService<ReportTemplate> _templateEntity;
        private readonly EntityService<ReportSection> _sectionEntity;
        private readonly IObjectStorage _storage;
        private readonly IUserContext _userContext;

        public ReportDefinitionController(
            EntityService<ReportTemplate> templateEntity,
            EntityService<ReportSection> sectionEntity,
            IObjectStorage storage,
            IUserContext userContext)
        {
            _templateEntity = templateEntity;
            _sectionEntity = sectionEntity;
            _storage = storage;
            _userContext = userContext;
        }

        #region 模板 CRUD

        /// <summary>
        /// 按上下文查询模板（主读接口）
        /// </summary>
        [HttpGet("template/context")]
        public async Task<IActionResult> GetTemplateByContext(
            [FromQuery] string orgCode,
            [FromQuery] string standardCode,
            [FromQuery] string phaseCode)
        {
            var result = await _templateEntity.GetOne(x =>
                x.OrgCode == orgCode &&
                x.StandardCode == standardCode &&
                x.PhaseCode == phaseCode &&
                x.IsValid == 1);

            return Ok(new { code = 200, data = result.Data });
        }

        /// <summary>
        /// 创建/更新模板（upsert：同 org+std+phase 只保留一条）
        /// </summary>
        [HttpPost("template/save")]
        public async Task<IActionResult> SaveTemplate([FromBody] ReportTemplate entity)
        {
            if (string.IsNullOrWhiteSpace(entity.TemplateName))
                return Ok(new { code = 400, message = "模板名称不能为空" });

            if (entity.Id > 0)
            {
                // 更新
                var existing = await _templateEntity.GetOne(x => x.Id == entity.Id);
                if (existing.Data == null)
                    return Ok(new { code = 404, message = "模板不存在" });

                var target = existing.Data;
                target.TemplateName = entity.TemplateName;
                target.TemplateFilePath = entity.TemplateFilePath;
                target.Remark = entity.Remark;
                target.IsDefault = entity.IsDefault;
                target.ModifyDate = DateTime.Now;

                var updateResult = await _templateEntity.Update(target);
                return Ok(new { code = updateResult.Success ? 200 : 500, data = target, message = updateResult.Error });
            }
            else
            {
                // 创建 — 先检查是否已存在
                var existing = await _templateEntity.GetOne(x =>
                    x.OrgCode == entity.OrgCode &&
                    x.StandardCode == entity.StandardCode &&
                    x.PhaseCode == entity.PhaseCode &&
                    x.IsValid == 1);

                if (existing.Data != null)
                {
                    // 已存在 → 更新
                    var target = existing.Data;
                    target.TemplateName = entity.TemplateName;
                    target.TemplateFilePath = entity.TemplateFilePath;
                    target.Remark = entity.Remark;
                    target.IsDefault = entity.IsDefault;
                    target.ModifyDate = DateTime.Now;

                    var updateResult = await _templateEntity.Update(target);
                    return Ok(new { code = updateResult.Success ? 200 : 500, data = target, message = updateResult.Error });
                }

                // 新建
                entity.Code = Guid.NewGuid().ToString("N");
                entity.CbCode = entity.OrgCode;  // 同步设置 CbCode（外键约束）
                entity.CreateBy = _userContext.UserCode;
                entity.IsValid = 1;

                var addResult = await _templateEntity.Insert(entity);
                return Ok(new { code = addResult.Success ? 200 : 500, data = entity, message = addResult.Error });
            }
        }

        /// <summary>
        /// 上传模板文件到 MinIO
        /// </summary>
        [HttpPost("template/upload")]
        [RequestSizeLimit(100_000_000)]
        public async Task<IActionResult> UploadTemplateFile(
            [FromForm] IFormFile file,
            [FromQuery] string orgCode,
            [FromQuery] string standardCode,
            [FromQuery] string phaseCode)
        {
            if (file == null || file.Length == 0)
                return Ok(new { code = 400, message = "请选择文件" });

            if (string.IsNullOrEmpty(orgCode) || string.IsNullOrEmpty(standardCode) || string.IsNullOrEmpty(phaseCode))
                return Ok(new { code = 400, message = "缺少上下文参数" });

            // 安全校验：上下文参数仅允许字母数字-_，防止对象键注入（防御性双保险，
            // MinIO 对象键无文件系统穿越风险，但防串改 bucket 内其他模块对象）
            if (!IsValidContextSegment(orgCode) || !IsValidContextSegment(standardCode) || !IsValidContextSegment(phaseCode))
                return Ok(new { code = 400, message = "上下文参数含非法字符" });

            var allowedExts = new[] { ".docx", ".xlsx", ".pdf", ".doc", ".xls" };
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExts.Contains(ext))
                return Ok(new { code = 400, message = "仅支持 .docx / .xlsx / .pdf 格式" });

            var safeFileName = file.FileName.Replace(" ", "_");
            var objectName = $"report/{orgCode}/{standardCode}/{phaseCode}/{safeFileName}";

            using var stream = file.OpenReadStream();
            await _storage.UploadAsync(objectName, stream, file.Length, file.ContentType);

            return Ok(new { code = 200, data = new { path = objectName, fileName = file.FileName, size = file.Length } });
        }

        /// <summary>
        /// 删除模板（级联删除章节）
        /// </summary>
        [HttpPost("template/delete")]
        public async Task<IActionResult> DeleteTemplate([FromQuery] long id)
        {
            var existing = await _templateEntity.GetOne(x => x.Id == id);
            if (existing.Data == null)
                return Ok(new { code = 404, message = "模板不存在" });

            var template = existing.Data;

            // 级联删除章节
            var sections = await _sectionEntity.GetListAsync(x => x.ReportCode == template.Code);
            if (sections.Data != null && sections.Data.Count > 0)
            {
                var sectionCodes = sections.Data.Select(s => s.Code).ToList();
                await _sectionEntity.DeleteBatch(sectionCodes, hardDelete: true);
            }

            // 硬删除模板
            var deleteResult = await _templateEntity.DeleteByCode(template.Code);
            return Ok(new { code = deleteResult.Success ? 200 : 500, message = deleteResult.Error });
        }

        #endregion

        #region 章节 CRUD

        /// <summary>
        /// 按模板 Code 查询章节列表
        /// </summary>
        [HttpGet("section/list")]
        public async Task<IActionResult> GetSectionList([FromQuery] string reportCode)
        {
            var result = await _sectionEntity.GetListAsync(x => x.ReportCode == reportCode);
            var sorted = (result.Data ?? new List<ReportSection>()).OrderBy(x => x.SortOrder).ToList();
            return Ok(new { code = 200, data = sorted });
        }

        /// <summary>
        /// 创建/更新章节
        /// </summary>
        [HttpPost("section/save")]
        public async Task<IActionResult> SaveSection([FromBody] ReportSection entity)
        {
            if (string.IsNullOrWhiteSpace(entity.SectionName))
                return Ok(new { code = 400, message = "章节名称不能为空" });

            if (string.IsNullOrWhiteSpace(entity.ReportCode))
                return Ok(new { code = 400, message = "缺少报告编码" });

            if (entity.Id > 0)
            {
                // 更新
                var existing = await _sectionEntity.GetOne(x => x.Id == entity.Id);
                if (existing.Data == null)
                    return Ok(new { code = 404, message = "章节不存在" });

                var target = existing.Data;
                target.SectionName = entity.SectionName;
                target.SectionNameEn = entity.SectionNameEn;
                target.Content = entity.Content;
                target.SortOrder = entity.SortOrder;
                target.IsActive = entity.IsActive;
                target.WorkflowCode = entity.WorkflowCode;
                target.WorkflowConfig = entity.WorkflowConfig;
                target.LayoutJson = entity.LayoutJson;
                target.ClauseCode = entity.ClauseCode;
                target.SectionJson = entity.SectionJson;
                target.Remark = entity.Remark;
                target.ModifyDate = DateTime.Now;

                var updateResult = await _sectionEntity.Update(target);
                return Ok(new { code = updateResult.Success ? 200 : 500, data = target, message = updateResult.Error });
            }
            else
            {
                // 新建
                entity.Code = Guid.NewGuid().ToString("N");
                entity.CreateBy = _userContext.UserCode;

                var addResult = await _sectionEntity.Insert(entity);
                return Ok(new { code = addResult.Success ? 200 : 500, data = entity, message = addResult.Error });
            }
        }

        /// <summary>
        /// 删除章节
        /// </summary>
        [HttpPost("section/delete")]
        public async Task<IActionResult> DeleteSection([FromQuery] long id)
        {
            var existing = await _sectionEntity.GetOne(x => x.Id == id);
            if (existing.Data == null)
                return Ok(new { code = 404, message = "章节不存在" });

            var result = await _sectionEntity.DeleteByCode(existing.Data.Code);
            return Ok(new { code = result.Success ? 200 : 500, message = result.Error });
        }

        #endregion

        /// <summary>上下文段合法性：字母/数字/连字符/下划线，长度 1-64</summary>
        private static bool IsValidContextSegment(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 64) return false;
            return value.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_');
        }
    }
}

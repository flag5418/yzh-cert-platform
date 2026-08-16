using System.Collections.Generic;
using System.Threading.Tasks;
using VOL.Core.BaseProvider;
using VOL.Core.Extensions.AutofacManager;
using VOL.Entity.CertPlatform.Wf;

namespace VOL.WebApi.Controllers.CertPlatform
{
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using VOL.Builder.IServices.CertPlatform;
    using VOL.Entity.DomainModels;

    [Route("api/workflow-definition")]
    [Authorize]
    public class WorkflowDefinitionController : ControllerBase
    {
        private readonly IWorkflowDefinitionService _service;

        public WorkflowDefinitionController(IWorkflowDefinitionService service)
        {
            _service = service;
        }

        [HttpPost("page")]
        public async Task<IActionResult> GetPage([FromBody] PageDataOptions options,
            [FromQuery] string workflowType = null,
            [FromQuery] bool? isActive = null)
        {
            var result = await _service.GetPageDataAsync(options, workflowType, isActive);
            return Ok(new { status = true, data = result });
        }

        [HttpGet("list")]
        public async Task<IActionResult> GetList([FromQuery] string workflowType = null, [FromQuery] bool? isActive = null)
        {
            var list = await _service.GetListAsync(workflowType, isActive);
            return Ok(new { status = true, data = list });
        }

        [HttpGet("{workflowCode}")]
        public async Task<IActionResult> Get(string workflowCode)
        {
            var entity = await _service.GetByCodeAsync(workflowCode);
            if (entity == null) return NotFound(new { status = false, message = "工作流不存在" });
            return Ok(new { status = true, data = entity });
        }

        [HttpPost]
        public async Task<IActionResult> Save([FromBody] WorkflowDefinition entity)
        {
            var result = await _service.SaveAsync(entity);
            return Ok(new { status = result, message = result ? "保存成功" : "保存失败" });
        }

        [HttpPost("delete/{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _service.DeleteAsync(id);
            return Ok(new { status = result, message = result ? "删除成功" : "删除失败" });
        }

        [HttpPost("toggle-active/{id}")]
        public async Task<IActionResult> ToggleActive(long id)
        {
            var result = await _service.ToggleActiveAsync(id);
            return Ok(new { status = result, message = result ? "操作成功" : "操作失败" });
        }
    }
}

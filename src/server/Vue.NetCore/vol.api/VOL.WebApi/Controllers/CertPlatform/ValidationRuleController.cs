using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VOL.Core.BaseProvider;
using VOL.Core.ManageUser;
using VOL.Entity.DomainModels;
using VOL.Builder.IServices.CertPlatform;
using VOL.Entity.CertPlatform.Cert;

namespace VOL.WebApi.Controllers.CertPlatform
{
    [Route("api/validation-rule")]
    [Authorize]
    public class ValidationRuleController : ControllerBase
    {
        private readonly IValidationRuleService _service;

        public ValidationRuleController(IValidationRuleService service)
        {
            _service = service;
        }

        [HttpPost("page")]
        public async Task<IActionResult> GetPage([FromBody] PageDataOptions options,
            [FromQuery] string orgCode = null,
            [FromQuery] string standardCode = null,
            [FromQuery] string phaseCode = null)
        {
            var result = await _service.GetPageDataAsync(options, orgCode, standardCode, phaseCode);
            return Ok(new { status = true, data = result });
        }

        [HttpGet("list")]
        public async Task<IActionResult> GetList([FromQuery] string orgCode, [FromQuery] string standardCode, [FromQuery] string phaseCode)
        {
            var list = await _service.GetByOrgStandardPhaseAsync(orgCode, standardCode, phaseCode);
            return Ok(new { status = true, data = list });
        }

        [HttpGet("{ruleCode}")]
        public async Task<IActionResult> Get(string ruleCode)
        {
            var entity = await _service.GetByRuleCodeAsync(ruleCode);
            return Ok(new { status = true, data = entity });
        }

        [HttpPost]
        public async Task<IActionResult> Save([FromBody] ValidationRule entity)
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

        [HttpPost("copy/{sourceRuleCode}")]
        public async Task<IActionResult> Copy(string sourceRuleCode)
        {
            var copy = await _service.CopyAsync(sourceRuleCode);
            if (copy == null) return Ok(new { status = false, message = "源规则不存在" });
            return Ok(new { status = true, data = copy });
        }
    }
}

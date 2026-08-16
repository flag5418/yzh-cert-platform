using Microsoft.AspNetCore.Mvc;
using VOL.Builder.IServices.CertPlatform;
using VOL.Entity.CertPlatform.Wf;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace VOL.WebApi.Controllers.CertPlatform
{
    /// <summary>
    /// Workflow Skill 管理接口
    /// </summary>
    [ApiController]
    [Route("api/wf-skill")]
    public class WfSkillController : ControllerBase
    {
        private readonly ISkillService _skillService;

        public WfSkillController(ISkillService skillService)
        {
            _skillService = skillService;
        }

        [HttpGet("list")]
        public async Task<IActionResult> List()
        {
            var skills = await _skillService.GetAllAsync();
            return Ok(new { status = true, data = skills });
        }

        [HttpGet("{skillCode}")]
        public async Task<IActionResult> Get(string skillCode)
        {
            var skill = await _skillService.GetByCodeAsync(skillCode);
            if (skill == null)
                return NotFound(new { status = false, message = "Skill not found" });
            return Ok(new { status = true, data = skill });
        }
    }
}

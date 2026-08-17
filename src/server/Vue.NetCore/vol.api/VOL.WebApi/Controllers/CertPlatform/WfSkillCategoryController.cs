using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VOL.Entity.CertPlatform.Wf;
using VOL.Builder.IServices.CertPlatform;

namespace VOL.WebApi.Controllers.CertPlatform
{
    /// <summary>
    /// Skill 分类管理（基础资料，页面左侧导航）
    /// </summary>
    [Route("api/skill-category")]
    [Authorize]
    public class WfSkillCategoryController : ControllerBase
    {
        private readonly IWfSkillCategoryService _service;

        public WfSkillCategoryController(IWfSkillCategoryService service)
        {
            _service = service;
        }

        [HttpGet("list")]
        public async Task<IActionResult> GetList()
        {
            var list = await _service.GetListAsync();
            return Ok(new { status = true, data = list });
        }

        [HttpPost]
        public async Task<IActionResult> Save([FromBody] WfSkillCategory entity)
        {
            var (ok, message) = await _service.SaveAsync(entity);
            return Ok(new { status = ok, message });
        }

        [HttpPost("delete/{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var (ok, message) = await _service.DeleteAsync(id);
            return Ok(new { status = ok, message });
        }

        [HttpPost("toggle-active/{id}")]
        public async Task<IActionResult> ToggleActive(long id)
        {
            var result = await _service.ToggleActiveAsync(id);
            return Ok(new { status = result, message = result ? "操作成功" : "操作失败" });
        }
    }
}

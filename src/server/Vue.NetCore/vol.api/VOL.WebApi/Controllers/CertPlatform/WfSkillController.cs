using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VOL.Core.BaseProvider;
using VOL.Entity.CertPlatform.Wf;
using VOL.Entity.DomainModels;
using VOL.Builder.IServices.CertPlatform;

namespace VOL.WebApi.Controllers.CertPlatform
{
    /// <summary>
    /// Skill 管理（自定义工作流引擎 V1.2 §6.1）
    /// 供 Skill 配置页维护，list-active 供工作流配置面板引用
    /// </summary>
    [Route("api/skill")]
    [Authorize]
    public class WfSkillController : ControllerBase
    {
        private readonly IWfSkillService _service;

        public WfSkillController(IWfSkillService service)
        {
            _service = service;
        }

        [HttpPost("page")]
        public async Task<IActionResult> GetPage([FromBody] PageDataOptions options,
            [FromQuery] string keyword = null,
            [FromQuery] string category = null)
        {
            var result = await _service.GetPageDataAsync(options, keyword, category);
            return Ok(new { status = true, data = result });
        }

        [HttpGet("{skillCode}")]
        public async Task<IActionResult> GetDetail(string skillCode)
        {
            var detail = await _service.GetDetailAsync(skillCode);
            if (detail == null) return Ok(new { status = false, message = "Skill 不存在" });
            return Ok(new { status = true, data = detail });
        }

        [HttpPost]
        public async Task<IActionResult> Save([FromBody] SkillDetailDto dto)
        {
            var (ok, message) = await _service.SaveAsync(dto);
            return Ok(new { status = ok, message });
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

        /// <summary>启用 Skill 完整描述（含输入模板/输出契约），供工作流配置面板引用</summary>
        [HttpGet("list-active")]
        public async Task<IActionResult> GetActiveSkills()
        {
            var list = await _service.GetActiveSkillsAsync();
            return Ok(new { status = true, data = list });
        }
    }
}

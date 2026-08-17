using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VOL.Builder.IServices.CertPlatform;
using VOL.Entity.CertPlatform.Cert;

namespace VOL.WebApi.Controllers.CertPlatform
{
    [Route("api/iso-clause")]
    [Authorize]
    public class ISOClauseController : ControllerBase
    {
        private readonly IISOClauseService _service;

        public ISOClauseController(IISOClauseService service)
        {
            _service = service;
        }

        /// <summary>
        /// 获取所有 ISO 标准列表（供标准条款页面选择）
        /// </summary>
        [HttpGet("standards")]
        public async Task<IActionResult> GetStandards()
        {
            var standards = await _service.GetStandardsAsync();
            return Ok(new { status = true, data = standards });
        }

        /// <summary>
        /// 获取指定标准的条款树形数据
        /// </summary>
        [HttpGet("tree")]
        public async Task<IActionResult> GetTree([FromQuery] string standardCode)
        {
            if (string.IsNullOrWhiteSpace(standardCode))
                return Ok(new { status = false, message = "standardCode 不能为空" });
            var tree = await _service.GetClauseTreeAsync(standardCode);
            return Ok(new { status = true, data = tree });
        }

        /// <summary>
        /// 获取条款列表（平铺）
        /// </summary>
        [HttpGet("list")]
        public async Task<IActionResult> GetList([FromQuery] string standardCode)
        {
            var list = await _service.GetListAsync(standardCode);
            return Ok(new { status = true, data = list });
        }

        /// <summary>
        /// 保存条款（新增/编辑）
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Save([FromBody] ISOClause entity)
        {
            var result = await _service.SaveAsync(entity);
            return Ok(new { status = result, message = result ? "保存成功" : "保存失败" });
        }

        /// <summary>
        /// 删除条款（软删除，有子条款时不允许删除）
        /// </summary>
        [HttpPost("delete/{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _service.DeleteAsync(id);
            if (!result)
                return Ok(new { status = false, message = "删除失败：条款不存在或存在子条款" });
            return Ok(new { status = true, message = "删除成功" });
        }
    }
}

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VOL.Builder.IServices.CertPlatform;
using VOL.Core.Extensions;
using VOL.Core.ManageUser;

namespace VOL.WebApi.Controllers.CertPlatform
{
    [Route("api/sys-config")]
    [Authorize]
    public class SysConfigController : ControllerBase
    {
        private readonly ISysConfigService _configService;

        public SysConfigController(ISysConfigService configService)
        {
            _configService = configService;
        }

        [HttpGet("list")]
        public async Task<IActionResult> GetList([FromQuery] string category)
        {
            var list = await _configService.GetByCategoryAsync(category ?? "");
            return new JsonResult(new { status = true, data = list });
        }

        [HttpGet("value/{configKey}")]
        public IActionResult GetValue(string configKey)
        {
            var value = _configService.Get(configKey);
            return new JsonResult(new { status = true, data = value });
        }

        [HttpPost("update")]
        public async Task<IActionResult> Update([FromBody] CertConfigUpdateDto dto)
        {
            try
            {
                await _configService.SetAsync(dto.ConfigKey, dto.ConfigValue);
                return new JsonResult(new { status = true, message = "更新成功" });
            }
            catch (System.Exception ex)
            {
                return new JsonResult(new { status = false, message = ex.Message });
            }
        }

        [HttpPost("update-batch")]
        public async Task<IActionResult> UpdateBatch([FromBody] List<CertConfigUpdateDto> configs)
        {
            try
            {
                await _configService.UpdateBatchAsync(configs);
                return new JsonResult(new { status = true, message = "批量更新成功" });
            }
            catch (System.Exception ex)
            {
                return new JsonResult(new { status = false, message = ex.Message });
            }
        }
    }
}

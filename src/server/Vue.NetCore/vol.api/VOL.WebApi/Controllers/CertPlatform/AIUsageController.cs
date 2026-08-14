using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VOL.Builder.IServices.CertPlatform;
using VOL.Core.ManageUser;
using VOL.Entity.CertPlatform.DocExtraction;

namespace VOL.WebApi.Controllers.CertPlatform
{
    [Route("api/ai-usage")]
    [Authorize]
    public class AIUsageController : ControllerBase
    {
        private readonly IAIUsageLogService _usageService;
        private readonly ISysConfigService _configService;

        public AIUsageController(IAIUsageLogService usageService, ISysConfigService configService)
        {
            _usageService = usageService;
            _configService = configService;
        }

        [HttpGet("balance")]
        public async Task<IActionResult> GetBalance()
        {
            var info = await _usageService.GetBalanceInfoAsync();
            return Ok(new { status = true, data = info });
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
        {
            var summary = await _usageService.GetSummaryAsync();
            return Ok(new { status = true, data = summary });
        }

        [HttpGet("daily-costs")]
        public async Task<IActionResult> GetDailyCosts(
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            var start = startDate ?? DateTime.Now.AddDays(-30);
            var end = endDate ?? DateTime.Now;
            var costs = await _usageService.GetDailyCostsAsync(start, end);
            return Ok(new { status = true, data = costs });
        }

        [HttpGet("calls")]
        public async Task<IActionResult> GetCalls(
            int page = 1,
            int pageSize = 20,
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            var start = startDate;
            var end = endDate?.AddDays(1).AddSeconds(-1);
            var total = await _usageService.GetTotalCountAsync(start, end);
            var calls = await _usageService.GetRecentCallsAsync(page, pageSize, start, end);
            return Ok(new { status = true, data = calls, total });
        }

        [HttpGet("aliyun-status")]
        public IActionResult GetAliyunStatus()
        {
            var keyId = _configService.Get("aliyun_access_key_id");
            var hasKey = !string.IsNullOrWhiteSpace(keyId);
            return Ok(new { status = true, data = new { configured = hasKey } });
        }
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using VOL.Builder.Services.CertPlatform;
using VOL.Entity.CertPlatform.Cert;
using VOL.Builder.IServices.CertPlatform;

namespace VOL.WebApi.Controllers.CertPlatform
{
    [Route("api/cert-platform")]
    [Authorize]
    public class CertPlatformController : ControllerBase
    {
        private readonly ICertPlatformTreeService _treeService;

        public CertPlatformController(ICertPlatformTreeService treeService)
        {
            _treeService = treeService;
        }

        [HttpGet("tree")]
        public async Task<IActionResult> GetTree()
        {
            var tree = await _treeService.GetOrgStandardPhaseTreeAsync();
            return Ok(new { status = true, data = tree });
        }

        [HttpGet("orgs")]
        public async Task<IActionResult> GetOrgs()
        {
            var list = await _treeService.GetOrgsAsync();
            return Ok(new { status = true, data = list });
        }

        [HttpGet("standards")]
        public async Task<IActionResult> GetStandards([FromQuery] string orgCode = null)
        {
            var list = await _treeService.GetStandardsAsync(orgCode);
            return Ok(new { status = true, data = list });
        }

        [HttpGet("phases")]
        public async Task<IActionResult> GetPhases()
        {
            var list = await _treeService.GetPhasesAsync();
            return Ok(new { status = true, data = list });
        }
    }
}

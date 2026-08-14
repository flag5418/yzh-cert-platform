using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using VOL.Builder.IServices.CertPlatform;
using VOL.Core.Controllers.Basic;
using VOL.Entity.AttributeManager;

namespace VOL.WebApi.Controllers.CertPlatform
{
    [Route("api/Auditor")]
    [PermissionTable(Name = "Auditor")]
    [ApiController]
    public class AuditorController : ApiBaseController<IAuditorService>
    {
        public AuditorController(IAuditorService service) : base(service) { }

        /// <summary>
        /// 发送验证码
        /// </summary>
        [HttpPost, Route("sendSmsCode")]
        public async Task<IActionResult> SendSmsCode([FromBody] SendSmsRequest request)
        {
            var result = await Service.SendSmsCodeAsync(request.Phone);
            return Ok(result);
        }

        /// <summary>
        /// 审核员注册
        /// </summary>
        [HttpPost, Route("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            var result = await Service.RegisterAsync(request.Phone, request.SmsCode, request.AuditorName, request.OrgCode);
            return Ok(result);
        }

        /// <summary>
        /// 获取审核员列表
        /// </summary>
        [HttpGet, Route("list")]
        public async Task<IActionResult> GetList([FromQuery] string orgCode, [FromQuery] int page = 1, [FromQuery] int rows = 20)
        {
            var (items, total) = await Service.GetListAsync(orgCode, page, rows);
            return Ok(new { status = true, data = items, total });
        }

        /// <summary>
        /// 获取审核员详情
        /// </summary>
        [HttpGet, Route("detail")]
        public async Task<IActionResult> GetDetail([FromQuery] long userId)
        {
            var profile = await Service.GetDetailAsync(userId);
            return Ok(new { status = true, data = profile });
        }

        /// <summary>
        /// 更新审核员资质
        /// </summary>
        [HttpPost, Route("updateProfile")]
        public async Task<IActionResult> UpdateProfile([FromBody] VOL.Entity.CertPlatform.Cert.AuditorProfile profile)
        {
            var result = await Service.UpdateProfileAsync(profile);
            return Ok(result);
        }
    }

    public class SendSmsRequest
    {
        public string Phone { get; set; }
    }

    public class RegisterRequest
    {
        public string Phone { get; set; }
        public string SmsCode { get; set; }
        public string AuditorName { get; set; }
        public string OrgCode { get; set; }
    }
}

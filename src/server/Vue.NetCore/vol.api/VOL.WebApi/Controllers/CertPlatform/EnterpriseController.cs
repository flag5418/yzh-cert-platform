using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using VOL.Builder.IServices.CertPlatform;
using VOL.Core.Controllers.Basic;
using VOL.Entity.AttributeManager;

namespace VOL.WebApi.Controllers.CertPlatform
{
    [Route("api/Enterprise")]
    [PermissionTable(Name = "Enterprise")]
    [ApiController]
    public class EnterpriseController : ApiBaseController<IEnterpriseService>
    {
        public EnterpriseController(IEnterpriseService service) : base(service) { }

        /// <summary>
        /// 获取企业列表
        /// </summary>
        [HttpPost, Route("getList")]
        public async Task<IActionResult> GetList([FromBody] EnterpriseListRequest request)
        {
            var (items, total) = await Service.GetListAsync(request.OrgCode, request.Page, request.Rows);
            return Ok(new { status = true, data = items, total });
        }

        /// <summary>
        /// 获取企业详情
        /// </summary>
        [HttpGet, Route("detail")]
        public async Task<IActionResult> GetDetail([FromQuery] string code)
        {
            var entity = await Service.GetDetailAsync(code);
            return Ok(new { status = true, data = entity });
        }

        /// <summary>
        /// 创建企业
        /// </summary>
        [HttpPost, Route("create")]
        public async Task<IActionResult> Create([FromBody] VOL.Entity.CertPlatform.Ent.Enterprise entity)
        {
            var result = await Service.CreateAsync(entity);
            return Ok(result);
        }

        /// <summary>
        /// 更新企业
        /// </summary>
        [HttpPost, Route("update")]
        public async Task<IActionResult> Update([FromBody] VOL.Entity.CertPlatform.Ent.Enterprise entity)
        {
            var result = await Service.UpdateAsync(entity);
            return Ok(result);
        }

        /// <summary>
        /// 删除企业
        /// </summary>
        [HttpPost, Route("delete")]
        public async Task<IActionResult> Delete([FromBody] DeleteRequest request)
        {
            var result = await Service.DeleteAsync(request.Code);
            return Ok(result);
        }
    }

    public class EnterpriseListRequest
    {
        public string OrgCode { get; set; }
        public int Page { get; set; } = 1;
        public int Rows { get; set; } = 20;
    }

    public class DeleteRequest
    {
        public string Code { get; set; }
    }
}

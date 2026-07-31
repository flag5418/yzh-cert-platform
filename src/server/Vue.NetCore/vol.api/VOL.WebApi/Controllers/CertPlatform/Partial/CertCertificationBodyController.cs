/*
 *接口编写处...
 *如果接口需要做Action的权限验证，请在Action上使用属性
 *如: [ApiActionPermission("CertCertificationBody", Enums.ActionPermissionOptions.Search)]
 */
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using VOL.Entity.CertPlatform.Cert;
using VOL.Builder.IServices.CertPlatform;
using VOL.Core.Filters;

namespace VOL.WebApi.Controllers.CertPlatform
{
    public partial class CertCertificationBodyController
    {
        private readonly ICertCertificationBodyService _service;
        private readonly IHttpContextAccessor _httpContextAccessor;

        [ActivatorUtilitiesConstructor]
        public CertCertificationBodyController(
            ICertCertificationBodyService service,
            IHttpContextAccessor httpContextAccessor
        )
        : base(service)
        {
            _service = service;
            _httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// 获取所有启用的认证机构列表（用于下拉选择）
        /// TODO: Phase 2 实现具体业务逻辑时启用
        /// </summary>
        /*
        [HttpPost("GetActiveList")]
        [ApiActionPermission()]
        public async Task<IActionResult> GetActiveList()
        {
            try
            {
                var data = await _service.GetActiveListAsync();
                return JsonNormal(data);
            }
            catch (Exception ex)
            {
                return JsonNormal(new { error = ex.Message });
            }
        }
        */

        /// <summary>
        /// 根据 ID 获取单个认证机构详情
        /// TODO: Phase 2 实现具体业务逻辑时启用
        /// </summary>
        /*
        [HttpPost("GetById")]
        [ApiActionPermission()]
        public async Task<IActionResult> GetById([FromBody] long id)
        {
            try
            {
                var data = await _service.FindFirstAsync(x => x.Id == id);
                return JsonNormal(data);
            }
            catch (Exception ex)
            {
                return JsonNormal(new { error = ex.Message });
            }
        }
        */
    }
}

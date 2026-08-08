/*
 * 机构-标准 / 机构-阶段 关联管理 API
 *
 * 职责：
 *   1. SyncOrgStandards — 批量同步机构-标准关联（勾选即保存）
 *   2. SyncOrgStages — 批量同步机构-阶段关联（勾选即保存）
 *   3. GetOrgStdIds — 查询某机构已关联的标准 ID 列表
 *   4. GetOrgStageIds — 查询某机构已关联的阶段 ID 列表
 *
 * 调用方式（前端 YzhTreeCheckboxTable）：
 *   - 切换树节点时调用 Get*Ids 加载已勾选状态
 *   - 用户勾选/取消 checkbox 时调用 Sync* 实时保存
 *
 * 设计原则（2026-08-08 确认）：
 * - 使用 Service 层处理业务逻辑
 * - Controller 只负责请求参数校验和响应格式化
 */
using Microsoft.AspNetCore.Mvc;
using VOL.Core.Controllers.Basic;
using VOL.Core.Utilities;
using VOL.Core.Filters;
using VOL.Builder.IServices.CertPlatform;
using Microsoft.Extensions.DependencyInjection;

namespace VOL.WebApi.Controllers.CertPlatform
{
    [Route("api/org-link")]
    [ApiController]
    [JWTAuthorize]
    public class OrgLinkController : ApiBaseController<object>
    {
        private readonly IOrgLinkService _service;

        [ActivatorUtilitiesConstructor]
        public OrgLinkController(IOrgLinkService service)
        : base(service)
        {
            _service = service;
        }

        // ============================================================
        // 机构-标准 关联 API
        // ============================================================

        [HttpPost("SyncOrgStandards")]
        public IActionResult SyncOrgStandards([FromBody] SyncLinkRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.CbCode))
                return JsonNormal(new WebResponseContent().Error("缺少机构编码"));

            var result = _service.SyncOrgStandards(request.CbCode, request.AddStdIds, request.RemoveStdIds);
            return JsonNormal(result);
        }

        [HttpGet("GetOrgStdIds/{cbCode}")]
        public IActionResult GetOrgStdIds(string cbCode)
        {
            if (string.IsNullOrEmpty(cbCode))
                return JsonNormal(new WebResponseContent().Error("缺少机构编码"));

            var result = _service.GetOrgStdIds(cbCode);
            return JsonNormal(result);
        }

        // ============================================================
        // 机构-阶段 关联 API
        // ============================================================

        [HttpPost("SyncOrgStages")]
        public IActionResult SyncOrgStages([FromBody] SyncLinkRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.CbCode))
                return JsonNormal(new WebResponseContent().Error("缺少机构编码"));

            var result = _service.SyncOrgStages(request.CbCode, request.AddStageIds, request.RemoveStageIds);
            return JsonNormal(result);
        }

        [HttpGet("GetOrgStageIds/{cbCode}")]
        public IActionResult GetOrgStageIds(string cbCode)
        {
            if (string.IsNullOrEmpty(cbCode))
                return JsonNormal(new WebResponseContent().Error("缺少机构编码"));

            var result = _service.GetOrgStageIds(cbCode);
            return JsonNormal(result);
        }
    }

    // ============================================================
    // 请求 DTO
    // ============================================================

    public class SyncLinkRequest
    {
        public string CbCode { get; set; }
        public long[] AddStdIds { get; set; }
        public long[] RemoveStdIds { get; set; }
        public long[] AddStageIds { get; set; }
        public long[] RemoveStageIds { get; set; }
    }
}

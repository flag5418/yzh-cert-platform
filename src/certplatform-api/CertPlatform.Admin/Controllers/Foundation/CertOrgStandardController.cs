extern alias SharedEntities;

using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Services;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Result;

using CB = SharedEntities::YZH.Entity.Admin.Platform.Cert.CertificationBody;
using ISO = SharedEntities::YZH.Entity.Admin.Platform.Cert.ISOStandard;
using Link = SharedEntities::YZH.Entity.Admin.Platform.Sys.CertOrgStandard;

namespace CertPlatform.Admin.Controllers.Foundation;

/// <summary>
/// 机构-标准关联管理控制器（勾选分配模式）
///
/// <para>路由前缀：/api/Foundation/CertOrgStandard</para>
/// <para>左树：认证机构（cert_certification_body，扁平）</para>
/// <para>右表：所有有效 ISO 标准（cert_iso_standard），带 Linked 标记</para>
/// <para>交互：勾选 = 创建关联，取消 = 删除关联，无弹窗</para>
/// </summary>
[ApiController]
[Route("api/Foundation/[controller]")]
public class CertOrgStandardController : ControllerBase
{
    private readonly EntityService<CB> _cbService;
    private readonly EntityService<ISO> _isoService;
    private readonly EntityService<Link> _linkService;
    private readonly IUserContext _userContext;

    public CertOrgStandardController(
        EntityService<CB> cbService,
        EntityService<ISO> isoService,
        EntityService<Link> linkService,
        IUserContext userContext)
    {
        _cbService = cbService;
        _isoService = isoService;
        _linkService = linkService;
        _userContext = userContext;
    }

    // ========================================================
    // 左树：所有有效认证机构
    // ========================================================

    /// <summary>
    /// 获取根节点（所有有效认证机构）
    /// </summary>
    [HttpPost("tree/root")]
    public async Task<ActionResult<ApiResponse<object?>>> TreeRoot()
    {
        try
        {
            var list = await _cbService.GetListAsync(p => p.IsValid == 1 && !p.IsDeleted);
            if (!list.Success)
                return BadRequest(ApiResponse.Fail(list.Error));

            var nodes = list.Data!.Select(p => new
            {
                Code = p.Code,
                Name = p.Name,
                ParentCode = (string?)null,
                Level = 1,
                IsLeaf = true,
                Extra = new { }
            }).ToList();

            return Ok(ApiResponse<object?>.Ok(nodes));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    // ========================================================
    // 右表：全量标准 + Linked 状态
    // ========================================================

    /// <summary>
    /// 获取所有有效标准，并标记哪些已关联当前机构
    /// </summary>
    [HttpPost("list")]
    public async Task<ActionResult<ApiResponse<object?>>> List([FromBody] ListRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.OrgCode))
                return Ok(ApiResponse<object?>.Ok(new List<object>()));

            // 1. 查询所有有效标准
            var standards = await _isoService.GetListAsync(p => p.IsValid == 1 && !p.IsDeleted);
            if (!standards.Success)
                return BadRequest(ApiResponse.Fail(standards.Error));

            // 2. 查询该机构已关联的标准编码
            var linked = await _linkService.GetListAsync(p =>
                p.OrgCode == request.OrgCode && !p.IsDeleted);
            var linkedCodes = new HashSet<string>(
                linked.Data?.Select(p => p.StandardCode) ?? Array.Empty<string>());

            // 3. 合并返回
            var items = standards.Data!.OrderByDescending(p => p.VersionYear).Select(p => new
            {
                Code = p.Code,
                StandardCode = p.StandardCode,
                StandardName = p.StandardName,
                VersionYear = p.VersionYear,
                Category = p.Category,
                Linked = linkedCodes.Contains(p.Code)
            }).ToList();

            return Ok(ApiResponse<object?>.Ok(items));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    // ========================================================
    // 保存：勾选/取消
    // ========================================================

    /// <summary>
    /// 单条保存：勾选创建关联，取消删除关联
    /// </summary>
    [HttpPost("save")]
    public async Task<ActionResult<ApiResponse<object?>>> Save([FromBody] SaveRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.OrgCode) || string.IsNullOrEmpty(request.StandardCode))
                return BadRequest(ApiResponse.Fail("OrgCode 和 StandardCode 不能为空"));

            if (request.Linked)
            {
                // 勾选 → 创建关联
                var exists = await _linkService.ExistsAsync(p =>
                    p.OrgCode == request.OrgCode &&
                    p.StandardCode == request.StandardCode &&
                    !p.IsDeleted);
                if (exists.Data)
                    return Ok(ApiResponse<object?>.Ok(new { request.OrgCode, request.StandardCode, Linked = true }));

                var entity = new Link
                {
                    Code = Guid.NewGuid().ToString("N"),
                    OrgCode = request.OrgCode,
                    StandardCode = request.StandardCode,
                    IsValid = 1,
                    CreateBy = _userContext.UserCode,
                    CreateTime = DateTime.UtcNow
                };
                var result = await _linkService.Insert(entity, _userContext.ClientIp);
                if (!result.Success)
                    return BadRequest(ApiResponse.Fail(result.Error));
            }
            else
            {
                // 取消 → 删除关联
                var existing = await _linkService.GetListAsync(p =>
                    p.OrgCode == request.OrgCode &&
                    p.StandardCode == request.StandardCode &&
                    !p.IsDeleted);
                if (existing.Success && existing.Data != null && existing.Data.Count > 0)
                {
                    foreach (var item in existing.Data)
                    {
                        await _linkService.DeleteByCode(item.Code, _userContext.ClientIp);
                    }
                }
            }

            return Ok(ApiResponse<object?>.Ok(new { request.OrgCode, request.StandardCode, request.Linked }));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    // ========================================================
    // 请求模型
    // ========================================================

    public class ListRequest
    {
        public string OrgCode { get; set; } = string.Empty;
    }

    public class SaveRequest
    {
        public string OrgCode { get; set; } = string.Empty;
        public string StandardCode { get; set; } = string.Empty;
        public bool Linked { get; set; }
    }
}

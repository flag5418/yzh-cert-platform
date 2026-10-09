
using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Services;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Result;
using CertPlatform.Admin.Entities.Cert;
using CertPlatform.Admin.Entities.Sys;

using CB = CertPlatform.Shared.Entities.Cert.CertificationBody;
using ISO = CertPlatform.Shared.Entities.Cert.ISOStandard;
using Link = CertPlatform.Admin.Entities.Sys.CertOrgStandard;
using Family = CertPlatform.Admin.Entities.Cert.CertStandardFamily;

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

/// <para><b>★ 端标记（2026-10-03）</b>：路由加 <c>Admin/</c> 段，与专家端 <c>/api/Auditor/*</c> 对称。
/// <para>背景：后台端 20 个 Controller 此前零端标记，4 个连业务域前缀都没有（<c>api/AIUsage</c>
/// <c>api/PromptTemplate</c> <c>api/ValidationRule</c> <c>api/ReportDefinition</c>），
/// 且 <c>api/System/[controller]</c> 与框架层 <c>YZH.Core.Web</c> 的 <c>api/System/*</c> 撞前缀。</para>
/// <para><b>不影响授权</b>：<c>ApiCode = Sha256("{METHOD}|{路由末段}|{动作名}")</c>（ApiScanner.cs:326-331）
/// 只取路由<b>末段</b>作控制器名，本 Controller 的末段未变 ⇒ <c>ApiCode</c> 不变 ⇒
/// <b>角色-接口关联不断裂</b>，无需重跑 ApiSync。</para>
[Route("api/Admin/Foundation/[controller]")]
public class CertOrgStandardController : ControllerBase
{
    private readonly EntityService<CB> _cbService;
    private readonly EntityService<ISO> _isoService;
    private readonly EntityService<Link> _linkService;
    private readonly EntityService<Family> _familyService;
    private readonly IUserContext _userContext;

    public CertOrgStandardController(
        EntityService<CB> cbService,
        EntityService<ISO> isoService,
        EntityService<Link> linkService,
        EntityService<Family> familyService,
        IUserContext userContext)
    {
        _cbService = cbService;
        _isoService = isoService;
        _linkService = linkService;
        _familyService = familyService;
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
                return Ok(ApiResponse.Fail(list.Error));

            var nodes = list.Data!.Select(p => new
            {
                Code = p.Code,
                Name = p.Name,
                ParentCode = (string?)null,
                Level = 1,
                IsLeaf = true,
                // 启用/禁用徽章（前端左树统一读 Extra.IsValid；该查询已过滤 IsValid=1）
                Extra = new { IsValid = p.IsValid }
            }).ToList();

            return Ok(ApiResponse<object?>.Ok(nodes));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse.Fail(ex.Message));
        }
    }

    // ========================================================
    // 右表：全量标准 + Linked 状态
    // ========================================================

    /// <summary>
    /// 获取所有有效标准，并标记哪些已关联当前机构
    /// </summary>
    [HttpPost("list")]
    public async Task<ActionResult<ApiResponse<object?>>> List([FromBody] CertOrgStandardListRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.OrgCode))
                return Ok(ApiResponse<object?>.Ok(new List<object>()));

            // 1. 查询所有有效标准
            var standards = await _isoService.GetListAsync(p => p.IsValid == 1 && !p.IsDeleted);
            if (!standards.Success)
                return Ok(ApiResponse.Fail(standards.Error));

            // 2. 查询该机构已关联的标准编码
            var linked = await _linkService.GetListAsync(p =>
                p.OrgCode == request.OrgCode && !p.IsDeleted);
            var linkedCodes = new HashSet<string>(
                linked.Data?.Select(p => p.StandardCode) ?? Array.Empty<string>());

            // 3. 查询族信息，构建 FamilyCode → FamilyName 映射
            var families = (await _familyService.GetListAsync()).Data ?? new();
            var familyMap = new Dictionary<string, string>(
                families.Where(f => f.Code != null).ToDictionary(f => f.Code!, f => $"{f.FamilyNo} {f.FamilyName}".Trim()));

            // 4. 合并返回（含族信息，供前端构建树）
            var items = standards.Data!.OrderByDescending(p => p.VersionYear).Select(p => new
            {
                Code = p.Code,
                StandardCode = p.StandardCode,
                StandardName = p.StandardName,
                VersionYear = p.VersionYear,
                Category = p.Category,
                FamilyCode = p.FamilyCode,
                FamilyName = p.FamilyCode != null && familyMap.TryGetValue(p.FamilyCode, out var fn) ? fn : string.Empty,
                Linked = linkedCodes.Contains(p.Code)
            }).ToList();

            return Ok(ApiResponse<object?>.Ok(items));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse.Fail(ex.Message));
        }
    }

    // ========================================================
    // 保存：勾选/取消
    // ========================================================

    /// <summary>
    /// 单条保存：勾选创建关联，取消删除关联
    /// </summary>
    [HttpPost("save")]
    public async Task<ActionResult<ApiResponse<object?>>> Save([FromBody] CertOrgStandardSaveRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.OrgCode) || string.IsNullOrEmpty(request.StandardCode))
                return Ok(ApiResponse.Fail("OrgCode 和 StandardCode 不能为空"));

            if (request.Linked)
            {
                // ★ 契约守卫（2026-10-09）：StandardCode 必须是 cert_iso_standard.Code（GUID）。
                //   传业务编号 slug（如 iso13485）会静默入库，但本接口 List 与专家端
                //   EnterpriseStageController 均按 Code 比对 ⇒ 库里有行却永不回显（勾选"没保存成功"）。
                var stdCheck = await _isoService.GetListAsync(p =>
                    p.Code == request.StandardCode && !p.IsDeleted);
                if (stdCheck.Data == null || stdCheck.Data.Count == 0)
                    return Ok(ApiResponse.Fail(
                        $"标准不存在：{request.StandardCode}（须传 cert_iso_standard.Code）"));

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
                    return Ok(ApiResponse.Fail(result.Error));
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
            return Ok(ApiResponse.Fail(ex.Message));
        }
    }

    // ========================================================
    // 请求模型
    // ========================================================

    public class CertOrgStandardListRequest
    {
        public string OrgCode { get; set; } = string.Empty;
    }

    public class CertOrgStandardSaveRequest
    {
        public string OrgCode { get; set; } = string.Empty;
        public string StandardCode { get; set; } = string.Empty;
        public bool Linked { get; set; }
    }
}

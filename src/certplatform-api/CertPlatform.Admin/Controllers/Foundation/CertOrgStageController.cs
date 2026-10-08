using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Services;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Result;
using CertPlatform.Admin.Entities.Cert;
using CertPlatform.Admin.Entities.Sys;

using CB = CertPlatform.Shared.Entities.Cert.CertificationBody;
using Stage = CertPlatform.Admin.Entities.Cert.CertStageView;
using Link = CertPlatform.Admin.Entities.Sys.CertOrgStage;

namespace CertPlatform.Admin.Controllers.Foundation;

/// <summary>
/// 机构-阶段关联管理控制器（勾选分配模式）
///
/// <para>路由前缀：/api/Foundation/CertOrgStage</para>
/// <para>左树：认证机构（cert_certification_body，扁平）</para>
/// <para>右表：所有有效认证阶段（cert_cert_stage，与「认证阶段定义」页同源），带 Linked 标记</para>
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
public class CertOrgStageController : ControllerBase
{
    private readonly EntityService<CB> _cbService;
    private readonly EntityService<Stage> _stageService;
    private readonly EntityService<Link> _linkService;
    private readonly IUserContext _userContext;

    public CertOrgStageController(
        EntityService<CB> cbService,
        EntityService<Stage> stageService,
        EntityService<Link> linkService,
        IUserContext userContext)
    {
        _cbService = cbService;
        _stageService = stageService;
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
    // 右表：全量阶段 + Linked 状态
    // ========================================================

    /// <summary>
    /// 获取所有有效阶段，并标记哪些已关联当前机构
    /// </summary>
    [HttpPost("list")]
    public async Task<ActionResult<ApiResponse<object?>>> List([FromBody] CertOrgStageListRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.OrgCode))
                return Ok(ApiResponse<object?>.Ok(new List<object>()));

            // 1. 查询所有有效阶段（与「认证阶段定义」页同源：cert_cert_stage）
            var stages = await _stageService.GetListAsync(p =>
                p.IsValid == 1 && !p.IsDeleted);
            if (!stages.Success)
                return Ok(ApiResponse.Fail(stages.Error));

            // 2. 查询该机构已关联的阶段键（cert_org_stage.StageCode ★Q2 起存 Code/GUID）
            var linked = await _linkService.GetListAsync(p =>
                p.OrgCode == request.OrgCode && !p.IsDeleted);
            var linkedCodes = new HashSet<string>(
                linked.Data?.Select(p => p.StageCode) ?? Array.Empty<string>());

            // 3. 合并返回（字段名对齐前端 CertOrgStageItem：PhaseCode=业务码仅供显示）
            var items = stages.Data!.OrderBy(p => p.SortOrder).ThenBy(p => p.StageCode).Select(p => new
            {
                Code = p.Code,
                PhaseCode = p.StageCode,
                PhaseName = p.StageName,
                SortOrder = p.SortOrder,
                Category = p.Category,
                CategoryName = p.CategoryName,
                // ★ Q2：关联判定用 Code（GUID），⛔ 不再用业务码 p.StageCode
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
    public async Task<ActionResult<ApiResponse<object?>>> Save([FromBody] CertOrgStageSaveRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.OrgCode) || string.IsNullOrEmpty(request.StageCode))
                return Ok(ApiResponse.Fail("OrgCode 和 StageCode 不能为空"));

            // ★ Q2：request.StageCode = 前端传入的 cert_cert_stage.Code（GUID），与 cert_org_stage.StageCode 同口径
            if (request.Linked)
            {
                // 勾选 → 创建关联
                var exists = await _linkService.ExistsAsync(p =>
                    p.OrgCode == request.OrgCode &&
                    p.StageCode == request.StageCode &&
                    !p.IsDeleted);
                if (exists.Data)
                    return Ok(ApiResponse<object?>.Ok(new { request.OrgCode, request.StageCode, Linked = true }));

                var entity = new Link
                {
                    Code = Guid.NewGuid().ToString("N"),
                    OrgCode = request.OrgCode,
                    StageCode = request.StageCode,
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
                    p.StageCode == request.StageCode &&
                    !p.IsDeleted);
                if (existing.Success && existing.Data != null && existing.Data.Count > 0)
                {
                    foreach (var item in existing.Data)
                    {
                        await _linkService.DeleteByCode(item.Code, _userContext.ClientIp);
                    }
                }
            }

            return Ok(ApiResponse<object?>.Ok(new { request.OrgCode, request.StageCode, request.Linked }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse.Fail(ex.Message));
        }
    }

    // ========================================================
    // 请求模型
    // ========================================================

    public class CertOrgStageListRequest
    {
        public string OrgCode { get; set; } = string.Empty;
    }

    public class CertOrgStageSaveRequest
    {
        public string OrgCode { get; set; } = string.Empty;
        public string StageCode { get; set; } = string.Empty;
        public bool Linked { get; set; }
    }
}

using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Controllers;
using YZH.Core.Api.Models.Users;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.DataBase.Services;
using YZH.Core.Stand.Models.Result;
using CertPlatform.Shared.Domain.Scope;
using CertPlatform.Shared.Entities.Cert;
using CertPlatform.Shared.Entities.Dir;

namespace CertPlatform.Admin.Controllers.System;

/// <summary>
/// 队列监控控制器
/// 路由前缀：/api/System/QueueMonitor
/// </summary>
[ApiController]

/// <para><b>★ 端标记（2026-10-03）</b>：路由加 <c>Admin/</c> 段，与专家端 <c>/api/Auditor/*</c> 对称。
/// <para>背景：后台端 20 个 Controller 此前零端标记，4 个连业务域前缀都没有（<c>api/AIUsage</c>
/// <c>api/PromptTemplate</c> <c>api/ValidationRule</c> <c>api/ReportDefinition</c>），
/// 且 <c>api/System/[controller]</c> 与框架层 <c>YZH.Core.Web</c> 的 <c>api/System/*</c> 撞前缀。</para>
/// <para><b>不影响授权</b>：<c>ApiCode = Sha256("{METHOD}|{路由末段}|{动作名}")</c>（ApiScanner.cs:326-331）
/// 只取路由<b>末段</b>作控制器名，本 Controller 的末段未变 ⇒ <c>ApiCode</c> 不变 ⇒
/// <b>角色-接口关联不断裂</b>，无需重跑 ApiSync。</para>
[Route("api/Admin/System/[controller]")]
public class QueueMonitorController : WebControllerBase
{
    private readonly QueueManager _queueManager;
    private readonly IDbOrm _db;
    /// <summary>核心资源检索唯一入口（铁律 L5）。</summary>
    private readonly ICoreResourceQuery _core;

    public QueueMonitorController(QueueManager queueManager, IDbOrm db, ICoreResourceQuery core)
    {
        _queueManager = queueManager;
        _db = db;
        _core = core;
    }

    [HttpPost("list")]
    public async Task<IActionResult> GetList([FromBody] QueueListRequest req)
    {
        var result = await _queueManager.GetQueueListAsync(
            req.Type, req.Status, req.StartTime, req.EndTime, req.Page, req.Rows,
            orgCode: req.OrgCode,
            scopeResolver: ResolveScopeDisplayAsync,
            creatorResolver: ResolveCreatorNameAsync);
        return Ok(ApiResponse<object?>.Ok(data: result));
    }

    [HttpPost("detail")]
    public async Task<IActionResult> GetDetail([FromBody] QueueDetailRequest req)
    {
        var result = await _queueManager.GetQueueDetailAsync(req.QueueCode, ResolveCreatorNameAsync, req.OrgCode);
        return Ok(ApiResponse<object?>.Ok(data: result));
    }

    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel([FromBody] QueueCancelRequest req)
    {
        var (ok, error) = await _queueManager.CancelQueueAsync(req.QueueCode, req.OrgCode);
        return Ok(ok ? ApiResponse<object?>.Ok() : ApiResponse<object?>.Fail(error));
    }

    [HttpPost("retry")]
    public async Task<IActionResult> Retry([FromBody] QueueRetryRequest req)
    {
        var (ok, error) = await _queueManager.RetryQueueAsync(req.QueueCode, req.OrgCode);
        return Ok(ok ? ApiResponse<object?>.Ok() : ApiResponse<object?>.Fail(error));
    }

    [HttpPost("task/retry")]
    public async Task<IActionResult> RetryTask([FromBody] TaskRetryRequest req)
    {
        var (ok, error) = await _queueManager.RetryTaskAsync(req.TaskCode, req.OrgCode);
        return Ok(ok ? ApiResponse<object?>.Ok() : ApiResponse<object?>.Fail(error));
    }

    [HttpPost("status")]
    public async Task<IActionResult> GetStatus()
    {
        var result = await _queueManager.GetQueueStatsAsync();
        return Ok(ApiResponse<object?>.Ok(data: result));
    }

    [HttpPost("resource/locked")]
    public async Task<IActionResult> GetResourceLocked([FromBody] ResourceLockRequest req)
    {
        var result = await _queueManager.FindResourceLockAsync(req.ResourceTable, req.ResourceCodes);
        return Ok(ApiResponse<object?>.Ok(data: result));
    }

    /// <summary>
    ///     ★ 把 <c>CreateBy</c>（<b>用户 Code</b>）解析为人话姓名。
    ///
    ///     <para><b>为什么必须解析</b>：<c>CreateBy</c> 按契约存 Code（<c>BaseEntity</c> 注释口径，
    /// 2026-10-11 队列入队处也已统一）⇒ 直接渲染就是「创建人 = <c>df6e03e9…</c>」
    /// （裸 Code 当人名）。解析顺序：真实姓名 → 登录名 → 空串（前端回落「—」或原 Code）。</para>
    ///
    ///     <para>⚠️ 已停用用户（<c>IncludeDisabled</c>）也要能显示姓名 —— 历史队列的创建人可能已停用。
    /// 异常一律吃掉并返回空串：创建人解析是<b>旁路</b>，⛔ 不能因为它让整个队列页打不开。</para>
    /// </summary>
    private async Task<string> ResolveCreatorNameAsync(string? userCode)
    {
        if (string.IsNullOrWhiteSpace(userCode)) return string.Empty;

        try
        {
            var users = (await _db.GetListAsync<Sys_User>(x => x.Code == userCode, includeDisabled: true)).Data;
            var u = users?.FirstOrDefault();
            if (u == null) return string.Empty;
            return string.IsNullOrWhiteSpace(u.UserTrueName) ? (u.UserName ?? string.Empty) : u.UserTrueName!;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// 按队列类型解析 ScopeKey 为人话（企业名 / 标准名 / 阶段名）。
    /// 解析失败时回落空字符串 → 前端回落显示原 ScopeKey。
    /// </summary>
    private async Task<string> ResolveScopeDisplayAsync(string queueType, string? scopeKey)
    {
        if (string.IsNullOrWhiteSpace(scopeKey)) return string.Empty;

        try
        {
            switch (queueType)
            {
                // ── file_convert：ScopeKey = cert_standard_directory_config.Code ──
                case "file_convert":
                    return await ResolveFileConvertScopeAsync(scopeKey);

                // ── doc_extract：ScopeKey = cert_enterprise.Code ──
                case "doc_extract":
                    return await ResolveDocExtractScopeAsync(scopeKey);

                // ── enterprise_normalize：ScopeKey = "entnorm:{EnterpriseCode}:{StageCode}" ──
                case "enterprise_normalize":
                    return await ResolveEnterpriseNormalizeScopeAsync(scopeKey);

                // ── enterprise_original_*：ScopeKey = "{enterpriseCode}@{stageCode}" ──
                case "enterprise_original_file":
                case "enterprise_original_ingest":
                case "enterprise_original_analyze":
                    return await ResolveEnterpriseOriginalScopeAsync(scopeKey);

                default:
                    return string.Empty;
            }
        }
        catch
        {
            return string.Empty;
        }
    }

    private async Task<string> ResolveFileConvertScopeAsync(string configCode)
    {
        var config = await _core.GetDirectoryConfigByCodeAsync(configCode);
        if (config == null) return string.Empty;

        var stdName = (await _core.GetStandardByCodeAsync(config.StandardCode))?.StandardName;
        var stageName = (await _core.GetStageByCodeAsync(config.StageCode))?.StageName;

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(stdName)) parts.Add(stdName);
        if (!string.IsNullOrWhiteSpace(stageName)) parts.Add(stageName);
        return parts.Count > 0 ? string.Join(" / ", parts) : configCode;
    }

    private async Task<string> ResolveDocExtractScopeAsync(string enterpriseCode)
    {
        var ent = await _core.GetEnterpriseByCodeAsync(enterpriseCode);
        return ent?.Name ?? string.Empty;
    }

    private async Task<string> ResolveEnterpriseNormalizeScopeAsync(string scopeKey)
    {
        // 格式：entnorm:{EnterpriseCode}:{StageCode}
        var parts = scopeKey.Split(':');
        if (parts.Length < 3) return string.Empty;
        var enterpriseCode = parts[1];
        var stageCode = parts[2];
        return await BuildEnterpriseStageDisplayAsync(enterpriseCode, stageCode);
    }

    private async Task<string> ResolveEnterpriseOriginalScopeAsync(string scopeKey)
    {
        // 格式：{enterpriseCode}@{stageCode}
        var parts = scopeKey.Split('@');
        if (parts.Length < 2) return string.Empty;
        return await BuildEnterpriseStageDisplayAsync(parts[0], parts[1]);
    }

    /// <summary>组装「企业名 / 阶段名」</summary>
    private async Task<string> BuildEnterpriseStageDisplayAsync(string enterpriseCode, string stageCode)
    {
        var entName = (await _core.GetEnterpriseByCodeAsync(enterpriseCode))?.Name;
        var stageName = (await _core.GetStageByCodeAsync(stageCode))?.StageName;

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(entName)) parts.Add(entName);
        if (!string.IsNullOrWhiteSpace(stageName)) parts.Add(stageName);
        return parts.Count > 0 ? string.Join(" / ", parts) : string.Empty;
    }
}

#region 请求 DTO

/// <summary>OrgCode 可选租户过滤（H12 / R-03）：传入则只看该机构的队列；空 = 平台全量（后台监控合法视角）。</summary>
public class QueueListRequest
{
    public string? Type { get; set; }
    public string? Status { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int Page { get; set; } = 1;
    public int Rows { get; set; } = 20;
    public string? OrgCode { get; set; }
}

public class QueueDetailRequest
{
    public string QueueCode { get; set; } = "";
    public string? OrgCode { get; set; }
}

public class QueueCancelRequest
{
    public string QueueCode { get; set; } = "";
    public string? OrgCode { get; set; }
}

public class QueueRetryRequest
{
    public string QueueCode { get; set; } = "";
    public string? OrgCode { get; set; }
}

public class TaskRetryRequest
{
    public string TaskCode { get; set; } = "";
    public string? OrgCode { get; set; }
}

public class ResourceLockRequest
{
    public string ResourceTable { get; set; } = "";
    public List<string> ResourceCodes { get; set; } = new();
}

#endregion

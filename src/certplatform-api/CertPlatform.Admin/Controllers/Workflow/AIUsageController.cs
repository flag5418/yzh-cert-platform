using Microsoft.AspNetCore.Mvc;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Models.Result;

namespace CertPlatform.Admin.Controllers.Workflow;

/// <summary>
/// AI 费用监控控制器
/// <para>路由前缀：/api/AIUsage</para>
/// <para>数据来源：cert_ai_usage_log（DocExtractionRuleService.AI 写入）</para>
/// <para>非标准 CRUD（聚合统计），直接使用 IDbOrm 原生 SQL，不继承 YzhControllerBase</para>
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AIUsageController : ControllerBase
{
    private readonly IDbOrm _db;

    public AIUsageController(IDbOrm db)
    {
        _db = db;
    }

    #region DTOs

    private class UsageRow
    {
        public long id { get; set; }
        public string? call_id { get; set; }
        public string? business_type { get; set; }
        public string? business_ref { get; set; }
        public string? skill { get; set; }
        public string? provider { get; set; }
        public string? model { get; set; }
        public int prompt_tokens { get; set; }
        public int completion_tokens { get; set; }
        public int total_tokens { get; set; }
        public decimal cost_usd { get; set; }
        public long duration_ms { get; set; }
        public int success { get; set; }
        public string? error_message { get; set; }
        public DateTime? CreateTime { get; set; }
    }

    private class DailyCostRow
    {
        public string date { get; set; } = "";
        public decimal cost { get; set; }
        public int calls { get; set; }
    }

    private class SummaryRow
    {
        public decimal totalCost { get; set; }
        public decimal monthCost { get; set; }
        public decimal weekCost { get; set; }
        public decimal todayCost { get; set; }
        public int totalCalls { get; set; }
        public int monthCalls { get; set; }
        public int weekCalls { get; set; }
        public int todayCalls { get; set; }
    }

    private class AliyunStatusRow
    {
        public int configured { get; set; }
    }

    #endregion

    #region 端点

    /// <summary>分页查询调用记录（POST /api/AIUsage/getPageData）</summary>
    [HttpPost("getPageData")]
    public async Task<IActionResult> GetPageData([FromBody] PageRequest request)
    {
        int page = request.Page > 0 ? request.Page : 1;
        int rows = request.Rows > 0 ? request.Rows : 20;
        int offset = (page - 1) * rows;

        var sql = @"SELECT id, call_id, business_type, business_ref, skill, provider, model,
                           prompt_tokens, completion_tokens, total_tokens, cost_usd, duration_ms,
                           success, error_message, CreateTime
                    FROM cert_ai_usage_log
                    WHERE IsDeleted = 0";
        var countSql = "SELECT COUNT(*) FROM cert_ai_usage_log WHERE IsDeleted = 0";

        if (!string.IsNullOrEmpty(request.StartDate))
        {
            sql += " AND CreateTime >= @StartDate";
            countSql += " AND CreateTime >= @StartDate";
        }
        if (!string.IsNullOrEmpty(request.EndDate))
        {
            sql += " AND DATE(CreateTime) <= @EndDate";
            countSql += " AND DATE(CreateTime) <= @EndDate";
        }

        sql += " ORDER BY CreateTime DESC LIMIT @Rows OFFSET @Offset";

        var list = await _db.SqlQueryAsync<UsageRow>(sql, new
        {
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Rows = rows,
            Offset = offset
        });
        var countResult = await _db.SqlQueryAsync<CountRow>(countSql, new
        {
            StartDate = request.StartDate,
            EndDate = request.EndDate
        });

        var items = (list.Data ?? new()).Select(r => new
        {
            Id = r.id,
            CallId = r.call_id,
            BusinessType = r.business_type,
            BusinessRef = r.business_ref,
            Skill = r.skill,
            Provider = r.provider,
            Model = r.model,
            PromptTokens = r.prompt_tokens,
            CompletionTokens = r.completion_tokens,
            TotalTokens = r.total_tokens,
            CostUsd = r.cost_usd,
            DurationMs = r.duration_ms,
            Success = r.success == 1,
            ErrorMessage = r.error_message,
            CreateTime = r.CreateTime?.ToString("yyyy-MM-dd HH:mm:ss")
        }).ToList();

        var total = countResult.Data?.FirstOrDefault()?.total ?? 0;

        return Ok(ApiResponse<object>.Ok(new { rows = items, total }));
    }

    /// <summary>费用摘要统计（GET /api/AIUsage/summary）</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var sql = @"SELECT
                      SUM(cost_usd) as totalCost,
                      SUM(CASE WHEN CreateTime >= DATE_FORMAT(NOW(), '%Y-%m-01') THEN cost_usd ELSE 0 END) as monthCost,
                      SUM(CASE WHEN CreateTime >= DATE_SUB(CURDATE(), INTERVAL WEEKDAY(CURDATE()) DAY) THEN cost_usd ELSE 0 END) as weekCost,
                      SUM(CASE WHEN DATE(CreateTime) = CURDATE() THEN cost_usd ELSE 0 END) as todayCost,
                      COUNT(*) as totalCalls,
                      SUM(CASE WHEN CreateTime >= DATE_FORMAT(NOW(), '%Y-%m-01') THEN 1 ELSE 0 END) as monthCalls,
                      SUM(CASE WHEN CreateTime >= DATE_SUB(CURDATE(), INTERVAL WEEKDAY(CURDATE()) DAY) THEN 1 ELSE 0 END) as weekCalls,
                      SUM(CASE WHEN DATE(CreateTime) = CURDATE() THEN 1 ELSE 0 END) as todayCalls
                    FROM cert_ai_usage_log WHERE IsDeleted = 0";

        var result = await _db.SqlQueryAsync<SummaryRow>(sql);
        var data = result.Data?.FirstOrDefault();

        return Ok(ApiResponse<object>.Ok(new
        {
            TotalCost = data?.totalCost ?? 0m,
            MonthCost = data?.monthCost ?? 0m,
            WeekCost = data?.weekCost ?? 0m,
            TodayCost = data?.todayCost ?? 0m,
            TotalCalls = data?.totalCalls ?? 0,
            MonthCalls = data?.monthCalls ?? 0,
            WeekCalls = data?.weekCalls ?? 0,
            TodayCalls = data?.todayCalls ?? 0
        }));
    }

    /// <summary>按日费用趋势（GET /api/AIUsage/daily-costs?startDate=yyyy-MM-dd&endDate=yyyy-MM-dd）</summary>
    [HttpGet("daily-costs")]
    public async Task<IActionResult> GetDailyCosts([FromQuery] string? startDate, [FromQuery] string? endDate)
    {
        var sql = @"SELECT DATE_FORMAT(CreateTime, '%Y-%m-%d') as date,
                           SUM(cost_usd) as cost,
                           COUNT(*) as calls
                    FROM cert_ai_usage_log
                    WHERE IsDeleted = 0";
        if (!string.IsNullOrEmpty(startDate))
            sql += " AND CreateTime >= @StartDate";
        if (!string.IsNullOrEmpty(endDate))
            sql += " AND DATE(CreateTime) <= @EndDate";
        sql += " GROUP BY DATE_FORMAT(CreateTime, '%Y-%m-%d') ORDER BY date ASC";

        var result = await _db.SqlQueryAsync<DailyCostRow>(sql, new { StartDate = startDate, EndDate = endDate });

        var items = (result.Data ?? new()).Select(r => new
        {
            Date = r.date,
            Cost = r.cost,
            Calls = r.calls
        }).ToList();

        return Ok(ApiResponse<object>.Ok(new { data = items }));
    }

    /// <summary>阿里云 AI 配置状态（GET /api/AIUsage/aliyun-status）</summary>
    [HttpGet("aliyun-status")]
    public async Task<IActionResult> GetAliyunStatus()
    {
        // 检查是否配置了 AI API Key（任一启用配置即视为已配置）
        var sql = @"SELECT COUNT(*) as configured FROM cert_ai_config WHERE IsEnabled = 1 AND IsDeleted = 0";
        var result = await _db.SqlQueryAsync<AliyunStatusRow>(sql);
        var configured = (result.Data?.FirstOrDefault()?.configured ?? 0) > 0;

        return Ok(ApiResponse<object>.Ok(new
        {
            Configured = configured,
            DashboardUrl = "https://dashscope.console.aliyun.com/"
        }));
    }

    #endregion

    #region 请求模型

    public class PageRequest
    {
        public int Page { get; set; } = 1;
        public int Rows { get; set; } = 20;
        public string? StartDate { get; set; }
        public string? EndDate { get; set; }
    }

    private class CountRow
    {
        public int total { get; set; }
    }

    #endregion
}

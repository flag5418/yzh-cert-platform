using System.Linq;
using Microsoft.AspNetCore.Mvc;
using SqlSugar;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Models.Result;
using CertPlatform.Shared.Entities.Doc;

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

        var query = _db.Client.Queryable<AiUsageLog>()
            .Where(x => !x.IsDeleted);

        if (!string.IsNullOrEmpty(request.StartDate))
            query = query.Where(x => x.CreateTime >= DateTime.Parse(request.StartDate));
        if (!string.IsNullOrEmpty(request.EndDate))
            query = query.Where(x => x.CreateTime <= DateTime.Parse(request.EndDate).Date.AddDays(1).AddTicks(-1));

        var total = await query.CountAsync();
        var list = await query
            .OrderByDescending(x => x.CreateTime)
            .Skip(offset)
            .Take(rows)
            .Select(x => new UsageRow
            {
                id = x.Id,
                call_id = x.CallId,
                business_type = x.BusinessType,
                business_ref = x.BusinessRef,
                skill = x.Skill,
                provider = x.Provider,
                model = x.Model,
                prompt_tokens = x.PromptTokens,
                completion_tokens = x.CompletionTokens,
                total_tokens = x.TotalTokens,
                cost_usd = x.CostUsd,
                duration_ms = x.DurationMs,
                success = x.Success ? 1 : 0,
                error_message = x.ErrorMessage,
                CreateTime = x.CreateTime
            })
            .ToListAsync();

        var items = list.Select(r => new
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

        return Ok(ApiResponse<object>.Ok(new { rows = items, total }));
    }

    /// <summary>费用摘要统计（GET /api/AIUsage/summary）</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var now = DateTime.Now;
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var weekStart = now.AddDays(-(int)now.DayOfWeek);
        var todayStart = now.Date;

        var result = await _db.Client.Queryable<AiUsageLog>()
            .Where(x => !x.IsDeleted)
            .Select(x => new SummaryRow
            {
                totalCost = x.CostUsd,
                monthCost = x.CreateTime >= monthStart ? x.CostUsd : 0,
                weekCost = x.CreateTime >= weekStart ? x.CostUsd : 0,
                todayCost = x.CreateTime >= todayStart ? x.CostUsd : 0,
                totalCalls = 1,
                monthCalls = x.CreateTime >= monthStart ? 1 : 0,
                weekCalls = x.CreateTime >= weekStart ? 1 : 0,
                todayCalls = x.CreateTime >= todayStart ? 1 : 0
            })
            .ToListAsync();

        var data = new SummaryRow();
        foreach (var row in result)
        {
            data.totalCost += row.totalCost;
            data.monthCost += row.monthCost;
            data.weekCost += row.weekCost;
            data.todayCost += row.todayCost;
            data.totalCalls += row.totalCalls;
            data.monthCalls += row.monthCalls;
            data.weekCalls += row.weekCalls;
            data.todayCalls += row.todayCalls;
        }

        return Ok(ApiResponse<object>.Ok(new
        {
            TotalCost = data.totalCost,
            MonthCost = data.monthCost,
            WeekCost = data.weekCost,
            TodayCost = data.todayCost,
            TotalCalls = data.totalCalls,
            MonthCalls = data.monthCalls,
            WeekCalls = data.weekCalls,
            TodayCalls = data.todayCalls
        }));
    }

    /// <summary>按日费用趋势（GET /api/AIUsage/daily-costs?startDate=yyyy-MM-dd&endDate=yyyy-MM-dd）</summary>
    [HttpGet("daily-costs")]
    public async Task<IActionResult> GetDailyCosts([FromQuery] string? startDate, [FromQuery] string? endDate)
    {
        var query = _db.Client.Queryable<AiUsageLog>()
            .Where(x => !x.IsDeleted);

        if (!string.IsNullOrEmpty(startDate))
            query = query.Where(x => x.CreateTime >= DateTime.Parse(startDate));
        if (!string.IsNullOrEmpty(endDate))
            query = query.Where(x => x.CreateTime <= DateTime.Parse(endDate).Date.AddDays(1).AddTicks(-1));

        var result = await query
            .GroupBy(x => x.CreateTime.Year.ToString() + "-" +
                           x.CreateTime.Month.ToString("D2") + "-" +
                           x.CreateTime.Day.ToString("D2"))
            .Select(x => new DailyCostRow
            {
                date = x.CreateTime.Year.ToString() + "-" +
                       x.CreateTime.Month.ToString("D2") + "-" +
                       x.CreateTime.Day.ToString("D2"),
                cost = SqlFunc.AggregateSum(x.CostUsd),
                calls = SqlFunc.AggregateCount(x.Id)
            })
            .OrderBy(x => x.date)
            .ToListAsync();

        var items = result.Select(r => new
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
        var configured = await _db.Client.Queryable<AiConfig>()
            .Where(x => x.IsEnabled && !x.IsDeleted)
            .CountAsync() > 0;

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

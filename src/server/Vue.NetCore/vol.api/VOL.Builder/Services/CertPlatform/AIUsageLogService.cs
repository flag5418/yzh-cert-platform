using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VOL.Builder.IServices.CertPlatform;
using VOL.Core.EFDbContext;
using VOL.Core.Extensions.AutofacManager;
using VOL.Entity.CertPlatform.DocExtraction;

namespace VOL.Builder.Services.CertPlatform
{
    public class AIUsageLogService : IAIUsageLogService, IDependency
    {
        private readonly VOLContext _db;

        // 千问模型单价（美元/百万tokens），与实际计费一致
        private static readonly Dictionary<string, decimal> ModelPrices = new()
        {
            { "qwen-turbo", 0.5m },
            { "qwen-plus", 2.0m },
            { "qwen-max", 10.0m },
            { "qwen-max-longcontext", 10.0m },
            { "deepseek-chat", 0.5m },
            { "deepseek-reasoner", 0.5m },
        };

        public AIUsageLogService(VOLContext db)
        {
            _db = db;
        }

        public async Task LogCallAsync(AIUsageLog log)
        {
            await _db.Set<AIUsageLog>().AddAsync(log);
            await _db.SaveChangesAsync();
        }

        public async Task<AILocalBalanceDto> GetBalanceInfoAsync()
        {
            var aiConfig = await _db.Set<AIConfig>()
                .Where(c => c.IsEnabled)
                .FirstOrDefaultAsync();

            var sysConfig = AutofacContainerModule.GetService<ISysConfigService>();
            var aliyunKey = sysConfig?.Get("aliyun_access_key_id") ?? "";
            var aliyunSecret = sysConfig?.Get("aliyun_access_key_secret") ?? "";
            var hasAliyun = !string.IsNullOrWhiteSpace(aliyunKey) && !string.IsNullOrWhiteSpace(aliyunSecret);

            var summary = await GetSummaryAsync();

            return new AILocalBalanceDto
            {
                HasConfiguration = aiConfig != null,
                Provider = aiConfig?.Provider ?? "qwen",
                Model = aiConfig?.Model ?? "qwen-turbo",
                AliyunStatus = hasAliyun ? "已配置" : "未配置",
                AlipayUrl = hasAliyun
                    ? "https://usercenter2.aliyun.com/finance/overage"
                    : "",
                LocalAccumulatedCost = summary.TotalCost,
                TotalCalls = summary.TotalCalls
            };
        }

        public async Task<AIUsageSummaryDto> GetSummaryAsync()
        {
            var now = DateTime.Now;
            var todayStart = new DateTime(now.Year, now.Month, now.Day);
            var weekStart = todayStart.AddDays(-(int)now.DayOfWeek + 1);
            var monthStart = new DateTime(now.Year, now.Month, 1);

            var all = await _db.Set<AIUsageLog>()
                .Where(l => l.Success)
                .ToListAsync();

            var today = all.Where(l => l.CreateDate >= todayStart).ToList();
            var week = all.Where(l => l.CreateDate >= weekStart).ToList();
            var month = all.Where(l => l.CreateDate >= monthStart).ToList();

            return new AIUsageSummaryDto
            {
                TodayCost = today.Sum(l => l.CostUsd),
                WeekCost = week.Sum(l => l.CostUsd),
                MonthCost = month.Sum(l => l.CostUsd),
                TotalCost = all.Sum(l => l.CostUsd),
                TodayCalls = today.Count,
                WeekCalls = week.Count,
                MonthCalls = month.Count,
                TotalCalls = all.Count,
                TotalTokens = all.Sum(l => l.TotalTokens)
            };
        }

        public async Task<List<AIDailyCostDto>> GetDailyCostsAsync(DateTime startDate, DateTime endDate)
        {
            var logs = await _db.Set<AIUsageLog>()
                .Where(l => l.Success
                    && l.CreateDate >= startDate
                    && l.CreateDate <= endDate)
                .OrderBy(l => l.CreateDate)
                .ToListAsync();

            var grouped = logs
                .GroupBy(l => l.CreateDate.Date)
                .Select(g => new AIDailyCostDto
                {
                    Date = g.Key.ToString("yyyy-MM-dd"),
                    Cost = g.Sum(l => l.CostUsd),
                    Calls = g.Count(),
                    TotalTokens = g.Sum(l => l.TotalTokens)
                })
                .ToList();

            return grouped;
        }

        public async Task<List<AIUsageLog>> GetRecentCallsAsync(int page, int pageSize, DateTime? startDate, DateTime? endDate)
        {
            var query = _db.Set<AIUsageLog>()
                .OrderByDescending(l => l.CreateDate)
                .AsQueryable();

            if (startDate.HasValue)
                query = query.Where(l => l.CreateDate >= startDate.Value);
            if (endDate.HasValue)
                query = query.Where(l => l.CreateDate <= endDate.Value);

            var total = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return items;
        }

        public async Task<long> GetTotalCountAsync(DateTime? startDate, DateTime? endDate)
        {
            var query = _db.Set<AIUsageLog>().AsQueryable();
            if (startDate.HasValue)
                query = query.Where(l => l.CreateDate >= startDate.Value);
            if (endDate.HasValue)
                query = query.Where(l => l.CreateDate <= endDate.Value);
            return await query.CountAsync();
        }

        public static decimal CalculateCost(string model, int promptTokens, int completionTokens)
        {
            var rate = ModelPrices.TryGetValue(model, out var r) ? r : ModelPrices["qwen-turbo"];
            var totalTokens = (decimal)(promptTokens + completionTokens);
            return Math.Round(totalTokens / 1_000_000m * rate, 6);
        }
    }
}

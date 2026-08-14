using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VOL.Core.Extensions.AutofacManager;
using VOL.Entity.CertPlatform.DocExtraction;

namespace VOL.Builder.IServices.CertPlatform
{
    public class AILocalBalanceDto
    {
        public bool HasConfiguration { get; set; }
        public string Provider { get; set; }
        public string Model { get; set; }
        public string AliyunStatus { get; set; }
        public string AlipayUrl { get; set; }
        public decimal LocalAccumulatedCost { get; set; }
        public int TotalCalls { get; set; }
    }

    public class AIDailyCostDto
    {
        public string Date { get; set; }
        public decimal Cost { get; set; }
        public int Calls { get; set; }
        public int TotalTokens { get; set; }
    }

    public class AIUsageSummaryDto
    {
        public decimal TodayCost { get; set; }
        public decimal WeekCost { get; set; }
        public decimal MonthCost { get; set; }
        public int TodayCalls { get; set; }
        public int WeekCalls { get; set; }
        public int MonthCalls { get; set; }
        public decimal TotalCost { get; set; }
        public int TotalCalls { get; set; }
        public int TotalTokens { get; set; }
    }

    public interface IAIUsageLogService : IDependency
    {
        Task LogCallAsync(AIUsageLog log);
        Task<AILocalBalanceDto> GetBalanceInfoAsync();
        Task<AIUsageSummaryDto> GetSummaryAsync();
        Task<List<AIDailyCostDto>> GetDailyCostsAsync(DateTime startDate, DateTime endDate);
        Task<List<AIUsageLog>> GetRecentCallsAsync(int page, int pageSize, DateTime? startDate, DateTime? endDate);
        Task<long> GetTotalCountAsync(DateTime? startDate, DateTime? endDate);
    }
}

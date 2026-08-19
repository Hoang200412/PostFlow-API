using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PostFlow.Models;

namespace PostFlow.Services.Interfaces
{
    public interface IAnalyticsService
    {
        Task<object> GetSummaryAsync(string userId, DateTime? fromDate, DateTime? toDate);
        Task<IEnumerable<Analytics>> GetJobDetailsAsync(string userId, string jobId);
    }
}

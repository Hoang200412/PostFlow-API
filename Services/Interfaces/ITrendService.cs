using System.Collections.Generic;
using System.Threading.Tasks;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Models.DTOs.Responses;

namespace PostFlow.Services.Interfaces
{
    public interface ITrendService
    {
        Task<IEnumerable<TrendTopicDto>> GetTrendingTopicsAsync(string? platform = null, string? category = null);
        Task<TrendAnalysisResponse> AnalyzeTrendAsync(string userId, AnalyzeTrendRequest request);
    }
}

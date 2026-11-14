using System.Threading.Tasks;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Models.DTOs.Responses;

namespace PostFlow.Services.Interfaces
{
    public interface IExplainerVideoService
    {
        /// <summary>
        /// Tạo tác vụ nghiên cứu chủ đề ngầm qua Hangfire
        /// </summary>
        string CreateResearchJob(string userId, ExplainerResearchRequest request);

        /// <summary>
        /// Tạo tác vụ sinh kịch bản phân cảnh ngầm qua Hangfire
        /// </summary>
        string CreateStoryboardJob(string userId, ExplainerRenderRequest request);

        /// <summary>
        /// Nghiên cứu chuyên sâu về chủ đề bằng Google Gemini AI (Fact-checking, bài học, viral hooks)
        /// </summary>
        Task<ExplainerResearchResponse> ResearchTopicAsync(string userId, ExplainerResearchRequest request);

        /// <summary>
        /// Sinh kịch bản phân cảnh cấu trúc JSON (Flowchart, Card, Metric, Code)
        /// </summary>
        Task<ExplainerStoryboardResponse> GenerateStoryboardAsync(string userId, ExplainerRenderRequest request);

        /// <summary>
        /// Tạo tác vụ kết xuất video ngầm (Hangfire Job)
        /// </summary>
        string CreateRenderJob(string userId, ExplainerRenderRequest request);

        /// <summary>
        /// Lấy trạng thái tiến độ render của Job
        /// </summary>
        ExplainerJobStatusResponse? GetJobStatus(string jobId, string userId);
    }
}


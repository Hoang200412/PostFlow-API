using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Services.Interfaces;

namespace PostFlow.Controllers
{
    [ApiController]
    [Route("api/explainer")]
    [Authorize]
    public class ExplainerVideoController : ControllerBase
    {
        private readonly IExplainerVideoService _explainerService;

        public ExplainerVideoController(IExplainerVideoService explainerService)
        {
            _explainerService = explainerService;
        }

        /// <summary>
        /// Bước 1: Khởi chạy tác vụ nghiên cứu chuyên sâu chủ đề ngầm qua Hangfire
        /// </summary>
        [HttpPost("research")]
        public IActionResult ResearchTopic([FromBody] ExplainerResearchRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
            var jobId = _explainerService.CreateResearchJob(userId, request);
            return Accepted(new { jobId, status = "Pending", message = "Đã tiếp nhận yêu cầu nghiên cứu chủ đề." });
        }

        /// <summary>
        /// Bước 2: Khởi chạy tác vụ sinh kịch bản phân cảnh ngầm qua Hangfire
        /// </summary>
        [HttpPost("storyboard")]
        public IActionResult GenerateStoryboard([FromBody] ExplainerRenderRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
            var jobId = _explainerService.CreateStoryboardJob(userId, request);
            return Accepted(new { jobId, status = "Pending", message = "Đã tiếp nhận yêu cầu sinh kịch bản phân cảnh." });
        }

        /// <summary>
        /// Bước 3: Đưa tác vụ Render Remotion Explainer Video vào hàng đợi Hangfire
        /// </summary>
        [HttpPost("render")]
        public IActionResult RenderVideo([FromBody] ExplainerRenderRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
            var jobId = _explainerService.CreateRenderJob(userId, request);
            return Accepted(new { jobId, status = "Pending", message = "Đã tiếp nhận yêu cầu render Explainer Video." });
        }

        /// <summary>
        /// Theo dõi tiến độ render thời gian thực
        /// </summary>
        [HttpGet("jobs/{jobId}")]
        public IActionResult GetJobStatus(string jobId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
            var job = _explainerService.GetJobStatus(jobId, userId);
            if (job == null)
            {
                return NotFound(new { message = "Không tìm thấy tác vụ render." });
            }
            return Ok(job);
        }
    }
}


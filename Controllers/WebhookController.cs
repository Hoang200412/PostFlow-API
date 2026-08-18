using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PostFlow.Data;
using PostFlow.Models;

namespace PostFlow.Controllers
{
    [ApiController]
    [Route("api/webhooks")]
    public class WebhookController : ControllerBase
    {
        private readonly AppDbContext _context;

        public WebhookController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost("platform-callback")]
        public async Task<IActionResult> ReceiveCallback([FromBody] PlatformCallbackRequest request)
        {
            if (string.IsNullOrEmpty(request.JobId) || string.IsNullOrEmpty(request.Status))
            {
                return BadRequest(new { message = "JobId và Status không được để trống." });
            }

            var job = await _context.UploadJobs.FindAsync(request.JobId);
            if (job == null)
            {
                return NotFound(new { message = "Không tìm thấy job tương ứng." });
            }

            if (request.Status.ToLower() == "success")
            {
                job.Status = UploadStatus.Completed;
                job.VideoUrl = request.VideoUrl ?? job.VideoUrl;
                job.CompletedAt = DateTime.UtcNow;
            }
            else
            {
                job.Status = UploadStatus.Failed;
                job.ErrorMessage = request.ErrorMessage ?? "Platform reported posting failure.";
                job.CompletedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Nhận webhook thành công." });
        }
    }

    public class PlatformCallbackRequest
    {
        public string JobId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? VideoUrl { get; set; }
        public string? ErrorMessage { get; set; }
    }
}

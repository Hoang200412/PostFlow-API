using System;
using System.Threading.Tasks;
using PostFlow.Data;
using PostFlow.Exceptions;
using PostFlow.Models;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services
{
    public class WebhookService : IWebhookService
    {
        private readonly AppDbContext _context;

        public WebhookService(AppDbContext context)
        {
            _context = context;
        }

        public async Task ProcessCallbackAsync(PlatformCallbackRequest request)
        {
            if (string.IsNullOrEmpty(request.JobId) || string.IsNullOrEmpty(request.Status))
            {
                throw new BadRequestException("JobId và Status không được để trống.");
            }

            var job = await _context.UploadJobs.FindAsync(request.JobId);
            if (job == null)
            {
                throw new NotFoundException($"Không tìm thấy job tương ứng với Job ID {request.JobId}");
            }

            if (request.Status.Equals("success", StringComparison.OrdinalIgnoreCase))
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
        }
    }
}

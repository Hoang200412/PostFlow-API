using System.Collections.Generic;
using System.Threading.Tasks;
using PostFlow.Models;

namespace PostFlow.Services.Interfaces
{
    public interface IVideoUploadService
    {
        /// <summary>
        /// Tạo một Job upload mới và đưa vào hàng đợi xử lý nền.
        /// </summary>
        Task<UploadJob> EnqueueUploadJobAsync(string userId, string videoAssetId, string socialAccountId, System.DateTime? scheduledAt = null, string? platformMetadataOverride = null);

        /// <summary>
        /// Lấy chi tiết Job theo ID.
        /// </summary>
        Task<UploadJob?> GetJobByIdAsync(string jobId);

        /// <summary>
        /// Lấy danh sách tất cả các Jobs.
        /// </summary>
        Task<IEnumerable<UploadJob>> GetAllJobsAsync();

        /// <summary>
        /// Phương thức được gọi bởi Hangfire để thực thi upload dưới background.
        /// </summary>
        Task ProcessUploadJobAsync(string jobId, System.Threading.CancellationToken cancellationToken);
    }
}

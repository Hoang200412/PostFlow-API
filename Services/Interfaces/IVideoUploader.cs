using System;
using System.Threading;
using System.Threading.Tasks;
using PostFlow.Models;

namespace PostFlow.Services.Interfaces
{
    public interface IVideoUploader
    {
        PlatformType Platform { get; }
        
        Task<UploadResult> UploadAsync(
            string videoFilePath, 
            VideoMetadata metadata, 
            IProgress<int>? progress = null, 
            CancellationToken cancellationToken = default);
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;
using PostFlow.Models;

namespace PostFlow.Services.Interfaces
{
    public interface ISocialPublisher
    {
        PlatformType Platform { get; }
        
        Task<PublishResult> PublishAsync(
            string videoFilePath, 
            VideoMetadata metadata, 
            SocialAccount? account = null,
            IProgress<int>? progress = null, 
            CancellationToken cancellationToken = default);
    }
}

using PostFlow.Models;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services.Interfaces
{
    public interface IPlatformPublisherFactory
    {
        IVideoUploader GetPublisher(PlatformType platform);
    }
}

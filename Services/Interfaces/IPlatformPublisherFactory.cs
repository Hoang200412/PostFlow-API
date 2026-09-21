using PostFlow.Models;

namespace PostFlow.Services.Interfaces
{
    public interface IPlatformPublisherFactory
    {
        ISocialPublisher GetPublisher(PlatformType platform);
    }
}

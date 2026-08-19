using System.Threading.Tasks;
using PostFlow.Models.DTOs.Requests;

namespace PostFlow.Services.Interfaces
{
    public interface IWebhookService
    {
        Task ProcessCallbackAsync(PlatformCallbackRequest request);
    }
}

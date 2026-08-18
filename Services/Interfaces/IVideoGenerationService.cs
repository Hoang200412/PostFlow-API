using System.Threading.Tasks;

namespace PostFlow.Services.Interfaces
{
    public interface IVideoGenerationService
    {
        Task<string> GenerateVideoAsync(string userId, string topic, string contentDnaId);
    }
}

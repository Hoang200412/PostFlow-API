using System.Collections.Generic;
using System.Threading.Tasks;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Models.DTOs.Responses;

namespace PostFlow.Services.Interfaces
{
    public interface IChannelService
    {
        Task<IEnumerable<SocialAccountDto>> GetListAsync(string userId);
        Task<SocialAccountDto> ConnectAsync(string userId, ConnectChannelRequest request);
        Task DisconnectAsync(string userId, string id);
    }
}

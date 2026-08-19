using System.Collections.Generic;
using System.Threading.Tasks;
using PostFlow.Models;
using PostFlow.Models.DTOs.Requests;

namespace PostFlow.Services.Interfaces
{
    public interface IChannelService
    {
        Task<IEnumerable<SocialAccount>> GetListAsync(string userId);
        Task<SocialAccount> ConnectAsync(string userId, ConnectChannelRequest request);
        Task DisconnectAsync(string userId, string id);
    }
}

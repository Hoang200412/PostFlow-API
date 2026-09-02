using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Models.DTOs.Responses;

namespace PostFlow.Services.Interfaces
{
    public interface IContentDnaService
    {
        Task<IEnumerable<ContentDnaDto>> GetListAsync(string userId);
        Task<ContentDnaDto> CreateAsync(string userId, ContentDnaCreateDto dto);
        Task<ContentDnaDto> UpdateAsync(string userId, string id, ContentDnaCreateDto dto);
        Task DeleteAsync(string userId, string id);
        Task<string> UploadAvatarAsync(string userId, IFormFile file);
    }
}

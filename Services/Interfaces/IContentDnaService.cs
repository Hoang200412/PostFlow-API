using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using PostFlow.Models;
using PostFlow.Models.DTOs.Requests;

namespace PostFlow.Services.Interfaces
{
    public interface IContentDnaService
    {
        Task<IEnumerable<ContentDNA>> GetListAsync(string userId);
        Task<ContentDNA> CreateAsync(string userId, ContentDnaCreateDto dto);
        Task<ContentDNA> UpdateAsync(string userId, string id, ContentDnaCreateDto dto);
        Task DeleteAsync(string userId, string id);
        Task<string> UploadAvatarAsync(string userId, IFormFile file);
    }
}

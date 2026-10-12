using System.Collections.Generic;
using System.Threading.Tasks;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Models.DTOs.Responses;

namespace PostFlow.Services.Interfaces;

public interface IPostService
{
    Task<PostDto> GenerateAsync(string userId, GeneratePostRequest request);
    Task<IEnumerable<PostDto>> GetListAsync(string userId);
    Task<PostDto> GetByIdAsync(string userId, string id);
    Task<PostDto> UpdateAsync(string userId, string id, UpdatePostRequest request);
    Task DeleteAsync(string userId, string id);
}

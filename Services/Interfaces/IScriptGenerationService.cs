using System.Threading.Tasks;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Models.DTOs.Responses;

namespace PostFlow.Services.Interfaces;

public interface IScriptGenerationService
{
    Task<ScriptDto> GenerateScriptAsync(string userId, GenerateScriptRequest request);
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Services.Interfaces;

namespace PostFlow.Controllers;

[ApiController]
[Route("api/admin/media-library")]
[Authorize(Roles = "Admin")]
public class AdminMediaLibraryController : ControllerBase
{
    private readonly IMediaLibraryService _service;
    public AdminMediaLibraryController(IMediaLibraryService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] MediaLibraryQueryRequest request, CancellationToken cancellationToken)
        => Ok(await _service.GetAsync(request, cancellationToken));

    [HttpPost("upload")]
    [RequestSizeLimit(250 * 1024 * 1024)]
    public async Task<IActionResult> Upload([FromForm] MediaLibraryUploadRequest request, CancellationToken cancellationToken)
    {
        var adminId = User.FindFirst("sub")?.Value ?? User.FindFirst("id")?.Value ?? User.Identity?.Name ?? "admin";
        return Ok(await _service.UploadAsync(adminId, request, cancellationToken));
    }

    [HttpPut("{id}")]
    public Task<Models.DTOs.Responses.MediaLibraryAssetDto> Update(string id, [FromBody] MediaLibraryMetadataRequest request)
        => _service.UpdateAsync(id, request);

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id) => await _service.DeleteAsync(id) ? NoContent() : NotFound();
}

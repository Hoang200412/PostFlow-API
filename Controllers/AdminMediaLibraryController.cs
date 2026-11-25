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
    private readonly IAuditLogService _auditLog;

    public AdminMediaLibraryController(IMediaLibraryService service, IAuditLogService auditLog)
    {
        _service = service;
        _auditLog = auditLog;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] MediaLibraryQueryRequest request, CancellationToken cancellationToken)
        => Ok(await _service.GetAsync(request, cancellationToken));

    [HttpPost("upload")]
    [RequestSizeLimit(250 * 1024 * 1024)]
    public async Task<IActionResult> Upload([FromForm] MediaLibraryUploadRequest request, CancellationToken cancellationToken)
    {
        var adminId = User.FindFirst("sub")?.Value ?? User.FindFirst("id")?.Value ?? User.Identity?.Name ?? "admin";
        var asset = await _service.UploadAsync(adminId, request, cancellationToken);
        await _auditLog.LogAsync("MEDIA_UPLOAD", "Media", asset.Id, asset.Title, new { after = new { asset.Type, asset.Title } });
        return Ok(asset);
    }

    [HttpPut("{id}")]
    public async Task<Models.DTOs.Responses.MediaLibraryAssetDto> Update(string id, [FromBody] MediaLibraryMetadataRequest request)
    {
        var asset = await _service.UpdateAsync(id, request);
        await _auditLog.LogAsync("MEDIA_UPDATE", "Media", id, asset.Title);
        return asset;
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _service.DeleteAsync(id)) return NotFound();
        await _auditLog.LogAsync("MEDIA_DELETE", "Media", id, id);
        return NoContent();
    }
}

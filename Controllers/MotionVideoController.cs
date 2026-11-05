using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Services.Interfaces;

namespace PostFlow.Controllers;

[Authorize]
[ApiController]
[Route("api/motion-videos")]
public class MotionVideoController : ControllerBase
{
    private readonly IMotionVideoService _service;

    public MotionVideoController(IMotionVideoService service) => _service = service;

    private string? UserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    [HttpGet("stock/search")]
    public async Task<IActionResult> Search([FromQuery] SearchStockVideoRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(UserId)) return Unauthorized();
        return Ok(await _service.SearchStockVideosAsync(request, cancellationToken));
    }

    [HttpPost("render")]
    public IActionResult Render([FromBody] RenderMotionVideoRequest request)
    {
        if (string.IsNullOrWhiteSpace(UserId)) return Unauthorized();
        var jobId = _service.CreateRenderJob(UserId, request);
        return Accepted(new { jobId, status = "Pending" });
    }

    [HttpGet("render/status/{jobId}")]
    public IActionResult Status(string jobId)
    {
        if (string.IsNullOrWhiteSpace(UserId)) return Unauthorized();
        var job = _service.GetJob(jobId, UserId);
        return job == null ? NotFound(new { message = "Không tìm thấy tác vụ video động." }) : Ok(job);
    }

    [HttpPost("music/upload")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadMusic(IFormFile file, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(UserId)) return Unauthorized();
        var objectKey = await _service.UploadMusicAsync(UserId, file, cancellationToken);
        return Ok(new { objectKey });
    }
}

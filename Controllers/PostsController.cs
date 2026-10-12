using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Models.DTOs.Responses;
using PostFlow.Services.Interfaces;

namespace PostFlow.Controllers;

[Authorize]
[ApiController]
[Route("api/posts")]
public class PostsController : ControllerBase
{
    private readonly IPostService _postService;

    public PostsController(IPostService postService)
    {
        _postService = postService;
    }

    private string? CurrentUserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    [HttpPost("generate")]
    public async Task<ActionResult<PostDto>> Generate([FromBody] GeneratePostRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId)) return Unauthorized();
        return Ok(await _postService.GenerateAsync(CurrentUserId, request));
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<PostDto>>> GetList()
    {
        if (string.IsNullOrEmpty(CurrentUserId)) return Unauthorized();
        return Ok(await _postService.GetListAsync(CurrentUserId));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<PostDto>> GetById(string id)
    {
        if (string.IsNullOrEmpty(CurrentUserId)) return Unauthorized();
        return Ok(await _postService.GetByIdAsync(CurrentUserId, id));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<PostDto>> Update(string id, [FromBody] UpdatePostRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId)) return Unauthorized();
        return Ok(await _postService.UpdateAsync(CurrentUserId, id, request));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        if (string.IsNullOrEmpty(CurrentUserId)) return Unauthorized();
        await _postService.DeleteAsync(CurrentUserId, id);
        return NoContent();
    }
}

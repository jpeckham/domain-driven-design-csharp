using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialDDD.Application.Posts;
using SocialDDD.Application.Posts.Commands;
using SocialDDD.Application.Posts.DTOs;
using SocialDDD.Domain.Exceptions;

namespace SocialDDD.Api.Controllers;

[ApiController]
[Route("api/posts")]
public sealed class PostsController(
    PostService postService,
    LikePostCommandHandler likeHandler,
    UnlikePostCommandHandler unlikeHandler) : ControllerBase
{
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePostRequest request, CancellationToken ct)
    {
        try
        {
            var post = await postService.CreateAsync(request, ct);
            return CreatedAtAction(nameof(GetFeed), new { }, post);
        }
        catch (DomainValidationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (DomainException ex) { return Conflict(new { error = ex.Message }); }
    }

    [Authorize]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var requesterId = GetRequesterId();
        if (requesterId is null) return Unauthorized();

        try
        {
            await postService.DeleteAsync(id, requesterId.Value, ct);
            return NoContent();
        }
        catch (DomainException ex)
        {
            return ex.Message.Contains("not found")
                ? NotFound(new { error = ex.Message })
                : Forbid();
        }
    }

    [HttpGet("feed")]
    public async Task<IActionResult> GetFeed(
        [FromQuery] int skip = 0,
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        var requesterId = GetRequesterId();
        var requesterHandle = requesterId.HasValue
            ? await postService.GetHandleByUserIdAsync(requesterId.Value, ct)
            : null;

        var posts = await postService.GetFeedAsync(skip, limit, requesterHandle, ct);
        return Ok(posts);
    }

    [HttpGet("by-user/{userId:guid}")]
    public async Task<IActionResult> GetByUser(Guid userId, CancellationToken ct)
    {
        var requesterId = GetRequesterId();
        var requesterHandle = requesterId.HasValue
            ? await postService.GetHandleByUserIdAsync(requesterId.Value, ct)
            : null;

        var posts = await postService.GetByAuthorAsync(userId, requesterHandle, ct);
        return Ok(posts);
    }

    [Authorize]
    [HttpPost("{postId:guid}/likes")]
    public async Task<IActionResult> LikePost(Guid postId, CancellationToken ct)
    {
        var requesterId = GetRequesterId();
        if (requesterId is null) return Unauthorized();

        try
        {
            var likeCount = await likeHandler.HandleAsync(new LikePostCommand(postId, requesterId.Value), ct);
            return Ok(new { likeCount });
        }
        catch (DomainValidationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (DomainException ex)
        {
            return ex.Message.Contains("not found")
                ? NotFound(new { error = ex.Message })
                : Conflict(new { error = ex.Message });
        }
    }

    [Authorize]
    [HttpDelete("{postId:guid}/likes")]
    public async Task<IActionResult> UnlikePost(Guid postId, CancellationToken ct)
    {
        var requesterId = GetRequesterId();
        if (requesterId is null) return Unauthorized();

        try
        {
            var likeCount = await unlikeHandler.HandleAsync(new UnlikePostCommand(postId, requesterId.Value), ct);
            return Ok(new { likeCount });
        }
        catch (DomainValidationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (DomainException ex)
        {
            return ex.Message.Contains("not found")
                ? NotFound(new { error = ex.Message })
                : Conflict(new { error = ex.Message });
        }
    }

    private Guid? GetRequesterId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}

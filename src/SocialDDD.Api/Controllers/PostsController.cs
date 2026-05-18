using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialDDD.Application.Posts;
using SocialDDD.Application.Posts.Commands;
using SocialDDD.Application.Posts.DTOs;
using SocialDDD.Application.Posts.Queries;
using SocialDDD.Domain.Exceptions;

namespace SocialDDD.Api.Controllers;

[ApiController]
[Route("api/posts")]
public sealed class PostsController(
    PostService postService,
    LikePostCommandHandler likeHandler,
    UnlikePostCommandHandler unlikeHandler,
    CreateReplyCommandHandler createReplyHandler,
    CreateRepostCommandHandler createRepostHandler,
    DeleteRepostCommandHandler deleteRepostHandler,
    GetPostWithConversationQueryHandler conversationHandler) : ControllerBase
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
    [HttpPost("{postId:guid}/replies")]
    public async Task<IActionResult> CreateReply(Guid postId, [FromBody] CreateReplyRequest request, CancellationToken ct)
    {
        var requesterId = GetRequesterId();
        if (requesterId is null) return Unauthorized();

        try
        {
            var command = new CreateReplyCommand(postId, requesterId.Value, request.Content);
            var reply = await createReplyHandler.HandleAsync(command, ct);
            return CreatedAtAction(nameof(GetPost), new { postId = reply.PostId }, reply);
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
    [HttpPost("{postId:guid}/reposts")]
    public async Task<IActionResult> CreateRepost(Guid postId, [FromBody] CreateRepostRequest request, CancellationToken ct)
    {
        var requesterId = GetRequesterId();
        if (requesterId is null) return Unauthorized();

        try
        {
            var command = new CreateRepostCommand(postId, requesterId.Value, request.Commentary);
            var repost = await createRepostHandler.HandleAsync(command, ct);
            return CreatedAtAction(nameof(GetPost), new { postId = repost.PostId }, repost);
        }
        catch (DuplicateRepostException ex) { return Conflict(new { error = ex.Message }); }
        catch (DomainValidationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (DomainException ex)
        {
            return ex.Message.Contains("not found")
                ? NotFound(new { error = ex.Message })
                : BadRequest(new { error = ex.Message });
        }
    }

    [Authorize]
    [HttpDelete("{postId:guid}/reposts/mine")]
    public async Task<IActionResult> DeleteRepost(Guid postId, CancellationToken ct)
    {
        var requesterId = GetRequesterId();
        if (requesterId is null) return Unauthorized();

        try
        {
            await deleteRepostHandler.HandleAsync(new DeleteRepostCommand(postId, requesterId.Value), ct);
            return Ok();
        }
        catch (DomainException ex)
        {
            return ex.Message.Contains("not found")
                ? NotFound(new { error = ex.Message })
                : Conflict(new { error = ex.Message });
        }
    }

    [HttpGet("{postId:guid}")]
    public async Task<IActionResult> GetPost(Guid postId, [FromQuery] int depthLimit = 3, [FromQuery] int repliesPerLevel = 100, CancellationToken ct = default)
    {
        var requesterId = GetRequesterId();
        var requesterHandle = requesterId.HasValue
            ? await postService.GetHandleByUserIdAsync(requesterId.Value, ct)
            : null;

        try
        {
            var query = new GetPostWithConversationQuery(postId, depthLimit, repliesPerLevel);
            var result = await conversationHandler.HandleAsync(query, requesterHandle, requesterId, ct);
            return Ok(result);
        }
        catch (DomainException ex)
        {
            return ex.Message.Contains("not found")
                ? NotFound(new { error = ex.Message })
                : Conflict(new { error = ex.Message });
        }
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
        [FromQuery] bool rootOnly = false,
        CancellationToken ct = default)
    {
        var requesterId = GetRequesterId();
        var posts = await postService.GetFeedAsync(skip, limit, requesterId, rootOnly, ct);
        return Ok(posts);
    }

    [HttpGet("by-user/{userId:guid}")]
    public async Task<IActionResult> GetByUser(Guid userId, CancellationToken ct)
    {
        var requesterId = GetRequesterId();
        var posts = await postService.GetByAuthorAsync(userId, requesterId, ct);
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
        catch (AlreadyLikedException ex) { return Conflict(new { error = ex.Message }); }
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
        catch (NotLikedException ex) { return NotFound(new { error = ex.Message }); }
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

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialDDD.Application.Posts.Commands;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Posts;

namespace SocialDDD.Api.Controllers;

[ApiController]
public sealed class PostMediaController(
    BeginPostMediaUploadCommandHandler beginHandler,
    CompletePostMediaUploadCommandHandler completeHandler,
    IPostMediaStorageService storageService) : ControllerBase
{
    [Authorize]
    [HttpPost("api/posts/media/upload-sessions")]
    public async Task<IActionResult> BeginUpload(
        [FromBody] BeginPostMediaUploadRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        try
        {
            var (assetId, uploadUrl) = await beginHandler.HandleAsync(
                new BeginPostMediaUploadCommand(userId.Value, request.ContentType), ct);
            return Ok(new { assetId, uploadUrl });
        }
        catch (DomainValidationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [Authorize]
    [HttpPut("api/media/uploads/post/{assetId:guid}")]
    public async Task<IActionResult> StoreUpload(Guid assetId, CancellationToken ct)
    {
        var contentType = Request.ContentType ?? "application/octet-stream";
        await storageService.StoreAsync(assetId, assetId.ToString(), Request.Body, contentType, ct);
        return Ok();
    }

    [Authorize]
    [HttpPost("api/posts/media/{assetId:guid}/complete")]
    public async Task<IActionResult> CompleteUpload(
        Guid assetId, [FromBody] CompletePostMediaUploadRequest request, CancellationToken ct)
    {
        if (GetUserId() is null) return Unauthorized();

        try
        {
            var dto = await completeHandler.HandleAsync(new CompletePostMediaUploadCommand(
                assetId,
                request.ContentType,
                request.ByteLength,
                request.Width,
                request.Height,
                request.DurationMs,
                request.AltText), ct);
            return Ok(dto);
        }
        catch (DomainValidationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (DomainException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpGet("api/post-media/{assetId:guid}")]
    public async Task<IActionResult> ServeMedia(Guid assetId, CancellationToken ct)
    {
        try
        {
            var contentType = await storageService.GetContentTypeAsync(assetId.ToString(), ct)
                ?? "application/octet-stream";
            var stream = await storageService.LoadAsync(assetId.ToString(), ct);
            return File(stream, contentType);
        }
        catch (FileNotFoundException) { return NotFound(); }
    }

    private Guid? GetUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    public sealed record BeginPostMediaUploadRequest(string ContentType);

    public sealed record CompletePostMediaUploadRequest(
        string ContentType,
        long ByteLength,
        int? Width,
        int? Height,
        long? DurationMs,
        string? AltText);
}

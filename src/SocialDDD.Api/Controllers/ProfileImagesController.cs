using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialDDD.Application.Users.Commands;
using SocialDDD.Application.Users.Queries;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Users;

namespace SocialDDD.Api.Controllers;

[ApiController]
public sealed class ProfileImagesController(
    BeginProfileImageUploadCommandHandler beginHandler,
    CompleteProfileImageUploadCommandHandler completeHandler,
    RemoveProfileImageCommandHandler removeHandler,
    GetProfileImageQueryHandler serveHandler,
    IProfileImageStorageService storageService) : ControllerBase
{
    [Authorize]
    [HttpPost("api/users/me/profile-image/upload-sessions")]
    public async Task<IActionResult> BeginUpload([FromBody] BeginUploadRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        try
        {
            var (assetId, uploadUrl) = await beginHandler.HandleAsync(
                new BeginProfileImageUploadCommand(userId.Value, request.ContentType), ct);
            return Ok(new { assetId, uploadUrl });
        }
        catch (DomainValidationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (DomainException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpPut("api/media/uploads/profile/{assetId:guid}")]
    public async Task<IActionResult> StoreUpload(Guid assetId, CancellationToken ct)
    {
        var contentType = Request.ContentType ?? "application/octet-stream";
        await storageService.StoreAsync(assetId, assetId.ToString(), Request.Body, contentType, ct);
        return Ok();
    }

    [Authorize]
    [HttpPost("api/users/me/profile-image/complete")]
    public async Task<IActionResult> CompleteUpload([FromBody] CompleteUploadRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        try
        {
            await completeHandler.HandleAsync(new CompleteProfileImageUploadCommand(
                userId.Value, request.AssetId, request.ContentType, request.ByteLength, request.Width, request.Height), ct);
            return NoContent();
        }
        catch (DomainValidationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (DomainException ex) { return NotFound(new { error = ex.Message }); }
    }

    [Authorize]
    [HttpDelete("api/users/me/profile-image")]
    public async Task<IActionResult> RemoveProfileImage(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        try
        {
            await removeHandler.HandleAsync(new RemoveProfileImageCommand(userId.Value), ct);
            return NoContent();
        }
        catch (DomainValidationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (DomainException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpGet("api/profile-images/{assetId:guid}")]
    public async Task<IActionResult> ServeImage(Guid assetId, CancellationToken ct)
    {
        try
        {
            var (stream, contentType) = await serveHandler.HandleAsync(new GetProfileImageQuery(assetId), ct);
            return File(stream, contentType);
        }
        catch (DomainException) { return NotFound(); }
    }

    private Guid? GetUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    public sealed record BeginUploadRequest(string ContentType);

    public sealed record CompleteUploadRequest(
        Guid AssetId, string ContentType, long ByteLength, int? Width, int? Height);
}

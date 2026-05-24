using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialDDD.Application.Social.Follows;
using SocialDDD.Domain.Social.Blocks;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Identity.Users;
using SocialDDD.Domain.Social.Profiles;

namespace SocialDDD.Api.Controllers;

[ApiController]
[Authorize]
public sealed class UserRelationshipsController(
    IUserRepository userRepository,
    FollowService followService,
    IBlockRepository blockRepository) : ControllerBase
{
    [HttpPost("api/users/{handle}/follows")]
    [HttpPost("api/users/by-handle/{handle}/follow")]
    public async Task<IActionResult> Follow(string handle, CancellationToken ct)
    {
        var requester = await GetRequesterAsync(ct);
        if (requester is null) return Unauthorized();

        try
        {
            var target = await GetTargetAsync(handle, ct);
            await followService.FollowAsync(requester.Handle, target.Handle, ct);
            return NoContent();
        }
        catch (DomainValidationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (DomainException ex) { return ToRelationshipError(ex); }
    }

    [HttpDelete("api/users/{handle}/follows")]
    [HttpDelete("api/users/by-handle/{handle}/follow")]
    public async Task<IActionResult> Unfollow(string handle, CancellationToken ct)
    {
        var requester = await GetRequesterAsync(ct);
        if (requester is null) return Unauthorized();

        try
        {
            var target = await GetTargetAsync(handle, ct);
            await followService.UnfollowAsync(requester.Handle, target.Handle, ct);
            return NoContent();
        }
        catch (DomainValidationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (DomainException ex) { return ToRelationshipError(ex); }
    }

    [HttpPost("api/users/{handle}/blocks")]
    [HttpPost("api/users/by-handle/{handle}/block")]
    public async Task<IActionResult> Block(string handle, CancellationToken ct)
    {
        var requester = await GetRequesterAsync(ct);
        if (requester is null) return Unauthorized();

        try
        {
            var target = await GetTargetAsync(handle, ct);
            var existing = await blockRepository.FindAsync(requester.Handle, target.Handle, ct);
            if (existing is null)
                await blockRepository.SaveAsync(SocialDDD.Domain.Social.Blocks.Block.Create(requester.Handle, target.Handle), ct);
            await followService.UnfollowAsync(requester.Handle, target.Handle, ct);
            await followService.UnfollowAsync(target.Handle, requester.Handle, ct);
            return NoContent();
        }
        catch (DomainValidationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (DomainException ex) { return ToRelationshipError(ex); }
    }

    [HttpDelete("api/users/{handle}/blocks")]
    [HttpDelete("api/users/by-handle/{handle}/block")]
    public async Task<IActionResult> Unblock(string handle, CancellationToken ct)
    {
        var requester = await GetRequesterAsync(ct);
        if (requester is null) return Unauthorized();

        try
        {
            var target = await GetTargetAsync(handle, ct);
            var existing = await blockRepository.FindAsync(requester.Handle, target.Handle, ct);
            if (existing is not null)
                await blockRepository.DeleteAsync(existing, ct);
            return NoContent();
        }
        catch (DomainValidationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (DomainException ex) { return ToRelationshipError(ex); }
    }

    private async Task<User?> GetRequesterAsync(CancellationToken ct)
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id)
            ? await userRepository.GetByIdAsync(UserId.From(id), ct)
            : null;
    }

    private async Task<User> GetTargetAsync(string rawHandle, CancellationToken ct)
    {
        var handle = new Handle(rawHandle);
        return await userRepository.FindByHandleAsync(handle, ct)
            ?? throw new DomainException($"User with handle @{handle.Value} not found.");
    }

    private ObjectResult ToRelationshipError(DomainException ex) =>
        ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase)
            ? NotFound(new { error = ex.Message })
            : BadRequest(new { error = ex.Message });
}

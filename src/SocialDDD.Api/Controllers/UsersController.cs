using SocialDDD.Domain.Social.Profiles;
using SocialDDD.Application.Social.Profiles.Queries;
using SocialDDD.Application.Social.Profiles.DTOs;
using SocialDDD.Application.Social.Profiles.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialDDD.Application.Identity.Accounts;
using SocialDDD.Application.Identity.Accounts.DTOs;
using SocialDDD.Domain.Exceptions;

namespace SocialDDD.Api.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController(UserService userService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        try
        {
            var response = await userService.RegisterAsync(request, ct);
            return CreatedAtAction(nameof(GetById), new { id = response.UserId }, response);
        }
        catch (DomainValidationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (DomainException ex) { return Conflict(new { error = ex.Message }); }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        try
        {
            var response = await userService.LoginAsync(request, ct);
            return Ok(response);
        }
        catch (DomainValidationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (DomainException ex) { return Unauthorized(new { error = ex.Message }); }
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetMe(CancellationToken ct)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { error = "Invalid token." });

        try
        {
            var user = await userService.GetByIdAsync(userId, ct);
            return Ok(user);
        }
        catch (DomainException ex) { return NotFound(new { error = ex.Message }); }
    }

    [Authorize]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        try
        {
            var user = await userService.GetByIdAsync(id, ct);
            return Ok(user);
        }
        catch (DomainException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpGet("by-handle/{handle}")]
    public async Task<IActionResult> GetByHandle(string handle, CancellationToken ct)
    {
        var requesterId = GetUserId();
        try
        {
            var user = await userService.GetProfileByHandleAsync(handle, requesterId, ct);
            return Ok(user);
        }
        catch (DomainValidationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (DomainException ex) { return NotFound(new { error = ex.Message }); }
    }

    [Authorize]
    [HttpPut("me/display-name")]
    public async Task<IActionResult> UpdateDisplayName([FromBody] UpdateDisplayNameRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized(new { error = "Invalid token." });

        try
        {
            await userService.UpdateDisplayNameAsync(userId.Value, request, ct);
            return NoContent();
        }
        catch (DomainValidationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (DomainException ex) { return NotFound(new { error = ex.Message }); }
    }

    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}

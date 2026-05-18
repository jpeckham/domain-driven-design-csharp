using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialDDD.Application.Users;
using SocialDDD.Application.Users.DTOs;
using SocialDDD.Domain.Exceptions;

namespace SocialDDD.Api.Controllers;

/// <summary>
/// POST /api/accounts — dev-convenience path: creates an Active user immediately (no email verification).
/// </summary>
[ApiController]
[Route("api/accounts")]
public sealed class AccountsController(UserService userService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] RegisterRequest request, CancellationToken ct)
    {
        try
        {
            var response = await userService.RegisterAsync(request, ct);
            return CreatedAtAction(nameof(GetById), new { id = response.UserId }, response);
        }
        catch (DomainValidationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (DomainException ex) { return Conflict(new { error = ex.Message }); }
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
}

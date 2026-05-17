using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialDDD.Application.Users;
using SocialDDD.Application.Users.DTOs;
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
        catch (DomainException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        try
        {
            var response = await userService.LoginAsync(request, ct);
            return Ok(response);
        }
        catch (DomainException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
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
        catch (DomainException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}

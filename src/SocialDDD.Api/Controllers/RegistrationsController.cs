using Microsoft.AspNetCore.Mvc;
using SocialDDD.Application.Identity.Accounts.Commands;
using SocialDDD.Application.Identity.Accounts.DTOs;
using SocialDDD.Domain.Exceptions;

namespace SocialDDD.Api.Controllers;

[ApiController]
[Route("api/registrations")]
public sealed class RegistrationsController(
    RegisterPendingUserCommand registerCommand,
    VerifyRegistrationCommand verifyCommand) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegisterPendingRequest request, CancellationToken ct)
    {
        try
        {
            await registerCommand.ExecuteAsync(request, ct);
            return Accepted(new { message = "Registration started. Check your email for a verification code." });
        }
        catch (DomainValidationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (DomainException ex) { return Conflict(new { error = ex.Message }); }
    }

    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] VerifyRegistrationRequest request, CancellationToken ct)
    {
        try
        {
            var response = await verifyCommand.ExecuteAsync(request, ct);
            return Ok(response);
        }
        catch (DomainValidationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (DomainException ex) { return BadRequest(new { error = ex.Message }); }
    }
}

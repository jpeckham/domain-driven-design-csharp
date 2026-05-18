using Microsoft.AspNetCore.Mvc;
using SocialDDD.Application.Users.Commands;
using SocialDDD.Domain.Exceptions;

namespace SocialDDD.Api.Controllers;

[ApiController]
public sealed class PasswordResetController(
    RequestPasswordResetCommand requestResetCommand,
    ResetPasswordCommand resetPasswordCommand) : ControllerBase
{
    [HttpPost("api/password-reset-requests")]
    public async Task<IActionResult> RequestReset([FromBody] PasswordResetRequestDto request, CancellationToken ct)
    {
        await requestResetCommand.ExecuteAsync(request.Email, ct);
        return Accepted(new { message = "If that email address is registered, a password reset link has been sent." });
    }

    [HttpPost("api/password-resets")]
    public async Task<IActionResult> ResetPassword([FromBody] PasswordResetDto request, CancellationToken ct)
    {
        try
        {
            await resetPasswordCommand.ExecuteAsync(request.Token, request.NewPassword, ct);
            return Ok(new { message = "Password has been reset successfully. Please log in with your new password." });
        }
        catch (DomainValidationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (DomainException ex) { return BadRequest(new { error = ex.Message }); }
    }
}

public sealed record PasswordResetRequestDto(string Email);
public sealed record PasswordResetDto(string Token, string NewPassword);

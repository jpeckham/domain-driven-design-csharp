using Microsoft.AspNetCore.Mvc;
using SocialDDD.Application.Identity.Accounts.Commands;
using SocialDDD.Application.Identity.Accounts.DTOs;
using SocialDDD.Domain.Exceptions;

namespace SocialDDD.Api.Controllers;

[ApiController]
[Route("api/sessions")]
public sealed class SessionsController(
    LoginWithDeviceCommand loginWithDeviceCommand,
    VerifyDeviceOtpCommand verifyDeviceOtpCommand) : ControllerBase
{
    [HttpPost("device")]
    public async Task<IActionResult> LoginWithDevice([FromBody] LoginWithDeviceRequest request, CancellationToken ct)
    {
        try
        {
            var result = await loginWithDeviceCommand.ExecuteAsync(request, ct);
            return result switch
            {
                LoginWithDeviceResult.Success s => Ok(new TokenResponse(s.Token, s.UserId, s.Username)),
                LoginWithDeviceResult.OtpRequired => Accepted(new { message = "OTP sent to your email address." }),
                _ => StatusCode(500)
            };
        }
        catch (DomainValidationException ex) { return Unauthorized(new { error = ex.Message }); }
        catch (DomainException ex) { return Unauthorized(new { error = ex.Message }); }
    }

    [HttpPost("device/verify")]
    public async Task<IActionResult> VerifyDeviceOtp([FromBody] VerifyDeviceOtpRequest request, CancellationToken ct)
    {
        try
        {
            var response = await verifyDeviceOtpCommand.ExecuteAsync(request, ct);
            return Ok(response);
        }
        catch (DomainValidationException ex) { return Unauthorized(new { error = ex.Message }); }
        catch (DomainException ex) { return Unauthorized(new { error = ex.Message }); }
    }
}

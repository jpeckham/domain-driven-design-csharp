using Microsoft.Extensions.Logging;
using SocialDDD.Application.Interfaces;

namespace SocialDDD.Infrastructure.Emails;

internal sealed class ConsoleEmailService(ILogger<ConsoleEmailService> logger) : IEmailService
{
    public Task SendVerificationEmailAsync(string toEmail, string code, CancellationToken ct = default)
    {
        logger.LogInformation("Verification email to {Email}: code = {Code}", toEmail, code);
        return Task.CompletedTask;
    }

    public Task SendOtpEmailAsync(string toEmail, string otp, CancellationToken ct = default)
    {
        logger.LogInformation("OTP email to {Email}: otp = {Otp}", toEmail, otp);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetEmailAsync(string toEmail, string token, CancellationToken ct = default)
    {
        logger.LogInformation("Password reset email to {Email}: token = {Token}", toEmail, token);
        return Task.CompletedTask;
    }
}

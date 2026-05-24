using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SocialDDD.Application.Interfaces;

namespace SocialDDD.Infrastructure.Identity.Emails;

internal sealed class ConsoleEmailService(
    ILogger<ConsoleEmailService> logger,
    IConfiguration configuration) : IEmailService
{
    private const string DefaultClientBaseUrl = "http://localhost:5200";

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
        var clientBaseUrl = (configuration["Client:BaseUrl"] ?? DefaultClientBaseUrl).TrimEnd('/');
        var resetLink = $"{clientBaseUrl}/reset-password?token={Uri.EscapeDataString(token)}";

        logger.LogInformation("Password reset email to {Email}: reset link = {ResetLink}; token = {Token}", toEmail, resetLink, token);
        return Task.CompletedTask;
    }
}

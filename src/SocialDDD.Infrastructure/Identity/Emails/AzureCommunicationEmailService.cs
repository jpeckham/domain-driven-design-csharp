using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Options;
using SocialDDD.Application.Interfaces;

namespace SocialDDD.Infrastructure.Identity.Emails;

internal sealed class AzureCommunicationEmailService(IOptions<AcsEmailOptions> options) : IEmailService
{
    public Task SendVerificationEmailAsync(string toEmail, string code, CancellationToken ct = default) =>
        SendAsync(
            toEmail,
            "Verify your SocialDDD account",
            $"Your verification code is {code}.",
            ct);

    public Task SendOtpEmailAsync(string toEmail, string otp, CancellationToken ct = default) =>
        SendAsync(
            toEmail,
            "Your SocialDDD sign-in code",
            $"Your sign-in code is {otp}.",
            ct);

    public Task SendPasswordResetEmailAsync(string toEmail, string token, CancellationToken ct = default) =>
        SendAsync(
            toEmail,
            "Reset your SocialDDD password",
            $"Use this password reset token: {token}",
            ct);

    private async Task SendAsync(string toEmail, string subject, string body, CancellationToken ct)
    {
        var value = options.Value;
        if (string.IsNullOrWhiteSpace(value.ConnectionString))
            throw new InvalidOperationException("AcsEmail:ConnectionString is not configured.");
        if (string.IsNullOrWhiteSpace(value.SenderAddress))
            throw new InvalidOperationException("AcsEmail:SenderAddress is not configured.");

        var client = new EmailClient(value.ConnectionString);
        await client.SendAsync(
            WaitUntil.Started,
            value.SenderAddress,
            toEmail,
            subject,
            body,
            cancellationToken: ct);
    }
}

internal sealed class AcsEmailOptions
{
    public const string SectionName = "AcsEmail";

    public string ConnectionString { get; init; } = string.Empty;
    public string SenderAddress { get; init; } = string.Empty;
}

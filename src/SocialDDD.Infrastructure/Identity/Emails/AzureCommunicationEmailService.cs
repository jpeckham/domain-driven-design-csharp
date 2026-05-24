using SocialDDD.Application.Interfaces;

namespace SocialDDD.Infrastructure.Identity.Emails;

internal sealed class AzureCommunicationEmailService : IEmailService
{
    public Task SendVerificationEmailAsync(string toEmail, string code, CancellationToken ct = default)
    {
        throw new NotImplementedException("AzureCommunicationEmailService is not yet implemented.");
    }

    public Task SendOtpEmailAsync(string toEmail, string otp, CancellationToken ct = default)
    {
        throw new NotImplementedException("AzureCommunicationEmailService is not yet implemented.");
    }

    public Task SendPasswordResetEmailAsync(string toEmail, string token, CancellationToken ct = default)
    {
        throw new NotImplementedException("AzureCommunicationEmailService is not yet implemented.");
    }
}

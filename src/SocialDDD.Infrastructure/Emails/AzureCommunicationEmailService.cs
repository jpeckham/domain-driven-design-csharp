using SocialDDD.Application.Interfaces;

namespace SocialDDD.Infrastructure.Emails;

internal sealed class AzureCommunicationEmailService : IEmailService
{
    public Task SendVerificationEmailAsync(string toEmail, string code, CancellationToken ct = default)
    {
        throw new NotImplementedException("AzureCommunicationEmailService is not yet implemented.");
    }
}

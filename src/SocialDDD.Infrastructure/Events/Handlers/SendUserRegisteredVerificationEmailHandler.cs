using System.Security.Cryptography;
using SocialDDD.Application.Interfaces;
using SocialDDD.Domain.Users;
using SocialDDD.Domain.Users.Events;

namespace SocialDDD.Infrastructure.Events.Handlers;

internal sealed class SendUserRegisteredVerificationEmailHandler(
    IVerificationCodeRepository codeRepository,
    IEmailService emailService) : IDomainEventHandler<UserRegistered>
{
    public async Task HandleAsync(UserRegistered domainEvent, CancellationToken ct = default)
    {
        var code = new VerificationCode(
            GenerateCode(),
            DateTimeOffset.UtcNow.AddMinutes(15));

        await codeRepository.SaveAsync(domainEvent.UserId, code, ct);
        await emailService.SendVerificationEmailAsync(domainEvent.Email.Value, code.Code, ct);
    }

    private static string GenerateCode()
    {
        var value = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return value.ToString("D6");
    }
}

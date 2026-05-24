using SocialDDD.Application.Interfaces;
using SocialDDD.Domain.Identity.Users.Events;

namespace SocialDDD.Infrastructure.Events.Handlers;

internal sealed class SendLoginChallengedEmailHandler(
    IEmailService emailService) : IDomainEventHandler<LoginChallenged>
{
    public Task HandleAsync(LoginChallenged domainEvent, CancellationToken ct = default) =>
        emailService.SendOtpEmailAsync(domainEvent.Email.Value, domainEvent.Otp, ct);
}

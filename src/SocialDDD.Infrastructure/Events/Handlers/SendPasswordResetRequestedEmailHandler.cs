using SocialDDD.Application.Interfaces;
using SocialDDD.Domain.Users.Events;

namespace SocialDDD.Infrastructure.Events.Handlers;

internal sealed class SendPasswordResetRequestedEmailHandler(
    IEmailService emailService) : IDomainEventHandler<PasswordResetRequested>
{
    public Task HandleAsync(PasswordResetRequested domainEvent, CancellationToken ct = default) =>
        emailService.SendPasswordResetEmailAsync(domainEvent.Email.Value, domainEvent.Token, ct);
}

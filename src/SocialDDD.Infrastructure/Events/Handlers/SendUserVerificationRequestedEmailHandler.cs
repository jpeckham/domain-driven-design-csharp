using SocialDDD.Application.Interfaces;
using SocialDDD.Domain.Identity.Users.Events;

namespace SocialDDD.Infrastructure.Events.Handlers;

internal sealed class SendUserVerificationRequestedEmailHandler(
    IEmailService emailService) : IDomainEventHandler<UserVerificationRequested>
{
    public Task HandleAsync(UserVerificationRequested domainEvent, CancellationToken ct = default) =>
        emailService.SendVerificationEmailAsync(domainEvent.Email.Value, domainEvent.Code, ct);
}

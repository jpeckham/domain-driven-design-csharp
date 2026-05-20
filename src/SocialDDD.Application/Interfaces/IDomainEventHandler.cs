using SocialDDD.Domain.Primitives;

namespace SocialDDD.Application.Interfaces;

public interface IDomainEventHandler<in TDomainEvent>
    where TDomainEvent : IDomainEvent
{
    Task HandleAsync(TDomainEvent domainEvent, CancellationToken ct = default);
}

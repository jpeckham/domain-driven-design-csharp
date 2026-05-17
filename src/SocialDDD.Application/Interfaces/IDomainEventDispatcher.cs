using SocialDDD.Domain.Primitives;

namespace SocialDDD.Application.Interfaces;

public interface IDomainEventDispatcher
{
    Task DispatchAsync(IReadOnlyList<IDomainEvent> domainEvents, CancellationToken ct = default);
}

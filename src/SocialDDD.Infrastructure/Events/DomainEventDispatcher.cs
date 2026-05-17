using SocialDDD.Application.Interfaces;
using SocialDDD.Domain.Primitives;

namespace SocialDDD.Infrastructure.Events;

internal sealed class DomainEventDispatcher : IDomainEventDispatcher
{
    // Synchronous in-process dispatch.
    // Extend by registering IDomainEventHandler<T> implementations in DI.
    public Task DispatchAsync(IReadOnlyList<IDomainEvent> domainEvents, CancellationToken ct = default)
    {
        // No handlers wired yet — events are raised and consumed for traceability.
        // Add: services.AddScoped<IDomainEventHandler<UserRegistered>, SendWelcomeEmailHandler>()
        return Task.CompletedTask;
    }
}

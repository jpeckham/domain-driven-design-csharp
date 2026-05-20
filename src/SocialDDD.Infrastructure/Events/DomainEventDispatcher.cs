using SocialDDD.Application.Interfaces;
using SocialDDD.Domain.Primitives;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SocialDDD.Infrastructure.Events;

internal sealed class DomainEventDispatcher(
    IServiceScopeFactory scopeFactory,
    ILogger<DomainEventDispatcher>? logger = null) : IDomainEventDispatcher
{
    public Task DispatchAsync(IReadOnlyList<IDomainEvent> domainEvents, CancellationToken ct = default)
    {
        if (domainEvents.Count == 0)
            return Task.CompletedTask;

        foreach (var domainEvent in domainEvents.ToArray())
            _ = Task.Run(() => DispatchEventAsync(domainEvent), CancellationToken.None);

        return Task.CompletedTask;
    }

    private async Task DispatchEventAsync(IDomainEvent domainEvent)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());
            var handlers = scope.ServiceProvider.GetServices(handlerType).OfType<object>().ToArray();

            foreach (var handler in handlers)
            {
                var task = (Task?)handlerType.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.HandleAsync))
                    ?.Invoke(handler, [domainEvent, CancellationToken.None]);
                if (task is not null)
                    await task.ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Domain event handler failed for {DomainEventType}.", domainEvent.GetType().Name);
        }
    }
}

using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SocialDDD.Application.Interfaces;
using SocialDDD.Domain.Primitives;
using SocialDDD.Infrastructure;

namespace SocialDDD.Domain.Tests;

public sealed class DomainEventDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_InvokesRegisteredHandler()
    {
        TestEventHandler.Reset();
        var services = new ServiceCollection();
        services.AddInfrastructure(new ConfigurationBuilder().Build());
        services.AddSingleton<IDomainEventHandler<TestEvent>, TestEventHandler>();
        await using var provider = services.BuildServiceProvider();

        var dispatcher = provider.GetRequiredService<IDomainEventDispatcher>();

        await dispatcher.DispatchAsync([new TestEvent("hello")]);

        var completed = await Task.WhenAny(TestEventHandler.Handled.Task, Task.Delay(TimeSpan.FromSeconds(3)));
        completed.Should().Be(TestEventHandler.Handled.Task);
        (await TestEventHandler.Handled.Task).Should().Be("hello");
    }

    private sealed record TestEvent(string Message) : IDomainEvent;

    private sealed class TestEventHandler : IDomainEventHandler<TestEvent>
    {
        public static TaskCompletionSource<string> Handled { get; private set; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public static void Reset() =>
            Handled = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task HandleAsync(TestEvent domainEvent, CancellationToken ct = default)
        {
            Handled.SetResult(domainEvent.Message);
            return Task.CompletedTask;
        }
    }
}

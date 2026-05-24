using FluentAssertions;
using SocialDDD.Application.Social.Follows;
using SocialDDD.Application.Interfaces;
using SocialDDD.Domain.Social.Blocks;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Social.Follows;
using SocialDDD.Domain.Primitives;
using SocialDDD.Domain.Identity.Users;

namespace SocialDDD.Domain.Tests;

public class FollowTests
{
    [Fact]
    public void Create_SameFollowerAndFollowed_ThrowsDomainException()
    {
        var handle = new Handle("alice");

        var act = () => Follow.Create(handle, handle);

        act.Should().Throw<DomainException>()
            .WithMessage("Users cannot follow themselves.");
    }

    [Fact]
    public async Task FollowAsync_NewRelationship_SavesFollow()
    {
        var repository = new FakeFollowRepository();
        var service = new FollowService(repository, new FollowDomainService(new FakeBlockRepository()), new NoOpDomainEventDispatcher());

        await service.FollowAsync(new Handle("alice"), new Handle("bob"));

        (await repository.IsFollowingAsync(new Handle("alice"), new Handle("bob"))).Should().BeTrue();
        (await repository.CountFollowersAsync(new Handle("bob"))).Should().Be(1);
        (await repository.CountFollowingAsync(new Handle("alice"))).Should().Be(1);
    }

    [Fact]
    public async Task UnfollowAsync_ExistingRelationship_RemovesFollow()
    {
        var repository = new FakeFollowRepository();
        await repository.SaveAsync(Follow.Create(new Handle("alice"), new Handle("bob")));
        var service = new FollowService(repository, new FollowDomainService(new FakeBlockRepository()), new NoOpDomainEventDispatcher());

        await service.UnfollowAsync(new Handle("alice"), new Handle("bob"));

        (await repository.IsFollowingAsync(new Handle("alice"), new Handle("bob"))).Should().BeFalse();
    }

    [Fact]
    public async Task FollowAsync_WhenEitherUserBlocked_ThrowsDomainException()
    {
        var repository = new FakeFollowRepository();
        var blocks = new FakeBlockRepository([Block.Create(new Handle("alice"), new Handle("bob"))]);
        var service = new FollowService(repository, new FollowDomainService(blocks), new NoOpDomainEventDispatcher());

        var act = async () => await service.FollowAsync(new Handle("bob"), new Handle("alice"));

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("Cannot follow a user when either user has blocked the other.");
        (await repository.IsFollowingAsync(new Handle("bob"), new Handle("alice"))).Should().BeFalse();
    }

    [Fact]
    public async Task EnsureCanFollowAsync_WhenEitherUserBlocked_ThrowsDomainException()
    {
        var blocks = new FakeBlockRepository([Block.Create(new Handle("alice"), new Handle("bob"))]);
        var domainService = new FollowDomainService(blocks);

        var act = async () => await domainService.EnsureCanFollowAsync(new Handle("bob"), new Handle("alice"));

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("Cannot follow a user when either user has blocked the other.");
    }

    private sealed class FakeFollowRepository : IFollowRepository
    {
        private readonly List<Follow> _follows = [];

        public Task SaveAsync(Follow follow, CancellationToken ct = default)
        {
            if (!_follows.Any(f => f.FollowerHandle == follow.FollowerHandle && f.FollowedHandle == follow.FollowedHandle))
                _follows.Add(follow);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Follow follow, CancellationToken ct = default)
        {
            _follows.RemoveAll(f => f.FollowerHandle == follow.FollowerHandle && f.FollowedHandle == follow.FollowedHandle);
            return Task.CompletedTask;
        }

        public Task<Follow?> FindAsync(Handle follower, Handle followed, CancellationToken ct = default) =>
            Task.FromResult(_follows.FirstOrDefault(f => f.FollowerHandle == follower && f.FollowedHandle == followed));

        public Task<bool> IsFollowingAsync(Handle follower, Handle followed, CancellationToken ct = default) =>
            Task.FromResult(_follows.Any(f => f.FollowerHandle == follower && f.FollowedHandle == followed));
        public Task<IReadOnlyList<Handle>> GetFollowedHandlesAsync(Handle follower, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Handle>>(_follows.Where(f => f.FollowerHandle == follower).Select(f => f.FollowedHandle).ToList());

        public Task<int> CountFollowersAsync(Handle followed, CancellationToken ct = default) =>
            Task.FromResult(_follows.Count(f => f.FollowedHandle == followed));

        public Task<int> CountFollowingAsync(Handle follower, CancellationToken ct = default) =>
            Task.FromResult(_follows.Count(f => f.FollowerHandle == follower));
    }

    private sealed class FakeBlockRepository(IReadOnlyList<Block>? blocks = null) : IBlockRepository
    {
        private readonly IReadOnlyList<Block> _blocks = blocks ?? [];

        public Task SaveAsync(Block block, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Block block, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Block?> FindAsync(Handle blocker, Handle blocked, CancellationToken ct = default) =>
            Task.FromResult(_blocks.FirstOrDefault(b => b.BlockerHandle == blocker && b.BlockedHandle == blocked));
        public Task<IReadOnlyList<Handle>> GetBlockedHandlesAsync(Handle blocker, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Handle>>(_blocks.Where(b => b.BlockerHandle == blocker).Select(b => b.BlockedHandle).ToList());
        public Task<IReadOnlyList<Handle>> GetBlockerHandlesAsync(Handle blocked, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Handle>>(_blocks.Where(b => b.BlockedHandle == blocked).Select(b => b.BlockerHandle).ToList());
        public Task<bool> IsBlockedAsync(Handle blocker, Handle blocked, CancellationToken ct = default) =>
            Task.FromResult(_blocks.Any(b => b.BlockerHandle == blocker && b.BlockedHandle == blocked));
    }

    private sealed class NoOpDomainEventDispatcher : IDomainEventDispatcher
    {
        public Task DispatchAsync(IReadOnlyList<IDomainEvent> domainEvents, CancellationToken ct = default) =>
            Task.CompletedTask;
    }
}

using SocialDDD.Application.Interfaces;
using SocialDDD.Domain.Blocks;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Follows;
using SocialDDD.Domain.Users;

namespace SocialDDD.Application.Follows;

public sealed class FollowService(
    IFollowRepository followRepository,
    IBlockRepository blockRepository,
    IDomainEventDispatcher eventDispatcher)
{
    public async Task FollowAsync(Handle follower, Handle followed, CancellationToken ct = default)
    {
        if (await blockRepository.IsBlockedAsync(follower, followed, ct)
            || await blockRepository.IsBlockedAsync(followed, follower, ct))
            throw new DomainException("Cannot follow a user when either user has blocked the other.");

        var existing = await followRepository.FindAsync(follower, followed, ct);
        if (existing is not null) return;

        var follow = Follow.Create(follower, followed);
        await followRepository.SaveAsync(follow, ct);
        await eventDispatcher.DispatchAsync(follow.PopDomainEvents(), ct);
    }

    public async Task UnfollowAsync(Handle follower, Handle followed, CancellationToken ct = default)
    {
        var existing = await followRepository.FindAsync(follower, followed, ct);
        if (existing is null) return;

        await followRepository.DeleteAsync(existing, ct);
    }
}

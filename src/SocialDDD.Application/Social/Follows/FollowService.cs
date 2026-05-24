using SocialDDD.Domain.Social.Profiles;
using SocialDDD.Application.Interfaces;
using SocialDDD.Domain.Social.Follows;
using SocialDDD.Domain.Identity.Users;

namespace SocialDDD.Application.Social.Follows;

public sealed class FollowService(
    IFollowRepository followRepository,
    FollowDomainService followDomainService,
    IDomainEventDispatcher eventDispatcher)
{
    public async Task FollowAsync(Handle follower, Handle followed, CancellationToken ct = default)
    {
        await followDomainService.EnsureCanFollowAsync(follower, followed, ct);

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

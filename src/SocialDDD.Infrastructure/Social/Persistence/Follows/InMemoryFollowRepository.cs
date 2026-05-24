using SocialDDD.Domain.Social.Profiles;
using SocialDDD.Domain.Social.Follows;
using SocialDDD.Domain.Identity.Users;

namespace SocialDDD.Infrastructure.Social.Persistence.Follows;

internal sealed class InMemoryFollowRepository : IFollowRepository
{
    private readonly List<Follow> _follows = [];
    private readonly object _gate = new();

    public Task SaveAsync(Follow follow, CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (!_follows.Any(f => f.FollowerHandle == follow.FollowerHandle && f.FollowedHandle == follow.FollowedHandle))
                _follows.Add(follow);
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(Follow follow, CancellationToken ct = default)
    {
        lock (_gate)
        {
            _follows.RemoveAll(f => f.FollowerHandle == follow.FollowerHandle && f.FollowedHandle == follow.FollowedHandle);
        }

        return Task.CompletedTask;
    }

    public Task<Follow?> FindAsync(Handle follower, Handle followed, CancellationToken ct = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_follows.FirstOrDefault(f => f.FollowerHandle == follower && f.FollowedHandle == followed));
        }
    }

    public Task<bool> IsFollowingAsync(Handle follower, Handle followed, CancellationToken ct = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_follows.Any(f => f.FollowerHandle == follower && f.FollowedHandle == followed));
        }
    }

    public Task<IReadOnlyList<Handle>> GetFollowedHandlesAsync(Handle follower, CancellationToken ct = default)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<Handle>>(
                _follows.Where(f => f.FollowerHandle == follower).Select(f => f.FollowedHandle).ToList());
        }
    }

    public Task<int> CountFollowersAsync(Handle followed, CancellationToken ct = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_follows.Count(f => f.FollowedHandle == followed));
        }
    }

    public Task<int> CountFollowingAsync(Handle follower, CancellationToken ct = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_follows.Count(f => f.FollowerHandle == follower));
        }
    }
}

using SocialDDD.Domain.Social.Profiles;
using SocialDDD.Domain.Identity.Users;

namespace SocialDDD.Domain.Social.Follows;

public interface IFollowRepository
{
    Task SaveAsync(Follow follow, CancellationToken ct = default);
    Task DeleteAsync(Follow follow, CancellationToken ct = default);
    Task<Follow?> FindAsync(Handle follower, Handle followed, CancellationToken ct = default);
    Task<bool> IsFollowingAsync(Handle follower, Handle followed, CancellationToken ct = default);
    Task<IReadOnlyList<Handle>> GetFollowedHandlesAsync(Handle follower, CancellationToken ct = default);
    Task<int> CountFollowersAsync(Handle followed, CancellationToken ct = default);
    Task<int> CountFollowingAsync(Handle follower, CancellationToken ct = default);
}

using SocialDDD.Infrastructure.Persistence;
using SocialDDD.Domain.Social.Profiles;
using MongoDB.Driver;
using SocialDDD.Domain.Social.Follows;
using SocialDDD.Domain.Identity.Users;

namespace SocialDDD.Infrastructure.Social.Persistence.Follows;

internal sealed class MongoDbFollowRepository(MongoDbContext context) : IFollowRepository
{
    public Task SaveAsync(Follow follow, CancellationToken ct = default) =>
        context.Follows.ReplaceOneAsync(
            f => f.FollowerHandle == follow.FollowerHandle && f.FollowedHandle == follow.FollowedHandle,
            follow,
            new ReplaceOptions { IsUpsert = true },
            ct);

    public Task DeleteAsync(Follow follow, CancellationToken ct = default) =>
        context.Follows.DeleteOneAsync(
            f => f.FollowerHandle == follow.FollowerHandle && f.FollowedHandle == follow.FollowedHandle,
            ct);

    public async Task<Follow?> FindAsync(Handle follower, Handle followed, CancellationToken ct = default) =>
        await context.Follows
            .Find(f => f.FollowerHandle == follower && f.FollowedHandle == followed)
            .FirstOrDefaultAsync(ct);

    public async Task<bool> IsFollowingAsync(Handle follower, Handle followed, CancellationToken ct = default) =>
        await context.Follows.CountDocumentsAsync(
            f => f.FollowerHandle == follower && f.FollowedHandle == followed,
            cancellationToken: ct) > 0;

    public async Task<IReadOnlyList<Handle>> GetFollowedHandlesAsync(Handle follower, CancellationToken ct = default) =>
        await context.Follows
            .Find(f => f.FollowerHandle == follower)
            .Project(f => f.FollowedHandle)
            .ToListAsync(ct);

    public async Task<int> CountFollowersAsync(Handle followed, CancellationToken ct = default) =>
        (int)await context.Follows.CountDocumentsAsync(f => f.FollowedHandle == followed, cancellationToken: ct);

    public async Task<int> CountFollowingAsync(Handle follower, CancellationToken ct = default) =>
        (int)await context.Follows.CountDocumentsAsync(f => f.FollowerHandle == follower, cancellationToken: ct);
}

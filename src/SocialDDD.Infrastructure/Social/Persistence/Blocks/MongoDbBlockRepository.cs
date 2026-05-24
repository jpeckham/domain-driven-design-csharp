using SocialDDD.Infrastructure.Persistence;
using SocialDDD.Domain.Social.Profiles;
using MongoDB.Driver;
using SocialDDD.Domain.Social.Blocks;
using SocialDDD.Domain.Identity.Users;

namespace SocialDDD.Infrastructure.Social.Persistence.Blocks;

internal sealed class MongoDbBlockRepository(MongoDbContext context) : IBlockRepository
{
    public Task SaveAsync(Block block, CancellationToken ct = default) =>
        context.Blocks.ReplaceOneAsync(
            b => b.BlockerHandle == block.BlockerHandle && b.BlockedHandle == block.BlockedHandle,
            block,
            new ReplaceOptions { IsUpsert = true },
            ct);

    public Task DeleteAsync(Block block, CancellationToken ct = default) =>
        context.Blocks.DeleteOneAsync(
            b => b.BlockerHandle == block.BlockerHandle && b.BlockedHandle == block.BlockedHandle,
            ct);

    public async Task<Block?> FindAsync(Handle blocker, Handle blocked, CancellationToken ct = default) =>
        await context.Blocks
            .Find(b => b.BlockerHandle == blocker && b.BlockedHandle == blocked)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<Handle>> GetBlockedHandlesAsync(Handle blocker, CancellationToken ct = default)
    {
        var blocks = await context.Blocks
            .Find(b => b.BlockerHandle == blocker)
            .ToListAsync(ct);

        return blocks.Select(b => b.BlockedHandle).ToList();
    }

    public async Task<IReadOnlyList<Handle>> GetBlockerHandlesAsync(Handle blocked, CancellationToken ct = default)
    {
        var blocks = await context.Blocks
            .Find(b => b.BlockedHandle == blocked)
            .ToListAsync(ct);

        return blocks.Select(b => b.BlockerHandle).ToList();
    }

    public async Task<bool> IsBlockedAsync(Handle blocker, Handle blocked, CancellationToken ct = default) =>
        await context.Blocks.CountDocumentsAsync(
            b => b.BlockerHandle == blocker && b.BlockedHandle == blocked,
            cancellationToken: ct) > 0;
}

using SocialDDD.Domain.Users;

namespace SocialDDD.Domain.Blocks;

public interface IBlockRepository
{
    Task SaveAsync(Block block, CancellationToken ct = default);
    Task DeleteAsync(Block block, CancellationToken ct = default);
    Task<Block?> FindAsync(Handle blocker, Handle blocked, CancellationToken ct = default);
    Task<IReadOnlyList<Handle>> GetBlockedHandlesAsync(Handle blocker, CancellationToken ct = default);
    Task<IReadOnlyList<Handle>> GetBlockerHandlesAsync(Handle blocked, CancellationToken ct = default);
    Task<bool> IsBlockedAsync(Handle blocker, Handle blocked, CancellationToken ct = default);
}

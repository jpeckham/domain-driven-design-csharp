using SocialDDD.Domain.Blocks;
using SocialDDD.Domain.Users;

namespace SocialDDD.Infrastructure.Persistence.Blocks;

internal sealed class InMemoryBlockRepository : IBlockRepository
{
    private readonly List<Block> _blocks = [];
    private readonly object _gate = new();

    public Task SaveAsync(Block block, CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (!_blocks.Any(b => b.BlockerHandle == block.BlockerHandle && b.BlockedHandle == block.BlockedHandle))
                _blocks.Add(block);
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(Block block, CancellationToken ct = default)
    {
        lock (_gate)
        {
            _blocks.RemoveAll(b => b.BlockerHandle == block.BlockerHandle && b.BlockedHandle == block.BlockedHandle);
        }

        return Task.CompletedTask;
    }

    public Task<Block?> FindAsync(Handle blocker, Handle blocked, CancellationToken ct = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_blocks.FirstOrDefault(b => b.BlockerHandle == blocker && b.BlockedHandle == blocked));
        }
    }

    public Task<IReadOnlyList<Handle>> GetBlockedHandlesAsync(Handle blocker, CancellationToken ct = default)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<Handle>>(
                _blocks.Where(b => b.BlockerHandle == blocker).Select(b => b.BlockedHandle).ToList());
        }
    }

    public Task<IReadOnlyList<Handle>> GetBlockerHandlesAsync(Handle blocked, CancellationToken ct = default)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<Handle>>(
                _blocks.Where(b => b.BlockedHandle == blocked).Select(b => b.BlockerHandle).ToList());
        }
    }

    public Task<bool> IsBlockedAsync(Handle blocker, Handle blocked, CancellationToken ct = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_blocks.Any(b => b.BlockerHandle == blocker && b.BlockedHandle == blocked));
        }
    }
}

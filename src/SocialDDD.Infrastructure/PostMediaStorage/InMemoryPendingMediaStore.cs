using System.Collections.Concurrent;
using SocialDDD.Application.Posts;
using SocialDDD.Domain.Posts;

namespace SocialDDD.Infrastructure.PostMediaStorage;

public sealed class InMemoryPendingMediaStore : IPendingMediaStore
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(1);

    private readonly ConcurrentDictionary<Guid, DateTime> _reserved = new();
    private readonly ConcurrentDictionary<Guid, (PostMedia Media, DateTime Expiry)> _completed = new();

    public void Reserve(Guid assetId)
        => _reserved[assetId] = DateTime.UtcNow.Add(Ttl);

    public bool IsReserved(Guid assetId)
        => _reserved.TryGetValue(assetId, out var expiry) && expiry > DateTime.UtcNow;

    public void Complete(Guid assetId, PostMedia media)
    {
        _reserved.TryRemove(assetId, out _);
        _completed[assetId] = (media, DateTime.UtcNow.Add(Ttl));
    }

    public bool TryGetCompleted(Guid assetId, out PostMedia? media)
    {
        if (_completed.TryGetValue(assetId, out var entry) && entry.Expiry > DateTime.UtcNow)
        {
            media = entry.Media;
            return true;
        }
        media = null;
        return false;
    }
}

using System.Collections.Concurrent;
using SocialDDD.Domain.Users;

namespace SocialDDD.Infrastructure.Persistence.PasswordResetTokens;

internal sealed class InMemoryPasswordResetTokenRepository : IPasswordResetTokenRepository
{
    // Keyed by userId string
    private readonly ConcurrentDictionary<string, (string userId, PasswordResetToken token)> _store = new();

    public Task SaveAsync(UserId userId, PasswordResetToken token, CancellationToken ct = default)
    {
        var key = userId.Value.ToString();
        _store[key] = (key, token);
        return Task.CompletedTask;
    }

    public Task<(UserId UserId, PasswordResetToken Token)?> FindByTokenAsync(string token, CancellationToken ct = default)
    {
        foreach (var (_, entry) in _store)
        {
            if (entry.token.Token == token)
            {
                var userId = UserId.From(Guid.Parse(entry.userId));
                return Task.FromResult<(UserId, PasswordResetToken)?>((userId, entry.token));
            }
        }
        return Task.FromResult<(UserId, PasswordResetToken)?>(null);
    }

    public Task DeleteByUserIdAsync(UserId userId, CancellationToken ct = default)
    {
        _store.TryRemove(userId.Value.ToString(), out _);
        return Task.CompletedTask;
    }
}

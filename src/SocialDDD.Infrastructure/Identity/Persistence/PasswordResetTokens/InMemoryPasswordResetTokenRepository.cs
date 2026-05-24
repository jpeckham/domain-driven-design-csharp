using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using SocialDDD.Domain.Identity.Users;

namespace SocialDDD.Infrastructure.Identity.Persistence.PasswordResetTokens;

internal sealed class InMemoryPasswordResetTokenRepository : IPasswordResetTokenRepository
{
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
            var storedBytes = Encoding.UTF8.GetBytes(entry.token.Token);
            var providedBytes = Encoding.UTF8.GetBytes(token);
            if (CryptographicOperations.FixedTimeEquals(storedBytes, providedBytes))
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

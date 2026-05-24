using System.Collections.Concurrent;
using SocialDDD.Domain.Identity.Users;

namespace SocialDDD.Infrastructure.Identity.Persistence.VerificationCodes;

internal sealed class InMemoryVerificationCodeRepository : IVerificationCodeRepository
{
    private readonly ConcurrentDictionary<string, VerificationCode> _store = new();

    public Task SaveAsync(UserId userId, VerificationCode code, CancellationToken ct = default)
    {
        _store[userId.Value.ToString()] = code;
        return Task.CompletedTask;
    }

    public Task<VerificationCode?> FindByUserIdAsync(UserId userId, CancellationToken ct = default)
    {
        _store.TryGetValue(userId.Value.ToString(), out var code);
        return Task.FromResult(code);
    }

    public Task DeleteAsync(UserId userId, CancellationToken ct = default)
    {
        _store.TryRemove(userId.Value.ToString(), out _);
        return Task.CompletedTask;
    }
}

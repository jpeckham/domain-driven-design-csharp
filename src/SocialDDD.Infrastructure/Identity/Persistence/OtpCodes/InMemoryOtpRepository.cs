using System.Collections.Concurrent;
using SocialDDD.Domain.Identity.Users;

namespace SocialDDD.Infrastructure.Identity.Persistence.OtpCodes;

internal sealed class InMemoryOtpRepository : IOtpRepository
{
    private readonly ConcurrentDictionary<string, OneTimePasscode> _store = new();

    private static string Key(UserId userId, DeviceId deviceId) =>
        $"{userId.Value}:{deviceId.Value}";

    public Task SaveAsync(UserId userId, DeviceId deviceId, OneTimePasscode otp, CancellationToken ct = default)
    {
        _store[Key(userId, deviceId)] = otp;
        return Task.CompletedTask;
    }

    public Task<OneTimePasscode?> FindAsync(UserId userId, DeviceId deviceId, CancellationToken ct = default)
    {
        _store.TryGetValue(Key(userId, deviceId), out var otp);
        return Task.FromResult(otp);
    }

    public Task DeleteAsync(UserId userId, DeviceId deviceId, CancellationToken ct = default)
    {
        _store.TryRemove(Key(userId, deviceId), out _);
        return Task.CompletedTask;
    }
}

using System.Collections.Concurrent;
using SocialDDD.Domain.Users;

namespace SocialDDD.Infrastructure.Persistence.RememberedDevices;

internal sealed class InMemoryRememberedDeviceRepository : IRememberedDeviceRepository
{
    private static readonly ConcurrentDictionary<string, byte> _store = new();

    private static string Key(UserId userId, DeviceId deviceId) =>
        $"{userId.Value}:{deviceId.Value}";

    public Task<bool> IsRememberedAsync(UserId userId, DeviceId deviceId, CancellationToken ct = default)
        => Task.FromResult(_store.ContainsKey(Key(userId, deviceId)));

    public Task RememberAsync(UserId userId, DeviceId deviceId, CancellationToken ct = default)
    {
        _store.TryAdd(Key(userId, deviceId), 0);
        return Task.CompletedTask;
    }
}

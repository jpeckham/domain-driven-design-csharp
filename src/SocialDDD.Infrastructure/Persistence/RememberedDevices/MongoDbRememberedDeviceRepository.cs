using MongoDB.Driver;
using SocialDDD.Domain.Users;

namespace SocialDDD.Infrastructure.Persistence.RememberedDevices;

internal sealed class MongoDbRememberedDeviceRepository(MongoDbContext context) : IRememberedDeviceRepository
{
    public async Task<bool> IsRememberedAsync(UserId userId, DeviceId deviceId, CancellationToken ct = default)
    {
        var doc = await context.RememberedDevices
            .Find(d => d.UserId == userId.Value.ToString() && d.DeviceId == deviceId.Value)
            .FirstOrDefaultAsync(ct);
        return doc is not null;
    }

    public Task RememberAsync(UserId userId, DeviceId deviceId, CancellationToken ct = default)
    {
        var doc = new RememberedDeviceDocument(userId.Value.ToString(), deviceId.Value);
        return context.RememberedDevices.ReplaceOneAsync(
            d => d.UserId == doc.UserId && d.DeviceId == doc.DeviceId,
            doc,
            new ReplaceOptions { IsUpsert = true },
            ct);
    }
}

internal sealed class RememberedDeviceDocument(string userId, string deviceId)
{
    public string UserId { get; init; } = userId;
    public string DeviceId { get; init; } = deviceId;
}

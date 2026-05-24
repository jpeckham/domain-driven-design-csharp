using SocialDDD.Infrastructure.Persistence;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using SocialDDD.Domain.Identity.Users;

namespace SocialDDD.Infrastructure.Identity.Persistence.RememberedDevices;

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
        var userKey = userId.Value.ToString();
        var deviceKey = deviceId.Value;
        var update = Builders<RememberedDeviceDocument>.Update
            .Set(d => d.UserId, userKey)
            .Set(d => d.DeviceId, deviceKey);

        return context.RememberedDevices.UpdateOneAsync(
            d => d.UserId == userKey && d.DeviceId == deviceKey,
            update,
            new UpdateOptions { IsUpsert = true },
            ct);
    }
}

internal sealed class RememberedDeviceDocument(string userId, string deviceId)
{
    [BsonId]
    public ObjectId Id { get; init; } = ObjectId.GenerateNewId();
    public string UserId { get; init; } = userId;
    public string DeviceId { get; init; } = deviceId;
}

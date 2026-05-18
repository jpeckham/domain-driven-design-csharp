using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using SocialDDD.Domain.Users;

namespace SocialDDD.Infrastructure.Persistence.OtpCodes;

internal sealed class MongoDbOtpRepository(MongoDbContext context) : IOtpRepository
{
    public Task SaveAsync(UserId userId, DeviceId deviceId, OneTimePasscode otp, CancellationToken ct = default)
    {
        var doc = new OtpDocument(userId.Value.ToString(), deviceId.Value, otp.Code, otp.ExpiresAt.UtcDateTime);
        return context.DeviceOtps.ReplaceOneAsync(
            d => d.UserId == doc.UserId && d.DeviceId == doc.DeviceId,
            doc,
            new ReplaceOptions { IsUpsert = true },
            ct);
    }

    public async Task<OneTimePasscode?> FindAsync(UserId userId, DeviceId deviceId, CancellationToken ct = default)
    {
        var userKey = userId.Value.ToString();
        var deviceKey = deviceId.Value;
        var doc = await context.DeviceOtps
            .Find(d => d.UserId == userKey && d.DeviceId == deviceKey)
            .FirstOrDefaultAsync(ct);

        if (doc is null) return null;
        return new OneTimePasscode(doc.Code, new DateTimeOffset(doc.ExpiresAt, TimeSpan.Zero));
    }

    public Task DeleteAsync(UserId userId, DeviceId deviceId, CancellationToken ct = default)
    {
        var userKey = userId.Value.ToString();
        var deviceKey = deviceId.Value;
        return context.DeviceOtps.DeleteOneAsync(
            d => d.UserId == userKey && d.DeviceId == deviceKey, ct);
    }
}

internal sealed class OtpDocument(string userId, string deviceId, string code, DateTime expiresAt)
{
    [BsonId]
    public ObjectId Id { get; init; } = ObjectId.GenerateNewId();
    public string UserId { get; init; } = userId;
    public string DeviceId { get; init; } = deviceId;
    public string Code { get; init; } = code;
    public DateTime ExpiresAt { get; init; } = expiresAt;
}

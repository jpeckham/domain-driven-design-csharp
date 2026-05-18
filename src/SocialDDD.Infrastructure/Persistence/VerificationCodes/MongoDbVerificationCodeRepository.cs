using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using SocialDDD.Domain.Users;

namespace SocialDDD.Infrastructure.Persistence.VerificationCodes;

internal sealed class MongoDbVerificationCodeRepository(MongoDbContext context) : IVerificationCodeRepository
{
    public Task SaveAsync(UserId userId, VerificationCode code, CancellationToken ct = default)
    {
        var doc = new VerificationCodeDocument(userId.Value.ToString(), code.Code, code.ExpiresAt.UtcDateTime);
        return context.VerificationCodes.ReplaceOneAsync(
            d => d.UserId == doc.UserId,
            doc,
            new ReplaceOptions { IsUpsert = true },
            ct);
    }

    public async Task<VerificationCode?> FindByUserIdAsync(UserId userId, CancellationToken ct = default)
    {
        var key = userId.Value.ToString();
        var doc = await context.VerificationCodes
            .Find(d => d.UserId == key)
            .FirstOrDefaultAsync(ct);

        if (doc is null) return null;
        return new VerificationCode(doc.Code, new DateTimeOffset(doc.ExpiresAt, TimeSpan.Zero));
    }

    public Task DeleteAsync(UserId userId, CancellationToken ct = default)
    {
        var key = userId.Value.ToString();
        return context.VerificationCodes.DeleteOneAsync(d => d.UserId == key, ct);
    }
}

internal sealed class VerificationCodeDocument(string userId, string code, DateTime expiresAt)
{
    [BsonId]
    public ObjectId Id { get; init; } = ObjectId.GenerateNewId();
    public string UserId { get; init; } = userId;
    public string Code { get; init; } = code;
    public DateTime ExpiresAt { get; init; } = expiresAt;
}

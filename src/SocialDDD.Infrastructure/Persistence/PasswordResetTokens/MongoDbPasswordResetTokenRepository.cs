using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using SocialDDD.Domain.Users;

namespace SocialDDD.Infrastructure.Persistence.PasswordResetTokens;

internal sealed class MongoDbPasswordResetTokenRepository(MongoDbContext context) : IPasswordResetTokenRepository
{
    public Task SaveAsync(UserId userId, PasswordResetToken token, CancellationToken ct = default)
    {
        var doc = new PasswordResetTokenDocument(userId.Value.ToString(), token.Token, token.ExpiresAt.UtcDateTime);
        return context.PasswordResetTokens.ReplaceOneAsync(
            d => d.UserId == doc.UserId,
            doc,
            new ReplaceOptions { IsUpsert = true },
            ct);
    }

    public async Task<(UserId UserId, PasswordResetToken Token)?> FindByTokenAsync(string token, CancellationToken ct = default)
    {
        var doc = await context.PasswordResetTokens
            .Find(d => d.Token == token)
            .FirstOrDefaultAsync(ct);

        if (doc is null) return null;

        var userId = UserId.From(Guid.Parse(doc.UserId));
        var resetToken = new PasswordResetToken(doc.Token, new DateTimeOffset(doc.ExpiresAt, TimeSpan.Zero));
        return (userId, resetToken);
    }

    public Task DeleteByUserIdAsync(UserId userId, CancellationToken ct = default)
    {
        var key = userId.Value.ToString();
        return context.PasswordResetTokens.DeleteOneAsync(d => d.UserId == key, ct);
    }
}

internal sealed class PasswordResetTokenDocument(string userId, string token, DateTime expiresAt)
{
    [BsonId]
    public ObjectId Id { get; init; } = ObjectId.GenerateNewId();
    public string UserId { get; init; } = userId;
    public string Token { get; init; } = token;
    public DateTime ExpiresAt { get; init; } = expiresAt;
}

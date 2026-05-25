using SocialDDD.Domain.Social.Profiles;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using SocialDDD.Domain.Social.Blocks;
using SocialDDD.Domain.Social.Follows;
using SocialDDD.Domain.Social.Posts;
using SocialDDD.Domain.Identity.Users;
using SocialDDD.Infrastructure.Persistence.Mapping;
using SocialDDD.Infrastructure.Identity.Persistence.OtpCodes;
using SocialDDD.Infrastructure.Identity.Persistence.PasswordResetTokens;
using SocialDDD.Infrastructure.Identity.Persistence.RememberedDevices;
using SocialDDD.Infrastructure.Identity.Persistence.VerificationCodes;

namespace SocialDDD.Infrastructure.Persistence;

public sealed class MongoDbContext
{
    private readonly IMongoDatabase _database;

    public MongoDbContext(IOptions<MongoSettings> options)
    {
        BsonMappings.Register();

        var client = new MongoClient(options.Value.ConnectionString);
        _database = client.GetDatabase(options.Value.DatabaseName);

        EnsureIndexes();
    }

    public IMongoCollection<User> Users => _database.GetCollection<User>("users");
    public IMongoCollection<Post> Posts => _database.GetCollection<Post>("posts");
    internal IMongoCollection<Follow> Follows => _database.GetCollection<Follow>("follows");
    internal IMongoCollection<Block> Blocks => _database.GetCollection<Block>("blocks");
    internal IMongoCollection<VerificationCodeDocument> VerificationCodes =>
        _database.GetCollection<VerificationCodeDocument>("verification_codes");
    internal IMongoCollection<RememberedDeviceDocument> RememberedDevices =>
        _database.GetCollection<RememberedDeviceDocument>("remembered_devices");
    internal IMongoCollection<OtpDocument> DeviceOtps =>
        _database.GetCollection<OtpDocument>("device_otps");
    internal IMongoCollection<PasswordResetTokenDocument> PasswordResetTokens =>
        _database.GetCollection<PasswordResetTokenDocument>("password_reset_tokens");

    private void EnsureIndexes()
    {
        var handleIndex = new CreateIndexModel<User>(
            Builders<User>.IndexKeys.Ascending("handle"),
            new CreateIndexOptions { Background = true, Name = "handle_asc" });

        Users.Indexes.CreateOne(handleIndex);

        var blockPairIndex = new CreateIndexModel<Block>(
            Builders<Block>.IndexKeys
                .Ascending("blockerHandle")
                .Ascending("blockedHandle"),
            new CreateIndexOptions { Background = true, Name = "blockerHandle_blockedHandle_asc" });

        Blocks.Indexes.CreateOne(blockPairIndex);

        var blockedHandleIndex = new CreateIndexModel<Block>(
            Builders<Block>.IndexKeys.Ascending("blockedHandle"),
            new CreateIndexOptions { Background = true, Name = "blockedHandle_asc" });

        Blocks.Indexes.CreateOne(blockedHandleIndex);

        var followPairIndex = new CreateIndexModel<Follow>(
            Builders<Follow>.IndexKeys
                .Ascending("followerHandle")
                .Ascending("followedHandle"),
            new CreateIndexOptions { Background = true, Name = "followerHandle_followedHandle_asc" });

        Follows.Indexes.CreateOne(followPairIndex);

        var followedHandleIndex = new CreateIndexModel<Follow>(
            Builders<Follow>.IndexKeys.Ascending("followedHandle"),
            new CreateIndexOptions { Background = true, Name = "followedHandle_asc" });

        Follows.Indexes.CreateOne(followedHandleIndex);

        var ttlIndex = new CreateIndexModel<VerificationCodeDocument>(
            Builders<VerificationCodeDocument>.IndexKeys.Ascending(d => d.ExpiresAt),
            new CreateIndexOptions { ExpireAfter = TimeSpan.Zero, Name = "expiresAt_ttl" });

        VerificationCodes.Indexes.CreateOne(ttlIndex);

        var rememberedDeviceIndex = new CreateIndexModel<RememberedDeviceDocument>(
            Builders<RememberedDeviceDocument>.IndexKeys
                .Ascending(d => d.UserId)
                .Ascending(d => d.DeviceId),
            new CreateIndexOptions { Background = true, Name = "userId_deviceId_asc" });

        RememberedDevices.Indexes.CreateOne(rememberedDeviceIndex);

        var otpTtlIndex = new CreateIndexModel<OtpDocument>(
            Builders<OtpDocument>.IndexKeys.Ascending(d => d.ExpiresAt),
            new CreateIndexOptions { ExpireAfter = TimeSpan.Zero, Name = "otp_expiresAt_ttl" });

        DeviceOtps.Indexes.CreateOne(otpTtlIndex);

        var passwordResetTtlIndex = new CreateIndexModel<PasswordResetTokenDocument>(
            Builders<PasswordResetTokenDocument>.IndexKeys.Ascending(d => d.ExpiresAt),
            new CreateIndexOptions { ExpireAfter = TimeSpan.Zero, Name = "passwordReset_expiresAt_ttl" });

        PasswordResetTokens.Indexes.CreateOne(passwordResetTtlIndex);

        var postLikedByIndex = new CreateIndexModel<Post>(
            Builders<Post>.IndexKeys.Ascending("likedBy"),
            new CreateIndexOptions { Background = true, Name = "likedBy_asc" });

        Posts.Indexes.CreateOne(postLikedByIndex);

        var postParentIdIndex = new CreateIndexModel<Post>(
            Builders<Post>.IndexKeys.Ascending("parentPostId"),
            new CreateIndexOptions { Background = true, Name = "parentPostId_asc" });

        Posts.Indexes.CreateOne(postParentIdIndex);

        var postRepostIndex = new CreateIndexModel<Post>(
            Builders<Post>.IndexKeys
                .Ascending("originalPostId")
                .Ascending(p => p.AuthorId),
            new CreateIndexOptions { Background = true, Name = "originalPostId_authorId_asc" });

        Posts.Indexes.CreateOne(postRepostIndex);

        var postHashtagsIndex = new CreateIndexModel<Post>(
            Builders<Post>.IndexKeys.Ascending("hashtags"),
            new CreateIndexOptions { Background = true, Name = "hashtags_asc" });

        Posts.Indexes.CreateOne(postHashtagsIndex);
    }
}

using Microsoft.Extensions.Options;
using MongoDB.Driver;
using SocialDDD.Domain.Posts;
using SocialDDD.Domain.Users;
using SocialDDD.Infrastructure.Persistence.Mapping;
using SocialDDD.Infrastructure.Persistence.OtpCodes;
using SocialDDD.Infrastructure.Persistence.PasswordResetTokens;
using SocialDDD.Infrastructure.Persistence.RememberedDevices;
using SocialDDD.Infrastructure.Persistence.VerificationCodes;

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
            new CreateIndexOptions { Unique = true, Background = true, Name = "handle_unique" });

        Users.Indexes.CreateOne(handleIndex);

        var ttlIndex = new CreateIndexModel<VerificationCodeDocument>(
            Builders<VerificationCodeDocument>.IndexKeys.Ascending(d => d.ExpiresAt),
            new CreateIndexOptions { ExpireAfter = TimeSpan.Zero, Name = "expiresAt_ttl" });

        VerificationCodes.Indexes.CreateOne(ttlIndex);

        var rememberedDeviceIndex = new CreateIndexModel<RememberedDeviceDocument>(
            Builders<RememberedDeviceDocument>.IndexKeys
                .Ascending(d => d.UserId)
                .Ascending(d => d.DeviceId),
            new CreateIndexOptions { Unique = true, Background = true, Name = "userId_deviceId_unique" });

        RememberedDevices.Indexes.CreateOne(rememberedDeviceIndex);

        var otpTtlIndex = new CreateIndexModel<OtpDocument>(
            Builders<OtpDocument>.IndexKeys.Ascending(d => d.ExpiresAt),
            new CreateIndexOptions { ExpireAfter = TimeSpan.Zero, Name = "otp_expiresAt_ttl" });

        DeviceOtps.Indexes.CreateOne(otpTtlIndex);

        var passwordResetTtlIndex = new CreateIndexModel<PasswordResetTokenDocument>(
            Builders<PasswordResetTokenDocument>.IndexKeys.Ascending(d => d.ExpiresAt),
            new CreateIndexOptions { ExpireAfter = TimeSpan.Zero, Name = "passwordReset_expiresAt_ttl" });

        PasswordResetTokens.Indexes.CreateOne(passwordResetTtlIndex);
    }
}

using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using SocialDDD.Domain.Users;
using SocialDDD.Infrastructure;
using Testcontainers.MongoDb;

namespace SocialDDD.Domain.Tests;

public sealed class MongoUserSecretRepositoryTests : IAsyncLifetime
{
    private readonly MongoDbContainer _mongo = new MongoDbBuilder("mongo:7.0")
        .Build();

    private ServiceProvider _provider = null!;
    private string _databaseName = null!;

    public async Task InitializeAsync()
    {
        await _mongo.StartAsync();
        _databaseName = $"socialddd_secret_tests_{Guid.NewGuid():N}";

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mongo:ConnectionString"] = _mongo.GetConnectionString(),
                ["Mongo:DatabaseName"] = _databaseName,
                ["Features:EmailVerificationRepository"] = "MongoDb",
                ["Features:OtpRepository"] = "MongoDb",
                ["Features:RememberedDeviceRepository"] = "MongoDb",
                ["Features:PasswordResetTokenRepository"] = "MongoDb"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);
        _provider = services.BuildServiceProvider();
    }

    public async Task DisposeAsync()
    {
        if (_provider is not null)
            await _provider.DisposeAsync();

        if (_databaseName is not null)
        {
            var client = new MongoClient(_mongo.GetConnectionString());
            await client.DropDatabaseAsync(_databaseName);
        }

        await _mongo.DisposeAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task OtpRepository_SaveAsync_CanReplaceExistingOtpForSameUserDevice()
    {
        var repository = _provider.GetRequiredService<IOtpRepository>();
        var userId = UserId.New();
        var deviceId = DeviceId.New();

        await repository.SaveAsync(userId, deviceId, new OneTimePasscode("111111", DateTimeOffset.UtcNow.AddMinutes(10)));
        await repository.SaveAsync(userId, deviceId, new OneTimePasscode("222222", DateTimeOffset.UtcNow.AddMinutes(10)));

        var stored = await repository.FindAsync(userId, deviceId);
        stored.Should().NotBeNull();
        stored!.Code.Should().Be("222222");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task VerificationCodeRepository_SaveAsync_CanReplaceExistingCodeForSameUser()
    {
        var repository = _provider.GetRequiredService<IVerificationCodeRepository>();
        var userId = UserId.New();

        await repository.SaveAsync(userId, new VerificationCode("111111", DateTimeOffset.UtcNow.AddMinutes(15)));
        await repository.SaveAsync(userId, new VerificationCode("222222", DateTimeOffset.UtcNow.AddMinutes(15)));

        var stored = await repository.FindByUserIdAsync(userId);
        stored.Should().NotBeNull();
        stored!.Code.Should().Be("222222");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task PasswordResetTokenRepository_SaveAsync_CanReplaceExistingTokenForSameUser()
    {
        var repository = _provider.GetRequiredService<IPasswordResetTokenRepository>();
        var userId = UserId.New();

        await repository.SaveAsync(userId, new PasswordResetToken("token-one", DateTimeOffset.UtcNow.AddMinutes(5)));
        await repository.SaveAsync(userId, new PasswordResetToken("token-two", DateTimeOffset.UtcNow.AddMinutes(5)));

        var stored = await repository.FindByTokenAsync("token-two");
        stored.Should().NotBeNull();
        stored!.Value.UserId.Should().Be(userId);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RememberedDeviceRepository_RememberAsync_CanBeCalledTwiceForSameUserDevice()
    {
        var repository = _provider.GetRequiredService<IRememberedDeviceRepository>();
        var userId = UserId.New();
        var deviceId = DeviceId.New();

        await repository.RememberAsync(userId, deviceId);
        await repository.RememberAsync(userId, deviceId);

        var isRemembered = await repository.IsRememberedAsync(userId, deviceId);
        isRemembered.Should().BeTrue();
    }
}

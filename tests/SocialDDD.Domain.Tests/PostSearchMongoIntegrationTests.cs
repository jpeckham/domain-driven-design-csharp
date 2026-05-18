using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using SocialDDD.Domain.Blocks;
using SocialDDD.Domain.Posts;
using SocialDDD.Domain.Users;
using SocialDDD.Infrastructure;
using SocialDDD.Infrastructure.Persistence;
using Testcontainers.MongoDb;

namespace SocialDDD.Domain.Tests;

public sealed class PostSearchMongoIntegrationTests : IAsyncLifetime
{
    private readonly MongoDbContainer _mongo = new MongoDbBuilder("mongo:7.0")
        .Build();

    private ServiceProvider _provider = null!;
    private string _databaseName = null!;

    public async Task InitializeAsync()
    {
        await _mongo.StartAsync();
        _databaseName = $"socialddd_search_tests_{Guid.NewGuid():N}";

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mongo:ConnectionString"] = _mongo.GetConnectionString(),
                ["Mongo:DatabaseName"] = _databaseName
            })
            .Build();

        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);
        _provider = services.BuildServiceProvider();

        _provider.GetRequiredService<MongoDbContext>();
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
    public async Task SearchAsync_TextSearch_ReturnsMatchingPosts()
    {
        var repository = _provider.GetRequiredService<IPostRepository>();
        var matchingAuthor = await AddUserAsync("alice");
        var nonMatchingAuthor = await AddUserAsync("bob");
        var match = Post.Create(matchingAuthor.Id, new PostContent("hello from mongodb search"));
        var other = Post.Create(nonMatchingAuthor.Id, new PostContent("unrelated content"));
        await repository.AddAsync(match);
        await repository.AddAsync(other);

        var results = await repository.SearchAsync(
            "hello",
            requesterHandle: null,
            excludedHandles: new HashSet<Handle>(),
            limit: 20,
            offset: 0);

        results.Should().ContainSingle();
        results[0].Id.Should().Be(match.Id);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SearchAsync_BlockedFirstMatch_DoesNotConsumePageLimit()
    {
        var repository = _provider.GetRequiredService<IPostRepository>();
        var blockRepository = _provider.GetRequiredService<IBlockRepository>();
        var requester = await AddUserAsync("requester");
        var blockedAuthor = await AddUserAsync("blocked_author");
        var visibleAuthor = await AddUserAsync("visible_author");

        var blockedPost = Post.Create(blockedAuthor.Id, new PostContent("hello blocked first"));
        var visiblePost = Post.Create(visibleAuthor.Id, new PostContent("hello visible second"));
        await repository.AddAsync(blockedPost);
        await Task.Delay(5);
        await repository.AddAsync(visiblePost);
        await blockRepository.SaveAsync(Block.Create(requester.Handle, blockedAuthor.Handle));

        var excludedHandles = new HashSet<Handle>(
            await blockRepository.GetBlockedHandlesAsync(requester.Handle));

        var results = await repository.SearchAsync(
            "hello",
            requester.Handle,
            excludedHandles,
            limit: 1,
            offset: 0);

        results.Should().ContainSingle();
        results[0].AuthorId.Should().Be(visibleAuthor.Id);
    }

    private async Task<User> AddUserAsync(string handle)
    {
        var user = User.RegisterImmediate(
            new Username(handle),
            new Email($"{handle}@example.com"),
            new PasswordHash("hash"),
            new Handle(handle),
            new DisplayName(handle));

        await _provider.GetRequiredService<IUserRepository>().AddAsync(user);
        return user;
    }
}
